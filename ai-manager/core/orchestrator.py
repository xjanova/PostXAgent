"""
AI Manager Core - Process Orchestrator
Manages 40+ CPU cores for parallel task processing
"""
import os
import sys
import signal
import asyncio
import logging
import multiprocessing as mp
from multiprocessing import Process, Queue, Manager, cpu_count
from concurrent.futures import ProcessPoolExecutor, ThreadPoolExecutor
from typing import Dict, List, Optional, Callable, Any
from dataclasses import dataclass
from datetime import datetime
import threading
import time
import json
import redis.asyncio as redis
from enum import Enum

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from config.settings import settings, SocialPlatform

logger = logging.getLogger(__name__)


class TaskStatus(Enum):
    """Task execution status"""
    PENDING = "pending"
    QUEUED = "queued"
    RUNNING = "running"
    COMPLETED = "completed"
    FAILED = "failed"
    CANCELLED = "cancelled"


class TaskType(Enum):
    """Types of tasks the system can handle"""
    GENERATE_CONTENT = "generate_content"
    GENERATE_IMAGE = "generate_image"
    POST_CONTENT = "post_content"
    SCHEDULE_POST = "schedule_post"
    ANALYZE_METRICS = "analyze_metrics"
    MONITOR_ENGAGEMENT = "monitor_engagement"


@dataclass
class Task:
    """Task definition"""
    id: str
    type: TaskType
    platform: SocialPlatform
    user_id: int
    brand_id: int
    payload: Dict[str, Any]
    priority: int = 0
    created_at: datetime = None
    status: TaskStatus = TaskStatus.PENDING
    retries: int = 0
    result: Optional[Dict] = None
    error: Optional[str] = None

    def __post_init__(self):
        if self.created_at is None:
            self.created_at = datetime.utcnow()

    def to_dict(self) -> Dict:
        return {
            "id": self.id,
            "type": self.type.value,
            "platform": self.platform.value,
            "user_id": self.user_id,
            "brand_id": self.brand_id,
            "payload": self.payload,
            "priority": self.priority,
            "created_at": self.created_at.isoformat(),
            "status": self.status.value,
            "retries": self.retries,
            "result": self.result,
            "error": self.error,
        }

    @classmethod
    def from_dict(cls, data: Dict) -> "Task":
        return cls(
            id=data["id"],
            type=TaskType(data["type"]),
            platform=SocialPlatform(data["platform"]),
            user_id=data["user_id"],
            brand_id=data["brand_id"],
            payload=data["payload"],
            priority=data.get("priority", 0),
            created_at=datetime.fromisoformat(data["created_at"]) if data.get("created_at") else None,
            status=TaskStatus(data.get("status", "pending")),
            retries=data.get("retries", 0),
            result=data.get("result"),
            error=data.get("error"),
        )


