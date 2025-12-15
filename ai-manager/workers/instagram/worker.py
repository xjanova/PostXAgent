"""
Instagram Platform Worker
Supports: Feed, Stories, Reels
Uses Instagram Graph API (via Facebook)
"""
import aiohttp
from typing import Dict, List
from datetime import datetime
from workers.base_worker import BasePlatformWorker, PostContent, EngagementMetrics


class InstagramWorker(BasePlatformWorker):
    """Worker for Instagram platform operations"""

    BASE_URL = "https://graph.facebook.com/v19.0"

    def __init__(self):
        super().__init__("instagram")
        self.access_token = None
        self.instagram_account_id = None

    async def authenticate(self, credentials: Dict) -> bool:
        """Authenticate with Instagram Graph API"""
        self.access_token = credentials.get("access_token")
        self.instagram_account_id = credentials.get("instagram_account_id")

        if not self.access_token or not self.instagram_account_id:
            self.logger.error("Missing credentials")
            return False

        try:
            async with aiohttp.ClientSession() as session:
                async with session.get(
                    f"{self.BASE_URL}/{self.instagram_account_id}",
                    params={
                        "fields": "id,username",
                        "access_token": self.access_token
                    }
                ) as response:
                    if response.status == 200:
                        self._authenticated = True
                        self.logger.info("Instagram authentication successful")
                        return True
                    return False
        except Exception as e:
            self.logger.error(f"Instagram authentication error: {e}")
            return False

    async def post_content(self, content: PostContent) -> Dict:
        """Post content to Instagram"""
        await self.check_rate_limit()

        if not self._authenticated:
            return {"success": False, "error": "Not authenticated"}

        # Instagram requires media (image or video)
        if not content.images and not content.videos:
            return {"success": False, "error": "Instagram requires image or video"}

        try:
            async with aiohttp.ClientSession() as session:
                if content.videos:
                    return await self._post_reel(session, content)
                elif len(content.images) > 1:
                    return await self._post_carousel(session, content)
                else:
                    return await self._post_single_image(session, content)

        except Exception as e:
            self.logger.error(f"Instagram post failed: {e}")
            return {"success": False, "error": str(e)}

    async def _post_single_image(self, session: aiohttp.ClientSession, content: PostContent) -> Dict:
        """Post a single image"""
        # Create media container
        async with session.post(
            f"{self.BASE_URL}/{self.instagram_account_id}/media",
            data={
                "image_url": content.images[0],
                "caption": self._format_caption(content),
                "access_token": self.access_token,
            }
        ) as response:
            result = await response.json()

            if "id" not in result:
                return {"success": False, "error": result.get("error", {}).get("message")}

            container_id = result["id"]

        # Publish the container
        async with session.post(
            f"{self.BASE_URL}/{self.instagram_account_id}/media_publish",
            data={
                "creation_id": container_id,
                "access_token": self.access_token,
            }
        ) as response:
            result = await response.json()

            if "id" in result:
                return {
                    "success": True,
                    "post_id": result["id"],
                    "platform": "instagram",
                }
            return {"success": False, "error": result.get("error", {}).get("message")}

    async def _post_carousel(self, session: aiohttp.ClientSession, content: PostContent) -> Dict:
        """Post a carousel (multiple images)"""
        children = []

        # Create container for each image
        for image_url in content.images[:10]:  # Max 10 images
            async with session.post(
                f"{self.BASE_URL}/{self.instagram_account_id}/media",
                data={
                    "image_url": image_url,
                    "is_carousel_item": "true",
                    "access_token": self.access_token,
                }
            ) as response:
                result = await response.json()
                if "id" in result:
                    children.append(result["id"])

        if not children:
            return {"success": False, "error": "Failed to create carousel items"}

        # Create carousel container
        async with session.post(
            f"{self.BASE_URL}/{self.instagram_account_id}/media",
            data={
                "media_type": "CAROUSEL",
                "caption": self._format_caption(content),
                "children": ",".join(children),
                "access_token": self.access_token,
            }
        ) as response:
            result = await response.json()
            if "id" not in result:
                return {"success": False, "error": result.get("error", {}).get("message")}
            container_id = result["id"]

        # Publish carousel
        async with session.post(
            f"{self.BASE_URL}/{self.instagram_account_id}/media_publish",
            data={
                "creation_id": container_id,
                "access_token": self.access_token,
            }
        ) as response:
            result = await response.json()
            if "id" in result:
                return {
                    "success": True,
                    "post_id": result["id"],
                    "platform": "instagram",
                    "type": "carousel",
                }
            return {"success": False, "error": result.get("error", {}).get("message")}

    async def _post_reel(self, session: aiohttp.ClientSession, content: PostContent) -> Dict:
        """Post a Reel (video)"""
        async with session.post(
            f"{self.BASE_URL}/{self.instagram_account_id}/media",
            data={
                "media_type": "REELS",
                "video_url": content.videos[0],
                "caption": self._format_caption(content),
                "access_token": self.access_token,
            }
        ) as response:
            result = await response.json()
            if "id" not in result:
                return {"success": False, "error": result.get("error", {}).get("message")}
            container_id = result["id"]

        # Wait for video processing
        await self._wait_for_processing(session, container_id)

        # Publish
        async with session.post(
            f"{self.BASE_URL}/{self.instagram_account_id}/media_publish",
            data={
                "creation_id": container_id,
                "access_token": self.access_token,
            }
        ) as response:
            result = await response.json()
            if "id" in result:
                return {
                    "success": True,
                    "post_id": result["id"],
                    "platform": "instagram",
                    "type": "reel",
                }
            return {"success": False, "error": result.get("error", {}).get("message")}

    async def _wait_for_processing(self, session: aiohttp.ClientSession, container_id: str, max_attempts: int = 30):
        """Wait for video to finish processing"""
        import asyncio

        for _ in range(max_attempts):
            async with session.get(
                f"{self.BASE_URL}/{container_id}",
                params={
                    "fields": "status_code",
                    "access_token": self.access_token,
                }
            ) as response:
                result = await response.json()
                status = result.get("status_code")

                if status == "FINISHED":
                    return True
                elif status == "ERROR":
                    raise Exception("Video processing failed")

            await asyncio.sleep(2)

        raise Exception("Video processing timeout")

    async def schedule_post(self, content: PostContent, scheduled_at: datetime) -> Dict:
        """Schedule a post for later (not natively supported, use internal scheduler)"""
        # Instagram API doesn't support native scheduling
        # We'll store it and let our scheduler handle it
        return {
            "success": True,
            "scheduled": True,
            "scheduled_at": scheduled_at.isoformat(),
            "content": content.__dict__,
            "platform": "instagram",
        }

    async def get_metrics(self, post_id: str) -> EngagementMetrics:
        """Get engagement metrics for a post"""
        try:
            async with aiohttp.ClientSession() as session:
                async with session.get(
                    f"{self.BASE_URL}/{post_id}",
                    params={
                        "fields": "like_count,comments_count,insights.metric(reach,impressions,saved,shares)",
                        "access_token": self.access_token,
                    }
                ) as response:
                    data = await response.json()

                    insights = {}
                    for insight in data.get("insights", {}).get("data", []):
                        insights[insight["name"]] = insight["values"][0]["value"]

                    return EngagementMetrics(
                        post_id=post_id,
                        likes=data.get("like_count", 0),
                        comments=data.get("comments_count", 0),
                        shares=insights.get("shares", 0),
                        saves=insights.get("saved", 0),
                        reach=insights.get("reach", 0),
                        impressions=insights.get("impressions", 0),
                    )

        except Exception as e:
            self.logger.error(f"Failed to get metrics: {e}")
            return EngagementMetrics(post_id=post_id)

    async def delete_post(self, post_id: str) -> bool:
        """Delete a post"""
        try:
            async with aiohttp.ClientSession() as session:
                async with session.delete(
                    f"{self.BASE_URL}/{post_id}",
                    params={"access_token": self.access_token}
                ) as response:
                    result = await response.json()
                    return result.get("success", False)
        except Exception as e:
            self.logger.error(f"Failed to delete post: {e}")
            return False

    def _format_caption(self, content: PostContent) -> str:
        """Format caption with hashtags"""
        caption = content.text

        if content.hashtags:
            hashtags = self.format_hashtags(content.hashtags[:30])  # Max 30 hashtags
            caption = f"{caption}\n\n{hashtags}"

        return caption[:2200]  # Max caption length

    def optimize_for_platform(self, content: PostContent) -> PostContent:
        """Optimize content for Instagram"""
        optimized = content

        # Optimal hashtag count: 5-15
        if len(content.hashtags) > 15:
            optimized.hashtags = content.hashtags[:15]

        # Ensure images are square or portrait (4:5 ratio)
        # This would require image processing

        return optimized
