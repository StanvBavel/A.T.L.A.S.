# A.T.L.A.S. (Autonomous Technological Logic & Assistance System)

A.T.L.A.S. is a fully autonomous, 100% locally-hosted AI assistant inspired by J.A.R.V.I.S. It leverages local large language models (Ollama) to parse intents, uses local HTML scraping for visual data, and connects to local Docker containers for generating 3D spatial meshes without relying on any external cloud APIs.

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
  │                                     ├──► [ Local Image Search Service ]
  │                                     │      (HtmlAgilityPack Web Scraper)
  │                                     │
  │                                     └──► [ Local 3D Generation API ]
  │                                            (Dockerized TripoSR/Shap-E via Port 5000)
  ▼
[ USER HUD ]
```

## Prerequisites & Infrastructure
Because A.T.L.A.S. is entirely on-premise, your system acts as the entire AI cluster. You will need:
- **.NET 8.0 SDK**
- **Node.js & NPM**
- **Docker Desktop** (for running the external local AI models)

### 1. Docker Compose Setup
Create a `docker-compose.yml` file in your preferred AI models directory with the following structure to host the required localized AI engines:

```yaml
version: '3.8'

services:
  ollama:
    image: ollama/ollama:latest
    container_name: atlas_ollama
    ports:
      - "11434:11434"
    volumes:
      - ./ollama_data:/root/.ollama
    restart: unless-stopped

  stable-diffusion:
    # Optional image fallback endpoint
    image: originalgarments/stable-diffusion-webui-docker
    container_name: atlas_stable_diffusion
    ports:
      - "7860:7860"
    environment:
      - COMMANDLINE_ARGS=--api --listen

  tripo-sr:
    # Example local 3D generation API (replace with specific Python API image)
    image: local-tripo-sr-api:latest
    container_name: atlas_3d_api
    ports:
      - "5000:5000"
```

Start the engines:
```bash
docker-compose up -d
```

### 2. Pulling the Language Model
Once the Ollama container is running, execute the following command to pull a model highly capable of JSON Tool Calling (like `llama3.1` or `mistral`):
```bash
docker exec -it atlas_ollama ollama run llama3.1
```

## Installation & Configuration

A.T.L.A.S. explicitly forbids the use of external cloud API keys for visual or spatial synthesis. Update your `Atlas.Api/appsettings.json` to point exclusively to localhost instances:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=atlas.sqlite"
  },
  "AiSettings": {
    "OllamaEndpoint": "http://localhost:11434/api/chat",
    "ModelName": "llama3.1"
  },
  "Local3DApi": {
    "Endpoint": "http://localhost:5000/generate-3d",
    "StatusEndpoint": "http://localhost:5000/tasks/"
  }
}
```

## Running A.T.L.A.S.

1. **Build Frontend:**
   Navigate to the `atlas-frontend` directory and build the Svelte app:
   ```bash
   cd atlas-frontend
   npm install
   npm run build
   cp -R dist/* ../Atlas.Api/wwwroot/
   ```

2. **Start Core Server:**
   Navigate to the `Atlas.Api` directory and launch the C# backend:
   ```bash
   cd ../Atlas.Api
   dotnet run
   ```

3. **Engage System:**
   Open your browser to `http://localhost:5258` (or the port specified in the console). When the interface loads, authorize microphone and camera access to engage the Spatial Hand Tracking and Neural Voice Link.
