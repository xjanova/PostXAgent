"""
AI Content Generator Service
Supports multiple AI providers (both paid and free)
"""
import aiohttp
import asyncio
from typing import Dict, Optional, List
from abc import ABC, abstractmethod
from dataclasses import dataclass
import logging
import json

from config.settings import settings, AIProvider

logger = logging.getLogger(__name__)


@dataclass
class GeneratedContent:
    """Generated content result"""
    text: str
    hashtags: List[str]
    provider: str
    tokens_used: int = 0
    cost: float = 0.0


class BaseTextProvider(ABC):
    """Abstract base class for text generation providers"""

    @abstractmethod
    async def generate(self, prompt: str, **kwargs) -> GeneratedContent:
        pass


class OpenAIProvider(BaseTextProvider):
    """OpenAI GPT Provider (Paid)"""

    BASE_URL = "https://api.openai.com/v1"

    def __init__(self):
        self.api_key = settings.ai_service.openai_api_key
        self.model = settings.ai_service.openai_model

    async def generate(self, prompt: str, **kwargs) -> GeneratedContent:
        async with aiohttp.ClientSession() as session:
            system_prompt = self._build_system_prompt(kwargs)

            async with session.post(
                f"{self.BASE_URL}/chat/completions",
                headers={
                    "Authorization": f"Bearer {self.api_key}",
                    "Content-Type": "application/json",
                },
                json={
                    "model": self.model,
                    "messages": [
                        {"role": "system", "content": system_prompt},
                        {"role": "user", "content": prompt}
                    ],
                    "temperature": 0.7,
                    "max_tokens": kwargs.get("max_tokens", 1000),
                }
            ) as response:
                result = await response.json()

                if "error" in result:
                    raise Exception(result["error"]["message"])

                content = result["choices"][0]["message"]["content"]
                usage = result.get("usage", {})

                return GeneratedContent(
                    text=self._extract_text(content),
                    hashtags=self._extract_hashtags(content),
                    provider="openai",
                    tokens_used=usage.get("total_tokens", 0),
                    cost=self._calculate_cost(usage),
                )

    def _build_system_prompt(self, kwargs: Dict) -> str:
        platform = kwargs.get("platform", "general")
        content_type = kwargs.get("content_type", "promotional")
        language = kwargs.get("language", "th")
        brand_info = kwargs.get("brand_info", {})

        lang_instruction = "ตอบเป็นภาษาไทย" if language == "th" else f"Respond in {language}"

        return f"""You are an expert social media content creator for {platform}.
Create {content_type} content that is engaging and optimized for the platform.

Brand Information:
- Name: {brand_info.get('name', 'N/A')}
- Industry: {brand_info.get('industry', 'N/A')}
- Target Audience: {brand_info.get('target_audience', 'General')}
- Tone: {brand_info.get('tone', 'Professional yet friendly')}

{lang_instruction}

Include relevant hashtags at the end.
Format:
[Main content here]

#hashtag1 #hashtag2 #hashtag3"""

    def _extract_text(self, content: str) -> str:
        # Remove hashtags section for separate processing
        lines = content.split('\n')
        text_lines = [line for line in lines if not line.strip().startswith('#') or len(line.strip()) > 50]
        return '\n'.join(text_lines).strip()

    def _extract_hashtags(self, content: str) -> List[str]:
        import re
        hashtags = re.findall(r'#(\w+)', content)
        return hashtags[:20]

    def _calculate_cost(self, usage: Dict) -> float:
        # GPT-4 Turbo pricing (approximate)
        input_cost = usage.get("prompt_tokens", 0) * 0.00001
        output_cost = usage.get("completion_tokens", 0) * 0.00003
        return input_cost + output_cost


