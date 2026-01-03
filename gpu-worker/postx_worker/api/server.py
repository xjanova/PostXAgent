"""
FastAPI Server
==============
Main API server for GPU Worker.
"""
import logging
import asyncio
from typing import Optional, Dict, Any, List
from datetime import datetime
from contextlib import asynccontextmanager

from fastapi import FastAPI, HTTPException, BackgroundTasks, WebSocket, WebSocketDisconnect
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel, Field

from ..config import WorkerConfig, get_config
from ..utils.gpu_monitor import GPUMonitor, get_gpu_monitor
from ..models.image_generator import get_generator, GenerationRequest
from ..models.video_generator import get_video_generator, VideoRequest

logger = logging.getLogger(__name__)


# ============================================================================
# Pydantic Models
# ============================================================================

class WorkerStatus(BaseModel):
    """Worker status response"""
    worker_id: str
    status: str
    gpu_count: int
    gpus: List[Dict[str, Any]]
    total_vram_gb: float
    free_vram_gb: float
    current_task: Optional[str] = None
    uptime_seconds: float
    tasks_completed: int
    tasks_failed: int


class ImageGenerationRequest(BaseModel):
    """Image generation API request"""
    prompt: str
    negative_prompt: str = ""
    width: int = 1024
    height: int = 1024
    steps: int = 30
    guidance_scale: float = 7.5
    seed: int = -1
    batch_size: int = 1
    model_id: str = "stabilityai/stable-diffusion-xl-base-1.0"


class VideoGenerationRequest(BaseModel):
    """Video generation API request"""
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


class TaskResponse(BaseModel):
    """Task submission response"""
    task_id: str
    status: str
    message: str


class TaskResult(BaseModel):
    """Task result response"""
    task_id: str
    status: str  # pending, processing, completed, failed
    result: Optional[Dict[str, Any]] = None
    error: Optional[str] = None
    generation_time: Optional[float] = None


# ============================================================================
# Worker State
# ============================================================================

class WorkerState:
    """Global worker state"""
    def __init__(self):
        self.config = get_config()
        self.start_time = datetime.now()
        self.tasks_completed = 0
        self.tasks_failed = 0
        self.current_task: Optional[str] = None
        self.task_results: Dict[str, TaskResult] = {}
        self.connected_clients: List[WebSocket] = []
        self.task_queue: asyncio.Queue = asyncio.Queue()

    @property
    def uptime_seconds(self) -> float:
        return (datetime.now() - self.start_time).total_seconds()


state = WorkerState()


# ============================================================================
# WebSocket Manager
# ============================================================================

class ConnectionManager:
    """Manage WebSocket connections"""
    def __init__(self):
        self.active_connections: List[WebSocket] = []

    async def connect(self, websocket: WebSocket):
        await websocket.accept()
        self.active_connections.append(websocket)
        logger.info(f"Client connected. Total: {len(self.active_connections)}")

    def disconnect(self, websocket: WebSocket):
        self.active_connections.remove(websocket)
        logger.info(f"Client disconnected. Total: {len(self.active_connections)}")

    async def broadcast(self, message: dict):
        """Send message to all connected clients"""
        for connection in self.active_connections:
            try:
                await connection.send_json(message)
            except Exception as e:
                logger.error(f"Failed to send message: {e}")


manager = ConnectionManager()


# ============================================================================
# Background Task Processor
# ============================================================================

async def process_task_queue():
    """Process tasks from queue"""
    while True:
        try:
            task = await state.task_queue.get()
            task_id = task["task_id"]
            task_type = task["type"]

            state.current_task = task_id
            state.task_results[task_id] = TaskResult(
                task_id=task_id,
                status="processing"
            )

            # Broadcast status
            await manager.broadcast({
                "type": "task_started",
                "task_id": task_id
            })

            try:
                if task_type == "image":
                    result = await asyncio.to_thread(
                        process_image_generation,
                        task["request"]
                    )
                elif task_type == "video":
                    result = await asyncio.to_thread(
                        process_video_generation,
                        task["request"]
                    )
                else:
                    raise ValueError(f"Unknown task type: {task_type}")

                state.task_results[task_id] = TaskResult(
                    task_id=task_id,
                    status="completed",
                    result=result,
                    generation_time=result.get("generation_time")
                )
                state.tasks_completed += 1

                await manager.broadcast({
                    "type": "task_completed",
                    "task_id": task_id,
                    "result": result
                })

            except Exception as e:
                logger.error(f"Task {task_id} failed: {e}")
                state.task_results[task_id] = TaskResult(
                    task_id=task_id,
                    status="failed",
                    error=str(e)
                )
                state.tasks_failed += 1

                await manager.broadcast({
                    "type": "task_failed",
                    "task_id": task_id,
                    "error": str(e)
                })

            finally:
                state.current_task = None
                state.task_queue.task_done()

        except asyncio.CancelledError:
            break
        except Exception as e:
            logger.error(f"Queue processor error: {e}")


