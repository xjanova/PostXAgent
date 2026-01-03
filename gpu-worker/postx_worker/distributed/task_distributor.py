"""
Task Distributor
================
Intelligent task distribution across GPU workers.
"""
import logging
import asyncio
from typing import Optional, List, Dict, Any, Callable
from dataclasses import dataclass
from datetime import datetime
from enum import Enum

from .pool_manager import GPUPoolManager, WorkerNode, DistributionMode

logger = logging.getLogger(__name__)


class TaskPriority(Enum):
    """Task priority levels"""
    LOW = 1
    NORMAL = 2
    HIGH = 3
    URGENT = 4


class TaskStatus(Enum):
    """Task status"""
    PENDING = "pending"
    QUEUED = "queued"
    DISTRIBUTED = "distributed"
    PROCESSING = "processing"
    COMPLETED = "completed"
    FAILED = "failed"
    CANCELLED = "cancelled"


@dataclass
class DistributedTask:
    """Represents a task to be distributed"""
    id: str
    type: str  # "image" or "video"
    request: Dict[str, Any]
    priority: TaskPriority = TaskPriority.NORMAL
    mode: DistributionMode = DistributionMode.PARALLEL
    status: TaskStatus = TaskStatus.PENDING
    assigned_workers: List[str] = None
    subtasks: List[str] = None
    result: Optional[Dict[str, Any]] = None
    error: Optional[str] = None
    created_at: datetime = None
    started_at: Optional[datetime] = None
    completed_at: Optional[datetime] = None
    callback: Optional[Callable] = None

    def __post_init__(self):
        if self.assigned_workers is None:
            self.assigned_workers = []
        if self.subtasks is None:
            self.subtasks = []
        if self.created_at is None:
            self.created_at = datetime.now()


