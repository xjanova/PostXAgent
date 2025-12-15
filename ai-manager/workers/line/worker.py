"""
LINE Official Account Platform Worker
Supports: Broadcast, Push, Rich Menu, Flex Messages
Uses LINE Messaging API
"""
import aiohttp
from typing import Dict, List
from datetime import datetime
from workers.base_worker import BasePlatformWorker, PostContent, EngagementMetrics


class LineWorker(BasePlatformWorker):
    """Worker for LINE Official Account operations"""

    BASE_URL = "https://api.line.me/v2/bot"

    def __init__(self):
        super().__init__("line")
        self.channel_access_token = None

    async def authenticate(self, credentials: Dict) -> bool:
        """Authenticate with LINE Messaging API"""
        self.channel_access_token = credentials.get("channel_access_token")

        if not self.channel_access_token:
            self.logger.error("No channel access token provided")
            return False

        try:
            async with aiohttp.ClientSession() as session:
                async with session.get(
                    f"{self.BASE_URL}/info",
                    headers={"Authorization": f"Bearer {self.channel_access_token}"}
                ) as response:
                    if response.status == 200:
                        self._authenticated = True
                        self.logger.info("LINE authentication successful")
                        return True
                    return False
        except Exception as e:
            self.logger.error(f"LINE authentication error: {e}")
            return False

    async def post_content(self, content: PostContent) -> Dict:
        """Broadcast message to all followers"""
        await self.check_rate_limit()

        if not self._authenticated:
            return {"success": False, "error": "Not authenticated"}

        try:
            messages = self._build_messages(content)

            async with aiohttp.ClientSession() as session:
                async with session.post(
                    f"{self.BASE_URL}/message/broadcast",
                    headers={
                        "Authorization": f"Bearer {self.channel_access_token}",
                        "Content-Type": "application/json",
                    },
                    json={"messages": messages}
                ) as response:
                    if response.status == 200:
                        return {
                            "success": True,
                            "platform": "line",
                            "type": "broadcast",
                        }
                    else:
                        result = await response.json()
                        return {"success": False, "error": result.get("message")}

        except Exception as e:
            self.logger.error(f"LINE broadcast failed: {e}")
            return {"success": False, "error": str(e)}

    async def push_message(self, user_id: str, content: PostContent) -> Dict:
        """Push message to specific user"""
        if not self._authenticated:
            return {"success": False, "error": "Not authenticated"}

        try:
            messages = self._build_messages(content)

            async with aiohttp.ClientSession() as session:
                async with session.post(
                    f"{self.BASE_URL}/message/push",
                    headers={
                        "Authorization": f"Bearer {self.channel_access_token}",
                        "Content-Type": "application/json",
                    },
                    json={
                        "to": user_id,
                        "messages": messages
                    }
                ) as response:
                    if response.status == 200:
                        return {
                            "success": True,
                            "platform": "line",
                            "type": "push",
                        }
                    else:
                        result = await response.json()
                        return {"success": False, "error": result.get("message")}

        except Exception as e:
            self.logger.error(f"LINE push failed: {e}")
            return {"success": False, "error": str(e)}

    async def multicast(self, user_ids: List[str], content: PostContent) -> Dict:
        """Send message to multiple users"""
        if not self._authenticated:
            return {"success": False, "error": "Not authenticated"}

        try:
            messages = self._build_messages(content)

            async with aiohttp.ClientSession() as session:
                async with session.post(
                    f"{self.BASE_URL}/message/multicast",
                    headers={
                        "Authorization": f"Bearer {self.channel_access_token}",
                        "Content-Type": "application/json",
                    },
                    json={
                        "to": user_ids[:500],  # Max 500 users
                        "messages": messages
                    }
                ) as response:
                    if response.status == 200:
                        return {
                            "success": True,
                            "platform": "line",
                            "type": "multicast",
                            "recipient_count": len(user_ids),
                        }
                    else:
                        result = await response.json()
                        return {"success": False, "error": result.get("message")}

        except Exception as e:
            self.logger.error(f"LINE multicast failed: {e}")
            return {"success": False, "error": str(e)}

    def _build_messages(self, content: PostContent) -> List[Dict]:
        """Build LINE message objects"""
        messages = []

        # Text message
        if content.text:
            messages.append({
                "type": "text",
                "text": content.text
            })

        # Image messages
        for image_url in content.images[:5]:  # Max 5 messages
            messages.append({
                "type": "image",
                "originalContentUrl": image_url,
                "previewImageUrl": image_url,
            })

        # Video messages
        for video_url in content.videos[:5]:
            messages.append({
                "type": "video",
                "originalContentUrl": video_url,
                "previewImageUrl": video_url,  # Should be a thumbnail
            })

        return messages[:5]  # Max 5 messages per request

    async def create_flex_message(self, flex_content: Dict) -> Dict:
        """Create and send a Flex Message"""
        if not self._authenticated:
            return {"success": False, "error": "Not authenticated"}

        try:
            messages = [{
                "type": "flex",
                "altText": flex_content.get("alt_text", "Promotional message"),
                "contents": flex_content.get("contents", {})
            }]

            async with aiohttp.ClientSession() as session:
                async with session.post(
                    f"{self.BASE_URL}/message/broadcast",
                    headers={
                        "Authorization": f"Bearer {self.channel_access_token}",
                        "Content-Type": "application/json",
                    },
                    json={"messages": messages}
                ) as response:
                    if response.status == 200:
                        return {"success": True, "platform": "line", "type": "flex"}
                    else:
                        result = await response.json()
                        return {"success": False, "error": result.get("message")}

        except Exception as e:
            self.logger.error(f"LINE flex message failed: {e}")
            return {"success": False, "error": str(e)}

    async def set_rich_menu(self, rich_menu_id: str, user_id: str = None) -> Dict:
        """Set rich menu for user or as default"""
        if not self._authenticated:
            return {"success": False, "error": "Not authenticated"}

        try:
            async with aiohttp.ClientSession() as session:
                if user_id:
                    url = f"{self.BASE_URL}/user/{user_id}/richmenu/{rich_menu_id}"
                else:
                    url = f"{self.BASE_URL}/user/all/richmenu/{rich_menu_id}"

                async with session.post(
                    url,
                    headers={"Authorization": f"Bearer {self.channel_access_token}"}
                ) as response:
                    if response.status == 200:
                        return {"success": True, "platform": "line", "type": "rich_menu"}
                    else:
                        result = await response.json()
                        return {"success": False, "error": result.get("message")}

        except Exception as e:
            self.logger.error(f"LINE rich menu failed: {e}")
            return {"success": False, "error": str(e)}

    async def schedule_post(self, content: PostContent, scheduled_at: datetime) -> Dict:
        """Schedule a broadcast for later"""
        # LINE doesn't have native scheduling, use our scheduler
        return {
            "success": True,
            "scheduled": True,
            "scheduled_at": scheduled_at.isoformat(),
            "platform": "line",
        }

    async def get_metrics(self, post_id: str = None) -> EngagementMetrics:
        """Get messaging statistics"""
        try:
            async with aiohttp.ClientSession() as session:
                # Get number of message deliveries
                async with session.get(
                    f"{self.BASE_URL}/insight/message/delivery",
                    headers={"Authorization": f"Bearer {self.channel_access_token}"},
                    params={"date": datetime.utcnow().strftime("%Y%m%d")}
                ) as response:
                    data = await response.json()

                    return EngagementMetrics(
                        post_id=post_id or "broadcast",
                        views=data.get("broadcast", 0),
                        reach=data.get("targeting", 0),
                    )

        except Exception as e:
            self.logger.error(f"Failed to get metrics: {e}")
            return EngagementMetrics(post_id=post_id or "broadcast")

    async def get_follower_count(self) -> int:
        """Get number of followers"""
        try:
            async with aiohttp.ClientSession() as session:
                async with session.get(
                    f"{self.BASE_URL}/insight/followers",
                    headers={"Authorization": f"Bearer {self.channel_access_token}"},
                    params={"date": datetime.utcnow().strftime("%Y%m%d")}
                ) as response:
                    data = await response.json()
                    return data.get("followers", 0)
        except Exception as e:
            self.logger.error(f"Failed to get follower count: {e}")
            return 0

    async def delete_post(self, post_id: str) -> bool:
        """LINE messages cannot be deleted after sending"""
        self.logger.warning("LINE messages cannot be recalled after sending")
        return False

    def optimize_for_platform(self, content: PostContent) -> PostContent:
        """Optimize content for LINE"""
        optimized = content

        # LINE message length limit
        if len(content.text) > 5000:
            optimized.text = content.text[:4997] + "..."

        return optimized
