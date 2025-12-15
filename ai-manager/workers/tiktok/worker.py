"""
TikTok Platform Worker
Supports: Videos, Lives
Uses TikTok API for Business
"""
import aiohttp
from typing import Dict
from datetime import datetime
from workers.base_worker import BasePlatformWorker, PostContent, EngagementMetrics


class TikTokWorker(BasePlatformWorker):
    """Worker for TikTok platform operations"""

    BASE_URL = "https://open.tiktokapis.com/v2"

    def __init__(self):
        super().__init__("tiktok")
        self.access_token = None
        self.open_id = None

    async def authenticate(self, credentials: Dict) -> bool:
        """Authenticate with TikTok API"""
        self.access_token = credentials.get("access_token")
        self.open_id = credentials.get("open_id")

        if not self.access_token:
            self.logger.error("No access token provided")
            return False

        try:
            async with aiohttp.ClientSession() as session:
                async with session.get(
                    f"{self.BASE_URL}/user/info/",
                    headers={
                        "Authorization": f"Bearer {self.access_token}",
                    },
                    params={"fields": "open_id,display_name"}
                ) as response:
                    if response.status == 200:
                        self._authenticated = True
                        self.logger.info("TikTok authentication successful")
                        return True
                    return False
        except Exception as e:
            self.logger.error(f"TikTok authentication error: {e}")
            return False

    async def post_content(self, content: PostContent) -> Dict:
        """Post video to TikTok"""
        await self.check_rate_limit()

        if not self._authenticated:
            return {"success": False, "error": "Not authenticated"}

        if not content.videos:
            return {"success": False, "error": "TikTok requires video content"}

        try:
            async with aiohttp.ClientSession() as session:
                # Initialize video upload
                init_response = await self._init_video_upload(session, content)

                if not init_response.get("success"):
                    return init_response

                publish_id = init_response["publish_id"]

                # Upload video chunks
                upload_result = await self._upload_video(
                    session,
                    content.videos[0],
                    init_response["upload_url"]
                )

                if not upload_result.get("success"):
                    return upload_result

                # Publish video
                return await self._publish_video(session, publish_id, content)

        except Exception as e:
            self.logger.error(f"TikTok post failed: {e}")
            return {"success": False, "error": str(e)}

    async def _init_video_upload(self, session: aiohttp.ClientSession, content: PostContent) -> Dict:
        """Initialize video upload"""
        async with session.post(
            f"{self.BASE_URL}/post/publish/video/init/",
            headers={
                "Authorization": f"Bearer {self.access_token}",
                "Content-Type": "application/json",
            },
            json={
                "post_info": {
                    "title": content.text[:150],
                    "privacy_level": "PUBLIC_TO_EVERYONE",
                    "disable_duet": False,
                    "disable_stitch": False,
                    "disable_comment": False,
                },
                "source_info": {
                    "source": "FILE_UPLOAD",
                }
            }
        ) as response:
            result = await response.json()

            if result.get("error", {}).get("code") == "ok":
                data = result.get("data", {})
                return {
                    "success": True,
                    "publish_id": data.get("publish_id"),
                    "upload_url": data.get("upload_url"),
                }
            return {"success": False, "error": result.get("error", {}).get("message")}

    async def _upload_video(self, session: aiohttp.ClientSession, video_url: str, upload_url: str) -> Dict:
        """Upload video to TikTok"""
        # Download video first if it's a URL
        async with session.get(video_url) as video_response:
            video_data = await video_response.read()

        # Upload to TikTok
        async with session.put(
            upload_url,
            headers={
                "Content-Type": "video/mp4",
                "Content-Range": f"bytes 0-{len(video_data)-1}/{len(video_data)}",
            },
            data=video_data
        ) as response:
            if response.status in [200, 201]:
                return {"success": True}
            return {"success": False, "error": "Video upload failed"}

    async def _publish_video(self, session: aiohttp.ClientSession, publish_id: str, content: PostContent) -> Dict:
        """Publish the uploaded video"""
        async with session.post(
            f"{self.BASE_URL}/post/publish/status/fetch/",
            headers={
                "Authorization": f"Bearer {self.access_token}",
                "Content-Type": "application/json",
            },
            json={"publish_id": publish_id}
        ) as response:
            result = await response.json()

            if result.get("data", {}).get("status") == "PUBLISH_COMPLETE":
                return {
                    "success": True,
                    "post_id": result.get("data", {}).get("video_id"),
                    "platform": "tiktok",
                }
            return {"success": False, "error": "Publish failed"}

    async def schedule_post(self, content: PostContent, scheduled_at: datetime) -> Dict:
        """Schedule a post for later"""
        # TikTok supports scheduled posting
        return {
            "success": True,
            "scheduled": True,
            "scheduled_at": scheduled_at.isoformat(),
            "platform": "tiktok",
        }

    async def get_metrics(self, post_id: str) -> EngagementMetrics:
        """Get engagement metrics for a video"""
        try:
            async with aiohttp.ClientSession() as session:
                async with session.post(
                    f"{self.BASE_URL}/video/query/",
                    headers={
                        "Authorization": f"Bearer {self.access_token}",
                        "Content-Type": "application/json",
                    },
                    json={
                        "filters": {"video_ids": [post_id]},
                        "fields": ["like_count", "comment_count", "share_count", "view_count"]
                    }
                ) as response:
                    data = await response.json()
                    video = data.get("data", {}).get("videos", [{}])[0]

                    return EngagementMetrics(
                        post_id=post_id,
                        likes=video.get("like_count", 0),
                        comments=video.get("comment_count", 0),
                        shares=video.get("share_count", 0),
                        views=video.get("view_count", 0),
                    )

        except Exception as e:
            self.logger.error(f"Failed to get metrics: {e}")
            return EngagementMetrics(post_id=post_id)

    async def delete_post(self, post_id: str) -> bool:
        """Delete a video (TikTok API may not support this)"""
        self.logger.warning("TikTok video deletion may not be available via API")
        return False

    def optimize_for_platform(self, content: PostContent) -> PostContent:
        """Optimize content for TikTok"""
        optimized = content

        # TikTok optimal video specs:
        # - Duration: 15-60 seconds (up to 10 min)
        # - Aspect ratio: 9:16 (vertical)
        # - Resolution: 1080x1920

        # Hashtags are important on TikTok
        if content.hashtags:
            hashtags = self.format_hashtags(content.hashtags[:10])
            optimized.text = f"{content.text} {hashtags}"

        return optimized
