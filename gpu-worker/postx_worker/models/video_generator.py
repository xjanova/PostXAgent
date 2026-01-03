"""
Video Generation Module
=======================
Support for AI video generation models.
"""
import logging
import torch
from pathlib import Path
from typing import Optional, List, Dict, Any
from dataclasses import dataclass
from PIL import Image
import io
import base64

logger = logging.getLogger(__name__)


@dataclass
class VideoRequest:
    """Video generation request"""
    prompt: str
    negative_prompt: str = ""
    width: int = 512
    height: int = 512
    num_frames: int = 16
    fps: int = 8
    steps: int = 25
    guidance_scale: float = 7.5
    seed: int = -1
    model_id: str = "ali-vilab/text-to-video-ms-1.7b"


@dataclass
class VideoResult:
    """Video generation result"""
    frames: List[Image.Image]
    fps: int
    seed: int
    generation_time: float
    model_id: str

    def to_base64_frames(self) -> List[str]:
        """Convert frames to base64 strings"""
        result = []
        for frame in self.frames:
            buffer = io.BytesIO()
            frame.save(buffer, format="PNG")
            buffer.seek(0)
            result.append(base64.b64encode(buffer.read()).decode())
        return result

    def save_video(self, output_path: str, codec: str = "mp4v") -> bool:
        """Save frames as video file"""
        try:
            import cv2
            import numpy as np

            if not self.frames:
                return False

            # Get dimensions from first frame
            first_frame = np.array(self.frames[0])
            height, width = first_frame.shape[:2]

            # Create video writer
            fourcc = cv2.VideoWriter_fourcc(*codec)
            out = cv2.VideoWriter(output_path, fourcc, self.fps, (width, height))

            for frame in self.frames:
                # Convert PIL to numpy and BGR
                frame_np = np.array(frame)
                if frame_np.shape[-1] == 4:  # RGBA
                    frame_np = frame_np[:, :, :3]
                frame_bgr = cv2.cvtColor(frame_np, cv2.COLOR_RGB2BGR)
                out.write(frame_bgr)

            out.release()
            return True

        except Exception as e:
            logger.error(f"Failed to save video: {e}")
            return False


class VideoGenerator:
    """
    Video generator supporting multiple models.
    """

    SUPPORTED_MODELS = {
        # Text-to-Video
        "ali-vilab/text-to-video-ms-1.7b": {"type": "t2v", "vram": 8},
        "damo-vilab/text-to-video-ms-1.7b-legacy": {"type": "t2v", "vram": 8},

        # AnimateDiff
        "guoyww/animatediff-motion-adapter-v1-5-2": {"type": "animatediff", "vram": 6},

        # Stable Video Diffusion
        "stabilityai/stable-video-diffusion-img2vid": {"type": "svd", "vram": 16},
        "stabilityai/stable-video-diffusion-img2vid-xt": {"type": "svd-xt", "vram": 24},
    }

    def __init__(self, device: str = "cuda", dtype: torch.dtype = torch.float16):
        self.device = device
        self.dtype = dtype
        self.current_model_id: Optional[str] = None
        self.pipeline = None

    def load_model(self, model_id: str) -> bool:
        """Load a video model"""
        if self.current_model_id == model_id and self.pipeline is not None:
            logger.info(f"Model {model_id} already loaded")
            return True

        try:
            self.unload_model()
            logger.info(f"Loading video model: {model_id}")

            model_info = self.SUPPORTED_MODELS.get(model_id, {"type": "t2v", "vram": 8})
            model_type = model_info["type"]

            if model_type == "t2v":
                from diffusers import DiffusionPipeline

                self.pipeline = DiffusionPipeline.from_pretrained(
                    model_id,
                    torch_dtype=self.dtype,
                )

            elif model_type == "animatediff":
                from diffusers import AnimateDiffPipeline, MotionAdapter, DDIMScheduler

                adapter = MotionAdapter.from_pretrained(
                    model_id,
                    torch_dtype=self.dtype,
                )
                self.pipeline = AnimateDiffPipeline.from_pretrained(
                    "runwayml/stable-diffusion-v1-5",
                    motion_adapter=adapter,
                    torch_dtype=self.dtype,
                )
                self.pipeline.scheduler = DDIMScheduler.from_config(
                    self.pipeline.scheduler.config,
                    clip_sample=False,
                    timestep_spacing="linspace",
                    beta_schedule="linear",
                    steps_offset=1,
                )

            elif model_type in ["svd", "svd-xt"]:
                from diffusers import StableVideoDiffusionPipeline

                self.pipeline = StableVideoDiffusionPipeline.from_pretrained(
                    model_id,
                    torch_dtype=self.dtype,
                )

            else:
                raise ValueError(f"Unknown video model type: {model_type}")

            self.pipeline = self.pipeline.to(self.device)

            # Enable optimizations
            if hasattr(self.pipeline, 'enable_attention_slicing'):
                self.pipeline.enable_attention_slicing()

            self.current_model_id = model_id
            logger.info(f"Video model {model_id} loaded successfully")
            return True

        except Exception as e:
            logger.error(f"Failed to load video model {model_id}: {e}")
            return False

    def unload_model(self):
        """Unload current model"""
        if self.pipeline is not None:
            del self.pipeline
            self.pipeline = None
            self.current_model_id = None

            if torch.cuda.is_available():
                torch.cuda.empty_cache()

            logger.info("Video model unloaded")

    def generate(self, request: VideoRequest) -> Optional[VideoResult]:
        """Generate video from request"""
        import time

        if self.current_model_id != request.model_id:
            if not self.load_model(request.model_id):
                return None

        try:
            start_time = time.time()

            seed = request.seed if request.seed >= 0 else torch.randint(0, 2**32 - 1, (1,)).item()
            generator = torch.Generator(device=self.device).manual_seed(seed)

            model_info = self.SUPPORTED_MODELS.get(request.model_id, {"type": "t2v"})
            model_type = model_info["type"]

            if model_type == "t2v":
                output = self.pipeline(
                    prompt=request.prompt,
                    negative_prompt=request.negative_prompt or None,
                    num_frames=request.num_frames,
                    width=request.width,
                    height=request.height,
                    num_inference_steps=request.steps,
                    guidance_scale=request.guidance_scale,
                    generator=generator,
                )
                frames = output.frames[0]

            elif model_type == "animatediff":
                output = self.pipeline(
                    prompt=request.prompt,
                    negative_prompt=request.negative_prompt or None,
                    num_frames=request.num_frames,
                    width=request.width,
                    height=request.height,
                    num_inference_steps=request.steps,
                    guidance_scale=request.guidance_scale,
                    generator=generator,
                )
                frames = output.frames[0]

            else:
                logger.error(f"Generation not implemented for {model_type}")
                return None

            # Convert to PIL images if needed
            pil_frames = []
            for frame in frames:
                if isinstance(frame, Image.Image):
                    pil_frames.append(frame)
                else:
                    pil_frames.append(Image.fromarray(frame))

            generation_time = time.time() - start_time

            return VideoResult(
                frames=pil_frames,
                fps=request.fps,
                seed=seed,
                generation_time=generation_time,
                model_id=request.model_id,
            )

        except Exception as e:
            logger.error(f"Video generation failed: {e}")
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


# Global instance
video_generator: Optional[VideoGenerator] = None


def get_video_generator() -> VideoGenerator:
    """Get or create the global video generator"""
    global video_generator
    if video_generator is None:
        device = "cuda" if torch.cuda.is_available() else "cpu"
        dtype = torch.float16 if device == "cuda" else torch.float32
        video_generator = VideoGenerator(device=device, dtype=dtype)
    return video_generator
