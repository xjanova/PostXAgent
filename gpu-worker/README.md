# PostX GPU Worker

GPU Worker สำหรับสร้างภาพและวีดีโอด้วย AI - รองรับระบบกระจายงานแบบ Mining Pool

## สถาปัตยกรรม

```
┌─────────────────────────────────────────────────────────────┐
│                    PostXAgent (Master)                       │
│                         ↕ WebSocket                          │
└─────────────────────────────────────────────────────────────┘
                              │
        ┌─────────────────────┼─────────────────────┐
        │                     │                     │
        ▼                     ▼                     ▼
┌─────────────┐       ┌─────────────┐       ┌─────────────┐
│ GPU Worker 1│       │ GPU Worker 2│       │ GPU Worker 3│
│  (Local)    │       │  (Colab)    │       │  (Runpod)   │
│  RTX 4090   │       │  T4/A100    │       │  A100/H100  │
└─────────────┘       └─────────────┘       └─────────────┘
```

## โหมดการทำงาน

### 1. Parallel Mode (1 GPU = 1 งาน)
เหมือนขุดคริปโตแบบแยกกัน - แต่ละ Worker รับงานไปทำเอง

### 2. Combined Mode (รวมพลัง)
เหมือน Mining Pool - แบ่งงานตามกำลัง GPU แล้วรวมผลลัพธ์

## การติดตั้ง

### Local Installation

```bash
# Clone repository
cd gpu-worker

# Create virtual environment
python -m venv venv
source venv/bin/activate  # Linux/Mac
venv\Scripts\activate     # Windows

# Install dependencies
pip install -r requirements.txt

# Run worker
python run_worker.py --port 8080
```

### Google Colab

1. เปิด notebook: `notebooks/PostX_GPU_Worker.ipynb`
2. เปลี่ยน Runtime เป็น GPU
3. รันทุก Cell ตามลำดับ
4. คัดลอก ngrok URL ไปใส่ในโปรแกรม

### Docker (Coming Soon)

```bash
docker run -d --gpus all -p 8080:8080 postxagent/gpu-worker
```

## API Endpoints

### Status
```bash
GET /status

Response:
{
  "worker_id": "worker-abc123",
  "status": "online",
  "gpu_count": 1,
  "gpus": [...],
  "total_vram_gb": 24.0,
  "free_vram_gb": 20.0
}
```

### Generate Image
```bash
POST /generate/image

Request:
{
  "prompt": "A beautiful sunset",
  "negative_prompt": "blur, low quality",
  "width": 1024,
  "height": 1024,
  "steps": 30,
  "guidance_scale": 7.5,
  "seed": -1,
  "batch_size": 1,
  "model_id": "stabilityai/stable-diffusion-xl-base-1.0"
}

Response:
{
  "task_id": "uuid",
  "status": "completed",
  "result": {
    "images": ["base64..."],
    "seed": 12345,
    "generation_time": 15.5
  }
}
```

### Generate Video
```bash
POST /generate/video

Request:
{
  "prompt": "A cat playing piano",
  "width": 512,
  "height": 512,
  "num_frames": 16,
  "fps": 8,
  "steps": 25,
  "model_id": "ali-vilab/text-to-video-ms-1.7b"
}
```

### Model Management
```bash
# Load model (pre-warm)
POST /model/load?model_id=stabilityai/stable-diffusion-xl-base-1.0

# Unload model (free VRAM)
POST /model/unload
```

## Supported Models

### Image Generation
| Model | Type | VRAM |
|-------|------|------|
| stabilityai/stable-diffusion-xl-base-1.0 | SDXL | 8 GB |
| stabilityai/sdxl-turbo | SDXL Turbo | 8 GB |
| runwayml/stable-diffusion-v1-5 | SD 1.5 | 4 GB |
| black-forest-labs/FLUX.1-schnell | FLUX | 12 GB |
| black-forest-labs/FLUX.1-dev | FLUX | 24 GB |

### Video Generation
| Model | Type | VRAM |
|-------|------|------|
| ali-vilab/text-to-video-ms-1.7b | Text-to-Video | 8 GB |
| stabilityai/stable-video-diffusion-img2vid | SVD | 16 GB |

## Configuration

สร้างไฟล์ `.env`:

```env
POSTX_WORKER_ID=my-worker-001
POSTX_WORKER_NAME=My GPU Worker
POSTX_WORKER_API_PORT=8080
POSTX_WORKER_MASTER_URL=http://your-postxagent:5000
```

หรือใช้ command line:

```bash
python run_worker.py \
  --port 8080 \
  --name "My-GPU-Worker" \
  --master "http://localhost:5000"
```

## Development

```bash
# Install dev dependencies
pip install -r requirements-dev.txt

# Run tests
pytest

# Format code
black .

# Type check
mypy .
```

## License

MIT License - PostXAgent Team
