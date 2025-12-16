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

// Use type hints
public function processTask(TaskItem $task): TaskResult

// Use Laravel conventions
// Controllers: PascalCase + Controller suffix
// Models: PascalCase singular
// Tables: snake_case plural
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

## Session Handoff Notes (Updated: 16 Dec 2025)

### Repository Paths

| Type | Path |
|------|------|
| **Main Repository** | `D:/Code/PostXAgent` |
| **Worktrees Directory** | `C:/Users/xman/.claude-worktrees/PostXAgent/` |

**สำคัญ**: ใช้ไดร์ฟ D (`D:/Code/PostXAgent`) เป็นหลักสำหรับการทำงาน

### Current Project State

โปรเจคนี้อยู่ในสถานะ **พร้อมใช้งาน** - CI ผ่านทั้งหมดแล้ว (Version 1.0.0)

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
```
