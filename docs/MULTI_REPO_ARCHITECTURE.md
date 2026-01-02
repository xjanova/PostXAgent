# PostXAgent Multi-Repository Architecture

## Overview

PostXAgent ecosystem ประกอบด้วยหลาย repositories ที่ทำงานร่วมกัน เอกสารนี้อธิบายความสัมพันธ์ระหว่าง repos และวิธีการ integrate

## Repository Map

```
┌─────────────────────────────────────────────────────────────────────────┐
│                        PostXAgent Ecosystem                              │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│   ┌───────────────────────┐      ┌───────────────────────┐              │
│   │   xmanstudio          │      │   xmanstudio-rental   │              │
│   │   (Main License Hub)  │◄────►│   (Rental Backend)    │              │
│   │   github.com/xjanova  │      │   D:\Code\xmanstudio  │              │
│   └───────────┬───────────┘      └───────────┬───────────┘              │
│               │                              │                           │
│               │ License API                  │ Admin API                 │
│               ▼                              ▼                           │
│   ┌─────────────────────────────────────────────────────────────────┐   │
│   │                      PostXAgent                                  │   │
│   │                   D:\Code\PostXAgent                             │   │
│   │  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐  │   │
│   │  │  laravel-backend │  │ AIManagerCore   │  │  MyPostXAgent   │  │   │
│   │  │  (Web Panel)     │  │ (C# API/UI)     │  │  (Desktop App)  │  │   │
│   │  └────────┬─────────┘  └────────┬────────┘  └────────┬────────┘  │   │
│   │           │                     │                     │           │   │
│   │           └─────────────────────┴─────────────────────┘           │   │
│   │                          │                                        │   │
│   └──────────────────────────┼────────────────────────────────────────┘   │
│                              │                                            │
│                              ▼                                            │
│                    Social Media Platforms                                 │
│            (Facebook, Instagram, TikTok, etc.)                           │
│                                                                          │
└─────────────────────────────────────────────────────────────────────────┘
```

## Repositories

### 1. PostXAgent (Main Repository)
- **Location**: `D:\Code\PostXAgent`
- **GitHub**: `github.com/xjanova/PostXAgent`
- **Purpose**: AI-powered Brand Promotion Manager

| Component | Path | Technology | Port |
|-----------|------|------------|------|
| Laravel Backend | `laravel-backend/` | PHP 8.2, Laravel 11 | 8000 |
| AIManagerCore API | `AIManagerCore/src/AIManager.API/` | .NET 8 | 5000-5002 |
| AIManagerCore UI | `AIManagerCore/src/AIManager.UI/` | WPF | - |
| MyPostXAgent | `MyPostXAgent/` | WPF, .NET 8 | - |

### 2. xmanstudio (License Hub)
- **Location**: External
- **GitHub**: `github.com/xjanova/xmanstudio`
- **Purpose**: Central license management system

| Component | Description |
|-----------|-------------|
| License API | RESTful API for license validation |
| Admin Panel | Web interface for license management |
| Product Management | Manage products and license keys |

### 3. xmanstudio-rental (Rental Backend)
- **Location**: `D:\Code\xmanstudio-rental`
- **Purpose**: Backend rental system for AI Core Manager

| Component | Path | Description |
|-----------|------|-------------|
| License API Controller | `app/Http/Controllers/Api/LicenseApiController.php` | License validation endpoints |
| License Service | `app/Services/LicenseService.php` | License business logic |
| License Key Model | `app/Models/LicenseKey.php` | License data model |

## API Integration

### License API (xmanstudio)

Base URL: `https://api.xmanstudio.com` (production) or configured in `.env`

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/v1/license/activate` | POST | Activate license on machine |
| `/api/v1/license/validate` | POST | Validate license status |
| `/api/v1/license/deactivate` | POST | Deactivate license |
| `/api/v1/license/status/{key}` | GET | Get license status |
| `/api/v1/license/demo` | POST | Start demo license |
| `/api/v1/license/demo/check` | POST | Check demo eligibility |

### Request Format

```json
// Activate License
POST /api/v1/license/activate
{
    "license_key": "XXXX-XXXX-XXXX-XXXX",
    "machine_id": "unique-machine-identifier",
    "machine_fingerprint": "hardware-fingerprint-hash",
    "app_version": "1.0.0"
}

// Validate License
POST /api/v1/license/validate
{
    "license_key": "XXXX-XXXX-XXXX-XXXX",
    "machine_id": "unique-machine-identifier"
}

// Start Demo
POST /api/v1/license/demo
{
    "machine_id": "unique-machine-identifier",
    "machine_fingerprint": "hardware-fingerprint-hash"
}
```

### Response Format

```json
// Success Response
{
    "success": true,
    "data": {
        "license_key": "XXXX-XXXX-XXXX-XXXX",
        "type": "monthly",
        "expires_at": "2025-02-01T00:00:00Z",
        "days_remaining": 30
    },
    "message": "License activated successfully"
}

