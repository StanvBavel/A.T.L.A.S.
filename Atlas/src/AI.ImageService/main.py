import os
import io
import base64
import torch
from fastapi import FastAPI, HTTPException
from pydantic import BaseModel
from diffusers import AutoPipelineForText2Image

app = FastAPI(title="A.T.L.A.S. Local Image Service")

# Use SDXL Turbo for fast, high-quality local generation.
# It runs well on consumer GPUs and requires fewer steps.
MODEL_ID = "stabilityai/sdxl-turbo"

# Determine device (CUDA if available, otherwise CPU/MPS)
device = "cuda" if torch.cuda.is_available() else "cpu"
print(f"[INIT] Loading {MODEL_ID} on {device}...")

# Load pipeline globally to keep it in memory
try:
    pipeline = AutoPipelineForText2Image.from_pretrained(
        MODEL_ID,
        torch_dtype=torch.float16 if device == "cuda" else torch.float32,
        variant="fp16" if device == "cuda" else None,
        cache_dir="/root/.cache/huggingface" # Important: mapped via docker-compose volume
    )
    pipeline = pipeline.to(device)
    print("[INIT] Model loaded successfully.")
except Exception as e:
    print(f"[ERROR] Failed to load model: {e}")
    pipeline = None

class GenerateRequest(BaseModel):
    prompt: str

@app.post("/generate-image")
async def generate_image(req: GenerateRequest):
    if not pipeline:
        raise HTTPException(status_code=500, detail="Model pipeline is not initialized.")

    print(f"[IMAGE_SERVICE] Generating image for prompt: '{req.prompt}'")

    try:
        # Generate image (SDXL Turbo requires very few steps, typically 1 to 4)
        result = pipeline(prompt=req.prompt, num_inference_steps=2, guidance_scale=0.0).images[0]

        # Convert PIL Image to Base64 to return directly in the API response
        buffered = io.BytesIO()
        result.save(buffered, format="JPEG")
        img_str = base64.b64encode(buffered.getvalue()).decode("utf-8")

        # Construct data URI
        base64_url = f"data:image/jpeg;base64,{img_str}"

        return {"status": "SUCCEEDED", "image_url": base64_url}

    except Exception as e:
        print(f"[ERROR] Generation failed: {e}")
        raise HTTPException(status_code=500, detail=str(e))

@app.get("/health")
def health_check():
    return {"status": "healthy", "device": device, "model_loaded": pipeline is not None}
