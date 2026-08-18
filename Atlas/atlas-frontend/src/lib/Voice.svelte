<script>
    import { createEventDispatcher, onMount } from 'svelte';
    const dispatch = createEventDispatcher();

    export let isListening = false;
    export let speakText = "";
    export let atlasState = "STANDBY";

    let recognition;
    let synth = window.speechSynthesis;
    let atlasVoiceInstance = null;
    let explicitlyStopped = true;

    $: if (speakText) {
        speak(speakText);
    }

    onMount(() => {
        if (speechSynthesis.onvoiceschanged !== undefined) {
            speechSynthesis.onvoiceschanged = setAtlasVoice;
        }
        setAtlasVoice();
        initSTT();
    });

    function setAtlasVoice() {
        const voices = synth.getVoices();
        if (voices.length === 0) return;

        const targetVoices = ["Microsoft George", "Google UK English Male", "Microsoft Mark"];
        for (const target of targetVoices) {
            const found = voices.find(v => v.name.includes(target));
            if (found) {
                atlasVoiceInstance = found;
                return;
            }
        }
        atlasVoiceInstance = voices.find(v => v.lang.includes('en') && (v.name.includes('Male') || v.name.includes('Guy')))
                             || voices.find(v => v.lang.includes('en'))
                             || voices[0];
    }

    function initSTT() {
        const SpeechRecognition = window.SpeechRecognition || window.webkitSpeechRecognition;
        if (!SpeechRecognition) return;

        recognition = new SpeechRecognition();
        recognition.lang = 'en-US';
        recognition.continuous = true;
        recognition.interimResults = false;

        recognition.onstart = () => {
            isListening = true;
            explicitlyStopped = false;
            dispatch('stateChange', 'LISTENING');
        };

        recognition.onresult = (event) => {
            const last = event.results.length - 1;
            const transcript = event.results[last][0].transcript.trim().toLowerCase();

            if (transcript.includes("hey atlas") || transcript.includes("he atlas") || transcript.includes("atlas")) {
                let command = transcript.replace(/hey atlas|he atlas|atlas/g, "").trim();
                dispatch('command', command);
            }
        };

        recognition.onerror = (event) => {
            if (event.error === 'not-allowed' || event.error === 'service-not-allowed') {
                explicitlyStopped = true;
                isListening = false;
                dispatch('stateChange', 'STANDBY');
            }
        };

        recognition.onend = () => {
            isListening = false;
            if (!explicitlyStopped) {
                try { recognition.start(); } catch (e) {}
            } else {
                dispatch('stateChange', 'STANDBY');
            }
        };
    }

    export function toggleListening() {
        if (isListening) {
            explicitlyStopped = true;
            recognition.stop();
        } else {
            explicitlyStopped = false;
            try { recognition.start(); } catch (e) {}
        }
    }

    function speak(text) {
        if (!synth) return;
        synth.cancel();

        const utterance = new SpeechSynthesisUtterance(text);
        if (atlasVoiceInstance) utterance.voice = atlasVoiceInstance;
        utterance.pitch = 0.85;
        utterance.rate = 0.95;

        utterance.onstart = () => dispatch('stateChange', 'SPEAKING');
        utterance.onend = () => dispatch('stateChange', 'STANDBY');

        synth.speak(utterance);
    }
</script>
