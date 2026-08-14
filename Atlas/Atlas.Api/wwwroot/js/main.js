function logMessage(msg) {
    const logContainer = document.getElementById('log-container');
    const entry = document.createElement('div');
    entry.innerHTML = `<span style="color:rgba(255,255,255,0.5)">[${new Date().toLocaleTimeString()}]</span> ${msg}`;
    logContainer.appendChild(entry);
    logContainer.scrollTop = logContainer.scrollHeight;
}

function updateUiState(state) {
    const coreStatus = document.getElementById('core-status');
    const statusText = document.getElementById('status-text');

    coreStatus.className = `status-${state.toLowerCase()}`;
    statusText.innerText = state;
}

function handleSend() {
    const input = document.getElementById('chat-input');
    const text = input.value.trim();
    if (text) {
        logMessage(`[USER]: <span style="color:#fff">${text}</span>`);
        sendToServer(text);
        input.value = '';
    }
}

function showConsentDialog(toolName, args) {
    const result = confirm(`⚠️ SECURITY ALERT ⚠️\n\nA.T.L.A.S wants to execute:\n[ ${toolName} ]\nArguments: ${args}\n\nDo you grant permission?`);
    if (result) {
        logMessage(`[SEC]: Permission GRANTED for ${toolName}`);
        grantPermissionToServer(toolName, args);
    } else {
        logMessage(`[SEC]: Permission DENIED for ${toolName}`);
        sendToServer("Ik heb de toestemming voor deze actie geweigerd.");
    }
}

function updateWidgets() {
    const now = new Date();
    document.getElementById('time-value').innerText = now.toLocaleTimeString();

    // Request telemetry from server
    if (window.connection && window.connection.state === "Connected") {
        window.connection.invoke("RequestTelemetry").catch(e => console.error(e));
    }
}

document.addEventListener("DOMContentLoaded", () => {
    logMessage("A.T.L.A.S. Core Interface Initialized.");
    startSignalR();

    document.getElementById('send-button').addEventListener('click', handleSend);
    document.getElementById('chat-input').addEventListener('keypress', (e) => {
        if (e.key === 'Enter') handleSend();
    });

    const micBtn = document.getElementById('mic-button');
    micBtn.addEventListener('click', () => {
        if (atlasVoice.isListening) {
            atlasVoice.stopListening();
            micBtn.classList.remove('active');
        } else {
            atlasVoice.startListening();
            micBtn.classList.add('active');
        }
    });

    setInterval(updateWidgets, 1000);
});
