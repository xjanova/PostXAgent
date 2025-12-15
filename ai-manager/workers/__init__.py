"""
Platform Workers Package
"""
from typing import Dict, Type
from config.settings import SocialPlatform
from workers.base_worker import BasePlatformWorker
from workers.facebook.worker import FacebookWorker
from workers.instagram.worker import InstagramWorker
from workers.tiktok.worker import TikTokWorker
from workers.twitter.worker import TwitterWorker
from workers.line.worker import LineWorker
from workers.youtube.worker import YouTubeWorker
from workers.threads.worker import ThreadsWorker
from workers.linkedin.worker import LinkedInWorker
from workers.pinterest.worker import PinterestWorker


# Platform worker registry
PLATFORM_WORKERS: Dict[SocialPlatform, Type[BasePlatformWorker]] = {
    SocialPlatform.FACEBOOK: FacebookWorker,
    SocialPlatform.INSTAGRAM: InstagramWorker,
    SocialPlatform.TIKTOK: TikTokWorker,
    SocialPlatform.TWITTER: TwitterWorker,
    SocialPlatform.LINE: LineWorker,
    SocialPlatform.YOUTUBE: YouTubeWorker,
    SocialPlatform.THREADS: ThreadsWorker,
    SocialPlatform.LINKEDIN: LinkedInWorker,
    SocialPlatform.PINTEREST: PinterestWorker,
}


def get_worker_for_platform(platform: SocialPlatform) -> BasePlatformWorker:
    """Get a worker instance for the specified platform"""
    worker_class = PLATFORM_WORKERS.get(platform)
    if not worker_class:
        raise ValueError(f"No worker available for platform: {platform}")
    return worker_class()


__all__ = [
    "BasePlatformWorker",
    "get_worker_for_platform",
    "FacebookWorker",
    "InstagramWorker",
    "TikTokWorker",
    "TwitterWorker",
    "LineWorker",
    "YouTubeWorker",
    "ThreadsWorker",
    "LinkedInWorker",
    "PinterestWorker",
]
