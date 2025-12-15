"""
AI Image Generator Service
Supports multiple image generation providers (both paid and free)
"""
import aiohttp
import asyncio
import base64
from typing import Dict, Optional, List
from abc import ABC, abstractmethod
from dataclasses import dataclass
import logging
from io import BytesIO

from config.settings import settings, AIProvider

logger = logging.getLogger(__name__)


@dataclass
class GeneratedImage:
    """Generated image result"""
    url: str
    base64_data: Optional[str] = None
    provider: str = ""
    width: int = 0
    height: int = 0
    cost: float = 0.0


class BaseImageProvider(ABC):
    """Abstract base class for image generation providers"""

    @abstractmethod
    async def generate(self, prompt: str, **kwargs) -> GeneratedImage:
        pass


class DALLEProvider(BaseImageProvider):
    """OpenAI DALL-E Provider (Paid)"""

    BASE_URL = "https://api.openai.com/v1"

    def __init__(self):
        self.api_key = settings.ai_service.openai_api_key
        self.model = settings.ai_service.dalle_model

    async def generate(self, prompt: str, **kwargs) -> GeneratedImage:
        size = kwargs.get("size", "1024x1024")
        style = kwargs.get("style", "vivid")

        async with aiohttp.ClientSession() as session:
            async with session.post(
                f"{self.BASE_URL}/images/generations",
                headers={
                    "Authorization": f"Bearer {self.api_key}",
                    "Content-Type": "application/json",
                },
                json={
                    "model": self.model,
                    "prompt": self._enhance_prompt(prompt, kwargs),
                    "n": 1,
                    "size": size,
                    "quality": kwargs.get("quality", "standard"),
                    "style": style,
                }
            ) as response:
                result = await response.json()

                if "error" in result:
                    raise Exception(result["error"]["message"])

                image_data = result["data"][0]
                width, height = map(int, size.split("x"))

                return GeneratedImage(
                    url=image_data.get("url", ""),
                    base64_data=image_data.get("b64_json"),
                    provider="dalle",
                    width=width,
                    height=height,
                    cost=self._calculate_cost(size, kwargs.get("quality", "standard")),
                )

    def _enhance_prompt(self, prompt: str, kwargs: Dict) -> str:
        style = kwargs.get("image_style", "modern, professional")
        brand_info = kwargs.get("brand_info", {})
        brand_name = brand_info.get("name", "")

        enhanced = f"{prompt}"
        if brand_name:
            enhanced = f"Brand: {brand_name}. {enhanced}"
        enhanced = f"{enhanced}. Style: {style}. High quality, suitable for social media marketing."

        return enhanced

    def _calculate_cost(self, size: str, quality: str) -> float:
        # DALL-E 3 pricing
        pricing = {
            ("1024x1024", "standard"): 0.04,
            ("1024x1024", "hd"): 0.08,
            ("1024x1792", "standard"): 0.08,
            ("1024x1792", "hd"): 0.12,
            ("1792x1024", "standard"): 0.08,
            ("1792x1024", "hd"): 0.12,
        }
        return pricing.get((size, quality), 0.04)


class StableDiffusionProvider(BaseImageProvider):
    """Stable Diffusion Provider (Free - Self-hosted)"""

    def __init__(self):
        self.api_url = settings.ai_service.sd_api_url

    async def generate(self, prompt: str, **kwargs) -> GeneratedImage:
        size = kwargs.get("size", "1024x1024")
        width, height = map(int, size.split("x"))

        async with aiohttp.ClientSession() as session:
            async with session.post(
                f"{self.api_url}/sdapi/v1/txt2img",
                json={
                    "prompt": self._enhance_prompt(prompt, kwargs),
                    "negative_prompt": kwargs.get("negative_prompt", "blurry, low quality, distorted"),
                    "width": width,
                    "height": height,
                    "steps": kwargs.get("steps", 30),
                    "cfg_scale": kwargs.get("cfg_scale", 7),
                    "sampler_name": kwargs.get("sampler", "DPM++ 2M Karras"),
                }
            ) as response:
                result = await response.json()

                if not result.get("images"):
                    raise Exception("No image generated")

                base64_image = result["images"][0]

                return GeneratedImage(
                    url="",  # SD returns base64, not URL
                    base64_data=base64_image,
                    provider="stable_diffusion",
                    width=width,
                    height=height,
                    cost=0.0,  # Free - self-hosted
                )

    def _enhance_prompt(self, prompt: str, kwargs: Dict) -> str:
        style = kwargs.get("image_style", "modern")
        quality_tags = "masterpiece, best quality, highly detailed, sharp focus"
        return f"{quality_tags}, {prompt}, {style} style"