class AnthropicProvider(BaseTextProvider):
    """Anthropic Claude Provider (Paid)"""

    BASE_URL = "https://api.anthropic.com/v1"

    def __init__(self):
        self.api_key = settings.ai_service.anthropic_api_key
        self.model = settings.ai_service.anthropic_model

    async def generate(self, prompt: str, **kwargs) -> GeneratedContent:
        async with aiohttp.ClientSession() as session:
            system_prompt = self._build_system_prompt(kwargs)

            async with session.post(
                f"{self.BASE_URL}/messages",
                headers={
                    "x-api-key": self.api_key,
                    "anthropic-version": "2023-06-01",
                    "Content-Type": "application/json",
                },
                json={
                    "model": self.model,
                    "max_tokens": kwargs.get("max_tokens", 1000),
                    "system": system_prompt,
                    "messages": [
                        {"role": "user", "content": prompt}
                    ],
                }
            ) as response:
                result = await response.json()

                if "error" in result:
                    raise Exception(result["error"]["message"])

                content = result["content"][0]["text"]
                usage = result.get("usage", {})

                return GeneratedContent(
                    text=self._extract_text(content),
                    hashtags=self._extract_hashtags(content),
                    provider="anthropic",
                    tokens_used=usage.get("input_tokens", 0) + usage.get("output_tokens", 0),
                    cost=self._calculate_cost(usage),
                )

    def _build_system_prompt(self, kwargs: Dict) -> str:
        platform = kwargs.get("platform", "general")
        content_type = kwargs.get("content_type", "promotional")
        language = kwargs.get("language", "th")
        brand_info = kwargs.get("brand_info", {})

        lang_instruction = "ตอบเป็นภาษาไทย" if language == "th" else f"Respond in {language}"

        return f"""You are an expert social media content creator for {platform}.
Create {content_type} content that is engaging and optimized for the platform.

Brand Information:
- Name: {brand_info.get('name', 'N/A')}
- Industry: {brand_info.get('industry', 'N/A')}
- Target Audience: {brand_info.get('target_audience', 'General')}
- Tone: {brand_info.get('tone', 'Professional yet friendly')}

{lang_instruction}

Include relevant hashtags at the end."""

    def _extract_text(self, content: str) -> str:
        lines = content.split('\n')
        text_lines = [line for line in lines if not line.strip().startswith('#') or len(line.strip()) > 50]
        return '\n'.join(text_lines).strip()

    def _extract_hashtags(self, content: str) -> List[str]:
        import re
        hashtags = re.findall(r'#(\w+)', content)
        return hashtags[:20]

    def _calculate_cost(self, usage: Dict) -> float:
        # Claude 3 Opus pricing (approximate)
        input_cost = usage.get("input_tokens", 0) * 0.000015
        output_cost = usage.get("output_tokens", 0) * 0.000075
        return input_cost + output_cost


class GoogleGeminiProvider(BaseTextProvider):
    """Google Gemini Provider (Free tier available)"""

    BASE_URL = "https://generativelanguage.googleapis.com/v1beta"

    def __init__(self):
        self.api_key = settings.ai_service.google_api_key
        self.model = settings.ai_service.google_model

    async def generate(self, prompt: str, **kwargs) -> GeneratedContent:
        async with aiohttp.ClientSession() as session:
            system_prompt = self._build_system_prompt(kwargs)
            full_prompt = f"{system_prompt}\n\nUser request: {prompt}"

            async with session.post(
                f"{self.BASE_URL}/models/{self.model}:generateContent",
                params={"key": self.api_key},
                headers={"Content-Type": "application/json"},
                json={
                    "contents": [{
                        "parts": [{"text": full_prompt}]
                    }],
                    "generationConfig": {
                        "temperature": 0.7,
                        "maxOutputTokens": kwargs.get("max_tokens", 1000),
                    }
                }
            ) as response:
                result = await response.json()

                if "error" in result:
                    raise Exception(result["error"]["message"])

                content = result["candidates"][0]["content"]["parts"][0]["text"]

                return GeneratedContent(
                    text=self._extract_text(content),
                    hashtags=self._extract_hashtags(content),
                    provider="google",
                    tokens_used=0,  # Gemini doesn't return token count in free tier
                    cost=0.0,  # Free tier
                )

    def _build_system_prompt(self, kwargs: Dict) -> str:
        platform = kwargs.get("platform", "general")
        content_type = kwargs.get("content_type", "promotional")
        language = kwargs.get("language", "th")
        brand_info = kwargs.get("brand_info", {})

        lang_instruction = "ตอบเป็นภาษาไทย" if language == "th" else f"Respond in {language}"

        return f"""You are an expert social media content creator for {platform}.
Create {content_type} content. {lang_instruction}

Brand: {brand_info.get('name', 'N/A')}
Industry: {brand_info.get('industry', 'N/A')}

Include relevant hashtags."""

    def _extract_text(self, content: str) -> str:
        lines = content.split('\n')
        text_lines = [line for line in lines if not line.strip().startswith('#') or len(line.strip()) > 50]
        return '\n'.join(text_lines).strip()

    def _extract_hashtags(self, content: str) -> List[str]:
        import re
        hashtags = re.findall(r'#(\w+)', content)
        return hashtags[:20]


