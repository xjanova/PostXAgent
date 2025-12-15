"""
Threads Platform Worker
Supports: Text Posts, Image Posts
Uses Threads API (via Instagram Graph API)
"""
import aiohttp
from typing import Dict
from datetime import datetime
from workers.base_worker import BasePlatformWorker, PostContent, EngagementMetrics


class ThreadsWorker(BasePlatformWorker):
    """Worker for Threads platform operations"""

    BASE_URL = "https://graph.threads.net/v1.0"

    def __init__(self):
        super().__init__("threads")
        self.access_token = None
        self.user_id = None

    async def authenticate(self, credentials: Dict) -> bool:
        """Authenticate with Threads API"""
        self.access_token = credentials.get("access_token")
        self.user_id = credentials.get("user_id")

        if not self.access_token:
            self.logger.error("No access token provided")
            return False

        try:
            async with aiohttp.ClientSession() as session:
                async with session.get(
                    f"{self.BASE_URL}/me",
                    params={
                        "fields": "id,username",
                        "access_token": self.access_token
                    }
                ) as response:
                    if response.status == 200:
                        data = await response.json()
                        self.user_id = data.get("id")
                        self._authenticated = True
                        self.logger.info("Threads authentication successful")
                        return True
                    return False
        except Exception as e:
            self.logger.error(f"Threads authentication error: {e}")
            return False

    async def post_content(self, content: PostContent) -> Dict:
        """Post content to Threads"""
        await self.check_rate_limit()

        if not self._authenticated:
            return {"success": False, "error": "Not authenticated"}

        try:
            async with aiohttp.ClientSession() as session:
                # Create media container
                container_data = {
                    "media_type": "TEXT" if not content.images else "IMAGE",
                    "text": content.text,
                    "access_token": self.access_token,
                }

                if content.images:
                    container_data["image_url"] = content.images[0]

                async with session.post(
                    f"{self.BASE_URL}/{self.user_id}/threads",
                    data=container_data
                ) as response:
                    result = await response.json()

                    if "id" not in result:
                        return {"success": False, "error": result.get("error", {}).get("message")}

                    container_id = result["id"]

                # Publish the container
                async with session.post(
                    f"{self.BASE_URL}/{self.user_id}/threads_publish",
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
                            "platform": "threads",
                        }
                    return {"success": False, "error": result.get("error", {}).get("message")}

        except Exception as e:
            self.logger.error(f"Threads post failed: {e}")
            return {"success": False, "error": str(e)}

    async def post_carousel(self, content: PostContent) -> Dict:
        """Post a carousel (multiple images)"""
        if not self._authenticated:
            return {"success": False, "error": "Not authenticated"}

        if len(content.images) < 2:
            return await self.post_content(content)

        try:
            async with aiohttp.ClientSession() as session:
                children = []

                # Create container for each image
                for image_url in content.images[:10]:  # Max 10 images
                    async with session.post(
                        f"{self.BASE_URL}/{self.user_id}/threads",
                        data={
                            "media_type": "IMAGE",
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
                    f"{self.BASE_URL}/{self.user_id}/threads",
                    data={
                        "media_type": "CAROUSEL",
                        "text": content.text,
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
                    f"{self.BASE_URL}/{self.user_id}/threads_publish",
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
                            "platform": "threads",
                            "type": "carousel",
                        }
                    return {"success": False, "error": result.get("error", {}).get("message")}

        except Exception as e:
            self.logger.error(f"Threads carousel failed: {e}")
            return {"success": False, "error": str(e)}

    async def schedule_post(self, content: PostContent, scheduled_at: datetime) -> Dict:
        """Schedule a post for later"""
        # Threads doesn't have native scheduling
        return {
            "success": True,
            "scheduled": True,
            "scheduled_at": scheduled_at.isoformat(),
            "platform": "threads",
        }

    async def get_metrics(self, post_id: str) -> EngagementMetrics:
        """Get engagement metrics for a post"""
        try:
            async with aiohttp.ClientSession() as session:
                async with session.get(
                    f"{self.BASE_URL}/{post_id}",
                    params={
                        "fields": "like_count,reply_count,repost_count,quote_count,views",
                        "access_token": self.access_token,
                    }
                ) as response:
                    data = await response.json()

                    return EngagementMetrics(
                        post_id=post_id,
                        likes=data.get("like_count", 0),
                        comments=data.get("reply_count", 0),
                        shares=data.get("repost_count", 0) + data.get("quote_count", 0),
                        views=data.get("views", 0),
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

    def optimize_for_platform(self, content: PostContent) -> PostContent:
        """Optimize content for Threads"""
        optimized = content

        # Threads character limit: 500
        if len(content.text) > 500:
            optimized.text = content.text[:497] + "..."

        return optimized
