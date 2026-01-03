"""
GPU Pool Manager
================
Manages a pool of GPU workers for distributed computing.
Similar to cryptocurrency mining pool concept.
"""
import logging
import asyncio
import httpx
from typing import Optional, List, Dict, Any
from dataclasses import dataclass, field
from datetime import datetime
from enum import Enum

logger = logging.getLogger(__name__)


class DistributionMode(Enum):
    """Task distribution modes"""
    PARALLEL = "parallel"       # 1 GPU = 1 Task (like mining different coins)
    COMBINED = "combined"       # All GPUs work on 1 task (like merged mining)
    AUTO = "auto"               # Automatically choose based on task


class WorkerStatus(Enum):
    """Worker status"""
    ONLINE = "online"
    OFFLINE = "offline"
    BUSY = "busy"
    ERROR = "error"


@dataclass
class WorkerNode:
    """Represents a GPU worker in the pool"""
    id: str
    name: str
    host: str
    port: int
    gpu_count: int = 0
    total_vram_gb: float = 0
    free_vram_gb: float = 0
    status: WorkerStatus = WorkerStatus.OFFLINE
    current_task: Optional[str] = None
    tasks_completed: int = 0
    tasks_failed: int = 0
    last_heartbeat: datetime = field(default_factory=datetime.now)
    compute_power: float = 1.0  # Relative compute power (for weighted distribution)

    @property
    def url(self) -> str:
        return f"http://{self.host}:{self.port}"

    @property
    def is_available(self) -> bool:
        return self.status == WorkerStatus.ONLINE and self.current_task is None


@dataclass
class PoolStats:
    """Pool statistics"""
    total_workers: int
    online_workers: int
    busy_workers: int
    total_gpus: int
    total_vram_gb: float
    free_vram_gb: float
    total_compute_power: float
    tasks_in_queue: int
    tasks_completed: int
    tasks_failed: int


