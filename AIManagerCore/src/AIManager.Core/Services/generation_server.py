#!/usr/bin/env python3
"""
PostXAgent Diffusers Generation Server - Production Version
A FastAPI-based server for image/video generation using HuggingFace Diffusers
with full scheduler support, LoRA loading, progress streaming, and VRAM optimization.
"""

import argparse
import asyncio
import base64
import gc
import io
import json
import os
import sys
import time
import traceback
from contextlib import asynccontextmanager
from typing import Optional, Dict, Any, List
from threading import Lock

# Check for required packages
MISSING_PACKAGES = []

try:
    import torch
except ImportError:
    MISSING_PACKAGES.append("torch")

try:
    from PIL import Image
except ImportError:
    MISSING_PACKAGES.append("pillow")

try:
    from fastapi import FastAPI, HTTPException, BackgroundTasks
    from fastapi.middleware.cors import CORSMiddleware
    from fastapi.responses import JSONResponse, StreamingResponse
    from pydantic import BaseModel, Field
    import uvicorn
except ImportError:
    MISSING_PACKAGES.append("fastapi uvicorn pydantic")

try:
    from diffusers import (
        StableDiffusionPipeline,
        StableDiffusionXLPipeline,
        StableDiffusionImg2ImgPipeline,
        StableDiffusionXLImg2ImgPipeline,
        StableVideoDiffusionPipeline,
        DiffusionPipeline,
        AutoPipelineForText2Image,
        AutoPipelineForImage2Image,
        # Schedulers
        DDIMScheduler,
        DDPMScheduler,
        PNDMScheduler,
        EulerDiscreteScheduler,
        EulerAncestralDiscreteScheduler,
        DPMSolverMultistepScheduler,
        DPMSolverSinglestepScheduler,
        HeunDiscreteScheduler,
        KDPM2DiscreteScheduler,
        KDPM2AncestralDiscreteScheduler,
        LMSDiscreteScheduler,
        UniPCMultistepScheduler,
    )
    HAS_DIFFUSERS = True
except ImportError:
    HAS_DIFFUSERS = False
    MISSING_PACKAGES.append("diffusers transformers accelerate safetensors")

if MISSING_PACKAGES:
    print(f"ERROR: Missing required packages: {', '.join(MISSING_PACKAGES)}")
    print(f"Install with: pip install {' '.join(MISSING_PACKAGES)}")
    sys.exit(1)


# ============================================================================
# Pydantic Models
# ============================================================================

class LoadModelRequest(BaseModel):
    model_id: str
    model_type: str = "TextToImage"
    precision: str = "fp16"
    enable_offload: bool = False
    enable_attention_slicing: bool = True
    enable_vae_slicing: bool = True
    enable_vae_tiling: bool = False

class UnloadModelRequest(BaseModel):
    clear_cache: bool = True

class ImageGenerationRequest(BaseModel):
    prompt: str
    negative_prompt: Optional[str] = ""
    width: int = Field(default=1024, ge=256, le=2048)
    height: int = Field(default=1024, ge=256, le=2048)
    steps: int = Field(default=30, ge=1, le=150)
    guidance_scale: float = Field(default=7.5, ge=1.0, le=30.0)
    seed: int = -1
    batch_size: int = Field(default=1, ge=1, le=4)
    scheduler: Optional[str] = None
    clip_skip: int = Field(default=1, ge=1, le=4)
    # LoRA support
    lora_models: Optional[List[Dict[str, Any]]] = None

class Img2ImgRequest(ImageGenerationRequest):
    image: str  # Base64 encoded image
    strength: float = Field(default=0.75, ge=0.0, le=1.0)

class VideoGenerationRequest(BaseModel):
    image: str  # Base64 encoded image
    num_frames: int = Field(default=25, ge=14, le=50)
    fps: int = Field(default=7, ge=1, le=30)
    motion_bucket_id: int = Field(default=127, ge=1, le=255)
    noise_aug_strength: float = Field(default=0.02, ge=0.0, le=1.0)
    seed: int = -1

class LoraInfo(BaseModel):
    path: str
    weight: float = Field(default=1.0, ge=0.0, le=2.0)
    name: Optional[str] = None