class LeonardoProvider(BaseImageProvider):
    """Leonardo.ai Provider (Free tier available)"""

    BASE_URL = "https://cloud.leonardo.ai/api/rest/v1"

    def __init__(self):
        self.api_key = settings.ai_service.leonardo_api_key

    async def generate(self, prompt: str, **kwargs) -> GeneratedImage:
        size = kwargs.get("size", "1024x1024")
        width, height = map(int, size.split("x"))

        async with aiohttp.ClientSession() as session:
            # Create generation
            async with session.post(
                f"{self.BASE_URL}/generations",
                headers={
                    "Authorization": f"Bearer {self.api_key}",
                    "Content-Type": "application/json",
                },
                json={
                    "prompt": self._enhance_prompt(prompt, kwargs),
                    "negative_prompt": "blurry, low quality",
                    "width": width,
                    "height": height,
                    "num_images": 1,
                    "guidance_scale": 7,
                    "modelId": kwargs.get("model_id", "6bef9f1b-29cb-40c7-b9df-32b51c1f67d3"),  # Leonardo Creative
                }
            ) as response:
                result = await response.json()

                if "error" in result:
                    raise Exception(str(result["error"]))

                generation_id = result["sdGenerationJob"]["generationId"]

            # Wait for generation to complete
            image_url = await self._wait_for_generation(session, generation_id)

            return GeneratedImage(
                url=image_url,
                provider="leonardo",
                width=width,
                height=height,
                cost=0.0,  # Free tier
            )

    async def _wait_for_generation(self, session: aiohttp.ClientSession, generation_id: str, max_attempts: int = 30) -> str:
        for _ in range(max_attempts):
            async with session.get(
                f"{self.BASE_URL}/generations/{generation_id}",
                headers={"Authorization": f"Bearer {self.api_key}"}
            ) as response:
                result = await response.json()
                generation = result.get("generations_by_pk", {})

                if generation.get("status") == "COMPLETE":
                    images = generation.get("generated_images", [])
                    if images:
                        return images[0].get("url", "")

            await asyncio.sleep(2)

        raise Exception("Generation timeout")

    def _enhance_prompt(self, prompt: str, kwargs: Dict) -> str:
        style = kwargs.get("image_style", "modern, professional")
        return f"{prompt}, {style}, high quality, marketing material"


class BingImageProvider(BaseImageProvider):
    """Bing Image Creator Provider (Free)"""

    # Note: Bing Image Creator doesn't have an official API
    # This is a placeholder for when API becomes available
    # Currently requires browser automation

    async def generate(self, prompt: str, **kwargs) -> GeneratedImage:
        # This would require browser automation (Playwright/Selenium)
        # For now, we'll raise an error directing to use other providers
        raise NotImplementedError(
            "Bing Image Creator requires browser automation. "
            "Consider using Stable Diffusion (free) or Leonardo.ai (free tier)."
        )


