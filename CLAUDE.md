# Claude Development Guidelines for PostXAgent

## Project Overview

PostXAgent is an AI-powered Brand Promotion Manager system that automates social media marketing across multiple platforms in Thailand.

### Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                     Laravel Backend                          │
│                    (Web Control Panel)                       │
│                     Port: 8000                               │
└─────────────────────┬───────────────────────────────────────┘
                      │ HTTP/SignalR
                      ▼
┌─────────────────────────────────────────────────────────────┐
│              C# AI Manager Core (Windows Server)             │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐         │
│  │  REST API   │  │  WebSocket  │  │  SignalR    │         │
│  │  Port 5000  │  │  Port 5001  │  │  Port 5002  │         │
│  └─────────────┘  └─────────────┘  └─────────────┘         │
│                          │                                   │
│              ┌───────────┴───────────┐                      │
│              │  Process Orchestrator │                      │
│              │    (40+ CPU Cores)    │                      │
│              └───────────┬───────────┘                      │
│                          │                                   │
│    ┌─────────┬─────────┬─┴─────────┬─────────┬─────────┐   │
│    │ FB      │ IG      │ TikTok   │ Twitter │ LINE    │   │
│    │ Worker  │ Worker  │ Worker   │ Worker  │ Worker  │   │
│    └─────────┴─────────┴──────────┴─────────┴─────────┘   │
└─────────────────────────────────────────────────────────────┘
```

## Tech Stack

| Component | Technology | Version |
|-----------|------------|---------|
| AI Manager Core | C# / .NET | 8.0 |
| AI Manager UI | WPF + Material Design | - |
| Web Backend | Laravel (PHP) | 11.x |
| Frontend | Vue.js | 3.x |
| Database | MySQL/PostgreSQL | 8.0+ |
| Cache | Redis | 7.x |
| Real-time | SignalR | - |

## Development Rules

### 1. Language & Localization

- **Primary Language**: Thai (th) for user-facing content
- **Code Comments**: English
- **Variable Names**: English (camelCase for JS/C#, snake_case for PHP)
- **Support both Thai and English** in all user interfaces

### 2. Code Style

#### PHP/Laravel
```php
// Use strict types
declare(strict_types=1);

// Use type hints everywhere
public function processTask(TaskItem $task): TaskResult

// Use Laravel conventions
// Controllers: PascalCase + Controller suffix
// Models: PascalCase singular
// Tables: snake_case plural

// PHPDoc annotations สำหรับ properties ใน Models
/**
 * @property int $id
 * @property string $name
 * @property \Illuminate\Support\Carbon|null $created_at
 */
class User extends Authenticatable

// Type hints ใน closures (arrow functions)
->map(fn(Activity $log) => [...])
->map(fn(Campaign $c): array => [...])
->groupBy(fn(Post $p): string => $p->published_at->toDateString())

// Null-safe operator เมื่อมี nullable
$p->published_at?->toDateString() ?? ''

// PHPStan type annotations เมื่อจำเป็น
/** @var array<string, mixed> $poolArray */
$poolArray = $pool->toArray();

// PHPStan ignore comments เมื่อไม่สามารถแก้ได้
/** @phpstan-ignore-next-line */
```

#### C#/.NET
```csharp
// Use nullable reference types
public string? OptionalField { get; set; }

// Use async/await properly
public async Task<Result> ProcessAsync(CancellationToken ct)

// Use dependency injection
public class MyService(ILogger<MyService> logger)
```

#### Vue.js
```vue
<!-- Use Composition API -->
<script setup>
import { ref, computed } from 'vue'
</script>

<!-- Use scoped styles -->
<style scoped>
</style>
```

### 3. API Design

- **Version prefix**: `/api/v1/`
- **RESTful conventions**: GET, POST, PUT, DELETE
- **Response format**:
```json
{
  "success": true,
  "data": { },
  "message": "Operation completed",
  "errors": []
}
```

### 4. Social Media Platforms

Support all 9 platforms:
1. **Facebook** - Graph API
2. **Instagram** - Graph API (via Facebook)
3. **TikTok** - TikTok API
4. **Twitter/X** - Twitter API v2
5. **LINE** - Messaging API
6. **YouTube** - Data API v3
7. **Threads** - Threads API
8. **LinkedIn** - Marketing API
9. **Pinterest** - API v5

### 5. AI Providers

#### Content Generation (Priority Order)
1. **Ollama** (Free, Local) - Default for development
2. **Google Gemini** (Free tier available)
3. **OpenAI GPT-4** (Paid)
4. **Anthropic Claude** (Paid)

#### Image Generation (Priority Order)
1. **Stable Diffusion** (Free, Self-hosted)
2. **Leonardo.ai** (Free tier)
3. **DALL-E 3** (Paid)

#### Video Generation (Priority Order)
1. **Freepik Pikaso AI** (PRIMARY) - Placeholder until API available
2. **Runway ML** - Alternative (API available)
3. **Pika Labs** - Alternative (API available)
4. **Luma AI** - Alternative (API available)

#### Music Generation (Priority Order)
1. **Suno AI** (PRIMARY) - Placeholder until API available
2. **Stable Audio** - Alternative (API available)
3. **AudioCraft** (Meta) - Alternative (Open source)
4. **MusicGen** - Alternative (Open source)

### 6. Environment Variables

Required in `.env`:
```env
# AI Manager Connection
AI_MANAGER_HOST=localhost
AI_MANAGER_API_PORT=5000
AI_MANAGER_SIGNALR_PORT=5002