def process_image_generation(request: ImageGenerationRequest) -> Dict[str, Any]:
    """Process image generation in thread"""
    generator = get_generator()

    gen_request = GenerationRequest(
        prompt=request.prompt,
        negative_prompt=request.negative_prompt,
        width=request.width,
        height=request.height,
        steps=request.steps,
        guidance_scale=request.guidance_scale,
        seed=request.seed,
        batch_size=request.batch_size,
        model_id=request.model_id,
    )

    result = generator.generate(gen_request)

    if result is None:
        raise Exception("Generation failed")

    return {
        "images": result.to_base64(),
        "seed": result.seed,
        "generation_time": result.generation_time,
        "model_id": result.model_id,
    }


def process_video_generation(request: VideoGenerationRequest) -> Dict[str, Any]:
    """Process video generation in thread"""
    generator = get_video_generator()

    gen_request = VideoRequest(
        prompt=request.prompt,
        negative_prompt=request.negative_prompt,
        width=request.width,
        height=request.height,
        num_frames=request.num_frames,
        fps=request.fps,
        steps=request.steps,
        guidance_scale=request.guidance_scale,
        seed=request.seed,
        model_id=request.model_id,
    )

    result = generator.generate(gen_request)

    if result is None:
        raise Exception("Video generation failed")

    return {
        "frames": result.to_base64_frames(),
        "fps": result.fps,
        "seed": result.seed,
        "generation_time": result.generation_time,
        "model_id": result.model_id,
    }


# ============================================================================
# App Lifecycle
# ============================================================================

@asynccontextmanager
async def lifespan(app: FastAPI):
    """App lifespan manager"""
    # Startup
    logger.info(f"Starting GPU Worker: {state.config.worker_id}")

    # Start task processor
    task_processor = asyncio.create_task(process_task_queue())

    yield

    # Shutdown
    logger.info("Shutting down GPU Worker")
    task_processor.cancel()
    try:
        await task_processor
    except asyncio.CancelledError:
        pass


# ============================================================================
# FastAPI App
# ============================================================================

def create_app() -> FastAPI:
    """Create FastAPI application"""
    app = FastAPI(
        title="PostX GPU Worker",
        description="Distributed GPU Worker for AI Image/Video Generation",
        version="1.0.0",
        lifespan=lifespan,
    )

    # CORS
    app.add_middleware(
        CORSMiddleware,
        allow_origins=["*"],
        allow_credentials=True,
        allow_methods=["*"],
        allow_headers=["*"],
    )

    return app


app = create_app()


# ============================================================================
# API Endpoints
# ============================================================================

@app.get("/")
async def root():
    """Root endpoint"""
    return {
        "service": "PostX GPU Worker",
        "version": "1.0.0",
        "status": "running"
    }


@app.get("/health")
async def health():
    """Health check"""
    return {"status": "healthy"}


@app.get("/status", response_model=WorkerStatus)
async def get_status():
    """Get worker status"""
    monitor = get_gpu_monitor()
    gpus = monitor.get_all_gpus()

    return WorkerStatus(
        worker_id=state.config.worker_id,
        status="busy" if state.current_task else "idle",
        gpu_count=monitor.get_gpu_count(),
        gpus=[{
            "id": gpu.id,
            "name": gpu.name,
            "memory_total_gb": gpu.memory_total / 1024,
            "memory_used_gb": gpu.memory_used / 1024,
            "memory_free_gb": gpu.memory_free / 1024,
            "utilization": gpu.utilization,
            "temperature": gpu.temperature,
            "power_draw": gpu.power_draw,
        } for gpu in gpus],
        total_vram_gb=monitor.get_total_vram() / 1024,
        free_vram_gb=monitor.get_free_vram() / 1024,
        current_task=state.current_task,
        uptime_seconds=state.uptime_seconds,
        tasks_completed=state.tasks_completed,
        tasks_failed=state.tasks_failed,
    )


