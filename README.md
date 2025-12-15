# PostXAgent - AI Brand Promotion Manager

## Overview

PostXAgent is a comprehensive AI-powered brand promotion management system that automates content creation, image generation, and social media posting across all major platforms in Thailand.

## Architecture

```
PostXAgent/
├── ai-manager/              # AI Manager Core (Python - Multi-process Engine)
│   ├── core/               # Core orchestration logic
│   ├── workers/            # Platform-specific AI workers
│   ├── services/           # AI services (OpenAI, Claude, Stable Diffusion)
│   ├── utils/              # Utility functions
│   └── config/             # Configuration files
├── laravel-backend/        # Laravel API & Admin Panel
├── docker/                 # Docker deployment configs
├── docs/                   # Documentation
└── scripts/               # Deployment & maintenance scripts
```

## Key Features

### 1. AI Manager Core (40+ CPU Cores Utilization)
- Multi-process architecture using Python multiprocessing
- Process pool for parallel task execution
- Queue-based task distribution (Redis/RabbitMQ)
- Health monitoring and auto-recovery
- Load balancing across CPU cores

### 2. Supported Social Media Platforms (Thailand)
- Facebook (Pages, Groups, Marketplace)
- Instagram (Feed, Stories, Reels)
- TikTok (Videos, Lives)
- Twitter/X (Posts, Threads)
- LINE Official Account (Broadcast, Rich Menu)
- YouTube (Videos, Shorts, Community)
- Threads (Posts)
- LinkedIn (Posts, Articles)
- Pinterest (Pins, Boards)

### 3. AI Content Generation
- **Text Generation**
  - OpenAI GPT-4 (Paid)
  - Anthropic Claude (Paid)
  - Google Gemini (Free tier available)
  - Ollama/Local LLMs (Free)

- **Image Generation**
  - DALL-E 3 (Paid)
  - Midjourney (Paid)
  - Stable Diffusion (Free - Self-hosted)
  - Leonardo.ai (Free tier)
  - Bing Image Creator (Free)

### 4. Billing & Subscription (Stripe)
- Multiple pricing tiers
- Usage-based billing
- Automated invoicing
- Payment webhooks

## System Requirements

- **CPU**: 40+ cores recommended
- **RAM**: 64GB+ recommended
- **Storage**: 500GB+ SSD
- **OS**: Ubuntu 22.04 LTS
- **Python**: 3.11+
- **PHP**: 8.2+
- **Node.js**: 20 LTS
- **Redis**: 7+
- **PostgreSQL**: 15+

## Quick Start

```bash
# Clone repository
git clone https://github.com/your-org/postxagent.git
cd postxagent

# Setup AI Manager
cd ai-manager
python -m venv venv
source venv/bin/activate
pip install -r requirements.txt

# Setup Laravel Backend
cd ../laravel-backend
composer install
cp .env.example .env
php artisan key:generate
php artisan migrate

# Start services with Docker
docker-compose up -d
```

## Environment Variables

See `.env.example` for all required environment variables.

## License

Proprietary - All rights reserved
