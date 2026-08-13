let connection;

function startSignalR() {
    connection = new signalR.HubConnectionBuilder()
        .withUrl("/hubs/atlas")
        .withAutomaticReconnect()
        .build();

    connection.on("ReceiveMessage", (message) => {
        logMessage(`[A.T.L.A.S]: <span style="color:#0f0">${message}</span>`);
        if (window.atlasVoice) {
            atlasVoice.speak(message);
        }
    });

    connection.on("UpdateCoreState", (state) => {
        updateUiState(state);
    });

    connection.on("RequireUserConsent", (toolName, args) => {
        logMessage(`<span style="color:#f00">[WARNING] Action Requires Consent: ${toolName} ${args}</span>`);
        showConsentDialog(toolName, args);
    });

    connection.on("ReceiveTelemetry", (cpu, ram) => {
        document.getElementById('cpu-value').innerText = cpu;
        document.getElementById('ram-value').innerText = ram;
    });

    connection.start()
        .then(() => {
            logMessage("Neural Link Established. SignalR Connected.");
        })
        .catch(err => {
            logMessage(`<span style="color:#f00">SignalR Connection Error: ${err.toString()}</span>`);
        });
}

function sendToServer(text) {
    if (connection && connection.state === signalR.HubConnectionState.Connected) {
        connection.invoke("SendText", text).catch(err => console.error(err.toString()));
    } else {
        logMessage("Error: Neural Link Offline.");
    }
}

function grantPermissionToServer(toolName, args) {
    if (connection && connection.state === signalR.HubConnectionState.Connected) {
        connection.invoke("GrantPermission", toolName, args).catch(err => console.error(err.toString()));
    }
}
