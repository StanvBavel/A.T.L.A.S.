// Voice API integration (Web Speech API)
class AtlasVoice {
    constructor() {
        this.recognition = null;
        this.synth = window.speechSynthesis;
        this.isListening = false;

        this.initSTT();
    }

    initSTT() {
        const SpeechRecognition = window.SpeechRecognition || window.webkitSpeechRecognition;
        if (!SpeechRecognition) {
            console.error("Web Speech API (STT) not supported in this browser.");
            return;
        }

        this.recognition = new SpeechRecognition();
        this.recognition.lang = 'nl-NL';
        this.recognition.continuous = true;
        this.recognition.interimResults = false;

        this.recognition.onstart = () => {
            this.isListening = true;
            if (window.logMessage) window.logMessage("[VOICE]: Listening for Wake Word...");
            if (window.updateUiState) window.updateUiState("LISTENING");
        };

        this.recognition.onresult = (event) => {
            const last = event.results.length - 1;
            const transcript = event.results[last][0].transcript.trim().toLowerCase();

            if (transcript.includes("hey atlas") || transcript.includes("he atlas")) {
                const command = transcript.replace(/hey atlas|he atlas/g, "").trim();
                if (window.logMessage) window.logMessage(`[VOICE IN]: ${transcript}`);

                if (command.length > 0) {
                    if (window.sendToServer) window.sendToServer(command);
                } else {
                    this.speak("Ja?");
                }
            }
        };

        this.recognition.onerror = (event) => {
            if (window.logMessage) window.logMessage(`[VOICE ERROR]: ${event.error}`);
            if (event.error !== 'no-speech') {
                if (window.updateUiState) window.updateUiState("STANDBY");
                this.isListening = false;
            }
        };

        this.recognition.onend = () => {
            if (this.isListening) {
                this.recognition.start();
            } else {
                if (window.updateUiState) window.updateUiState("STANDBY");
            }
        };
    }

    startListening() {
        if (this.recognition && !this.isListening) {
            this.recognition.start();
        }
    }

    stopListening() {
        if (this.recognition && this.isListening) {
            this.isListening = false;
            this.recognition.stop();
        }
    }

    speak(text) {
        if (!this.synth) return;

        // Cancel any ongoing speech
        this.synth.cancel();

        const utterance = new SpeechSynthesisUtterance(text);
        utterance.lang = 'en-US'; // Set to english for the intro, can be dynamic later

        utterance.onstart = () => {
            if (window.updateUiState) window.updateUiState("SPEAKING");
        };

        utterance.onend = () => {
            if (window.updateUiState) window.updateUiState("STANDBY");
        };

        utterance.onerror = (e) => {
            if (window.logMessage) window.logMessage(`[VOICE TTS ERROR]: ${e.error}`);
            if (window.updateUiState) window.updateUiState("STANDBY");
        };

        this.synth.speak(utterance);
    }
}

const atlasVoice = new AtlasVoice();
window.atlasVoice = atlasVoice; // Ensure it is globally accessible
