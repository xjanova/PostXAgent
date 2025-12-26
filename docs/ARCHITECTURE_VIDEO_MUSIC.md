# สถาปัตยกรรมระบบ AI Video & Music Generation

**Version**: 2.0.0
**Last Updated**: 24 December 2025
**Author**: PostXAgent Development Team

---

## 📋 สารบัญ

1. [ภาพรวมระบบ](#ภาพรวมระบบ)
2. [สถาปัตยกรรมแบบ Microservices](#สถาปัตยกรรมแบบ-microservices)
3. [Media Processing Service](#media-processing-service)
4. [Video Generation Pipeline](#video-generation-pipeline)
5. [Music Generation Pipeline](#music-generation-pipeline)
6. [Queue System](#queue-system)
7. [Web Automation Strategy](#web-automation-strategy)
8. [Data Flow](#data-flow)
9. [Deployment Architecture](#deployment-architecture)

---

## ภาพรวมระบบ

ระบบ PostXAgent เวอร์ชัน 2.0 เพิ่มความสามารถในการสร้างวีดีโอและเพลงด้วย AI โดยใช้สถาปัตยกรรมแบบ **Microservices** เพื่อแยก concerns และ scale ได้อิสระ

### Tech Stack ที่เพิ่มเข้ามา

| Component | Technology | Version | Purpose |
|-----------|------------|---------|---------|
| **Runtime** | Node.js | 20.x LTS | ประมวลผล JavaScript/TypeScript |
| **Language** | TypeScript | 5.x | Type-safe development |
| **Framework** | Fastify | 4.x | High-performance API server |
| **Queue** | BullMQ | 5.x | Job queue management |
| **Cache/Queue Store** | Redis | 7.x | In-memory data store |
| **Database** | PostgreSQL | 16.x | Relational database |
| **ORM** | Prisma | 5.x | Type-safe database client |
| **Automation** | Playwright | 1.40.x | Browser automation |
| **Media Processing** | FFmpeg | 6.x | Video/Audio processing |
| **Process Manager** | PM2 | 5.x | Production process management |

---

## สถาปัตยกรรมแบบ Microservices

```
┌────────────────────────────────────────────────────────────────────────────┐
│                            Client Applications                              │
│                     (Web Dashboard, Mobile App, CLI)                        │
└────────────────────────────┬───────────────────────────────────────────────┘
                             │
                             ▼
┌────────────────────────────────────────────────────────────────────────────┐
│                         API Gateway / Load Balancer                         │
│                              (Nginx / Traefik)                              │
└────────────┬───────────────────────────┬──────────────────┬────────────────┘
             │                           │                  │
             ▼                           ▼                  ▼
┌─────────────────────┐   ┌──────────────────────┐   ┌──────────────────────┐
│  Laravel Backend    │   │ Media Service        │   │ AI Manager Core      │
│  (Port 8000)        │   │ (Port 3000)          │   │ (Port 5000-5002)     │
│                     │   │                      │   │                      │
│ • User Management   │   │ • Video Generation   │   │ • Social Platform    │
│ • Campaigns         │   │ • Music Generation   │   │   Workers            │
│ • Brands            │   │ • Media Processing   │   │ • Web Automation     │
│ • Posts             │   │ • Queue Management   │   │ • Workflow Learning  │
│ • Analytics         │   │                      │   │                      │
└──────────┬──────────┘   └───────────┬──────────┘   └──────────┬───────────┘
           │                          │                         │
           │                          │                         │
           └────────────┬─────────────┴─────────────┬───────────┘
                        │                           │
                        ▼                           ▼
           ┌────────────────────────┐   ┌─────────────────────┐
           │     Redis Cluster      │   │   PostgreSQL        │
           │                        │   │   + MySQL           │
           │ • Cache                │   │                     │
           │ • Job Queues           │   │ • Persistent Data   │
           │ • Session Store        │   │ • Relational Data   │
           │ • Real-time Events     │   │                     │
           └────────────────────────┘   └─────────────────────┘
                        │
                        ▼
           ┌────────────────────────┐
           │   Object Storage       │
           │   (S3 / MinIO)         │
           │                        │
           │ • Generated Videos     │
           │ • Generated Music      │
           │ • Assets & Media       │
           └────────────────────────┘
```

---

## Media Processing Service

### โครงสร้างไดเรกทอรี

```
media-service/
├── src/
│   ├── api/                    # API Routes & Controllers
│   │   ├── routes/
│   │   │   ├── video.routes.ts
│   │   │   ├── music.routes.ts
│   │   │   ├── processing.routes.ts
│   │   │   └── health.routes.ts
│   │   └── controllers/
│   │       ├── VideoController.ts
│   │       ├── MusicController.ts
│   │       └── ProcessingController.ts
│   │
│   ├── services/               # Business Logic Services
│   │   ├── video/
│   │   │   ├── VideoGenerationService.ts
│   │   │   ├── providers/
│   │   │   │   ├── BaseVideoProvider.ts
│   │   │   │   ├── RunwayProvider.ts
│   │   │   │   ├── PikaLabsProvider.ts
│   │   │   │   └── LumaAIProvider.ts
│   │   │   └── VideoDownloader.ts
│   │   │
│   │   ├── music/
│   │   │   ├── MusicGenerationService.ts
│   │   │   ├── SunoAIProvider.ts
│   │   │   └── MusicDownloader.ts
│   │   │
│   │   ├── processing/
│   │   │   ├── FFmpegService.ts
│   │   │   ├── VideoProcessor.ts
│   │   │   ├── AudioProcessor.ts
│   │   │   ├── ConcatenationService.ts
│   │   │   └── MixingService.ts
│   │   │
│   │   └── automation/
│   │       ├── BrowserAutomation.ts
│   │       ├── SessionManager.ts
│   │       ├── LoginService.ts
│   │       └── CaptchaSolver.ts
│   │
│   ├── queues/                 # Job Queue Management
│   │   ├── QueueManager.ts
│   │   ├── workers/
│   │   │   ├── VideoGenerationWorker.ts
│   │   │   ├── MusicGenerationWorker.ts
│   │   │   ├── ProcessingWorker.ts
│   │   │   └── DownloadWorker.ts
│   │   └── jobs/
│   │       ├── VideoGenerationJob.ts
│   │       ├── MusicGenerationJob.ts
│   │       └── ProcessingJob.ts
│   │
│   ├── storage/                # Storage Management
│   │   ├── StorageService.ts
│   │   ├── S3Storage.ts
│   │   ├── LocalStorage.ts
│   │   └── FileManager.ts
│   │
│   ├── utils/                  # Utilities
│   │   ├── logger.ts
│   │   ├── validator.ts
│   │   ├── errors.ts
│   │   └── helpers.ts
│   │
│   ├── config/                 # Configuration
│   │   ├── app.config.ts
│   │   ├── queue.config.ts
│   │   ├── redis.config.ts
│   │   └── providers.config.ts
│   │
│   ├── types/                  # TypeScript Types
│   │   ├── video.types.ts
│   │   ├── music.types.ts
│   │   ├── processing.types.ts
│   │   └── common.types.ts
│   │
│   ├── middlewares/            # Express/Fastify Middlewares
│   │   ├── auth.middleware.ts
│   │   ├── validation.middleware.ts
│   │   ├── ratelimit.middleware.ts
│   │   └── error.middleware.ts
│   │
│   ├── prisma/                 # Prisma ORM
│   │   ├── schema.prisma
│   │   └── migrations/
│   │
│   └── app.ts                  # Application Entry Point
│
├── tests/                      # Tests
│   ├── unit/
│   ├── integration/
│   └── e2e/
│
├── docker/                     # Docker Configurations
│   ├── Dockerfile
│   ├── Dockerfile.dev
│   └── docker-compose.yml
│
├── scripts/                    # Utility Scripts
│   ├── setup.sh
│   ├── migrate.sh
│   └── seed.sh
│
├── docs/                       # Documentation
│   ├── API.md
│   ├── PROVIDERS.md
│   └── DEPLOYMENT.md
│
├── .env.example
├── .eslintrc.js
├── .prettierrc
├── tsconfig.json
├── package.json
└── README.md
```

---

## Video Generation Pipeline

### Workflow แบบ High-Level

```
┌─────────────────┐
│  Client Request │
│                 │
│ • concept       │
│ • duration      │
│ • style         │
│ • aspect_ratio  │
└────────┬────────┘
         │
         ▼
┌─────────────────────────────┐
│  API Endpoint               │
│  POST /api/v1/video/generate│
└────────┬────────────────────┘
         │
         ▼
┌─────────────────────────────┐
│  Video Generation Service   │
│                             │
│ 1. Validate input           │
│ 2. Select provider          │
│ 3. Create job               │
│ 4. Queue job                │
└────────┬────────────────────┘
         │
         ▼
┌─────────────────────────────┐
│  BullMQ Queue               │
│  "video-generation"         │
└────────┬────────────────────┘
         │
         ▼
┌─────────────────────────────┐
│  Video Generation Worker    │
│                             │
│ 1. Initialize browser       │
│ 2. Login to provider        │
│ 3. Submit generation req    │
│ 4. Monitor progress         │
│ 5. Download result          │
└────────┬────────────────────┘
         │
         ▼
┌─────────────────────────────┐
│  Storage Service            │
│                             │
│ • Upload to S3/MinIO        │
│ • Generate thumbnails       │
│ • Update database           │
└────────┬────────────────────┘
         │
         ▼
┌─────────────────────────────┐
│  Webhook Notification       │
│  (Optional)                 │
└─────────────────────────────┘
```

### รายละเอียด Video Providers

#### 1. Runway ML

**Features:**
- Text-to-Video
- Image-to-Video
- Video-to-Video
- Motion Brush
- Frame Interpolation

**Pricing:**
- Free Tier: 125 credits/month (~5 videos)
- Paid: $12/month (625 credits)

**Automation Strategy:**
```typescript
/**
 * Runway Provider Implementation
 * ใช้ Playwright เพื่อ automate การสร้างวีดีโอผ่าน Runway ML web interface
 */
class RunwayProvider extends BaseVideoProvider {
  async generate(config: VideoGenerationConfig): Promise<VideoResult> {
    // 1. เปิด browser และ login
    await this.browserAutomation.launch();
    await this.loginService.login('runway', credentials);

    // 2. Navigate ไปหน้าสร้างวีดีโอ
    await this.page.goto('https://app.runwayml.com/video-tools/teams/.../gen-2');

    // 3. เลือก mode (text-to-video, image-to-video)
    await this.selectMode(config.mode);

    // 4. กรอก prompt และ settings
    await this.fillGenerationForm(config);

    // 5. เริ่มสร้างวีดีโอ
    await this.clickGenerate();

    // 6. รอจนกว่าจะสร้างเสร็จ (polling)
    const result = await this.waitForCompletion();

    // 7. Download วีดีโอ
    const videoPath = await this.downloadVideo(result.url);

    return {
      success: true,
      videoPath,
      metadata: result.metadata
    };
  }
}
```

#### 2. Pika Labs

**Features:**
- Text-to-Video
- Image-to-Video
- Expand Canvas
- Modify Region
- Lip Sync

**Pricing:**
- Free Tier: 250 credits (limited)
- Paid: $10/month (550 credits)

**Automation Strategy:**
```typescript
/**
 * Pika Labs Provider Implementation
 * ใช้ Discord bot automation (Pika ใช้ Discord เป็น interface)
 */
class PikaLabsProvider extends BaseVideoProvider {
  async generate(config: VideoGenerationConfig): Promise<VideoResult> {
    // 1. Connect to Discord bot
    await this.discordClient.login(process.env.DISCORD_BOT_TOKEN);

    // 2. ส่ง command ไปยัง Pika bot
    const channel = await this.discordClient.channels.fetch(PIKA_CHANNEL_ID);

    // 3. ส่ง prompt
    await channel.send(`/create prompt:"${config.prompt}"`);

    // 4. รอ response จาก bot
    const response = await this.waitForBotResponse();

    // 5. Download วีดีโอจาก Discord attachment
    const videoUrl = response.attachments.first()?.url;
    const videoPath = await this.downloadVideo(videoUrl);

    return {
      success: true,
      videoPath,
      metadata: response.metadata
    };
  }
}
```

#### 3. Luma AI (Dream Machine)

**Features:**
- Realistic video generation
- Consistent characters
- Smooth motion
- 120 frames (5 seconds)

**Pricing:**
- Free: 30 videos/month
- Paid: $9.99/month (unlimited)

---

## Music Generation Pipeline

### Workflow

```
┌─────────────────┐
│  Client Request │
│                 │
│ • prompt        │
│ • duration      │
│ • genre         │
│ • mood          │
└────────┬────────┘
         │
         ▼
┌─────────────────────────────┐
│  API Endpoint               │
│  POST /api/v1/music/generate│
└────────┬────────────────────┘
         │
         ▼
┌─────────────────────────────┐
│  Music Generation Service   │
│                             │
│ 1. Validate prompt          │
│ 2. Create job               │
│ 3. Queue job                │
└────────┬────────────────────┘
         │
         ▼
┌─────────────────────────────┐
│  BullMQ Queue               │
│  "music-generation"         │
└────────┬────────────────────┘
         │
         ▼
┌─────────────────────────────┐
│  Music Generation Worker    │
│                             │
│ 1. Open Suno AI             │
│ 2. Login with session       │
│ 3. Submit music request     │
│ 4. Wait for generation      │
│ 5. Download MP3             │
└────────┬────────────────────┘
         │
         ▼
┌─────────────────────────────┐
│  Storage & Metadata         │
└─────────────────────────────┘
```

### Suno AI Automation

```typescript
/**
 * Suno AI Provider
 * ใช้ webview automation เพื่อสร้างเพลง
 */
class SunoAIProvider {
  /**
   * สร้างเพลงจาก prompt
   */
  async generateMusic(config: MusicGenerationConfig): Promise<MusicResult> {
    // 1. Launch browser
    const browser = await playwright.chromium.launch();
    const context = await browser.newContext({
      storageState: await this.sessionManager.getSession('suno')
    });

    const page = await context.newPage();

    // 2. ไปหน้า Suno AI
    await page.goto('https://app.suno.ai');

    // 3. คลิก Create
    await page.click('[data-testid="create-button"]');

    // 4. กรอก prompt และ settings
    await page.fill('[data-testid="song-description"]', config.prompt);
    await page.selectOption('[data-testid="genre-select"]', config.genre || 'pop');

    // 5. เริ่มสร้างเพลง
    await page.click('[data-testid="generate-button"]');

    // 6. รอจนกว่าเพลงจะสร้างเสร็จ (ประมาณ 60-120 วินาที)
    await page.waitForSelector('[data-testid="download-button"]', {
      timeout: 180000 // 3 นาที
    });

    // 7. Download MP3
    const downloadUrl = await page.getAttribute(
      '[data-testid="download-button"]',
      'href'
    );

    const musicPath = await this.downloadMusic(downloadUrl);

    // 8. บันทึก session
    await context.storageState({
      path: './sessions/suno-session.json'
    });

    await browser.close();

    return {
      success: true,
      musicPath,
      duration: config.duration,
      metadata: {
        genre: config.genre,
        prompt: config.prompt
      }
    };
  }
}
```

---

## Queue System

### BullMQ Architecture

```typescript
/**
 * Queue Manager
 * จัดการทุก queues ในระบบ
 */
class QueueManager {
  private queues: Map<string, Queue> = new Map();

  constructor() {
    // Video Generation Queue
    this.registerQueue('video-generation', {
      limiter: {
        max: 10,        // ประมวลผล 10 jobs ต่อ interval
        duration: 60000 // 1 นาที
      },
      defaultJobOptions: {
        attempts: 3,
        backoff: {
          type: 'exponential',
          delay: 5000
        },
        removeOnComplete: {
          age: 86400 // เก็บ completed jobs 24 ชั่วโมง
        },
        removeOnFail: {
          age: 604800 // เก็บ failed jobs 7 วัน
        }
      }
    });

    // Music Generation Queue
    this.registerQueue('music-generation', {
      limiter: {
        max: 5,
        duration: 60000
      }
    });

    // Media Processing Queue
    this.registerQueue('media-processing', {
      limiter: {
        max: 20,
        duration: 60000
      }
    });
  }

  /**
   * เพิ่ม job เข้า queue
   */
  async addJob(queueName: string, jobData: any, options?: JobOptions) {
    const queue = this.queues.get(queueName);
    if (!queue) {
      throw new Error(`Queue "${queueName}" not found`);
    }

    return queue.add(jobData.type, jobData, options);
  }

  /**
   * ดึงสถานะ queue
   */
  async getQueueStatus(queueName: string) {
    const queue = this.queues.get(queueName);
    if (!queue) {
      throw new Error(`Queue "${queueName}" not found`);
    }

    return {
      waiting: await queue.getWaitingCount(),
      active: await queue.getActiveCount(),
      completed: await queue.getCompletedCount(),
      failed: await queue.getFailedCount(),
      delayed: await queue.getDelayedCount()
    };
  }
}
```

### Queue Workers

```typescript
/**
 * Video Generation Worker
 * ประมวลผล video generation jobs
 */
class VideoGenerationWorker {
  private worker: Worker;

  constructor(queueManager: QueueManager) {
    this.worker = new Worker(
      'video-generation',
      async (job: Job) => {
        const { config } = job.data;

        // อัพเดทความคืบหน้า
        await job.updateProgress(10);

        // เลือก provider
        const provider = this.selectProvider(config.provider);

        await job.updateProgress(20);

        // สร้างวีดีโอ
        const result = await provider.generate(config);

        await job.updateProgress(80);

        // Upload ไป storage
        const uploadedUrl = await this.storageService.upload(
          result.videoPath,
          `videos/${job.id}.mp4`
        );

        await job.updateProgress(100);

        return {
          success: true,
          videoUrl: uploadedUrl,
          metadata: result.metadata
        };
      },
      {
        connection: queueManager.getRedisConnection(),
        concurrency: 3, // รัน 3 jobs พร้อมกัน
      }
    );

    // Event handlers
    this.worker.on('completed', (job) => {
      logger.info(`Job ${job.id} completed successfully`);
    });

    this.worker.on('failed', (job, err) => {
      logger.error(`Job ${job?.id} failed:`, err);
    });
  }
}
```

---

## Web Automation Strategy

### Browser Management

```typescript
/**
 * Browser Automation Service
 * จัดการ browser instances และ sessions
 */
class BrowserAutomation {
  private browserPool: Map<string, Browser> = new Map();

  /**
   * Launch browser instance
   */
  async launch(provider: string, options?: LaunchOptions): Promise<Page> {
    // ใช้ existing browser ถ้ามี
    if (this.browserPool.has(provider)) {
      const browser = this.browserPool.get(provider)!;
      const page = await browser.newPage();
      return page;
    }

    // สร้าง browser ใหม่
    const browser = await playwright.chromium.launch({
      headless: process.env.NODE_ENV === 'production',
      args: [
        '--no-sandbox',
        '--disable-setuid-sandbox',
        '--disable-dev-shm-usage',
        '--disable-accelerated-2d-canvas',
        '--disable-gpu'
      ],
      ...options
    });

    this.browserPool.set(provider, browser);

    return browser.newPage();
  }

  /**
   * ปิด browser instance
   */
  async close(provider: string) {
    const browser = this.browserPool.get(provider);
    if (browser) {
      await browser.close();
      this.browserPool.delete(provider);
    }
  }
}
```

### Session Management

```typescript
/**
 * Session Manager
 * จัดการ login sessions สำหรับแต่ละ provider
 */
class SessionManager {
  private sessionsDir = './sessions';

  /**
   * บันทึก session
   */
  async saveSession(provider: string, state: any) {
    const sessionPath = path.join(this.sessionsDir, `${provider}-session.json`);
    await fs.writeFile(sessionPath, JSON.stringify(state, null, 2));
  }

  /**
   * โหลด session
   */
  async getSession(provider: string): Promise<any | null> {
    const sessionPath = path.join(this.sessionsDir, `${provider}-session.json`);

    try {
      const data = await fs.readFile(sessionPath, 'utf-8');
      return JSON.parse(data);
    } catch (error) {
      return null;
    }
  }

  /**
   * ตรวจสอบว่า session ยังใช้ได้หรือไม่
   */
  async isSessionValid(provider: string): Promise<boolean> {
    const session = await this.getSession(provider);
    if (!session) return false;

    // ตรวจสอบ expiry
    if (session.expiresAt && Date.now() > session.expiresAt) {
      return false;
    }

    return true;
  }
}
```

---

## Data Flow

### Complete Video + Music Generation Flow

```
┌──────────────────────────────────────────────────────────────────────────┐
│                         User Request                                      │
│  POST /api/v1/media/create-video-with-music                              │
│                                                                           │
│  {                                                                        │
│    "video": {                                                             │
│      "concept": "A serene beach at sunset",                               │
│      "duration": 10,                                                      │
│      "provider": "runway"                                                 │
│    },                                                                     │
│    "music": {                                                             │
│      "prompt": "Calm ambient beach music",                                │
│      "genre": "ambient",                                                  │
│      "duration": 10                                                       │
│    },                                                                     │
│    "output": {                                                            │
│      "format": "mp4",                                                     │
│      "quality": "1080p"                                                   │
│    }                                                                      │
│  }                                                                        │
└──────────────────────────┬───────────────────────────────────────────────┘
                           │
                           ▼
┌──────────────────────────────────────────────────────────────────────────┐
│                    Media Service API Layer                                │
│                                                                           │
│  1. Validate request                                                      │
│  2. Create parent job (orchestration)                                     │
│  3. Create child jobs:                                                    │
│     a. Video generation job                                               │
│     b. Music generation job                                               │
└──────────────────────────┬───────────────────────────────────────────────┘
                           │
                           ├──────────────────┬──────────────────┐
                           │                  │                  │
                           ▼                  ▼                  ▼
              ┌──────────────────┐  ┌──────────────────┐  ┌──────────────────┐
              │ Video Queue      │  │ Music Queue      │  │ Status Queue     │
              └────────┬─────────┘  └────────┬─────────┘  └──────────────────┘
                       │                     │
                       ▼                     ▼
              ┌──────────────────┐  ┌──────────────────┐
              │ Video Worker     │  │ Music Worker     │
              │                  │  │                  │
              │ 1. Launch browser│  │ 1. Launch browser│
              │ 2. Login to      │  │ 2. Login to      │
              │    Runway        │  │    Suno AI       │
              │ 3. Generate video│  │ 3. Generate music│
              │ 4. Download      │  │ 4. Download      │
              └────────┬─────────┘  └────────┬─────────┘
                       │                     │
                       │                     │
                       └──────────┬──────────┘
                                  │
                                  ▼
                      ┌───────────────────────┐
                      │ Processing Queue      │
                      │                       │
                      │ Waiting for both:     │
                      │ • video.mp4           │
                      │ • music.mp3           │
                      └──────────┬────────────┘
                                 │
                                 ▼
                      ┌───────────────────────┐
                      │ Processing Worker     │
                      │                       │
                      │ 1. Load video         │
                      │ 2. Load music         │
                      │ 3. Mix audio          │
                      │ 4. Encode output      │
                      │ 5. Upload result      │
                      └──────────┬────────────┘
                                 │
                                 ▼
                      ┌───────────────────────┐
                      │ Storage Service       │
                      │                       │
                      │ • S3/MinIO upload     │
                      │ • Generate preview    │
                      │ • Update database     │
                      └──────────┬────────────┘
                                 │
                                 ▼
                      ┌───────────────────────┐
                      │ Response to Client    │
                      │                       │
                      │ {                     │
                      │   "jobId": "...",     │
                      │   "status": "...",    │
                      │   "videoUrl": "...",  │
                      │   "previewUrl": "..." │
                      │ }                     │
                      └───────────────────────┘
```

---

## Deployment Architecture

### Production Setup

```
                              ┌────────────────────────┐
                              │   Cloudflare CDN       │
                              │   (Static Assets)      │
                              └───────────┬────────────┘
                                          │
                                          ▼
                              ┌────────────────────────┐
                              │   Load Balancer        │
                              │   (Nginx/HAProxy)      │
                              └───────────┬────────────┘
                                          │
                 ┌────────────────────────┼────────────────────────┐
                 │                        │                        │
                 ▼                        ▼                        ▼
     ┌──────────────────────┐ ┌──────────────────────┐ ┌──────────────────────┐
     │  Laravel Instance    │ │  Media Service       │ │  AI Manager Core     │
     │  (3 replicas)        │ │  (3 replicas)        │ │  (Windows Server)    │
     │                      │ │                      │ │                      │
     │  Docker Container    │ │  Docker Container    │ │  IIS / Windows Svc   │
     │  Port: 8000          │ │  Port: 3000          │ │  Port: 5000-5002     │
     └──────────┬───────────┘ └──────────┬───────────┘ └──────────┬───────────┘
                │                        │                        │
                └────────────────────────┼────────────────────────┘
                                         │
                 ┌───────────────────────┼───────────────────────┐
                 │                       │                       │
                 ▼                       ▼                       ▼
     ┌──────────────────────┐ ┌──────────────────────┐ ┌──────────────────────┐
     │  Redis Cluster       │ │  PostgreSQL Primary  │ │  MinIO/S3 Storage    │
     │  (Master + 2 Slaves) │ │  + Replica           │ │                      │
     │                      │ │                      │ │  • Videos            │
     │  • Cache             │ │  • Media metadata    │ │  • Music             │
     │  • Queues            │ │  • Jobs              │ │  • Assets            │
     │  • Sessions          │ │  • Logs              │ │                      │
     └──────────────────────┘ └──────────────────────┘ └──────────────────────┘
```

### Docker Compose Example

```yaml
version: '3.8'

services:
  # Media Service
  media-service:
    build:
      context: ./media-service
      dockerfile: Dockerfile
    ports:
      - "3000:3000"
    environment:
      - NODE_ENV=production
      - REDIS_URL=redis://redis:6379
      - DATABASE_URL=postgresql://user:pass@postgres:5432/mediadb
      - S3_ENDPOINT=http://minio:9000
    depends_on:
      - redis
      - postgres
      - minio
    volumes:
      - ./media-service/sessions:/app/sessions
      - ./media-service/temp:/app/temp
    restart: unless-stopped
    deploy:
      replicas: 3
      resources:
        limits:
          cpus: '2'
          memory: 4G

  # Redis
  redis:
    image: redis:7-alpine
    ports:
      - "6379:6379"
    volumes:
      - redis-data:/data
    command: redis-server --appendonly yes
    restart: unless-stopped

  # PostgreSQL
  postgres:
    image: postgres:16-alpine
    ports:
      - "5432:5432"
    environment:
      - POSTGRES_USER=mediauser
      - POSTGRES_PASSWORD=mediapass
      - POSTGRES_DB=mediadb
    volumes:
      - postgres-data:/var/lib/postgresql/data
    restart: unless-stopped

  # MinIO (S3-compatible storage)
  minio:
    image: minio/minio:latest
    ports:
      - "9000:9000"
      - "9001:9001"
    environment:
      - MINIO_ROOT_USER=minioadmin
      - MINIO_ROOT_PASSWORD=minioadmin123
    volumes:
      - minio-data:/data
    command: server /data --console-address ":9001"
    restart: unless-stopped

volumes:
  redis-data:
  postgres-data:
  minio-data:
```

---

## Security Considerations

### 1. Credentials Management

```typescript
/**
 * Encrypted Credentials Storage
 */
class CredentialsVault {
  private encryptionKey: string;

  /**
   * เข้ารหัสและบันทึก credentials
   */
  async saveCredentials(provider: string, credentials: any) {
    const encrypted = this.encrypt(JSON.stringify(credentials));
    await this.db.credentials.create({
      data: {
        provider,
        encryptedData: encrypted,
        createdAt: new Date()
      }
    });
  }

  /**
   * ถอดรหัสและดึง credentials
   */
  async getCredentials(provider: string): Promise<any> {
    const record = await this.db.credentials.findFirst({
      where: { provider }
    });

    if (!record) return null;

    const decrypted = this.decrypt(record.encryptedData);
    return JSON.parse(decrypted);
  }

  private encrypt(text: string): string {
    const cipher = crypto.createCipher('aes-256-cbc', this.encryptionKey);
    let encrypted = cipher.update(text, 'utf8', 'hex');
    encrypted += cipher.final('hex');
    return encrypted;
  }

  private decrypt(encryptedText: string): string {
    const decipher = crypto.createDecipher('aes-256-cbc', this.encryptionKey);
    let decrypted = decipher.update(encryptedText, 'hex', 'utf8');
    decrypted += decipher.final('utf8');
    return decrypted;
  }
}
```

### 2. Rate Limiting

```typescript
/**
 * API Rate Limiting
 */
const rateLimitConfig = {
  windowMs: 15 * 60 * 1000, // 15 นาที
  max: 100, // จำกัด 100 requests ต่อ window
  message: 'Too many requests from this IP',
  standardHeaders: true,
  legacyHeaders: false,
};

app.use('/api/', rateLimit(rateLimitConfig));
```

---

## Performance Optimization

### 1. Caching Strategy

```typescript
/**
 * Multi-Layer Caching
 */
class CacheManager {
  /**
   * L1: In-Memory Cache (LRU)
   */
  private l1Cache = new LRU({
    max: 1000,
    ttl: 5 * 60 * 1000 // 5 นาที
  });

  /**
   * L2: Redis Cache
   */
  private l2Cache: Redis;

  async get(key: string): Promise<any> {
    // ลอง L1 ก่อน
    const l1Result = this.l1Cache.get(key);
    if (l1Result) return l1Result;

    // ถ้าไม่มีใน L1 ลอง L2
    const l2Result = await this.l2Cache.get(key);
    if (l2Result) {
      this.l1Cache.set(key, l2Result);
      return JSON.parse(l2Result);
    }

    return null;
  }

  async set(key: string, value: any, ttl: number = 300) {
    this.l1Cache.set(key, value);
    await this.l2Cache.setex(key, ttl, JSON.stringify(value));
  }
}
```

### 2. Connection Pooling

```typescript
/**
 * Database Connection Pool
 */
const prisma = new PrismaClient({
  datasources: {
    db: {
      url: process.env.DATABASE_URL
    }
  },
  log: ['query', 'error', 'warn'],
});

// Connection pool settings
// จะถูกตั้งค่าใน DATABASE_URL:
// postgresql://user:pass@host:5432/db?connection_limit=10&pool_timeout=60
```

---

## Monitoring & Logging

### 1. Structured Logging

```typescript
/**
 * Winston Logger Configuration
 */
const logger = winston.createLogger({
  level: process.env.LOG_LEVEL || 'info',
  format: winston.format.combine(
    winston.format.timestamp(),
    winston.format.errors({ stack: true }),
    winston.format.json()
  ),
  defaultMeta: { service: 'media-service' },
  transports: [
    // Console output
    new winston.transports.Console({
      format: winston.format.combine(
        winston.format.colorize(),
        winston.format.simple()
      )
    }),

    // File output
    new winston.transports.File({
      filename: 'logs/error.log',
      level: 'error'
    }),
    new winston.transports.File({
      filename: 'logs/combined.log'
    }),

    // ส่งไป Loki/Elasticsearch (production)
    new LokiTransport({
      host: process.env.LOKI_HOST
    })
  ]
});
```

### 2. Metrics Collection

```typescript
/**
 * Prometheus Metrics
 */
const register = new promClient.Registry();

// Request duration histogram
const httpRequestDuration = new promClient.Histogram({
  name: 'http_request_duration_seconds',
  help: 'Duration of HTTP requests in seconds',
  labelNames: ['method', 'route', 'status_code'],
  registers: [register]
});

// Job processing metrics
const jobProcessingDuration = new promClient.Histogram({
  name: 'job_processing_duration_seconds',
  help: 'Duration of job processing in seconds',
  labelNames: ['queue', 'job_type'],
  registers: [register]
});

// Active jobs gauge
const activeJobs = new promClient.Gauge({
  name: 'active_jobs_count',
  help: 'Number of currently active jobs',
  labelNames: ['queue'],
  registers: [register]
});

// Expose metrics endpoint
app.get('/metrics', async (req, res) => {
  res.set('Content-Type', register.contentType);
  res.end(await register.metrics());
});
```

---

## Disaster Recovery

### Backup Strategy

```yaml
# Automated Backup Configuration
backups:
  database:
    schedule: "0 2 * * *"  # ทุกวัน 2:00 AM
    retention: 30          # เก็บ 30 วัน
    destination: "s3://backups/postgres/"

  redis:
    schedule: "0 */6 * * *"  # ทุก 6 ชั่วโมง
    type: "RDB"
    destination: "s3://backups/redis/"

  storage:
    schedule: "0 1 * * 0"  # ทุกวันอาทิตย์ 1:00 AM
    type: "incremental"
    destination: "s3://backups/media/"
```

---

## Conclusion

สถาปัตยกรรมนี้ออกแบบมาเพื่อ:

✅ **Scalability** - Scale ได้อิสระแต่ละ component
✅ **Reliability** - มี failover และ retry mechanisms
✅ **Performance** - ใช้ caching และ queue system
✅ **Maintainability** - Code ที่ clean และมี type safety
✅ **Security** - Encrypted credentials และ rate limiting
✅ **Observability** - Logging, metrics, และ monitoring ครบถ้วน

---

**Next Steps:**
1. Implement core services
2. Setup development environment
3. Write tests
4. Deploy to staging
5. Performance testing
6. Production deployment

---

**Document Version**: 1.0.0
**Last Updated**: 24 December 2025
**Contact**: dev@postxagent.com