class WorkerProcess:
    """Individual worker process for handling tasks"""

    def __init__(self, worker_id: int, platform: SocialPlatform, task_queue: Queue, result_queue: Queue):
        self.worker_id = worker_id
        self.platform = platform
        self.task_queue = task_queue
        self.result_queue = result_queue
        self.running = True
        self.current_task: Optional[Task] = None

    def run(self):
        """Main worker loop"""
        logger.info(f"Worker {self.worker_id} started for platform {self.platform.value}")

        while self.running:
            try:
                # Get task from queue with timeout
                task_data = self.task_queue.get(timeout=1)

                if task_data is None:  # Shutdown signal
                    break

                task = Task.from_dict(task_data)
                self.current_task = task

                logger.info(f"Worker {self.worker_id} processing task {task.id}")

                # Process the task
                result = self.process_task(task)

                # Send result back
                self.result_queue.put({
                    "worker_id": self.worker_id,
                    "task_id": task.id,
                    "status": "completed" if result.get("success") else "failed",
                    "result": result,
                })

                self.current_task = None

            except mp.queues.Empty:
                continue
            except Exception as e:
                logger.error(f"Worker {self.worker_id} error: {e}")
                if self.current_task:
                    self.result_queue.put({
                        "worker_id": self.worker_id,
                        "task_id": self.current_task.id,
                        "status": "failed",
                        "error": str(e),
                    })
                    self.current_task = None

        logger.info(f"Worker {self.worker_id} stopped")

    def process_task(self, task: Task) -> Dict:
        """Process a single task based on type"""
        from workers import get_worker_for_platform

        worker = get_worker_for_platform(task.platform)

        if task.type == TaskType.GENERATE_CONTENT:
            return worker.generate_content(task)
        elif task.type == TaskType.GENERATE_IMAGE:
            return worker.generate_image(task)
        elif task.type == TaskType.POST_CONTENT:
            return worker.post_content(task)
        elif task.type == TaskType.SCHEDULE_POST:
            return worker.schedule_post(task)
        elif task.type == TaskType.ANALYZE_METRICS:
            return worker.analyze_metrics(task)
        elif task.type == TaskType.MONITOR_ENGAGEMENT:
            return worker.monitor_engagement(task)
        else:
            raise ValueError(f"Unknown task type: {task.type}")

    def stop(self):
        """Stop the worker"""
        self.running = False


