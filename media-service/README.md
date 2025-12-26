# PostXAgent Media Service

**AI Video & Music Generation Microservice**

Version: 2.0.0
Node.js: >=20.0.0
TypeScript: 5.x

---

## 📋 สารบัญ

- [ภาพรวม](#ภาพรวม)
- [คุณสมบัติ](#คุณสมบัติ)
- [สถาปัตยกรรม](#สถาปัตยกรรม)
- [การติดตั้ง](#การติดตั้ง)
- [การตั้งค่า](#การตั้งค่า)
- [การใช้งาน](#การใช้งาน)
- [API Documentation](#api-documentation)
- [Web Learning System](#web-learning-system)
- [Providers](#providers)
- [Development](#development)
- [Testing](#testing)
- [Deployment](#deployment)
- [Troubleshooting](#troubleshooting)

---

## ภาพรวม

Media Service เป็น microservice สำหรับการสร้างวีดีโอและเพลงด้วย AI โดยใช้ระบบ **Web Learning Automation** เพื่อทำงานกับแพลตฟอร์มต่างๆ ผ่าน web interface แทนการใช้ API โดยตรง

### จุดเด่น

✨ **Web Learning** - เรียนรู้ workflow อัตโนมัติและ adapt เมื่อ UI เปลี่ยน
✨ **Multi-Provider Support** - รองรับหลาย AI providers พร้อม fallback
✨ **Queue-Based Processing** - ใช้ BullMQ สำหรับ async job processing
✨ **Type-Safe** - เขียนด้วย TypeScript 100%
✨ **Production-Ready** - พร้อม Docker, monitoring, และ CI/CD

---

## คุณสมบัติ

### Video Generation

- **Text-to-Video** - สร้างวีดีโอจากข้อความ
- **Image-to-Video** - แปลงรูปภาพเป็นวีดีโอ
- **Video-to-Video** - แปลงและปรับปรุงวีดีโอ
- **Multi-Provider** - Freepik (หลัก), Runway, Pika, Luma (fallback)
- **Web Learning** - เรียนรู้ workflow อัตโนมัติ
- **Auto-Retry** - Retry อัตโนมัติเมื่อล้มเหลว
- **Session Management** - จัดการ login sessions

### Music Generation

- **Text-to-Music** - สร้างเพลงจากคำอธิบาย
- **Suno AI Integration** - ใช้ Suno AI สำหรับสร้างเพลง
- **Genre Support** - รองรับหลาย genres
- **Duration Control** - กำหนดความยาวของเพลงได้

### Media Processing

- **FFmpeg Integration** - ประมวลผลวีดีโอและเสียงด้วย FFmpeg
- **Video Concatenation** - ต่อคลิปวีดีโอหลายๆ คลิป
- **Audio Mixing** - ผสมเสียงเข้ากับวีดีโอ
- **Format Conversion** - แปลงไฟล์หลายรูปแบบ
- **Thumbnail Generation** - สร้าง thumbnail อัตโนมัติ

### Queue System

- **BullMQ** - Reliable job queue
- **Redis-Backed** - ใช้ Redis สำหรับ persistence
- **Concurrency Control** - จำกัดจำนวน jobs ที่รันพร้อมกัน
- **Priority Queue** - จัดลำดับความสำคัญของ jobs
- **Progress Tracking** - ติดตามความคืบหน้าแบบ real-time

---

## สถาปัตยกรรม

```
┌─────────────────────────────────────────────────────────────┐
│                    Media Service                            │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  ┌────────────┐  ┌──────────────┐  ┌─────────────────┐    │
│  │  Fastify   │  │  BullMQ      │  │  Playwright     │    │
│  │  REST API  │  │  Job Queue   │  │  Web Automation │    │
│  └─────┬──────┘  └──────┬───────┘  └────────┬────────┘    │
│        │                │                   │              │
│        └────────────────┼───────────────────┘              │
│                         │                                  │
│  ┌──────────────────────┴───────────────────────────────┐  │
│  │              Service Layer                           │  │
│  │  ┌─────────────┐  ┌──────────┐  ┌────────────────┐  │  │
│  │  │   Video     │  │  Music   │  │  Processing    │  │  │
│  │  │  Service    │  │ Service  │  │    Service     │  │  │
│  │  └──────┬──────┘  └────┬─────┘  └────────┬───────┘  │  │
│  │         │              │                 │          │  │
│  │    ┌────┴──────────────┴─────────────────┴────┐     │  │
│  │    │          Provider Layer                  │     │  │
│  │    │  ┌─────────┐  ┌────────┐  ┌──────────┐  │     │  │
│  │    │  │ Freepik │  │ Runway │  │  Suno AI │  │     │  │
│  │    │  │(Primary)│  │(Backup)│  │  (Music) │  │     │  │
│  │    │  └─────────┘  └────────┘  └──────────┘  │     │  │
│  │    └───────────────────────────────────────────┘     │  │
│  └─────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────┘
         │                    │                    │
         ▼                    ▼                    ▼
    ┌─────────┐          ┌─────────┐         ┌──────────┐
    │  Redis  │          │ Storage │         │PostgreSQL│
    │  Queue  │          │ (S3/Min)│         │  (Prisma)│
    └─────────┘          └─────────┘         └──────────┘
```

---

## การติดตั้ง

### Prerequisites

```bash
# ✅ Node.js 20+ และ npm
node --version  # v20.x.x
npm --version   # 10.x.x

# ✅ Redis (สำหรับ queue)
redis-server --version  # 7.x.x

# ✅ PostgreSQL (สำหรับ database)
psql --version  # 16.x

# ✅ FFmpeg (สำหรับ video processing)
ffmpeg -version  # 6.x

# ✅ Playwright browsers
npx playwright install chromium
```

### Installation Steps

```bash
# 1. Clone repository (ถ้ายังไม่ได้ clone)
git clone https://github.com/your-org/PostXAgent.git
cd PostXAgent

# 2. เข้าไปใน media-service directory
cd media-service

# 3. ติดตั้ง dependencies
npm install

# 4. Install Playwright browsers
npx playwright install chromium

# 5. Copy .env.example
cp .env.example .env

# 6. แก้ไข .env file (ใส่ credentials)
nano .env

# 7. Generate Prisma Client
npm run prisma:generate

# 8. Run database migrations
npm run prisma:migrate

# 9. Build TypeScript
npm run build
```

---

## การตั้งค่า

### Environment Variables

แก้ไขไฟล์ `.env`:

```env
# Application
NODE_ENV=development
PORT=3000

# Database
DATABASE_URL="postgresql://mediauser:mediapass@localhost:5432/mediadb"

# Redis
REDIS_HOST=localhost
REDIS_PORT=6379

# Storage (MinIO / S3)
STORAGE_PROVIDER=s3
S3_ENDPOINT=http://localhost:9000
S3_ACCESS_KEY=minioadmin
S3_SECRET_KEY=minioadmin123
S3_BUCKET=postxagent-media

# 🔑 Freepik Credentials (PRIMARY)
FREEPIK_EMAIL=your-email@example.com
FREEPIK_PASSWORD=your-strong-password

# 🔑 Suno AI Credentials
SUNO_EMAIL=your-email@example.com
SUNO_PASSWORD=your-strong-password

# Optional: Fallback Providers
RUNWAY_EMAIL=
RUNWAY_PASSWORD=
```

### Database Setup

```bash
# สร้าง database
createdb mediadb

# Run migrations
npm run prisma:migrate

# (Optional) Seed sample data
npm run prisma:seed
```

### Redis Setup

```bash
# Start Redis (Docker)
docker run -d --name redis \
  -p 6379:6379 \
  redis:7-alpine

# หรือใช้ redis-server local
redis-server --daemonize yes
```

### Storage Setup (MinIO)

```bash
# Start MinIO (Docker)
docker run -d --name minio \
  -p 9000:9000 \
  -p 9001:9001 \
  -e "MINIO_ROOT_USER=minioadmin" \
  -e "MINIO_ROOT_PASSWORD=minioadmin123" \
  minio/minio server /data --console-address ":9001"

# สร้าง bucket
# เข้า http://localhost:9001 และสร้าง bucket "postxagent-media"
```

---

## การใช้งาน

### Development Mode

```bash
# Start development server with auto-reload
npm run dev
```

Server จะรันที่ `http://localhost:3000`

### Production Mode

```bash
# Build
npm run build

# Start with PM2
npm run start:prod

# หรือ cluster mode (ใช้ทุก CPU cores)
npm run start:cluster
```

### Docker

```bash
# Build image
npm run docker:build

# Run with Docker Compose
npm run docker:prod

# View logs
docker-compose logs -f media-service
```

---

## API Documentation

### Health Check

```bash
GET /health
```

**Response:**
```json
{
  "status": "healthy",
  "timestamp": "2025-12-24T12:00:00.000Z",
  "uptime": 3600,
  "version": "2.0.0"
}
```

---

### Generate Video

```bash
POST /api/v1/video/generate
Content-Type: application/json
```

**Request Body:**
```json
{
  "provider": "freepik",
  "mode": "text-to-video",
  "prompt": "A serene beach at sunset with gentle waves",
  "duration": 5,
  "aspectRatio": "16:9",
  "quality": "1080p",
  "style": "cinematic"
}
```

**Response:**
```json
{
  "success": true,
  "jobId": "550e8400-e29b-41d4-a716-446655440000",
  "status": "processing",
  "message": "Video generation job created successfully"
}
```

---

### Get Job Status

```bash
GET /api/v1/jobs/{jobId}
```

**Response:**
```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "status": "completed",
  "progress": 100,
  "result": {
    "success": true,
    "videoUrl": "https://storage.postxagent.com/videos/550e8400.mp4",
    "thumbnailUrl": "https://storage.postxagent.com/thumbnails/550e8400.jpg",
    "metadata": {
      "duration": 5,
      "width": 1920,
      "height": 1080,
      "fileSize": 15728640
    }
  }
}
```

---

### Generate Music

```bash
POST /api/v1/music/generate
Content-Type: application/json
```

**Request Body:**
```json
{
  "prompt": "Upbeat electronic dance music with synth melodies",
  "duration": 30,
  "genre": "electronic",
  "mood": "energetic"
}
```

**Response:**
```json
{
  "success": true,
  "jobId": "660e8400-e29b-41d4-a716-446655440001",
  "status": "processing",
  "message": "Music generation job created successfully"
}
```

---

### Process Video (Mix with Music)

```bash
POST /api/v1/processing/mix
Content-Type: application/json
```

**Request Body:**
```json
{
  "videoPath": "/path/to/video.mp4",
  "musicPath": "/path/to/music.mp3",
  "outputFormat": "mp4",
  "quality": "1080p"
}
```

---

## Web Learning System

Media Service ใช้ระบบ **Web Learning** เพื่อเรียนรู้ workflow อัตโนมัติจากการใช้งานจริง

### วิธีการทำงาน

1. **Learning Mode** - ครั้งแรกที่ใช้งาน provider ใหม่ ระบบจะเข้าสู่ Learning Mode
2. **Element Detection** - AI จะค้นหา elements (buttons, inputs) บนหน้าเว็บ
3. **Workflow Recording** - บันทึก workflow ที่ทำงานสำเร็จ
4. **Workflow Storage** - เก็บ workflow ลงไฟล์ JSON
5. **Auto-Execution** - ครั้งต่อไปจะใช้ workflow ที่เรียนรู้ไว้แล้ว
6. **Self-Healing** - ถ้า element เปลี่ยนตำแหน่ง ระบบจะพยายามหาใหม่อัตโนมัติ

### Workflow File Structure

```json
{
  "version": "1.0",
  "provider": "freepik",
  "createdAt": "2025-12-24T12:00:00.000Z",
  "steps": [
    {
      "action": "fill",
      "selector": "textarea[placeholder*='prompt']",
      "field": "prompt",
      "description": "กรอก video prompt"
    },
    {
      "action": "click",
      "selector": "button:has-text('Generate')",
      "description": "คลิกปุ่ม generate"
    },
    {
      "action": "wait",
      "selector": "video",
      "timeout": 180000,
      "description": "รอวีดีโอสร้างเสร็จ"
    }
  ]
}
```

### Manual Workflow Editing

คุณสามารถแก้ไข workflow manually:

```bash
# Workflow files อยู่ที่
./workflows/freepik-workflow.json
./workflows/suno-workflow.json

# แก้ไข workflow
nano ./workflows/freepik-workflow.json

# Restart service
npm run restart
```

---

## Providers

### Freepik (Pikaso AI) - Primary

**Website:** https://www.freepik.com/pikaso/ai-video-generator

**Features:**
- ✅ Text-to-Video
- ✅ Image-to-Video
- ✅ Fast generation (30-60 seconds)
- ✅ High quality outputs
- ✅ Multiple aspect ratios

**Pricing:**
- Free: 3 videos/day
- Premium: 50 videos/day ($12.99/month)

**Configuration:**
```typescript
{
  provider: 'freepik',
  mode: 'text-to-video',
  prompt: 'Your prompt here',
  duration: 5,
  aspectRatio: '16:9',
  providerSpecific: {
    animationStyle: 'smooth',
    cameraMovement: 'pan',
    motionIntensity: 7
  }
}
```

---

### Suno AI - Music Generation

**Website:** https://app.suno.ai

**Features:**
- ✅ Text-to-Music
- ✅ Multiple genres
- ✅ Vocal & Instrumental
- ✅ High quality audio

**Pricing:**
- Free: 50 credits/month (~10 songs)
- Pro: 2,500 credits/month ($10/month)

---

### Runway ML - Fallback Video Provider

**Features:**
- ✅ Gen-2 Text-to-Video
- ✅ Motion Brush
- ✅ Frame Interpolation

**Pricing:**
- Free: 125 credits/month
- Paid: $12/month (625 credits)

---

## Development

### Project Structure

```
media-service/
├── src/
│   ├── api/                    # API routes & controllers
│   ├── services/               # Business logic
│   │   ├── video/              # Video generation
│   │   │   └── providers/      # Video providers
│   │   ├── music/              # Music generation
│   │   ├── processing/         # FFmpeg processing
│   │   └── automation/         # Browser automation
│   ├── queues/                 # Job queues
│   ├── storage/                # File storage
│   ├── utils/                  # Utilities
│   ├── config/                 # Configuration
│   ├── types/                  # TypeScript types
│   └── middlewares/            # Fastify middlewares
├── tests/                      # Tests
├── docker/                     # Docker configs
├── workflows/                  # Learned workflows
└── docs/                       # Documentation
```

### Code Style

```bash
# Lint code
npm run lint

# Fix lint issues
npm run lint:fix

# Format code
npm run format

# Type check
npm run type-check
```

### Adding a New Provider

1. สร้างไฟล์ใน `src/services/video/providers/YourProvider.ts`
2. Extend `BaseVideoProvider`
3. Implement required methods
4. เพิ่ม provider ใน `VideoProvider` enum
5. Update provider factory

**Example:**

```typescript
import { BaseVideoProvider } from './BaseVideoProvider';

export class YourProvider extends BaseVideoProvider {
  protected readonly providerName = 'your-provider' as VideoProvider;
  protected readonly providerUrl = 'https://your-provider.com';

  async initialize(): Promise<void> {
    // Implementation
  }

  async generate(config: VideoGenerationConfig): Promise<VideoResult> {
    // Implementation
  }

  // ... implement other methods
}
```

---

## Testing

### Run All Tests

```bash
npm test
```

### Unit Tests

```bash
npm run test:unit
```

### Integration Tests

```bash
npm run test:integration
```

### E2E Tests

```bash
npm run test:e2e
```

### Test Coverage

```bash
npm test -- --coverage
```

Coverage report: `coverage/lcov-report/index.html`

---

## Deployment

### Production Checklist

- [ ] Set `NODE_ENV=production`
- [ ] Configure real database (not SQLite)
- [ ] Setup Redis cluster
- [ ] Configure S3/MinIO storage
- [ ] Setup SSL certificates
- [ ] Configure rate limiting
- [ ] Enable monitoring (Prometheus)
- [ ] Setup log aggregation (Loki/ELK)
- [ ] Configure backups
- [ ] Test failover scenarios

### Docker Production

```bash
# Build
docker build -t postxagent/media-service:latest .

# Run
docker-compose -f docker/docker-compose.prod.yml up -d

# Scale
docker-compose -f docker/docker-compose.prod.yml up -d --scale media-service=3
```

### PM2 Production

```bash
# Start cluster
npm run start:cluster

# Monitor
npm run monitor

# Logs
npm run logs

# Restart
npm run restart
```

---

## Troubleshooting

### ไม่สามารถ login ได้

```bash
# ลบ session เก่า
rm -rf ./sessions/*.json

# ลอง login ใหม่
# Service จะพยายาม login อัตโนมัติ
```

### Workflow ไม่ทำงาน

```bash
# ลบ workflow เพื่อให้ re-learn
rm ./workflows/freepik-workflow.json

# Restart service
npm run restart

# Service จะเข้า learning mode อีกครั้ง
```

### Browser ไม่เปิด (Headless Mode)

```bash
# ปิด headless เพื่อ debug
# ใน .env
PLAYWRIGHT_HEADLESS=false

# Restart
npm run dev
```

### Out of Memory

```bash
# เพิ่ม Node.js memory
NODE_OPTIONS="--max-old-space-size=4096" npm start

# หรือใน ecosystem.config.js
node_args: ['--max-old-space-size=4096']
```

### Redis Connection Failed

```bash
# ตรวจสอบ Redis ทำงานหรือไม่
redis-cli ping
# PONG

# ถ้าไม่ทำงาน
redis-server

# หรือใช้ Docker
docker start redis
```

---

## Support & Contributing

### หาความช่วยเหลือ

- 📧 Email: dev@postxagent.com
- 💬 Discord: https://discord.gg/postxagent
- 📚 Documentation: https://docs.postxagent.com

### Contributing

1. Fork the repository
2. สร้าง feature branch (`git checkout -b feature/amazing-feature`)
3. Commit changes (`git commit -m 'Add amazing feature'`)
4. Push to branch (`git push origin feature/amazing-feature`)
5. เปิด Pull Request

---

## License

Proprietary - All Rights Reserved
Copyright © 2025 PostXAgent Development Team

---

## Changelog

### v2.0.0 (2025-12-24)

- ✨ เพิ่ม Freepik/Pikaso AI provider พร้อม Web Learning
- ✨ เพิ่มระบบ Music Generation (Suno AI)
- ✨ เพิ่ม FFmpeg Media Processing Pipeline
- ✨ เพิ่ม BullMQ Queue System
- ✨ รองรับ Aspect Ratios หลายแบบ
- ✨ Auto-retry และ Self-healing
- 🐛 แก้ bug session management
- 📝 เพิ่มเอกสารครบถ้วน

---

**Built with ❤️ in Thailand**

🚀 **PostXAgent Media Service** - The Future of AI Media Generation
