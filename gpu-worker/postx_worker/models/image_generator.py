"""
Image Generation Module
=======================
Support for various image generation models from HuggingFace.
"""
import logging
import torch
from pathlib import Path
from typing import Optional, List, Dict, Any, Union
from dataclasses import dataclass
from PIL import Image
import io
import base64

logger = logging.getLogger(__name__)


@dataclass
class GenerationRequest:
    """Image generation request"""
    prompt: str
    negative_prompt: str = ""
    width: int = 1024
    height: int = 1024
    steps: int = 30
    guidance_scale: float = 7.5
    seed: int = -1
    batch_size: int = 1
    model_id: str = "stabilityai/stable-diffusion-xl-base-1.0"


@dataclass
class GenerationResult:
    """Image generation result"""
    images: List[Image.Image]
    seed: int
    generation_time: float
    model_id: str

    def to_base64(self) -> List[str]:
        """Convert images to base64 strings"""
        result = []
        for img in self.images:
            buffer = io.BytesIO()
            img.save(buffer, format="PNG")
            buffer.seek(0)
            result.append(base64.b64encode(buffer.read()).decode())
        return result


class ImageGenerator:
    """
    Unified image generator supporting multiple models.
    """

    SUPPORTED_MODELS = {
        # Stable Diffusion XL
        "stabilityai/stable-diffusion-xl-base-1.0": {"type": "sdxl", "vram": 8},
        "stabilityai/sdxl-turbo": {"type": "sdxl-turbo", "vram": 8},

        # Stable Diffusion 1.5
        "runwayml/stable-diffusion-v1-5": {"type": "sd15", "vram": 4},
        "dreamlike-art/dreamlike-photoreal-2.0": {"type": "sd15", "vram": 4},

        # FLUX
        "black-forest-labs/FLUX.1-schnell": {"type": "flux", "vram": 12},
        "black-forest-labs/FLUX.1-dev": {"type": "flux", "vram": 24},

        # Realistic
        "SG161222/Realistic_Vision_V5.1_noVAE": {"type": "sd15", "vram": 4},
    }

    def __init__(self, device: str = "cuda", dtype: torch.dtype = torch.float16):
        self.device = device
        self.dtype = dtype
        self.current_model_id: Optional[str] = None
        self.pipeline = None
        self._model_cache: Dict[str, Any] = {}

    def load_model(self, model_id: str, use_cache: bool = True) -> bool:
        """Load a model from HuggingFace"""
        if self.current_model_id == model_id and self.pipeline is not None:
            logger.info(f"Model {model_id} already loaded")
            return True

        try:
            # Unload current model
            self.unload_model()

            logger.info(f"Loading model: {model_id}")

            model_info = self.SUPPORTED_MODELS.get(model_id, {"type": "sdxl", "vram": 8})
            model_type = model_info["type"]

            if model_type in ["sdxl", "sdxl-turbo"]:
                from diffusers import StableDiffusionXLPipeline, AutoencoderKL

                # Load with optimizations
                self.pipeline = StableDiffusionXLPipeline.from_pretrained(
                    model_id,
                    torch_dtype=self.dtype,
                    use_safetensors=True,
                    variant="fp16" if self.dtype == torch.float16 else None,
                )

            elif model_type == "sd15":
                from diffusers import StableDiffusionPipeline

                self.pipeline = StableDiffusionPipeline.from_pretrained(
                    model_id,
                    torch_dtype=self.dtype,
                    use_safetensors=True,
                )

            elif model_type == "flux":
                from diffusers import FluxPipeline

                self.pipeline = FluxPipeline.from_pretrained(
                    model_id,
                    torch_dtype=self.dtype,
                )

            else:
                raise ValueError(f"Unknown model type: {model_type}")

            # Move to device and enable optimizations
            self.pipeline = self.pipeline.to(self.device)

            # Enable memory optimizations
            if hasattr(self.pipeline, 'enable_attention_slicing'):
                self.pipeline.enable_attention_slicing()

            if hasattr(self.pipeline, 'enable_vae_slicing'):
                self.pipeline.enable_vae_slicing()

            self.current_model_id = model_id
            logger.info(f"Model {model_id} loaded successfully")
            return True

        except Exception as e:
            logger.error(f"Failed to load model {model_id}: {e}")
            return False

    def unload_model(self):
        """Unload current model and free VRAM"""
        if self.pipeline is not None:
            del self.pipeline
            self.pipeline = None
            self.current_model_id = None

            # Clear CUDA cache
            if torch.cuda.is_available():
                torch.cuda.empty_cache()

            logger.info("Model unloaded, VRAM freed")

    def generate(self, request: GenerationRequest) -> Optional[GenerationResult]:
        """Generate images from a request"""
        import time

        # Ensure model is loaded
        if self.current_model_id != request.model_id:
            if not self.load_model(request.model_id):
                return None

        try:
            start_time = time.time()

            # Set seed
            seed = request.seed if request.seed >= 0 else torch.randint(0, 2**32 - 1, (1,)).item()
            generator = torch.Generator(device=self.device).manual_seed(seed)

            # Generate
            output = self.pipeline(
                prompt=request.prompt,
                negative_prompt=request.negative_prompt or None,
                width=request.width,
                height=request.height,
                num_inference_steps=request.steps,
                guidance_scale=request.guidance_scale,
                num_images_per_prompt=request.batch_size,
                generator=generator,
            )

            generation_time = time.time() - start_time

            return GenerationResult(
                images=output.images,
                seed=seed,
                generation_time=generation_time,
                model_id=request.model_id,
            )

        except Exception as e:
            logger.error(f"Generation failed: {e}")
            return None

    def get_model_info(self, model_id: str) -> Optional[Dict[str, Any]]:
        """Get information about a model"""
        if model_id in self.SUPPORTED_MODELS:
            info = self.SUPPORTED_MODELS[model_id].copy()
            info["model_id"] = model_id
            info["is_loaded"] = self.current_model_id == model_id
            return info
        return None

    def list_models(self) -> List[Dict[str, Any]]:
        """List all supported models"""
        models = []
        for model_id, info in self.SUPPORTED_MODELS.items():
            model_info = info.copy()
            model_info["model_id"] = model_id
            model_info["is_loaded"] = self.current_model_id == model_id
            models.append(model_info)
        return models


# Global generator instance
image_generator: Optional[ImageGenerator] = None


def get_generator() -> ImageGenerator:
    """Get or create the global image generator"""
    global image_generator
    if image_generator is None:
        device = "cuda" if torch.cuda.is_available() else "cpu"
        dtype = torch.float16 if device == "cuda" else torch.float32
        image_generator = ImageGenerator(device=device, dtype=dtype)
    return image_generator
