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
import uuid
from collections import deque
from contextlib import asynccontextmanager
from dataclasses import dataclass, field
from typing import Optional, Dict, Any, List, Callable
from threading import Lock, Event, Thread

# ============================================================================
# Startup Validation System
# ============================================================================

class StartupValidator:
    """Validates all dependencies and reports status step by step."""

    def __init__(self):
        self.steps = []
        self.errors = []
        self.warnings = []
        self.missing_features = []
        self.current_step = 0
        self.total_steps = 7

    def log_step(self, step_num: int, name: str, status: str, message: str = ""):
        """Log a validation step."""
        # Use ASCII-safe icons to avoid encoding issues on Windows cp874/cp1252
        icon = "[OK]" if status == "ok" else "[ERROR]" if status == "error" else "[WARN]" if status == "warning" else "[...]"
        step_info = f"[Step {step_num}/{self.total_steps}] {icon} {name}"
        if message:
            step_info += f": {message}"
        # Force UTF-8 encoding or fallback to ASCII with replacement
        try:
            print(step_info)
        except UnicodeEncodeError:
            print(step_info.encode('ascii', 'replace').decode('ascii'))
        self.steps.append({"step": step_num, "name": name, "status": status, "message": message})

    def validate_all(self) -> dict:
        """Run all validation steps and return results."""
        print("=" * 60)
        print("[Startup] Validating system requirements...")
        print("=" * 60)

        # Step 1: Check Python version
        self._check_python_version()

        # Step 2: Check PyTorch
        has_torch = self._check_pytorch()

        # Step 3: Check CUDA/GPU
        if has_torch:
            self._check_cuda()

        # Step 4: Check PIL
        self._check_pil()

        # Step 5: Check FastAPI
        self._check_fastapi()

        # Step 6: Check Diffusers
        self._check_diffusers()

        # Step 7: Check Optional packages
        self._check_optional_packages()

        # Summary
        print("=" * 60)
        if self.errors:
            self._safe_print(f"[Startup] [ERROR] Found {len(self.errors)} error(s) - cannot start")
            for err in self.errors:
                self._safe_print(f"  - {err}")
        elif self.warnings:
            self._safe_print(f"[Startup] [WARN] Found {len(self.warnings)} warning(s) - can run with limited features")
            for warn in self.warnings:
                self._safe_print(f"  - {warn}")
        else:
            self._safe_print("[Startup] [OK] All systems ready")
        print("=" * 60)

        return {
            "success": len(self.errors) == 0,
            "can_continue": len(self.errors) == 0,
            "errors": self.errors,
            "warnings": self.warnings,
            "missing_features": self.missing_features,
            "steps": self.steps
        }

    def _safe_print(self, text: str):
        """Print with fallback for encoding issues on Windows."""
        try:
            print(text)
        except UnicodeEncodeError:
            # Replace non-ASCII characters with ?
            print(text.encode('ascii', 'replace').decode('ascii'))

    def _check_python_version(self):
        import sys
        version = sys.version_info
        if version.major >= 3 and version.minor >= 9:
            self.log_step(1, "Python Version", "ok", f"Python {version.major}.{version.minor}.{version.micro}")
        else:
            self.log_step(1, "Python Version", "error", f"Python {version.major}.{version.minor} - requires 3.9+")
            self.errors.append(f"Python version too low (requires 3.9+, found {version.major}.{version.minor})")

    def _check_pytorch(self) -> bool:
        try:
            import torch
            self.log_step(2, "PyTorch", "ok", f"Version {torch.__version__}")
            return True
        except ImportError:
            self.log_step(2, "PyTorch", "error", "Not found - need to install")
            self.errors.append("PyTorch not installed - run: pip install torch torchvision torchaudio")
            return False

    def _check_cuda(self):
        import torch
        if torch.cuda.is_available():
            gpu_name = torch.cuda.get_device_name(0)
            vram = torch.cuda.get_device_properties(0).total_memory / (1024**3)
            self.log_step(3, "CUDA/GPU", "ok", f"{gpu_name} ({vram:.1f} GB VRAM)")
        else:
            self.log_step(3, "CUDA/GPU", "warning", "No GPU found - will use CPU (very slow)")
            self.warnings.append("No CUDA GPU - generation will be very slow")
            self.missing_features.append("GPU Acceleration")

    def _check_pil(self):
        try:
            from PIL import Image
            import PIL
            self.log_step(4, "Pillow (PIL)", "ok", f"Version {PIL.__version__}")
        except ImportError:
            self.log_step(4, "Pillow (PIL)", "error", "Not found - need to install")
            self.errors.append("Pillow not installed - run: pip install pillow")

    def _check_fastapi(self):
        try:
            import fastapi
            import uvicorn
            self.log_step(5, "FastAPI + Uvicorn", "ok", f"FastAPI {fastapi.__version__}")
        except ImportError as e:
            self.log_step(5, "FastAPI + Uvicorn", "error", f"Not found - {e}")
            self.errors.append("FastAPI/Uvicorn not installed - run: pip install fastapi uvicorn")

    def _check_diffusers(self):
        try:
            import diffusers
            self.log_step(6, "Diffusers", "ok", f"Version {diffusers.__version__}")
        except ImportError:
            self.log_step(6, "Diffusers", "error", "Not found - need to install")
            self.errors.append("Diffusers not installed - run: pip install diffusers transformers accelerate safetensors")

    def _check_optional_packages(self):
        optional_status = []

        # ControlNet Aux
        try:
            import controlnet_aux
            optional_status.append("ControlNet Aux [OK]")
        except ImportError:
            optional_status.append("ControlNet Aux [X]")
            self.missing_features.append("ControlNet Preprocessing (canny, pose, depth auto-detect)")

        # IP-Adapter
        try:
            from transformers import CLIPVisionModelWithProjection
            optional_status.append("IP-Adapter [OK]")
        except ImportError:
            optional_status.append("IP-Adapter [X]")
            self.missing_features.append("IP-Adapter (style transfer from reference image)")

        # Real-ESRGAN
        try:
            import realesrgan
            optional_status.append("Real-ESRGAN [OK]")
        except ImportError:
            optional_status.append("Real-ESRGAN [X]")
            self.missing_features.append("AI Upscaling (Real-ESRGAN)")

        # xformers
        try:
            import xformers
            optional_status.append("xformers [OK]")
        except ImportError:
            optional_status.append("xformers [X]")
            self.missing_features.append("Memory-efficient attention (xformers)")

        status_str = ", ".join(optional_status)
        if self.missing_features:
            self.log_step(7, "Optional Packages", "warning", status_str)
            self.warnings.append(f"Missing optional packages: {', '.join(self.missing_features)}")
        else:
            self.log_step(7, "Optional Packages", "ok", status_str)


# Run validation at import time
print()  # Empty line for readability
_validator = StartupValidator()
_validation_result = _validator.validate_all()

# Store for later access
STARTUP_VALIDATION = _validation_result
MISSING_FEATURES = _validation_result.get("missing_features", [])

# Exit if critical errors
if not _validation_result["success"]:
    print("\n[ERROR] Cannot start server - missing required packages")
    print("[ERROR] Please install the packages listed above and try again")
    sys.exit(1)

# Now import the packages (we know they exist)
import torch
from PIL import Image
from fastapi import FastAPI, HTTPException, BackgroundTasks
from fastapi.middleware.cors import CORSMiddleware
from fastapi.responses import JSONResponse, StreamingResponse
from pydantic import BaseModel, Field
import uvicorn

