"""
Base Worker Class for Social Media Platforms
"""
from abc import ABC, abstractmethod
from typing import Dict, Any, Optional, List
from dataclasses import dataclass
from datetime import datetime
import logging
import asyncio

logger = logging.getLogger(__name__)


@dataclass
class PostContent:
    """Content ready for posting"""
    text: str
    images: List[str] = None  # URLs or file paths
    videos: List[str] = None
    hashtags: List[str] = None
    mentions: List[str] = None
    link: Optional[str] = None
    location: Optional[str] = None
    scheduled_at: Optional[datetime] = None

    def __post_init__(self):
        self.images = self.images or []
        self.videos = self.videos or []
        self.hashtags = self.hashtags or []
        self.mentions = self.mentions or []


@dataclass
class EngagementMetrics:
    """Engagement metrics for a post"""
    post_id: str
    likes: int = 0
    comments: int = 0
    shares: int = 0
    views: int = 0
    clicks: int = 0
    saves: int = 0
    reach: int = 0
    impressions: int = 0
    engagement_rate: float = 0.0
    retrieved_at: datetime = None

    def __post_init__(self):
        if self.retrieved_at is None:
            self.retrieved_at = datetime.utcnow()


class BasePlatformWorker(ABC):
    """
    Abstract base class for platform-specific workers
    Each platform (Facebook, Instagram, etc.) extends this
    """

    def __init__(self, platform_name: str):
        self.platform_name = platform_name
        self.logger = logging.getLogger(f"worker.{platform_name}")
        self._authenticated = False
        self._rate_limit_remaining = 100
        self._rate_limit_reset = None

    @abstractmethod
    async def authenticate(self, credentials: Dict) -> bool:
        """Authenticate with the platform API"""
        pass

    @abstractmethod
    async def post_content(self, content: PostContent) -> Dict:
        """Post content to the platform"""
        pass

    @abstractmethod
    async def schedule_post(self, content: PostContent, scheduled_at: datetime) -> Dict:
        """Schedule a post for later"""
        pass

    @abstractmethod
    async def get_metrics(self, post_id: str) -> EngagementMetrics:
        """Get engagement metrics for a post"""
        pass

    @abstractmethod
    async def delete_post(self, post_id: str) -> bool:
        """Delete a post"""
        pass

    # Shared methods for all platforms

    def generate_content(self, task: Any) -> Dict:
        """Generate content using AI services"""
        from services.content_generator import ContentGenerator

        generator = ContentGenerator()
        prompt = task.payload.get("prompt", "")
        brand_info = task.payload.get("brand_info", {})
        content_type = task.payload.get("content_type", "promotional")
        language = task.payload.get("language", "th")  # Thai as default

        try:
            content = asyncio.run(generator.generate_text(
                prompt=prompt,
                brand_info=brand_info,
                platform=self.platform_name,
                content_type=content_type,
                language=language,
            ))

            return {
                "success": True,
                "content": content,
                "platform": self.platform_name,
            }

        except Exception as e:
            self.logger.error(f"Content generation failed: {e}")
            return {
                "success": False,
                "error": str(e),
            }

    def generate_image(self, task: Any) -> Dict:
        """Generate image using AI services"""
        from services.image_generator import ImageGenerator

        generator = ImageGenerator()
        prompt = task.payload.get("prompt", "")
        style = task.payload.get("style", "modern")
        size = task.payload.get("size", "1024x1024")
        provider = task.payload.get("provider", "auto")  # auto, dalle, sd, leonardo

        try:
            image_url = asyncio.run(generator.generate_image(
                prompt=prompt,
                style=style,
                size=size,
                provider=provider,
            ))

            return {
                "success": True,
                "image_url": image_url,
                "platform": self.platform_name,
            }

        except Exception as e:
            self.logger.error(f"Image generation failed: {e}")
            return {
                "success": False,
                "error": str(e),
            }

    def analyze_metrics(self, task: Any) -> Dict:
        """Analyze engagement metrics for posts"""
        post_ids = task.payload.get("post_ids", [])
        metrics_list = []

        try:
            for post_id in post_ids:
                metrics = asyncio.run(self.get_metrics(post_id))
                metrics_list.append({
                    "post_id": post_id,
                    "likes": metrics.likes,
                    "comments": metrics.comments,
                    "shares": metrics.shares,
                    "views": metrics.views,
                    "engagement_rate": metrics.engagement_rate,
                })

            return {
                "success": True,
                "metrics": metrics_list,
                "platform": self.platform_name,
            }

        except Exception as e:
            self.logger.error(f"Metrics analysis failed: {e}")
            return {
                "success": False,
                "error": str(e),
            }

    def monitor_engagement(self, task: Any) -> Dict:
        """Monitor ongoing engagement"""
        # This is typically called periodically
        brand_id = task.payload.get("brand_id")
        time_range = task.payload.get("time_range", "24h")

        try:
            # Get recent posts and their metrics
            summary = {
                "total_posts": 0,
                "total_engagement": 0,
                "top_performing": [],
                "platform": self.platform_name,
            }

            return {
                "success": True,
                "summary": summary,
            }

        except Exception as e:
            self.logger.error(f"Engagement monitoring failed: {e}")
            return {
                "success": False,
                "error": str(e),
            }

    async def check_rate_limit(self) -> bool:
        """Check if we're within rate limits"""
        if self._rate_limit_remaining <= 0:
            if self._rate_limit_reset and datetime.utcnow() < self._rate_limit_reset:
                wait_time = (self._rate_limit_reset - datetime.utcnow()).total_seconds()
                self.logger.warning(f"Rate limited. Waiting {wait_time}s")
                await asyncio.sleep(wait_time)

        return True

    async def update_rate_limit(self, remaining: int, reset_at: datetime):
        """Update rate limit info from API response"""
        self._rate_limit_remaining = remaining
        self._rate_limit_reset = reset_at

    def format_hashtags(self, hashtags: List[str]) -> str:
        """Format hashtags for the platform"""
        if not hashtags:
            return ""
        return " ".join(f"#{tag.strip('#')}" for tag in hashtags)

    def optimize_for_platform(self, content: PostContent) -> PostContent:
        """Optimize content for specific platform requirements"""
        # Override in subclasses for platform-specific optimization
        return content
