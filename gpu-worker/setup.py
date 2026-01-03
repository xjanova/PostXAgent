"""
PostX GPU Worker - Setup Script
"""
from setuptools import setup, find_packages

with open("README.md", "r", encoding="utf-8") as f:
    long_description = f.read()

setup(
    name="postx-gpu-worker",
    version="1.0.0",
    author="PostXAgent Team",
    description="Distributed GPU Worker for AI Image/Video Generation",
    long_description=long_description,
    long_description_content_type="text/markdown",
    url="https://github.com/postxagent/gpu-worker",
    packages=find_packages(),
    classifiers=[
        "Development Status :: 4 - Beta",
        "Intended Audience :: Developers",
        "License :: OSI Approved :: MIT License",
        "Programming Language :: Python :: 3",
        "Programming Language :: Python :: 3.10",
        "Programming Language :: Python :: 3.11",
        "Topic :: Scientific/Engineering :: Artificial Intelligence",
    ],
    python_requires=">=3.10",
    install_requires=[
        "torch>=2.0.0",
        "diffusers>=0.25.0",
        "transformers>=4.36.0",
        "accelerate>=0.25.0",
        "safetensors>=0.4.0",
        "fastapi>=0.108.0",
        "uvicorn[standard]>=0.25.0",
        "websockets>=12.0",
        "huggingface-hub>=0.20.0",
        "pillow>=10.0.0",
        "pynvml>=11.5.0",
        "psutil>=5.9.0",
        "pydantic>=2.5.0",
        "httpx>=0.26.0",
    ],
    extras_require={
        "dev": [
            "pytest>=7.0.0",
            "pytest-asyncio>=0.23.0",
            "black>=23.0.0",
            "mypy>=1.0.0",
        ],
        "video": [
            "imageio>=2.33.0",
            "imageio-ffmpeg>=0.4.9",
            "opencv-python>=4.8.0",
        ],
    },
    entry_points={
        "console_scripts": [
            "postx-worker=postx_worker.worker:main",
        ],
    },
)
