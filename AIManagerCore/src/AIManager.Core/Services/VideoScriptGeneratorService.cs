using System.Text.Json;
using AIManager.Core.Models;
using Microsoft.Extensions.Logging;

namespace AIManager.Core.Services;

/// <summary>
/// Video Script Generator Service
/// ใช้ AI สร้าง Video Script จาก Content Concept
/// </summary>
public class VideoScriptGeneratorService
{
    private readonly ContentGeneratorService _contentGenerator;
    private readonly ILogger<VideoScriptGeneratorService>? _logger;

    public VideoScriptGeneratorService(
        ContentGeneratorService contentGenerator,
        ILogger<VideoScriptGeneratorService>? logger = null)
    {
        _contentGenerator = contentGenerator;
        _logger = logger;
    }

    /// <summary>
    /// สร้าง Video Script จาก Content Concept
    /// </summary>
    public async Task<VideoScript> GenerateScriptAsync(
        ContentConcept concept,
        UserPackage package,
        CancellationToken ct = default)
    {
        _logger?.LogInformation("Generating video script for topic: {Topic}", concept.Topic);

        // Calculate number of scenes based on duration
        var targetDuration = Math.Min(concept.TargetDurationSeconds, package.MaxVideoDurationSeconds);
        var sceneDuration = 5.0; // Average seconds per scene
        var numScenes = Math.Min((int)Math.Ceiling(targetDuration / sceneDuration), package.MaxImagesPerVideo);

        var prompt = BuildScriptPrompt(concept, numScenes, targetDuration);

        var result = await _contentGenerator.GenerateAsync(
            prompt,
            concept.BrandInfo,
            "youtube", // Use YouTube format as base
            concept.Language,
            ct);

        var script = ParseScriptFromResponse(result.Text, concept, numScenes, targetDuration);
        script.ProviderUsed = result.Provider;

        _logger?.LogInformation("Generated script with {SceneCount} scenes, total duration: {Duration}s",
            script.Scenes.Count, script.TotalDurationSeconds);

        return script;
    }

