"""
GPU Monitoring Utilities
========================
Monitor GPU usage, VRAM, temperature, and power consumption.
"""
import logging
from dataclasses import dataclass
from typing import List, Optional, Dict, Any
import platform

logger = logging.getLogger(__name__)


@dataclass
class GPUInfo:
    """Information about a single GPU"""
    index: int
    name: str
    total_memory: float  # GB
    used_memory: float  # GB
    free_memory: float  # GB
    utilization: float  # percentage
    temperature: float  # Celsius
    power_draw: float  # Watts
    power_limit: float  # Watts

    @property
    def memory_percent(self) -> float:
        return (self.used_memory / self.total_memory) * 100 if self.total_memory > 0 else 0

    @property
    def power_percent(self) -> float:
        return (self.power_draw / self.power_limit) * 100 if self.power_limit > 0 else 0

    def to_dict(self) -> Dict[str, Any]:
        return {
            "index": self.index,
            "name": self.name,
            "total_memory_gb": round(self.total_memory, 2),
            "used_memory_gb": round(self.used_memory, 2),
            "free_memory_gb": round(self.free_memory, 2),
            "memory_percent": round(self.memory_percent, 1),
            "utilization": round(self.utilization, 1),
            "temperature": round(self.temperature, 1),
            "power_draw": round(self.power_draw, 1),
            "power_limit": round(self.power_limit, 1),
            "power_percent": round(self.power_percent, 1),
        }


class GPUMonitor:
    """Monitor GPU status and resources"""

    def __init__(self):
        self._nvml_available = False
        self._init_nvml()

    def _init_nvml(self):
        """Initialize NVML for GPU monitoring"""
        try:
            import pynvml
            pynvml.nvmlInit()
            self._nvml_available = True
            self._nvml = pynvml
            logger.info("NVML initialized successfully")
        except Exception as e:
            logger.warning(f"NVML not available: {e}")
            self._nvml_available = False

    def get_gpu_count(self) -> int:
        """Get number of available GPUs"""
        if not self._nvml_available:
            return 0
        try:
            return self._nvml.nvmlDeviceGetCount()
        except Exception:
            return 0

    def get_gpu_info(self, index: int = 0) -> Optional[GPUInfo]:
        """Get information about a specific GPU"""
        if not self._nvml_available:
            return None

        try:
            handle = self._nvml.nvmlDeviceGetHandleByIndex(index)

            # Name
            name = self._nvml.nvmlDeviceGetName(handle)
            if isinstance(name, bytes):
                name = name.decode('utf-8')

            # Memory
            mem_info = self._nvml.nvmlDeviceGetMemoryInfo(handle)
            total_mem = mem_info.total / (1024 ** 3)  # Convert to GB
            used_mem = mem_info.used / (1024 ** 3)
            free_mem = mem_info.free / (1024 ** 3)

            # Utilization
            util = self._nvml.nvmlDeviceGetUtilizationRates(handle)
            gpu_util = util.gpu

            # Temperature
            try:
                temp = self._nvml.nvmlDeviceGetTemperature(
                    handle, self._nvml.NVML_TEMPERATURE_GPU
                )
            except Exception:
                temp = 0

            # Power
            try:
                power_draw = self._nvml.nvmlDeviceGetPowerUsage(handle) / 1000  # mW to W
                power_limit = self._nvml.nvmlDeviceGetEnforcedPowerLimit(handle) / 1000
            except Exception:
                power_draw = 0
                power_limit = 0

            return GPUInfo(
                index=index,
                name=name,
                total_memory=total_mem,
                used_memory=used_mem,
                free_memory=free_mem,
                utilization=gpu_util,
                temperature=temp,
                power_draw=power_draw,
                power_limit=power_limit,
            )
        except Exception as e:
            logger.error(f"Error getting GPU {index} info: {e}")
            return None

    def get_all_gpus(self) -> List[GPUInfo]:
        """Get information about all GPUs"""
        gpus = []
        count = self.get_gpu_count()
        for i in range(count):
            info = self.get_gpu_info(i)
            if info:
                gpus.append(info)
        return gpus

    def get_total_vram(self) -> float:
        """Get total VRAM across all GPUs in GB"""
        return sum(gpu.total_memory for gpu in self.get_all_gpus())

    def get_free_vram(self) -> float:
        """Get total free VRAM across all GPUs in GB"""
        return sum(gpu.free_memory for gpu in self.get_all_gpus())

    def get_system_info(self) -> Dict[str, Any]:
        """Get system information"""
        import psutil

        return {
            "platform": platform.system(),
            "python_version": platform.python_version(),
            "cpu_count": psutil.cpu_count(),
            "cpu_percent": psutil.cpu_percent(),
            "ram_total_gb": round(psutil.virtual_memory().total / (1024 ** 3), 2),
            "ram_used_gb": round(psutil.virtual_memory().used / (1024 ** 3), 2),
            "ram_percent": psutil.virtual_memory().percent,
            "gpu_count": self.get_gpu_count(),
            "gpus": [gpu.to_dict() for gpu in self.get_all_gpus()],
        }

    def can_load_model(self, required_vram_gb: float) -> bool:
        """Check if there's enough VRAM to load a model"""
        return self.get_free_vram() >= required_vram_gb

    def get_best_gpu(self) -> Optional[int]:
        """Get the GPU with most free VRAM"""
        gpus = self.get_all_gpus()
        if not gpus:
            return None
        return max(gpus, key=lambda g: g.free_memory).index

    def shutdown(self):
        """Cleanup NVML"""
        if self._nvml_available:
            try:
                self._nvml.nvmlShutdown()
            except Exception:
                pass


# Global monitor instance
gpu_monitor = GPUMonitor()
