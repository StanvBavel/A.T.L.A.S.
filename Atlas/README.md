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
  │                                     ├──► [ Ollama (Local LLM via Port 11434) ]
  │                                     │      (Function Calling & Intent Routing)
  │                                     │
  │                                     ├──► [ Custom Image Service (FastAPI) ]
  │                                     │      (SDXL Turbo via Port 8000)
  │                                     │
  │                                     └──► [ Custom Hologram Service (FastAPI) ]
  │                                            (Shap-E via Port 5000)
  ▼
[ USER HUD ]
```

## Infrastructure Setup
Because A.T.L.A.S. is entirely on-premise, your system acts as the entire AI cluster. You will need:
- **Docker Desktop** (for running the Python AI microservices)
- **.NET 8.0 SDK** (If you want to run the C# backend outside of Docker)
- **Node.js & NPM** (For compiling the Svelte frontend)

### 1. Build and Start the Local AI Cluster (Docker Compose)
A `docker-compose.yml` file is provided in the root directory. It builds the custom Python microservices from source.

1. Open your terminal in the repository root.
2. Execute the build and start command:
   ```bash
   docker-compose up --build -d
   ```
   *(Note: The first build will take considerable time as it downloads PyTorch, HuggingFace libraries, and the AI model weights. Subsequent runs will use the cache volumes mounted in the project directory).*

### 2. Pulling the Language Model
Once the Ollama container is running, execute the following command to pull a model highly capable of JSON Tool Calling (like `llama3.1`):
```bash
docker exec -it atlas_ollama ollama run llama3.1
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
   Navigate to the C# Backend directory and launch it:
   ```bash
   cd ../Backend/Atlas.Api
   dotnet run
   ```

3. **Engage System:**
   Open your browser to `http://localhost:5258`. When the interface loads, authorize microphone and camera access to engage the Spatial Hand Tracking and Neural Voice Link.
