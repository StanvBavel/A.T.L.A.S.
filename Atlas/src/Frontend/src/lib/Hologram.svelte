<script>
    import { onMount, onDestroy } from 'svelte';
    import * as THREE from 'three';
    import { GLTFLoader } from 'three/examples/jsm/loaders/GLTFLoader.js';

    export let active = false;
    export let loading = false;
    export let loadingText = "";
    export let modelUrl = null;

    let videoElement;
    let canvasContainer;

    let scene, camera, renderer, loadedModel, gridHelper;
    let hands, mpCamera;

    let isGrabbing = false;
    let previousHandCenter = { x: 0, y: 0 };

    $: if (active && !scene && canvasContainer) {
        initThreeJs();
        startCamera();
    }

    $: if (!active && scene) {
        deactivate();
    }

    $: if (modelUrl && scene) {
        loadModel(modelUrl);
    }

    function initThreeJs() {
        scene = new THREE.Scene();
        const aspect = window.innerWidth / window.innerHeight;
        camera = new THREE.PerspectiveCamera(75, aspect, 0.1, 1000);
        camera.position.z = 5;

        renderer = new THREE.WebGLRenderer({ alpha: true, antialias: true });
        renderer.setSize(window.innerWidth, window.innerHeight);
        canvasContainer.innerHTML = '';
        canvasContainer.appendChild(renderer.domElement);

        const ambientLight = new THREE.AmbientLight(0xffffff, 0.6);
        scene.add(ambientLight);
        const pointLight = new THREE.PointLight(0x00f3ff, 1.5, 100);
        pointLight.position.set(5, 5, 5);
        scene.add(pointLight);

        gridHelper = new THREE.GridHelper(10, 10, 0x00f3ff, 0x00f3ff);
        gridHelper.material.opacity = 0.2;
        gridHelper.material.transparent = true;
        gridHelper.position.y = -1;
        scene.add(gridHelper);

        animate();
    }

    async function startCamera() {
        try {
            const stream = await navigator.mediaDevices.getUserMedia({ video: { width: 640, height: 480 } });
            videoElement.srcObject = stream;

            videoElement.onloadedmetadata = () => {
                videoElement.play();
                initMediaPipe();
            };
        } catch (err) {
            console.error("Camera error:", err);
        }
    }

    function initMediaPipe() {
        // Using globals from CDN scripts included in index.html
        if (!window.Hands || !window.Camera) return;

        hands = new window.Hands({locateFile: (file) => `https://cdn.jsdelivr.net/npm/@mediapipe/hands/${file}`});

        hands.setOptions({
            maxNumHands: 1,
            modelComplexity: 1,
            minDetectionConfidence: 0.5,
            minTrackingConfidence: 0.5
        });

        hands.onResults(onMediaPipeResults);

        mpCamera = new window.Camera(videoElement, {
            onFrame: async () => {
                if (active) await hands.send({image: videoElement});
            },
            width: 640,
            height: 480
        });
        mpCamera.start();
    }

    function onMediaPipeResults(results) {
        if (!loadedModel || !active) return;

        if (results.multiHandLandmarks && results.multiHandLandmarks.length > 0) {
            const landmarks = results.multiHandLandmarks[0];

            const thumbTip = landmarks[4];
            const indexTip = landmarks[8];
            const middleTip = landmarks[12];

            const dx = indexTip.x - thumbTip.x;
            const dy = indexTip.y - thumbTip.y;
            const pinchDistance = Math.sqrt(dx*dx + dy*dy);

            const gdx = middleTip.x - thumbTip.x;
            const gdy = middleTip.y - thumbTip.y;
            const grabDistance = Math.sqrt(gdx*gdx + gdy*gdy);

            const isPinching = pinchDistance < 0.05;
            const isFist = grabDistance < 0.08;

            const handCenter = {
                x: (indexTip.x + thumbTip.x) / 2,
                y: (indexTip.y + thumbTip.y) / 2
            };

            // 1. PINCH TO ZOOM
            if (isPinching && !isFist) {
                const deltaY = handCenter.y - previousHandCenter.y;
                let targetScale = loadedModel.scale.x - (deltaY * 5);
                targetScale = Math.max(0.1, Math.min(targetScale, 5.0));
                loadedModel.scale.lerp(new THREE.Vector3(targetScale, targetScale, targetScale), 0.2);
            }
            // 2. GRAB TO ROTATE
            else if (isFist) {
                const deltaX = handCenter.x - previousHandCenter.x;
                const deltaY = handCenter.y - previousHandCenter.y;
                loadedModel.rotation.y += deltaX * 10;
                loadedModel.rotation.x += deltaY * 10;
            }
            // 3. OPEN HAND TO PAN
            else {
                const targetX = (handCenter.x - 0.5) * -10;
                const targetY = (handCenter.y - 0.5) * -10;
                loadedModel.position.x += (targetX - loadedModel.position.x) * 0.1;
                loadedModel.position.y += (targetY - loadedModel.position.y) * 0.1;
            }

            previousHandCenter = handCenter;
        }
    }

    function loadModel(url) {
        if (loadedModel) scene.remove(loadedModel);
        if (gridHelper) scene.remove(gridHelper);

        const loader = new GLTFLoader();
        loader.load(url, (gltf) => {
            loadedModel = gltf.scene;

            loadedModel.traverse((child) => {
                if (child.isMesh) {
                    child.material = new THREE.MeshBasicMaterial({
                        color: 0x00f3ff,
                        wireframe: true,
                        transparent: true,
                        opacity: 0.8
                    });
                }
            });

            const box = new THREE.Box3().setFromObject(loadedModel);
            const size = box.getSize(new THREE.Vector3());
            const maxDim = Math.max(size.x, size.y, size.z);
            const scale = 3 / maxDim;
            loadedModel.scale.set(scale, scale, scale);

            const center = box.getCenter(new THREE.Vector3());
            loadedModel.position.sub(center.multiplyScalar(scale));

            scene.add(loadedModel);
        });
    }

    function animate() {
        if (!active) return;
        requestAnimationFrame(animate);

        if (gridHelper && !loadedModel) {
            gridHelper.rotation.y += 0.002;
        }

        if (renderer && scene && camera) {
            renderer.render(scene, camera);
        }
    }

    function deactivate() {
        if (mpCamera) mpCamera.stop();
        if (hands) hands.close();
        if (videoElement && videoElement.srcObject) {
            videoElement.srcObject.getTracks().forEach(t => t.stop());
        }
        if (renderer && canvasContainer) canvasContainer.innerHTML = '';
        scene = null;
        loadedModel = null;
    }

    onDestroy(() => {
        deactivate();
    });
</script>

<style>
    .overlay { position: absolute; top: 0; left: 0; width: 100%; height: 100%; z-index: 50; }
    #camera-video { position: absolute; bottom: 20px; right: 20px; width: 320px; height: 240px; border: 2px solid #00f3ff; opacity: 0.8; transform: scaleX(-1); z-index: 60;}
    #three-canvas-container { position: absolute; top: 0; left: 0; width: 100%; height: 100%; z-index: 55; pointer-events: none;}
    .loading-overlay { position: absolute; top: 50%; left: 50%; transform: translate(-50%, -50%); text-align: center; color: #00f3ff; background: rgba(0,0,0,0.8); padding: 20px; border: 1px solid #00f3ff; box-shadow: 0 0 20px rgba(0, 243, 255, 0.4); z-index: 70;}
</style>

{#if active}
    <div class="overlay">
        <div id="three-canvas-container" bind:this={canvasContainer}></div>
        <video id="camera-video" bind:this={videoElement} autoplay playsinline></video>

        {#if loading}
            <div class="loading-overlay">
                <h2>{loadingText}</h2>
            </div>
        {/if}
    </div>
{/if}