class ProcessOrchestrator:
    """
    Main orchestrator that manages all worker processes
    Utilizes all available CPU cores (40+) for maximum parallelism
    """

    def __init__(self):
        self.num_cores = settings.worker.num_cores or cpu_count()
        logger.info(f"Initializing ProcessOrchestrator with {self.num_cores} CPU cores")

        # Process management
        self.manager = Manager()
        self.workers: Dict[str, Process] = {}
        self.worker_queues: Dict[SocialPlatform, Queue] = {}
        self.result_queue = Queue()

        # Task tracking
        self.active_tasks: Dict[str, Task] = self.manager.dict()
        self.completed_tasks: Dict[str, Task] = self.manager.dict()

        # Redis connection for external queue
        self.redis: Optional[redis.Redis] = None

        # Control flags
        self.running = False
        self.shutdown_event = threading.Event()

        # Statistics
        self.stats = self.manager.dict({
            "tasks_processed": 0,
            "tasks_failed": 0,
            "tasks_queued": 0,
            "start_time": None,
        })

    async def initialize(self):
        """Initialize the orchestrator"""
        logger.info("Initializing orchestrator...")

        # Connect to Redis
        self.redis = redis.Redis.from_url(settings.redis.url)
        await self.redis.ping()
        logger.info("Redis connection established")

        # Initialize queues for each platform
        for platform in SocialPlatform:
            self.worker_queues[platform] = Queue()

        # Calculate workers per platform
        workers_per_platform = max(1, self.num_cores // len(SocialPlatform))
        logger.info(f"Allocating {workers_per_platform} workers per platform")

        # Create worker processes
        worker_id = 0
        for platform in SocialPlatform:
            for i in range(workers_per_platform):
                self._create_worker(worker_id, platform)
                worker_id += 1

        # If we have remaining cores, allocate to high-traffic platforms
        remaining_cores = self.num_cores - worker_id
        high_traffic_platforms = [
            SocialPlatform.FACEBOOK,
            SocialPlatform.INSTAGRAM,
            SocialPlatform.TIKTOK,
            SocialPlatform.LINE,
        ]

        for platform in high_traffic_platforms:
            if remaining_cores <= 0:
                break
            self._create_worker(worker_id, platform)
            worker_id += 1
            remaining_cores -= 1

        logger.info(f"Created {len(self.workers)} worker processes")
        self.stats["start_time"] = datetime.utcnow().isoformat()

    def _create_worker(self, worker_id: int, platform: SocialPlatform):
        """Create a new worker process"""
        worker = WorkerProcess(
            worker_id=worker_id,
            platform=platform,
            task_queue=self.worker_queues[platform],
            result_queue=self.result_queue,
        )

        process = Process(target=worker.run, daemon=True)
        self.workers[f"{platform.value}_{worker_id}"] = process

    async def start(self):
        """Start all worker processes and main loop"""
        logger.info("Starting orchestrator...")

        # Start all worker processes
        for name, process in self.workers.items():
            process.start()
            logger.debug(f"Started worker process: {name}")

        self.running = True

        # Start background tasks
        await asyncio.gather(
            self._redis_queue_listener(),
            self._result_processor(),
            self._health_monitor(),
            self._stats_reporter(),
        )

    async def stop(self):
        """Gracefully stop all processes"""
        logger.info("Stopping orchestrator...")
        self.running = False
        self.shutdown_event.set()

        # Send shutdown signal to all workers
        for platform in SocialPlatform:
            queue = self.worker_queues.get(platform)
            if queue:
                for _ in range(settings.worker.max_workers_per_platform):
                    queue.put(None)

        # Wait for processes to finish
        for name, process in self.workers.items():
            process.join(timeout=5)
            if process.is_alive():
                process.terminate()
                logger.warning(f"Force terminated worker: {name}")

        # Close Redis connection
        if self.redis:
            await self.redis.close()

        logger.info("Orchestrator stopped")

    async def submit_task(self, task: Task) -> str:
        """Submit a task for processing"""
        task_data = task.to_dict()

        # Add to Redis queue for persistence
        await self.redis.lpush(
            f"tasks:{task.platform.value}:pending",
            json.dumps(task_data)
        )

        # Also add to local queue for immediate processing
        self.worker_queues[task.platform].put(task_data)

        self.active_tasks[task.id] = task_data
        self.stats["tasks_queued"] = self.stats.get("tasks_queued", 0) + 1

        logger.info(f"Task {task.id} submitted for platform {task.platform.value}")
        return task.id

    async def get_task_status(self, task_id: str) -> Optional[Dict]:
        """Get the status of a task"""
        if task_id in self.active_tasks:
            return self.active_tasks[task_id]
        if task_id in self.completed_tasks:
            return self.completed_tasks[task_id]
        return None

    async def cancel_task(self, task_id: str) -> bool:
        """Cancel a pending task"""
        if task_id in self.active_tasks:
            task_data = self.active_tasks[task_id]
            task_data["status"] = TaskStatus.CANCELLED.value
            del self.active_tasks[task_id]
            self.completed_tasks[task_id] = task_data
            return True
        return False

    async def _redis_queue_listener(self):
        """Listen for tasks from Redis queue (from Laravel backend)"""
        logger.info("Starting Redis queue listener...")

        while self.running:
            try:
                for platform in SocialPlatform:
                    # Check for new tasks from Laravel
                    task_data = await self.redis.rpop(f"laravel:tasks:{platform.value}")

                    if task_data:
                        task = Task.from_dict(json.loads(task_data))
                        self.worker_queues[platform].put(task.to_dict())
                        self.active_tasks[task.id] = task.to_dict()
                        logger.debug(f"Received task {task.id} from Laravel")

                await asyncio.sleep(settings.worker.queue_poll_interval / 1000)

            except Exception as e:
                logger.error(f"Redis queue listener error: {e}")
                await asyncio.sleep(1)

    async def _result_processor(self):
        """Process results from worker processes"""
        logger.info("Starting result processor...")

        while self.running:
            try:
                while not self.result_queue.empty():
                    result = self.result_queue.get_nowait()

                    task_id = result.get("task_id")
                    status = result.get("status")

                    if task_id in self.active_tasks:
                        task_data = self.active_tasks[task_id]
                        task_data["status"] = status
                        task_data["result"] = result.get("result")
                        task_data["error"] = result.get("error")

                        # Move to completed
                        del self.active_tasks[task_id]
                        self.completed_tasks[task_id] = task_data

                        # Update stats
                        if status == "completed":
                            self.stats["tasks_processed"] = self.stats.get("tasks_processed", 0) + 1
                        else:
                            self.stats["tasks_failed"] = self.stats.get("tasks_failed", 0) + 1

                        # Notify Laravel via Redis
                        await self.redis.lpush(
                            "laravel:results",
                            json.dumps(task_data)
                        )

                        logger.info(f"Task {task_id} completed with status: {status}")

                await asyncio.sleep(0.1)

            except Exception as e:
                logger.error(f"Result processor error: {e}")
                await asyncio.sleep(1)

    async def _health_monitor(self):
        """Monitor health of worker processes"""
        logger.info("Starting health monitor...")

        while self.running:
            try:
                dead_workers = []

                for name, process in self.workers.items():
                    if not process.is_alive():
                        logger.warning(f"Worker {name} is dead, restarting...")
                        dead_workers.append(name)

                # Restart dead workers
                for name in dead_workers:
                    parts = name.split("_")
                    platform = SocialPlatform(parts[0])
                    worker_id = int(parts[1])

                    # Create new worker
                    self._create_worker(worker_id, platform)
                    self.workers[name].start()
                    logger.info(f"Restarted worker: {name}")

                await asyncio.sleep(settings.worker.health_check_interval)

            except Exception as e:
                logger.error(f"Health monitor error: {e}")
                await asyncio.sleep(5)

    async def _stats_reporter(self):
        """Report statistics periodically"""
        logger.info("Starting stats reporter...")

        while self.running:
            try:
                stats = {
                    "active_workers": sum(1 for p in self.workers.values() if p.is_alive()),
                    "total_workers": len(self.workers),
                    "tasks_queued": self.stats.get("tasks_queued", 0),
                    "tasks_processed": self.stats.get("tasks_processed", 0),
                    "tasks_failed": self.stats.get("tasks_failed", 0),
                    "active_tasks": len(self.active_tasks),
                    "uptime": self._get_uptime(),
                }

                await self.redis.set("orchestrator:stats", json.dumps(stats))
                logger.debug(f"Stats: {stats}")

                await asyncio.sleep(60)  # Report every minute

            except Exception as e:
                logger.error(f"Stats reporter error: {e}")
                await asyncio.sleep(60)

    def _get_uptime(self) -> str:
        """Calculate uptime"""
        start_time = self.stats.get("start_time")
        if not start_time:
            return "0s"

        start = datetime.fromisoformat(start_time)
        delta = datetime.utcnow() - start

        hours, remainder = divmod(int(delta.total_seconds()), 3600)
        minutes, seconds = divmod(remainder, 60)

        return f"{hours}h {minutes}m {seconds}s"

    def get_stats(self) -> Dict:
        """Get current statistics"""
        return dict(self.stats)


# Singleton instance
_orchestrator: Optional[ProcessOrchestrator] = None


def get_orchestrator() -> ProcessOrchestrator:
    """Get or create orchestrator instance"""
    global _orchestrator
    if _orchestrator is None:
        _orchestrator = ProcessOrchestrator()
    return _orchestrator


async def main():
    """Main entry point"""
    logging.basicConfig(
        level=logging.INFO,
        format="%(asctime)s - %(name)s - %(levelname)s - %(message)s"
    )

    orchestrator = get_orchestrator()

    # Handle shutdown signals
    def signal_handler(signum, frame):
        logger.info(f"Received signal {signum}")
        asyncio.create_task(orchestrator.stop())

    signal.signal(signal.SIGINT, signal_handler)
    signal.signal(signal.SIGTERM, signal_handler)

    try:
        await orchestrator.initialize()
        await orchestrator.start()
    except Exception as e:
        logger.error(f"Orchestrator error: {e}")
        await orchestrator.stop()
        raise


if __name__ == "__main__":
    asyncio.run(main())