class OllamaProvider(BaseTextProvider):
    """Ollama Local LLM Provider (Free - Self-hosted)"""

    def __init__(self):
        self.base_url = settings.ai_service.ollama_base_url
        self.model = settings.ai_service.ollama_model

    async def generate(self, prompt: str, **kwargs) -> GeneratedContent:
        async with aiohttp.ClientSession() as session:
            system_prompt = self._build_system_prompt(kwargs)

            async with session.post(
                f"{self.base_url}/api/chat",
                json={
                    "model": self.model,
                    "messages": [
                        {"role": "system", "content": system_prompt},
                        {"role": "user", "content": prompt}
                    ],
                    "stream": False,
                }
            ) as response:
                result = await response.json()
                content = result["message"]["content"]

                return GeneratedContent(
                    text=self._extract_text(content),
                    hashtags=self._extract_hashtags(content),
                    provider="ollama",
                    tokens_used=0,
                    cost=0.0,  # Free - self-hosted
                )

    def _build_system_prompt(self, kwargs: Dict) -> str:
        platform = kwargs.get("platform", "general")
        content_type = kwargs.get("content_type", "promotional")
        language = kwargs.get("language", "th")
        brand_info = kwargs.get("brand_info", {})

        lang_instruction = "ตอบเป็นภาษาไทย" if language == "th" else f"Respond in {language}"

        return f"""You are a social media content creator for {platform}.
Create {content_type} content. {lang_instruction}

Brand: {brand_info.get('name', 'N/A')}

Include relevant hashtags."""

    def _extract_text(self, content: str) -> str:
        lines = content.split('\n')
        text_lines = [line for line in lines if not line.strip().startswith('#') or len(line.strip()) > 50]
        return '\n'.join(text_lines).strip()

    def _extract_hashtags(self, content: str) -> List[str]:
        import re
        hashtags = re.findall(r'#(\w+)', content)
        return hashtags[:20]


class ContentGenerator:
    """
    Main content generator that manages multiple AI providers
    Automatically selects the best available provider
    """

    PROVIDERS = {
        AIProvider.OPENAI: OpenAIProvider,
        AIProvider.ANTHROPIC: AnthropicProvider,
        AIProvider.GOOGLE: GoogleGeminiProvider,
        AIProvider.OLLAMA: OllamaProvider,
    }

    # Priority order: free first, then paid
    FREE_PROVIDERS = [AIProvider.OLLAMA, AIProvider.GOOGLE]
    PAID_PROVIDERS = [AIProvider.OPENAI, AIProvider.ANTHROPIC]

    def __init__(self, prefer_free: bool = True):
        self.prefer_free = prefer_free
        self._providers: Dict[AIProvider, BaseTextProvider] = {}

    def _get_provider(self, provider_type: AIProvider) -> BaseTextProvider:
        if provider_type not in self._providers:
            provider_class = self.PROVIDERS.get(provider_type)
            if provider_class:
                self._providers[provider_type] = provider_class()
        return self._providers.get(provider_type)

    async def generate_text(
        self,
        prompt: str,
        brand_info: Dict = None,
        platform: str = "general",
        content_type: str = "promotional",
        language: str = "th",
        provider: AIProvider = None,
        max_tokens: int = 1000,
    ) -> GeneratedContent:
        """
        Generate text content using available AI providers

        Args:
            prompt: The content prompt
            brand_info: Brand information dict
            platform: Target social media platform
            content_type: Type of content (promotional, educational, etc.)
            language: Output language (th, en, etc.)
            provider: Specific provider to use (optional)
            max_tokens: Maximum tokens to generate
        """
        kwargs = {
            "brand_info": brand_info or {},
            "platform": platform,
            "content_type": content_type,
            "language": language,
            "max_tokens": max_tokens,
        }

        # If specific provider requested
        if provider:
            provider_instance = self._get_provider(provider)
            if provider_instance:
                try:
                    return await provider_instance.generate(prompt, **kwargs)
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
                    result = await provider_instance.generate(prompt, **kwargs)
                    logger.info(f"Content generated using {provider_type.value}")
                    return result
                except Exception as e:
                    logger.warning(f"Provider {provider_type} failed: {e}, trying next...")
                    continue

        raise Exception("All AI providers failed to generate content")

    async def generate_variations(
        self,
        prompt: str,
        count: int = 3,
        **kwargs
    ) -> List[GeneratedContent]:
        """Generate multiple variations of content"""
        tasks = [self.generate_text(f"{prompt}\n\nVariation {i+1}:", **kwargs)
                 for i in range(count)]
        return await asyncio.gather(*tasks)
