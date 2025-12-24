# AI Video & Music Generation - Design Documentation

## Overview

This document explains the design decisions for the AI Video & Music Generation system in PostXAgent.

## System Components

### 1. FreepikWorker
- **Purpose**: Generate AI videos using Freepik Pikaso AI
- **Status**: Placeholder implementation
- **File**: `AIManager.Core/Workers/FreepikWorker.cs`

### 2. SunoAIWorker
- **Purpose**: Generate AI music using Suno AI
- **Status**: Placeholder implementation
- **File**: `AIManager.Core/Workers/SunoAIWorker.cs`

### 3. MediaGenerationController
- **Purpose**: REST API endpoints for video/music generation
- **File**: `AIManager.API/Controllers/MediaGenerationController.cs`
- **Endpoints**:
  - `POST /api/MediaGeneration/generate-video` - Generate video
  - `POST /api/MediaGeneration/generate-music` - Generate music
  - `POST /api/MediaGeneration/process-video` - Process video (mix, concat, resize)
  - `GET /api/MediaGeneration/result/{taskId}` - Get task result
  - `POST /api/MediaGeneration/test/quick-video` - Quick video test
  - `POST /api/MediaGeneration/test/quick-music` - Quick music test

## Why Web Learning Is NOT Suitable

### Initial Consideration
Web Learning was initially considered for automating Freepik and Suno AI interactions.

### Key Limitation Discovered
The Web Learning system is designed for **posting automation**, not **content generation**:

1. **Input Handling**: Web Learning can inject user content (text, images, videos) into web forms via `WebPostContent` and variable substitution (`{{content.text}}`, etc.)

2. **Output Limitation**: Web Learning executes workflows but **cannot extract generated content**:
   - `WorkflowExecutionResult` only contains: `Success`, `Error`, `StepResults`
   - No mechanism to capture generated video URLs or audio files
   - Designed to verify posting success, not extract generated media

3. **Architectural Mismatch**:
   - **Web Learning purpose**: Automate repetitive posting tasks (Facebook, Instagram, TikTok)
   - **Media generation need**: Submit prompts → Wait for generation → Extract URLs → Download files
   - These are fundamentally different workflows

### Code Evidence

```csharp
// WorkflowExecutionResult structure (WorkflowExecutor.cs:388)
public class WorkflowExecutionResult
{
    public string WorkflowId { get; set; } = "";
    public bool Success { get; set; }
    public int? FailedAtStep { get; set; }
    public string? Error { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime CompletedAt { get; set; }
    public List<StepExecutionResult> StepResults { get; set; } = new();
    public TimeSpan Duration => CompletedAt - StartedAt;
}
// NOTE: No ExtractedData, no OutputUrls, no generated content capture
```

```csharp
// WebPostContent structure (WorkflowModels.cs)
public class WebPostContent
{
    public string Text { get; set; } = "";
    public List<string>? ImagePaths { get; set; }
    public List<string>? VideoPaths { get; set; }
    public string? Link { get; set; }
    public List<string>? Hashtags { get; set; }
    public string? Location { get; set; }
}
// NOTE: No Variables property for custom data like video configs
```

## Current Implementation

### FreepikWorker.cs
```csharp
/// <summary>
/// Worker for Freepik Pikaso AI video generation
/// PRIMARY video generation provider
///
/// NOTE: Web Learning integration not suitable for this use case because:
/// - Freepik is a generative AI service, not a posting platform
/// - Need to extract generated content (video URLs), which Web Learning doesn't support
/// - Web Learning is designed for posting automation, not media generation
///
/// Future enhancement: Direct API integration when Freepik provides official API
/// </summary>
public class FreepikWorker : BasePlatformWorker
{
    public async Task<TaskResult> GenerateVideoAsync(TaskItem task, CancellationToken ct)
    {
        // Currently returns placeholder result
        _logger2.LogInformation("URL: {Url}", FREEPIK_URL);
        return await GeneratePlaceholder(task, config, sw, ct);
    }
}
```