# ============================================================================
# Scheduler Registry
# ============================================================================

SCHEDULER_REGISTRY = {
    "ddim": DDIMScheduler,
    "ddpm": DDPMScheduler,
    "pndm": PNDMScheduler,
    "euler": EulerDiscreteScheduler,
    "euler_a": EulerAncestralDiscreteScheduler,
    "euler_ancestral": EulerAncestralDiscreteScheduler,
    "dpm++_2m": DPMSolverMultistepScheduler,
    "dpm++_2m_karras": lambda config: DPMSolverMultistepScheduler.from_config(config, use_karras_sigmas=True),
    "dpm++_2s": DPMSolverSinglestepScheduler,
    "dpm++_sde": lambda config: DPMSolverMultistepScheduler.from_config(config, algorithm_type="sde-dpmsolver++"),
    "dpm++_sde_karras": lambda config: DPMSolverMultistepScheduler.from_config(config, algorithm_type="sde-dpmsolver++", use_karras_sigmas=True),
    "heun": HeunDiscreteScheduler,
    "kdpm2": KDPM2DiscreteScheduler,
    "kdpm2_a": KDPM2AncestralDiscreteScheduler,
    "lms": LMSDiscreteScheduler,
    "unipc": UniPCMultistepScheduler,
}


# ============================================================================
# Generation Engine
# ============================================================================

