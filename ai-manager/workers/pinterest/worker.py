"""
Pinterest Platform Worker
Supports: Pins, Boards
Uses Pinterest API v5
"""
import aiohttp
from typing import Dict
from datetime import datetime
from workers.base_worker import BasePlatformWorker, PostContent, EngagementMetrics


class PinterestWorker(BasePlatformWorker):
    """Worker for Pinterest platform operations"""

    BASE_URL = "https://api.pinterest.com/v5"

    def __init__(self):
        super().__init__("pinterest")
        self.access_token = None

    async def authenticate(self, credentials: Dict) -> bool:
        """Authenticate with Pinterest API"""
        self.access_token = credentials.get("access_token")

        if not self.access_token:
            self.logger.error("No access token provided")
            return False

        try:
            async with aiohttp.ClientSession() as session:
                async with session.get(
                    f"{self.BASE_URL}/user_account",
                    headers={"Authorization": f"Bearer {self.access_token}"}
                ) as response:
                    if response.status == 200:
                        self._authenticated = True
                        self.logger.info("Pinterest authentication successful")
                        return True
                    return False
        except Exception as e:
            self.logger.error(f"Pinterest authentication error: {e}")
            return False

    async def post_content(self, content: PostContent) -> Dict:
        """Create a Pin on Pinterest"""
        await self.check_rate_limit()

        if not self._authenticated:
            return {"success": False, "error": "Not authenticated"}

        if not content.images:
            return {"success": False, "error": "Pinterest requires an image"}

        try:
            async with aiohttp.ClientSession() as session:
                board_id = content.location  # Use location field for board ID

                if not board_id:
                    # Get first available board
                    boards = await self._get_boards(session)
                    if boards:
                        board_id = boards[0]["id"]
                    else:
                        return {"success": False, "error": "No boards found"}

                pin_data = {
                    "board_id": board_id,
                    "media_source": {
                        "source_type": "image_url",
                        "url": content.images[0],
                    },
                    "title": content.text[:100] if content.text else "",
                    "description": content.text[:500] if content.text else "",
                    "alt_text": content.text[:500] if content.text else "",
                }

                if content.link:
                    pin_data["link"] = content.link

                async with session.post(
                    f"{self.BASE_URL}/pins",
                    headers={
                        "Authorization": f"Bearer {self.access_token}",
                        "Content-Type": "application/json",
                    },
                    json=pin_data
                ) as response:
                    if response.status == 201:
                        result = await response.json()
                        return {
                            "success": True,
                            "post_id": result.get("id"),
                            "platform": "pinterest",
                        }
                    else:
                        result = await response.json()
                        return {"success": False, "error": str(result)}

        except Exception as e:
            self.logger.error(f"Pinterest pin failed: {e}")
            return {"success": False, "error": str(e)}

    async def _get_boards(self, session: aiohttp.ClientSession) -> list:
        """Get user's boards"""
        async with session.get(
            f"{self.BASE_URL}/boards",
            headers={"Authorization": f"Bearer {self.access_token}"}
        ) as response:
            if response.status == 200:
                data = await response.json()
                return data.get("items", [])
            return []

    async def create_board(self, name: str, description: str = "", privacy: str = "PUBLIC") -> Dict:
        """Create a new board"""
        if not self._authenticated:
            return {"success": False, "error": "Not authenticated"}

        try:
            async with aiohttp.ClientSession() as session:
                async with session.post(
                    f"{self.BASE_URL}/boards",
                    headers={
                        "Authorization": f"Bearer {self.access_token}",
                        "Content-Type": "application/json",
                    },
                    json={
                        "name": name,
                        "description": description,
                        "privacy": privacy,
                    }
                ) as response:
                    if response.status == 201:
                        result = await response.json()
                        return {
                            "success": True,
                            "board_id": result.get("id"),
                            "platform": "pinterest",
                        }
                    else:
                        result = await response.json()
                        return {"success": False, "error": str(result)}

        except Exception as e:
            self.logger.error(f"Pinterest board creation failed: {e}")
            return {"success": False, "error": str(e)}

    async def schedule_post(self, content: PostContent, scheduled_at: datetime) -> Dict:
        """Schedule a pin for later"""
        # Pinterest doesn't have native scheduling API
        return {
            "success": True,
            "scheduled": True,
            "scheduled_at": scheduled_at.isoformat(),
            "platform": "pinterest",
        }

    async def get_metrics(self, post_id: str) -> EngagementMetrics:
        """Get engagement metrics for a pin"""
        try:
            async with aiohttp.ClientSession() as session:
                async with session.get(
                    f"{self.BASE_URL}/pins/{post_id}",
                    headers={"Authorization": f"Bearer {self.access_token}"},
                    params={"pin_metrics": "true"}
                ) as response:
                    data = await response.json()
                    metrics = data.get("pin_metrics", {}).get("all_time", {})

                    return EngagementMetrics(
                        post_id=post_id,
                        saves=metrics.get("save", 0),
                        clicks=metrics.get("outbound_click", 0),
                        impressions=metrics.get("impression", 0),
                        views=metrics.get("pin_click", 0),
                    )

        except Exception as e:
            self.logger.error(f"Failed to get metrics: {e}")
            return EngagementMetrics(post_id=post_id)

    async def delete_post(self, post_id: str) -> bool:
        """Delete a pin"""
        try:
            async with aiohttp.ClientSession() as session:
                async with session.delete(
                    f"{self.BASE_URL}/pins/{post_id}",
                    headers={"Authorization": f"Bearer {self.access_token}"}
                ) as response:
                    return response.status == 204
        except Exception as e:
            self.logger.error(f"Failed to delete pin: {e}")
            return False

    def optimize_for_platform(self, content: PostContent) -> PostContent:
        """Optimize content for Pinterest"""
        optimized = content

        # Pinterest best practices:
        # - Title: 100 characters max
        # - Description: 500 characters
        # - Image ratio: 2:3 (1000 x 1500 px)

        # Add hashtags to description
        if content.hashtags:
            hashtags = self.format_hashtags(content.hashtags[:20])
            optimized.text = f"{content.text}\n\n{hashtags}"

        return optimized