from diffusers import (
    StableDiffusionPipeline,
    StableDiffusionXLPipeline,
    StableDiffusionImg2ImgPipeline,
    StableDiffusionXLImg2ImgPipeline,
    StableVideoDiffusionPipeline,
    DiffusionPipeline,
    AutoPipelineForText2Image,
    AutoPipelineForImage2Image,
    # ControlNet
    StableDiffusionControlNetPipeline,
    StableDiffusionXLControlNetPipeline,
    ControlNetModel,
    # Inpainting
    StableDiffusionInpaintPipeline,
    StableDiffusionXLInpaintPipeline,
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

# ControlNet preprocessors (optional)
try:
    from controlnet_aux import (
        CannyDetector,
        OpenposeDetector,
        MidasDetector,
        HEDdetector,
        LineartDetector,
        PidiNetDetector,
    )
    HAS_CONTROLNET_AUX = True
except ImportError:
    HAS_CONTROLNET_AUX = False

# IP-Adapter support (optional)
try:
    from transformers import CLIPVisionModelWithProjection, CLIPImageProcessor
    HAS_IP_ADAPTER = True
except ImportError:
    HAS_IP_ADAPTER = False


# ============================================================================
# Pydantic Models
# ============================================================================

class LoadModelRequest(BaseModel):
    model_id: str
    model_type: str = "TextToImage"
    precision: str = "fp16"
    enable_offload: bool = True  # Enable CPU offload by default for low VRAM GPUs
    enable_attention_slicing: bool = True
    enable_vae_slicing: bool = True
    enable_vae_tiling: bool = True  # Enable VAE tiling by default for large images

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


class ControlNetRequest(BaseModel):
    """ControlNet generation request - supports multiple control types"""
    prompt: str
    negative_prompt: Optional[str] = ""
    # Control image (base64 encoded) - can be raw image or pre-processed
    control_image: str
    # ControlNet type: canny, pose, depth, hed, lineart, scribble, softedge, normal, tile
    control_type: str = "canny"
    # ControlNet model ID (auto-detected if not specified)
    controlnet_model: Optional[str] = None
    # Whether to preprocess the control image (auto-detect edges, pose, etc.)
    preprocess: bool = True
    # Preprocessing parameters
    canny_low: int = Field(default=100, ge=0, le=255)
    canny_high: int = Field(default=200, ge=0, le=255)
    # Generation parameters
    width: int = Field(default=1024, ge=256, le=2048)
    height: int = Field(default=1024, ge=256, le=2048)
    steps: int = Field(default=30, ge=1, le=150)
    guidance_scale: float = Field(default=7.5, ge=1.0, le=30.0)
    controlnet_conditioning_scale: float = Field(default=1.0, ge=0.0, le=2.0)
    seed: int = -1
    batch_size: int = Field(default=1, ge=1, le=4)
    scheduler: Optional[str] = None
    clip_skip: int = Field(default=1, ge=1, le=4)
    lora_models: Optional[List[Dict[str, Any]]] = None


class MultiControlNetRequest(BaseModel):
    """Multi-ControlNet generation - combine multiple control images"""
    prompt: str
    negative_prompt: Optional[str] = ""
    # List of control conditions
    controls: List[Dict[str, Any]]  # Each has: control_image, control_type, weight
    width: int = Field(default=1024, ge=256, le=2048)
    height: int = Field(default=1024, ge=256, le=2048)
    steps: int = Field(default=30, ge=1, le=150)
    guidance_scale: float = Field(default=7.5, ge=1.0, le=30.0)
    seed: int = -1
    batch_size: int = Field(default=1, ge=1, le=4)
    scheduler: Optional[str] = None
    lora_models: Optional[List[Dict[str, Any]]] = None


class InpaintRequest(BaseModel):
    """Inpainting request - edit specific parts of an image"""
    prompt: str
    negative_prompt: Optional[str] = ""
    # Original image (base64 encoded)
    image: str
    # Mask image (base64 encoded) - white = area to inpaint, black = keep
    mask: str
    # Generation parameters
    width: int = Field(default=1024, ge=256, le=2048)
    height: int = Field(default=1024, ge=256, le=2048)
    steps: int = Field(default=30, ge=1, le=150)
    guidance_scale: float = Field(default=7.5, ge=1.0, le=30.0)
    # Strength controls how much the masked area is changed (0.0-1.0)
    strength: float = Field(default=0.99, ge=0.0, le=1.0)
    seed: int = -1
    batch_size: int = Field(default=1, ge=1, le=4)
    scheduler: Optional[str] = None
    clip_skip: int = Field(default=1, ge=1, le=4)
    lora_models: Optional[List[Dict[str, Any]]] = None


class OutpaintRequest(BaseModel):
    """Outpainting request - extend image canvas"""
    prompt: str
    negative_prompt: Optional[str] = ""
    # Original image (base64 encoded)
    image: str
    # Direction to extend: left, right, top, bottom, or combination like "left,top"
    direction: str = "right"
    # How many pixels to extend
    extend_pixels: int = Field(default=256, ge=64, le=1024)
    # Generation parameters
    steps: int = Field(default=30, ge=1, le=150)
    guidance_scale: float = Field(default=7.5, ge=1.0, le=30.0)
    strength: float = Field(default=0.85, ge=0.0, le=1.0)
    seed: int = -1
    scheduler: Optional[str] = None
    # Feather/blend the mask edges for smoother transitions
    feather_pixels: int = Field(default=32, ge=0, le=128)


class UpscaleRequest(BaseModel):
    """Image upscaling request using Real-ESRGAN or similar models"""
    image: str  # Base64 encoded image
    scale: int = Field(default=2, ge=2, le=4)  # 2x, 3x, or 4x
    model: str = "realesrgan"  # realesrgan, realesrgan-anime, esrgan
    # Optional: denoise strength (0.0-1.0) for Real-ESRGAN
    denoise_strength: float = Field(default=0.5, ge=0.0, le=1.0)
    # Optional: tile size for processing large images (0 = no tiling)
    tile_size: int = Field(default=0, ge=0, le=1024)
    # Output format
    output_format: str = "png"  # png or jpg


class IPAdapterRequest(BaseModel):
    """IP-Adapter request - use reference image for style/content transfer"""
    prompt: str
    negative_prompt: Optional[str] = ""
    # Reference image(s) for IP-Adapter (base64 encoded)
    reference_images: List[str]
    # IP-Adapter scale (0.0-1.0, higher = more influence from reference)
    ip_adapter_scale: float = Field(default=0.6, ge=0.0, le=1.5)
    # Generation parameters
    width: int = Field(default=1024, ge=256, le=2048)
    height: int = Field(default=1024, ge=256, le=2048)
    steps: int = Field(default=30, ge=1, le=150)
    guidance_scale: float = Field(default=7.5, ge=1.0, le=30.0)
    seed: int = -1
    batch_size: int = Field(default=1, ge=1, le=4)
    scheduler: Optional[str] = None
    clip_skip: int = Field(default=1, ge=1, le=4)
    lora_models: Optional[List[Dict[str, Any]]] = None


class QueuedTaskRequest(BaseModel):
    """Request to add a task to the queue"""
    task_type: str  # image, img2img, video, controlnet, inpaint, outpaint, upscale, ip_adapter
    request_data: Dict[str, Any]
    priority: int = Field(default=0, ge=0, le=10)  # 0 = normal, higher = higher priority


class QueuedTask(BaseModel):
    """A task in the generation queue"""
    task_id: str
    task_type: str
    request_data: Dict[str, Any]
    priority: int = 0
    status: str = "pending"  # pending, processing, completed, failed, cancelled
    progress: int = 0
    result: Optional[Dict[str, Any]] = None
    error: Optional[str] = None
    created_at: float
    started_at: Optional[float] = None
    completed_at: Optional[float] = None


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

        # ControlNet state
        self.controlnet_pipeline = None
        self.loaded_controlnets: Dict[str, Any] = {}  # type -> model
        self._preprocessors: Dict[str, Any] = {}  # Cached preprocessors

        # IP-Adapter state
        self.ip_adapter_loaded = False
        self.ip_adapter_image_encoder = None
        self.ip_adapter_image_processor = None

        # Upscaler state
        self.upscaler_model = None
        self.upscaler_type: Optional[str] = None

        # Queue state
        self._task_queue: deque = deque()
        self._task_history: Dict[str, Dict[str, Any]] = {}
        self._queue_lock = Lock()
        self._queue_running = False
        self._queue_thread: Optional[Thread] = None

        self._lock = Lock()
        self._generation_progress = 0
        self._generation_step = 0
        self._generation_total_steps = 0
        self._cancel_event = Event()
        self._is_generating = False
        self._current_task_id: Optional[str] = None

        print(f"[Engine] Initialized")
        print(f"[Engine] Device: {self.device}")
        print(f"[Engine] Dtype: {self.dtype}")
        print(f"[Engine] Models directory: {models_dir}")
        print(f"[Engine] Low VRAM mode: {low_vram_mode}")
        print(f"[Engine] ControlNet aux available: {HAS_CONTROLNET_AUX}")

        if self.device == "cuda":
            props = torch.cuda.get_device_properties(0)
            print(f"[Engine] GPU: {props.name}")
            print(f"[Engine] Total VRAM: {props.total_memory / 1024**3:.1f} GB")

    # =========================================================================
    # ControlNet Model Registry - Auto-select based on base model and type
    # =========================================================================
    CONTROLNET_MODELS = {
        # SD 1.5 ControlNets
        "sd15": {
            "canny": "lllyasviel/control_v11p_sd15_canny",
            "pose": "lllyasviel/control_v11p_sd15_openpose",
            "depth": "lllyasviel/control_v11f1p_sd15_depth",
            "hed": "lllyasviel/control_v11p_sd15_softedge",
            "lineart": "lllyasviel/control_v11p_sd15_lineart",
            "scribble": "lllyasviel/control_v11p_sd15_scribble",
            "softedge": "lllyasviel/control_v11p_sd15_softedge",
            "normal": "lllyasviel/control_v11p_sd15_normalbae",
            "tile": "lllyasviel/control_v11f1e_sd15_tile",
            "inpaint": "lllyasviel/control_v11p_sd15_inpaint",
            "seg": "lllyasviel/control_v11p_sd15_seg",
        },
        # SDXL ControlNets
        "sdxl": {
            "canny": "diffusers/controlnet-canny-sdxl-1.0",
            "depth": "diffusers/controlnet-depth-sdxl-1.0",
            "pose": "thibaud/controlnet-openpose-sdxl-1.0",
        },
    }

    def _get_controlnet_model_id(self, control_type: str, custom_model: Optional[str] = None) -> str:
        """Get ControlNet model ID based on current base model and control type."""
        if custom_model:
            return custom_model

        # Detect if using SDXL
        model_lower = (self.current_model or "").lower()
        is_sdxl = "xl" in model_lower or "sdxl" in model_lower

        model_family = "sdxl" if is_sdxl else "sd15"
        models = self.CONTROLNET_MODELS.get(model_family, self.CONTROLNET_MODELS["sd15"])

        control_type_lower = control_type.lower()
        if control_type_lower not in models:
            raise ValueError(f"Unknown control type '{control_type}' for {model_family}. Available: {list(models.keys())}")

        return models[control_type_lower]

    def _get_preprocessor(self, control_type: str):
        """Get or create a preprocessor for the given control type."""
        if control_type in self._preprocessors:
            return self._preprocessors[control_type]

        preprocessor = None
        control_type_lower = control_type.lower()

        if HAS_CONTROLNET_AUX:
            if control_type_lower == "canny":
                preprocessor = CannyDetector()
            elif control_type_lower == "pose":
                preprocessor = OpenposeDetector.from_pretrained("lllyasviel/Annotators")
            elif control_type_lower == "depth":
                preprocessor = MidasDetector.from_pretrained("lllyasviel/Annotators")
            elif control_type_lower == "hed":
                preprocessor = HEDdetector.from_pretrained("lllyasviel/Annotators")
            elif control_type_lower == "lineart":
                preprocessor = LineartDetector.from_pretrained("lllyasviel/Annotators")
            elif control_type_lower == "softedge":
                preprocessor = PidiNetDetector.from_pretrained("lllyasviel/Annotators")

        self._preprocessors[control_type] = preprocessor
        return preprocessor

    def _preprocess_control_image(self, image: Image.Image, control_type: str,
                                   canny_low: int = 100, canny_high: int = 200) -> Image.Image:
        """Preprocess image for ControlNet based on control type."""
        control_type_lower = control_type.lower()

        # Try using controlnet_aux preprocessors first
        preprocessor = self._get_preprocessor(control_type)
        if preprocessor is not None:
            try:
                if control_type_lower == "canny":
                    return preprocessor(image, low_threshold=canny_low, high_threshold=canny_high)
                else:
                    return preprocessor(image)
            except Exception as e:
                print(f"[Engine] Preprocessor failed, falling back to basic: {e}")

        # Fallback: Basic OpenCV-based Canny for edge detection
        if control_type_lower == "canny":
            import numpy as np
            try:
                import cv2
                img_array = np.array(image)
                if len(img_array.shape) == 3:
                    gray = cv2.cvtColor(img_array, cv2.COLOR_RGB2GRAY)
                else:
                    gray = img_array
                edges = cv2.Canny(gray, canny_low, canny_high)
                return Image.fromarray(edges).convert("RGB")
            except ImportError:
                print("[Engine] Warning: OpenCV not available for Canny preprocessing")
                return image

        # For other types without preprocessors, return as-is
        print(f"[Engine] No preprocessor for '{control_type}', using image as-is")
        return image

    def _get_model_path(self, model_id: str):
        """Get model path and whether it's local. Returns (path, is_local, is_single_file) tuple."""
        # Check for local model in various locations
        local_paths = [
            os.path.join(self.models_dir, "checkpoints", model_id.replace("/", "--")),
            os.path.join(self.models_dir, "checkpoints", model_id.replace("/", "_")),
            os.path.join(self.models_dir, model_id.replace("/", "--")),
            os.path.join(self.models_dir, model_id),
        ]

        for path in local_paths:
            if os.path.exists(path):
                # Check if it's a directory with diffusers format or single checkpoint
                if os.path.isdir(path):
                    # Check for single checkpoint file inside folder
                    checkpoint_file = self._find_checkpoint_file(path)
                    if checkpoint_file:
                        print(f"[Engine] Found single checkpoint at: {checkpoint_file}")
                        return checkpoint_file, True, True

                    # Check if it's a valid diffusers format (has required subfolders or files)
                    if self._is_valid_diffusers_model(path):
                        print(f"[Engine] Found local diffusers model at: {path}")
                        return path, True, False
                    else:
                        print(f"[Engine] Invalid/incomplete model at {path}, will try HuggingFace")
                        continue

                elif path.endswith(('.safetensors', '.ckpt', '.pt')):
                    print(f"[Engine] Found single checkpoint file at: {path}")
                    return path, True, True

        # Return model_id for HuggingFace download
        print(f"[Engine] Model not found locally, will download from HuggingFace: {model_id}")
        return model_id, False, False

    def _is_valid_diffusers_model(self, path: str) -> bool:
        """Check if a directory contains a valid diffusers model."""
        # Must have model_index.json
        model_index = os.path.join(path, "model_index.json")
        if not os.path.exists(model_index):
            return False

        # Must have at least one of the required subfolders with actual model files
        required_folders = ["unet", "text_encoder", "vae", "scheduler"]
        has_valid_folder = False

        for folder in required_folders:
            folder_path = os.path.join(path, folder)
            if os.path.isdir(folder_path):
                # Check if folder has actual files (not just config)
                files = os.listdir(folder_path)
                model_files = [f for f in files if f.endswith(('.safetensors', '.bin', '.pt'))]
                if model_files:
                    has_valid_folder = True
                    break

        return has_valid_folder

    def _find_checkpoint_file(self, folder_path: str) -> Optional[str]:
        """Find single checkpoint file in a folder (not diffusers format)."""
        # Check if it has diffusers format (has scheduler subfolder or model_index.json with proper structure)
        scheduler_path = os.path.join(folder_path, "scheduler")
        unet_path = os.path.join(folder_path, "unet")

        # If it has diffusers format subfolders, it's not a single checkpoint
        if os.path.isdir(scheduler_path) or os.path.isdir(unet_path):
            return None

        # Look for checkpoint files
        checkpoint_extensions = ['.safetensors', '.ckpt', '.pt']
        checkpoint_files = []

        for file in os.listdir(folder_path):
            if any(file.endswith(ext) for ext in checkpoint_extensions):
                # Skip VAE, LoRA, and other non-main model files
                file_lower = file.lower()
                if 'vae' in file_lower and 'vae' not in os.path.basename(folder_path).lower():
                    continue
                if 'lora' in file_lower:
                    continue
                if 'offset' in file_lower:  # SDXL offset LoRA
                    continue
                checkpoint_files.append(os.path.join(folder_path, file))

        if checkpoint_files:
            # Prefer safetensors, then pick largest file
            safetensors_files = [f for f in checkpoint_files if f.endswith('.safetensors')]
            if safetensors_files:
                return max(safetensors_files, key=os.path.getsize)
            return max(checkpoint_files, key=os.path.getsize)

        return None

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

    def _load_single_file(self, checkpoint_path: str, model_type: str, model_id_lower: str, path_lower: str, dtype: torch.dtype):
        """Load model from a single checkpoint file (.safetensors, .ckpt)."""
        print(f"[Engine] Loading single checkpoint: {checkpoint_path}")

        # Determine model family from filename or model_id
        # Check FLUX first as it's more specific
        is_flux = any(x in model_id_lower or x in path_lower for x in ['flux', 'noobai-flux', 'rectifiedflow'])
        is_sdxl = not is_flux and any(x in model_id_lower or x in path_lower for x in ['xl', 'sdxl'])
        is_inpaint = 'inpaint' in path_lower or 'inpaint' in model_id_lower

        print(f"[Engine] Model detection - FLUX: {is_flux}, SDXL: {is_sdxl}, Inpaint: {is_inpaint}")

        # FLUX models need special handling with config from HuggingFace
        if is_flux:
            return self._load_flux_single_file(checkpoint_path, dtype, model_id_lower, path_lower)

        # SD/SDXL models
        return self._load_sd_single_file(checkpoint_path, model_type, is_sdxl, dtype)

    def _load_flux_single_file(self, checkpoint_path: str, dtype: torch.dtype, model_id_lower: str, path_lower: str):
        """Load FLUX model from single file with smart config detection."""
        print("[Engine] Loading as FLUX model from single file")

        # Determine which FLUX base config to use
        # NoobAI models typically use FLUX.1-dev as base
        if 'noobai' in model_id_lower or 'noobai' in path_lower:
            base_config = "black-forest-labs/FLUX.1-dev"
        elif 'schnell' in model_id_lower or 'schnell' in path_lower:
            base_config = "black-forest-labs/FLUX.1-schnell"
        else:
            base_config = "black-forest-labs/FLUX.1-dev"  # Default to dev

        print(f"[Engine] Using base config from: {base_config}")

        try:
            from diffusers import FluxPipeline

            # Try loading with original config first
            try:
                print("[Engine] Attempting FLUX load with base config...")
                pipe = FluxPipeline.from_single_file(
                    checkpoint_path,
                    config=base_config,
                    torch_dtype=dtype,
                )
                print("[Engine] FLUX model loaded successfully with base config")
                return pipe
            except Exception as e1:
                print(f"[Engine] Standard FLUX load failed: {e1}")

                # Try with ignore_mismatched_sizes for custom VAE models
                print("[Engine] Trying with ignore_mismatched_sizes=True...")
                try:
                    pipe = FluxPipeline.from_single_file(
                        checkpoint_path,
                        config=base_config,
                        torch_dtype=dtype,
                        ignore_mismatched_sizes=True,
                    )
                    print("[Engine] FLUX model loaded with mismatched sizes ignored")
                    return pipe
                except Exception as e2:
                    print(f"[Engine] FLUX load with ignore_mismatched_sizes failed: {e2}")

                    # Try DiffusionPipeline as last resort
                    print("[Engine] Trying generic DiffusionPipeline...")
                    try:
                        pipe = DiffusionPipeline.from_single_file(
                            checkpoint_path,
                            torch_dtype=dtype,
                        )
                        print("[Engine] Loaded via DiffusionPipeline")
                        return pipe
                    except Exception as e3:
                        print(f"[Engine] DiffusionPipeline also failed: {e3}")
                        raise RuntimeError(
                            f"Cannot load FLUX model - may need special config\n"
                            f"Try downloading diffusers format instead of .safetensors\n"
                            f"Or use a different FLUX model\n"
                            f"Error: {e1}"
                        )

        except ImportError:
            print("[Engine] FluxPipeline not available, trying DiffusionPipeline...")
            return DiffusionPipeline.from_single_file(
                checkpoint_path,
                torch_dtype=dtype,
            )

    def _load_sd_single_file(self, checkpoint_path: str, model_type: str, is_sdxl: bool, dtype: torch.dtype):
        """Load SD/SDXL model from single file."""
        if model_type in ["TextToImage", "txt2img"]:
            if is_sdxl:
                print("[Engine] Loading as SDXL txt2img from single file")
                return StableDiffusionXLPipeline.from_single_file(
                    checkpoint_path,
                    torch_dtype=dtype,
                    use_safetensors=checkpoint_path.endswith('.safetensors'),
                )
            else:
                print("[Engine] Loading as SD 1.5 txt2img from single file")
                return StableDiffusionPipeline.from_single_file(
                    checkpoint_path,
                    torch_dtype=dtype,
                    use_safetensors=checkpoint_path.endswith('.safetensors'),
                )

        elif model_type in ["ImageToImage", "img2img"]:
            if is_sdxl:
                print("[Engine] Loading as SDXL img2img from single file")
                return StableDiffusionXLImg2ImgPipeline.from_single_file(
                    checkpoint_path,
                    torch_dtype=dtype,
                    use_safetensors=checkpoint_path.endswith('.safetensors'),
                )
            else:
                print("[Engine] Loading as SD 1.5 img2img from single file")
                # Load txt2img and convert to img2img
                pipe = StableDiffusionPipeline.from_single_file(
                    checkpoint_path,
                    torch_dtype=dtype,
                    use_safetensors=checkpoint_path.endswith('.safetensors'),
                )
                return AutoPipelineForImage2Image.from_pipe(pipe)

        elif model_type in ["Inpainting", "inpaint"]:
            if is_sdxl:
                print("[Engine] Loading as SDXL inpaint from single file")
                return StableDiffusionXLInpaintPipeline.from_single_file(
                    checkpoint_path,
                    torch_dtype=dtype,
                    use_safetensors=checkpoint_path.endswith('.safetensors'),
                )
            else:
                print("[Engine] Loading as SD 1.5 inpaint from single file")
                return StableDiffusionInpaintPipeline.from_single_file(
                    checkpoint_path,
                    torch_dtype=dtype,
                    use_safetensors=checkpoint_path.endswith('.safetensors'),
                )

        else:
            # Default: txt2img
            if is_sdxl:
                print("[Engine] Loading as SDXL (default) from single file")
                return StableDiffusionXLPipeline.from_single_file(
                    checkpoint_path,
                    torch_dtype=dtype,
                    use_safetensors=checkpoint_path.endswith('.safetensors'),
                )
            else:
                print("[Engine] Loading as SD 1.5 (default) from single file")
                return StableDiffusionPipeline.from_single_file(
                    checkpoint_path,
                    torch_dtype=dtype,
                    use_safetensors=checkpoint_path.endswith('.safetensors'),
                )

    def load_model(self, request: LoadModelRequest) -> Dict[str, Any]:
        """Load a diffusion model."""
        with self._lock:
            try:
                # Unload current model first and clear VRAM
                if self.pipeline is not None:
                    self.unload_model()

                # Always clear VRAM before loading new model
                self._clear_vram()

                model_path, is_local, is_single_file = self._get_model_path(request.model_id)

                # Determine dtype
                dtype = torch.float32
                if request.precision == "fp16" and self.device == "cuda":
                    dtype = torch.float16
                elif request.precision == "bf16" and self.device == "cuda":
                    dtype = torch.bfloat16

                print(f"[Engine] Loading model: {request.model_id}")
                print(f"[Engine] Model path: {model_path}")
                print(f"[Engine] Model type: {request.model_type}")
                print(f"[Engine] Single file: {is_single_file}")
                print(f"[Engine] Precision: {request.precision} -> {dtype}")

                load_start = time.time()

                # Load based on model type
                model_id_lower = request.model_id.lower()
                path_lower = model_path.lower() if isinstance(model_path, str) else ""

                # Single checkpoint file loading
                if is_single_file:
                    self.pipeline = self._load_single_file(model_path, request.model_type, model_id_lower, path_lower, dtype)
                elif request.model_type in ["TextToImage", "txt2img"]:
                    if "xl" in model_id_lower or "sdxl" in model_id_lower:
                        # Try with fp16 variant first, then without
                        try:
                            self.pipeline = StableDiffusionXLPipeline.from_pretrained(
                                model_path,
                                torch_dtype=dtype,
                                use_safetensors=True,
                                local_files_only=is_local,
                                variant="fp16" if dtype == torch.float16 else None,
                            )
                        except Exception as e:
                            print(f"[Engine] Failed with fp16 variant, trying without: {e}")
                            self.pipeline = StableDiffusionXLPipeline.from_pretrained(
                                model_path,
                                torch_dtype=dtype,
                                use_safetensors=True,
                                local_files_only=is_local,
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

            # Clear ControlNets
            self.controlnet = None
            self.controlnet_type = None

            self._clear_vram()

            print("[Engine] Model unloaded")

    def _clear_vram(self):
        """Clear VRAM and garbage collect."""
        gc.collect()

        if torch.cuda.is_available():
            torch.cuda.empty_cache()
            torch.cuda.synchronize()
            torch.cuda.ipc_collect()

            # Get VRAM stats
            vram_allocated = torch.cuda.memory_allocated() / 1024**3
            vram_reserved = torch.cuda.memory_reserved() / 1024**3
            print(f"[Engine] VRAM cleared - Allocated: {vram_allocated:.2f} GB, Reserved: {vram_reserved:.2f} GB")

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

    def _progress_callback(self, pipe, step: int, timestep: int, callback_kwargs: Dict[str, Any]) -> Dict[str, Any]:
        """Callback for generation progress (new diffusers API)."""
        self._generation_step = step
        if self._generation_total_steps > 0:
            self._generation_progress = int((step / self._generation_total_steps) * 100)
        print(f"Step {step}/{self._generation_total_steps}")

        # Check for cancellation
        if self._cancel_event.is_set():
            raise InterruptedError("Generation cancelled by user")

        return callback_kwargs

    def _progress_callback_legacy(self, step: int, timestep: int, latents: torch.Tensor):
        """Legacy callback for older diffusers versions."""
        self._generation_step = step
        if self._generation_total_steps > 0:
            self._generation_progress = int((step / self._generation_total_steps) * 100)
        print(f"Step {step}/{self._generation_total_steps}")

        # Check for cancellation
        if self._cancel_event.is_set():
            raise InterruptedError("Generation cancelled by user")

    def cancel_generation(self) -> bool:
        """Cancel current generation."""
        if self._is_generating:
            self._cancel_event.set()
            print("[Engine] Cancellation requested")
            return True
        return False

    def get_progress(self) -> Dict[str, Any]:
        """Get current generation progress."""
        return {
            "is_generating": self._is_generating,
            "task_id": self._current_task_id,
            "step": self._generation_step,
            "total_steps": self._generation_total_steps,
            "progress": self._generation_progress,
        }

    def generate_image(self, request: ImageGenerationRequest, task_id: Optional[str] = None) -> Dict[str, Any]:
        """Generate image(s) from text prompt."""
        if self.pipeline is None:
            return {"success": False, "error": "No model loaded"}

        with self._lock:
            orig_clip_layers = None
            try:
                # Reset state
                self._generation_progress = 0
                self._generation_step = 0
                self._generation_total_steps = request.steps
                self._cancel_event.clear()
                self._is_generating = True
                self._current_task_id = task_id or str(int(time.time() * 1000))

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

                print(f"[Engine] Generating image (task: {self._current_task_id}):")
                print(f"  Prompt: {request.prompt[:100]}...")
                print(f"  Size: {request.width}x{request.height}")
                print(f"  Steps: {request.steps}")
                print(f"  Guidance: {request.guidance_scale}")
                print(f"  Seed: {seed}")

                start_time = time.time()

                # Build generation kwargs - use new callback API if available
                gen_kwargs = {
                    "prompt": request.prompt,
                    "width": request.width,
                    "height": request.height,
                    "num_inference_steps": request.steps,
                    "guidance_scale": request.guidance_scale,
                    "num_images_per_prompt": request.batch_size,
                    "generator": generator,
                }

                # Try new callback API first, fall back to legacy
                import inspect
                pipeline_call_sig = inspect.signature(self.pipeline.__call__)
                if "callback_on_step_end" in pipeline_call_sig.parameters:
                    gen_kwargs["callback_on_step_end"] = self._progress_callback
                else:
                    # Legacy callback
                    gen_kwargs["callback"] = self._progress_callback_legacy
                    gen_kwargs["callback_steps"] = 1

                if request.negative_prompt:
                    gen_kwargs["negative_prompt"] = request.negative_prompt

                # CLIP skip (only for SD 1.x pipelines)
                if request.clip_skip > 1 and hasattr(self.pipeline, "text_encoder"):
                    try:
                        if hasattr(self.pipeline.text_encoder.config, "num_hidden_layers"):
                            orig_clip_layers = self.pipeline.text_encoder.config.num_hidden_layers
                            self.pipeline.text_encoder.config.num_hidden_layers = orig_clip_layers - (request.clip_skip - 1)
                            print(f"[Engine] CLIP skip set to: {request.clip_skip}")
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
                    "task_id": self._current_task_id,
                }

            except InterruptedError as e:
                return {"success": False, "error": "cancelled", "cancelled": True, "task_id": self._current_task_id}
            except Exception as e:
                traceback.print_exc()
                return {"success": False, "error": str(e), "task_id": self._current_task_id}
            finally:
                # Reset state
                self._is_generating = False
                self._cancel_event.clear()
                # Reset CLIP skip to original value
                if orig_clip_layers is not None and hasattr(self.pipeline, "text_encoder"):
                    try:
                        self.pipeline.text_encoder.config.num_hidden_layers = orig_clip_layers
                    except Exception:
                        pass

    def generate_img2img(self, request: Img2ImgRequest, task_id: Optional[str] = None) -> Dict[str, Any]:
        """Generate image from image + prompt."""
        if self.pipeline is None:
            return {"success": False, "error": "No model loaded"}

        with self._lock:
            orig_clip_layers = None
            try:
                # Reset state
                self._generation_progress = 0
                self._generation_step = 0
                # Calculate effective steps (strength affects actual step count)
                effective_steps = int(request.steps * request.strength)
                self._generation_total_steps = max(1, effective_steps)
                self._cancel_event.clear()
                self._is_generating = True
                self._current_task_id = task_id or str(int(time.time() * 1000))

                # Set scheduler if specified
                if request.scheduler:
                    self._set_scheduler(request.scheduler)

                # Load LoRAs if specified
                if request.lora_models:
                    self.unload_loras()
                    for lora in request.lora_models:
                        self.load_lora(lora.get("path"), lora.get("weight", 1.0), lora.get("name"))

                # Decode input image
                image_data = request.image
                if image_data.startswith("data:"):
                    image_data = image_data.split(",")[1]

                input_image = Image.open(io.BytesIO(base64.b64decode(image_data)))
                input_image = input_image.convert("RGB")

                # Preserve aspect ratio if dimensions differ significantly
                orig_w, orig_h = input_image.size
                target_w, target_h = request.width, request.height
                orig_ratio = orig_w / orig_h
                target_ratio = target_w / target_h

                # Only resize if aspect ratios are similar (within 10%)
                if abs(orig_ratio - target_ratio) / max(orig_ratio, target_ratio) < 0.1:
                    input_image = input_image.resize((target_w, target_h), Image.Resampling.LANCZOS)
                else:
                    # Fit within target dimensions while preserving aspect ratio
                    input_image.thumbnail((target_w, target_h), Image.Resampling.LANCZOS)
                    print(f"[Engine] Preserved aspect ratio: {input_image.size[0]}x{input_image.size[1]}")

                # Generate seed
                if request.seed >= 0:
                    seed = request.seed
                else:
                    seed = int(torch.randint(0, 2**32 - 1, (1,)).item())

                generator = torch.Generator(device=self.device).manual_seed(seed)

                print(f"[Engine] Generating img2img:")
                print(f"  Prompt: {request.prompt[:100]}...")
                print(f"  Strength: {request.strength}")
                print(f"  Steps: {request.steps} (effective: {effective_steps})")
                print(f"  Seed: {seed}")

                start_time = time.time()

                # CLIP skip (only for SD 1.x pipelines)
                if request.clip_skip > 1 and hasattr(self.pipeline, "text_encoder"):
                    try:
                        if hasattr(self.pipeline.text_encoder.config, "num_hidden_layers"):
                            orig_clip_layers = self.pipeline.text_encoder.config.num_hidden_layers
                            self.pipeline.text_encoder.config.num_hidden_layers = orig_clip_layers - (request.clip_skip - 1)
                            print(f"[Engine] CLIP skip set to: {request.clip_skip}")
                    except Exception as e:
                        print(f"[Engine] CLIP skip not supported: {e}")

                gen_kwargs = {
                    "prompt": request.prompt,
                    "image": input_image,
                    "strength": request.strength,
                    "num_inference_steps": request.steps,
                    "guidance_scale": request.guidance_scale,
                    "num_images_per_prompt": request.batch_size,
                    "generator": generator,
                }

                # Try new callback API first, fall back to legacy
                import inspect
                pipeline_call_sig = inspect.signature(self.pipeline.__call__)
                if "callback_on_step_end" in pipeline_call_sig.parameters:
                    gen_kwargs["callback_on_step_end"] = self._progress_callback
                else:
                    gen_kwargs["callback"] = self._progress_callback_legacy
                    gen_kwargs["callback_steps"] = 1

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

                # Get VRAM usage
                vram_used = 0
                if self.device == "cuda":
                    vram_used = torch.cuda.memory_allocated() / 1024**3

                print(f"[Engine] Img2img generation complete in {gen_time:.1f}s")

                return {
                    "success": True,
                    "images": images,
                    "seed": seed,
                    "generation_time": gen_time,
                    "vram_used_gb": vram_used,
                    "task_id": self._current_task_id,
                }

            except InterruptedError:
                return {"success": False, "error": "cancelled", "cancelled": True, "task_id": self._current_task_id}
            except Exception as e:
                traceback.print_exc()
                return {"success": False, "error": str(e), "task_id": self._current_task_id}
            finally:
                # Reset state
                self._is_generating = False
                self._cancel_event.clear()
                # Reset CLIP skip to original value
                if orig_clip_layers is not None and hasattr(self.pipeline, "text_encoder"):
                    try:
                        self.pipeline.text_encoder.config.num_hidden_layers = orig_clip_layers
                    except Exception:
                        pass

    def _video_progress_callback(self, pipe, step: int, timestep: int, callback_kwargs: Dict[str, Any]) -> Dict[str, Any]:
        """Callback for video generation progress (new API)."""
        self._generation_step = step
        if self._generation_total_steps > 0:
            self._generation_progress = int((step / self._generation_total_steps) * 100)
        print(f"Video step {step}/{self._generation_total_steps}")

        # Check for cancellation
        if self._cancel_event.is_set():
            raise InterruptedError("Generation cancelled by user")

        return callback_kwargs

    def _video_progress_callback_legacy(self, step: int, timestep: int, latents: torch.Tensor):
        """Legacy callback for video generation progress."""
        self._generation_step = step
        if self._generation_total_steps > 0:
            self._generation_progress = int((step / self._generation_total_steps) * 100)
        print(f"Video step {step}/{self._generation_total_steps}")

        # Check for cancellation
        if self._cancel_event.is_set():
            raise InterruptedError("Generation cancelled by user")

    def generate_video(self, request: VideoGenerationRequest, task_id: Optional[str] = None) -> Dict[str, Any]:
        """Generate video from image."""
        if self.pipeline is None:
            return {"success": False, "error": "No model loaded"}

        if not isinstance(self.pipeline, StableVideoDiffusionPipeline):
            return {"success": False, "error": "Video generation requires a video model (SVD)"}

        with self._lock:
            try:
                # Reset state
                self._generation_progress = 0
                self._generation_step = 0
                self._generation_total_steps = 25  # SVD default steps
                self._cancel_event.clear()
                self._is_generating = True
                self._current_task_id = task_id or str(int(time.time() * 1000))

                # Decode input image
                image_data = request.image
                if image_data.startswith("data:"):
                    image_data = image_data.split(",")[1]

                input_image = Image.open(io.BytesIO(base64.b64decode(image_data)))
                input_image = input_image.convert("RGB")

                # SVD requires 1024x576 (16:9 landscape) or 576x1024 (portrait)
                orig_w, orig_h = input_image.size
                if orig_w >= orig_h:
                    # Landscape or square - use 1024x576
                    target_size = (1024, 576)
                else:
                    # Portrait - use 576x1024
                    target_size = (576, 1024)

                input_image = input_image.resize(target_size, Image.Resampling.LANCZOS)
                print(f"[Engine] Resized input image to: {target_size[0]}x{target_size[1]}")

                # Generate seed
                if request.seed >= 0:
                    seed = request.seed
                else:
                    seed = int(torch.randint(0, 2**32 - 1, (1,)).item())

                generator = torch.Generator(device=self.device).manual_seed(seed)

                print(f"[Engine] Generating video:")
                print(f"  Frames: {request.num_frames}")
                print(f"  FPS: {request.fps}")
                print(f"  Motion bucket: {request.motion_bucket_id}")
                print(f"  Seed: {seed}")

                start_time = time.time()

                gen_kwargs = {
                    "image": input_image,
                    "num_frames": request.num_frames,
                    "motion_bucket_id": request.motion_bucket_id,
                    "noise_aug_strength": request.noise_aug_strength,
                    "generator": generator,
                }

                # Try new callback API first, fall back to legacy
                import inspect
                pipeline_call_sig = inspect.signature(self.pipeline.__call__)
                if "callback_on_step_end" in pipeline_call_sig.parameters:
                    gen_kwargs["callback_on_step_end"] = self._video_progress_callback
                else:
                    gen_kwargs["callback"] = self._video_progress_callback_legacy
                    gen_kwargs["callback_steps"] = 1

                result = self.pipeline(**gen_kwargs)

                gen_time = time.time() - start_time

                # Encode frames to base64 with validation
                frames = []
                # Handle different result structures
                frame_list = result.frames[0] if isinstance(result.frames, list) and len(result.frames) > 0 else result.frames
                if hasattr(frame_list, '__iter__'):
                    for frame in frame_list:
                        buffer = io.BytesIO()
                        if hasattr(frame, 'save'):
                            frame.save(buffer, format="PNG", optimize=True)
                        else:
                            # Convert numpy array to PIL Image if needed
                            Image.fromarray(frame).save(buffer, format="PNG", optimize=True)
                        b64 = base64.b64encode(buffer.getvalue()).decode("utf-8")
                        frames.append(f"data:image/png;base64,{b64}")

                # Get VRAM usage
                vram_used = 0
                if self.device == "cuda":
                    vram_used = torch.cuda.memory_allocated() / 1024**3

                print(f"[Engine] Video generation complete in {gen_time:.1f}s ({len(frames)} frames)")

                return {
                    "success": True,
                    "frames": frames,
                    "fps": request.fps,
                    "seed": seed,
                    "generation_time": gen_time,
                    "vram_used_gb": vram_used,
                    "frame_count": len(frames),
                    "task_id": self._current_task_id,
                }

            except InterruptedError:
                return {"success": False, "error": "cancelled", "cancelled": True, "task_id": self._current_task_id}
            except Exception as e:
                traceback.print_exc()
                return {"success": False, "error": str(e), "task_id": self._current_task_id}
            finally:
                # Reset state
                self._is_generating = False
                self._cancel_event.clear()

    def load_controlnet(self, control_type: str, custom_model: Optional[str] = None) -> Dict[str, Any]:
        """Load a ControlNet model for the given control type."""
        try:
            controlnet_model_id = self._get_controlnet_model_id(control_type, custom_model)

            # Check if already loaded
            if control_type in self.loaded_controlnets:
                print(f"[Engine] ControlNet '{control_type}' already loaded")
                return {"success": True, "model": controlnet_model_id, "cached": True}

            print(f"[Engine] Loading ControlNet: {controlnet_model_id}")

            controlnet = ControlNetModel.from_pretrained(
                controlnet_model_id,
                torch_dtype=self.dtype,
            )
            controlnet = controlnet.to(self.device)

            self.loaded_controlnets[control_type] = controlnet

            print(f"[Engine] ControlNet loaded: {control_type}")
            return {"success": True, "model": controlnet_model_id, "control_type": control_type}

        except Exception as e:
            traceback.print_exc()
            return {"success": False, "error": str(e)}

    def unload_controlnets(self):
        """Unload all ControlNet models."""
        for ct, model in self.loaded_controlnets.items():
            del model
        self.loaded_controlnets = {}

        if self.controlnet_pipeline is not None:
            del self.controlnet_pipeline
            self.controlnet_pipeline = None

        gc.collect()
        if torch.cuda.is_available():
            torch.cuda.empty_cache()

        print("[Engine] All ControlNets unloaded")

    def generate_controlnet(self, request: ControlNetRequest, task_id: Optional[str] = None) -> Dict[str, Any]:
        """Generate image with ControlNet guidance."""
        if self.pipeline is None:
            return {"success": False, "error": "No base model loaded. Load a model first."}

        with self._lock:
            orig_clip_layers = None
            try:
                # Reset state
                self._generation_progress = 0
                self._generation_step = 0
                self._generation_total_steps = request.steps
                self._cancel_event.clear()
                self._is_generating = True
                self._current_task_id = task_id or str(int(time.time() * 1000))

                # Set scheduler if specified
                if request.scheduler:
                    self._set_scheduler(request.scheduler)

                # Load ControlNet if not already loaded
                control_type = request.control_type.lower()
                if control_type not in self.loaded_controlnets:
                    load_result = self.load_controlnet(control_type, request.controlnet_model)
                    if not load_result.get("success"):
                        return load_result

                controlnet = self.loaded_controlnets[control_type]

                # Decode and preprocess control image
                control_image_data = request.control_image
                if control_image_data.startswith("data:"):
                    control_image_data = control_image_data.split(",")[1]

                control_image = Image.open(io.BytesIO(base64.b64decode(control_image_data)))
                control_image = control_image.convert("RGB")

                # Resize control image to target size
                control_image = control_image.resize((request.width, request.height), Image.Resampling.LANCZOS)

                # Preprocess if requested
                if request.preprocess:
                    control_image = self._preprocess_control_image(
                        control_image,
                        control_type,
                        request.canny_low,
                        request.canny_high
                    )
                    print(f"[Engine] Control image preprocessed with '{control_type}'")

                # Create ControlNet pipeline
                model_lower = (self.current_model or "").lower()
                is_sdxl = "xl" in model_lower or "sdxl" in model_lower

                if is_sdxl:
                    self.controlnet_pipeline = StableDiffusionXLControlNetPipeline(
                        vae=self.pipeline.vae,
                        text_encoder=self.pipeline.text_encoder,
                        text_encoder_2=self.pipeline.text_encoder_2,
                        tokenizer=self.pipeline.tokenizer,
                        tokenizer_2=self.pipeline.tokenizer_2,
                        unet=self.pipeline.unet,
                        scheduler=self.pipeline.scheduler,
                        controlnet=controlnet,
                    )
                else:
                    self.controlnet_pipeline = StableDiffusionControlNetPipeline(
                        vae=self.pipeline.vae,
                        text_encoder=self.pipeline.text_encoder,
                        tokenizer=self.pipeline.tokenizer,
                        unet=self.pipeline.unet,
                        scheduler=self.pipeline.scheduler,
                        safety_checker=getattr(self.pipeline, "safety_checker", None),
                        feature_extractor=getattr(self.pipeline, "feature_extractor", None),
                        controlnet=controlnet,
                    )

                self.controlnet_pipeline = self.controlnet_pipeline.to(self.device)

                # Apply optimizations
                if hasattr(self.controlnet_pipeline, "enable_attention_slicing"):
                    self.controlnet_pipeline.enable_attention_slicing()

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

                print(f"[Engine] Generating ControlNet image (task: {self._current_task_id}):")
                print(f"  Control type: {control_type}")
                print(f"  Prompt: {request.prompt[:100]}...")
                print(f"  Size: {request.width}x{request.height}")
                print(f"  Steps: {request.steps}")
                print(f"  Guidance: {request.guidance_scale}")
                print(f"  ControlNet scale: {request.controlnet_conditioning_scale}")
                print(f"  Seed: {seed}")

                start_time = time.time()

                # Build generation kwargs
                gen_kwargs = {
                    "prompt": request.prompt,
                    "image": control_image,
                    "width": request.width,
                    "height": request.height,
                    "num_inference_steps": request.steps,
                    "guidance_scale": request.guidance_scale,
                    "controlnet_conditioning_scale": request.controlnet_conditioning_scale,
                    "num_images_per_prompt": request.batch_size,
                    "generator": generator,
                }

                # Try new callback API first
                import inspect
                pipeline_call_sig = inspect.signature(self.controlnet_pipeline.__call__)
                if "callback_on_step_end" in pipeline_call_sig.parameters:
                    gen_kwargs["callback_on_step_end"] = self._progress_callback
                else:
                    gen_kwargs["callback"] = self._progress_callback_legacy
                    gen_kwargs["callback_steps"] = 1

                if request.negative_prompt:
                    gen_kwargs["negative_prompt"] = request.negative_prompt

                # CLIP skip
                if request.clip_skip > 1 and hasattr(self.controlnet_pipeline, "text_encoder"):
                    try:
                        if hasattr(self.controlnet_pipeline.text_encoder.config, "num_hidden_layers"):
                            orig_clip_layers = self.controlnet_pipeline.text_encoder.config.num_hidden_layers
                            self.controlnet_pipeline.text_encoder.config.num_hidden_layers = orig_clip_layers - (request.clip_skip - 1)
                    except Exception as e:
                        print(f"[Engine] CLIP skip not supported: {e}")

                # Generate!
                result = self.controlnet_pipeline(**gen_kwargs)

                gen_time = time.time() - start_time

                # Encode images to base64
                images = []
                for img in result.images:
                    buffer = io.BytesIO()
                    img.save(buffer, format="PNG", optimize=True)
                    b64 = base64.b64encode(buffer.getvalue()).decode("utf-8")
                    images.append(f"data:image/png;base64,{b64}")

                # Encode preprocessed control image for reference
                control_preview_buffer = io.BytesIO()
                control_image.save(control_preview_buffer, format="PNG", optimize=True)
                control_preview = f"data:image/png;base64,{base64.b64encode(control_preview_buffer.getvalue()).decode('utf-8')}"

                # Get VRAM usage
                vram_used = 0
                if self.device == "cuda":
                    vram_used = torch.cuda.memory_allocated() / 1024**3

                print(f"[Engine] ControlNet generation complete in {gen_time:.1f}s")

                return {
                    "success": True,
                    "images": images,
                    "control_preview": control_preview,
                    "control_type": control_type,
                    "seed": seed,
                    "generation_time": gen_time,
                    "vram_used_gb": vram_used,
                    "task_id": self._current_task_id,
                }

            except InterruptedError:
                return {"success": False, "error": "cancelled", "cancelled": True, "task_id": self._current_task_id}
            except Exception as e:
                traceback.print_exc()
                return {"success": False, "error": str(e), "task_id": self._current_task_id}
            finally:
                self._is_generating = False
                self._cancel_event.clear()
                # Reset CLIP skip
                if orig_clip_layers is not None and self.controlnet_pipeline is not None:
                    try:
                        if hasattr(self.controlnet_pipeline, "text_encoder"):
                            self.controlnet_pipeline.text_encoder.config.num_hidden_layers = orig_clip_layers
                    except Exception:
                        pass

    def get_available_controlnet_types(self) -> Dict[str, Any]:
        """Get available ControlNet types for current model."""
        model_lower = (self.current_model or "").lower()
        is_sdxl = "xl" in model_lower or "sdxl" in model_lower
        model_family = "sdxl" if is_sdxl else "sd15"

        available = self.CONTROLNET_MODELS.get(model_family, self.CONTROLNET_MODELS["sd15"])
        loaded = list(self.loaded_controlnets.keys())

        return {
            "model_family": model_family,
            "available_types": list(available.keys()),
            "loaded_types": loaded,
            "has_controlnet_aux": HAS_CONTROLNET_AUX,
        }

    # =========================================================================
    # Inpainting / Outpainting
    # =========================================================================

    def generate_inpaint(self, request: InpaintRequest, task_id: Optional[str] = None) -> Dict[str, Any]:
        """Generate inpainted image - edit specific parts using a mask."""
        if self.pipeline is None:
            return {"success": False, "error": "No base model loaded. Load a model first."}

        with self._lock:
            orig_clip_layers = None
            inpaint_pipeline = None
            try:
                # Reset state
                self._generation_progress = 0
                self._generation_step = 0
                self._generation_total_steps = request.steps
                self._cancel_event.clear()
                self._is_generating = True
                self._current_task_id = task_id or str(int(time.time() * 1000))

                # Set scheduler if specified
                if request.scheduler:
                    self._set_scheduler(request.scheduler)

                # Decode input image
                image_data = request.image
                if image_data.startswith("data:"):
                    image_data = image_data.split(",")[1]
                input_image = Image.open(io.BytesIO(base64.b64decode(image_data))).convert("RGB")
                input_image = input_image.resize((request.width, request.height), Image.Resampling.LANCZOS)

                # Decode mask image
                mask_data = request.mask
                if mask_data.startswith("data:"):
                    mask_data = mask_data.split(",")[1]
                mask_image = Image.open(io.BytesIO(base64.b64decode(mask_data))).convert("L")
                mask_image = mask_image.resize((request.width, request.height), Image.Resampling.LANCZOS)

                # Detect model type
                model_lower = (self.current_model or "").lower()
                is_sdxl = "xl" in model_lower or "sdxl" in model_lower

                # Create inpaint pipeline from base model components
                if is_sdxl:
                    # For SDXL, try to use dedicated inpaint model or create from base
                    try:
                        inpaint_pipeline = StableDiffusionXLInpaintPipeline(
                            vae=self.pipeline.vae,
                            text_encoder=self.pipeline.text_encoder,
                            text_encoder_2=self.pipeline.text_encoder_2,
                            tokenizer=self.pipeline.tokenizer,
                            tokenizer_2=self.pipeline.tokenizer_2,
                            unet=self.pipeline.unet,
                            scheduler=self.pipeline.scheduler,
                        )
                    except Exception as e:
                        print(f"[Engine] Failed to create SDXL inpaint pipeline: {e}")
                        return {"success": False, "error": f"SDXL inpainting requires compatible model: {e}"}
                else:
                    # For SD 1.5
                    try:
                        inpaint_pipeline = StableDiffusionInpaintPipeline(
                            vae=self.pipeline.vae,
                            text_encoder=self.pipeline.text_encoder,
                            tokenizer=self.pipeline.tokenizer,
                            unet=self.pipeline.unet,
                            scheduler=self.pipeline.scheduler,
                            safety_checker=getattr(self.pipeline, "safety_checker", None),
                            feature_extractor=getattr(self.pipeline, "feature_extractor", None),
                        )
                    except Exception as e:
                        print(f"[Engine] Failed to create SD inpaint pipeline: {e}")
                        return {"success": False, "error": f"Inpainting requires compatible model: {e}"}

                inpaint_pipeline = inpaint_pipeline.to(self.device)

                # Apply optimizations
                if hasattr(inpaint_pipeline, "enable_attention_slicing"):
                    inpaint_pipeline.enable_attention_slicing()

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

                print(f"[Engine] Generating inpaint image (task: {self._current_task_id}):")
                print(f"  Prompt: {request.prompt[:100]}...")
                print(f"  Size: {request.width}x{request.height}")
                print(f"  Steps: {request.steps}")
                print(f"  Strength: {request.strength}")
                print(f"  Seed: {seed}")

                start_time = time.time()

                # Build generation kwargs
                gen_kwargs = {
                    "prompt": request.prompt,
                    "image": input_image,
                    "mask_image": mask_image,
                    "width": request.width,
                    "height": request.height,
                    "num_inference_steps": request.steps,
                    "guidance_scale": request.guidance_scale,
                    "strength": request.strength,
                    "num_images_per_prompt": request.batch_size,
                    "generator": generator,
                }

                # Try new callback API first
                import inspect
                pipeline_call_sig = inspect.signature(inpaint_pipeline.__call__)
                if "callback_on_step_end" in pipeline_call_sig.parameters:
                    gen_kwargs["callback_on_step_end"] = self._progress_callback
                else:
                    gen_kwargs["callback"] = self._progress_callback_legacy
                    gen_kwargs["callback_steps"] = 1

                if request.negative_prompt:
                    gen_kwargs["negative_prompt"] = request.negative_prompt

                # CLIP skip
                if request.clip_skip > 1 and hasattr(inpaint_pipeline, "text_encoder"):
                    try:
                        if hasattr(inpaint_pipeline.text_encoder.config, "num_hidden_layers"):
                            orig_clip_layers = inpaint_pipeline.text_encoder.config.num_hidden_layers
                            inpaint_pipeline.text_encoder.config.num_hidden_layers = orig_clip_layers - (request.clip_skip - 1)
                    except Exception as e:
                        print(f"[Engine] CLIP skip not supported: {e}")

                # Generate!
                result = inpaint_pipeline(**gen_kwargs)

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

                print(f"[Engine] Inpaint generation complete in {gen_time:.1f}s")

                return {
                    "success": True,
                    "images": images,
                    "seed": seed,
                    "generation_time": gen_time,
                    "vram_used_gb": vram_used,
                    "task_id": self._current_task_id,
                }

            except InterruptedError:
                return {"success": False, "error": "cancelled", "cancelled": True, "task_id": self._current_task_id}
            except Exception as e:
                traceback.print_exc()
                return {"success": False, "error": str(e), "task_id": self._current_task_id}
            finally:
                self._is_generating = False
                self._cancel_event.clear()
                # Reset CLIP skip
                if orig_clip_layers is not None and inpaint_pipeline is not None:
                    try:
                        if hasattr(inpaint_pipeline, "text_encoder"):
                            inpaint_pipeline.text_encoder.config.num_hidden_layers = orig_clip_layers
                    except Exception:
                        pass
                # Cleanup
                if inpaint_pipeline is not None:
                    del inpaint_pipeline
                    gc.collect()

    def generate_outpaint(self, request: OutpaintRequest, task_id: Optional[str] = None) -> Dict[str, Any]:
        """Extend image canvas in specified direction(s)."""
        if self.pipeline is None:
            return {"success": False, "error": "No base model loaded. Load a model first."}

        try:
            import numpy as np

            # Decode input image
            image_data = request.image
            if image_data.startswith("data:"):
                image_data = image_data.split(",")[1]
            input_image = Image.open(io.BytesIO(base64.b64decode(image_data))).convert("RGB")

            orig_w, orig_h = input_image.size
            directions = [d.strip().lower() for d in request.direction.split(",")]

            # Calculate new canvas size
            new_w, new_h = orig_w, orig_h
            paste_x, paste_y = 0, 0

            for direction in directions:
                if direction == "left":
                    new_w += request.extend_pixels
                    paste_x = request.extend_pixels
                elif direction == "right":
                    new_w += request.extend_pixels
                elif direction == "top":
                    new_h += request.extend_pixels
                    paste_y = request.extend_pixels
                elif direction == "bottom":
                    new_h += request.extend_pixels

            # Create new canvas with the original image
            new_canvas = Image.new("RGB", (new_w, new_h), (128, 128, 128))  # Gray fill
            new_canvas.paste(input_image, (paste_x, paste_y))

            # Create mask - white where we need to generate, black where we keep
            mask = Image.new("L", (new_w, new_h), 255)  # Start all white (inpaint)
            # Create black rectangle for original image area
            mask_draw = Image.new("L", (orig_w, orig_h), 0)  # Black = keep
            mask.paste(mask_draw, (paste_x, paste_y))

            # Apply feathering to mask edges for smooth blending
            if request.feather_pixels > 0:
                mask_array = np.array(mask)
                for direction in directions:
                    feather = request.feather_pixels
                    if direction == "left":
                        # Gradient from right edge of paste area
                        for i in range(feather):
                            alpha = i / feather
                            x = paste_x + i
                            if x < new_w:
                                mask_array[:, x] = int(alpha * 255)
                    elif direction == "right":
                        for i in range(feather):
                            alpha = i / feather
                            x = paste_x + orig_w - 1 - i
                            if x >= 0:
                                mask_array[:, x] = int(alpha * 255)
                    elif direction == "top":
                        for i in range(feather):
                            alpha = i / feather
                            y = paste_y + i
                            if y < new_h:
                                mask_array[y, :] = int(alpha * 255)
                    elif direction == "bottom":
                        for i in range(feather):
                            alpha = i / feather
                            y = paste_y + orig_h - 1 - i
                            if y >= 0:
                                mask_array[y, :] = int(alpha * 255)
                mask = Image.fromarray(mask_array)

            # Now use inpainting on the extended canvas
            inpaint_request = InpaintRequest(
                prompt=request.prompt,
                negative_prompt=request.negative_prompt or "",
                image=self._image_to_base64(new_canvas),
                mask=self._image_to_base64(mask),
                width=new_w,
                height=new_h,
                steps=request.steps,
                guidance_scale=request.guidance_scale,
                strength=request.strength,
                seed=request.seed,
                scheduler=request.scheduler,
            )

            result = self.generate_inpaint(inpaint_request, task_id)

            if result.get("success"):
                result["original_size"] = {"width": orig_w, "height": orig_h}
                result["new_size"] = {"width": new_w, "height": new_h}
                result["directions"] = directions

            return result

        except Exception as e:
            traceback.print_exc()
            return {"success": False, "error": str(e)}

    def _image_to_base64(self, image: Image.Image) -> str:
        """Convert PIL Image to base64 string."""
        buffer = io.BytesIO()
        image.save(buffer, format="PNG")
        return f"data:image/png;base64,{base64.b64encode(buffer.getvalue()).decode('utf-8')}"

    # =========================================================================
    # Upscaling (ESRGAN/Real-ESRGAN)
    # =========================================================================

    def _load_upscaler(self, model_type: str = "realesrgan") -> bool:
        """Load an upscaler model."""
        if self.upscaler_model is not None and self.upscaler_type == model_type:
            return True  # Already loaded

        try:
            # Unload existing upscaler
            if self.upscaler_model is not None:
                del self.upscaler_model
                self.upscaler_model = None
                gc.collect()
                if torch.cuda.is_available():
                    torch.cuda.empty_cache()

            model_type_lower = model_type.lower()

            # Try to import Real-ESRGAN
            try:
                from realesrgan import RealESRGANer
                from basicsr.archs.rrdbnet_arch import RRDBNet

                # Model configurations
                model_configs = {
                    "realesrgan": {
                        "model_name": "RealESRGAN_x4plus",
                        "model_url": "https://github.com/xinntao/Real-ESRGAN/releases/download/v0.1.0/RealESRGAN_x4plus.pth",
                        "scale": 4,
                        "arch": lambda: RRDBNet(num_in_ch=3, num_out_ch=3, num_feat=64, num_block=23, num_grow_ch=32, scale=4),
                    },
                    "realesrgan-anime": {
                        "model_name": "RealESRGAN_x4plus_anime_6B",
                        "model_url": "https://github.com/xinntao/Real-ESRGAN/releases/download/v0.2.2.4/RealESRGAN_x4plus_anime_6B.pth",
                        "scale": 4,
                        "arch": lambda: RRDBNet(num_in_ch=3, num_out_ch=3, num_feat=64, num_block=6, num_grow_ch=32, scale=4),
                    },
                    "realesrgan-x2": {
                        "model_name": "RealESRGAN_x2plus",
                        "model_url": "https://github.com/xinntao/Real-ESRGAN/releases/download/v0.2.1/RealESRGAN_x2plus.pth",
                        "scale": 2,
                        "arch": lambda: RRDBNet(num_in_ch=3, num_out_ch=3, num_feat=64, num_block=23, num_grow_ch=32, scale=2),
                    },
                }

                config = model_configs.get(model_type_lower, model_configs["realesrgan"])

                # Check for local model first
                model_path = os.path.join(self.models_dir, "upscalers", f"{config['model_name']}.pth")
                if not os.path.exists(model_path):
                    model_path = None  # Will download from URL

                self.upscaler_model = RealESRGANer(
                    scale=config["scale"],
                    model_path=model_path,
                    model=config["arch"](),
                    tile=0,
                    tile_pad=10,
                    pre_pad=0,
                    half=self.device == "cuda",
                    device=self.device,
                )
                self.upscaler_type = model_type_lower
                print(f"[Engine] Loaded upscaler: {model_type_lower}")
                return True

            except ImportError:
                print("[Engine] Real-ESRGAN not installed. Install with: pip install realesrgan")
                return False

        except Exception as e:
            print(f"[Engine] Failed to load upscaler: {e}")
            traceback.print_exc()
            return False

    def generate_upscale(self, request: UpscaleRequest, task_id: Optional[str] = None) -> Dict[str, Any]:
        """Upscale an image using Real-ESRGAN or similar."""
        self._current_task_id = task_id or str(uuid.uuid4())
        self._is_generating = True
        self._generation_progress = 0

        try:
            # Load upscaler if needed
            if not self._load_upscaler(request.model):
                return {"success": False, "error": "Failed to load upscaler. Install realesrgan: pip install realesrgan basicsr"}

            # Decode input image
            image_data = request.image
            if image_data.startswith("data:"):
                image_data = image_data.split(",")[1]
            input_image = Image.open(io.BytesIO(base64.b64decode(image_data))).convert("RGB")

            import numpy as np
            import cv2

            # Convert to numpy array (BGR for OpenCV)
            img_array = np.array(input_image)
            img_bgr = cv2.cvtColor(img_array, cv2.COLOR_RGB2BGR)

            self._generation_progress = 10

            # Apply upscaling
            output, _ = self.upscaler_model.enhance(img_bgr, outscale=request.scale)

            self._generation_progress = 90

            # Convert back to RGB PIL Image
            output_rgb = cv2.cvtColor(output, cv2.COLOR_BGR2RGB)
            output_image = Image.fromarray(output_rgb)

            # Encode output
            buffer = io.BytesIO()
            fmt = "PNG" if request.output_format.lower() == "png" else "JPEG"
            output_image.save(buffer, format=fmt, quality=95 if fmt == "JPEG" else None)
            output_b64 = base64.b64encode(buffer.getvalue()).decode("utf-8")

            self._generation_progress = 100

            # VRAM usage
            vram_used_gb = 0
            if torch.cuda.is_available():
                vram_used_gb = torch.cuda.memory_allocated() / 1024**3

            return {
                "success": True,
                "task_id": self._current_task_id,
                "images": [f"data:image/{request.output_format};base64,{output_b64}"],
                "original_size": {"width": input_image.width, "height": input_image.height},
                "output_size": {"width": output_image.width, "height": output_image.height},
                "scale": request.scale,
                "model": request.model,
                "vram_used_gb": vram_used_gb,
            }

        except Exception as e:
            traceback.print_exc()
            return {"success": False, "error": str(e), "task_id": self._current_task_id}
        finally:
            self._is_generating = False

    # =========================================================================
    # IP-Adapter
    # =========================================================================

    def load_ip_adapter(self, adapter_name: str = "ip-adapter_sd15") -> Dict[str, Any]:
        """Load IP-Adapter for style/content transfer from reference images."""
        if not HAS_IP_ADAPTER:
            return {"success": False, "error": "IP-Adapter dependencies not installed. Install transformers."}

        if self.pipeline is None:
            return {"success": False, "error": "No base model loaded. Load a model first."}

        try:
            # Determine adapter based on model type
            is_sdxl = "xl" in (self.current_model or "").lower()

            if is_sdxl:
                adapter_id = "h94/IP-Adapter"
                subfolder = "sdxl_models"
                weight_name = "ip-adapter_sdxl.bin"
            else:
                adapter_id = "h94/IP-Adapter"
                subfolder = "models"
                weight_name = f"{adapter_name}.bin"

            print(f"[Engine] Loading IP-Adapter: {adapter_id}/{subfolder}/{weight_name}")

            # Load IP-Adapter into pipeline
            self.pipeline.load_ip_adapter(
                adapter_id,
                subfolder=subfolder,
                weight_name=weight_name,
            )

            # Load image encoder
            self.ip_adapter_image_encoder = CLIPVisionModelWithProjection.from_pretrained(
                "h94/IP-Adapter",
                subfolder="models/image_encoder",
                torch_dtype=self.dtype,
            ).to(self.device)

            self.ip_adapter_image_processor = CLIPImageProcessor.from_pretrained(
                "openai/clip-vit-large-patch14"
            )

            self.ip_adapter_loaded = True
            print("[Engine] IP-Adapter loaded successfully")

            return {"success": True, "adapter": adapter_name}

        except Exception as e:
            traceback.print_exc()
            return {"success": False, "error": str(e)}

    def unload_ip_adapter(self) -> Dict[str, Any]:
        """Unload IP-Adapter to free VRAM."""
        try:
            if self.pipeline is not None and hasattr(self.pipeline, "unload_ip_adapter"):
                self.pipeline.unload_ip_adapter()

            if self.ip_adapter_image_encoder is not None:
                del self.ip_adapter_image_encoder
                self.ip_adapter_image_encoder = None

            if self.ip_adapter_image_processor is not None:
                del self.ip_adapter_image_processor
                self.ip_adapter_image_processor = None

            self.ip_adapter_loaded = False
            gc.collect()
            if torch.cuda.is_available():
                torch.cuda.empty_cache()

            print("[Engine] IP-Adapter unloaded")
            return {"success": True}

        except Exception as e:
            return {"success": False, "error": str(e)}

    def generate_ip_adapter(self, request: IPAdapterRequest, task_id: Optional[str] = None) -> Dict[str, Any]:
        """Generate image using IP-Adapter for style/content transfer."""
        if self.pipeline is None:
            return {"success": False, "error": "No model loaded. Load a model first."}

        if not self.ip_adapter_loaded:
            # Try to load IP-Adapter
            load_result = self.load_ip_adapter()
            if not load_result.get("success"):
                return load_result

        self._current_task_id = task_id or str(uuid.uuid4())
        self._is_generating = True
        self._cancel_event.clear()

        orig_clip_layers = None

        try:
            # Decode reference images
            reference_pil_images = []
            for ref_b64 in request.reference_images:
                if ref_b64.startswith("data:"):
                    ref_b64 = ref_b64.split(",")[1]
                ref_img = Image.open(io.BytesIO(base64.b64decode(ref_b64))).convert("RGB")
                reference_pil_images.append(ref_img)

            if not reference_pil_images:
                return {"success": False, "error": "No reference images provided"}

            # Load LoRAs if specified
            if request.lora_models:
                for lora in request.lora_models:
                    self.load_lora(lora.get("path"), lora.get("weight", 1.0), lora.get("name"))

            # Apply CLIP skip
            if request.clip_skip > 1 and hasattr(self.pipeline, "text_encoder"):
                orig_clip_layers = self.pipeline.text_encoder.config.num_hidden_layers
                self.pipeline.text_encoder.config.num_hidden_layers = orig_clip_layers - (request.clip_skip - 1)

            # Set scheduler
            if request.scheduler:
                self._set_scheduler(request.scheduler)

            # Set seed
            seed = request.seed if request.seed >= 0 else torch.randint(0, 2**32, (1,)).item()
            generator = torch.Generator(device=self.device).manual_seed(seed)

            # Set IP-Adapter scale
            self.pipeline.set_ip_adapter_scale(request.ip_adapter_scale)

            # Progress callback
            self._generation_step = 0
            self._generation_total_steps = request.steps

            def progress_callback(pipe, step, timestep, callback_kwargs):
                self._generation_step = step + 1
                self._generation_progress = int((step + 1) / request.steps * 100)
                if self._cancel_event.is_set():
                    raise InterruptedError("Generation cancelled by user")
                return callback_kwargs

            # Generate
            output = self.pipeline(
                prompt=request.prompt,
                negative_prompt=request.negative_prompt,
                ip_adapter_image=reference_pil_images if len(reference_pil_images) > 1 else reference_pil_images[0],
                width=request.width,
                height=request.height,
                num_inference_steps=request.steps,
                guidance_scale=request.guidance_scale,
                generator=generator,
                num_images_per_prompt=request.batch_size,
                callback_on_step_end=progress_callback,
            )

            # Encode output images
            images_b64 = []
            for img in output.images:
                buffer = io.BytesIO()
                img.save(buffer, format="PNG")
                images_b64.append(f"data:image/png;base64,{base64.b64encode(buffer.getvalue()).decode('utf-8')}")

            # VRAM usage
            vram_used_gb = 0
            if torch.cuda.is_available():
                vram_used_gb = torch.cuda.memory_allocated() / 1024**3

            return {
                "success": True,
                "task_id": self._current_task_id,
                "images": images_b64,
                "seed": seed,
                "ip_adapter_scale": request.ip_adapter_scale,
                "reference_count": len(reference_pil_images),
                "vram_used_gb": vram_used_gb,
            }

        except InterruptedError:
            return {"success": False, "cancelled": True, "task_id": self._current_task_id}
        except Exception as e:
            traceback.print_exc()
            return {"success": False, "error": str(e), "task_id": self._current_task_id}
        finally:
            self._is_generating = False
            self._cancel_event.clear()
            # Reset CLIP skip
            if orig_clip_layers is not None:
                try:
                    self.pipeline.text_encoder.config.num_hidden_layers = orig_clip_layers
                except Exception:
                    pass

    # =========================================================================
    # Multi-ControlNet
    # =========================================================================

    def generate_multi_controlnet(self, request: MultiControlNetRequest, task_id: Optional[str] = None) -> Dict[str, Any]:
        """Generate image using multiple ControlNet conditions simultaneously."""
        if self.pipeline is None:
            return {"success": False, "error": "No model loaded. Load a model first."}

        if not request.controls or len(request.controls) < 2:
            return {"success": False, "error": "Multi-ControlNet requires at least 2 control conditions"}

        self._current_task_id = task_id or str(uuid.uuid4())
        self._is_generating = True
        self._cancel_event.clear()

        try:
            # Load all required ControlNets
            controlnets = []
            control_images = []
            controlnet_scales = []

            for ctrl in request.controls:
                control_type = ctrl.get("control_type", "canny")
                control_weight = ctrl.get("weight", 1.0)
                control_image_b64 = ctrl.get("control_image")
                preprocess = ctrl.get("preprocess", True)
                canny_low = ctrl.get("canny_low", 100)
                canny_high = ctrl.get("canny_high", 200)

                if not control_image_b64:
                    return {"success": False, "error": f"Missing control_image for {control_type}"}

                # Load ControlNet model if not already loaded
                if control_type not in self.loaded_controlnets:
                    load_result = self.load_controlnet(control_type)
                    if not load_result.get("success"):
                        return load_result

                controlnets.append(self.loaded_controlnets[control_type])

                # Decode and preprocess control image
                if control_image_b64.startswith("data:"):
                    control_image_b64 = control_image_b64.split(",")[1]
                ctrl_img = Image.open(io.BytesIO(base64.b64decode(control_image_b64))).convert("RGB")

                if preprocess:
                    ctrl_img = self._preprocess_control_image(ctrl_img, control_type, canny_low, canny_high)

                # Resize to match output size
                ctrl_img = ctrl_img.resize((request.width, request.height), Image.LANCZOS)
                control_images.append(ctrl_img)
                controlnet_scales.append(control_weight)

            # Determine pipeline class based on model
            is_sdxl = "xl" in (self.current_model or "").lower()
            if is_sdxl:
                from diffusers import StableDiffusionXLControlNetPipeline
                PipelineClass = StableDiffusionXLControlNetPipeline
            else:
                from diffusers import StableDiffusionControlNetPipeline
                PipelineClass = StableDiffusionControlNetPipeline

            # Create multi-controlnet pipeline
            multi_controlnet_pipe = PipelineClass.from_pipe(
                self.pipeline,
                controlnet=controlnets,
                torch_dtype=self.dtype,
            )
            multi_controlnet_pipe.to(self.device)

            # Load LoRAs if specified
            if request.lora_models:
                for lora in request.lora_models:
                    path = lora.get("path")
                    weight = lora.get("weight", 1.0)
                    name = lora.get("name")
                    if path:
                        multi_controlnet_pipe.load_lora_weights(path)
                        if name:
                            multi_controlnet_pipe.fuse_lora(lora_scale=weight)

            # Set scheduler
            if request.scheduler:
                sched_lower = request.scheduler.lower()
                if sched_lower in SCHEDULER_REGISTRY:
                    sched = SCHEDULER_REGISTRY[sched_lower]
                    if callable(sched) and not isinstance(sched, type):
                        multi_controlnet_pipe.scheduler = sched(multi_controlnet_pipe.scheduler.config)
                    else:
                        multi_controlnet_pipe.scheduler = sched.from_config(multi_controlnet_pipe.scheduler.config)

            # Set seed
            seed = request.seed if request.seed >= 0 else torch.randint(0, 2**32, (1,)).item()
            generator = torch.Generator(device=self.device).manual_seed(seed)

            # Progress callback
            self._generation_step = 0
            self._generation_total_steps = request.steps

            def progress_callback(pipe, step, timestep, callback_kwargs):
                self._generation_step = step + 1
                self._generation_progress = int((step + 1) / request.steps * 100)
                if self._cancel_event.is_set():
                    raise InterruptedError("Generation cancelled by user")
                return callback_kwargs

            # Generate
            output = multi_controlnet_pipe(
                prompt=request.prompt,
                negative_prompt=request.negative_prompt,
                image=control_images,
                width=request.width,
                height=request.height,
                num_inference_steps=request.steps,
                guidance_scale=request.guidance_scale,
                controlnet_conditioning_scale=controlnet_scales,
                generator=generator,
                num_images_per_prompt=request.batch_size,
                callback_on_step_end=progress_callback,
            )

            # Encode output images
            images_b64 = []
            for img in output.images:
                buffer = io.BytesIO()
                img.save(buffer, format="PNG")
                images_b64.append(f"data:image/png;base64,{base64.b64encode(buffer.getvalue()).decode('utf-8')}")

            # Cleanup
            del multi_controlnet_pipe
            gc.collect()
            if torch.cuda.is_available():
                torch.cuda.empty_cache()

            # VRAM usage
            vram_used_gb = 0
            if torch.cuda.is_available():
                vram_used_gb = torch.cuda.memory_allocated() / 1024**3

            return {
                "success": True,
                "task_id": self._current_task_id,
                "images": images_b64,
                "seed": seed,
                "control_types": [c.get("control_type") for c in request.controls],
                "control_scales": controlnet_scales,
                "vram_used_gb": vram_used_gb,
            }

        except InterruptedError:
            return {"success": False, "cancelled": True, "task_id": self._current_task_id}
        except Exception as e:
            traceback.print_exc()
            return {"success": False, "error": str(e), "task_id": self._current_task_id}
        finally:
            self._is_generating = False
            self._cancel_event.clear()

    # =========================================================================
    # Queue System
    # =========================================================================

    def queue_add_task(self, request: QueuedTaskRequest) -> Dict[str, Any]:
        """Add a task to the generation queue."""
        task_id = str(uuid.uuid4())
        task = {
            "task_id": task_id,
            "task_type": request.task_type,
            "request_data": request.request_data,
            "priority": request.priority,
            "status": "pending",
            "progress": 0,
            "result": None,
            "error": None,
            "created_at": time.time(),
            "started_at": None,
            "completed_at": None,
        }

        with self._queue_lock:
            # Insert based on priority (higher priority = earlier in queue)
            inserted = False
            for i, existing in enumerate(self._task_queue):
                if request.priority > existing.get("priority", 0):
                    self._task_queue.insert(i, task)
                    inserted = True
                    break
            if not inserted:
                self._task_queue.append(task)

            self._task_history[task_id] = task

        # Start queue processing if not running
        self._start_queue_processor()

        print(f"[Queue] Added task {task_id} (type={request.task_type}, priority={request.priority})")
        return {"success": True, "task_id": task_id, "position": self._get_task_position(task_id)}

    def queue_get_status(self, task_id: str) -> Dict[str, Any]:
        """Get status of a queued task."""
        with self._queue_lock:
            task = self._task_history.get(task_id)
            if task is None:
                return {"success": False, "error": "Task not found"}

            position = self._get_task_position(task_id) if task["status"] == "pending" else None

            return {
                "success": True,
                "task_id": task_id,
                "status": task["status"],
                "progress": task["progress"],
                "position": position,
                "result": task["result"],
                "error": task["error"],
                "created_at": task["created_at"],
                "started_at": task["started_at"],
                "completed_at": task["completed_at"],
            }

    def queue_cancel_task(self, task_id: str) -> Dict[str, Any]:
        """Cancel a queued task."""
        with self._queue_lock:
            task = self._task_history.get(task_id)
            if task is None:
                return {"success": False, "error": "Task not found"}

            if task["status"] == "pending":
                # Remove from queue
                self._task_queue = deque([t for t in self._task_queue if t["task_id"] != task_id])
                task["status"] = "cancelled"
                task["completed_at"] = time.time()
                return {"success": True, "message": "Task cancelled"}
            elif task["status"] == "processing":
                # Try to cancel current generation
                self.cancel_generation()
                return {"success": True, "message": "Cancellation requested"}
            else:
                return {"success": False, "error": f"Cannot cancel task in status: {task['status']}"}

    def queue_list(self) -> Dict[str, Any]:
        """List all tasks in the queue."""
        with self._queue_lock:
            pending = [t for t in self._task_queue]
            processing = [t for t in self._task_history.values() if t["status"] == "processing"]
            recent_completed = sorted(
                [t for t in self._task_history.values() if t["status"] in ("completed", "failed", "cancelled")],
                key=lambda x: x.get("completed_at", 0),
                reverse=True,
            )[:10]  # Last 10 completed

            return {
                "success": True,
                "queue_running": self._queue_running,
                "pending_count": len(pending),
                "pending": pending,
                "processing": processing,
                "recent_completed": recent_completed,
            }

    def queue_clear(self) -> Dict[str, Any]:
        """Clear all pending tasks from the queue."""
        with self._queue_lock:
            cleared_count = len(self._task_queue)
            for task in self._task_queue:
                task["status"] = "cancelled"
                task["completed_at"] = time.time()
            self._task_queue.clear()

        return {"success": True, "cleared_count": cleared_count}

    def _get_task_position(self, task_id: str) -> Optional[int]:
        """Get position of task in queue (0-indexed)."""
        for i, task in enumerate(self._task_queue):
            if task["task_id"] == task_id:
                return i
        return None

    def _start_queue_processor(self):
        """Start the queue processor thread if not running."""
        if self._queue_running:
            return

        self._queue_running = True
        self._queue_thread = Thread(target=self._process_queue, daemon=True)
        self._queue_thread.start()
        print("[Queue] Processor started")

    def _process_queue(self):
        """Process tasks in the queue (runs in background thread)."""
        while True:
            task = None
            with self._queue_lock:
                if not self._task_queue:
                    self._queue_running = False
                    print("[Queue] No more tasks, processor stopping")
                    return

                task = self._task_queue.popleft()
                task["status"] = "processing"
                task["started_at"] = time.time()

            if task:
                try:
                    print(f"[Queue] Processing task {task['task_id']} (type={task['task_type']})")
                    result = self._execute_queued_task(task)

                    with self._queue_lock:
                        task["result"] = result
                        task["status"] = "completed" if result.get("success") else "failed"
                        task["error"] = result.get("error")
                        task["completed_at"] = time.time()

                    print(f"[Queue] Task {task['task_id']} completed (success={result.get('success')})")

                except Exception as e:
                    traceback.print_exc()
                    with self._queue_lock:
                        task["status"] = "failed"
                        task["error"] = str(e)
                        task["completed_at"] = time.time()

    def _execute_queued_task(self, task: Dict[str, Any]) -> Dict[str, Any]:
        """Execute a queued task based on its type."""
        task_type = task["task_type"]
        request_data = task["request_data"]
        task_id = task["task_id"]

        # Update progress in task
        def update_progress():
            with self._queue_lock:
                task["progress"] = self._generation_progress

        # Type-specific execution
        if task_type == "image":
            req = ImageGenerationRequest(**request_data)
            return self.generate_image(req, task_id)
        elif task_type == "img2img":
            req = Img2ImgRequest(**request_data)
            return self.generate_img2img(req, task_id)
        elif task_type == "video":
            req = VideoGenerationRequest(**request_data)
            return self.generate_video(req, task_id)
        elif task_type == "controlnet":
            req = ControlNetRequest(**request_data)
            return self.generate_controlnet(req, task_id)
        elif task_type == "multi_controlnet":
            req = MultiControlNetRequest(**request_data)
            return self.generate_multi_controlnet(req, task_id)
        elif task_type == "inpaint":
            req = InpaintRequest(**request_data)
            return self.generate_inpaint(req, task_id)
        elif task_type == "outpaint":
            req = OutpaintRequest(**request_data)
            return self.generate_outpaint(req, task_id)
        elif task_type == "upscale":
            req = UpscaleRequest(**request_data)
            return self.generate_upscale(req, task_id)
        elif task_type == "ip_adapter":
            req = IPAdapterRequest(**request_data)
            return self.generate_ip_adapter(req, task_id)
        else:
            return {"success": False, "error": f"Unknown task type: {task_type}"}

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

        # Get ControlNet info
        controlnet_info = self.get_available_controlnet_types() if self.current_model else {}

        # Queue info
        queue_info = {
            "running": self._queue_running,
            "pending_count": len(self._task_queue),
        }

        return {
            "status": "ready" if self.pipeline else "idle",
            "device": self.device,
            "dtype": str(self.dtype),
            "current_model": self.current_model,
            "current_model_type": self.current_model_type,
            "loaded_loras": self.loaded_loras,
            "loaded_controlnets": list(self.loaded_controlnets.keys()),
            "controlnet": controlnet_info,
            "ip_adapter_loaded": self.ip_adapter_loaded,
            "upscaler_loaded": self.upscaler_type,
            "queue": queue_info,
            "gpu": gpu_info,
            "has_diffusers": HAS_DIFFUSERS,
            "has_controlnet_aux": HAS_CONTROLNET_AUX,
            "has_ip_adapter": HAS_IP_ADAPTER,
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
    # Startup message
    print("=" * 60)
    print("[Server] [OK] PostXAgent Diffusers Server is ready")
    print("[Server] [OK] Ready to accept requests")
    print("=" * 60)
    yield
    # Cleanup on shutdown
    print("[Server] Shutting down...")
    if engine:
        engine.unload_model()
    print("[Server] Shutdown complete")

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


@app.get("/startup-status")
async def get_startup_status():
    """Get startup validation status with missing features."""
    return {
        "success": STARTUP_VALIDATION.get("success", False),
        "can_continue": STARTUP_VALIDATION.get("can_continue", False),
        "errors": STARTUP_VALIDATION.get("errors", []),
        "warnings": STARTUP_VALIDATION.get("warnings", []),
        "missing_features": STARTUP_VALIDATION.get("missing_features", []),
        "steps": STARTUP_VALIDATION.get("steps", []),
        "has_gpu": torch.cuda.is_available() if torch else False,
        "gpu_name": torch.cuda.get_device_name(0) if torch and torch.cuda.is_available() else None,
        "controlnet_available": HAS_CONTROLNET_AUX,
        "ip_adapter_available": HAS_IP_ADAPTER,
    }


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
    try:
        result = engine.load_model(request)
        if not result.get("success"):
            error_msg = result.get("error", "Unknown error")
            print(f"[Server] Load model failed: {error_msg}")
            raise HTTPException(status_code=400, detail=error_msg)
        return result
    except HTTPException:
        raise
    except Exception as e:
        import traceback
        error_detail = f"{str(e)}\n{traceback.format_exc()}"
        print(f"[Server] Load model exception: {error_detail}")
        raise HTTPException(status_code=500, detail=str(e))


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


# ============================================================================
# ControlNet Endpoints
# ============================================================================

@app.post("/generate/controlnet")
async def generate_controlnet(request: ControlNetRequest):
    """Generate image with ControlNet guidance.

    Control types available:
    - SD 1.5: canny, pose, depth, hed, lineart, scribble, softedge, normal, tile, inpaint, seg
    - SDXL: canny, depth, pose

    Set preprocess=true to automatically extract edges/pose/depth from the image.
    Set preprocess=false if you're providing a pre-processed control image.
    """
    if engine is None:
        raise HTTPException(status_code=500, detail="Engine not initialized")
    result = engine.generate_controlnet(request)
    if not result.get("success"):
        raise HTTPException(status_code=400, detail=result.get("error", "Unknown error"))
    return result


@app.get("/controlnet/types")
async def get_controlnet_types():
    """Get available ControlNet types for the current loaded model."""
    if engine is None:
        raise HTTPException(status_code=500, detail="Engine not initialized")
    return engine.get_available_controlnet_types()


@app.post("/controlnet/load")
async def load_controlnet(control_type: str, custom_model: Optional[str] = None):
    """Pre-load a ControlNet model to speed up first generation."""
    if engine is None:
        raise HTTPException(status_code=500, detail="Engine not initialized")
    result = engine.load_controlnet(control_type, custom_model)
    if not result.get("success"):
        raise HTTPException(status_code=400, detail=result.get("error", "Unknown error"))
    return result


@app.post("/controlnet/unload")
async def unload_controlnets():
    """Unload all ControlNet models to free VRAM."""
    if engine is None:
        raise HTTPException(status_code=500, detail="Engine not initialized")
    engine.unload_controlnets()
    return {"success": True}


# ============================================================================
# Inpainting / Outpainting Endpoints
# ============================================================================

@app.post("/generate/inpaint")
async def generate_inpaint(request: InpaintRequest):
    """Inpaint (edit) specific parts of an image using a mask.

    The mask should be a grayscale image where:
    - White (255) = area to inpaint/regenerate
    - Black (0) = area to keep unchanged

    Strength controls how much the masked area changes:
    - 1.0 = completely regenerate (ignore original pixels in mask)
    - 0.5 = blend original with generated
    - 0.0 = no change (pointless)
    """
    if engine is None:
        raise HTTPException(status_code=500, detail="Engine not initialized")
    result = engine.generate_inpaint(request)
    if not result.get("success"):
        raise HTTPException(status_code=400, detail=result.get("error", "Unknown error"))
    return result


@app.post("/generate/outpaint")
async def generate_outpaint(request: OutpaintRequest):
    """Extend the image canvas in specified direction(s).

    Directions can be: left, right, top, bottom, or combinations like "left,top"

    This automatically:
    1. Creates an extended canvas
    2. Generates a feathered mask for smooth blending
    3. Uses inpainting to fill the new area
    """
    if engine is None:
        raise HTTPException(status_code=500, detail="Engine not initialized")
    result = engine.generate_outpaint(request)
    if not result.get("success"):
        raise HTTPException(status_code=400, detail=result.get("error", "Unknown error"))
    return result


@app.get("/progress")
async def get_progress():
    """Get current generation progress."""
    if engine is None:
        raise HTTPException(status_code=500, detail="Engine not initialized")
    return engine.get_progress()


@app.post("/cancel")
async def cancel_generation():
    """Cancel current generation."""
    if engine is None:
        raise HTTPException(status_code=500, detail="Engine not initialized")
    cancelled = engine.cancel_generation()
    return {"success": cancelled, "message": "Cancellation requested" if cancelled else "No generation in progress"}


# ============================================================================
# Upscaling Endpoints
# ============================================================================

@app.post("/generate/upscale")
async def generate_upscale(request: UpscaleRequest):
    """Upscale an image using Real-ESRGAN.

    Available models:
    - realesrgan: General purpose 4x upscaler
    - realesrgan-anime: Optimized for anime/illustration
    - realesrgan-x2: 2x upscaler

    Requires: pip install realesrgan basicsr
    """
    if engine is None:
        raise HTTPException(status_code=500, detail="Engine not initialized")
    result = engine.generate_upscale(request)
    if not result.get("success"):
        raise HTTPException(status_code=400, detail=result.get("error", "Unknown error"))
    return result


# ============================================================================
# IP-Adapter Endpoints
# ============================================================================

@app.post("/ip-adapter/load")
async def load_ip_adapter(adapter_name: str = "ip-adapter_sd15"):
    """Load IP-Adapter for style/content transfer.

    Available adapters for SD 1.5:
    - ip-adapter_sd15: General purpose
    - ip-adapter_sd15_light: Lighter influence
    - ip-adapter-plus_sd15: More faithful to reference
    - ip-adapter-plus-face_sd15: Face-focused

    For SDXL, the adapter is auto-selected.
    """
    if engine is None:
        raise HTTPException(status_code=500, detail="Engine not initialized")
    result = engine.load_ip_adapter(adapter_name)
    if not result.get("success"):
        raise HTTPException(status_code=400, detail=result.get("error", "Unknown error"))
    return result


@app.post("/ip-adapter/unload")
async def unload_ip_adapter():
    """Unload IP-Adapter to free VRAM."""
    if engine is None:
        raise HTTPException(status_code=500, detail="Engine not initialized")
    result = engine.unload_ip_adapter()
    return result


@app.post("/generate/ip-adapter")
async def generate_ip_adapter(request: IPAdapterRequest):
    """Generate image using IP-Adapter for style/content transfer.

    Uses reference images to guide the generation style/content.
    The ip_adapter_scale controls how much influence the reference has:
    - 0.0 = no influence (just prompt)
    - 0.6 = balanced (default)
    - 1.0+ = strong reference influence
    """
    if engine is None:
        raise HTTPException(status_code=500, detail="Engine not initialized")
    result = engine.generate_ip_adapter(request)
    if not result.get("success"):
        raise HTTPException(status_code=400, detail=result.get("error", "Unknown error"))
    return result


# ============================================================================
# Multi-ControlNet Endpoints
# ============================================================================

@app.post("/generate/multi-controlnet")
async def generate_multi_controlnet(request: MultiControlNetRequest):
    """Generate image using multiple ControlNet conditions simultaneously.

    Example controls array:
    [
        {"control_type": "canny", "control_image": "base64...", "weight": 1.0},
        {"control_type": "depth", "control_image": "base64...", "weight": 0.8}
    ]

    Each control condition can have:
    - control_type: canny, pose, depth, hed, lineart, etc.
    - control_image: base64 encoded image
    - weight: conditioning scale (0.0-2.0)
    - preprocess: whether to auto-preprocess (default true)
    """
    if engine is None:
        raise HTTPException(status_code=500, detail="Engine not initialized")
    result = engine.generate_multi_controlnet(request)
    if not result.get("success"):
        raise HTTPException(status_code=400, detail=result.get("error", "Unknown error"))
    return result


# ============================================================================
# Queue System Endpoints
# ============================================================================

@app.post("/queue/add")
async def queue_add_task(request: QueuedTaskRequest):
    """Add a task to the generation queue.

    Task types: image, img2img, video, controlnet, multi_controlnet,
                inpaint, outpaint, upscale, ip_adapter

    Priority: 0-10 (higher = processed first)

    Example:
    {
        "task_type": "image",
        "request_data": {"prompt": "A cat", "width": 512, "height": 512},
        "priority": 5
    }
    """
    if engine is None:
        raise HTTPException(status_code=500, detail="Engine not initialized")
    return engine.queue_add_task(request)


@app.get("/queue/status/{task_id}")
async def queue_get_status(task_id: str):
    """Get status of a queued task."""
    if engine is None:
        raise HTTPException(status_code=500, detail="Engine not initialized")
    return engine.queue_get_status(task_id)


@app.post("/queue/cancel/{task_id}")
async def queue_cancel_task(task_id: str):
    """Cancel a queued task."""
    if engine is None:
        raise HTTPException(status_code=500, detail="Engine not initialized")
    return engine.queue_cancel_task(task_id)


@app.get("/queue/list")
async def queue_list():
    """List all tasks in the queue."""
    if engine is None:
        raise HTTPException(status_code=500, detail="Engine not initialized")
    return engine.queue_list()


@app.post("/queue/clear")
async def queue_clear():
    """Clear all pending tasks from the queue."""
    if engine is None:
        raise HTTPException(status_code=500, detail="Engine not initialized")
    return engine.queue_clear()


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
    parser.add_argument("--hf-token", type=str, default=None, help="HuggingFace API token for gated models")
    args = parser.parse_args()

    # Set HuggingFace token if provided
    if args.hf_token:
        os.environ["HF_TOKEN"] = args.hf_token
        os.environ["HUGGING_FACE_HUB_TOKEN"] = args.hf_token
        print(f"[Server] HuggingFace token configured (length: {len(args.hf_token)})")
        # Also try to login via huggingface_hub if available
        try:
            from huggingface_hub import login
            login(token=args.hf_token, add_to_git_credential=False)
            print("[Server] [OK] Logged in to HuggingFace")
        except Exception as e:
            print(f"[Server] [WARN] Could not login to HuggingFace: {e}")

    # Initialize engine
    engine = DiffusersEngine(args.models_dir, low_vram_mode=args.low_vram)

    print(f"[Server] Starting on {args.host}:{args.port}")
    print(f"[Server] Models directory: {args.models_dir}")

    # Run server
    uvicorn.run(app, host=args.host, port=args.port, log_level="info")


if __name__ == "__main__":
    main()
