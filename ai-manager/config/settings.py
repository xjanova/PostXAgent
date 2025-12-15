"""
AI Manager Configuration Settings
"""
import os
from typing import Dict, List, Optional
from dataclasses import dataclass, field
from enum import Enum


class AIProvider(Enum):
    """Supported AI providers"""
    OPENAI = "openai"
    ANTHROPIC = "anthropic"
    GOOGLE = "google"
    OLLAMA = "ollama"  # Free - Local
    STABLE_DIFFUSION = "stable_diffusion"  # Free - Self-hosted
    DALLE = "dalle"
    MIDJOURNEY = "midjourney"
    LEONARDO = "leonardo"  # Free tier
    BING_IMAGE = "bing_image"  # Free


class SocialPlatform(Enum):
    """Supported social media platforms in Thailand"""
    FACEBOOK = "facebook"
    INSTAGRAM = "instagram"
    TIKTOK = "tiktok"
    TWITTER = "twitter"
    LINE = "line"
    YOUTUBE = "youtube"
    THREADS = "threads"
    LINKEDIN = "linkedin"
    PINTEREST = "pinterest"


@dataclass
class RedisConfig:
    """Redis configuration"""
    host: str = os.getenv("REDIS_HOST", "localhost")
    port: int = int(os.getenv("REDIS_PORT", 6379))
    password: str = os.getenv("REDIS_PASSWORD", "")
    db: int = int(os.getenv("REDIS_DB", 0))

    @property
    def url(self) -> str:
        if self.password:
            return f"redis://:{self.password}@{self.host}:{self.port}/{self.db}"
        return f"redis://{self.host}:{self.port}/{self.db}"


@dataclass
class DatabaseConfig:
    """PostgreSQL configuration"""
    host: str = os.getenv("DB_HOST", "localhost")
    port: int = int(os.getenv("DB_PORT", 5432))
    database: str = os.getenv("DB_DATABASE", "postxagent")
    username: str = os.getenv("DB_USERNAME", "postgres")
    password: str = os.getenv("DB_PASSWORD", "")

    @property
    def url(self) -> str:
        return f"postgresql://{self.username}:{self.password}@{self.host}:{self.port}/{self.database}"


@dataclass
class AIServiceConfig:
    """AI service configurations"""
    # OpenAI
    openai_api_key: str = os.getenv("OPENAI_API_KEY", "")
    openai_model: str = os.getenv("OPENAI_MODEL", "gpt-4-turbo-preview")

    # Anthropic Claude
    anthropic_api_key: str = os.getenv("ANTHROPIC_API_KEY", "")
    anthropic_model: str = os.getenv("ANTHROPIC_MODEL", "claude-3-opus-20240229")

    # Google Gemini (Free tier available)
    google_api_key: str = os.getenv("GOOGLE_API_KEY", "")
    google_model: str = os.getenv("GOOGLE_MODEL", "gemini-pro")

    # Ollama (Free - Local)
    ollama_base_url: str = os.getenv("OLLAMA_BASE_URL", "http://localhost:11434")
    ollama_model: str = os.getenv("OLLAMA_MODEL", "llama2")

    # Stable Diffusion (Free - Self-hosted)
    sd_api_url: str = os.getenv("SD_API_URL", "http://localhost:7860")

    # DALL-E
    dalle_model: str = os.getenv("DALLE_MODEL", "dall-e-3")

    # Leonardo.ai (Free tier)
    leonardo_api_key: str = os.getenv("LEONARDO_API_KEY", "")


@dataclass
class WorkerConfig:
    """Worker process configuration"""
    # Number of CPU cores to use (0 = auto-detect all cores)
    num_cores: int = int(os.getenv("NUM_CORES", 0))

    # Maximum workers per platform
    max_workers_per_platform: int = int(os.getenv("MAX_WORKERS_PER_PLATFORM", 5))

    # Task timeout in seconds
    task_timeout: int = int(os.getenv("TASK_TIMEOUT", 300))

    # Health check interval in seconds
    health_check_interval: int = int(os.getenv("HEALTH_CHECK_INTERVAL", 30))

    # Queue polling interval in milliseconds
    queue_poll_interval: int = int(os.getenv("QUEUE_POLL_INTERVAL", 100))

    # Maximum retries for failed tasks
    max_retries: int = int(os.getenv("MAX_RETRIES", 3))

    # Retry delay in seconds (exponential backoff base)
    retry_delay_base: int = int(os.getenv("RETRY_DELAY_BASE", 5))


@dataclass
class SocialMediaConfig:
    """Social media platform API configurations"""
    # Facebook
    facebook_app_id: str = os.getenv("FACEBOOK_APP_ID", "")
    facebook_app_secret: str = os.getenv("FACEBOOK_APP_SECRET", "")

    # Instagram (uses Facebook Graph API)
    instagram_business_id: str = os.getenv("INSTAGRAM_BUSINESS_ID", "")

    # TikTok
    tiktok_client_key: str = os.getenv("TIKTOK_CLIENT_KEY", "")
    tiktok_client_secret: str = os.getenv("TIKTOK_CLIENT_SECRET", "")

    # Twitter/X
    twitter_api_key: str = os.getenv("TWITTER_API_KEY", "")
    twitter_api_secret: str = os.getenv("TWITTER_API_SECRET", "")
    twitter_bearer_token: str = os.getenv("TWITTER_BEARER_TOKEN", "")

    # LINE Official Account
    line_channel_id: str = os.getenv("LINE_CHANNEL_ID", "")
    line_channel_secret: str = os.getenv("LINE_CHANNEL_SECRET", "")
    line_channel_access_token: str = os.getenv("LINE_CHANNEL_ACCESS_TOKEN", "")

    # YouTube
    youtube_api_key: str = os.getenv("YOUTUBE_API_KEY", "")
    youtube_client_id: str = os.getenv("YOUTUBE_CLIENT_ID", "")
    youtube_client_secret: str = os.getenv("YOUTUBE_CLIENT_SECRET", "")

    # Threads (uses Instagram API)

    # LinkedIn
    linkedin_client_id: str = os.getenv("LINKEDIN_CLIENT_ID", "")
    linkedin_client_secret: str = os.getenv("LINKEDIN_CLIENT_SECRET", "")

    # Pinterest
    pinterest_app_id: str = os.getenv("PINTEREST_APP_ID", "")
    pinterest_app_secret: str = os.getenv("PINTEREST_APP_SECRET", "")


@dataclass
class Settings:
    """Main settings class"""
    # Application
    app_name: str = "PostXAgent"
    app_env: str = os.getenv("APP_ENV", "development")
    debug: bool = os.getenv("DEBUG", "true").lower() == "true"
    log_level: str = os.getenv("LOG_LEVEL", "INFO")

    # Sub-configurations
    redis: RedisConfig = field(default_factory=RedisConfig)
    database: DatabaseConfig = field(default_factory=DatabaseConfig)
    ai_service: AIServiceConfig = field(default_factory=AIServiceConfig)
    worker: WorkerConfig = field(default_factory=WorkerConfig)
    social_media: SocialMediaConfig = field(default_factory=SocialMediaConfig)

    # Laravel API
    laravel_api_url: str = os.getenv("LARAVEL_API_URL", "http://localhost:8000/api")
    laravel_api_key: str = os.getenv("LARAVEL_API_KEY", "")

    @classmethod
    def load(cls) -> "Settings":
        """Load settings from environment"""
        return cls()


# Global settings instance
settings = Settings.load()
