# Video & Music Generation Guide
## PostXAgent AI Media Generation System

**Version**: 1.0.0
**Last Updated**: December 24, 2025

---

## 📋 Table of Contents

1. [Overview](#overview)
2. [Features](#features)
3. [Prerequisites](#prerequisites)
4. [Quick Start](#quick-start)
5. [Video Generation](#video-generation)
6. [Music Generation](#music-generation)
7. [Video Processing](#video-processing)
8. [API Reference](#api-reference)
9. [Web Learning System](#web-learning-system)
10. [Troubleshooting](#troubleshooting)

---

## 🎯 Overview

PostXAgent ตอนนี้รองรับการสร้างวีดีโอและเพลงด้วย AI แล้ว! ระบบใช้:

- **Freepik Pikaso AI** (PRIMARY) สำหรับการสร้างวีดีโอ
- **Suno AI** (PRIMARY) สำหรับการสร้างเพลง
- **FFmpeg** สำหรับการประมวลผลวีดีโอ/เสียง
- **Web Learning System** สำหรับการเรียนรู้และทำงานอัตโนมัติ

### Architecture Overview

```
┌─────────────────────────────────────────────────────────────┐
│                  Laravel Backend (Web UI)                    │
│                      Port: 8000                              │
└───────────────────────────┬─────────────────────────────────┘
                            │ HTTP API
                            ▼
┌─────────────────────────────────────────────────────────────┐
│               AI Manager Core (.NET 8.0)                     │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐      │
│  │ MediaGen API │  │ FreepikWorker│  │ SunoAIWorker │      │
│  │ Port 5000    │  │ (Video)      │  │ (Music)      │      │
│  └──────────────┘  └──────────────┘  └──────────────┘      │
│                            │                                 │
│              ┌─────────────┴──────────────┐                 │
│              │   Web Learning Engine       │                 │
│              │   (Browser Automation)      │                 │
│              └─────────────┬──────────────┘                 │
│                            │                                 │
│              ┌─────────────┴──────────────┐                 │
│              │      FFmpeg Service         │                 │
│              │   (Video/Audio Processing)  │                 │
│              └─────────────────────────────┘                 │
└─────────────────────────────────────────────────────────────┘
```

---

## ✨ Features

### Video Generation (Freepik Pikaso AI)

- ✅ Text-to-Video: สร้างวีดีโอจากคำอธิบาย
- ✅ Image-to-Video: แปลงรูปภาพเป็นวีดีโอ
- ✅ Video-to-Video: แปลงวีดีโอให้เป็นสไตล์ใหม่
- ✅ รองรับ Aspect Ratios: 16:9, 9:16, 1:1, 4:3, 21:9
- ✅ Quality Options: 480p, 720p, 1080p, 4K
- ✅ Customizable: Animation style, camera movement, lighting, color palette
- ✅ Web Learning: เรียนรู้และทำงานอัตโนมัติ

### Music Generation (Suno AI)

- ✅ Text-to-Music: สร้างเพลงจากคำอธิบาย
- ✅ 20+ Music Genres: Pop, Rock, Electronic, Jazz, Classical, etc.
- ✅ 12+ Moods: Happy, Sad, Energetic, Calm, etc.
- ✅ Instrumental/Vocal: เลือกได้ว่าต้องการเสียงร้องหรือไม่
- ✅ Custom Lyrics: สามารถกำหนดเนื้อเพลงเองได้
- ✅ Multiple Variations: สร้างหลายเวอร์ชันพร้อมกัน
- ✅ Web Learning: เรียนรู้และทำงานอัตโนมัติ

### Video Processing (FFmpeg)

- ✅ Mix Video with Audio: ผสมวีดีโอกับเพลง
- ✅ Concatenate Videos: ต่อวีดีโอหลายไฟล์
- ✅ Extract Audio: ดึงเสียงออกจากวีดีโอ
- ✅ Generate Thumbnails: สร้าง thumbnail อัตโนมัติ
- ✅ Convert Formats: แปลงรูปแบบวีดีโอ
- ✅ Resize Videos: ปรับขนาดและ aspect ratio
- ✅ Platform Optimization: ปรับวีดีโอให้เหมาะกับแต่ละแพลตฟอร์ม

---

## 📦 Prerequisites

### Required Software

1. **FFmpeg** (สำหรับการประมวลผลวีดีโอ/เสียง)
   ```bash
   # Windows (ใช้ Chocolatey)
   choco install ffmpeg

   # หรือดาวน์โหลดจาก
   https://ffmpeg.org/download.html
   ```

2. **Playwright** (สำหรับ Web Learning - ติดตั้งอัตโนมัติ)
   - จะถูกติดตั้งพร้อมกับ AIManager.Core

3. **.NET 8.0 SDK**
   - ดาวน์โหลดจาก: https://dotnet.microsoft.com/download

### Optional (สำหรับการพัฒนา)

- **Node.js** (สำหรับ TypeScript media-service)
- **Docker** (สำหรับ deployment)

---

## 🚀 Quick Start

### 1. การตั้งค่าครั้งแรก

```bash
# 1. Build AIManager.Core
cd AIManagerCore
dotnet build

# 2. Run API Server
cd src/AIManager.API
dotnet run

# API จะทำงานที่: http://localhost:5000
```

### 2. ทดสอบการสร้างวีดีโอ (Quick Test)

```bash
# ใช้ curl หรือ Postman
curl -X POST http://localhost:5000/api/MediaGeneration/test/quick-video \
  -H "Content-Type: application/json" \
  -d '{
    "prompt": "สวนสาธารณะสวยๆ ยามเช้า มีนกบินผ่าน",
    "duration": 5,
    "aspectRatio": "Landscape_16_9"
  }'
```

### 3. ทดสอบการสร้างเพลง (Quick Test)

```bash
curl -X POST http://localhost:5000/api/MediaGeneration/test/quick-music \
  -H "Content-Type: application/json" \
  -d '{
    "prompt": "เพลงสบายๆ สไตล์ acoustic guitar",
    "duration": 30,
    "genre": "Acoustic",
    "mood": "Calm",
    "instrumental": true
  }'
```

---

## 🎬 Video Generation

### Basic Usage

#### Text-to-Video

```csharp
var request = new VideoGenerationRequest
{
    UserId = 1,
    BrandId = 1,
    Config = new VideoGenerationConfig
    {
        Mode = VideoGenerationMode.TextToVideo,
        Prompt = "แมวน้อยน่ารักกำลังเล่นในสวน",
        Duration = 5,
        AspectRatio = AspectRatio.Landscape_16_9,
        Quality = VideoQuality.High_1080p,
        Fps = 30
    }
};

// Submit via API
var response = await httpClient.PostAsJsonAsync(
    "http://localhost:5000/api/MediaGeneration/generate-video",
    request
);
```

#### Image-to-Video

```csharp
var config = new VideoGenerationConfig
{
    Mode = VideoGenerationMode.ImageToVideo,
    SourceImageUrl = "https://example.com/image.jpg",
    Prompt = "ทำให้ภาพเคลื่อนไหวแบบนุ่มนวล",
    Duration = 5
};
```

### Advanced Configuration

#### Freepik-Specific Options

```csharp
var config = new VideoGenerationConfig
{
    Prompt = "ทะเลยามพระอาทิตย์ตก คลื่นเบาๆ",
    Duration = 10,
    AspectRatio = AspectRatio.Landscape_16_9,
    Quality = VideoQuality.High_1080p,

    // Freepik-specific settings
    FreepikOptions = new FreepikOptions
    {
        AnimationStyle = "smooth",      // smooth, dynamic, dramatic
        CameraMovement = "pan",         // static, pan, zoom, rotate, orbit
        MotionIntensity = 7,            // 1-10
        ColorPalette = "warm",          // vibrant, pastel, monochrome, warm, cool
        Lighting = "natural",           // natural, studio, dramatic, soft, neon
        EndFrame = "fade"               // zoom_in, zoom_out, fade, still
    }
};
```

### Aspect Ratios

| Aspect Ratio | Resolution | Use Case |
|--------------|------------|----------|
| Landscape_16_9 | 1920x1080 | YouTube, Facebook, General |
| Portrait_9_16 | 1080x1920 | TikTok, Instagram Stories/Reels |
| Square_1_1 | 1080x1080 | Instagram Feed |
| Classic_4_3 | 1440x1080 | Classic TV format |
| Ultrawide_21_9 | 2560x1080 | Cinematic |

### Quality Levels

| Quality | Resolution | Bitrate | Use Case |
|---------|------------|---------|----------|
| Low_480p | 854x480 | Low | Preview, Testing |
| Medium_720p | 1280x720 | Medium | Web, Mobile |
| High_1080p | 1920x1080 | High | Social Media, General |
| Ultra_4K | 3840x2160 | Very High | YouTube, Premium |

---

## 🎵 Music Generation

### Basic Usage

```csharp
var request = new MusicGenerationRequest
{
    UserId = 1,
    BrandId = 1,
    Config = new MusicGenerationConfig
    {
        Prompt = "เพลงสนุกสนานสำหรับโฆษณาผลิตภัณฑ์",
        Duration = 30,
        Genre = MusicGenre.Pop,
        Mood = MusicMood.Happy,
        Instrumental = true,
        NumberOfOutputs = 2  // สร้าง 2 เวอร์ชัน
    }
};
```

### With Custom Lyrics

```csharp
var config = new MusicGenerationConfig
{
    Prompt = "เพลงรักสไตล์ acoustic",
    Duration = 60,
    Genre = MusicGenre.Acoustic,
    Mood = MusicMood.Romantic,
    Instrumental = false,
    Lyrics = @"
        สวัสดีเธอผู้น่ารัก
        ฉันมีเรื่องจะบอก
        ว่าฉันรักเธอนะ
        มากกว่าที่เคย
    ",
    Bpm = 90,
    KeySignature = "C Major"
};
```

### Available Genres

```
Pop, Rock, Electronic, HipHop, Jazz, Classical, Ambient,
Cinematic, LoFi, Acoustic, Country, Blues, Reggae, Metal,
Folk, RnB, Dance, Indie, Soul, Funk
```

### Available Moods

```
Happy, Sad, Energetic, Calm, Romantic, Aggressive,
Mysterious, Epic, Peaceful, Dark, Uplifting, Melancholic
```

---

## 🎞️ Video Processing

### Mix Video with Audio

```csharp
var request = new VideoProcessingRequest
{
    UserId = 1,
    BrandId = 1,
    Config = new MediaProcessingConfig
    {
        VideoPath = "/path/to/video.mp4",
        AudioPath = "/path/to/music.mp3",
        MixAudio = true,
        AudioVolume = 0.8,
        OutputFormat = "mp4",
        GenerateThumbnail = true
    }
};
```

### Concatenate Multiple Videos

```csharp
var config = new MediaProcessingConfig
{
    VideosToConcat = new List<string>
    {
        "/path/to/video1.mp4",
        "/path/to/video2.mp4",
        "/path/to/video3.mp4"
    },
    OutputFormat = "mp4",
    GenerateThumbnail = true
};
```

### Prepare Video for Platform

```csharp
// ใช้ VideoProcessor โดยตรง
var videoProcessor = new VideoProcessor(ffmpegService, logger);

var result = await videoProcessor.PrepareForPlatformAsync(
    videoPath: "/path/to/video.mp4",
    platform: SocialPlatform.TikTok,
    targetAspectRatio: AspectRatio.Portrait_9_16
);
```

---

## 📡 API Reference

### Base URL

```
http://localhost:5000/api/MediaGeneration
```

### Endpoints

#### 1. Generate Video

**POST** `/generate-video`

Request Body:
```json
{
  "userId": 1,
  "brandId": 1,
  "priority": 5,
  "config": {
    "mode": "TextToVideo",
    "prompt": "สวนสาธารณะสวยๆ ยามเช้า",
    "duration": 5,
    "aspectRatio": "Landscape_16_9",
    "quality": "High_1080p",
    "fps": 30,
    "freepikOptions": {
      "animationStyle": "smooth",
      "cameraMovement": "pan",
      "motionIntensity": 7
    }
  }
}
```

Response:
```json
{
  "success": true,
  "taskId": "550e8400-e29b-41d4-a716-446655440000",
  "message": "Video generation task submitted successfully"
}
```

#### 2. Generate Music

**POST** `/generate-music`

Request Body:
```json
{
  "userId": 1,
  "brandId": 1,
  "config": {
    "prompt": "เพลงสบายๆ สไตล์ acoustic",
    "duration": 30,
    "genre": "Acoustic",
    "mood": "Calm",
    "instrumental": true,
    "numberOfOutputs": 2
  }
}
```

#### 3. Process Video

**POST** `/process-video`

Request Body:
```json
{
  "userId": 1,
  "brandId": 1,
  "config": {
    "videoPath": "/path/to/video.mp4",
    "audioPath": "/path/to/music.mp3",
    "mixAudio": true,
    "audioVolume": 0.8,
    "outputFormat": "mp4",
    "generateThumbnail": true
  }
}
```

#### 4. Get Result

**GET** `/result/{taskId}`

Response:
```json
{
  "success": true,
  "taskId": "550e8400-e29b-41d4-a716-446655440000",
  "status": "Completed",
  "result": {
    "videoUrl": "https://...",
    "videoPath": "/path/to/generated/video.mp4",
    "thumbnailPath": "/path/to/thumbnail.jpg",
    "metadata": {
      "width": 1920,
      "height": 1080,
      "duration": 5.0,
      "fps": 30,
      "fileSize": 12345678,
      "format": "mp4"
    }
  }
}
```

#### 5. Quick Test Endpoints

**POST** `/test/quick-video`
```json
{
  "prompt": "แมวน้อยน่ารัก",
  "duration": 5,
  "aspectRatio": "Landscape_16_9"
}
```

**POST** `/test/quick-music`
```json
{
  "prompt": "เพลงสบายๆ",
  "duration": 30,
  "genre": "Acoustic",
  "mood": "Calm"
}
```

---

## 🤖 Web Learning System

PostXAgent ใช้ **Web Learning System** ที่ทรงพลังในการเรียนรู้และทำงานกับเว็บไซต์อัตโนมัติ

### How It Works

1. **Learning Phase** (ครั้งแรก)
   - ระบบจะเปิดบราวเซอร์และรอให้ผู้ใช้ทำงาน
   - AI จะสังเกตและจดจำทุกขั้นตอน
   - บันทึกเป็น Workflow

2. **Execution Phase** (ครั้งต่อๆ ไป)
   - ระบบรัน Workflow ที่เรียนรู้ไว้แล้วอัตโนมัติ
   - ไม่ต้องมีการแทรกแซงจากผู้ใช้

3. **Auto-Repair** (ถ้าเจอปัญหา)
   - ถ้า Workflow ใช้ไม่ได้ (UI เปลี่ยน)
   - ระบบจะพยายามซ่อมแซมอัตโนมัติ
   - หรือขอให้ผู้ใช้สอนใหม่

### Learning Modes

| Mode | Description |
|------|-------------|
| **Manual** | ผู้ใช้สอนทีละขั้นตอน (click, type, etc.) |
| **AIObserved** | AI สังเกตการทำงานของผู้ใช้ |
| **AutoRepair** | AI พยายามซ่อมแซม workflow ที่เสีย |
| **PatternLearning** | เรียนรู้จากหลายๆ ตัวอย่าง |
| **ExecutionFeedback** | ปรับปรุงจาก feedback ตอนรัน |

### Workflow Storage

Workflows จะถูกเก็บไว้ที่:
```
AIManagerCore/workflows/
├── freepik_video_generation.json
├── suno_music_generation.json
└── ...
```

### First-Time Setup

เมื่อรันครั้งแรก:

1. ระบบจะเปิดบราวเซอร์ Chromium
2. Navigate ไปที่ Freepik/Suno AI
3. **ผู้ใช้ต้องทำตามขั้นตอนเหล่านี้:**
   - Login (ถ้าจำเป็น)
   - ใส่ prompt
   - กด generate
   - รอจนได้วีดีโอ/เพลง
   - คัดลอก URL
4. ระบบจะจำทุกขั้นตอนและบันทึก
5. ครั้งต่อไปจะทำอัตโนมัติ

---

## 🔧 Troubleshooting

### Problem: FFmpeg not found

**Solution**:
```bash
# Windows
choco install ffmpeg

# หรือ download จาก https://ffmpeg.org
# แล้วเพิ่มใน PATH
```

### Problem: Browser automation fails

**Solution**:
1. ตรวจสอบว่า Playwright ติดตั้งแล้ว:
   ```bash
   dotnet tool install --global Microsoft.Playwright.CLI
   playwright install
   ```

2. ตรวจสอบ workflow file:
   ```bash
   ls AIManagerCore/workflows/
   ```

3. ลบ workflow แล้วให้เรียนรู้ใหม่:
   ```bash
   rm AIManagerCore/workflows/freepik_video_generation.json
   ```

### Problem: Video generation timeout

**Cause**: Freepik/Suno AI อาจใช้เวลานาน

**Solution**:
- เพิ่ม timeout ใน configuration
- ลดความยาววีดีโอ/เพลง
- ลดจำนวน outputs

### Problem: Generated video quality is low

**Solution**:
```csharp
// เปลี่ยน quality setting
config.Quality = VideoQuality.Ultra_4K;

// ปรับ FFmpeg CRF (lower = better quality)
processingConfig.Crf = 18;  // default: 23
```

### Problem: Music generation returns only 1 variation

**Check**: NumberOfOutputs setting
```csharp
musicConfig.NumberOfOutputs = 2;  // Suno AI สร้าง 2 เวอร์ชัน
```

---

## 📚 Examples

### Example 1: Complete Social Media Video Workflow

```csharp
// 1. Generate video
var videoTask = new TaskItem
{
    Type = TaskType.GenerateVideo,
    Platform = SocialPlatform.Freepik,
    Payload = new TaskPayload
    {
        VideoConfig = new VideoGenerationConfig
        {
            Prompt = "ผลิตภัณฑ์ใหม่ของเรา น่าสนใจ ทันสมัย",
            Duration = 10,
            AspectRatio = AspectRatio.Portrait_9_16,  // สำหรับ TikTok
            Quality = VideoQuality.High_1080p
        }
    }
};

var videoResult = await orchestrator.SubmitTaskAsync(videoTask);

// 2. Generate music
var musicTask = new TaskItem
{
    Type = TaskType.GenerateMusic,
    Platform = SocialPlatform.SunoAI,
    Payload = new TaskPayload
    {
        MusicConfig = new MusicGenerationConfig
        {
            Prompt = "เพลงสนุกสนาน เหมาะกับโฆษณา",
            Duration = 10,
            Genre = MusicGenre.Pop,
            Mood = MusicMood.Energetic,
            Instrumental = true
        }
    }
};

var musicResult = await orchestrator.SubmitTaskAsync(musicTask);

// 3. Wait for both to complete
await Task.WhenAll(
    WaitForCompletion(videoResult),
    WaitForCompletion(musicResult)
);

// 4. Mix video with music
var mixTask = new TaskItem
{
    Type = TaskType.MixVideoWithMusic,
    Payload = new TaskPayload
    {
        ProcessingConfig = new MediaProcessingConfig
        {
            VideoPath = GetVideoPath(videoResult),
            AudioPath = GetAudioPath(musicResult),
            MixAudio = true,
            AudioVolume = 0.7,
            OutputFormat = "mp4"
        }
    }
};

var finalResult = await orchestrator.SubmitTaskAsync(mixTask);
```

### Example 2: Batch Video Creation

```csharp
var prompts = new[]
{
    "วิวทะเลสวยๆ ยามพระอาทิตย์ตก",
    "ภูเขาหิมะสูงตระหง่าน",
    "ป่าดงดิบเขียวชอุ่ม มีน้ำตกไหลผ่าน"
};

var tasks = prompts.Select(prompt => new TaskItem
{
    Type = TaskType.GenerateVideo,
    Platform = SocialPlatform.Freepik,
    Payload = new TaskPayload
    {
        VideoConfig = new VideoGenerationConfig
        {
            Prompt = prompt,
            Duration = 5,
            AspectRatio = AspectRatio.Landscape_16_9
        }
    }
}).ToList();

// Submit all tasks
var taskIds = await Task.WhenAll(
    tasks.Select(t => orchestrator.SubmitTaskAsync(t))
);

Console.WriteLine($"Submitted {taskIds.Length} video generation tasks");
```

---

## 🎓 Best Practices

### 1. Prompt Engineering (Video)

**Good prompts**:
- "สวนสาธารณะสวยๆ ยามเช้า มีนกบินผ่าน บรรยากาศสงบ"
- "แมวน้อยขาวนุ่มนิ่ม กำลังเล่นกับลูกบอลสีแดง บนพรมนุ่ม"

**Bad prompts**:
- "แมว" (สั้นเกินไป)
- "รูปแบบต่างๆของแมวที่มีพื้นหลังหลากหลาย..." (ยาวเกินไป ซับซ้อน)

### 2. Prompt Engineering (Music)

**Good prompts**:
- "เพลงสนุกสนาน เหมาะกับงานปาร์ตี้ มีจังหวะเร้าใจ"
- "เพลงบรรเลงสงบๆ สำหรับฟังขณะทำงาน"

**With genre + mood**:
```csharp
// ดีกว่าการใช้ prompt เดี่ยว
config.Prompt = "เพลงสำหรับเด็ก";
config.Genre = MusicGenre.Pop;
config.Mood = MusicMood.Happy;
```

### 3. Performance Optimization

```csharp
// 1. ใช้ batch processing
var tasks = videos.Select(v => CreateTask(v)).ToList();
await Task.WhenAll(tasks.Select(t => orchestrator.SubmitTaskAsync(t)));

// 2. ใช้ appropriate quality
config.Quality = platform == SocialPlatform.TikTok
    ? VideoQuality.High_1080p  // เพียงพอ
    : VideoQuality.Ultra_4K;   // สำหรับ YouTube

// 3. Cache workflows
// Workflows จะถูก cache อัตโนมัติ
```

### 4. Error Handling

```csharp
try
{
    var result = await orchestrator.SubmitTaskAsync(task);

    // Monitor task status
    while (true)
    {
        var status = orchestrator.GetTask(result);
        if (status.Status == TaskStatus.Completed) break;
        if (status.Status == TaskStatus.Failed)
        {
            logger.LogError("Task failed: {Error}", status.Error);
            break;
        }
        await Task.Delay(1000);
    }
}
catch (Exception ex)
{
    logger.LogError(ex, "Failed to process media");
    // Implement retry logic
}
```

---

## 📝 Notes

### Limitations

1. **Freepik Pikaso AI**:
   - ความยาวสูงสุด: ~30 วินาที (ขึ้นอยู่กับ plan)
   - ต้องมี account และ credits

2. **Suno AI**:
   - ความยาวสูงสุด: ~2 นาที per generation
   - Free tier: จำกัดจำนวนการสร้าง
   - สร้างทีละ 2 เวอร์ชัน

3. **FFmpeg**:
   - ต้องติดตั้งบนเครื่อง
   - Performance ขึ้นอยู่กับ hardware

### Future Enhancements

- [ ] รองรับ Runway ML (fallback provider)
- [ ] รองรับ Pika Labs (fallback provider)
- [ ] รองรับ Luma Dream Machine (fallback provider)
- [ ] GPU Acceleration สำหรับ FFmpeg
- [ ] Real-time progress tracking
- [ ] Webhook notifications
- [ ] Video preview generation

---

## 🆘 Support

หากมีปัญหาหรือข้อสงสัย:

1. ตรวจสอบ logs:
   ```
   AIManagerCore/logs/
   ```

2. ดู workflow files:
   ```
   AIManagerCore/workflows/
   ```

3. Enable debug logging:
   ```csharp
   builder.Logging.SetMinimumLevel(LogLevel.Debug);
   ```

4. Create issue ที่ GitHub repository

---

**Happy Creating! 🎬🎵**