class DiffusersEngine:
    def __init__(self, models_dir: str, low_vram_mode: bool = False):
        self.models_dir = models_dir
        self.low_vram_mode = low_vram_mode
        self.device = "cuda" if torch.cuda.is_available() else "cpu"
        self.dtype = torch.float16 if self.device == "cuda" else torch.float32

        self.pipeline = None
        self.current_model: Optional[str] = None
        self.current_model_type: Optional[str] = None
        self.loaded_loras: List[str] = []

        self._lock = Lock()
        self._generation_progress = 0
        self._generation_step = 0
        self._generation_total_steps = 0

        print(f"[Engine] Initialized")
        print(f"[Engine] Device: {self.device}")
        print(f"[Engine] Dtype: {self.dtype}")
        print(f"[Engine] Models directory: {models_dir}")
        print(f"[Engine] Low VRAM mode: {low_vram_mode}")

        if self.device == "cuda":
            props = torch.cuda.get_device_properties(0)
            print(f"[Engine] GPU: {props.name}")
            print(f"[Engine] Total VRAM: {props.total_memory / 1024**3:.1f} GB")

    def _get_model_path(self, model_id: str):
        """Get model path and whether it's local. Returns (path, is_local) tuple."""
        # Check for local model in various locations
        local_paths = [
            os.path.join(self.models_dir, "checkpoints", model_id.replace("/", "--")),
            os.path.join(self.models_dir, "checkpoints", model_id.replace("/", "_")),
            os.path.join(self.models_dir, model_id.replace("/", "--")),
            os.path.join(self.models_dir, model_id),
        ]

        for path in local_paths:
            if os.path.exists(path):
                print(f"[Engine] Found local model at: {path}")
                return path, True

        # Return model_id for HuggingFace download
        print(f"[Engine] Model not found locally, will download from HuggingFace: {model_id}")
        return model_id, False

    def _set_scheduler(self, scheduler_name: str):
        """Set the scheduler for the pipeline."""
        if scheduler_name is None or self.pipeline is None:
            return

        scheduler_name = scheduler_name.lower().replace(" ", "_").replace("-", "_")

        if scheduler_name not in SCHEDULER_REGISTRY:
            print(f"[Engine] Unknown scheduler: {scheduler_name}, keeping default")
            return

        scheduler_class = SCHEDULER_REGISTRY[scheduler_name]

        try:
            if callable(scheduler_class) and not isinstance(scheduler_class, type):
                # It's a factory function
                self.pipeline.scheduler = scheduler_class(self.pipeline.scheduler.config)
            else:
                # It's a class
                self.pipeline.scheduler = scheduler_class.from_config(self.pipeline.scheduler.config)
            print(f"[Engine] Scheduler set to: {scheduler_name}")
        except Exception as e:
            print(f"[Engine] Failed to set scheduler {scheduler_name}: {e}")

    def _apply_optimizations(self, enable_attention_slicing: bool, enable_vae_slicing: bool,
                            enable_vae_tiling: bool, enable_offload: bool):
        """Apply memory optimizations to the pipeline."""
        if self.pipeline is None:
            return

        if enable_attention_slicing and hasattr(self.pipeline, "enable_attention_slicing"):
            self.pipeline.enable_attention_slicing()
            print("[Engine] Attention slicing enabled")

        if enable_vae_slicing and hasattr(self.pipeline, "enable_vae_slicing"):
            self.pipeline.enable_vae_slicing()
            print("[Engine] VAE slicing enabled")

        if enable_vae_tiling and hasattr(self.pipeline, "enable_vae_tiling"):
            self.pipeline.enable_vae_tiling()
            print("[Engine] VAE tiling enabled")

        if enable_offload and self.device == "cuda":
            if hasattr(self.pipeline, "enable_sequential_cpu_offload"):
                self.pipeline.enable_sequential_cpu_offload()
                print("[Engine] Sequential CPU offload enabled")
            elif hasattr(self.pipeline, "enable_model_cpu_offload"):
                self.pipeline.enable_model_cpu_offload()
                print("[Engine] Model CPU offload enabled")

    def load_model(self, request: LoadModelRequest) -> Dict[str, Any]:
        """Load a diffusion model."""
        with self._lock:
            try:
                # Unload current model first
                if self.pipeline is not None:
                    self.unload_model()

                model_path, is_local = self._get_model_path(request.model_id)

                # Determine dtype
                dtype = torch.float32
                if request.precision == "fp16" and self.device == "cuda":
                    dtype = torch.float16
                elif request.precision == "bf16" and self.device == "cuda":
                    dtype = torch.bfloat16

                print(f"[Engine] Loading model: {request.model_id}")
                print(f"[Engine] Model type: {request.model_type}")
                print(f"[Engine] Precision: {request.precision} -> {dtype}")

                load_start = time.time()

                # Load based on model type
                model_id_lower = request.model_id.lower()

                if request.model_type in ["TextToImage", "txt2img"]:
                    if "xl" in model_id_lower or "sdxl" in model_id_lower:
                        self.pipeline = StableDiffusionXLPipeline.from_pretrained(
                            model_path,
                            torch_dtype=dtype,
                            use_safetensors=True,
                            local_files_only=is_local,
                            variant="fp16" if dtype == torch.float16 else None,
                        )
                    elif "flux" in model_id_lower:
                        self.pipeline = DiffusionPipeline.from_pretrained(
                            model_path,
                            torch_dtype=dtype,
                            local_files_only=is_local,
                        )
                    else:
                        # Try AutoPipeline first, fallback to SD pipeline
                        try:
                            self.pipeline = AutoPipelineForText2Image.from_pretrained(
                                model_path,
                                torch_dtype=dtype,
                                use_safetensors=True,
                                local_files_only=is_local,
                            )
                        except Exception:
                            self.pipeline = StableDiffusionPipeline.from_pretrained(
                                model_path,
                                torch_dtype=dtype,
                                use_safetensors=True,
                                local_files_only=is_local,
                            )

                elif request.model_type in ["ImageToImage", "img2img"]:
                    if "xl" in model_id_lower or "sdxl" in model_id_lower:
                        self.pipeline = StableDiffusionXLImg2ImgPipeline.from_pretrained(
                            model_path,
                            torch_dtype=dtype,
                            use_safetensors=True,
                            local_files_only=is_local,
                        )
                    else:
                        self.pipeline = AutoPipelineForImage2Image.from_pretrained(
                            model_path,
                            torch_dtype=dtype,
                            use_safetensors=True,
                            local_files_only=is_local,
                        )

                elif request.model_type in ["TextToVideo", "ImageToVideo", "video"]:
                    self.pipeline = StableVideoDiffusionPipeline.from_pretrained(
                        model_path,
                        torch_dtype=dtype,
                        local_files_only=is_local,
                    )

                else:
                    # Default to AutoPipeline
                    self.pipeline = AutoPipelineForText2Image.from_pretrained(
                        model_path,
                        torch_dtype=dtype,
                        use_safetensors=True,
                        local_files_only=is_local,
                    )

                # Move to device (unless offloading)
                if not request.enable_offload:
                    self.pipeline = self.pipeline.to(self.device)

                # Apply optimizations
                self._apply_optimizations(
                    request.enable_attention_slicing,
                    request.enable_vae_slicing,
                    request.enable_vae_tiling,
                    request.enable_offload,
                )

                self.current_model = request.model_id
                self.current_model_type = request.model_type
                self.loaded_loras = []

                load_time = time.time() - load_start

                # Get memory usage
                vram_used = 0
                if self.device == "cuda":
                    vram_used = torch.cuda.memory_allocated() / 1024**3

                print(f"[Engine] Model loaded in {load_time:.1f}s, VRAM used: {vram_used:.1f} GB")

                return {
                    "success": True,
                    "model": request.model_id,
                    "load_time": load_time,
                    "vram_used_gb": vram_used,
                }

            except Exception as e:
                traceback.print_exc()
                return {"success": False, "error": str(e)}

    def unload_model(self, clear_cache: bool = True):
        """Unload current model and free memory."""
        with self._lock:
            if self.pipeline is not None:
                del self.pipeline
                self.pipeline = None

            self.current_model = None
            self.current_model_type = None
            self.loaded_loras = []

            gc.collect()

            if clear_cache and torch.cuda.is_available():
                torch.cuda.empty_cache()
                torch.cuda.synchronize()

            print("[Engine] Model unloaded")

    def load_lora(self, lora_path: str, weight: float = 1.0, adapter_name: Optional[str] = None):
        """Load a LoRA adapter."""
        if self.pipeline is None:
            raise ValueError("No model loaded")

        if not hasattr(self.pipeline, "load_lora_weights"):
            raise ValueError("Current pipeline does not support LoRA")

        adapter_name = adapter_name or os.path.basename(lora_path).replace(".safetensors", "")

        try:
            self.pipeline.load_lora_weights(lora_path, adapter_name=adapter_name)
            self.pipeline.set_adapters([adapter_name], adapter_weights=[weight])
            self.loaded_loras.append(adapter_name)
            print(f"[Engine] LoRA loaded: {adapter_name} (weight: {weight})")
        except Exception as e:
            print(f"[Engine] Failed to load LoRA {lora_path}: {e}")
            raise

    def unload_loras(self):
        """Unload all LoRA adapters."""
        if self.pipeline is not None and hasattr(self.pipeline, "unload_lora_weights"):
            self.pipeline.unload_lora_weights()
            self.loaded_loras = []
            print("[Engine] All LoRAs unloaded")

    def _progress_callback(self, step: int, timestep: int, latents: torch.Tensor):
        """Callback for generation progress."""
        self._generation_step = step
        if self._generation_total_steps > 0:
            self._generation_progress = int((step / self._generation_total_steps) * 100)
        print(f"Step {step}/{self._generation_total_steps}")

    def generate_image(self, request: ImageGenerationRequest) -> Dict[str, Any]:
        """Generate image(s) from text prompt."""
        if self.pipeline is None:
            return {"success": False, "error": "No model loaded"}

        with self._lock:
            try:
                # Reset progress
                self._generation_progress = 0
                self._generation_step = 0
                self._generation_total_steps = request.steps

                # Set scheduler if specified
                if request.scheduler:
                    self._set_scheduler(request.scheduler)

                # Load LoRAs if specified
                if request.lora_models:
                    self.unload_loras()
                    for lora in request.lora_models:
                        self.load_lora(lora.get("path"), lora.get("weight", 1.0), lora.get("name"))

                # Generate seed
                if request.seed >= 0:
                    seed = request.seed
                else:
                    seed = int(torch.randint(0, 2**32 - 1, (1,)).item())

                generator = torch.Generator(device=self.device).manual_seed(seed)

                print(f"[Engine] Generating image:")
                print(f"  Prompt: {request.prompt[:100]}...")
                print(f"  Size: {request.width}x{request.height}")
                print(f"  Steps: {request.steps}")
                print(f"  Guidance: {request.guidance_scale}")
                print(f"  Seed: {seed}")

                start_time = time.time()

                # Build generation kwargs
                gen_kwargs = {
                    "prompt": request.prompt,
                    "width": request.width,
                    "height": request.height,
                    "num_inference_steps": request.steps,
                    "guidance_scale": request.guidance_scale,
                    "num_images_per_prompt": request.batch_size,
                    "generator": generator,
                    "callback": self._progress_callback,
                    "callback_steps": 1,
                }

                if request.negative_prompt:
                    gen_kwargs["negative_prompt"] = request.negative_prompt

                # CLIP skip (only for SD 1.x pipelines)
                if request.clip_skip > 1 and hasattr(self.pipeline, "text_encoder"):
                    try:
                        if hasattr(self.pipeline.text_encoder.config, "num_hidden_layers"):
                            orig_layers = self.pipeline.text_encoder.config.num_hidden_layers
                            self.pipeline.text_encoder.config.num_hidden_layers = orig_layers - (request.clip_skip - 1)
                    except Exception as e:
                        print(f"[Engine] CLIP skip not supported: {e}")

                # Generate!
                result = self.pipeline(**gen_kwargs)

                gen_time = time.time() - start_time

                # Encode images to base64
                images = []
                for img in result.images:
                    buffer = io.BytesIO()
                    img.save(buffer, format="PNG", optimize=True)
                    b64 = base64.b64encode(buffer.getvalue()).decode("utf-8")
                    images.append(f"data:image/png;base64,{b64}")

                # Get VRAM usage
                vram_used = 0
                if self.device == "cuda":
                    vram_used = torch.cuda.memory_allocated() / 1024**3

                print(f"[Engine] Generation complete in {gen_time:.1f}s")

                return {
                    "success": True,
                    "images": images,
                    "seed": seed,
                    "generation_time": gen_time,
                    "vram_used_gb": vram_used,
                }

            except Exception as e:
                traceback.print_exc()
                return {"success": False, "error": str(e)}

    def generate_img2img(self, request: Img2ImgRequest) -> Dict[str, Any]:
        """Generate image from image + prompt."""
        if self.pipeline is None:
            return {"success": False, "error": "No model loaded"}

        with self._lock:
            try:
                # Decode input image
                image_data = request.image
                if image_data.startswith("data:"):
                    image_data = image_data.split(",")[1]

                input_image = Image.open(io.BytesIO(base64.b64decode(image_data)))
                input_image = input_image.convert("RGB")
                input_image = input_image.resize((request.width, request.height))

                # Generate seed
                if request.seed >= 0:
                    seed = request.seed
                else:
                    seed = int(torch.randint(0, 2**32 - 1, (1,)).item())

                generator = torch.Generator(device=self.device).manual_seed(seed)

                print(f"[Engine] Generating img2img:")
                print(f"  Prompt: {request.prompt[:100]}...")
                print(f"  Strength: {request.strength}")
                print(f"  Seed: {seed}")

                start_time = time.time()

                gen_kwargs = {
                    "prompt": request.prompt,
                    "image": input_image,
                    "strength": request.strength,
                    "num_inference_steps": request.steps,
                    "guidance_scale": request.guidance_scale,
                    "num_images_per_prompt": request.batch_size,
                    "generator": generator,
                }

                if request.negative_prompt:
                    gen_kwargs["negative_prompt"] = request.negative_prompt

                result = self.pipeline(**gen_kwargs)

                gen_time = time.time() - start_time

                images = []
                for img in result.images:
                    buffer = io.BytesIO()
                    img.save(buffer, format="PNG", optimize=True)
                    b64 = base64.b64encode(buffer.getvalue()).decode("utf-8")
                    images.append(f"data:image/png;base64,{b64}")

                return {
                    "success": True,
                    "images": images,
                    "seed": seed,
                    "generation_time": gen_time,
                }

            except Exception as e:
                traceback.print_exc()
                return {"success": False, "error": str(e)}

    def generate_video(self, request: VideoGenerationRequest) -> Dict[str, Any]:
        """Generate video from image."""
        if self.pipeline is None:
            return {"success": False, "error": "No model loaded"}

        if not isinstance(self.pipeline, StableVideoDiffusionPipeline):
            return {"success": False, "error": "Video generation requires a video model (SVD)"}

        with self._lock:
            try:
                # Decode input image
                image_data = request.image
                if image_data.startswith("data:"):
                    image_data = image_data.split(",")[1]

                input_image = Image.open(io.BytesIO(base64.b64decode(image_data)))
                input_image = input_image.convert("RGB")
                input_image = input_image.resize((1024, 576))  # SVD requires this size

                # Generate seed
                if request.seed >= 0:
                    seed = request.seed
                else:
                    seed = int(torch.randint(0, 2**32 - 1, (1,)).item())

                generator = torch.Generator(device=self.device).manual_seed(seed)

                print(f"[Engine] Generating video:")
                print(f"  Frames: {request.num_frames}")
                print(f"  FPS: {request.fps}")
                print(f"  Seed: {seed}")

                start_time = time.time()

                result = self.pipeline(
                    input_image,
                    num_frames=request.num_frames,
                    motion_bucket_id=request.motion_bucket_id,
                    noise_aug_strength=request.noise_aug_strength,
                    generator=generator,
                )

                gen_time = time.time() - start_time

                # Encode frames to base64
                frames = []
                for frame in result.frames[0]:
                    buffer = io.BytesIO()
                    frame.save(buffer, format="PNG", optimize=True)
                    b64 = base64.b64encode(buffer.getvalue()).decode("utf-8")
                    frames.append(f"data:image/png;base64,{b64}")

                print(f"[Engine] Video generation complete in {gen_time:.1f}s")

                return {
                    "success": True,
                    "frames": frames,
                    "fps": request.fps,
                    "seed": seed,
                    "generation_time": gen_time,
                }

            except Exception as e:
                traceback.print_exc()
                return {"success": False, "error": str(e)}

    def get_info(self) -> Dict[str, Any]:
        """Get engine status information."""
        gpu_info = {}
        if torch.cuda.is_available():
            props = torch.cuda.get_device_properties(0)
            allocated = torch.cuda.memory_allocated() / 1024**3
            reserved = torch.cuda.memory_reserved() / 1024**3
            total = props.total_memory / 1024**3

            gpu_info = {
                "name": props.name,
                "total_memory_gb": total,
                "allocated_memory_gb": allocated,
                "reserved_memory_gb": reserved,
                "free_memory_gb": total - allocated,
                "utilization_percent": (allocated / total) * 100 if total > 0 else 0,
            }

        return {
            "status": "ready" if self.pipeline else "idle",
            "device": self.device,
            "dtype": str(self.dtype),
            "current_model": self.current_model,
            "current_model_type": self.current_model_type,
            "loaded_loras": self.loaded_loras,
            "gpu": gpu_info,
            "has_diffusers": HAS_DIFFUSERS,
            "available_schedulers": list(SCHEDULER_REGISTRY.keys()),
            "generation_progress": self._generation_progress,
        }

    def get_vram_usage(self) -> Dict[str, Any]:
        """Get detailed VRAM usage."""
        if not torch.cuda.is_available():
            return {"available": False}

        props = torch.cuda.get_device_properties(0)
        allocated = torch.cuda.memory_allocated()
        reserved = torch.cuda.memory_reserved()
        total = props.total_memory

        return {
            "available": True,
            "total_mb": total / 1024**2,
            "allocated_mb": allocated / 1024**2,
            "reserved_mb": reserved / 1024**2,
            "free_mb": (total - allocated) / 1024**2,
            "utilization_percent": (allocated / total) * 100 if total > 0 else 0,
        }


