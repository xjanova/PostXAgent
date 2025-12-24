# Media Generation API Testing Guide

## Prerequisites

1. Start the AI Manager API:
```bash
cd D:\Code\PostXAgent\AIManagerCore\src\AIManager.API
dotnet run
```

API should be running on: `http://localhost:5000`

## Test Endpoints

### 1. Quick Video Generation Test

**Endpoint**: `POST /api/MediaGeneration/test/quick-video`

**Request**:
```bash
curl -X POST http://localhost:5000/api/MediaGeneration/test/quick-video \
  -H "Content-Type: application/json" \
  -d "{\"prompt\":\"A cat playing piano in a jazz club\",\"duration\":5,\"aspectRatio\":\"Landscape_16_9\"}"
```

**PowerShell**:
```powershell
$body = @{
    prompt = "A cat playing piano in a jazz club"
    duration = 5
    aspectRatio = "Landscape_16_9"
} | ConvertTo-Json

Invoke-RestMethod -Uri "http://localhost:5000/api/MediaGeneration/test/quick-video" `
    -Method Post `
    -ContentType "application/json" `
    -Body $body
```

**Expected Response**:
```json
{
  "taskId": "guid-here",
  "workerId": 0,
  "platform": "Freepik",
  "success": true,
  "data": {
    "mediaResult": {
      "videoUrl": "https://placeholder.freepik.com/video/{guid}",
      "videoPath": null,
      "thumbnailUrl": null,
      "thumbnailPath": null,
      "audioUrl": null,
      "audioPath": null,
      "metadata": {
        "width": 1920,
        "height": 1080,
        "duration": 5,
        "fps": 30,
        "format": "mp4",
        "fileSize": 0,
        "hasAudio": false,
        "hasVideo": true,
        "createdAt": "2025-12-24T..."
      }
    },
    "message": "Placeholder video result for prompt: 'A cat playing piano in a jazz club'. Real Freepik Pikaso AI integration pending official API availability."
  },
  "processingTimeMs": 45
}
```

### 2. Quick Music Generation Test

**Endpoint**: `POST /api/MediaGeneration/test/quick-music`

**Request**:
```bash
curl -X POST http://localhost:5000/api/MediaGeneration/test/quick-music \
  -H "Content-Type: application/json" \
  -d "{\"prompt\":\"Upbeat electronic dance music\",\"duration\":30,\"genre\":\"Electronic\",\"mood\":\"Energetic\",\"instrumental\":false}"
```

**PowerShell**:
```powershell
$body = @{
    prompt = "Upbeat electronic dance music"
    duration = 30
    genre = "Electronic"
    mood = "Energetic"
    instrumental = $false
} | ConvertTo-Json

Invoke-RestMethod -Uri "http://localhost:5000/api/MediaGeneration/test/quick-music" `
    -Method Post `
    -ContentType "application/json" `
    -Body $body
```

**Expected Response**:
```json
{
  "taskId": "guid-here",
  "workerId": 0,
  "platform": "SunoAI",
  "success": true,
  "data": {
    "mediaResult": {
      "videoUrl": null,
      "audioUrl": "https://placeholder.suno.ai/audio/{guid}",
      "audioPath": null,
      "metadata": {
        "duration": 30,
        "format": "mp3",
        "fileSize": 0,
        "hasAudio": true,
        "hasVideo": false,
        "createdAt": "2025-12-24T..."
      },
      "multipleOutputs": [
        {
          "index": 1,
          "url": "https://placeholder.suno.ai/audio/{guid-1}",
          "path": null,
          "metadata": { ... }
        },
        {
          "index": 2,
          "url": "https://placeholder.suno.ai/audio/{guid-2}",
          "path": null,
          "metadata": { ... }
        }
      ]
    },
    "message": "Placeholder music result for prompt: 'Upbeat electronic dance music' (2 variations). Real Suno AI integration pending official API availability."
  },
  "processingTimeMs": 52
}
```

### 3. Full Video Generation Workflow

**Endpoint**: `POST /api/MediaGeneration/generate-video`

