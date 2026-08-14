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
            logMessage("Error: Web Speech API (STT) not supported in this browser.");
            return;
        }

        this.recognition = new SpeechRecognition();
        this.recognition.lang = 'nl-NL'; // Default to Dutch, can be made configurable
        this.recognition.continuous = true;
        this.recognition.interimResults = false;

        this.recognition.onstart = () => {
            this.isListening = true;
            logMessage("[VOICE]: Listening for Wake Word...");
            updateUiState("LISTENING");
        };

        this.recognition.onresult = (event) => {
            const last = event.results.length - 1;
            const transcript = event.results[last][0].transcript.trim().toLowerCase();

            if (transcript.includes("hey atlas") || transcript.includes("he atlas")) {
                const command = transcript.replace(/hey atlas|he atlas/g, "").trim();
                logMessage(`[VOICE IN]: ${transcript}`);

                if (command.length > 0) {
                    sendToServer(command);
                } else {
                    this.speak("Ja?");
                }
            }
        };

        this.recognition.onerror = (event) => {
            logMessage(`[VOICE ERROR]: ${event.error}`);
            if (event.error !== 'no-speech') {
                updateUiState("STANDBY");
                this.isListening = false;
            }
        };

        this.recognition.onend = () => {
            // Auto-restart if we want continuous listening, but for now we require explicit start
            if (this.isListening) {
                this.recognition.start();
            } else {
                updateUiState("STANDBY");
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

        updateUiState("SPEAKING");

        // Cancel any ongoing speech
        this.synth.cancel();

        const utterance = new SpeechSynthesisUtterance(text);
        utterance.lang = 'nl-NL';

        utterance.onend = () => {
            updateUiState("STANDBY");
        };

        utterance.onerror = (e) => {
            logMessage(`[VOICE TTS ERROR]: ${e.error}`);
            updateUiState("STANDBY");
        };

        this.synth.speak(utterance);
    }
}

const atlasVoice = new AtlasVoice();