# ============================================================================
# FastAPI Application
# ============================================================================

engine: Optional[DiffusersEngine] = None

@asynccontextmanager
async def lifespan(app: FastAPI):
    """Application lifespan manager."""
    yield
    # Cleanup on shutdown
    if engine:
        engine.unload_model()

app = FastAPI(
    title="PostXAgent Diffusers Server",
    description="Production-grade image/video generation server",
    version="2.0.0",
    lifespan=lifespan,
)

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)


@app.get("/health")
async def health_check():
    """Health check endpoint."""
    return {"status": "ok", "server": "PostXAgent Diffusers Server", "version": "2.0.0"}


@app.get("/info")
async def get_info():
    """Get engine information."""
    if engine is None:
        raise HTTPException(status_code=500, detail="Engine not initialized")
    return engine.get_info()


@app.get("/vram")
async def get_vram():
    """Get VRAM usage."""
    if engine is None:
        raise HTTPException(status_code=500, detail="Engine not initialized")
    return engine.get_vram_usage()


@app.post("/load-model")
async def load_model(request: LoadModelRequest):
    """Load a diffusion model."""
    if engine is None:
        raise HTTPException(status_code=500, detail="Engine not initialized")
    result = engine.load_model(request)
    if not result.get("success"):
        raise HTTPException(status_code=400, detail=result.get("error", "Unknown error"))
    return result


