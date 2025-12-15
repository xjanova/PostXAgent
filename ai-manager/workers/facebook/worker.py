"""
Facebook Platform Worker
Supports: Pages, Groups, Marketplace
Uses Facebook Graph API
"""
import aiohttp
import asyncio
from typing import Dict, Optional, List
from datetime import datetime
from workers.base_worker import BasePlatformWorker, PostContent, EngagementMetrics
from config.settings import settings


class FacebookWorker(BasePlatformWorker):
    """Worker for Facebook platform operations"""

    BASE_URL = "https://graph.facebook.com/v19.0"

    def __init__(self):
        super().__init__("facebook")
        self.access_token = None
        self.page_id = None

    async def authenticate(self, credentials: Dict) -> bool:
        """Authenticate with Facebook Graph API"""
        self.access_token = credentials.get("access_token")
        self.page_id = credentials.get("page_id")

        if not self.access_token:
            self.logger.error("No access token provided")
            return False

        try:
            async with aiohttp.ClientSession() as session:
                async with session.get(
                    f"{self.BASE_URL}/me",
                    params={"access_token": self.access_token}
                ) as response:
                    if response.status == 200:
                        self._authenticated = True
                        self.logger.info("Facebook authentication successful")
                        return True
                    else:
                        error = await response.json()
                        self.logger.error(f"Facebook auth failed: {error}")
                        return False
        except Exception as e:
            self.logger.error(f"Facebook authentication error: {e}")
            return False

    async def post_content(self, content: PostContent) -> Dict:
        """Post content to Facebook Page/Group"""
        await self.check_rate_limit()

        if not self._authenticated:
            return {"success": False, "error": "Not authenticated"}

        try:
            async with aiohttp.ClientSession() as session:
                # Prepare post data
                post_data = {
                    "message": content.text,
                    "access_token": self.access_token,
                }

                if content.link:
                    post_data["link"] = content.link

                # Handle image posts
                if content.images:
                    return await self._post_with_images(session, content)

                # Handle video posts
                if content.videos:
                    return await self._post_video(session, content)

                # Regular text/link post
                async with session.post(
                    f"{self.BASE_URL}/{self.page_id}/feed",
                    data=post_data
                ) as response:
                    result = await response.json()

                    if response.status == 200 and "id" in result:
                        return {
                            "success": True,
                            "post_id": result["id"],
                            "platform": "facebook",
                        }
                    else:
                        return {
                            "success": False,
                            "error": result.get("error", {}).get("message", "Unknown error"),
                        }

        except Exception as e:
            self.logger.error(f"Facebook post failed: {e}")
            return {"success": False, "error": str(e)}

    async def _post_with_images(self, session: aiohttp.ClientSession, content: PostContent) -> Dict:
        """Post content with images"""
        photo_ids = []

        # Upload each image
        for image_url in content.images:
            async with session.post(
                f"{self.BASE_URL}/{self.page_id}/photos",
                data={
                    "url": image_url,
                    "published": "false",
                    "access_token": self.access_token,
                }
            ) as response:
                result = await response.json()
                if "id" in result:
                    photo_ids.append({"media_fbid": result["id"]})

        if not photo_ids:
            return {"success": False, "error": "Failed to upload images"}

        # Create post with attached photos
        attached_media = {f"attached_media[{i}]": f'{{"media_fbid":"{p["media_fbid"]}"}}'
                         for i, p in enumerate(photo_ids)}

        post_data = {
            "message": content.text,
            "access_token": self.access_token,
            **attached_media,
        }

        async with session.post(
            f"{self.BASE_URL}/{self.page_id}/feed",
            data=post_data
        ) as response:
            result = await response.json()
            if "id" in result:
                return {
                    "success": True,
                    "post_id": result["id"],
                    "platform": "facebook",
                }
            return {"success": False, "error": result.get("error", {}).get("message")}

    async def _post_video(self, session: aiohttp.ClientSession, content: PostContent) -> Dict:
        """Post video content"""
        video_url = content.videos[0]

        async with session.post(
            f"{self.BASE_URL}/{self.page_id}/videos",
            data={
                "file_url": video_url,
                "description": content.text,
                "access_token": self.access_token,
            }
        ) as response:
            result = await response.json()
            if "id" in result:
                return {
                    "success": True,
                    "post_id": result["id"],
                    "platform": "facebook",
                }
            return {"success": False, "error": result.get("error", {}).get("message")}

    async def schedule_post(self, content: PostContent, scheduled_at: datetime) -> Dict:
        """Schedule a post for later"""
        if not self._authenticated:
            return {"success": False, "error": "Not authenticated"}

        try:
            async with aiohttp.ClientSession() as session:
                post_data = {
                    "message": content.text,
                    "published": "false",
                    "scheduled_publish_time": int(scheduled_at.timestamp()),
                    "access_token": self.access_token,
                }

                if content.link:
                    post_data["link"] = content.link

                async with session.post(
                    f"{self.BASE_URL}/{self.page_id}/feed",
                    data=post_data
                ) as response:
                    result = await response.json()

                    if "id" in result:
                        return {
                            "success": True,
                            "post_id": result["id"],
                            "scheduled_at": scheduled_at.isoformat(),
                            "platform": "facebook",
                        }
                    return {"success": False, "error": result.get("error", {}).get("message")}

        except Exception as e:
            self.logger.error(f"Facebook schedule failed: {e}")
            return {"success": False, "error": str(e)}

    async def get_metrics(self, post_id: str) -> EngagementMetrics:
        """Get engagement metrics for a post"""
        try:
            async with aiohttp.ClientSession() as session:
                async with session.get(
                    f"{self.BASE_URL}/{post_id}",
                    params={
                        "fields": "likes.summary(true),comments.summary(true),shares,insights",
                        "access_token": self.access_token,
                    }
                ) as response:
                    data = await response.json()

                    return EngagementMetrics(
                        post_id=post_id,
                        likes=data.get("likes", {}).get("summary", {}).get("total_count", 0),
                        comments=data.get("comments", {}).get("summary", {}).get("total_count", 0),
                        shares=data.get("shares", {}).get("count", 0) if data.get("shares") else 0,
                        views=0,  # Retrieved from insights
                        reach=0,
                        impressions=0,
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

    async def get_page_insights(self, metrics: List[str], period: str = "day") -> Dict:
        """Get page-level insights"""
        try:
            async with aiohttp.ClientSession() as session:
                async with session.get(
                    f"{self.BASE_URL}/{self.page_id}/insights",
                    params={
                        "metric": ",".join(metrics),
                        "period": period,
                        "access_token": self.access_token,
                    }
                ) as response:
                    return await response.json()

        except Exception as e:
            self.logger.error(f"Failed to get insights: {e}")
            return {}

    def optimize_for_platform(self, content: PostContent) -> PostContent:
        """Optimize content for Facebook"""
        # Facebook character limit for posts is 63,206
        # But optimal length is 40-80 characters for engagement

        optimized = content

        # Add hashtags at the end (Facebook supports them but they're less important)
        if content.hashtags:
            hashtag_str = self.format_hashtags(content.hashtags[:5])  # Limit to 5
            optimized.text = f"{content.text}\n\n{hashtag_str}"

        return optimized
