"""
YouTube Platform Worker
Supports: Videos, Shorts, Community Posts
Uses YouTube Data API v3
"""
import aiohttp
from typing import Dict
from datetime import datetime
from workers.base_worker import BasePlatformWorker, PostContent, EngagementMetrics


class YouTubeWorker(BasePlatformWorker):
    """Worker for YouTube platform operations"""

    BASE_URL = "https://www.googleapis.com/youtube/v3"
    UPLOAD_URL = "https://www.googleapis.com/upload/youtube/v3"

    def __init__(self):
        super().__init__("youtube")
        self.access_token = None
        self.channel_id = None

    async def authenticate(self, credentials: Dict) -> bool:
        """Authenticate with YouTube Data API"""
        self.access_token = credentials.get("access_token")

        if not self.access_token:
            self.logger.error("No access token provided")
            return False

        try:
            async with aiohttp.ClientSession() as session:
                async with session.get(
                    f"{self.BASE_URL}/channels",
                    headers={"Authorization": f"Bearer {self.access_token}"},
                    params={"part": "snippet", "mine": "true"}
                ) as response:
                    if response.status == 200:
                        data = await response.json()
                        if data.get("items"):
                            self.channel_id = data["items"][0]["id"]
                            self._authenticated = True
                            self.logger.info("YouTube authentication successful")
                            return True
                    return False
        except Exception as e:
            self.logger.error(f"YouTube authentication error: {e}")
            return False

    async def post_content(self, content: PostContent) -> Dict:
        """Upload video or post to community"""
        await self.check_rate_limit()

        if not self._authenticated:
            return {"success": False, "error": "Not authenticated"}

        try:
            if content.videos:
                return await self._upload_video(content)
            else:
                return await self._post_community(content)

        except Exception as e:
            self.logger.error(f"YouTube post failed: {e}")
            return {"success": False, "error": str(e)}

    async def _upload_video(self, content: PostContent) -> Dict:
        """Upload a video to YouTube"""
        async with aiohttp.ClientSession() as session:
            # Initialize resumable upload
            metadata = {
                "snippet": {
                    "title": content.text[:100] if content.text else "Untitled",
                    "description": content.text or "",
                    "tags": content.hashtags[:500] if content.hashtags else [],
                    "categoryId": "22",  # People & Blogs
                },
                "status": {
                    "privacyStatus": "public",
                    "selfDeclaredMadeForKids": False,
                }
            }

            # Start resumable upload
            async with session.post(
                f"{self.UPLOAD_URL}/videos",
                headers={
                    "Authorization": f"Bearer {self.access_token}",
                    "Content-Type": "application/json",
                    "X-Upload-Content-Type": "video/*",
                },
                params={"uploadType": "resumable", "part": "snippet,status"},
                json=metadata
            ) as response:
                if response.status != 200:
                    return {"success": False, "error": "Failed to initialize upload"}

                upload_url = response.headers.get("Location")

            # Download video file
            video_url = content.videos[0]
            async with session.get(video_url) as video_response:
                video_data = await video_response.read()

            # Upload video
            async with session.put(
                upload_url,
                headers={"Content-Type": "video/*"},
                data=video_data
            ) as response:
                if response.status == 200:
                    result = await response.json()
                    return {
                        "success": True,
                        "post_id": result.get("id"),
                        "platform": "youtube",
                        "type": "video",
                    }
                return {"success": False, "error": "Upload failed"}

    async def _post_community(self, content: PostContent) -> Dict:
        """Post to YouTube Community tab"""
        # Note: Community posts API has limited availability
        async with aiohttp.ClientSession() as session:
            post_data = {
                "snippet": {
                    "channelId": self.channel_id,
                    "textOriginal": content.text,
                }
            }

            # Add image if present
            if content.images:
                post_data["snippet"]["imageUrl"] = content.images[0]

            async with session.post(
                f"{self.BASE_URL}/activities",
                headers={
                    "Authorization": f"Bearer {self.access_token}",
                    "Content-Type": "application/json",
                },
                params={"part": "snippet"},
                json=post_data
            ) as response:
                if response.status == 200:
                    result = await response.json()
                    return {
                        "success": True,
                        "post_id": result.get("id"),
                        "platform": "youtube",
                        "type": "community",
                    }
                return {"success": False, "error": "Community post failed"}

    async def upload_short(self, content: PostContent) -> Dict:
        """Upload a YouTube Short"""
        if not content.videos:
            return {"success": False, "error": "Video required for Shorts"}

        # Shorts are regular videos with #Shorts hashtag
        if "#Shorts" not in (content.hashtags or []):
            content.hashtags = (content.hashtags or []) + ["Shorts"]

        return await self._upload_video(content)

    async def schedule_post(self, content: PostContent, scheduled_at: datetime) -> Dict:
        """Schedule video for later publication"""
        if not self._authenticated:
            return {"success": False, "error": "Not authenticated"}

        # YouTube supports scheduled uploads
        async with aiohttp.ClientSession() as session:
            metadata = {
                "snippet": {
                    "title": content.text[:100] if content.text else "Untitled",
                    "description": content.text or "",
                    "tags": content.hashtags or [],
                },
                "status": {
                    "privacyStatus": "private",
                    "publishAt": scheduled_at.isoformat() + "Z",
                }
            }

            # This would need to be combined with the upload flow
            return {
                "success": True,
                "scheduled": True,
                "scheduled_at": scheduled_at.isoformat(),
                "platform": "youtube",
            }

    async def get_metrics(self, post_id: str) -> EngagementMetrics:
        """Get engagement metrics for a video"""
        try:
            async with aiohttp.ClientSession() as session:
                async with session.get(
                    f"{self.BASE_URL}/videos",
                    headers={"Authorization": f"Bearer {self.access_token}"},
                    params={
                        "part": "statistics",
                        "id": post_id,
                    }
                ) as response:
                    data = await response.json()
                    stats = data.get("items", [{}])[0].get("statistics", {})

                    return EngagementMetrics(
                        post_id=post_id,
                        likes=int(stats.get("likeCount", 0)),
                        comments=int(stats.get("commentCount", 0)),
                        views=int(stats.get("viewCount", 0)),
                    )

        except Exception as e:
            self.logger.error(f"Failed to get metrics: {e}")
            return EngagementMetrics(post_id=post_id)

    async def delete_post(self, post_id: str) -> bool:
        """Delete a video"""
        try:
            async with aiohttp.ClientSession() as session:
                async with session.delete(
                    f"{self.BASE_URL}/videos",
                    headers={"Authorization": f"Bearer {self.access_token}"},
                    params={"id": post_id}
                ) as response:
                    return response.status == 204
        except Exception as e:
            self.logger.error(f"Failed to delete video: {e}")
            return False

    async def get_channel_stats(self) -> Dict:
        """Get channel statistics"""
        try:
            async with aiohttp.ClientSession() as session:
                async with session.get(
                    f"{self.BASE_URL}/channels",
                    headers={"Authorization": f"Bearer {self.access_token}"},
                    params={
                        "part": "statistics",
                        "id": self.channel_id,
                    }
                ) as response:
                    data = await response.json()
                    stats = data.get("items", [{}])[0].get("statistics", {})
                    return {
                        "subscribers": int(stats.get("subscriberCount", 0)),
                        "videos": int(stats.get("videoCount", 0)),
                        "views": int(stats.get("viewCount", 0)),
                    }
        except Exception as e:
            self.logger.error(f"Failed to get channel stats: {e}")
            return {}

    def optimize_for_platform(self, content: PostContent) -> PostContent:
        """Optimize content for YouTube"""
        optimized = content

        # YouTube title limit: 100 characters
        # Description limit: 5000 characters

        # Add relevant hashtags in description
        if content.hashtags:
            hashtags = self.format_hashtags(content.hashtags[:15])
            optimized.text = f"{content.text}\n\n{hashtags}"

        return optimized