@app.post("/unload-model")
async def unload_model(request: UnloadModelRequest = None):
    """Unload current model."""
    if engine is None:
        raise HTTPException(status_code=500, detail="Engine not initialized")
    clear_cache = request.clear_cache if request else True
    engine.unload_model(clear_cache)
    return {"success": True}


@app.post("/generate/image")
async def generate_image(request: ImageGenerationRequest):
    """Generate image from text."""
    if engine is None:
        raise HTTPException(status_code=500, detail="Engine not initialized")
    result = engine.generate_image(request)
    if not result.get("success"):
        raise HTTPException(status_code=400, detail=result.get("error", "Unknown error"))
    return result


@app.post("/generate/img2img")
async def generate_img2img(request: Img2ImgRequest):
    """Generate image from image."""
    if engine is None:
        raise HTTPException(status_code=500, detail="Engine not initialized")
    result = engine.generate_img2img(request)
    if not result.get("success"):
        raise HTTPException(status_code=400, detail=result.get("error", "Unknown error"))
    return result


@app.post("/generate/video")
async def generate_video(request: VideoGenerationRequest):
    """Generate video from image."""
    if engine is None:
        raise HTTPException(status_code=500, detail="Engine not initialized")
    result = engine.generate_video(request)
    if not result.get("success"):
        raise HTTPException(status_code=400, detail=result.get("error", "Unknown error"))
    return result


