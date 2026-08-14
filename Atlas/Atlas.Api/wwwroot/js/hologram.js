class HologramController {
    constructor() {
        this.videoElement = document.getElementById('camera-video');
        this.canvasContainer = document.getElementById('three-canvas-container');
        this.overlay = document.getElementById('hologram-overlay');
        this.loadingOverlay = document.getElementById('hologram-loading');
        this.loadingText = document.getElementById('hologram-loading-text');

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

    async activateLoadingMode(objectName) {
        if (this.isActive) return;
        this.isActive = true;
        this.overlay.style.display = 'block';
        this.loadingOverlay.style.display = 'block';
        if (this.loadingText) {
            this.loadingText.innerText = `Generating structural blueprint for [${objectName.toUpperCase()}]... Remote processing engaged.`;
        }

        this.initThreeJs();
        await this.startCamera();
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

        this.animate();
    }

    async startCamera() {
        try {
            const stream = await navigator.mediaDevices.getUserMedia({ video: { width: 640, height: 480 } });
            this.videoElement.srcObject = stream;

            this.videoElement.onloadedmetadata = () => {
                this.videoElement.play();
                // Send an initial frame to let backend know camera is active if needed
                setTimeout(() => {
                    this.captureAndSendFrame();
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
            window.connection.invoke("ProcessCameraFrame", base64Image).catch(err => console.error(err));
        }
    }

    loadGltfModel(modelUrl) {
        if (!this.isActive) return;

        // Remove old model if present
        if (this.mockModel) {
            this.scene.remove(this.mockModel);
            this.mockModel = null;
        }

        if (window.logMessage) window.logMessage("[SYS] Processing downloaded spatial mesh...");

        // Load via GLTFLoader
        const loader = new THREE.GLTFLoader();
        loader.load(
            modelUrl,
            (gltf) => {
                this.mockModel = gltf.scene;

                // Add a neon wireframe material override for JARVIS aesthetic
                this.mockModel.traverse((child) => {
                    if (child.isMesh) {
                        // Apply a wireframe neon material to the imported mesh
                        child.material = new THREE.MeshBasicMaterial({
                            color: 0x00f3ff,
                            wireframe: true,
                            transparent: true,
                            opacity: 0.8
                        });
                    }
                });

                // Auto-scale to fit view roughly
                const box = new THREE.Box3().setFromObject(this.mockModel);
                const size = box.getSize(new THREE.Vector3());
                const maxDim = Math.max(size.x, size.y, size.z);
                const scale = 3 / maxDim; // Normalize size to about 3 units wide
                this.mockModel.scale.set(scale, scale, scale);

                // Center model
                const center = box.getCenter(new THREE.Vector3());
                this.mockModel.position.sub(center.multiplyScalar(scale));

                this.scene.add(this.mockModel);

                // Remove Loading UI
                this.loadingOverlay.style.display = 'none';

                // Initiate Hand Tracking Interaction once loaded
                this.initMediaPipe();
            },
            (xhr) => {
                if(this.loadingText) {
                    this.loadingText.innerText = `Downloading mesh data: ${Math.round(xhr.loaded / xhr.total * 100)}%`;
                }
            },
            (error) => {
                console.error("[GLTF ERROR]", error);
                if (window.logMessage) window.logMessage("[SYS_ERROR] Failed to compile downloaded mesh.");
                this.deactivate();
            }
        );
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

            // 1. Position mapping (Pan)
            const targetX = (wrist.x - 0.5) * -10;
            const targetY = (wrist.y - 0.5) * -10;

            this.mockModel.position.x += (targetX - this.mockModel.position.x) * 0.2;
            this.mockModel.position.y += (targetY - this.mockModel.position.y) * 0.2;

            // 2. Rotation
            const dx = indexTip.x - wrist.x;
            const dy = indexTip.y - wrist.y;

            this.targetRotationY = dx * Math.PI * 4;
            this.targetRotationX = dy * Math.PI * 4;

            this.mockModel.rotation.y += (this.targetRotationY - this.mockModel.rotation.y) * 0.1;
            this.mockModel.rotation.x += (this.targetRotationX - this.mockModel.rotation.x) * 0.1;

            // 3. Zoom (Pinch)
            const pinchDx = indexTip.x - thumbTip.x;
            const pinchDy = indexTip.y - thumbTip.y;
            const distance = Math.sqrt(pinchDx*pinchDx + pinchDy*pinchDy);

            if (this.lastPinchDistance !== null) {
                let targetScale = this.mockModel.scale.x + ((distance - this.lastPinchDistance) * 5);
                targetScale = Math.max(0.1, Math.min(targetScale, 5.0));

                this.mockModel.scale.lerp(new THREE.Vector3(targetScale, targetScale, targetScale), 0.2);
            }
            this.lastPinchDistance = distance;

        } else {
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

        this.overlay.style.display = 'none';
        this.loadingOverlay.style.display = 'none';

        if (this.mpCamera) {
            this.mpCamera.stop();
        }
        if (this.hands) {
            this.hands.close();
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

        if (this.renderer) {
            this.canvasContainer.innerHTML = '';
        }

        if (window.logMessage) window.logMessage("[SYS] Hologram and camera systems deactivated.");
    }
}

window.hologramController = new HologramController();