// Error Response
{
    "success": false,
    "error": "License key invalid",
    "code": "INVALID_KEY"
}
```

## License Types

| Type | Duration | Description |
|------|----------|-------------|
| `demo` | 3 days | Trial license |
| `monthly` | 30 days | Monthly subscription |
| `yearly` | 365 days | Annual subscription |
| `lifetime` | Unlimited | Permanent license |
| `product` | Varies | Per-product license |

## Configuration

### PostXAgent `.env`

```env
# License API Configuration
LICENSE_API_URL=https://api.xmanstudio.com
LICENSE_API_VERSION=v1

# For development/testing
LICENSE_API_URL=http://localhost:8001
```

### MyPostXAgent (C#)

Configuration in `LicenseService.cs`:

```csharp
public LicenseService(
    HttpClient httpClient,
    MachineIdGenerator machineIdGenerator,
    string apiBaseUrl = "https://api.postxagent.com", // Change to xmanstudio URL
    string appVersion = "1.0.0",
    ILogger<LicenseService>? logger = null)
```

## Data Flow

### License Activation Flow

```
┌─────────────┐     ┌─────────────────┐     ┌─────────────────┐
│ MyPostXAgent │────►│ xmanstudio API  │────►│ License Database│
│ (Desktop)    │     │ (License Hub)   │     │                 │
└─────────────┘     └─────────────────┘     └─────────────────┘
       │                    │                        │
       │ 1. Activate        │ 2. Validate Key        │
       │    Request         │    Check Machine       │
       │                    │                        │
       │                    │ 3. Store Activation    │
       │ 4. Return Result   │◄───────────────────────┘
       │◄───────────────────┘
       │
       ▼
┌─────────────┐
│ Local Cache │
│ (Offline)   │
└─────────────┘
```

### Posting Flow

```
┌─────────────┐     ┌─────────────────┐     ┌─────────────────┐
│ Laravel Web │────►│ AIManagerCore   │────►│ Platform Workers│
│ Panel       │     │ (C# Backend)    │     │ (FB, IG, etc.)  │
└─────────────┘     └─────────────────┘     └─────────────────┘
       │                    │                        │
       │ 1. Create Post     │ 2. Queue Task          │
       │    Schedule        │    Process             │
       │                    │                        │
       │                    │ 3. Execute Post        │
       │ 4. Return Status   │◄───────────────────────┘
       │◄───────────────────┘
```

## Development Setup

### 1. Clone All Repositories

```bash
# Main repository
git clone https://github.com/xjanova/PostXAgent.git D:/Code/PostXAgent

# License system (for reference)
git clone https://github.com/xjanova/xmanstudio.git D:/Code/xmanstudio

# Rental backend (if developing license features)
# Already at D:/Code/xmanstudio-rental
```

### 2. Configure Environment

```bash
# PostXAgent Laravel
cd D:/Code/PostXAgent/laravel-backend
cp .env.example .env
# Edit LICENSE_API_URL

# AIManagerCore
cd D:/Code/PostXAgent/AIManagerCore
# Edit appsettings.json for API configuration
```

### 3. Build All Components

```bash
# Laravel Backend
cd D:/Code/PostXAgent/laravel-backend
composer install
npm install && npm run build
php artisan migrate

# AIManagerCore
cd D:/Code/PostXAgent/AIManagerCore
dotnet build

# MyPostXAgent
cd D:/Code/PostXAgent/MyPostXAgent
dotnet build
```

## Migration Notes

### From Internal to xmanstudio License System

As of January 2025, PostXAgent migrated from internal rental/license system to xmanstudio:

**Removed from PostXAgent:**
- All rental-related models, controllers, services
- Payment gateway integration
- License generation endpoints
- Rental scheduled commands

**Now handled by xmanstudio:**
- License key generation
- License activation/validation
- Demo management
- Payment processing (via xmanstudio admin)

**API Endpoint Changes:**
| Old Endpoint | New Endpoint |
|--------------|--------------|
| `/api/v1/license/activate-demo` | `/api/v1/license/demo` |
| `/api/v1/license/check-demo` | `/api/v1/license/demo/check` |
| `machine_hash` parameter | `machine_fingerprint` parameter |

## Troubleshooting

### License Validation Fails

1. Check API URL in configuration
2. Verify machine_id is consistent
3. Check network connectivity to xmanstudio
4. Review license status in xmanstudio admin

### Offline Mode

MyPostXAgent supports offline validation:
- Caches last successful validation
- Allows usage if license was valid within 7 days
- Requires online validation after grace period

## Related Documentation

- [PostXAgent CLAUDE.md](../CLAUDE.md) - Main development guidelines
- [xmanstudio README](https://github.com/xjanova/xmanstudio) - License hub documentation
- [API Testing Guide](../AIManagerCore/docs/API_TESTING.md) - API testing procedures

## Contact

For questions about multi-repo integration:
- Create issue in respective repository
- Tag with `multi-repo` label
