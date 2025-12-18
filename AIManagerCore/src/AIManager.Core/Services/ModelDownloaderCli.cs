namespace AIManager.Core.Services;

/// <summary>
/// CLI helper สำหรับโหลด model - สามารถเรียกจาก code หรือ command line
/// </summary>
public static class ModelDownloaderCli
{
    /// <summary>
    /// โหลด model ที่ดีที่สุดสำหรับระบบปัจจุบัน
    /// </summary>
    public static async Task<DownloadResult> DownloadBestModelAsync(
        CancellationToken ct = default)
    {
        var manager = new HuggingFaceModelManager();
        var model = manager.GetBestModelForSystem();

        Console.WriteLine($"╔══════════════════════════════════════════════════════════════╗");
        Console.WriteLine($"║           PostXAgent - AI Model Downloader                   ║");
        Console.WriteLine($"╠══════════════════════════════════════════════════════════════╣");
        Console.WriteLine($"║  Selected Model: {model.DisplayName,-42} ║");
        Console.WriteLine($"║  Size: {model.SizeGB:F1} GB                                             ║");
        Console.WriteLine($"║  Source: Hugging Face ({model.HuggingFaceRepo})");
        Console.WriteLine($"╚══════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        // Check if already downloaded
        if (manager.IsModelDownloaded(model))
        {
            Console.WriteLine($"✓ Model already downloaded at: {manager.GetModelPath(model)}");
            return new DownloadResult
            {
                Success = true,
                ModelPath = manager.GetModelPath(model),
                Message = "Already downloaded"
            };
        }

        Console.WriteLine($"Downloading from Hugging Face...");
        Console.WriteLine();

        var progress = new Progress<DownloadProgressEventArgs>(args =>
        {
            var bar = new string('█', (int)(args.ProgressPercent / 2));
            var empty = new string('░', 50 - bar.Length);
            Console.Write($"\r[{bar}{empty}] {args.ProgressPercent:F1}% ({args.DownloadedMB:F1}/{args.TotalMB:F1} MB)");
        });

        var result = await manager.DownloadAndImportAsync(model, ct, progress);

        Console.WriteLine();
        Console.WriteLine();

        if (result.Success)
        {
            Console.WriteLine($"✓ Download completed!");
            Console.WriteLine($"  Path: {result.ModelPath}");
            if (result.ImportedToOllama)
            {
                Console.WriteLine($"  ✓ Imported to Ollama as: {model.OllamaModelName}");
            }
            else
            {
                Console.WriteLine($"  Note: Run 'ollama pull {model.OllamaModelName}' to use with Ollama");
            }
        }
        else
        {
            Console.WriteLine($"✗ Download failed: {result.Error}");
        }

        return result;
    }

    /// <summary>
    /// แสดงรายการ models ที่รองรับ
    /// </summary>
    public static void ListModels()
    {
        var manager = new HuggingFaceModelManager();
        var detector = new SystemResourceDetector();
        var resources = detector.DetectResources();

        Console.WriteLine($"╔══════════════════════════════════════════════════════════════╗");
        Console.WriteLine($"║              Available AI Models                             ║");
        Console.WriteLine($"╠══════════════════════════════════════════════════════════════╣");
        Console.WriteLine($"║  System: {resources.TotalRamMB / 1000.0:F1}GB RAM, {resources.TotalVramMB / 1000.0:F1}GB VRAM");
        Console.WriteLine($"╠══════════════════════════════════════════════════════════════╣");

        var bestModel = manager.GetBestModelForSystem();

        foreach (var model in HuggingFaceModelManager.LLMModels)
        {
            var downloaded = manager.IsModelDownloaded(model) ? "✓" : " ";
            var recommended = model.Id == bestModel.Id ? " ★ RECOMMENDED" : "";
            var canRun = resources.TotalRamMB / 1000.0 >= model.RequiredRamGB ? "" : " (insufficient RAM)";

            Console.WriteLine($"║ [{downloaded}] {model.DisplayName,-30} {model.SizeGB:F1}GB{recommended}{canRun}");
        }

        Console.WriteLine($"╚══════════════════════════════════════════════════════════════╝");
    }

    /// <summary>
    /// โหลด model ที่ระบุ
    /// </summary>
    public static async Task<DownloadResult> DownloadModelByIdAsync(
        string modelId,
        CancellationToken ct = default)
    {
        var manager = new HuggingFaceModelManager();
        var model = HuggingFaceModelManager.AllModels
            .FirstOrDefault(m => m.Id == modelId || m.OllamaModelName == modelId);

        if (model == null)
        {
            Console.WriteLine($"Unknown model: {modelId}");
            ListModels();
            return new DownloadResult { Success = false, Error = "Unknown model" };
        }

        Console.WriteLine($"Downloading: {model.DisplayName}");

        var progress = new Progress<DownloadProgressEventArgs>(args =>
        {
            var bar = new string('█', (int)(args.ProgressPercent / 2));
            var empty = new string('░', 50 - bar.Length);
            Console.Write($"\r[{bar}{empty}] {args.ProgressPercent:F1}%");
        });

        var result = await manager.DownloadAndImportAsync(model, ct, progress);

        Console.WriteLine();

        if (result.Success)
        {
            Console.WriteLine($"✓ Downloaded to: {result.ModelPath}");
        }
        else
        {
            Console.WriteLine($"✗ Failed: {result.Error}");
        }

        return result;
    }
}
