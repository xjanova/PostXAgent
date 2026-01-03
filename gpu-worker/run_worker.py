#!/usr/bin/env python3
"""
PostX GPU Worker - Startup Script
==================================
Run this script to start the GPU worker.

Usage:
    python run_worker.py [--port PORT] [--master URL] [--name NAME]

Examples:
    # Standalone mode (no master)
    python run_worker.py --port 8080

    # Connect to master server
    python run_worker.py --port 8080 --master http://localhost:5000

    # With custom name
    python run_worker.py --port 8080 --name "My-GPU-Worker"
"""
import argparse
import logging
import os
import sys

# Add parent directory to path
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))


def main():
    parser = argparse.ArgumentParser(
        description="PostX GPU Worker for AI Image/Video Generation"
    )
    parser.add_argument(
        "--port",
        type=int,
        default=8080,
        help="API server port (default: 8080)"
    )
    parser.add_argument(
        "--master",
        type=str,
        default=None,
        help="Master server URL (e.g., http://localhost:5000)"
    )
    parser.add_argument(
        "--name",
        type=str,
        default=None,
        help="Worker name (auto-generated if not specified)"
    )
    parser.add_argument(
        "--id",
        type=str,
        default=None,
        help="Worker ID (auto-generated if not specified)"
    )
    parser.add_argument(
        "--log-level",
        type=str,
        default="INFO",
        choices=["DEBUG", "INFO", "WARNING", "ERROR"],
        help="Logging level (default: INFO)"
    )
    parser.add_argument(
        "--standalone",
        action="store_true",
        help="Run in standalone mode (API server only)"
    )

    args = parser.parse_args()

    # Setup logging
    logging.basicConfig(
        level=getattr(logging, args.log_level),
        format="%(asctime)s - %(name)s - %(levelname)s - %(message)s",
    )

    logger = logging.getLogger(__name__)

    # Check for GPU
    try:
        import torch
        if torch.cuda.is_available():
            gpu_count = torch.cuda.device_count()
            logger.info(f"Found {gpu_count} GPU(s)")
            for i in range(gpu_count):
                name = torch.cuda.get_device_name(i)
                memory = torch.cuda.get_device_properties(i).total_memory / 1024**3
                logger.info(f"  GPU {i}: {name} ({memory:.1f} GB)")
        else:
            logger.warning("No GPU found! Running in CPU mode (slow)")
    except ImportError:
        logger.error("PyTorch not installed! Please run: pip install torch")
        sys.exit(1)

    # Set environment variables for config
    if args.port:
        os.environ["POSTX_WORKER_API_PORT"] = str(args.port)
    if args.master:
        os.environ["POSTX_WORKER_MASTER_URL"] = args.master
    if args.name:
        os.environ["POSTX_WORKER_NAME"] = args.name
    if args.id:
        os.environ["POSTX_WORKER_ID"] = args.id

    if args.standalone:
        # Run API server only
        logger.info(f"Starting standalone API server on port {args.port}")
        from postx_worker.api.server import run_server
        run_server(host="0.0.0.0", port=args.port)
    else:
        # Run full worker
        from postx_worker.worker import main as worker_main
        worker_main()


if __name__ == "__main__":
    main()