**Request**:
```bash
curl -X POST http://localhost:5000/api/MediaGeneration/generate-video \
  -H "Content-Type: application/json" \
  -d "{
    \"userId\": 1,
    \"brandId\": 1,
    \"priority\": 5,
    \"config\": {
      \"prompt\": \"Sunset over mountains with flying birds\",
      \"duration\": 10,
      \"aspectRatio\": \"Landscape_16_9\",
      \"quality\": \"High_1080p\",
      \"fps\": 30
    }
  }"
```

**PowerShell**:
```powershell
$body = @{
    userId = 1
    brandId = 1
    priority = 5
    config = @{
        prompt = "Sunset over mountains with flying birds"
        duration = 10
        aspectRatio = "Landscape_16_9"
        quality = "High_1080p"
        fps = 30
    }
} | ConvertTo-Json -Depth 3

Invoke-RestMethod -Uri "http://localhost:5000/api/MediaGeneration/generate-video" `
    -Method Post `
    -ContentType "application/json" `
    -Body $body
```

**Expected Response**:
```json
{
  "success": true,
  "taskId": "task-guid-here",
  "message": "Video generation task submitted successfully"
}
```

### 4. Full Music Generation Workflow

**Endpoint**: `POST /api/MediaGeneration/generate-music`

**Request**:
```bash
curl -X POST http://localhost:5000/api/MediaGeneration/generate-music \
  -H "Content-Type: application/json" \
  -d "{
    \"userId\": 1,
    \"brandId\": 1,
    \"priority\": 5,
    \"config\": {
      \"prompt\": \"Relaxing piano music for studying\",
      \"duration\": 60,
      \"genre\": \"Classical\",
      \"mood\": \"Calm\",
      \"instrumental\": true,
      \"numberOfOutputs\": 2
    }
  }"
```

**PowerShell**:
```powershell
$body = @{
    userId = 1
    brandId = 1
    priority = 5
    config = @{
        prompt = "Relaxing piano music for studying"
        duration = 60
        genre = "Classical"
        mood = "Calm"
        instrumental = $true
        numberOfOutputs = 2
    }
} | ConvertTo-Json -Depth 3

Invoke-RestMethod -Uri "http://localhost:5000/api/MediaGeneration/generate-music" `
    -Method Post `
    -ContentType "application/json" `
    -Body $body
```

**Expected Response**:
```json
{
  "success": true,
  "taskId": "task-guid-here",
  "message": "Music generation task submitted successfully"
}
```

### 5. Check Task Result

**Endpoint**: `GET /api/MediaGeneration/result/{taskId}`

**Request**:
```bash
curl -X GET http://localhost:5000/api/MediaGeneration/result/{taskId}
```

**PowerShell**:
```powershell
$taskId = "your-task-id-here"
Invoke-RestMethod -Uri "http://localhost:5000/api/MediaGeneration/result/$taskId" -Method Get
```

**Expected Response**:
```json
{
  "success": true,
  "taskId": "task-guid-here",
  "status": "Completed",
  "message": "Task completed successfully"
}
```

## Testing with Postman

### Import Collection

Create a Postman collection with the following requests:

1. **Quick Video Test**
   - Method: POST
   - URL: `{{baseUrl}}/api/MediaGeneration/test/quick-video`
   - Body (JSON):
   ```json
   {
     "prompt": "A cat playing piano in a jazz club",
     "duration": 5,
     "aspectRatio": "Landscape_16_9"
   }
   ```

2. **Quick Music Test**
   - Method: POST
   - URL: `{{baseUrl}}/api/MediaGeneration/test/quick-music`
   - Body (JSON):
   ```json
   {
     "prompt": "Upbeat electronic dance music",
     "duration": 30,
     "genre": "Electronic",
     "mood": "Energetic",
     "instrumental": false
   }
   ```

