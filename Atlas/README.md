# A.T.L.A.S. (Autonomous Technological Logic & Assistance System)

A.T.L.A.S. is a fully autonomous, 100% locally-hosted AI assistant monorepo. It leverages local large language models (Ollama) to parse intents, and custom-built local Python Docker containers for generating images and 3D spatial meshes. It strictly avoids external cloud APIs.

## System Architecture

```text
[ SVELTE FRONTEND (Vite) ] ──(SignalR)──┐
  │ - Three.js Holograms                │
  │ - MediaPipe Barehands Tracking      │
  │ - Web Speech API (STT/TTS)          ▼
  │                               [ C# .NET 8 BACKEND ]
  │                                     │
  │                                     ├──► [ Ollama (Local Windows Host via Port 11434) ]
  │                                     │      (Function Calling & Intent Routing)
  │                                     │
  │                                     ├──► [ Custom Image Service (FastAPI Docker) ]
  │                                     │      (SDXL Turbo via mapped Port 8000)
  │                                     │
  │                                     └──► [ Custom Hologram Service (FastAPI Docker) ]
  │                                            (Shap-E via mapped Port 5000)
  ▼
[ USER HUD ]
```

## Infrastructure Setup & Networking Strategy
Because A.T.L.A.S. is entirely on-premise, your system acts as the entire AI cluster.

**IMPORTANT NETWORKING NOTE:**
By default, this repository assumes a **Hybrid Execution Strategy**:
1. The **C# Backend** runs natively on your Windows Host (via `dotnet run` or Visual Studio).
2. The **Ollama LLM** runs natively on your Windows Host.
3. The **Python Microservices** run inside Docker.

Because the C# Backend runs natively, it communicates with everything via `localhost`. The Python Docker containers map their ports (`8000` and `5000`) directly to your host machine so they are accessible via `localhost`.

*(If you ever decide to run the C# Backend inside Docker instead, you must change the `appsettings.json` Ollama endpoint to `http://host.docker.internal:11434` to reach your host-OS Ollama installation).*

### 1. Build and Start the Local Python AI Cluster (Docker Compose)
A `docker-compose.yml` file is provided in the root directory. It builds the custom Python microservices from source.

1. Open your terminal in the repository root.
2. Execute the build and start command:
   ```bash
   docker-compose up --build -d
   ```
   *(Note: The first build will take considerable time as it downloads PyTorch, HuggingFace libraries, and the AI model weights. Subsequent runs will use the cache volumes mounted in the project directory).*

### 2. Install and Pull the Language Model
1. Install [Ollama](https://ollama.com/) natively on Windows.
2. Execute the following command to pull a model highly capable of JSON Tool Calling (like `llama3.1`):
   ```bash
   ollama run llama3.1
   ```

## Running A.T.L.A.S. Client

1. **Build Frontend:**
   Navigate to the Svelte directory and build the app into the C# backend:
   ```bash
   cd src/Frontend
   npm install
   npm run build
   cp -R dist/* ../Backend/Atlas.Api/wwwroot/
   ```

2. **Start Core Server:**
   Navigate to the C# Backend directory and launch it natively:
   ```bash
   cd src/Backend/Atlas.Api
   dotnet run
   ```

3. **Engage System:**
   Open your browser to `http://localhost:5258`. When the interface loads, authorize microphone and camera access to engage the Spatial Hand Tracking and Neural Voice Link.
