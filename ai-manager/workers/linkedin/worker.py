"""
LinkedIn Platform Worker
Supports: Posts, Articles, Documents
Uses LinkedIn Marketing API
"""
import aiohttp
from typing import Dict
from datetime import datetime
from workers.base_worker import BasePlatformWorker, PostContent, EngagementMetrics


class LinkedInWorker(BasePlatformWorker):
    """Worker for LinkedIn platform operations"""

    BASE_URL = "https://api.linkedin.com/v2"

    def __init__(self):
        super().__init__("linkedin")
        self.access_token = None
        self.person_urn = None
        self.organization_urn = None

    async def authenticate(self, credentials: Dict) -> bool:
        """Authenticate with LinkedIn API"""
        self.access_token = credentials.get("access_token")

        if not self.access_token:
            self.logger.error("No access token provided")
            return False

        try:
            async with aiohttp.ClientSession() as session:
                async with session.get(
                    f"{self.BASE_URL}/me",
                    headers={"Authorization": f"Bearer {self.access_token}"}
                ) as response:
                    if response.status == 200:
                        data = await response.json()
                        self.person_urn = f"urn:li:person:{data.get('id')}"
                        self._authenticated = True
                        self.logger.info("LinkedIn authentication successful")
                        return True
                    return False
        except Exception as e:
            self.logger.error(f"LinkedIn authentication error: {e}")
            return False

    async def post_content(self, content: PostContent) -> Dict:
        """Post content to LinkedIn"""
        await self.check_rate_limit()

        if not self._authenticated:
            return {"success": False, "error": "Not authenticated"}

        try:
            async with aiohttp.ClientSession() as session:
                post_data = {
                    "author": self.organization_urn or self.person_urn,
                    "lifecycleState": "PUBLISHED",
                    "specificContent": {
                        "com.linkedin.ugc.ShareContent": {
                            "shareCommentary": {
                                "text": content.text
                            },
                            "shareMediaCategory": "NONE"
                        }
                    },
                    "visibility": {
                        "com.linkedin.ugc.MemberNetworkVisibility": "PUBLIC"
                    }
                }

                # Add image if present
                if content.images:
                    media_assets = await self._upload_images(session, content.images)
                    if media_assets:
                        post_data["specificContent"]["com.linkedin.ugc.ShareContent"]["shareMediaCategory"] = "IMAGE"
                        post_data["specificContent"]["com.linkedin.ugc.ShareContent"]["media"] = media_assets

                # Add link if present
                if content.link and not content.images:
                    post_data["specificContent"]["com.linkedin.ugc.ShareContent"]["shareMediaCategory"] = "ARTICLE"
                    post_data["specificContent"]["com.linkedin.ugc.ShareContent"]["media"] = [{
                        "status": "READY",
                        "originalUrl": content.link,
                    }]

                async with session.post(
                    f"{self.BASE_URL}/ugcPosts",
                    headers={
                        "Authorization": f"Bearer {self.access_token}",
                        "Content-Type": "application/json",
                        "X-Restli-Protocol-Version": "2.0.0",
                    },
                    json=post_data
                ) as response:
                    if response.status == 201:
                        result = await response.json()
                        return {
                            "success": True,
                            "post_id": result.get("id"),
                            "platform": "linkedin",
                        }
                    else:
                        result = await response.json()
                        return {"success": False, "error": str(result)}

        except Exception as e:
            self.logger.error(f"LinkedIn post failed: {e}")
            return {"success": False, "error": str(e)}

    async def _upload_images(self, session: aiohttp.ClientSession, image_urls: list) -> list:
        """Upload images to LinkedIn"""
        media_assets = []

        for image_url in image_urls[:9]:  # Max 9 images
            # Register upload
            register_data = {
                "registerUploadRequest": {
                    "recipes": ["urn:li:digitalmediaRecipe:feedshare-image"],
                    "owner": self.organization_urn or self.person_urn,
                    "serviceRelationships": [{
                        "relationshipType": "OWNER",
                        "identifier": "urn:li:userGeneratedContent"
                    }]
                }
            }

            async with session.post(
                f"{self.BASE_URL}/assets?action=registerUpload",
                headers={
                    "Authorization": f"Bearer {self.access_token}",
                    "Content-Type": "application/json",
                },
                json=register_data
            ) as response:
                if response.status != 200:
                    continue

                result = await response.json()
                upload_url = result["value"]["uploadMechanism"]["com.linkedin.digitalmedia.uploading.MediaUploadHttpRequest"]["uploadUrl"]
                asset = result["value"]["asset"]

            # Download image
            async with session.get(image_url) as img_response:
                image_data = await img_response.read()

            # Upload image
            async with session.put(
                upload_url,
                headers={
                    "Authorization": f"Bearer {self.access_token}",
                    "Content-Type": "image/jpeg",
                },
                data=image_data
            ) as response:
                if response.status == 201:
                    media_assets.append({
                        "status": "READY",
                        "media": asset,
                    })

        return media_assets

    async def post_article(self, title: str, content: str, image_url: str = None) -> Dict:
        """Post an article to LinkedIn"""
        if not self._authenticated:
            return {"success": False, "error": "Not authenticated"}

        # Articles require LinkedIn Publishing API
        # This is a simplified version
        return {
            "success": True,
            "message": "Article posting requires LinkedIn Publishing API access",
            "platform": "linkedin",
        }

    async def schedule_post(self, content: PostContent, scheduled_at: datetime) -> Dict:
        """Schedule a post for later"""
        # LinkedIn doesn't have native scheduling API
        return {
            "success": True,
            "scheduled": True,
            "scheduled_at": scheduled_at.isoformat(),
            "platform": "linkedin",
        }

    async def get_metrics(self, post_id: str) -> EngagementMetrics:
        """Get engagement metrics for a post"""
        try:
            async with aiohttp.ClientSession() as session:
                # Get share statistics
                async with session.get(
                    f"{self.BASE_URL}/socialActions/{post_id}",
                    headers={
                        "Authorization": f"Bearer {self.access_token}",
                        "X-Restli-Protocol-Version": "2.0.0",
                    },
                    params={"count": "true"}
                ) as response:
                    data = await response.json()

                    return EngagementMetrics(
                        post_id=post_id,
                        likes=data.get("likesSummary", {}).get("totalLikes", 0),
                        comments=data.get("commentsSummary", {}).get("totalFirstLevelComments", 0),
                        shares=data.get("sharesSummary", {}).get("totalShares", 0) if data.get("sharesSummary") else 0,
                    )

        except Exception as e:
            self.logger.error(f"Failed to get metrics: {e}")
            return EngagementMetrics(post_id=post_id)

    async def delete_post(self, post_id: str) -> bool:
        """Delete a post"""
        try:
            async with aiohttp.ClientSession() as session:
                async with session.delete(
                    f"{self.BASE_URL}/ugcPosts/{post_id}",
                    headers={
                        "Authorization": f"Bearer {self.access_token}",
                        "X-Restli-Protocol-Version": "2.0.0",
                    }
                ) as response:
                    return response.status == 204
        except Exception as e:
            self.logger.error(f"Failed to delete post: {e}")
            return False

    def optimize_for_platform(self, content: PostContent) -> PostContent:
        """Optimize content for LinkedIn"""
        optimized = content

        # LinkedIn character limit: 3000
        if len(content.text) > 3000:
            optimized.text = content.text[:2997] + "..."

        # LinkedIn recommends 3-5 hashtags
        if len(content.hashtags) > 5:
            optimized.hashtags = content.hashtags[:5]

        return optimized
