<script>
    import { onMount } from 'svelte';
    import * as signalR from '@microsoft/signalr';
    import Hologram from './lib/Hologram.svelte';
    import Voice from './lib/Voice.svelte';

    let logs = [];
    let inputText = "";
    let systemState = "STANDBY";
    let telemetry = { cpu: "-- %", ram: "-- MB", time: "--:--:--" };
    let imageViewer = { active: false, url: "" };

    // Hologram State
    let hologramActive = false;
    let hologramLoading = false;
    let hologramText = "";
    let hologramModelUrl = null;

    // Voice State
    let isListening = false;
    let speakText = "";
    let voiceComponent;

    let connection;

    onMount(() => {
        logMessage("A.T.L.A.S. Svelte Core Initialized.");
        startSignalR();
        setInterval(updateTime, 1000);
    });

    function updateTime() {
        telemetry.time = new Date().toLocaleTimeString();
        if (connection && connection.state === "Connected") {
            connection.invoke("RequestTelemetry").catch(e => {});
        }
    }

    function logMessage(msg) {
        const timestamp = `[${new Date().toLocaleTimeString()}]`;
        logs = [...logs, { time: timestamp, text: msg }];
        setTimeout(() => {
            const container = document.getElementById('log-container');
            if(container) container.scrollTop = container.scrollHeight;
        }, 10);
    }

    function startSignalR() {
        connection = new signalR.HubConnectionBuilder()
            .withUrl("/hubs/atlas")
            .withAutomaticReconnect()
            .build();

        connection.on("ReceiveMessage", (message) => {
            logMessage(`<span style="color:#0f0">[A.T.L.A.S]: ${message}</span>`);
            speakText = message;
            setTimeout(() => { speakText = ""; }, 100);
        });

        connection.on("UpdateCoreState", (state) => {
            systemState = state;
        });

        connection.on("ReceiveTelemetry", (cpu, ram) => {
            telemetry.cpu = cpu;
            telemetry.ram = ram;
        });

        connection.on("DisplayImages", (urls) => {
            if (urls && urls.length > 0) {
                imageViewer.url = urls[0];
                imageViewer.active = true;
                setTimeout(() => { imageViewer.active = false; }, 15000);
            }
        });

        connection.on("ActivateHologramMode", () => {
            hologramActive = true;
        });

        connection.on("HologramGenerationStarted", (objectName) => {
            hologramActive = true;
            hologramLoading = true;
            hologramText = `SYNTHESIZING SPATIAL MESH FOR [${objectName.toUpperCase()}]...`;
        });

        connection.on("HologramReady", (url) => {
            hologramLoading = false;
            hologramModelUrl = url;
        });

        connection.on("DeactivateHologramMode", () => {
            hologramActive = false;
            hologramModelUrl = null;
        });

        connection.start()
            .then(() => logMessage("Neural Link Established."))
            .catch(err => logMessage(`<span style="color:#f00">Connection Error: ${err}</span>`));
    }

    function processCommand(textInput = null) {
        const text = (textInput || inputText).trim();
        if (!text) return;

        logMessage(`[USER]: <span style="color:#fff">${text}</span>`);

        // Pass everything to the backend. The LLM Tool Calling handles the intent routing.
        connection.invoke("SendText", text);

        inputText = "";
    }
</script>

<style>
    :global(body) {
        background-color: #050505; color: #00f3ff; font-family: 'Courier New', Courier, monospace; margin: 0; overflow: hidden;
        background: radial-gradient(circle at center, #111 0%, #000 100%);
    }
    .hud-container { text-align: center; position: relative; z-index: 2; padding: 30px; pointer-events: none;}
    .hud-container * { pointer-events: auto; }
    .layout-row { display: flex; justify-content: center; gap: 20px; }
    .log-container { height: 300px; width: 600px; overflow-y: auto; border: 1px solid rgba(0,243,255,0.3); padding: 15px; text-align: left; background: rgba(0,243,255,0.05); }
    .widgets-container { display: flex; flex-direction: column; gap: 10px; width: 250px; }
    .widget { border: 1px solid rgba(0,243,255,0.5); padding: 15px; text-align: left; background: rgba(0,0,0,0.5); }
    .widget h3 { margin: 0 0 10px 0; font-size: 1rem; border-bottom: 1px solid rgba(0,243,255,0.3); padding-bottom: 5px; }
    .input-container { margin-top: 20px; display: flex; justify-content: center; gap: 10px; }
    input, button { background: transparent; border: 1px solid #00f3ff; color: #00f3ff; padding: 10px; font-family: 'Courier New'; }
    input { width: 500px; }
    button:hover { background: #00f3ff; color: #000; cursor: pointer; }
    .btn-active { background: #0f0; color: #000; border-color: #0f0; }
    .image-viewer { position: absolute; top: 20%; right: 5%; width: 300px; border: 2px solid #00f3ff; background: rgba(0,0,0,0.8); padding: 10px; z-index: 100;}
    .image-viewer img { width: 100%; }
</style>

<Voice
    bind:this={voiceComponent}
    bind:isListening
    {speakText}
    on:stateChange={(e) => systemState = e.detail}
    on:command={(e) => processCommand(e.detail)}
/>

<Hologram
    active={hologramActive}
    loading={hologramLoading}
    loadingText={hologramText}
    modelUrl={hologramModelUrl}
    {connection}
/>

<div class="hud-container">
    <div style="margin-bottom: 20px;">
        <h1 style="font-size: 4rem; text-shadow: 0 0 20px #00f3ff; margin:0;">A.T.L.A.S.</h1>
        <p style="font-size: 1.2rem; letter-spacing: 2px;">SYSTEM {systemState}</p>
    </div>

    <div class="layout-row">
        <div id="log-container" class="log-container">
            {#each logs as log}
                <div><span style="color:rgba(255,255,255,0.5)">{log.time}</span> {@html log.text}</div>
            {/each}
        </div>

        <div class="widgets-container">
            <div class="widget"><h3>SYSTEM MEMORY</h3><div>{telemetry.ram}</div></div>
            <div class="widget"><h3>CPU LOAD</h3><div>{telemetry.cpu}</div></div>
            <div class="widget"><h3>TIME</h3><div>{telemetry.time}</div></div>
        </div>

        {#if imageViewer.active}
            <div class="image-viewer">
                <div style="font-size:0.8rem; border-bottom:1px solid #00f3ff; margin-bottom:10px;">VISUAL DATA</div>
                <img src={imageViewer.url} alt="Data" />
            </div>
        {/if}
    </div>

    <div class="input-container">
        <button class:btn-active={isListening} on:click={() => voiceComponent.toggleListening()}>🎤 MIC</button>
        <input type="text" bind:value={inputText} on:keypress={(e) => e.key === 'Enter' && processCommand()} placeholder="Enter command..." />
        <button on:click={() => processCommand()}>EXECUTE</button>
    </div>
</div>