# AI Providers
OPENAI_API_KEY=
ANTHROPIC_API_KEY=
GOOGLE_API_KEY=
OLLAMA_BASE_URL=http://localhost:11434

# Social Media APIs
FACEBOOK_APP_ID=
FACEBOOK_APP_SECRET=
TWITTER_API_KEY=
# ... etc
```

### 7. Git Workflow

#### Branch Naming
```
feature/   - New features
fix/       - Bug fixes
refactor/  - Code refactoring
docs/      - Documentation
```

#### Commit Messages
```
feat: Add new feature
fix: Fix bug in X
refactor: Improve Y performance
docs: Update README
chore: Update dependencies
```

#### Version Bumping
- **patch** (1.0.x): Bug fixes
- **minor** (1.x.0): New features (backward compatible)
- **major** (x.0.0): Breaking changes

### 8. File Structure

```
PostXAgent/
├── AIManagerCore/           # C# Solution
│   └── src/
│       ├── AIManager.Core/  # Core library
│       ├── AIManager.API/   # REST API
│       └── AIManager.UI/    # WPF Dashboard
├── laravel-backend/         # Laravel App
│   ├── app/
│   │   ├── Http/Controllers/Api/
│   │   ├── Models/
│   │   └── Services/
│   ├── config/
│   └── resources/js/components/
├── .github/workflows/       # CI/CD
└── docs/                    # Documentation
```

### 9. Security Guidelines

- **Never commit secrets** (.env, API keys, credentials)
- **Validate all inputs** on both client and server
- **Use HTTPS** in production
- **Implement rate limiting** on APIs
- **Use prepared statements** for database queries
- **Sanitize user content** before posting to social media

### 10. Testing Requirements

- **Laravel**: Feature tests for all API endpoints
- **C#**: Unit tests for core services
- **Minimum coverage**: 70%

```bash
# Laravel
php artisan test

# C#
dotnet test
```

### 11. Performance Guidelines

- **Use caching** for frequently accessed data
- **Implement pagination** for list endpoints
- **Use async operations** for I/O-bound tasks
- **Optimize database queries** (avoid N+1)
- **Use job queues** for long-running tasks

### 12. Error Handling

```php
// Laravel - Use custom exceptions
throw new AIManagerConnectionException('Failed to connect');

// Return consistent error responses
return response()->json([
    'success' => false,
    'error' => 'Connection failed',
    'code' => 'AI_MANAGER_OFFLINE'
], 503);
```

```csharp
// C# - Use Result pattern
public record Result<T>(bool Success, T? Data, string? Error);
```

## Common Tasks

### Adding a New Social Platform

1. Create worker in `AIManagerCore/src/AIManager.Core/Workers/`
2. Add enum value in `Enums.cs`
3. Register in `WorkerFactory.cs`
4. Add Laravel service method
5. Update Vue components

### Adding a New AI Provider

1. Add config in `AIConfig.cs`
2. Implement generator method in `ContentGeneratorService.cs`
3. Add to provider priority list
4. Update Laravel config

### Updating API Endpoints

1. Add route in `routes/api.php`
2. Create/update controller
3. Add service method if needed
4. Update Vue API client
5. Add tests

### Testing Media Generation APIs

See detailed testing guide in `AIManagerCore/docs/API_TESTING.md`

**Quick test endpoints**:
```bash
# Test video generation
POST /api/MediaGeneration/test/quick-video
{
  "prompt": "A cat playing piano",
  "duration": 5,
  "aspectRatio": "Landscape_16_9"
}

# Test music generation
POST /api/MediaGeneration/test/quick-music
{
  "prompt": "Upbeat electronic music",
  "duration": 30,
  "genre": "Electronic",
  "mood": "Energetic"
}
```

**Full workflow**:
```bash
# 1. Submit generation task
POST /api/MediaGeneration/generate-video
POST /api/MediaGeneration/generate-music

# 2. Check task status
GET /api/MediaGeneration/result/{taskId}

# 3. Process video (optional)
POST /api/MediaGeneration/process-video
```

**Media Processing Services**:
- `VideoProcessor` - Mix audio, concatenate, resize, format conversion
- `AudioProcessor` - Extract, trim, adjust volume, mix tracks, normalize
- `FFmpegService` - Low-level FFmpeg operations

## Quick Commands

```bash
# Start Laravel dev server
cd laravel-backend && php artisan serve

# Build C# solution
cd AIManagerCore && dotnet build

# Run C# API
cd AIManagerCore && dotnet run --project src/AIManager.API

# Run tests
php artisan test
dotnet test

# Create migration
php artisan make:migration create_xyz_table