    private string BuildScriptPrompt(ContentConcept concept, int numScenes, int targetDuration)
    {
        var isThai = concept.Language == "th";

        if (isThai)
        {
            return $@"สร้าง Video Script สำหรับหัวข้อ: {concept.Topic}

รายละเอียด: {concept.Description}
กลุ่มเป้าหมาย: {concept.TargetAudience}
โทน: {concept.Tone}
สไตล์: {concept.VideoStyle}
ความยาวเป้าหมาย: {targetDuration} วินาที
จำนวนฉาก: {numScenes} ฉาก

{(concept.ProductInfo != null ? $@"
ข้อมูลสินค้า:
- ชื่อ: {concept.ProductInfo.Name}
- รายละเอียด: {concept.ProductInfo.Description}
- ราคา: {concept.ProductInfo.Price}
- จุดเด่น: {string.Join(", ", concept.ProductInfo.Features)}
" : "")}

{(concept.CallToAction != null ? $"Call to Action: {concept.CallToAction}" : "")}

กรุณาสร้างสคริปต์ในรูปแบบ JSON ดังนี้:
{{
    ""title"": ""ชื่อวิดีโอ"",
    ""hook"": ""ประโยคเปิดที่ดึงดูดความสนใจ"",
    ""scenes"": [
        {{
            ""scene_number"": 1,
            ""duration_seconds"": 5,
            ""narration"": ""คำบรรยาย/เสียงพูด"",
            ""visual_description"": ""อธิบายภาพที่ควรแสดง"",
            ""image_prompt"": ""คำสั่งสำหรับสร้างภาพด้วย AI"",
            ""text_overlay"": ""ข้อความที่แสดงบนหน้าจอ"",
            ""transition"": ""fade/slide/zoom""
        }}
    ],
    ""call_to_action"": ""ประโยคเชิญชวนปิดท้าย"",
    ""hashtags"": [""hashtag1"", ""hashtag2""]
}}

สำคัญ:
- ตอบเป็น JSON เท่านั้น ไม่ต้องมีคำอธิบายอื่น
- image_prompt ต้องเป็นภาษาอังกฤษ สำหรับ AI สร้างภาพ
- แต่ละฉากควรมีความยาว 3-7 วินาที
- เนื้อหาต้องน่าสนใจ กระชับ เหมาะกับ social media";
        }

        return $@"Create a Video Script for topic: {concept.Topic}

Description: {concept.Description}
Target Audience: {concept.TargetAudience}
Tone: {concept.Tone}
Style: {concept.VideoStyle}
Target Duration: {targetDuration} seconds
Number of Scenes: {numScenes}

{(concept.ProductInfo != null ? $@"
Product Info:
- Name: {concept.ProductInfo.Name}
- Description: {concept.ProductInfo.Description}
- Price: {concept.ProductInfo.Price}
- Features: {string.Join(", ", concept.ProductInfo.Features)}
" : "")}

{(concept.CallToAction != null ? $"Call to Action: {concept.CallToAction}" : "")}

Please create the script in JSON format:
{{
    ""title"": ""Video title"",
    ""hook"": ""Opening hook to grab attention"",
    ""scenes"": [
        {{
            ""scene_number"": 1,
            ""duration_seconds"": 5,
            ""narration"": ""Voice-over/narration"",
            ""visual_description"": ""Description of visual"",
            ""image_prompt"": ""Prompt for AI image generation"",
            ""text_overlay"": ""Text to display on screen"",
            ""transition"": ""fade/slide/zoom""
        }}
    ],
    ""call_to_action"": ""Closing CTA"",
    ""hashtags"": [""hashtag1"", ""hashtag2""]
}}

Important:
- Reply with JSON only, no other text
- Each scene should be 3-7 seconds
- Make content engaging and social media friendly";
    }

    private VideoScript ParseScriptFromResponse(string response, ContentConcept concept, int numScenes, int targetDuration)
    {
        try
        {
            // Try to extract JSON from response
            var jsonStart = response.IndexOf('{');
            var jsonEnd = response.LastIndexOf('}');

            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var jsonStr = response.Substring(jsonStart, jsonEnd - jsonStart + 1);
                var json = JsonDocument.Parse(jsonStr);
                var root = json.RootElement;

                var script = new VideoScript
                {
                    Title = root.TryGetProperty("title", out var title) ? title.GetString() ?? concept.Topic : concept.Topic,
                    Hook = root.TryGetProperty("hook", out var hook) ? hook.GetString() ?? "" : "",
                    CallToAction = root.TryGetProperty("call_to_action", out var cta) ? cta.GetString() ?? "" : concept.CallToAction ?? ""
                };

                // Parse scenes
                if (root.TryGetProperty("scenes", out var scenesArray))
                {
                    foreach (var sceneElement in scenesArray.EnumerateArray())
                    {
                        script.Scenes.Add(new VideoScene
                        {
                            SceneNumber = sceneElement.TryGetProperty("scene_number", out var sn) ? sn.GetInt32() : script.Scenes.Count + 1,
                            DurationSeconds = sceneElement.TryGetProperty("duration_seconds", out var dur) ? dur.GetDouble() : 5,
                            Narration = sceneElement.TryGetProperty("narration", out var nar) ? nar.GetString() ?? "" : "",
                            VisualDescription = sceneElement.TryGetProperty("visual_description", out var vis) ? vis.GetString() ?? "" : "",
                            ImagePrompt = sceneElement.TryGetProperty("image_prompt", out var img) ? img.GetString() ?? "" : "",
                            TextOverlay = sceneElement.TryGetProperty("text_overlay", out var txt) ? txt.GetString() : null,
                            Transition = sceneElement.TryGetProperty("transition", out var trans) ? trans.GetString() ?? "fade" : "fade"
                        });
                    }
                }

                // Parse hashtags
                if (root.TryGetProperty("hashtags", out var hashtags))
                {
                    foreach (var tag in hashtags.EnumerateArray())
                    {
                        var tagStr = tag.GetString();
                        if (!string.IsNullOrEmpty(tagStr))
                        {
                            script.Hashtags.Add(tagStr.TrimStart('#'));
                        }
                    }
                }

                // Calculate total duration
                script.TotalDurationSeconds = (int)script.Scenes.Sum(s => s.DurationSeconds);

                // Generate captions from narration
                double currentTime = 0;
                foreach (var scene in script.Scenes)
                {
                    if (!string.IsNullOrEmpty(scene.Narration))
                    {
                        script.Captions.Add(new Caption
                        {
                            StartTime = currentTime,
                            EndTime = currentTime + scene.DurationSeconds,
                            Text = scene.Narration
                        });
                    }
                    currentTime += scene.DurationSeconds;
                }

                return script;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to parse script JSON, creating default script");
        }

        // Fallback: Create default script
        return CreateDefaultScript(concept, numScenes, targetDuration, response);
    }

    private VideoScript CreateDefaultScript(ContentConcept concept, int numScenes, int targetDuration, string rawContent)
    {
        var sceneDuration = targetDuration / (double)numScenes;

        var script = new VideoScript
        {
            Title = concept.Topic,
            Hook = $"มาดูกันเลย! {concept.Topic}",
            CallToAction = concept.CallToAction ?? "กดติดตามเพื่อไม่พลาดเนื้อหาดีๆ"
        };

        // Split content into sentences for scenes
        var sentences = rawContent.Split(new[] { '.', '!', '?', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(s => s.Trim().Length > 10)
            .Take(numScenes)
            .ToList();

        for (int i = 0; i < numScenes; i++)
        {
            var narration = i < sentences.Count ? sentences[i].Trim() : $"ฉากที่ {i + 1}";

            script.Scenes.Add(new VideoScene
            {
                SceneNumber = i + 1,
                DurationSeconds = sceneDuration,
                Narration = narration,
                VisualDescription = narration,
                ImagePrompt = $"Professional marketing image, {concept.Topic}, modern style, vibrant colors, high quality",
                TextOverlay = narration.Length > 50 ? narration.Substring(0, 50) + "..." : narration,
                Transition = i == 0 ? "fade" : i % 2 == 0 ? "slide" : "zoom"
            });
        }

        script.TotalDurationSeconds = (int)(sceneDuration * numScenes);

        // Add hashtags from keywords
        script.Hashtags = concept.Keywords.Take(5).ToList();
        if (script.Hashtags.Count == 0)
        {
            script.Hashtags.Add(concept.Topic.Replace(" ", ""));
        }

        return script;
    }

    /// <summary>
    /// Optimize script for specific platform
    /// </summary>
    public VideoScript OptimizeForPlatform(VideoScript script, SocialPlatform platform)
    {
        var optimized = script;

        switch (platform)
        {
            case SocialPlatform.TikTok:
                // TikTok: Short, punchy, vertical
                optimized.Scenes = script.Scenes.Take(10).ToList();
                foreach (var scene in optimized.Scenes)
                {
                    scene.DurationSeconds = Math.Min(scene.DurationSeconds, 3);
                }
                break;

            case SocialPlatform.Instagram:
                // Instagram Reels: Similar to TikTok
                optimized.Scenes = script.Scenes.Take(15).ToList();
                foreach (var scene in optimized.Scenes)
                {
                    scene.DurationSeconds = Math.Min(scene.DurationSeconds, 4);
                }
                break;

            case SocialPlatform.YouTube:
                // YouTube: Can be longer, more detailed
                // Keep as is
                break;

            case SocialPlatform.Facebook:
                // Facebook: Medium length, engaging hook
                if (string.IsNullOrEmpty(optimized.Hook) && optimized.Scenes.Count > 0)
                {
                    optimized.Hook = optimized.Scenes[0].Narration;
                }
                break;

            case SocialPlatform.Twitter:
                // Twitter: Very short
                optimized.Scenes = script.Scenes.Take(5).ToList();
                foreach (var scene in optimized.Scenes)
                {
                    scene.DurationSeconds = Math.Min(scene.DurationSeconds, 3);
                }
                break;
        }

        optimized.TotalDurationSeconds = (int)optimized.Scenes.Sum(s => s.DurationSeconds);
        return optimized;
    }
}
