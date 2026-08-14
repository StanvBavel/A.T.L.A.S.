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
        this.lastPinchDistance = null;
        this.targetRotationY = 0;
        this.targetRotationX = 0;
    }

    async activate() {
        if (this.isActive) return;
        this.isActive = true;
        this.overlay.style.display = 'block';

        this.initThreeJs();
        await this.startCameraAndSendFrame();
    }

    initThreeJs() {
        this.scene = new THREE.Scene();

        const aspect = window.innerWidth / window.innerHeight;
        this.camera = new THREE.PerspectiveCamera(75, aspect, 0.1, 1000);
        this.camera.position.z = 5;

        this.renderer = new THREE.WebGLRenderer({ alpha: true, antialias: true });
        this.renderer.setSize(window.innerWidth, window.innerHeight);
        this.canvasContainer.innerHTML = '';
        this.canvasContainer.appendChild(this.renderer.domElement);

        const ambientLight = new THREE.AmbientLight(0xffffff, 0.6);
        this.scene.add(ambientLight);

        const pointLight = new THREE.PointLight(0x00f3ff, 1.5, 100);
        pointLight.position.set(5, 5, 5);
        this.scene.add(pointLight);

        const dirLight = new THREE.DirectionalLight(0xffffff, 0.8);
        dirLight.position.set(-5, 5, 5);
        this.scene.add(dirLight);
    }

    async startCameraAndSendFrame() {
        try {
            const stream = await navigator.mediaDevices.getUserMedia({ video: { width: 640, height: 480 } });
            this.videoElement.srcObject = stream;

            this.videoElement.onloadedmetadata = () => {
                this.videoElement.play();
                setTimeout(() => {
                    this.captureAndSendFrame();
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

        const base64Image = canvas.toDataURL('image/jpeg', 0.5);

        if (window.connection && window.connection.state === signalR.HubConnectionState.Connected) {
            if (window.logMessage) window.logMessage("[SYS] Transmitting visual data to core for 3D reconstruction...");
            window.connection.invoke("ProcessCameraFrame", base64Image).catch(err => console.error(err));
        }
    }

    loadMockModel(modelName) {
        if (this.mockModel) {
            this.scene.remove(this.mockModel);
            this.mockModel = null;
        }

        const name = (modelName || "").toLowerCase();

        // Dynamic loading based on modelName using basic shapes as fallback for demonstration,
        // In a real scenario, use GLTFLoader to load specific GLB files based on the name.
        let geometry;
        if (name.includes("car") || name.includes("mustang")) {
            geometry = new THREE.BoxGeometry(4, 1.5, 2); // Roughly car shaped
        } else if (name.includes("sphere") || name.includes("ball")) {
            geometry = new THREE.SphereGeometry(1.5, 32, 32);
        } else if (name.includes("pyramid")) {
            geometry = new THREE.ConeGeometry(2, 3, 4);
        } else {
            geometry = new THREE.BoxGeometry(2, 2, 2);
        }

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
                if (this.isActive) {
                    await this.hands.send({image: this.videoElement});
                }
            },
            width: 640,
            height: 480
        });
        this.mpCamera.start();
    }

    onMediaPipeResults(results) {
        if (!this.mockModel || !this.isActive) return;

        if (results.multiHandLandmarks && results.multiHandLandmarks.length > 0) {
            const landmarks = results.multiHandLandmarks[0];

            const indexTip = landmarks[8];
            const thumbTip = landmarks[4];
            const wrist = landmarks[0];

            // 1. Position mapping (Pan) - using wrist for stability
            const targetX = (wrist.x - 0.5) * -10;
            const targetY = (wrist.y - 0.5) * -10;

            this.mockModel.position.x += (targetX - this.mockModel.position.x) * 0.2;
            this.mockModel.position.y += (targetY - this.mockModel.position.y) * 0.2;

            // 2. Rotation - Completely driven by hand position (e.g. index position relative to wrist)
            // No auto rotation!
            const dx = indexTip.x - wrist.x;
            const dy = indexTip.y - wrist.y;

            // Use hand angle to determine rotation target
            this.targetRotationY = dx * Math.PI * 4; // multiplier for sensitivity
            this.targetRotationX = dy * Math.PI * 4;

            this.mockModel.rotation.y += (this.targetRotationY - this.mockModel.rotation.y) * 0.1;
            this.mockModel.rotation.x += (this.targetRotationX - this.mockModel.rotation.x) * 0.1;

            // 3. Zoom (Pinch)
            const pinchDx = indexTip.x - thumbTip.x;
            const pinchDy = indexTip.y - thumbTip.y;
            const distance = Math.sqrt(pinchDx*pinchDx + pinchDy*pinchDy);

            if (this.lastPinchDistance !== null) {
                let targetScale = 1.0 + (distance * 4);
                targetScale = Math.max(0.3, Math.min(targetScale, 4.0));

                // Smooth scale
                this.mockModel.scale.lerp(new THREE.Vector3(targetScale, targetScale, targetScale), 0.1);
            }
            this.lastPinchDistance = distance;

        } else {
            // When no hand is detected, we do NOT auto-rotate anymore.
            // Model stays in its last mapped position/rotation.
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
        if (!this.isActive) return;
        this.isActive = false;

        // Hide UI
        this.overlay.style.display = 'none';

        // Stop MediaPipe
        if (this.mpCamera) {
            this.mpCamera.stop();
        }
        if (this.hands) {
            this.hands.close();
        }

        // Stop Camera Stream
        const stream = this.videoElement.srcObject;
        if (stream) {
            stream.getTracks().forEach(track => track.stop());
            this.videoElement.srcObject = null;
        }

        // Clean Three.js Scene
        if (this.scene && this.mockModel) {
            this.scene.remove(this.mockModel);
            // Dispose geometries and materials
            this.mockModel.geometry.dispose();
            this.mockModel.material.dispose();
            this.mockModel = null;
        }

        if (this.renderer) {
            this.canvasContainer.innerHTML = ''; // clear dom
        }

        if (window.logMessage) window.logMessage("[SYS] Hologram and camera systems deactivated.");
    }
}

window.hologramController = new HologramController();