@app.post("/lora/load")
async def load_lora(lora: LoraInfo):
    """Load a LoRA adapter."""
    if engine is None:
        raise HTTPException(status_code=500, detail="Engine not initialized")
    try:
        engine.load_lora(lora.path, lora.weight, lora.name)
        return {"success": True, "loaded_loras": engine.loaded_loras}
    except Exception as e:
        raise HTTPException(status_code=400, detail=str(e))


@app.post("/lora/unload")
async def unload_loras():
    """Unload all LoRA adapters."""
    if engine is None:
        raise HTTPException(status_code=500, detail="Engine not initialized")
    engine.unload_loras()
    return {"success": True}


@app.get("/schedulers")
async def list_schedulers():
    """List available schedulers."""
    return {"schedulers": list(SCHEDULER_REGISTRY.keys())}


@app.post("/shutdown")
async def shutdown():
    """Shutdown the server."""
    import threading

    def do_shutdown():
        import time
        time.sleep(0.5)
        os._exit(0)

    threading.Thread(target=do_shutdown, daemon=True).start()
    return {"status": "shutting down"}


# ============================================================================
# Main Entry Point
# ============================================================================

def main():
    global engine

    parser = argparse.ArgumentParser(description="PostXAgent Diffusers Generation Server")
    parser.add_argument("--port", type=int, default=5050, help="Server port")
    parser.add_argument("--models-dir", type=str, required=True, help="Models directory")
    parser.add_argument("--host", type=str, default="0.0.0.0", help="Host to bind")
    parser.add_argument("--low-vram", action="store_true", help="Enable low VRAM mode")
    args = parser.parse_args()

    # Initialize engine
    engine = DiffusersEngine(args.models_dir, low_vram_mode=args.low_vram)

    print(f"[Server] Starting on {args.host}:{args.port}")
    print(f"[Server] Models directory: {args.models_dir}")

    # Run server
    uvicorn.run(app, host=args.host, port=args.port, log_level="info")


if __name__ == "__main__":
    main()