### SunoAIWorker.cs
```csharp
/// <summary>
/// Worker for Suno AI music generation
/// PRIMARY music generation provider
///
/// NOTE: Web Learning integration not suitable for this use case because:
/// - Suno AI is a generative AI service, not a posting platform
/// - Need to extract generated content (audio URLs), which Web Learning doesn't support
/// - Web Learning is designed for posting automation, not media generation
///
/// Future enhancement: Direct API integration when Suno AI provides official API
/// </summary>
public class SunoAIWorker : BasePlatformWorker
{
    public async Task<TaskResult> GenerateMusicAsync(TaskItem task, CancellationToken ct)
    {
        // Currently returns placeholder result with multiple outputs
        // Suno AI typically generates 2 variations
        for (int i = 1; i <= config.NumberOfOutputs; i++)
        {
            multipleOutputs.Add(new MediaOutput { ... });
        }
        return result;
    }
}
```

## Future Implementation Path

### Option 1: Official APIs (Preferred)
Wait for Freepik and Suno AI to release official APIs:
- **Freepik**: Monitor https://www.freepik.com/api (currently limited)
- **Suno AI**: Monitor https://suno.com/api (not publicly available yet)

Benefits:
- Stable, supported integration
- Proper authentication and rate limiting
- Documented endpoints
- SLA guarantees

### Option 2: Alternative Services
Use providers with existing APIs:
- **Video**: Runway ML API, Pika Labs API, Luma AI API
- **Music**: Stable Audio API, AudioCraft (Meta), MusicGen

### Option 3: Custom Web Automation (Last Resort)
Build specialized extraction workflow:
1. Navigate to Freepik/Suno
2. Submit generation request
3. **Wait for completion** (polling or waiting)
4. **Extract generated URL** from DOM
5. Download media file

Challenges:
- Requires extending Web Learning with extraction capabilities
- Brittle (breaks when UI changes)
- Slower than API
- May violate Terms of Service

## Testing Strategy

### Current Test Endpoints
```bash
# Test video generation
POST /api/MediaGeneration/test/quick-video
{
  "prompt": "A cat playing piano in a jazz club",
  "duration": 5,
  "aspectRatio": "Landscape_16_9"
}

# Test music generation
POST /api/MediaGeneration/test/quick-music
{
  "prompt": "Upbeat electronic dance music",
  "duration": 30,
  "genre": "Electronic",
  "mood": "Energetic"
}
```

### Expected Response (Placeholder)
```json
{
  "success": true,
  "data": {
    "mediaResult": {
      "videoUrl": "https://placeholder.freepik.com/video/{guid}",
      "videoPath": null,
      "metadata": {
        "width": 1920,
        "height": 1080,
        "duration": 5,
        "fps": 30,
        "format": "mp4"
      }
    },
    "message": "Placeholder video result for prompt: 'A cat playing piano'. Real Freepik integration pending API availability."
  },
  "processingTimeMs": 45
}
```

## Integration with AI Manager Core

### Task Flow
```
1. API receives request → MediaGenerationController
2. Create TaskItem with VideoConfig/MusicConfig
3. Submit to ProcessOrchestrator queue
4. Worker processes task:
   - FreepikWorker.GenerateVideoAsync()
   - SunoAIWorker.GenerateMusicAsync()
5. Return TaskResult with MediaGenerationResult
6. API returns task ID to client
7. Client polls /result/{taskId} for completion
```

### Worker Factory Registration
```csharp
// WorkerFactory.cs
{ SocialPlatform.Freepik, () => {
    var services = _mediaServices.Value;
    var logger = LoggerFactory.Create(b => b.AddConsole()).CreateLogger<FreepikWorker>();
    return new FreepikWorker(logger, services.video);
}},

{ SocialPlatform.SunoAI, () => {
    var services = _mediaServices.Value;
    var logger = LoggerFactory.Create(b => b.AddConsole()).CreateLogger<SunoAIWorker>();
    return new SunoAIWorker(logger, services.audio);
}},
```

## Conclusion

The media generation workers are implemented with placeholder results and clear documentation explaining:
1. Why Web Learning is not suitable for this use case
2. What the actual requirements are (API integration)
3. How to proceed when official APIs become available

This design maintains clean architecture while being honest about current capabilities and providing a clear path forward.

---

**Last Updated**: 2025-12-24
**Status**: Phase 2 Complete - Workers functional with placeholders