class TaskDistributor:
    """
    Smart task distributor with multiple strategies.

    Distribution Strategies:
    1. Round Robin - Simple rotation
    2. Least Loaded - Send to worker with least tasks
    3. Weighted - Based on GPU compute power
    4. VRAM Based - Based on available VRAM
    5. Smart - AI-based decision (considers task requirements)
    """

    def __init__(self, pool_manager: GPUPoolManager):
        self.pool = pool_manager
        self.tasks: Dict[str, DistributedTask] = {}
        self.priority_queue: asyncio.PriorityQueue = asyncio.PriorityQueue()
        self._running = False
        self._last_worker_idx = 0

    async def start(self):
        """Start the distributor"""
        self._running = True
        asyncio.create_task(self._distribution_loop())
        logger.info("Task Distributor started")

    async def stop(self):
        """Stop the distributor"""
        self._running = False
        logger.info("Task Distributor stopped")

    async def submit(
        self,
        task_id: str,
        task_type: str,
        request: Dict[str, Any],
        priority: TaskPriority = TaskPriority.NORMAL,
        mode: Optional[DistributionMode] = None,
        callback: Optional[Callable] = None,
    ) -> DistributedTask:
        """Submit a task for distribution"""
        task = DistributedTask(
            id=task_id,
            type=task_type,
            request=request,
            priority=priority,
            mode=mode or self.pool.distribution_mode,
            callback=callback,
        )

        self.tasks[task_id] = task

        # Add to priority queue (negative priority for max-heap behavior)
        await self.priority_queue.put((-priority.value, task_id))

        logger.info(f"Task {task_id} submitted with priority {priority.name}")
        return task

    async def _distribution_loop(self):
        """Main distribution loop"""
        while self._running:
            try:
                # Get next task from priority queue
                _, task_id = await asyncio.wait_for(
                    self.priority_queue.get(),
                    timeout=1.0
                )

                if task_id not in self.tasks:
                    continue

                task = self.tasks[task_id]
                if task.status == TaskStatus.CANCELLED:
                    continue

                await self._distribute_task(task)

            except asyncio.TimeoutError:
                continue
            except Exception as e:
                logger.error(f"Distribution loop error: {e}")

    async def _distribute_task(self, task: DistributedTask):
        """Distribute a single task"""
        task.status = TaskStatus.QUEUED

        if task.mode == DistributionMode.COMBINED:
            await self._distribute_combined(task)
        else:
            await self._distribute_parallel(task)

    async def _distribute_parallel(self, task: DistributedTask):
        """Distribute to single best worker"""
        worker = self._select_best_worker(task)

        if not worker:
            # No workers available, requeue
            await self.priority_queue.put((-task.priority.value, task.id))
            await asyncio.sleep(1)
            return

        task.assigned_workers = [worker.id]
        task.status = TaskStatus.DISTRIBUTED
        task.started_at = datetime.now()

        # Submit to pool
        await self.pool.submit_task(
            task_id=task.id,
            task_type=task.type,
            request=task.request,
            mode=DistributionMode.PARALLEL,
        )

    async def _distribute_combined(self, task: DistributedTask):
        """Distribute across multiple workers for combined processing"""
        available = self.pool.get_available_workers()

        if not available:
            await self.priority_queue.put((-task.priority.value, task.id))
            await asyncio.sleep(1)
            return

        # Determine how to split the task
        batch_size = task.request.get("batch_size", 1)

        if batch_size > 1:
            # Split batch across workers
            subtasks = self._split_batch_task(task, available)
            task.subtasks = [st["task_id"] for st in subtasks]

            for subtask in subtasks:
                await self.pool.submit_task(
                    task_id=subtask["task_id"],
                    task_type=task.type,
                    request=subtask["request"],
                    mode=DistributionMode.PARALLEL,
                )
        else:
            # Single task - use best worker
            worker = self._select_best_worker(task)
            if worker:
                task.assigned_workers = [worker.id]
                await self.pool.submit_task(
                    task_id=task.id,
                    task_type=task.type,
                    request=task.request,
                    mode=DistributionMode.PARALLEL,
                )

        task.status = TaskStatus.DISTRIBUTED
        task.started_at = datetime.now()

    def _select_best_worker(self, task: DistributedTask) -> Optional[WorkerNode]:
        """Select the best worker for a task"""
        available = self.pool.get_available_workers()

        if not available:
            return None

        # Estimate VRAM requirement
        required_vram = self._estimate_vram_requirement(task)

        # Filter by VRAM
        suitable = [w for w in available if w.free_vram_gb >= required_vram]

        if not suitable:
            # Use any available worker (might swap to CPU)
            suitable = available

        # Sort by compute power
        suitable.sort(key=lambda w: (w.compute_power, w.free_vram_gb), reverse=True)

        return suitable[0]

    def _estimate_vram_requirement(self, task: DistributedTask) -> float:
        """Estimate VRAM required for a task"""
        model_id = task.request.get("model_id", "")

        # Known VRAM requirements (approximate)
        vram_requirements = {
            "stabilityai/stable-diffusion-xl": 8.0,
            "stabilityai/sdxl-turbo": 8.0,
            "runwayml/stable-diffusion-v1-5": 4.0,
            "black-forest-labs/FLUX.1-schnell": 12.0,
            "black-forest-labs/FLUX.1-dev": 24.0,
            "ali-vilab/text-to-video": 8.0,
        }

        # Find matching requirement
        for key, vram in vram_requirements.items():
            if key in model_id:
                return vram

        # Default based on type
        if task.type == "video":
            return 8.0
        return 6.0

    def _split_batch_task(
        self,
        task: DistributedTask,
        workers: List[WorkerNode]
    ) -> List[Dict[str, Any]]:
        """Split a batch task across workers"""
        batch_size = task.request.get("batch_size", 1)
        total_power = sum(w.compute_power for w in workers)

        subtasks = []
        remaining = batch_size

        for i, worker in enumerate(workers):
            if remaining <= 0:
                break

            # Allocate proportionally
            if i == len(workers) - 1:
                worker_batch = remaining
            else:
                worker_batch = max(1, int(batch_size * worker.compute_power / total_power))
                remaining -= worker_batch

            if worker_batch > 0:
                request = task.request.copy()
                request["batch_size"] = worker_batch

                subtasks.append({
                    "task_id": f"{task.id}_part{i}",
                    "worker_id": worker.id,
                    "request": request,
                })

        return subtasks

    def get_task(self, task_id: str) -> Optional[DistributedTask]:
        """Get task by ID"""
        return self.tasks.get(task_id)

    def cancel_task(self, task_id: str) -> bool:
        """Cancel a pending task"""
        if task_id in self.tasks:
            task = self.tasks[task_id]
            if task.status in [TaskStatus.PENDING, TaskStatus.QUEUED]:
                task.status = TaskStatus.CANCELLED
                return True
        return False

    def update_task_result(
        self,
        task_id: str,
        result: Optional[Dict[str, Any]] = None,
        error: Optional[str] = None,
    ):
        """Update task with result"""
        if task_id not in self.tasks:
            # Check if it's a subtask
            for task in self.tasks.values():
                if task_id in task.subtasks:
                    self._update_subtask_result(task, task_id, result, error)
                    return
            return

        task = self.tasks[task_id]
        task.completed_at = datetime.now()

        if error:
            task.status = TaskStatus.FAILED
            task.error = error
        else:
            task.status = TaskStatus.COMPLETED
            task.result = result

        # Call callback if set
        if task.callback:
            try:
                task.callback(task)
            except Exception as e:
                logger.error(f"Task callback failed: {e}")

    def _update_subtask_result(
        self,
        parent_task: DistributedTask,
        subtask_id: str,
        result: Optional[Dict[str, Any]],
        error: Optional[str],
    ):
        """Update subtask result and check if parent is complete"""
        # For now, just track completion
        # In a full implementation, we'd aggregate results

        # Check if all subtasks are complete
        # This is simplified - real implementation would track each subtask
        pass

    def get_stats(self) -> Dict[str, Any]:
        """Get distributor statistics"""
        status_counts = {}
        for task in self.tasks.values():
            status = task.status.value
            status_counts[status] = status_counts.get(status, 0) + 1

        return {
            "total_tasks": len(self.tasks),
            "queue_size": self.priority_queue.qsize(),
            "status_counts": status_counts,
            "distribution_mode": self.pool.distribution_mode.value,
        }
