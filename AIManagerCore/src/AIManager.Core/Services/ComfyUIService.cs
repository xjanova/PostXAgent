using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace AIManager.Core.Services;

/// <summary>
/// ComfyUI Service สำหรับสร้างรูปภาพและวิดีโอ
/// - เชื่อมต่อผ่าน WebSocket เพื่อดู progress แบบ real-time
/// - รองรับ workflow สำหรับ Image และ Video generation
/// - แสดง node ที่กำลังทำงานใน ComfyUI
/// </summary>
public class ComfyUIService : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ComfyUIService>? _logger;
    private ClientWebSocket? _webSocket;
    private CancellationTokenSource? _wsCts;
    private bool _isDisposed;

    public string BaseUrl { get; set; } = "http://127.0.0.1:8188";
    public string ClientId { get; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Event เมื่อ node เริ่มทำงาน
    /// </summary>
    public event EventHandler<NodeProgressEventArgs>? NodeStarted;

    /// <summary>
    /// Event เมื่อ node ทำงานเสร็จ
    /// </summary>
    public event EventHandler<NodeProgressEventArgs>? NodeCompleted;

    /// <summary>
    /// Event แสดง progress ของการ generate
    /// </summary>
    public event EventHandler<GenerationProgressEventArgs>? ProgressChanged;

    public ComfyUIService(ILogger<ComfyUIService>? logger = null)
    {
        _httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        _logger = logger;

        var envUrl = Environment.GetEnvironmentVariable("COMFYUI_URL");
        if (!string.IsNullOrEmpty(envUrl))
        {
            BaseUrl = envUrl;
        }
    }

    /// <summary>
    /// ตรวจสอบว่า ComfyUI พร้อมใช้งาน
    /// </summary>
    public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{BaseUrl}/system_stats", ct);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// ดึงรายการ models ที่มีใน ComfyUI
    /// </summary>
    public async Task<ComfyUIModels> GetAvailableModelsAsync(CancellationToken ct = default)
    {
        var result = new ComfyUIModels();

        try
        {
            // Get checkpoints
            var response = await _httpClient.GetAsync($"{BaseUrl}/object_info/CheckpointLoaderSimple", ct);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
                if (json.TryGetProperty("CheckpointLoaderSimple", out var node) &&
                    node.TryGetProperty("input", out var input) &&
                    input.TryGetProperty("required", out var required) &&
                    required.TryGetProperty("ckpt_name", out var ckptName) &&
                    ckptName.GetArrayLength() > 0)
                {
                    var options = ckptName[0];
                    foreach (var model in options.EnumerateArray())
                    {
                        result.Checkpoints.Add(model.GetString() ?? "");
                    }
                }
            }

            // Get LoRAs
            response = await _httpClient.GetAsync($"{BaseUrl}/object_info/LoraLoader", ct);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
                if (json.TryGetProperty("LoraLoader", out var node) &&
                    node.TryGetProperty("input", out var input) &&
                    input.TryGetProperty("required", out var required) &&
                    required.TryGetProperty("lora_name", out var loraName) &&
                    loraName.GetArrayLength() > 0)
                {
                    var options = loraName[0];
                    foreach (var lora in options.EnumerateArray())
                    {
                        result.Loras.Add(lora.GetString() ?? "");
                    }
                }
            }

            _logger?.LogInformation("Found {Checkpoints} checkpoints, {Loras} LoRAs",
                result.Checkpoints.Count, result.Loras.Count);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to get ComfyUI models");
        }

        return result;
    }

    /// <summary>
    /// เริ่มเชื่อมต่อ WebSocket เพื่อรับ progress updates
    /// </summary>
    public async Task ConnectWebSocketAsync(CancellationToken ct = default)
    {
        if (_webSocket?.State == WebSocketState.Open) return;

        _webSocket?.Dispose();
        _webSocket = new ClientWebSocket();
        _wsCts = new CancellationTokenSource();

        var wsUrl = BaseUrl.Replace("http://", "ws://").Replace("https://", "wss://");
        await _webSocket.ConnectAsync(new Uri($"{wsUrl}/ws?clientId={ClientId}"), ct);

        _logger?.LogInformation("Connected to ComfyUI WebSocket");

        // Start listening in background
        _ = ListenWebSocketAsync(_wsCts.Token);
    }

    private async Task ListenWebSocketAsync(CancellationToken ct)
    {
        var buffer = new byte[4096];
        var messageBuilder = new StringBuilder();

        try
        {
            while (_webSocket?.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                var result = await _webSocket.ReceiveAsync(buffer, ct);

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    messageBuilder.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));

                    if (result.EndOfMessage)
                    {
                        var message = messageBuilder.ToString();
                        messageBuilder.Clear();
                        ProcessWebSocketMessage(message);
                    }
                }
                else if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when cancelling
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "WebSocket error");
        }
    }

    private void ProcessWebSocketMessage(string message)
    {
        try
        {
            var json = JsonDocument.Parse(message);
            var root = json.RootElement;

            if (!root.TryGetProperty("type", out var typeElement)) return;
            var type = typeElement.GetString();

            switch (type)
            {
                case "executing":
                    if (root.TryGetProperty("data", out var execData))
                    {
                        var nodeId = execData.TryGetProperty("node", out var n) ? n.GetString() : null;
                        var promptId = execData.TryGetProperty("prompt_id", out var p) ? p.GetString() : null;

                        if (nodeId != null)
                        {
                            NodeStarted?.Invoke(this, new NodeProgressEventArgs
                            {
                                NodeId = nodeId,
                                PromptId = promptId ?? ""
                            });
                            _logger?.LogDebug("Executing node: {NodeId}", nodeId);
                        }
                    }
                    break;

                case "executed":
                    if (root.TryGetProperty("data", out var doneData))
                    {
                        var nodeId = doneData.TryGetProperty("node", out var n) ? n.GetString() : null;
                        var promptId = doneData.TryGetProperty("prompt_id", out var p) ? p.GetString() : null;

                        if (nodeId != null)
                        {
                            NodeCompleted?.Invoke(this, new NodeProgressEventArgs
                            {
                                NodeId = nodeId,
                                PromptId = promptId ?? "",
                                Output = doneData.TryGetProperty("output", out var o) ? o.ToString() : null
                            });
                        }
                    }
                    break;

                case "progress":
                    if (root.TryGetProperty("data", out var progressData))
                    {
                        var value = progressData.TryGetProperty("value", out var v) ? v.GetInt32() : 0;
                        var max = progressData.TryGetProperty("max", out var m) ? m.GetInt32() : 100;

                        ProgressChanged?.Invoke(this, new GenerationProgressEventArgs
                        {
                            CurrentStep = value,
                            TotalSteps = max,
                            Percentage = max > 0 ? (double)value / max * 100 : 0
                        });
                    }
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to parse WebSocket message");
        }
    }

    /// <summary>
    /// สร้างรูปภาพด้วย ComfyUI
    /// </summary>
    public async Task<ComfyUIResult> GenerateImageAsync(
        ImageGenerationRequest request,
        CancellationToken ct = default)
    {
        await ConnectWebSocketAsync(ct);

        var workflow = CreateImageWorkflow(request);
        return await QueueAndWaitAsync(workflow, ct);
    }

    /// <summary>
    /// สร้างวิดีโอด้วย ComfyUI (AnimateDiff หรือ SVD)
    /// </summary>
    public async Task<ComfyUIResult> GenerateVideoAsync(
        VideoGenerationRequest request,
        CancellationToken ct = default)
    {
        await ConnectWebSocketAsync(ct);

        var workflow = request.Method switch
        {
            VideoMethod.AnimateDiff => CreateAnimateDiffWorkflow(request),
            VideoMethod.StableVideoDiffusion => CreateSVDWorkflow(request),
            VideoMethod.Wan2_1 => CreateWan21Workflow(request),
            _ => CreateAnimateDiffWorkflow(request)
        };

        return await QueueAndWaitAsync(workflow, ct);
    }

    /// <summary>
    /// ส่ง workflow ไปยัง ComfyUI และรอผลลัพธ์
    /// </summary>
    private async Task<ComfyUIResult> QueueAndWaitAsync(
        Dictionary<string, object> workflow,
        CancellationToken ct)
    {
        // Queue the prompt
        var response = await _httpClient.PostAsJsonAsync(
            $"{BaseUrl}/prompt",
            new { prompt = workflow, client_id = ClientId },
            ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            throw new Exception($"ComfyUI queue failed: {error}");
        }

        var queueResult = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        var promptId = queueResult.GetProperty("prompt_id").GetString()!;

        _logger?.LogInformation("Queued prompt: {PromptId}", promptId);

        // Poll for completion
        for (var i = 0; i < 600; i++) // Max 10 minutes
        {
            await Task.Delay(1000, ct);

            var historyResponse = await _httpClient.GetAsync($"{BaseUrl}/history/{promptId}", ct);
            if (!historyResponse.IsSuccessStatusCode) continue;

            var history = await historyResponse.Content.ReadFromJsonAsync<JsonElement>(ct);
            if (!history.TryGetProperty(promptId, out var promptHistory)) continue;

            if (!promptHistory.TryGetProperty("outputs", out var outputs)) continue;

            // Look for output files
            var result = new ComfyUIResult { PromptId = promptId };

            foreach (var output in outputs.EnumerateObject())
            {
                // Check for images
                if (output.Value.TryGetProperty("images", out var images))
                {
                    foreach (var image in images.EnumerateArray())
                    {
                        var filename = image.GetProperty("filename").GetString()!;
                        var subfolder = image.TryGetProperty("subfolder", out var sf) ? sf.GetString() : "";
                        var type = image.TryGetProperty("type", out var t) ? t.GetString() : "output";

                        var fileUrl = $"{BaseUrl}/view?filename={filename}&subfolder={subfolder}&type={type}";
                        var fileBytes = await _httpClient.GetByteArrayAsync(fileUrl, ct);

                        result.Images.Add(new GeneratedFile
                        {
                            Filename = filename,
                            Url = fileUrl,
                            Base64Data = Convert.ToBase64String(fileBytes),
                            Type = "image"
                        });
                    }
                }

                // Check for GIFs/videos
                if (output.Value.TryGetProperty("gifs", out var gifs))
                {
                    foreach (var gif in gifs.EnumerateArray())
                    {
                        var filename = gif.GetProperty("filename").GetString()!;
                        var subfolder = gif.TryGetProperty("subfolder", out var sf) ? sf.GetString() : "";

                        var fileUrl = $"{BaseUrl}/view?filename={filename}&subfolder={subfolder}&type=output";
                        var fileBytes = await _httpClient.GetByteArrayAsync(fileUrl, ct);

                        result.Videos.Add(new GeneratedFile
                        {
                            Filename = filename,
                            Url = fileUrl,
                            Base64Data = Convert.ToBase64String(fileBytes),
                            Type = filename.EndsWith(".mp4") ? "video" : "gif"
                        });
                    }
                }
            }

            if (result.Images.Count > 0 || result.Videos.Count > 0)
            {
                _logger?.LogInformation("Generation complete: {Images} images, {Videos} videos",
                    result.Images.Count, result.Videos.Count);
                return result;
            }
        }

        throw new TimeoutException("ComfyUI generation timed out");
    }

    /// <summary>
    /// สร้าง workflow สำหรับ txt2img
    /// </summary>
    private Dictionary<string, object> CreateImageWorkflow(ImageGenerationRequest req)
    {
        var checkpoint = req.Checkpoint ?? "sd_xl_base_1.0.safetensors";
        var positivePrompt = $"masterpiece, best quality, {req.Prompt}";
        if (!string.IsNullOrEmpty(req.Style))
            positivePrompt += $", {req.Style} style";

        var negativePrompt = req.NegativePrompt ?? "blurry, low quality, distorted, watermark, text";

        return new Dictionary<string, object>
        {
            ["1"] = new
            {
                class_type = "CheckpointLoaderSimple",
                _meta = new { title = "Load Checkpoint" },
                inputs = new { ckpt_name = checkpoint }
            },
            ["2"] = new
            {
                class_type = "EmptyLatentImage",
                _meta = new { title = "Empty Latent Image" },
                inputs = new
                {
                    width = req.Width,
                    height = req.Height,
                    batch_size = 1
                }
            },
            ["3"] = new
            {
                class_type = "CLIPTextEncode",
                _meta = new { title = "Positive Prompt" },
                inputs = new
                {
                    text = positivePrompt,
                    clip = new object[] { "1", 1 }
                }
            },
            ["4"] = new
            {
                class_type = "CLIPTextEncode",
                _meta = new { title = "Negative Prompt" },
                inputs = new
                {
                    text = negativePrompt,
                    clip = new object[] { "1", 1 }
                }
            },
            ["5"] = new
            {
                class_type = "KSampler",
                _meta = new { title = "KSampler" },
                inputs = new
                {
                    seed = req.Seed ?? Random.Shared.Next(),
                    steps = req.Steps,
                    cfg = req.CfgScale,
                    sampler_name = req.Sampler,
                    scheduler = req.Scheduler,
                    denoise = 1.0,
                    model = new object[] { "1", 0 },
                    positive = new object[] { "3", 0 },
                    negative = new object[] { "4", 0 },
                    latent_image = new object[] { "2", 0 }
                }
            },
            ["6"] = new
            {
                class_type = "VAEDecode",
                _meta = new { title = "VAE Decode" },
                inputs = new
                {
                    samples = new object[] { "5", 0 },
                    vae = new object[] { "1", 2 }
                }
            },
            ["7"] = new
            {
                class_type = "SaveImage",
                _meta = new { title = "Save Image" },
                inputs = new
                {
                    filename_prefix = "PostXAgent_Image",
                    images = new object[] { "6", 0 }
                }
            }
        };
    }

    /// <summary>
    /// สร้าง workflow สำหรับ AnimateDiff (text to video)
    /// ต้องติดตั้ง ComfyUI-AnimateDiff-Evolved
    /// </summary>
    private Dictionary<string, object> CreateAnimateDiffWorkflow(VideoGenerationRequest req)
    {
        var checkpoint = req.Checkpoint ?? "sd_xl_base_1.0.safetensors";
        var motionModule = req.MotionModule ?? "mm_sd_v15_v2.ckpt";
        var positivePrompt = $"masterpiece, best quality, {req.Prompt}";
        var negativePrompt = req.NegativePrompt ?? "blurry, low quality, distorted, watermark";

        return new Dictionary<string, object>
        {
            ["1"] = new
            {
                class_type = "CheckpointLoaderSimple",
                _meta = new { title = "Load Checkpoint" },
                inputs = new { ckpt_name = checkpoint }
            },
            ["2"] = new
            {
                class_type = "ADE_AnimateDiffLoaderWithContext",
                _meta = new { title = "AnimateDiff Loader" },
                inputs = new
                {
                    model = new object[] { "1", 0 },
                    context_options = new object[] { "3", 0 },
                    motion_lora = null as object,
                    ad_settings = null as object,
                    sample_settings = null as object,
                    motion_model = motionModule,
                    beta_schedule = "sqrt_linear (AnimateDiff)"
                }
            },
            ["3"] = new
            {
                class_type = "ADE_StandardStaticContextOptions",
                _meta = new { title = "Context Options" },
                inputs = new
                {
                    context_length = 16,
                    context_stride = 1,
                    context_overlap = 4,
                    fuse_method = "flat",
                    use_on_equal_length = false,
                    start_percent = 0.0,
                    guarantee_steps = 1
                }
            },
            ["4"] = new
            {
                class_type = "EmptyLatentImage",
                _meta = new { title = "Empty Latent" },
                inputs = new
                {
                    width = req.Width,
                    height = req.Height,
                    batch_size = req.Frames
                }
            },
            ["5"] = new
            {
                class_type = "CLIPTextEncode",
                _meta = new { title = "Positive Prompt" },
                inputs = new
                {
                    text = positivePrompt,
                    clip = new object[] { "1", 1 }
                }
            },
            ["6"] = new
            {
                class_type = "CLIPTextEncode",
                _meta = new { title = "Negative Prompt" },
                inputs = new
                {
                    text = negativePrompt,
                    clip = new object[] { "1", 1 }
                }
            },
            ["7"] = new
            {
                class_type = "KSampler",
                _meta = new { title = "KSampler" },
                inputs = new
                {
                    seed = req.Seed ?? Random.Shared.Next(),
                    steps = req.Steps,
                    cfg = req.CfgScale,
                    sampler_name = "euler_ancestral",
                    scheduler = "normal",
                    denoise = 1.0,
                    model = new object[] { "2", 0 },
                    positive = new object[] { "5", 0 },
                    negative = new object[] { "6", 0 },
                    latent_image = new object[] { "4", 0 }
                }
            },
            ["8"] = new
            {
                class_type = "VAEDecode",
                _meta = new { title = "VAE Decode" },
                inputs = new
                {
                    samples = new object[] { "7", 0 },
                    vae = new object[] { "1", 2 }
                }
            },
            ["9"] = new
            {
                class_type = "ADE_AnimateDiffCombine",
                _meta = new { title = "Combine to Video" },
                inputs = new
                {
                    images = new object[] { "8", 0 },
                    frame_rate = req.Fps,
                    loop_count = 0,
                    filename_prefix = "PostXAgent_Video",
                    format = "video/h264-mp4",
                    pingpong = false,
                    save_image = true
                }
            }
        };
    }

    /// <summary>
    /// สร้าง workflow สำหรับ Stable Video Diffusion (img2video)
    /// ต้องติดตั้ง ComfyUI-VideoHelperSuite
    /// </summary>
    private Dictionary<string, object> CreateSVDWorkflow(VideoGenerationRequest req)
    {
        if (string.IsNullOrEmpty(req.InputImagePath))
            throw new ArgumentException("SVD requires an input image");

        return new Dictionary<string, object>
        {
            ["1"] = new
            {
                class_type = "ImageOnlyCheckpointLoader",
                _meta = new { title = "Load SVD Model" },
                inputs = new { ckpt_name = "svd_xt_1_1.safetensors" }
            },
            ["2"] = new
            {
                class_type = "LoadImage",
                _meta = new { title = "Load Input Image" },
                inputs = new { image = req.InputImagePath }
            },
            ["3"] = new
            {
                class_type = "SVD_img2vid_Conditioning",
                _meta = new { title = "SVD Conditioning" },
                inputs = new
                {
                    clip_vision = new object[] { "1", 1 },
                    init_image = new object[] { "2", 0 },
                    vae = new object[] { "1", 2 },
                    width = req.Width,
                    height = req.Height,
                    video_frames = req.Frames,
                    motion_bucket_id = 127,
                    fps = req.Fps,
                    augmentation_level = 0.0
                }
            },
            ["4"] = new
            {
                class_type = "KSampler",
                _meta = new { title = "KSampler" },
                inputs = new
                {
                    seed = req.Seed ?? Random.Shared.Next(),
                    steps = req.Steps,
                    cfg = req.CfgScale,
                    sampler_name = "euler",
                    scheduler = "karras",
                    denoise = 1.0,
                    model = new object[] { "1", 0 },
                    positive = new object[] { "3", 0 },
                    negative = new object[] { "3", 1 },
                    latent_image = new object[] { "3", 2 }
                }
            },
            ["5"] = new
            {
                class_type = "VAEDecode",
                _meta = new { title = "VAE Decode" },
                inputs = new
                {
                    samples = new object[] { "4", 0 },
                    vae = new object[] { "1", 2 }
                }
            },
            ["6"] = new
            {
                class_type = "VHS_VideoCombine",
                _meta = new { title = "Save Video" },
                inputs = new
                {
                    images = new object[] { "5", 0 },
                    frame_rate = req.Fps,
                    loop_count = 0,
                    filename_prefix = "PostXAgent_SVD",
                    format = "video/h264-mp4",
                    pingpong = false,
                    save_output = true
                }
            }
        };
    }

    /// <summary>
    /// สร้าง workflow สำหรับ Wan2.1 (text to video ใหม่ล่าสุด)
    /// </summary>
    private Dictionary<string, object> CreateWan21Workflow(VideoGenerationRequest req)
    {
        var positivePrompt = $"masterpiece, best quality, {req.Prompt}";
        var negativePrompt = req.NegativePrompt ?? "blurry, low quality, distorted";

        return new Dictionary<string, object>
        {
            ["1"] = new
            {
                class_type = "DownloadAndLoadWan21Model",
                _meta = new { title = "Load Wan2.1 Model" },
                inputs = new
                {
                    model = "Wan2.1-T2V-14B-bf16",
                    precision = "bf16",
                    fp8_fastmode = false
                }
            },
            ["2"] = new
            {
                class_type = "Wan21TextEncode",
                _meta = new { title = "Text Encode" },
                inputs = new
                {
                    prompt = positivePrompt,
                    negative_prompt = negativePrompt,
                    clip = new object[] { "1", 1 }
                }
            },
            ["3"] = new
            {
                class_type = "Wan21Sampler",
                _meta = new { title = "Wan2.1 Sampler" },
                inputs = new
                {
                    model = new object[] { "1", 0 },
                    positive = new object[] { "2", 0 },
                    negative = new object[] { "2", 1 },
                    width = req.Width,
                    height = req.Height,
                    num_frames = req.Frames,
                    steps = req.Steps,
                    cfg = req.CfgScale,
                    seed = req.Seed ?? Random.Shared.Next(),
                    sampler = "euler",
                    scheduler = "normal"
                }
            },
            ["4"] = new
            {
                class_type = "Wan21Decode",
                _meta = new { title = "Decode" },
                inputs = new
                {
                    samples = new object[] { "3", 0 },
                    vae = new object[] { "1", 2 }
                }
            },
            ["5"] = new
            {
                class_type = "VHS_VideoCombine",
                _meta = new { title = "Save Video" },
                inputs = new
                {
                    images = new object[] { "4", 0 },
                    frame_rate = req.Fps,
                    loop_count = 0,
                    filename_prefix = "PostXAgent_Wan21",
                    format = "video/h264-mp4",
                    pingpong = false,
                    save_output = true
                }
            }
        };
    }

    /// <summary>
    /// ดึงสถานะ queue ของ ComfyUI
    /// </summary>
    public async Task<QueueStatus> GetQueueStatusAsync(CancellationToken ct = default)
    {
        var response = await _httpClient.GetAsync($"{BaseUrl}/queue", ct);
        if (!response.IsSuccessStatusCode)
            return new QueueStatus();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);

        return new QueueStatus
        {
            Running = json.TryGetProperty("queue_running", out var r) ? r.GetArrayLength() : 0,
            Pending = json.TryGetProperty("queue_pending", out var p) ? p.GetArrayLength() : 0
        };
    }

    /// <summary>
    /// Interrupt การ generate ที่กำลังทำงาน
    /// </summary>
    public async Task InterruptAsync(CancellationToken ct = default)
    {
        await _httpClient.PostAsync($"{BaseUrl}/interrupt", null, ct);
        _logger?.LogInformation("Interrupted current generation");
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        _wsCts?.Cancel();
        _webSocket?.Dispose();
        _httpClient.Dispose();
    }
}

