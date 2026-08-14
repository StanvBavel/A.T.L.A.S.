function logMessage(msg) {
    const logContainer = document.getElementById('log-container');
    const entry = document.createElement('div');

    const timestampSpan = document.createElement('span');
    timestampSpan.style.color = "rgba(255,255,255,0.5)";
    timestampSpan.textContent = `[${new Date().toLocaleTimeString()}] `;

    const textNode = document.createTextNode(msg);

    entry.appendChild(timestampSpan);
    entry.appendChild(textNode);

    logContainer.appendChild(entry);
    logContainer.scrollTop = logContainer.scrollHeight;
}

function updateUiState(state) {
    const coreStatus = document.getElementById('core-status');
    const statusText = document.getElementById('status-text');

    coreStatus.className = `status-${state.toLowerCase()}`;
    statusText.innerText = state;
}

function processCommand(text) {
    text = text.trim();
    if (!text) return;

    logMessage(`[USER]: ${text}`);

    // Fallback voor onondersteunde acties (Afbeeldingen)
    const lowerText = text.toLowerCase();
    const imageKeywords = ["toon een afbeelding", "show an image", "genereer een plaatje", "generate an image", "laat een foto zien", "show a picture", "teken", "draw"];

    const hasImageRequest = imageKeywords.some(keyword => lowerText.includes(keyword));

    if (hasImageRequest) {
        const fallbackMsg = "I'm a text-based AI and do not have the ability to search or access images directly.";
        logMessage(`[A.T.L.A.S]: ${fallbackMsg}`);
        if (window.atlasVoice) window.atlasVoice.speak(fallbackMsg);
        return; // Stop execution, do not send to backend
    }

    // Send valid commands to the backend
    if (window.sendToServer) {
        window.sendToServer(text);
    }
}

// Make processCommand globally available for voice.js
window.processCommand = processCommand;

function handleSend() {
    const input = document.getElementById('chat-input');
    const text = input.value;
    processCommand(text);
    input.value = '';
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

    // Request telemetry from server if connected
    if (typeof connection !== 'undefined' && connection.state === "Connected") {
        connection.invoke("RequestTelemetry").catch(e => console.error(e));
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
