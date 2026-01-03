"""
Configuration for PostX GPU Worker
"""
import os
from dataclasses import dataclass, field
from typing import Optional, List
from pathlib import Path


@dataclass
class WorkerConfig:
    """GPU Worker Configuration"""

    # Worker Identity
    worker_id: str = field(default_factory=lambda: os.getenv("WORKER_ID", "worker-1"))
    worker_name: str = field(default_factory=lambda: os.getenv("WORKER_NAME", "PostX GPU Worker"))

    # Server Connection
    master_url: str = field(default_factory=lambda: os.getenv("MASTER_URL", "http://localhost:5000"))
    api_key: str = field(default_factory=lambda: os.getenv("API_KEY", ""))

    # API Server
    host: str = "0.0.0.0"
    port: int = field(default_factory=lambda: int(os.getenv("PORT", "8000")))

    # Model Storage
    models_dir: Path = field(default_factory=lambda: Path(os.getenv("MODELS_DIR", "./models")))
    cache_dir: Path = field(default_factory=lambda: Path(os.getenv("CACHE_DIR", "./cache")))
    output_dir: Path = field(default_factory=lambda: Path(os.getenv("OUTPUT_DIR", "./outputs")))

    # GPU Settings
    device: str = "cuda"  # cuda, cpu, mps
    gpu_ids: List[int] = field(default_factory=lambda: [0])
    max_vram_usage: float = 0.9  # 90% of VRAM

    # Generation Settings
    default_steps: int = 30
    default_guidance: float = 7.5
    max_batch_size: int = 4

    # HuggingFace
    hf_token: Optional[str] = field(default_factory=lambda: os.getenv("HF_TOKEN"))

    # Distributed Mode
    distributed_mode: str = "parallel"  # parallel, combined, auto

    def __post_init__(self):
        """Create directories if not exist"""
        self.models_dir.mkdir(parents=True, exist_ok=True)
        self.cache_dir.mkdir(parents=True, exist_ok=True)
        self.output_dir.mkdir(parents=True, exist_ok=True)


@dataclass
class ModelInfo:
    """Information about a loaded model"""
    model_id: str
    model_type: str  # sd15, sdxl, flux, svd
    vram_required: float  # in GB
    is_loaded: bool = False
    local_path: Optional[Path] = None


# Default configuration
config = WorkerConfig()
