# AI Manager Dockerfile
# Multi-process Python application for social media automation

FROM python:3.11-slim

LABEL maintainer="PostXAgent Team"
LABEL description="AI Manager Core - Multi-process orchestrator for social media automation"

# Set environment variables
ENV PYTHONDONTWRITEBYTECODE=1
ENV PYTHONUNBUFFERED=1
ENV PYTHONPATH=/app

# Install system dependencies
RUN apt-get update && apt-get install -y --no-install-recommends \
    build-essential \
    curl \
    git \
    && rm -rf /var/lib/apt/lists/*

# Create app directory
WORKDIR /app

# Copy requirements first for caching
COPY requirements.txt .

# Install Python dependencies
RUN pip install --no-cache-dir --upgrade pip && \
    pip install --no-cache-dir -r requirements.txt

# Copy application code
COPY . .

# Create non-root user
RUN useradd -m -u 1000 aimanager && \
    chown -R aimanager:aimanager /app

USER aimanager

# Expose metrics port
EXPOSE 9090

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
    CMD python -c "import redis; r = redis.Redis(host='redis'); r.ping()" || exit 1

# Run the orchestrator
CMD ["python", "-m", "core.orchestrator"]
