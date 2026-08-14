class HologramController {
    constructor() {
        this.videoElement = document.getElementById('camera-video');
        this.canvasContainer = document.getElementById('three-canvas-container');
        this.overlay = document.getElementById('hologram-overlay');

        this.isActive = false;

        // Three.js instances
        this.scene = null;
        this.camera = null;
        this.renderer = null;
        this.mockModel = null;

        // MediaPipe instances
        this.hands = null;
        this.mpCamera = null;

        // Interaction state
        this.baseScale = 1.0;
        this.lastPinchDistance = null;
    }

    async activate() {
        if (this.isActive) return;
        this.isActive = true;
        this.overlay.style.display = 'block';

        // 1. Initialize Three.js Scene
        this.initThreeJs();

        // 2. Start Camera and capture frame to send to backend
        await this.startCameraAndSendFrame();
    }

    initThreeJs() {
        this.scene = new THREE.Scene();

        // Setup Camera
        const aspect = window.innerWidth / window.innerHeight;
        this.camera = new THREE.PerspectiveCamera(75, aspect, 0.1, 1000);
        this.camera.position.z = 5;

        // Setup Renderer
        this.renderer = new THREE.WebGLRenderer({ alpha: true, antialias: true });
        this.renderer.setSize(window.innerWidth, window.innerHeight);
        this.canvasContainer.innerHTML = '';
        this.canvasContainer.appendChild(this.renderer.domElement);

        // Add Lights
        const ambientLight = new THREE.AmbientLight(0x00f3ff, 0.5); // Neon Cyan Ambient
        this.scene.add(ambientLight);

        const pointLight = new THREE.PointLight(0xffffff, 1, 100);
        pointLight.position.set(5, 5, 5);
        this.scene.add(pointLight);
    }

    async startCameraAndSendFrame() {
        try {
            const stream = await navigator.mediaDevices.getUserMedia({ video: { width: 640, height: 480 } });
            this.videoElement.srcObject = stream;

            // Wait for video to be ready
            this.videoElement.onloadedmetadata = () => {
                this.videoElement.play();

                // Capture frame after a short delay to ensure lighting adjusts
                setTimeout(() => {
                    this.captureAndSendFrame();
                    // Also start MediaPipe Hand Tracking
                    this.initMediaPipe();
                }, 1000);
            };
        } catch (err) {
            console.error("Camera access denied or failed:", err);
            if (window.logMessage) window.logMessage("[SYS_ERROR] Camera access denied.");
        }
    }

    captureAndSendFrame() {
        const canvas = document.createElement('canvas');
        canvas.width = this.videoElement.videoWidth;
        canvas.height = this.videoElement.videoHeight;
        const ctx = canvas.getContext('2d');
        ctx.drawImage(this.videoElement, 0, 0, canvas.width, canvas.height);

        const base64Image = canvas.toDataURL('image/jpeg', 0.5); // Compressing to 50% for transmission

        if (window.connection && window.connection.state === signalR.HubConnectionState.Connected) {
            if (window.logMessage) window.logMessage("[SYS] Transmitting visual data to core for 3D reconstruction...");
            window.connection.invoke("ProcessCameraFrame", base64Image).catch(err => console.error(err));
        }
    }

    loadMockModel(modelType) {
        // Creates a glowing wireframe box as the mock 3D reconstructed model
        const geometry = new THREE.BoxGeometry(2, 2, 2);

        // Create edges for wireframe aesthetic
        const edges = new THREE.EdgesGeometry(geometry);
        const material = new THREE.LineBasicMaterial({ color: 0x00f3ff, linewidth: 2 });
        this.mockModel = new THREE.LineSegments(edges, material);

        this.scene.add(this.mockModel);

        this.animate();
    }

    initMediaPipe() {
        this.hands = new Hands({locateFile: (file) => {
            return `https://cdn.jsdelivr.net/npm/@mediapipe/hands/${file}`;
        }});

        this.hands.setOptions({
            maxNumHands: 1,
            modelComplexity: 1,
            minDetectionConfidence: 0.5,
            minTrackingConfidence: 0.5
        });

        this.hands.onResults(this.onMediaPipeResults.bind(this));

        this.mpCamera = new Camera(this.videoElement, {
            onFrame: async () => {
                await this.hands.send({image: this.videoElement});
            },
            width: 640,
            height: 480
        });
        this.mpCamera.start();
    }

    onMediaPipeResults(results) {
        if (!this.mockModel) return;

        if (results.multiHandLandmarks && results.multiHandLandmarks.length > 0) {
            const landmarks = results.multiHandLandmarks[0];

            // Index finger tip (8) and Thumb tip (4)
            const indexTip = landmarks[8];
            const thumbTip = landmarks[4];

            // 1. Position mapping (Pan)
            // Map the palm base (0) or index tip to screen space.
            // Note: camera is mirrored, so x is inverted.
            const targetX = (indexTip.x - 0.5) * -10;
            const targetY = (indexTip.y - 0.5) * -10;

            // Smooth lerping
            this.mockModel.position.x += (targetX - this.mockModel.position.x) * 0.1;
            this.mockModel.position.y += (targetY - this.mockModel.position.y) * 0.1;

            // 2. Rotation (Rotate based on wrist angle or just auto-rotate slowly, plus hand movement)
            this.mockModel.rotation.y += (targetX * 0.05);
            this.mockModel.rotation.x += (targetY * 0.05);

            // 3. Zoom (Pinch)
            const dx = indexTip.x - thumbTip.x;
            const dy = indexTip.y - thumbTip.y;
            const distance = Math.sqrt(dx*dx + dy*dy);

            // If distance is small enough, it's a pinch.
            // We scale up/down based on pinch distance changes
            if (this.lastPinchDistance !== null) {
                const delta = distance - this.lastPinchDistance;
                // If it's a wide open hand (distance > 0.1), let it scale normally based on absolute distance
                let targetScale = 1.0 + (distance * 3);

                // Clamp scale between 0.5 and 3.0
                targetScale = Math.max(0.5, Math.min(targetScale, 3.0));

                this.mockModel.scale.set(targetScale, targetScale, targetScale);
            }
            this.lastPinchDistance = distance;

        } else {
            // Idle animation when no hands detected
            this.mockModel.rotation.y += 0.01;
            this.mockModel.rotation.x += 0.005;
            this.lastPinchDistance = null;
        }
    }

    animate() {
        if (!this.isActive) return;
        requestAnimationFrame(this.animate.bind(this));

        if (this.renderer && this.scene && this.camera) {
            this.renderer.render(this.scene, this.camera);
        }
    }

    deactivate() {
        this.isActive = false;
        this.overlay.style.display = 'none';

        if (this.mpCamera) {
            this.mpCamera.stop();
        }

        const stream = this.videoElement.srcObject;
        if (stream) {
            stream.getTracks().forEach(track => track.stop());
            this.videoElement.srcObject = null;
        }

        if (this.scene && this.mockModel) {
            this.scene.remove(this.mockModel);
            this.mockModel = null;
        }
    }
}

window.hologramController = new HologramController();
