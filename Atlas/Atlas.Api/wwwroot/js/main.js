function logMessage(msg) {
    const logContainer = document.getElementById('log-container');
    const entry = document.createElement('div');

    const timestampSpan = document.createElement('span');
    timestampSpan.style.color = "rgba(255,255,255,0.5)";
    timestampSpan.textContent = `[${new Date().toLocaleTimeString()}] `;

    const textNode = document.createElement('span');
    textNode.innerHTML = msg;

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

    logMessage(`[USER]: <span style="color:#fff">${text}</span>`);

    const lowerText = text.toLowerCase();

    // Improved Image Search Regex Extractor
    const imgRegex = /(?:show|find|lookup|search for|display)(?: me)?(?: a| an| the)? (?:image|picture|photo)(?: of)? (.+)/i;
    const match = text.match(imgRegex);

    if (match && match[1]) {
        const query = match[1].trim(); // Extract purely the subject (e.g., "ford mustang")
        if (window.sendToServer) {
            window.sendToServer(`/tool ImageSearch ${query}`);
            return;
        }
    }

    if (window.sendToServer) {
        window.sendToServer(text);
    }
}

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
        if (window.sendToServer) window.sendToServer("Permission denied by user.");
    }
}

function updateWidgets() {
    const now = new Date();
    document.getElementById('time-value').innerText = now.toLocaleTimeString();

    if (typeof connection !== 'undefined' && connection.state === "Connected") {
        connection.invoke("RequestTelemetry").catch(e => console.error(e));
    }
}

document.addEventListener("DOMContentLoaded", () => {
    logMessage("A.T.L.A.S. Core Interface Initialized.");
    if (window.startSignalR) startSignalR();

    document.getElementById('send-button').addEventListener('click', handleSend);
    document.getElementById('chat-input').addEventListener('keypress', (e) => {
        if (e.key === 'Enter') handleSend();
    });

    const micBtn = document.getElementById('mic-button');
    micBtn.addEventListener('click', () => {
        if (window.atlasVoice && window.atlasVoice.isListening) {
            window.atlasVoice.stopListening();
            micBtn.classList.remove('active');
        } else if (window.atlasVoice) {
            window.atlasVoice.startListening();
            micBtn.classList.add('active');
        }
    });

    setInterval(updateWidgets, 1000);
});