class ImageGenerator:
    """
    Main image generator that manages multiple AI providers
    Automatically selects the best available provider
    """

    PROVIDERS = {
        AIProvider.DALLE: DALLEProvider,
        AIProvider.STABLE_DIFFUSION: StableDiffusionProvider,
        AIProvider.LEONARDO: LeonardoProvider,
        AIProvider.BING_IMAGE: BingImageProvider,
    }

    # Priority order: free first, then paid
    FREE_PROVIDERS = [AIProvider.STABLE_DIFFUSION, AIProvider.LEONARDO]
    PAID_PROVIDERS = [AIProvider.DALLE]

    def __init__(self, prefer_free: bool = True):
        self.prefer_free = prefer_free
        self._providers: Dict[AIProvider, BaseImageProvider] = {}

    def _get_provider(self, provider_type: AIProvider) -> BaseImageProvider:
        if provider_type not in self._providers:
            provider_class = self.PROVIDERS.get(provider_type)
            if provider_class:
                self._providers[provider_type] = provider_class()
        return self._providers.get(provider_type)

    async def generate_image(
        self,
        prompt: str,
        style: str = "modern",
        size: str = "1024x1024",
        provider: str = "auto",
        brand_info: Dict = None,
        **kwargs
    ) -> GeneratedImage:
        """
        Generate image using available AI providers

        Args:
            prompt: The image generation prompt
            style: Image style (modern, vintage, minimalist, etc.)
            size: Image dimensions (e.g., "1024x1024")
            provider: Specific provider or "auto" for automatic selection
            brand_info: Brand information for customization
        """
        generation_kwargs = {
            "size": size,
            "image_style": style,
            "brand_info": brand_info or {},
            **kwargs
        }

        # Map string provider to enum
        provider_map = {
            "dalle": AIProvider.DALLE,
            "sd": AIProvider.STABLE_DIFFUSION,
            "stable_diffusion": AIProvider.STABLE_DIFFUSION,
            "leonardo": AIProvider.LEONARDO,
            "bing": AIProvider.BING_IMAGE,
        }

        if provider != "auto" and provider in provider_map:
            provider_enum = provider_map[provider]
            provider_instance = self._get_provider(provider_enum)
            if provider_instance:
                try:
                    return await provider_instance.generate(prompt, **generation_kwargs)
                except Exception as e:
                    logger.error(f"Provider {provider} failed: {e}")
                    raise

        # Auto-select provider based on preference
        providers_to_try = self.FREE_PROVIDERS + self.PAID_PROVIDERS if self.prefer_free \
            else self.PAID_PROVIDERS + self.FREE_PROVIDERS

        for provider_type in providers_to_try:
            provider_instance = self._get_provider(provider_type)
            if provider_instance:
                try:
                    result = await provider_instance.generate(prompt, **generation_kwargs)
                    logger.info(f"Image generated using {provider_type.value}")
                    return result
                except NotImplementedError:
                    continue
                except Exception as e:
                    logger.warning(f"Provider {provider_type} failed: {e}, trying next...")
                    continue

        raise Exception("All image providers failed to generate image")

    async def generate_for_platforms(
        self,
        prompt: str,
        platforms: List[str],
        **kwargs
    ) -> Dict[str, GeneratedImage]:
        """Generate optimized images for multiple platforms"""

        # Platform-specific image sizes
        platform_sizes = {
            "facebook": "1200x630",
            "instagram": "1080x1080",
            "instagram_story": "1080x1920",
            "twitter": "1200x675",
            "linkedin": "1200x627",
            "pinterest": "1000x1500",
            "tiktok": "1080x1920",
            "youtube": "1280x720",
            "threads": "1080x1080",
        }

        results = {}
        tasks = []

        for platform in platforms:
            size = platform_sizes.get(platform, "1024x1024")
            task = self.generate_image(prompt, size=size, **kwargs)
            tasks.append((platform, task))

        for platform, task in tasks:
            try:
                results[platform] = await task
            except Exception as e:
                logger.error(f"Failed to generate image for {platform}: {e}")
                results[platform] = None

        return results

    async def save_image(self, image: GeneratedImage, filepath: str) -> bool:
        """Save generated image to file"""
        try:
            if image.base64_data:
                import base64
                image_data = base64.b64decode(image.base64_data)
                with open(filepath, "wb") as f:
                    f.write(image_data)
                return True

            elif image.url:
                async with aiohttp.ClientSession() as session:
                    async with session.get(image.url) as response:
                        image_data = await response.read()
                        with open(filepath, "wb") as f:
                            f.write(image_data)
                        return True

            return False

        except Exception as e:
            logger.error(f"Failed to save image: {e}")
            return False

    def get_image_data_uri(self, image: GeneratedImage) -> str:
        """Get image as data URI for embedding"""
        if image.base64_data:
            return f"data:image/png;base64,{image.base64_data}"
        return image.url