3. **Generate Video (Full Workflow)**
   - Method: POST
   - URL: `{{baseUrl}}/api/MediaGeneration/generate-video`
   - Body (JSON):
   ```json
   {
     "userId": 1,
     "brandId": 1,
     "priority": 5,
     "config": {
       "prompt": "Sunset over mountains with flying birds",
       "duration": 10,
       "aspectRatio": "Landscape_16_9",
       "quality": "High_1080p",
       "fps": 30
     }
   }
   ```

4. **Generate Music (Full Workflow)**
   - Method: POST
   - URL: `{{baseUrl}}/api/MediaGeneration/generate-music`
   - Body (JSON):
   ```json
   {
     "userId": 1,
     "brandId": 1,
     "priority": 5,
     "config": {
       "prompt": "Relaxing piano music for studying",
       "duration": 60,
       "genre": "Classical",
       "mood": "Calm",
       "instrumental": true,
       "numberOfOutputs": 2
     }
   }
   ```

### Environment Variables
```
baseUrl = http://localhost:5000
```

## Video Configuration Options

### Aspect Ratios
- `Landscape_16_9` - 1920x1080 (YouTube, Facebook)
- `Portrait_9_16` - 1080x1920 (TikTok, Instagram Reels)
- `Square_1_1` - 1080x1080 (Instagram Posts)
- `Classic_4_3` - 1440x1080 (Classic TV)
- `Ultrawide_21_9` - 2560x1080 (Cinematic)

### Quality Options
- `Draft_360p` - 640x360
- `SD_480p` - 854x480
- `HD_720p` - 1280x720
- `High_1080p` - 1920x1080 (Default)
- `UltraHD_4K` - 3840x2160

### Duration
- Min: 1 second
- Max: 300 seconds (5 minutes)
- Recommended: 5-10 seconds for social media

### FPS (Frames Per Second)
- 24 - Cinematic
- 30 - Standard (Default)
- 60 - Smooth

## Music Configuration Options

### Genres
- `Pop`, `Rock`, `Electronic`, `HipHop`, `Jazz`
- `Classical`, `Country`, `RnB`, `Blues`, `Metal`
- `Folk`, `Reggae`, `Ambient`, `Experimental`

### Moods
- `Energetic`, `Calm`, `Happy`, `Sad`, `Angry`
- `Mysterious`, `Epic`, `Romantic`, `Playful`, `Dark`

### Duration
- Min: 10 seconds
- Max: 300 seconds (5 minutes)
- Recommended: 30-60 seconds for social media

### Number of Outputs
- Min: 1
- Max: 4
- Default: 2 (Suno AI typically generates 2 variations)

## Expected Behavior (Current Placeholder Mode)

### Video Generation
1. Returns placeholder URL: `https://placeholder.freepik.com/video/{guid}`
2. No actual video file generated
3. Metadata reflects requested configuration
4. Success = true
5. Processing time: ~40-50ms

### Music Generation
1. Returns placeholder URLs: `https://placeholder.suno.ai/audio/{guid}`
2. Generates multiple outputs (default 2)
3. No actual audio files generated
4. Metadata reflects requested configuration
5. Success = true
6. Processing time: ~50-60ms

## Next Steps (When APIs Available)

When Freepik and Suno AI APIs become available:

1. Update `FreepikWorker.GenerateVideoAsync()` to call actual API
2. Update `SunoAIWorker.GenerateMusicAsync()` to call actual API
3. Implement video/audio download from generated URLs
4. Add progress polling for long-running generations
5. Update tests to verify actual media files

## Troubleshooting

### API Not Starting
```bash
# Check if port 5000 is available
netstat -ano | findstr :5000

# Kill process if needed
taskkill /PID <pid> /F

# Start API
cd D:\Code\PostXAgent\AIManagerCore\src\AIManager.API
dotnet run
```

### Build Errors
```bash
# Rebuild solution
cd D:\Code\PostXAgent\AIManagerCore
dotnet clean
dotnet build
```

### Invalid JSON
- Ensure aspect ratio uses underscore: `Landscape_16_9`
- Ensure enum values match exactly (case-sensitive)
- Use `null` for optional fields, not empty strings

---

**Last Updated**: 2025-12-24
**Status**: Placeholder mode - Ready for API integration