# Bump version
# Use GitHub Actions: Version Bump workflow
```

## Contact & Resources

- **Repository**: PostXAgent
- **Version**: See `VERSION` file
- **Documentation**: `/docs` folder

---

## Session Handoff Notes (Updated: 16 Jan 2026 - Generation Server Improvements)

### Repository Paths

| Type | Path |
|------|------|
| **Main Repository** | `D:/Code/PostXAgent` |
| **Worktrees Directory** | `C:/Users/xman/.claude-worktrees/PostXAgent/` |

**สำคัญ**: ใช้ไดร์ฟ D (`D:/Code/PostXAgent`) เป็นหลักสำหรับการทำงาน

### Current Project State

โปรเจคนี้อยู่ในสถานะ **พร้อมใช้งาน** - CI ผ่านทั้งหมดแล้ว (Version 1.3.0)

### Recent Features Added (Jan 2026)

| Feature | Description | PR/Commit |
|---------|-------------|-----------|
| **🚀 Upscaling (ESRGAN)** | AI image upscaler 2x/3x/4x ด้วย Real-ESRGAN | 16 Jan 2026 |
| **🎨 IP-Adapter** | ใช้รูปเป็น style/content reference สำหรับ generation | 16 Jan 2026 |
| **📋 Queue System** | จัดคิว generation หลายงานพร้อมกัน พร้อม priority support | 16 Jan 2026 |
| **🎯 Multi-ControlNet** | ใช้หลาย ControlNet พร้อมกัน (canny+depth, etc.) | 16 Jan 2026 |
| **🎮 ControlNet Support** | Full ControlNet support - canny, pose, depth, hed, lineart, etc. | 16 Jan 2026 |
| **✨ Progress Endpoint** | `/progress` endpoint สำหรับ poll generation status | 16 Jan 2026 |
| **✨ Cancel Endpoint** | `/cancel` endpoint สำหรับยกเลิก generation กลางคัน | 16 Jan 2026 |
| **✨ New Callback API** | รองรับ `callback_on_step_end` (diffusers ใหม่) + legacy | 16 Jan 2026 |
| **✨ Task ID Tracking** | ทุก generation มี task_id สำหรับ tracking | 16 Jan 2026 |
| **✨ Embedded Resource** | Python script embed ใน assembly + copy to output | 16 Jan 2026 |
| Diffusers img2img Support | เพิ่ม img2img generation พร้อม progress callback, LoRA, scheduler | f7eaa31f |
| LoRA Management | C# API สำหรับ load/unload LoRA adapters | f7eaa31f |
| Video Progress Callback | เพิ่ม progress tracking สำหรับ SVD video generation | f7eaa31f |
| Scheduler Query API | C# API สำหรับ query available schedulers | f7eaa31f |
| CLIP Skip Fix | แก้ไข CLIP skip mutation ให้ reset หลัง generation | f7eaa31f |

### Recent Features Added (Dec 2025)

| Feature | Description | PR |
|---------|-------------|-----|
| Account Pool Management | ระบบจัดการ pool ของ social accounts สำหรับ rotation | #29 |
| AI Web Automation System | ระบบ automation สำหรับ web interactions | #29 |
| Enhanced AI Manager UI | ปรับปรุง WPF UI และ platform workers | #29 |
| Platform Workers | เพิ่ม workers สำหรับทุก platform (FB, IG, TikTok, etc.) | #28 |

### Key Files Location

| Purpose | Path |
|---------|------|
| CI/CD Workflow | `.github/workflows/ci.yml` |
| C# Solution | `AIManagerCore/AIManagerCore.sln` |
| C# Core Library | `AIManagerCore/src/AIManager.Core/` |
| C# WPF UI | `AIManagerCore/src/AIManager.UI/` |
| C# API | `AIManagerCore/src/AIManager.API/` |
| Laravel App | `laravel-backend/` |
| Vue Components | `laravel-backend/resources/js/components/` |
| PHP Composer | `laravel-backend/composer.json` + `composer.lock` |
| NPM Packages | `laravel-backend/package.json` + `package-lock.json` |

### CI/CD Configuration

**File**: `.github/workflows/ci.yml`

```yaml
# Triggers on:
- push to: main, develop, claude/**
# Note: PR checks disabled temporarily - use push checks on claude/** branches

# Jobs (4 total):
1. laravel: Laravel Tests (PHP 8.2, Redis 7)
2. dotnet: .NET Build (Windows, .NET 8.0)
3. lint: Code Quality (PHPStan, PHP-CS-Fixer)
4. security: Security Scan (composer audit)
```

### Laravel Models (Current)

```
app/Models/
├── AccountPool.php        # Pool ของ social accounts
├── AccountPoolMember.php  # Member ใน pool
├── AccountStatusLog.php   # Log การเปลี่ยนสถานะ account
├── BackupCredential.php   # Backup credentials
├── Brand.php              # Brand/แบรนด์
├── Campaign.php           # Campaign/แคมเปญ
├── Post.php               # โพสต์
├── SocialAccount.php      # Social media accounts
└── User.php               # ผู้ใช้
```

### C# Project Files (Current)

```
AIManager.Core/
├── Helpers/
│   └── ErrorClassifier.cs         # จัดประเภท errors
├── Models/
│   ├── Enums.cs                   # Enums (TaskStatus, Platform, etc.)
│   ├── PlatformCredentials.cs     # Credentials model
│   ├── TaskItem.cs                # Task model
│   ├── TaskResult.cs              # Result model
│   └── WorkerInfo.cs              # Worker info model
├── Orchestrator/
│   └── ProcessOrchestrator.cs     # จัดการ process orchestration
├── Services/
│   ├── AIBrainService.cs          # AI Brain สำหรับตัดสินใจ
│   ├── ContentGeneratorService.cs # สร้างเนื้อหา AI
│   ├── CredentialManagerService.cs # จัดการ credentials
│   ├── GroupSearchService.cs      # ค้นหากลุ่ม
│   ├── ImageGeneratorService.cs   # สร้างรูปภาพ AI
│   ├── LoggingService.cs          # Logging
│   ├── PostPublisherService.cs    # โพสต์ไปยัง platforms
│   └── SchedulerService.cs        # จัดตารางเวลา
├── WebAutomation/
│   ├── AIElementAnalyzer.cs       # วิเคราะห์ elements ด้วย AI
│   ├── BrowserController.cs       # ควบคุม browser
│   ├── Models/WorkflowModels.cs   # Workflow models
│   ├── WorkflowExecutor.cs        # รัน workflows
│   ├── WorkflowLearningEngine.cs  # เรียนรู้ workflows
│   └── WorkflowStorage.cs         # เก็บ workflows
└── Workers/
    ├── IPlatformWorker.cs         # Interface
    ├── BasePlatformWorker.cs      # Base class
    ├── FacebookWorker.cs          # Facebook worker
    ├── PlatformWorkers.cs         # All platform workers
    └── WorkerFactory.cs           # Factory pattern

AIManager.API/
├── Controllers/
│   ├── StatusController.cs        # สถานะระบบ
│   ├── TasksController.cs         # จัดการ tasks
│   ├── TestPostController.cs      # ทดสอบโพสต์
│   └── WebAutomationController.cs # Web automation API
├── Hubs/
│   └── AIManagerHub.cs            # SignalR Hub
└── Program.cs                     # Entry point

AIManager.UI/ViewModels/
├── BaseViewModel.cs       # Base MVVM
├── MainViewModel.cs       # Main window
├── DashboardViewModel.cs  # Dashboard
├── TasksViewModel.cs      # Tasks management
├── WorkersViewModel.cs    # Workers status
└── SettingsViewModel.cs   # Settings

AIManager.UI/Views/Pages/
├── AIProvidersPage.xaml   # AI Providers settings
├── DashboardPage.xaml     # Dashboard หลัก
├── LogsPage.xaml          # ดู logs
├── PlatformsPage.xaml     # Platform settings
├── SettingsPage.xaml      # Settings ทั่วไป
├── TasksPage.xaml         # จัดการ tasks
└── WorkersPage.xaml       # ดู workers
```

### Laravel Services (Current)

```
app/Services/
├── AccountRotationService.php     # หมุนเวียน accounts
├── AIManagerClient.php            # Client สำหรับเชื่อมต่อ AI Manager
├── AIManagerConnectionStatus.php  # สถานะการเชื่อมต่อ
└── AIManagerService.php           # Service หลักสำหรับ AI Manager
```

### Laravel Controllers (Current)

```
app/Http/Controllers/Api/
├── AccountPoolController.php      # จัดการ Account Pools
├── AIManagerController.php        # AI Manager operations
├── AIManagerStatusController.php  # สถานะ AI Manager
├── PostController.php             # จัดการโพสต์
└── SubscriptionController.php     # จัดการ subscriptions
```

### CI Fixes Reference (Dec 2025)

รายการปัญหาและวิธีแก้ที่เจอบ่อย:

| Problem | Solution | File |
|---------|----------|------|
| ViewModels namespace not found | สร้าง ViewModels folder + 6 classes | `AIManager.UI/ViewModels/` |
| Missing app.ico | สร้าง placeholder icon 16x16 | `AIManager.UI/Resources/app.ico` |
| NU1605 package downgrade warning | เพิ่ม `<NoWarn>NU1605</NoWarn>` | `AIManager.UI.csproj` |
| TaskStatus ambiguity | ใช้ `Models.TaskStatus` แทน `TaskStatus` | `AIManager.Core/` files |
| AddDebug not found | เปลี่ยนเป็น `AddConsole()` | `Program.cs` |
| PHP 8.4 vs 8.2 conflict | เพิ่ม `config.platform.php: "8.2.29"` | `composer.json` |
| npm cache error | เพิ่ม `cache: 'npm'` + `cache-dependency-path` | `ci.yml` |
| Missing package-lock.json | รัน `npm install` แล้ว commit | `laravel-backend/` |
| Missing composer.lock | รัน `composer update` แล้ว commit | `laravel-backend/` |
| predis version mismatch | Regenerate composer.lock หลัง update | `composer.lock` |

### How to Regenerate Lock Files

```bash
# Composer (PHP) - เมื่อ composer.json เปลี่ยน
cd laravel-backend
rm composer.lock
composer update --no-scripts --ignore-platform-req=ext-bcmath

# NPM - เมื่อ package.json เปลี่ยน
cd laravel-backend
rm package-lock.json
npm install
```

### Git Workflow

- `main` เป็น protected branch - ไม่สามารถ push ตรงได้
- ต้องสร้าง branch แยกแล้วทำ PR เข้า main
- Branch naming: `claude/<description>-<session-id>`
- Active worktree branches: `keen-albattani`, `reverent-pare`, `tender-mahavira`

### C# Project Structure (Full)

```
AIManagerCore/
├── AIManagerCore.sln          # Solution file
└── src/
    ├── AIManager.Core/        # Core library (26 files)
    │   ├── Helpers/           # ErrorClassifier
    │   ├── Models/            # TaskItem, Enums, WorkerInfo, etc.
    │   ├── Orchestrator/      # ProcessOrchestrator
    │   ├── Services/          # 8 services (AI, Content, Scheduler, etc.)
    │   ├── WebAutomation/     # 6 files (Browser, Workflow, AI Analyzer)
    │   └── Workers/           # Platform workers (5 files)
    ├── AIManager.API/         # REST API (ASP.NET Core, 6 files)
    │   ├── Controllers/       # Status, Tasks, TestPost, WebAutomation
    │   ├── Hubs/              # SignalR Hub
    │   └── Program.cs         # Entry point
    └── AIManager.UI/          # WPF Desktop App (16 files)
        ├── ViewModels/        # MVVM ViewModels (6 files)
        ├── Views/Pages/       # 7 XAML Pages
        ├── Converters/        # BoolToColorConverter
        ├── Resources/         # Icons, images
        └── App.xaml           # WPF App entry
```

### Laravel Project Structure (Full)

```
laravel-backend/
├── app/
│   ├── Console/Commands/      # ResetDailyAccountCounters
│   ├── Http/Controllers/Api/  # 5 API Controllers
│   ├── Models/                # 9 Eloquent Models
│   └── Services/              # 4 Business Services
├── config/                    # Configuration files
├── database/migrations/       # Database migrations
├── resources/
│   └── js/components/         # Vue.js components
├── routes/api.php             # API routes
├── composer.json              # PHP dependencies
├── composer.lock              # Locked PHP versions
├── package.json               # NPM dependencies
└── package-lock.json          # Locked NPM versions
```

### Important Notes for New Sessions

1. **ก่อนแก้ไขอะไร** - รัน `git status` และ `git pull origin main` ก่อน
2. **CI ต้องผ่าน** - ทุก PR ต้อง CI ผ่านก่อน merge
3. **Lock files สำคัญ** - ต้อง commit ทั้ง `composer.lock` และ `package-lock.json`
4. **PHP version** - CI ใช้ PHP 8.2 ไม่ใช่ 8.4
5. **Protected main** - ห้าม push ตรงเข้า main
6. **Worktrees** - อาจมีหลาย worktree branches ที่กำลังใช้งาน

### Diffusers Generation Server (Updated: 16 Jan 2026)

**ระบบ Image/Video Generation แบบ production-ready เหมือน ComfyUI**

#### Recent Updates (16 Jan 2026)

| Update | Description |
|--------|-------------|
| ✅ **ControlNet Support** | Full ControlNet - canny, pose, depth, hed, lineart, scribble, etc. |
| ✅ **Inpainting Support** | Mask-based image editing ด้วย SD/SDXL Inpaint models |
| ✅ **Outpainting Support** | ขยาย canvas ได้ทุกทิศทาง พร้อม feathered mask |
| ✅ Auto-preprocessing | ตรวจจับ edges/pose/depth อัตโนมัติด้วย controlnet_aux |
| ✅ Multi-model | รองรับทั้ง SD 1.5 และ SDXL ControlNets |
| ✅ `/progress` endpoint | ดู progress ระหว่าง generation (step, total_steps, percentage) |
| ✅ `/cancel` endpoint | ยกเลิก generation ที่กำลังทำงาน |
| ✅ New callback API | รองรับทั้ง `callback_on_step_end` (diffusers ใหม่) และ `callback` (legacy) |
| ✅ Cancellation support | ใช้ `threading.Event` สำหรับ cancel mid-generation |
| ✅ Task ID tracking | ทุก generation มี task_id สำหรับ tracking |
| ✅ EmbeddedResource | Python script ถูก embed ใน assembly + copy to output |

#### Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                   C# DiffusersGenerationEngine               │
│                      (AIManager.Core)                        │
└─────────────────────┬───────────────────────────────────────┘
                      │ HTTP REST API
                      ▼
┌─────────────────────────────────────────────────────────────┐
│              Python FastAPI Generation Server                │
│                   (generation_server.py)                     │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐         │
│  │  Diffusers  │  │   LoRA      │  │  Scheduler  │         │
│  │   Pipeline  │  │   Adapter   │  │   Manager   │         │
│  └─────────────┘  └─────────────┘  └─────────────┘         │
│                          │                                   │
│              ┌───────────┴───────────┐                      │
│              │   CUDA / GPU Engine   │                      │
│              │  (VRAM Optimization)  │                      │
│              └───────────────────────┘                      │
└─────────────────────────────────────────────────────────────┘
```

#### Key Files

| File | Location | Description |
|------|----------|-------------|
| `generation_server.py` | `AIManager.Core/Services/` | FastAPI Python server (~1000 lines) |
| `DiffusersGenerationEngine.cs` | `AIManager.Core/Services/` | C# wrapper ที่เรียก Python server (~1600 lines) |
| `DiffusersEngineManager.cs` | `AIManager.Core/Services/` | จัดการ engine lifecycle |
| `AutoSetupService.cs` | `AIManager.Core/Services/` | ติดตั้ง Python packages |
| `LocalGpuService.cs` | `AIManager.Core/Services/` | GPU detection (NVIDIA/AMD/Intel) |

#### API Endpoints

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/health` | GET | Health check |
| `/info` | GET | Engine info (model, GPU, VRAM) |
| `/vram` | GET | VRAM usage details |
| `/progress` | GET | Generation progress (step, total, percentage) |
| `/load-model` | POST | Load diffusion model |
| `/unload-model` | POST | Unload model & clear VRAM |
| `/generate/image` | POST | Text-to-Image generation |
| `/generate/img2img` | POST | Image-to-Image generation |
| `/generate/video` | POST | Video generation (SVD) |
| `/generate/controlnet` | POST | ControlNet generation |
| `/generate/multi-controlnet` | POST | **🎯 NEW** - Multi-ControlNet (combine multiple controls) |
| `/generate/inpaint` | POST | Inpainting with mask |
| `/generate/outpaint` | POST | Outpainting/extend canvas |
| `/generate/upscale` | POST | **🚀 NEW** - Real-ESRGAN upscaling (2x/3x/4x) |
| `/generate/ip-adapter` | POST | **🎨 NEW** - IP-Adapter style/content transfer |
| `/controlnet/types` | GET | List available ControlNet types |
| `/controlnet/load` | POST | Pre-load ControlNet model |
| `/controlnet/unload` | POST | Unload all ControlNets |
| `/ip-adapter/load` | POST | **🎨 NEW** - Load IP-Adapter |
| `/ip-adapter/unload` | POST | **🎨 NEW** - Unload IP-Adapter |
| `/lora/load` | POST | Load LoRA adapter |
| `/lora/unload` | POST | Unload LoRAs |
| `/schedulers` | GET | List available schedulers |
| `/queue/add` | POST | **📋 NEW** - Add task to generation queue |
| `/queue/status/{task_id}` | GET | **📋 NEW** - Get queued task status |
| `/queue/cancel/{task_id}` | POST | **📋 NEW** - Cancel queued task |
| `/queue/list` | GET | **📋 NEW** - List all queue tasks |
| `/queue/clear` | POST | **📋 NEW** - Clear pending queue |
| `/cancel` | POST | Cancel current generation |
| `/shutdown` | POST | Graceful shutdown |

#### Supported Schedulers (16+)

```
ddim, ddpm, pndm, euler, euler_a, euler_ancestral,
dpm++_2m, dpm++_2m_karras, dpm++_2s, dpm++_sde, dpm++_sde_karras,
heun, kdpm2, kdpm2_a, lms, unipc
```

#### Supported Models

- Stable Diffusion 1.5, 2.1
- SDXL (Stable Diffusion XL)
- Stable Video Diffusion (SVD)
- FLUX
- Local models + HuggingFace download

#### VRAM Optimizations

- Attention slicing
- VAE slicing
- VAE tiling
- Sequential CPU offload
- Model CPU offload

#### How to Run Server Manually

```bash
# Run server
python generation_server.py --port 5050 --models-dir C:/Models

# With low VRAM mode
python generation_server.py --port 5050 --models-dir C:/Models --low-vram

# Test health
curl http://localhost:5050/health

# Load model
curl -X POST http://localhost:5050/load-model \
  -H "Content-Type: application/json" \
  -d '{"model_id": "stabilityai/stable-diffusion-xl-base-1.0"}'

# Generate image
curl -X POST http://localhost:5050/generate/image \
  -H "Content-Type: application/json" \
  -d '{"prompt": "A beautiful sunset", "width": 1024, "height": 1024}'
```

#### Python Dependencies (Installed by AutoSetupService)

```
torch torchvision torchaudio (with CUDA)
diffusers transformers accelerate safetensors
fastapi uvicorn pydantic
pillow
```

#### C# Script Loading (DiffusersGenerationEngine.cs)

```csharp
// Load priority:
// 1. Embedded resource (AIManager.Core.Services.generation_server.py)
// 2. External file in output directory (generation_server.py)
// 3. Fallback minimal script (GenerateMinimalScript())
```

#### ControlNet Types (NEW)

**SD 1.5 ControlNets:**
| Type | Model | Description |
|------|-------|-------------|
| `canny` | lllyasviel/control_v11p_sd15_canny | Edge detection |
| `pose` | lllyasviel/control_v11p_sd15_openpose | Human pose |
| `depth` | lllyasviel/control_v11f1p_sd15_depth | Depth map |
| `hed` | lllyasviel/control_v11p_sd15_softedge | HED edge detection |
| `lineart` | lllyasviel/control_v11p_sd15_lineart | Line art |
| `scribble` | lllyasviel/control_v11p_sd15_scribble | Scribble/doodle |
| `softedge` | lllyasviel/control_v11p_sd15_softedge | Soft edges |
| `normal` | lllyasviel/control_v11p_sd15_normalbae | Normal map |
| `tile` | lllyasviel/control_v11f1e_sd15_tile | Tile/upscale |

**SDXL ControlNets:**
| Type | Model | Description |
|------|-------|-------------|
| `canny` | diffusers/controlnet-canny-sdxl-1.0 | Edge detection |
| `depth` | diffusers/controlnet-depth-sdxl-1.0 | Depth map |
| `pose` | thibaud/controlnet-openpose-sdxl-1.0 | Human pose |

#### ControlNet Usage Example

```bash
# Generate with ControlNet (auto-preprocess edges from image)
curl -X POST http://localhost:5050/generate/controlnet \
  -H "Content-Type: application/json" \
  -d '{
    "prompt": "A beautiful woman in a red dress",
    "control_image": "data:image/png;base64,<BASE64_IMAGE>",
    "control_type": "canny",
    "preprocess": true,
    "controlnet_conditioning_scale": 1.0,
    "width": 1024,
    "height": 1024
  }'

# Get available ControlNet types
curl http://localhost:5050/controlnet/types

# Pre-load ControlNet model
curl -X POST "http://localhost:5050/controlnet/load?control_type=canny"
```

#### C# ControlNet Example

```csharp
// Generate with ControlNet
var request = new DiffusersControlNetRequest
{
    Prompt = "A beautiful landscape",
    ControlImage = controlImageBase64,
    ControlType = "canny",  // or "pose", "depth", "lineart", etc.
    Preprocess = true,      // auto-detect edges
    ControlNetConditioningScale = 1.0,
    Width = 1024,
    Height = 1024
};

var result = await engine.GenerateControlNetAsync(request);
// result.Images = generated images
// result.ControlPreview = preprocessed control image (for debugging)
```

#### Inpainting/Outpainting Support (NEW)

**Inpainting** - แก้ไขส่วนที่เลือกของรูปด้วย mask

```bash
# Inpainting request
curl -X POST http://localhost:5050/generate/inpaint \
  -H "Content-Type: application/json" \
  -d '{
    "prompt": "A beautiful red rose",
    "image": "data:image/png;base64,<BASE64_IMAGE>",
    "mask": "data:image/png;base64,<BASE64_MASK>",
    "strength": 0.99,
    "guidance_scale": 7.5,
    "steps": 30
  }'
```

**Mask Format:**
- **White (255)** = บริเวณที่ต้องการ inpaint
- **Black (0)** = บริเวณที่ต้องการเก็บไว้

**Outpainting** - ขยาย canvas ของรูปออก

```bash
# Outpaint to the right
curl -X POST http://localhost:5050/generate/outpaint \
  -H "Content-Type: application/json" \
  -d '{
    "prompt": "A beautiful landscape with mountains",
    "image": "data:image/png;base64,<BASE64_IMAGE>",
    "direction": "right",
    "extend_pixels": 256,
    "feather_pixels": 32,
    "strength": 0.85
  }'

# Outpaint multiple directions
curl -X POST http://localhost:5050/generate/outpaint \
  -d '{
    "prompt": "Continuation of the scene",
    "image": "...",
    "direction": "left,bottom",
    "extend_pixels": 128
  }'
```

**Outpaint Directions:**
- `left`, `right`, `top`, `bottom`
- ผสมได้: `"left,top"`, `"right,bottom"`, etc.

**C# Inpainting Example:**

```csharp
// Inpainting
var request = new DiffusersInpaintRequest
{
    Prompt = "A cute cat sitting on the chair",
    NegativePrompt = "blurry, low quality",
    Image = imageBase64,      // รูปต้นฉบับ
    Mask = maskBase64,        // mask (white = inpaint area)
    Width = 1024,
    Height = 1024,
    Steps = 30,
    GuidanceScale = 7.5,
    Strength = 0.99           // 0.99 = เกือบ replace ทั้งหมด
};

var result = await engine.GenerateInpaintAsync(request);
// result.Images = inpainted images
// result.OriginalSize = ขนาดรูปต้นฉบับ
```

**C# Outpainting Example:**

```csharp
// Outpainting (extend canvas)
var request = new DiffusersOutpaintRequest
{
    Prompt = "A beautiful forest landscape",
    Image = imageBase64,
    Direction = "right",        // ทิศที่ต้องการขยาย
    ExtendPixels = 256,         // ขยายออกกี่ pixel
    FeatherPixels = 32,         // ความนุ่มของขอบ
    Strength = 0.85,
    Steps = 30
};

var result = await engine.GenerateOutpaintAsync(request);
// result.Images = extended images
// result.NewSize = ขนาดรูปใหม่หลังขยาย
// result.OriginalSize = ขนาดต้นฉบับ
```

#### Upscaling (Real-ESRGAN) - NEW

```bash
# Upscale image 4x
curl -X POST http://localhost:5050/generate/upscale \
  -H "Content-Type: application/json" \
  -d '{
    "image": "data:image/png;base64,<BASE64_IMAGE>",
    "scale": 4,
    "model": "realesrgan"
  }'
```

**Available Upscaler Models:**
- `realesrgan` - General purpose 4x upscaler
- `realesrgan-anime` - Optimized for anime/illustrations
- `realesrgan-x2` - 2x upscaler (faster, smaller output)

**C# Example:**

```csharp
var request = new DiffusersUpscaleRequest
{
    Image = imageBase64,
    Scale = 4,                    // 2x, 3x, or 4x
    Model = "realesrgan",         // or "realesrgan-anime"
    OutputFormat = "png"
};

var result = await engine.GenerateUpscaleAsync(request);
// result.Images = upscaled images
// result.OriginalSize = original dimensions
// result.OutputSize = new dimensions after upscaling
```

**Requires:** `pip install realesrgan basicsr`

#### IP-Adapter (Style/Content Transfer) - NEW

Use reference images to guide the generation style/content.

```bash
# Generate with IP-Adapter
curl -X POST http://localhost:5050/generate/ip-adapter \
  -H "Content-Type: application/json" \
  -d '{
    "prompt": "A portrait in similar style",
    "reference_images": ["data:image/png;base64,<REF_IMAGE>"],
    "ip_adapter_scale": 0.6,
    "width": 1024,
    "height": 1024
  }'
```

**IP-Adapter Scale:**
- `0.0` = No influence (just prompt)
- `0.6` = Balanced (default, recommended)
- `1.0+` = Strong reference influence

**C# Example:**

```csharp
var request = new DiffusersIPAdapterRequest
{
    Prompt = "A portrait of a woman in office",
    ReferenceImages = new List<string> { refImageBase64 },
    IPAdapterScale = 0.6,
    Width = 1024,
    Height = 1024,
    Steps = 30
};

var result = await engine.GenerateIPAdapterAsync(request);
// result.Images = generated images
// result.ReferenceCount = number of reference images used
```

#### Multi-ControlNet - NEW

Combine multiple ControlNet conditions simultaneously.

```bash
# Multi-ControlNet (canny + depth)
curl -X POST http://localhost:5050/generate/multi-controlnet \
  -H "Content-Type: application/json" \
  -d '{
    "prompt": "A beautiful landscape",
    "controls": [
      {"control_type": "canny", "control_image": "base64...", "weight": 1.0},
      {"control_type": "depth", "control_image": "base64...", "weight": 0.8}
    ],
    "width": 1024,
    "height": 1024
  }'
```

**C# Example:**

```csharp
var request = new DiffusersMultiControlNetRequest
{
    Prompt = "A beautiful landscape",
    Controls = new List<ControlCondition>
    {
        new() { ControlType = "canny", ControlImage = cannyImageBase64, Weight = 1.0 },
        new() { ControlType = "depth", ControlImage = depthImageBase64, Weight = 0.8 }
    },
    Width = 1024,
    Height = 1024
};

var result = await engine.GenerateMultiControlNetAsync(request);
// result.Images = generated images
// result.ControlTypes = ["canny", "depth"]
// result.ControlScales = [1.0, 0.8]
```

#### Queue System - NEW

Submit multiple generation tasks and process them in order.

```bash
# Add task to queue
curl -X POST http://localhost:5050/queue/add \
  -H "Content-Type: application/json" \
  -d '{
    "task_type": "image",
    "request_data": {"prompt": "A cat", "width": 1024, "height": 1024},
    "priority": 5
  }'

# Check task status
curl http://localhost:5050/queue/status/{task_id}

# List all queue tasks
curl http://localhost:5050/queue/list

# Cancel a task
curl -X POST http://localhost:5050/queue/cancel/{task_id}

# Clear pending queue
curl -X POST http://localhost:5050/queue/clear
```

**Task Types:**
- `image`, `img2img`, `video`, `controlnet`, `multi_controlnet`
- `inpaint`, `outpaint`, `upscale`, `ip_adapter`

**Priority:** 0-10 (higher = processed first)

**C# Example:**

```csharp
// Add task to queue
var addResult = await engine.QueueAddTaskAsync(new QueuedTaskRequest
{
    TaskType = "image",
    RequestData = new Dictionary<string, object>
    {
        ["prompt"] = "A beautiful sunset",
        ["width"] = 1024,
        ["height"] = 1024
    },
    Priority = 5
});
// addResult.TaskId = unique task ID
// addResult.Position = position in queue

// Check status
var status = await engine.QueueGetStatusAsync(addResult.TaskId!);
// status.Status = "pending" / "processing" / "completed" / "failed"
// status.Progress = 0-100
// status.Result = generation result when completed

// List queue
var list = await engine.QueueListAsync();
// list.Pending = pending tasks
// list.Processing = currently processing
// list.RecentCompleted = last 10 completed

// Cancel task
await engine.QueueCancelTaskAsync(taskId);

// Clear pending
await engine.QueueClearAsync();
```

#### Important Notes

1. **Python script ถูก embed + copy** - ทั้ง `<EmbeddedResource>` และ `<Content>` ใน `.csproj`
2. **ต้องมี CUDA** - ถ้าไม่มี GPU จะใช้ CPU (ช้ามาก)
3. **LoRA support** - Load/Unload ได้หลายตัว, ตั้ง weight ได้
4. **Progress callback** - รายงาน progress ระหว่าง generation
5. **Cancellation** - สามารถยกเลิก generation กลางคันได้ผ่าน `/cancel` endpoint
6. **Backward compatible** - รองรับทั้ง diffusers API เก่าและใหม่
7. **ControlNet** - รองรับทั้ง SD 1.5 และ SDXL, auto-detect model family
8. **Auto-preprocessing** - ใช้ controlnet_aux สำหรับ detect edges/pose/depth อัตโนมัติ
9. **Inpainting** - รองรับ mask-based image editing ด้วย SD 1.5/SDXL Inpaint models
10. **Outpainting** - ขยาย canvas ได้ทุกทิศทาง พร้อม feathered mask สำหรับ smooth blending

#### C# Methods สำหรับ Progress & Cancel

```csharp
// Get generation progress
var progress = await engine.GetGenerationProgressAsync();
// Returns: { IsGenerating, TaskId, Step, TotalSteps, Progress }

// Cancel current generation
var cancelled = await engine.CancelGenerationAsync();
// Returns: true if cancellation requested

// Response models
public class GenerationProgressInfo
{
    public bool IsGenerating { get; set; }
    public string? TaskId { get; set; }
    public int Step { get; set; }
    public int TotalSteps { get; set; }
    public int Progress { get; set; }  // 0-100%
}

public class DiffusersResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public bool Cancelled { get; set; }  // true if cancelled by user
    public string? TaskId { get; set; }
    public List<string>? Images { get; set; }
    public List<string>? Frames { get; set; }
    public double VramUsedGb { get; set; }
    // ...
}
```

---

### Useful Commands

```bash
# Check CI status
git log --oneline -5

# Build C# locally
cd AIManagerCore && dotnet build

# Test Laravel locally
cd laravel-backend && php artisan test

# Build Vue locally
cd laravel-backend && npm run build

# Create new branch for fixes
git checkout -b claude/<description>-<session-id>
git push -u origin claude/<description>-<session-id>

# List worktrees
git worktree list

# Test Diffusers Server
cd AIManagerCore/src/AIManager.Core/bin/Release/net8.0
python generation_server.py --port 5050 --models-dir C:/Models
```
