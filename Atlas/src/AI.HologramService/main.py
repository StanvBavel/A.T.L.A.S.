import os
import uuid
import torch
import trimesh
from fastapi import FastAPI, HTTPException, BackgroundTasks
from fastapi.responses import FileResponse
from pydantic import BaseModel
from shap_e.diffusion.sample import sample_latents
from shap_e.diffusion.gaussian_diffusion import diffusion_from_config
from shap_e.models.download import load_model, load_config
from shap_e.util.notebooks import decode_latent_mesh

app = FastAPI(title="A.T.L.A.S. Local Hologram Service")

# Setup device
device = torch.device('cuda' if torch.cuda.is_available() else 'cpu')
print(f"[INIT] Loading Shap-E 3D models on {device}...")

# Load models globally
xm = load_model('transmitter', device=device)
model = load_model('text300M', device=device)
diffusion = diffusion_from_config(load_config('diffusion'))

# In-memory task tracking for MVP. In production, use Redis/Celery.
tasks = {}
OUTPUT_DIR = "/app/output"
os.makedirs(OUTPUT_DIR, exist_ok=True)

class GenerateRequest(BaseModel):
    prompt: str

def generate_mesh_background(task_id: str, prompt: str):
    print(f"[3D_SERVICE] Starting generation for Task ID: {task_id}, Prompt: '{prompt}'")
    tasks[task_id] = "PROCESSING"

    try:
        batch_size = 1
        guidance_scale = 15.0

        latents = sample_latents(
            batch_size=batch_size,
            model=model,
            diffusion=diffusion,
            guidance_scale=guidance_scale,
            model_kwargs=dict(texts=[prompt] * batch_size),
            progress=True,
            clip_denoised=True,
            use_fp16=True,
            use_karras=True,
            karras_steps=64,
            sigma_min=1e-3,
            sigma_max=160,
            s_churn=0,
        )

        # Decode first latent into a mesh
        mesh_obj = decode_latent_mesh(xm, latents[0]).triangles_vertex_colors()

        # Shap-E returns a custom obj structure, we convert it to a standard file using trimesh
        output_path = os.path.join(OUTPUT_DIR, f"{task_id}.obj")

        with open(output_path, 'w') as f:
            f.write(mesh_obj)

        print(f"[3D_SERVICE] Task {task_id} SUCCEEDED. Saved to {output_path}")
        tasks[task_id] = "SUCCEEDED"

    except Exception as e:
        print(f"[3D_SERVICE] Task {task_id} FAILED: {e}")
        tasks[task_id] = "FAILED"

@app.post("/generate-3d")
async def generate_3d(req: GenerateRequest, background_tasks: BackgroundTasks):
    task_id = str(uuid.uuid4())
    tasks[task_id] = "PENDING"

    # Run heavy 3D generation in background
    background_tasks.add_task(generate_mesh_background, task_id, req.prompt)

    return {"task_id": task_id, "status": "PENDING"}

@app.get("/tasks/{task_id}")
async def get_task_status(task_id: str):
    if task_id not in tasks:
        raise HTTPException(status_code=404, detail="Task not found")

    status = tasks[task_id]

    if status == "SUCCEEDED":
        # Using a relative path for the frontend to download the file directly via this API
        return {
            "status": status,
            "obj_url": f"http://localhost:5000/download/{task_id}.obj"
        }

    return {"status": status}

@app.get("/download/{filename}")
async def download_file(filename: str):
    file_path = os.path.join(OUTPUT_DIR, filename)
    if not os.path.exists(file_path):
        raise HTTPException(status_code=404, detail="File not found")
    return FileResponse(file_path)

@app.get("/health")
def health_check():
    return {"status": "healthy", "device": str(device)}
