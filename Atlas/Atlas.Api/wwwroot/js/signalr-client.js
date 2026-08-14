let connection;

function startSignalR() {
    connection = new signalR.HubConnectionBuilder()
        .withUrl("/hubs/atlas")
        .withAutomaticReconnect()
        .build();

    connection.on("ReceiveMessage", (message) => {
        if (window.logMessage) window.logMessage(`[A.T.L.A.S]: <span style="color:#0f0">${message}</span>`);
        if (window.atlasVoice) {
            window.atlasVoice.speak(message);
        }
    });

    connection.on("UpdateCoreState", (state) => {
        if (window.updateUiState) window.updateUiState(state);
    });

    connection.on("RequireUserConsent", (toolName, args) => {
        if (window.logMessage) window.logMessage(`<span style="color:#f00">[WARNING] Action Requires Consent: ${toolName} ${args}</span>`);
        if (window.showConsentDialog) window.showConsentDialog(toolName, args);
    });

    connection.on("ReceiveTelemetry", (cpu, ram) => {
        const cpuEl = document.getElementById('cpu-value');
        const ramEl = document.getElementById('ram-value');
        if (cpuEl) cpuEl.innerText = cpu;
        if (ramEl) ramEl.innerText = ram;
    });

    connection.on("DisplayImages", (urls) => {
        if (urls && urls.length > 0) {
            const viewer = document.getElementById('image-viewer');
            const imgEl = document.getElementById('image-viewer-img');
            if (viewer && imgEl) {
                imgEl.src = urls[0];
                viewer.style.display = 'block';

                setTimeout(() => {
                    viewer.style.display = 'none';
                    imgEl.src = "";
                }, 15000);
            }
        }
    });

    // --- New Hologram Events ---
    connection.on("ActivateHologramMode", () => {
        if (window.hologramController) {
            window.hologramController.activate();
        }
    });

    connection.on("HologramGenerated", (modelType) => {
        if (window.hologramController) {
            window.hologramController.loadMockModel(modelType);
        }
    });

    connection.start()
        .then(() => {
            if (window.logMessage) window.logMessage("Neural Link Established. SignalR Connected.");
        })
        .catch(err => {
            if (window.logMessage) window.logMessage(`<span style="color:#f00">SignalR Connection Error: ${err.toString()}</span>`);
        });

    window.connection = connection;
}

function sendToServer(text) {
    if (connection && connection.state === signalR.HubConnectionState.Connected) {
        connection.invoke("SendText", text).catch(err => console.error(err.toString()));
    } else {
        if (window.logMessage) window.logMessage("Error: Neural Link Offline.");
    }
}

function grantPermissionToServer(toolName, args) {
    if (connection && connection.state === signalR.HubConnectionState.Connected) {
        connection.invoke("GrantPermission", toolName, args).catch(err => console.error(err.toString()));
    }
}

window.startSignalR = startSignalR;
window.sendToServer = sendToServer;
window.grantPermissionToServer = grantPermissionToServer;