@app.get("/models/image")
async def list_image_models():
    """List available image models"""
    generator = get_generator()
    return {"models": generator.list_models()}


@app.get("/models/video")
async def list_video_models():
    """List available video models"""
    generator = get_video_generator()
    return {"models": generator.list_models()}


@app.post("/generate/image", response_model=TaskResponse)
async def generate_image(request: ImageGenerationRequest):
    """Submit image generation task"""
    import uuid

    task_id = str(uuid.uuid4())

    # Add to queue
    await state.task_queue.put({
        "task_id": task_id,
        "type": "image",
        "request": request,
    })

    state.task_results[task_id] = TaskResult(
        task_id=task_id,
        status="pending"
    )

    return TaskResponse(
        task_id=task_id,
        status="pending",
        message="Task submitted to queue"
    )


@app.post("/generate/video", response_model=TaskResponse)
async def generate_video(request: VideoGenerationRequest):
    """Submit video generation task"""
    import uuid

    task_id = str(uuid.uuid4())

    await state.task_queue.put({
        "task_id": task_id,
        "type": "video",
        "request": request,
    })

    state.task_results[task_id] = TaskResult(
        task_id=task_id,
        status="pending"
    )

    return TaskResponse(
        task_id=task_id,
        status="pending",
        message="Task submitted to queue"
    )


@app.get("/task/{task_id}", response_model=TaskResult)
async def get_task_result(task_id: str):
    """Get task result"""
    if task_id not in state.task_results:
        raise HTTPException(status_code=404, detail="Task not found")

    return state.task_results[task_id]


@app.delete("/task/{task_id}")
async def cancel_task(task_id: str):
    """Cancel a pending task"""
    if task_id not in state.task_results:
        raise HTTPException(status_code=404, detail="Task not found")

    result = state.task_results[task_id]
    if result.status == "pending":
        result.status = "cancelled"
        return {"message": "Task cancelled"}
    else:
        raise HTTPException(
            status_code=400,
            detail=f"Cannot cancel task in {result.status} status"
        )


@app.post("/model/load")
async def load_model(model_id: str, model_type: str = "image"):
    """Pre-load a model"""
    try:
        if model_type == "image":
            generator = get_generator()
            success = await asyncio.to_thread(generator.load_model, model_id)
        elif model_type == "video":
            generator = get_video_generator()
            success = await asyncio.to_thread(generator.load_model, model_id)
        else:
            raise HTTPException(status_code=400, detail="Invalid model type")

        if success:
            return {"message": f"Model {model_id} loaded successfully"}
        else:
            raise HTTPException(status_code=500, detail="Failed to load model")

    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e))


@app.post("/model/unload")
async def unload_model(model_type: str = "image"):
    """Unload current model to free VRAM"""
    if model_type == "image":
        generator = get_generator()
        generator.unload_model()
    elif model_type == "video":
        generator = get_video_generator()
        generator.unload_model()
    else:
        raise HTTPException(status_code=400, detail="Invalid model type")

    return {"message": "Model unloaded, VRAM freed"}


# ============================================================================
# WebSocket Endpoint
# ============================================================================

@app.websocket("/ws")
async def websocket_endpoint(websocket: WebSocket):
    """WebSocket for real-time updates"""
    await manager.connect(websocket)

    try:
        # Send initial status
        monitor = get_gpu_monitor()
        await websocket.send_json({
            "type": "connected",
            "worker_id": state.config.worker_id,
            "gpu_count": monitor.get_gpu_count(),
        })

        while True:
            # Receive messages from client
            data = await websocket.receive_json()

            if data.get("type") == "ping":
                await websocket.send_json({"type": "pong"})

            elif data.get("type") == "get_status":
                gpus = monitor.get_all_gpus()
                await websocket.send_json({
                    "type": "status",
                    "current_task": state.current_task,
                    "queue_size": state.task_queue.qsize(),
                    "gpus": [{
                        "id": gpu.id,
                        "utilization": gpu.utilization,
                        "memory_used": gpu.memory_used,
                        "memory_free": gpu.memory_free,
                    } for gpu in gpus]
                })

    except WebSocketDisconnect:
        manager.disconnect(websocket)


# ============================================================================
# Run Server
# ============================================================================

def run_server(host: str = "0.0.0.0", port: int = 8080):
    """Run the FastAPI server"""
    import uvicorn
    uvicorn.run(app, host=host, port=port)


if __name__ == "__main__":
    run_server()