#region Request/Response Models

public class ImageGenerationRequest
{
    public string Prompt { get; set; } = "";
    public string? NegativePrompt { get; set; }
    public string? Style { get; set; }
    public string? Checkpoint { get; set; }
    public int Width { get; set; } = 1024;
    public int Height { get; set; } = 1024;
    public int Steps { get; set; } = 30;
    public double CfgScale { get; set; } = 7.0;
    public string Sampler { get; set; } = "dpmpp_2m";
    public string Scheduler { get; set; } = "karras";
    public int? Seed { get; set; }
}

public class VideoGenerationRequest
{
    public string Prompt { get; set; } = "";
    public string? NegativePrompt { get; set; }
    public string? Checkpoint { get; set; }
    public string? MotionModule { get; set; }
    public string? InputImagePath { get; set; } // For img2video
    public VideoMethod Method { get; set; } = VideoMethod.AnimateDiff;
    public int Width { get; set; } = 512;
    public int Height { get; set; } = 512;
    public int Frames { get; set; } = 16;
    public int Fps { get; set; } = 8;
    public int Steps { get; set; } = 25;
    public double CfgScale { get; set; } = 7.0;
    public int? Seed { get; set; }
}

public enum VideoMethod
{
    AnimateDiff,
    StableVideoDiffusion,
    Wan2_1
}

public class ComfyUIResult
{
    public string PromptId { get; set; } = "";
    public List<GeneratedFile> Images { get; set; } = new();
    public List<GeneratedFile> Videos { get; set; } = new();
}

public class GeneratedFile
{
    public string Filename { get; set; } = "";
    public string Url { get; set; } = "";
    public string Base64Data { get; set; } = "";
    public string Type { get; set; } = ""; // image, video, gif
}

public class ComfyUIModels
{
    public List<string> Checkpoints { get; set; } = new();
    public List<string> Loras { get; set; } = new();
    public List<string> MotionModules { get; set; } = new();
}

public class QueueStatus
{
    public int Running { get; set; }
    public int Pending { get; set; }
}

public class NodeProgressEventArgs : EventArgs
{
    public string NodeId { get; set; } = "";
    public string PromptId { get; set; } = "";
    public string? Output { get; set; }
}

public class GenerationProgressEventArgs : EventArgs
{
    public int CurrentStep { get; set; }
    public int TotalSteps { get; set; }
    public double Percentage { get; set; }
}

#endregion
