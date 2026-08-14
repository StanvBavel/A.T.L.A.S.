// Voice API integration (Web Speech API)
class AtlasVoice {
    constructor() {
        this.recognition = null;
        this.synth = window.speechSynthesis;
        this.isListening = false;
        this.explicitlyStopped = true;
        this.atlasVoiceInstance = null;

        // Wait for voices to load
        if (speechSynthesis.onvoiceschanged !== undefined) {
            speechSynthesis.onvoiceschanged = () => this.setAtlasVoice();
        }

        this.initSTT();
    }

    setAtlasVoice() {
        const voices = this.synth.getVoices();
        if (voices.length === 0) return;

        const targetVoices = [
            "Microsoft George",
            "Google UK English Male",
            "Microsoft Mark"
        ];

        for (const target of targetVoices) {
            const found = voices.find(v => v.name.includes(target));
            if (found) {
                this.atlasVoiceInstance = found;
                console.log(`[TTS] System Voice set to: ${found.name}`);
                return;
            }
        }

        // Fallback to first available English male voice (heuristic)
        this.atlasVoiceInstance = voices.find(v => v.lang.includes('en') && (v.name.includes('Male') || v.name.includes('Guy')))
                                  || voices.find(v => v.lang.includes('en'))
                                  || voices[0];
        console.log(`[TTS] Fallback System Voice set to: ${this.atlasVoiceInstance.name}`);
    }

    initSTT() {
        const SpeechRecognition = window.SpeechRecognition || window.webkitSpeechRecognition;
        if (!SpeechRecognition) {
            console.error("[STT] Web Speech API not supported in this browser.");
            return;
        }

        this.recognition = new SpeechRecognition();

        // Configuration for stability
        this.recognition.lang = 'en-US'; // English as baseline for JARVIS aesthetic, adjust to nl-NL if fully Dutch
        this.recognition.continuous = true;
        this.recognition.interimResults = false;
        this.recognition.maxAlternatives = 1;

        this.recognition.onstart = () => {
            this.isListening = true;
            this.explicitlyStopped = false;
            console.log("[STT] Audio capture activated. Listening for wake word...");
            if (window.updateUiState) window.updateUiState("LISTENING");
        };

        this.recognition.onresult = (event) => {
            const last = event.results.length - 1;
            const transcript = event.results[last][0].transcript.trim().toLowerCase();

            // Console logging for troubleshooting accuracy
            console.log(`[STT DEBUG] Browser transcribed: "${transcript}" (Confidence: ${event.results[last][0].confidence})`);

            // Wake-Word Filtering
            if (transcript.includes("hey atlas") || transcript.includes("he atlas") || transcript.includes("atlas")) {
                let command = transcript.replace(/hey atlas|he atlas|atlas/g, "").trim();

                if (window.logMessage) window.logMessage(`[VOICE IN]: ${transcript}`);

                if (command.length > 0) {
                    // Send to the central command processor instead of directly to server
                    if (window.processCommand) {
                        window.processCommand(command);
                    } else if (window.sendToServer) {
                        window.sendToServer(command);
                    }
                } else {
                    this.speak("Yes, sir?");
                }
            }
        };

        this.recognition.onerror = (event) => {
            console.error(`[STT ERROR] ${event.error}`);

            if (event.error !== 'no-speech') {
                if (window.logMessage) window.logMessage(`[STT ERROR]: ${event.error}`);
            }

            // Certain errors (like not-allowed) should stop the loop
            if (event.error === 'not-allowed' || event.error === 'service-not-allowed') {
                this.explicitlyStopped = true;
                if (window.updateUiState) window.updateUiState("STANDBY");
                this.isListening = false;
            }
        };

        this.recognition.onend = () => {
            console.log("[STT] Microphone stream ended.");
            this.isListening = false;

            // Auto-Restart Mechanism
            if (!this.explicitlyStopped) {
                console.log("[STT] Auto-restarting microphone...");
                try {
                    this.recognition.start();
                } catch (e) {
                    console.error("[STT] Auto-restart failed.", e);
                }
            } else {
                if (window.updateUiState) window.updateUiState("STANDBY");
            }
        };
    }

    startListening() {
        if (this.recognition && !this.isListening) {
            this.explicitlyStopped = false;
            try {
                this.recognition.start();
            } catch (e) {
                console.error("[STT] Start failed.", e);
            }
        }
    }

    stopListening() {
        if (this.recognition) {
            this.explicitlyStopped = true;
            this.isListening = false;
            this.recognition.stop();
            console.log("[STT] Microphone explicitly stopped.");
        }
    }

    speak(text) {
        if (!this.synth) return;

        // Cancel any ongoing speech
        this.synth.cancel();

        const utterance = new SpeechSynthesisUtterance(text);

        // Apply Voice Configuration
        if (this.atlasVoiceInstance) {
            utterance.voice = this.atlasVoiceInstance;
        }

        // JARVIS aesthetic modifiers
        utterance.pitch = 0.85;
        utterance.rate = 0.95;

        utterance.onstart = () => {
            if (window.updateUiState) window.updateUiState("SPEAKING");
        };

        utterance.onend = () => {
            if (window.updateUiState) window.updateUiState("STANDBY");
        };

        utterance.onerror = (e) => {
            console.error(`[TTS ERROR] ${e.error}`);
            if (window.updateUiState) window.updateUiState("STANDBY");
        };

        this.synth.speak(utterance);
    }
}

const atlasVoice = new AtlasVoice();
window.atlasVoice = atlasVoice;