class GPUPoolManager:
    """
    Manages a pool of distributed GPU workers.

    Features:
    - Worker registration and health monitoring
    - Task distribution across workers
    - Two modes: Parallel (1 GPU = 1 task) and Combined (all GPUs = 1 task)
    - Weighted task distribution based on GPU power
    - Automatic failover and retry
    """

    def __init__(self, master_url: Optional[str] = None):
        self.master_url = master_url
        self.workers: Dict[str, WorkerNode] = {}
        self.task_queue: asyncio.Queue = asyncio.Queue()
        self.distribution_mode = DistributionMode.PARALLEL
        self.tasks_completed = 0
        self.tasks_failed = 0
        self._heartbeat_interval = 30  # seconds
        self._running = False

    async def start(self):
        """Start the pool manager"""
        self._running = True
        asyncio.create_task(self._heartbeat_loop())
        asyncio.create_task(self._task_processor())
        logger.info("GPU Pool Manager started")

    async def stop(self):
        """Stop the pool manager"""
        self._running = False
        logger.info("GPU Pool Manager stopped")

    def register_worker(self, worker: WorkerNode) -> bool:
        """Register a new worker in the pool"""
        if worker.id in self.workers:
            logger.warning(f"Worker {worker.id} already registered, updating...")

        self.workers[worker.id] = worker
        logger.info(f"Worker registered: {worker.name} ({worker.id}) - {worker.gpu_count} GPUs")
        return True

    def unregister_worker(self, worker_id: str) -> bool:
        """Remove a worker from the pool"""
        if worker_id in self.workers:
            del self.workers[worker_id]
            logger.info(f"Worker unregistered: {worker_id}")
            return True
        return False

    async def update_worker_status(self, worker_id: str) -> bool:
        """Fetch and update worker status"""
        if worker_id not in self.workers:
            return False

        worker = self.workers[worker_id]

        try:
            async with httpx.AsyncClient(timeout=10) as client:
                response = await client.get(f"{worker.url}/status")
                response.raise_for_status()
                data = response.json()

                worker.gpu_count = data.get("gpu_count", 0)
                worker.total_vram_gb = data.get("total_vram_gb", 0)
                worker.free_vram_gb = data.get("free_vram_gb", 0)
                worker.current_task = data.get("current_task")
                worker.tasks_completed = data.get("tasks_completed", 0)
                worker.tasks_failed = data.get("tasks_failed", 0)
                worker.status = WorkerStatus.BUSY if worker.current_task else WorkerStatus.ONLINE
                worker.last_heartbeat = datetime.now()

                return True

        except Exception as e:
            logger.error(f"Failed to update worker {worker_id}: {e}")
            worker.status = WorkerStatus.OFFLINE
            return False

    async def _heartbeat_loop(self):
        """Periodically check worker health"""
        while self._running:
            for worker_id in list(self.workers.keys()):
                await self.update_worker_status(worker_id)

            await asyncio.sleep(self._heartbeat_interval)

    async def _task_processor(self):
        """Process tasks from queue"""
        while self._running:
            try:
                task = await asyncio.wait_for(
                    self.task_queue.get(),
                    timeout=1.0
                )
                await self._distribute_task(task)
            except asyncio.TimeoutError:
                continue
            except Exception as e:
                logger.error(f"Task processor error: {e}")

    async def _distribute_task(self, task: Dict[str, Any]):
        """Distribute a task based on current mode"""
        task_id = task.get("task_id")
        mode = task.get("mode", self.distribution_mode)

        if mode == DistributionMode.PARALLEL:
            await self._distribute_parallel(task)
        elif mode == DistributionMode.COMBINED:
            await self._distribute_combined(task)
        else:
            # Auto mode - decide based on task
            if task.get("requires_large_vram", False):
                await self._distribute_combined(task)
            else:
                await self._distribute_parallel(task)

    async def _distribute_parallel(self, task: Dict[str, Any]):
        """Distribute task to single worker (1 GPU = 1 task)"""
        # Find available worker with best specs
        available = [
            w for w in self.workers.values()
            if w.is_available
        ]

        if not available:
            # Requeue task
            await self.task_queue.put(task)
            await asyncio.sleep(1)
            return

        # Sort by compute power (highest first)
        available.sort(key=lambda w: w.compute_power, reverse=True)
        worker = available[0]

        try:
            await self._send_task_to_worker(worker, task)
        except Exception as e:
            logger.error(f"Failed to send task to {worker.id}: {e}")
            # Retry with another worker
            await self.task_queue.put(task)

    async def _distribute_combined(self, task: Dict[str, Any]):
        """
        Distribute task across all available workers.
        For tasks that can be parallelized (like batch image generation).
        """
        available = [
            w for w in self.workers.values()
            if w.is_available
        ]

        if not available:
            await self.task_queue.put(task)
            await asyncio.sleep(1)
            return

        # Calculate work distribution based on compute power
        total_power = sum(w.compute_power for w in available)

        # For batch tasks, split the batch
        batch_size = task.get("batch_size", 1)
        if batch_size > 1:
            # Distribute batch across workers
            distributed_tasks = []
            remaining = batch_size

            for i, worker in enumerate(available):
                # Allocate proportionally
                if i == len(available) - 1:
                    worker_batch = remaining
                else:
                    worker_batch = max(1, int(batch_size * worker.compute_power / total_power))
                    remaining -= worker_batch

                if worker_batch > 0:
                    worker_task = task.copy()
                    worker_task["batch_size"] = worker_batch
                    worker_task["subtask_id"] = f"{task['task_id']}_part{i}"
                    distributed_tasks.append((worker, worker_task))

            # Send all subtasks
            await asyncio.gather(*[
                self._send_task_to_worker(worker, subtask)
                for worker, subtask in distributed_tasks
            ])

        else:
            # Single item - send to best worker
            available.sort(key=lambda w: w.compute_power, reverse=True)
            await self._send_task_to_worker(available[0], task)

    async def _send_task_to_worker(self, worker: WorkerNode, task: Dict[str, Any]):
        """Send a task to a specific worker"""
        task_type = task.get("type", "image")
        endpoint = f"/generate/{task_type}"

        try:
            async with httpx.AsyncClient(timeout=300) as client:
                response = await client.post(
                    f"{worker.url}{endpoint}",
                    json=task.get("request", {})
                )
                response.raise_for_status()

                worker.current_task = task.get("task_id")
                logger.info(f"Task {task.get('task_id')} sent to worker {worker.id}")

                return response.json()

        except Exception as e:
            logger.error(f"Failed to send task to worker {worker.id}: {e}")
            raise

    async def submit_task(
        self,
        task_id: str,
        task_type: str,
        request: Dict[str, Any],
        mode: Optional[DistributionMode] = None,
    ) -> bool:
        """Submit a task to the pool"""
        task = {
            "task_id": task_id,
            "type": task_type,
            "request": request,
            "mode": mode or self.distribution_mode,
            "batch_size": request.get("batch_size", 1),
            "submitted_at": datetime.now().isoformat(),
        }

        await self.task_queue.put(task)
        logger.info(f"Task {task_id} submitted to pool queue")
        return True

    def get_stats(self) -> PoolStats:
        """Get pool statistics"""
        online = [w for w in self.workers.values() if w.status == WorkerStatus.ONLINE]
        busy = [w for w in self.workers.values() if w.status == WorkerStatus.BUSY]

        return PoolStats(
            total_workers=len(self.workers),
            online_workers=len(online),
            busy_workers=len(busy),
            total_gpus=sum(w.gpu_count for w in self.workers.values()),
            total_vram_gb=sum(w.total_vram_gb for w in self.workers.values()),
            free_vram_gb=sum(w.free_vram_gb for w in online),
            total_compute_power=sum(w.compute_power for w in online),
            tasks_in_queue=self.task_queue.qsize(),
            tasks_completed=self.tasks_completed,
            tasks_failed=self.tasks_failed,
        )

    def get_available_workers(self) -> List[WorkerNode]:
        """Get list of available workers"""
        return [w for w in self.workers.values() if w.is_available]

    def set_distribution_mode(self, mode: DistributionMode):
        """Set the default distribution mode"""
        self.distribution_mode = mode
        logger.info(f"Distribution mode set to: {mode.value}")


# Singleton instance
_pool_manager: Optional[GPUPoolManager] = None


def get_pool_manager() -> GPUPoolManager:
    """Get or create the global pool manager"""
    global _pool_manager
    if _pool_manager is None:
        _pool_manager = GPUPoolManager()
    return _pool_manager
