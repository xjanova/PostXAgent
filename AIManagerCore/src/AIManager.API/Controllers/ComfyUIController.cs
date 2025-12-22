using Microsoft.AspNetCore.Mvc;
using AIManager.Core.Services;

namespace AIManager.API.Controllers;

/// <summary>
/// API สำหรับควบคุม ComfyUI
/// - สร้างรูปภาพและวิดีโอ
/// - ดู workflow progress
/// - จัดการ queue
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ComfyUIController : ControllerBase
{
    private readonly ComfyUIService _comfyService;
    private readonly ILogger<ComfyUIController> _logger;

    public ComfyUIController(ILogger<ComfyUIController> logger)
    {
        _comfyService = new ComfyUIService();
        _logger = logger;
    }

    /// <summary>
    /// ตรวจสอบสถานะ ComfyUI
    /// </summary>
    [HttpGet("status")]
    public async Task<IActionResult> GetStatus()
    {
        var isAvailable = await _comfyService.IsAvailableAsync();
        var queue = await _comfyService.GetQueueStatusAsync();

        return Ok(new
        {
            available = isAvailable,
            url = _comfyService.BaseUrl,
            queue = new
            {
                running = queue.Running,
                pending = queue.Pending
            }
        });
    }

    /// <summary>
    /// ดึงรายการ models ที่มี
    /// </summary>
    [HttpGet("models")]
    public async Task<IActionResult> GetModels()
    {
        if (!await _comfyService.IsAvailableAsync())
        {
            return StatusCode(503, new { error = "ComfyUI not available", url = _comfyService.BaseUrl });
        }

        var models = await _comfyService.GetAvailableModelsAsync();
        return Ok(models);
    }

    /// <summary>
    /// สร้างรูปภาพ
    /// </summary>
    [HttpPost("generate/image")]
    public async Task<IActionResult> GenerateImage([FromBody] Core.Services.ImageGenerationRequest request)
    {
        if (!await _comfyService.IsAvailableAsync())
        {
            return StatusCode(503, new { error = "ComfyUI not available", url = _comfyService.BaseUrl });
        }

        try
        {
            _logger.LogInformation("Generating image: {Prompt}", request.Prompt);

            var result = await _comfyService.GenerateImageAsync(request);

            return Ok(new
            {
                success = true,
                promptId = result.PromptId,
                images = result.Images.Select(i => new
                {
                    filename = i.Filename,
                    url = i.Url,
                    base64 = i.Base64Data.Length > 1000 ? i.Base64Data[..100] + "..." : i.Base64Data,
                    fullBase64Available = true
                })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Image generation failed");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// ดึง base64 ของรูปภาพ
    /// </summary>
    [HttpGet("image/{promptId}/{filename}")]
    public async Task<IActionResult> GetImage(string promptId, string filename)
    {
        try
        {
            using var httpClient = new HttpClient();
            var url = $"{_comfyService.BaseUrl}/view?filename={filename}&type=output";
            var imageBytes = await httpClient.GetByteArrayAsync(url);

            return File(imageBytes, "image/png");
        }
        catch (Exception ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    /// <summary>
    /// สร้างวิดีโอ
    /// </summary>
    [HttpPost("generate/video")]
    public async Task<IActionResult> GenerateVideo([FromBody] Core.Services.VideoGenerationRequest request)
    {
        if (!await _comfyService.IsAvailableAsync())
        {
            return StatusCode(503, new { error = "ComfyUI not available", url = _comfyService.BaseUrl });
        }

        try
        {
            _logger.LogInformation("Generating video with {Method}: {Prompt}",
                request.Method, request.Prompt);

            var result = await _comfyService.GenerateVideoAsync(request);

            return Ok(new
            {
                success = true,
                promptId = result.PromptId,
                videos = result.Videos.Select(v => new
                {
                    filename = v.Filename,
                    url = v.Url,
                    type = v.Type
                }),
                images = result.Images.Select(i => new
                {
                    filename = i.Filename,
                    url = i.Url
                })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Video generation failed");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// ดึงวิดีโอ
    /// </summary>
    [HttpGet("video/{filename}")]
    public async Task<IActionResult> GetVideo(string filename)
    {
        try
        {
            using var httpClient = new HttpClient();
            var url = $"{_comfyService.BaseUrl}/view?filename={filename}&type=output";
            var videoBytes = await httpClient.GetByteArrayAsync(url);

            var contentType = filename.EndsWith(".gif") ? "image/gif" : "video/mp4";
            return File(videoBytes, contentType);
        }
        catch (Exception ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    /// <summary>
    /// ดึงสถานะ queue
    /// </summary>
    [HttpGet("queue")]
    public async Task<IActionResult> GetQueue()
    {
        var queue = await _comfyService.GetQueueStatusAsync();
        return Ok(queue);
    }

    /// <summary>
    /// หยุดการ generate ที่กำลังทำงาน
    /// </summary>
    [HttpPost("interrupt")]
    public async Task<IActionResult> Interrupt()
    {
        await _comfyService.InterruptAsync();
        return Ok(new { message = "Interrupted" });
    }

    /// <summary>
    /// ดึง workflow templates ที่มี
    /// </summary>
    [HttpGet("workflows")]
    public IActionResult GetWorkflows()
    {
        var workflows = new List<object>
        {
            new
            {
                id = "txt2img",
                name = "Text to Image",
                description = "สร้างรูปภาพจากข้อความ",
                method = "image",
                requirements = Array.Empty<string>()
            },
            new
            {
                id = "animatediff",
                name = "AnimateDiff",
                description = "สร้างวิดีโอจากข้อความ (ต้องติดตั้ง AnimateDiff)",
                method = "video",
                requirements = new[] { "ComfyUI-AnimateDiff-Evolved" }
            },
            new
            {
                id = "svd",
                name = "Stable Video Diffusion",
                description = "สร้างวิดีโอจากรูปภาพ (img2video)",
                method = "video",
                requirements = new[] { "ComfyUI-VideoHelperSuite", "svd_xt model" }
            },
            new
            {
                id = "wan21",
                name = "Wan2.1",
                description = "Text to Video ใหม่ล่าสุด (คุณภาพสูง)",
                method = "video",
                requirements = new[] { "ComfyUI-WanVideoWrapper" }
            }
        };

        return Ok(workflows);
    }
}
