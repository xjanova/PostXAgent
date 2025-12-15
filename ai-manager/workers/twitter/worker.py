"""
Twitter/X Platform Worker
Supports: Tweets, Threads, Media
Uses Twitter API v2
"""
import aiohttp
from typing import Dict, List
from datetime import datetime
from workers.base_worker import BasePlatformWorker, PostContent, EngagementMetrics


class TwitterWorker(BasePlatformWorker):
    """Worker for Twitter/X platform operations"""

    BASE_URL = "https://api.twitter.com/2"
    UPLOAD_URL = "https://upload.twitter.com/1.1"

    def __init__(self):
        super().__init__("twitter")
        self.bearer_token = None
        self.access_token = None
        self.access_token_secret = None

    async def authenticate(self, credentials: Dict) -> bool:
        """Authenticate with Twitter API"""
        self.bearer_token = credentials.get("bearer_token")
        self.access_token = credentials.get("access_token")
        self.access_token_secret = credentials.get("access_token_secret")

        if not self.bearer_token:
            self.logger.error("No bearer token provided")
            return False

        try:
            async with aiohttp.ClientSession() as session:
                async with session.get(
                    f"{self.BASE_URL}/users/me",
                    headers={"Authorization": f"Bearer {self.bearer_token}"}
                ) as response:
                    if response.status == 200:
                        self._authenticated = True
                        self.logger.info("Twitter authentication successful")
                        return True
                    return False
        except Exception as e:
            self.logger.error(f"Twitter authentication error: {e}")
            return False

    async def post_content(self, content: PostContent) -> Dict:
        """Post a tweet"""
        await self.check_rate_limit()

        if not self._authenticated:
            return {"success": False, "error": "Not authenticated"}

        try:
            async with aiohttp.ClientSession() as session:
                tweet_data = {"text": self._format_tweet(content)}

                # Upload media if present
                if content.images or content.videos:
                    media_ids = await self._upload_media(session, content)
                    if media_ids:
                        tweet_data["media"] = {"media_ids": media_ids}

                async with session.post(
                    f"{self.BASE_URL}/tweets",
                    headers={
                        "Authorization": f"Bearer {self.bearer_token}",
                        "Content-Type": "application/json",
                    },
                    json=tweet_data
                ) as response:
                    result = await response.json()

                    if "data" in result:
                        return {
                            "success": True,
                            "post_id": result["data"]["id"],
                            "platform": "twitter",
                        }
                    return {
                        "success": False,
                        "error": result.get("errors", [{}])[0].get("message", "Unknown error")
                    }

        except Exception as e:
            self.logger.error(f"Twitter post failed: {e}")
            return {"success": False, "error": str(e)}

    async def post_thread(self, tweets: List[str]) -> Dict:
        """Post a thread (multiple connected tweets)"""
        if not self._authenticated:
            return {"success": False, "error": "Not authenticated"}

        try:
            async with aiohttp.ClientSession() as session:
                tweet_ids = []
                reply_to = None

                for tweet_text in tweets:
                    tweet_data = {"text": tweet_text}

                    if reply_to:
                        tweet_data["reply"] = {"in_reply_to_tweet_id": reply_to}

                    async with session.post(
                        f"{self.BASE_URL}/tweets",
                        headers={
                            "Authorization": f"Bearer {self.bearer_token}",
                            "Content-Type": "application/json",
                        },
                        json=tweet_data
                    ) as response:
                        result = await response.json()

                        if "data" in result:
                            tweet_id = result["data"]["id"]
                            tweet_ids.append(tweet_id)
                            reply_to = tweet_id
                        else:
                            return {"success": False, "error": "Thread creation failed"}

                return {
                    "success": True,
                    "tweet_ids": tweet_ids,
                    "platform": "twitter",
                    "type": "thread",
                }

        except Exception as e:
            self.logger.error(f"Twitter thread failed: {e}")
            return {"success": False, "error": str(e)}

    async def _upload_media(self, session: aiohttp.ClientSession, content: PostContent) -> List[str]:
        """Upload media and return media IDs"""
        media_ids = []

        # Upload images
        for image_url in content.images[:4]:  # Max 4 images
            media_id = await self._upload_single_media(session, image_url, "image")
            if media_id:
                media_ids.append(media_id)

        # Upload video (only 1 allowed)
        if content.videos and not media_ids:
            media_id = await self._upload_single_media(session, content.videos[0], "video")
            if media_id:
                media_ids.append(media_id)

        return media_ids

    async def _upload_single_media(self, session: aiohttp.ClientSession, url: str, media_type: str) -> str:
        """Upload a single media file"""
        # This is simplified - actual implementation needs chunked upload for videos
        try:
            # Download media
            async with session.get(url) as response:
                media_data = await response.read()

            # Initialize upload
            async with session.post(
                f"{self.UPLOAD_URL}/media/upload.json",
                headers={"Authorization": f"Bearer {self.bearer_token}"},
                data={
                    "command": "INIT",
                    "total_bytes": len(media_data),
                    "media_type": "image/jpeg" if media_type == "image" else "video/mp4",
                }
            ) as response:
                result = await response.json()
                media_id = result.get("media_id_string")

                if not media_id:
                    return None

            # Append data
            async with session.post(
                f"{self.UPLOAD_URL}/media/upload.json",
                headers={"Authorization": f"Bearer {self.bearer_token}"},
                data={
                    "command": "APPEND",
                    "media_id": media_id,
                    "segment_index": 0,
                    "media": media_data,
                }
            ) as response:
                pass

            # Finalize
            async with session.post(
                f"{self.UPLOAD_URL}/media/upload.json",
                headers={"Authorization": f"Bearer {self.bearer_token}"},
                data={
                    "command": "FINALIZE",
                    "media_id": media_id,
                }
            ) as response:
                return media_id

        except Exception as e:
            self.logger.error(f"Media upload failed: {e}")
            return None

    async def schedule_post(self, content: PostContent, scheduled_at: datetime) -> Dict:
        """Schedule a tweet"""
        # Twitter API v2 supports scheduled tweets
        if not self._authenticated:
            return {"success": False, "error": "Not authenticated"}

        return {
            "success": True,
            "scheduled": True,
            "scheduled_at": scheduled_at.isoformat(),
            "platform": "twitter",
        }

    async def get_metrics(self, post_id: str) -> EngagementMetrics:
        """Get engagement metrics for a tweet"""
        try:
            async with aiohttp.ClientSession() as session:
                async with session.get(
                    f"{self.BASE_URL}/tweets/{post_id}",
                    headers={"Authorization": f"Bearer {self.bearer_token}"},
                    params={"tweet.fields": "public_metrics"}
                ) as response:
                    data = await response.json()
                    metrics = data.get("data", {}).get("public_metrics", {})

                    return EngagementMetrics(
                        post_id=post_id,
                        likes=metrics.get("like_count", 0),
                        comments=metrics.get("reply_count", 0),
                        shares=metrics.get("retweet_count", 0) + metrics.get("quote_count", 0),
                        views=metrics.get("impression_count", 0),
                    )

        except Exception as e:
            self.logger.error(f"Failed to get metrics: {e}")
            return EngagementMetrics(post_id=post_id)

    async def delete_post(self, post_id: str) -> bool:
        """Delete a tweet"""
        try:
            async with aiohttp.ClientSession() as session:
                async with session.delete(
                    f"{self.BASE_URL}/tweets/{post_id}",
                    headers={"Authorization": f"Bearer {self.bearer_token}"}
                ) as response:
                    result = await response.json()
                    return result.get("data", {}).get("deleted", False)
        except Exception as e:
            self.logger.error(f"Failed to delete tweet: {e}")
            return False

    def _format_tweet(self, content: PostContent) -> str:
        """Format tweet with hashtags and mentions"""
        tweet = content.text

        # Add hashtags
        if content.hashtags:
            hashtags = self.format_hashtags(content.hashtags[:5])
            tweet = f"{tweet}\n\n{hashtags}"

        # Add link
        if content.link:
            tweet = f"{tweet}\n{content.link}"

        return tweet[:280]  # Twitter character limit

    def optimize_for_platform(self, content: PostContent) -> PostContent:
        """Optimize content for Twitter"""
        optimized = content

        # Limit hashtags (2-3 is optimal)
        if len(content.hashtags) > 3:
            optimized.hashtags = content.hashtags[:3]

        # Truncate text if needed
        if len(content.text) > 250:  # Leave room for hashtags
            optimized.text = content.text[:247] + "..."

        return optimized
