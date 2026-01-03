"""
API Module
==========
FastAPI server for GPU Worker.
"""

from .server import app, create_app

__all__ = ["app", "create_app"]
