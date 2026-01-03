"""
GPU Worker Main Module
======================
Main entry point for the GPU Worker.
Handles connection to master server and task processing.
"""
import logging
import asyncio
import signal
import sys
from typing import Optional
from datetime import datetime

import httpx
import websockets
from websockets.client import WebSocketClientProtocol

from .config import WorkerConfig, get_config
from .utils.gpu_monitor import get_gpu_monitor
from .api.server import run_server

logger = logging.getLogger(__name__)


class GPUWorker:
    """
    Main GPU Worker class.

    Responsibilities:
    - Connect to PostXAgent master server
    - Report GPU status and availability
    - Receive and process tasks
    - Send results back to master
    """

    def __init__(self, config: Optional[WorkerConfig] = None):
        self.config = config or get_config()
        self.gpu_monitor = get_gpu_monitor()
        self.ws: Optional[WebSocketClientProtocol] = None
        self._running = False
        self._reconnect_delay = 5
        self._max_reconnect_delay = 60

    async def start(self):
        """Start the worker"""
        self._running = True
        logger.info(f"Starting GPU Worker: {self.config.worker_id}")
        logger.info(f"GPU Count: {self.gpu_monitor.get_gpu_count()}")
        logger.info(f"Total VRAM: {self.gpu_monitor.get_total_vram():.1f} MB")

        # Start tasks
        tasks = [
            asyncio.create_task(self._connection_loop()),
            asyncio.create_task(self._status_reporter()),
        ]

        # Also start API server if configured
        if self.config.api_port:
            logger.info(f"Starting API server on port {self.config.api_port}")
            # Run API server in background
            import uvicorn
            from .api.server import app

            config = uvicorn.Config(
                app,
                host="0.0.0.0",
                port=self.config.api_port,
                log_level="info",
            )
            server = uvicorn.Server(config)
            tasks.append(asyncio.create_task(server.serve()))

        try:
            await asyncio.gather(*tasks)
        except asyncio.CancelledError:
            pass
        finally:
            await self.stop()

    async def stop(self):
        """Stop the worker"""
        self._running = False

        if self.ws:
            await self.ws.close()

        logger.info("GPU Worker stopped")

    async def _connection_loop(self):
        """Maintain connection to master server"""
        delay = self._reconnect_delay

        while self._running:
            if not self.config.master_url:
                # No master configured, just run standalone
                await asyncio.sleep(60)
                continue

            try:
                ws_url = self.config.master_url.replace("http", "ws") + "/ws/worker"
                logger.info(f"Connecting to master: {ws_url}")

                async with websockets.connect(ws_url) as ws:
                    self.ws = ws
                    delay = self._reconnect_delay  # Reset delay on success

                    # Send registration
                    await self._register()

                    # Message loop
                    async for message in ws:
                        await self._handle_message(message)

            except websockets.exceptions.ConnectionClosed:
                logger.warning("Connection to master closed")
            except Exception as e:
                logger.error(f"Connection error: {e}")

            if self._running:
                logger.info(f"Reconnecting in {delay} seconds...")
                await asyncio.sleep(delay)
                delay = min(delay * 2, self._max_reconnect_delay)

    async def _register(self):
        """Register with master server"""
        if not self.ws:
            return

        gpus = self.gpu_monitor.get_all_gpus()

        registration = {
            "type": "register",
            "worker_id": self.config.worker_id,
            "worker_name": self.config.worker_name,
            "api_port": self.config.api_port,
            "gpu_count": self.gpu_monitor.get_gpu_count(),
            "total_vram_mb": self.gpu_monitor.get_total_vram(),
            "gpus": [{
                "id": gpu.id,
                "name": gpu.name,
                "memory_total": gpu.memory_total,
                "compute_capability": getattr(gpu, 'compute_capability', None),
            } for gpu in gpus],
            "supported_models": list(self._get_supported_models()),
            "timestamp": datetime.now().isoformat(),
        }

        import json
        await self.ws.send(json.dumps(registration))
        logger.info("Registered with master server")

    def _get_supported_models(self) -> set:
        """Get set of supported models"""
        from .models.image_generator import ImageGenerator
        from .models.video_generator import VideoGenerator

        models = set()
        models.update(ImageGenerator.SUPPORTED_MODELS.keys())
        models.update(VideoGenerator.SUPPORTED_MODELS.keys())
        return models

    async def _handle_message(self, message: str):
        """Handle message from master"""
        import json

        try:
            data = json.loads(message)
            msg_type = data.get("type")

            if msg_type == "ping":
                await self.ws.send(json.dumps({"type": "pong"}))

            elif msg_type == "task":
                await self._process_task(data)

            elif msg_type == "cancel":
                await self._cancel_task(data.get("task_id"))

            elif msg_type == "load_model":
                await self._load_model(data)

            elif msg_type == "unload_model":
                await self._unload_model(data)

            else:
                logger.warning(f"Unknown message type: {msg_type}")

        except json.JSONDecodeError:
            logger.error(f"Invalid JSON message: {message}")
        except Exception as e:
            logger.error(f"Error handling message: {e}")

    async def _process_task(self, data: dict):
        """Process a task from master"""
        task_id = data.get("task_id")
        task_type = data.get("task_type", "image")
        request = data.get("request", {})

        logger.info(f"Processing task: {task_id} ({task_type})")

        try:
            # Send status update
            await self._send_status_update(task_id, "processing")

            if task_type == "image":
                from .models.image_generator import get_generator, GenerationRequest

                generator = get_generator()
                gen_request = GenerationRequest(**request)
                result = await asyncio.to_thread(generator.generate, gen_request)

                if result:
                    await self._send_result(task_id, {
                        "images": result.to_base64(),
                        "seed": result.seed,
                        "generation_time": result.generation_time,
                        "model_id": result.model_id,
                    })
                else:
                    await self._send_error(task_id, "Generation failed")

            elif task_type == "video":
                from .models.video_generator import get_video_generator, VideoRequest

                generator = get_video_generator()
                gen_request = VideoRequest(**request)
                result = await asyncio.to_thread(generator.generate, gen_request)

                if result:
                    await self._send_result(task_id, {
                        "frames": result.to_base64_frames(),
                        "fps": result.fps,
                        "seed": result.seed,
                        "generation_time": result.generation_time,
                        "model_id": result.model_id,
                    })
                else:
                    await self._send_error(task_id, "Video generation failed")

            else:
                await self._send_error(task_id, f"Unknown task type: {task_type}")

        except Exception as e:
            logger.error(f"Task {task_id} failed: {e}")
            await self._send_error(task_id, str(e))

    async def _cancel_task(self, task_id: str):
        """Cancel a running task"""
        # In a real implementation, we'd need to track and cancel running tasks
        logger.info(f"Cancel requested for task: {task_id}")

    async def _load_model(self, data: dict):
        """Pre-load a model"""
        model_id = data.get("model_id")
        model_type = data.get("model_type", "image")

        try:
            if model_type == "image":
                from .models.image_generator import get_generator
                generator = get_generator()
                await asyncio.to_thread(generator.load_model, model_id)
            elif model_type == "video":
                from .models.video_generator import get_video_generator
                generator = get_video_generator()
                await asyncio.to_thread(generator.load_model, model_id)

            logger.info(f"Model {model_id} loaded")

        except Exception as e:
            logger.error(f"Failed to load model {model_id}: {e}")

    async def _unload_model(self, data: dict):
        """Unload a model to free VRAM"""
        model_type = data.get("model_type", "image")

        if model_type == "image":
            from .models.image_generator import get_generator
            get_generator().unload_model()
        elif model_type == "video":
            from .models.video_generator import get_video_generator
            get_video_generator().unload_model()

        logger.info(f"Model unloaded ({model_type})")

    async def _status_reporter(self):
        """Periodically report status to master"""
        while self._running:
            if self.ws and self.ws.open:
                try:
                    gpus = self.gpu_monitor.get_all_gpus()

                    import json
                    status = {
                        "type": "status",
                        "worker_id": self.config.worker_id,
                        "gpu_count": len(gpus),
                        "total_vram_mb": self.gpu_monitor.get_total_vram(),
                        "free_vram_mb": self.gpu_monitor.get_free_vram(),
                        "gpus": [{
                            "id": gpu.id,
                            "utilization": gpu.utilization,
                            "memory_used": gpu.memory_used,
                            "memory_free": gpu.memory_free,
                            "temperature": gpu.temperature,
                            "power_draw": gpu.power_draw,
                        } for gpu in gpus],
                        "timestamp": datetime.now().isoformat(),
                    }

                    await self.ws.send(json.dumps(status))

                except Exception as e:
                    logger.error(f"Failed to send status: {e}")

            await asyncio.sleep(self.config.heartbeat_interval)

    async def _send_status_update(self, task_id: str, status: str):
        """Send task status update"""
        if self.ws and self.ws.open:
            import json
            await self.ws.send(json.dumps({
                "type": "task_status",
                "task_id": task_id,
                "status": status,
                "timestamp": datetime.now().isoformat(),
            }))

    async def _send_result(self, task_id: str, result: dict):
        """Send task result"""
        if self.ws and self.ws.open:
            import json
            await self.ws.send(json.dumps({
                "type": "task_result",
                "task_id": task_id,
                "status": "completed",
                "result": result,
                "timestamp": datetime.now().isoformat(),
            }))

    async def _send_error(self, task_id: str, error: str):
        """Send task error"""
        if self.ws and self.ws.open:
            import json
            await self.ws.send(json.dumps({
                "type": "task_result",
                "task_id": task_id,
                "status": "failed",
                "error": error,
                "timestamp": datetime.now().isoformat(),
            }))


def main():
    """Main entry point"""
    # Setup logging
    logging.basicConfig(
        level=logging.INFO,
        format="%(asctime)s - %(name)s - %(levelname)s - %(message)s",
    )

    # Create worker
    worker = GPUWorker()

    # Handle signals
    loop = asyncio.new_event_loop()
    asyncio.set_event_loop(loop)

    def signal_handler():
        loop.create_task(worker.stop())

    for sig in (signal.SIGINT, signal.SIGTERM):
        try:
            loop.add_signal_handler(sig, signal_handler)
        except NotImplementedError:
            # Windows doesn't support add_signal_handler
            pass

    try:
        loop.run_until_complete(worker.start())
    except KeyboardInterrupt:
        loop.run_until_complete(worker.stop())
    finally:
        loop.close()


if __name__ == "__main__":
    main()
