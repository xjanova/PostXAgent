namespace AIManager.Core.Models;

/// <summary>
/// Complete pipeline configuration with validation
/// ระบบ Configuration ครบครันเหมือน ComfyUI
/// </summary>
public class PipelineConfiguration
{
    // Block 1: Input
    public InputBlockConfig Input { get; set; } = new();

    // Block 2: Model Configuration (ตั้งค่าได้ครบเหมือน ComfyUI)
    public ModelBlockConfig Model { get; set; } = new();

    // Block 3: Sampler/Scheduler Settings
    public SamplerBlockConfig Sampler { get; set; } = new();

    // Block 4: Processor Selection
    public ProcessorBlockConfig Processor { get; set; } = new();

    // Block 5: Post-Processing
    public PostProcessBlockConfig PostProcess { get; set; } = new();

    // Block 6: Output Settings
    public OutputBlockConfig Output { get; set; } = new();

    /// <summary>
    /// Validate all blocks in sequence
    /// </summary>
    public PipelineValidationResult Validate()
    {
        var result = new PipelineValidationResult();

        // Validate each block in order
        var inputResult = Input.Validate();
        result.BlockResults["Input"] = inputResult;
        if (!inputResult.IsValid)
        {
            result.IsValid = false;
            result.FirstInvalidBlock = "Input";
            result.Errors.AddRange(inputResult.Errors);
            return result;
        }

        var modelResult = Model.Validate();
        result.BlockResults["Model"] = modelResult;
        if (!modelResult.IsValid)
        {
            result.IsValid = false;
            result.FirstInvalidBlock = "Model";
            result.Errors.AddRange(modelResult.Errors);
            return result;
        }

        var samplerResult = Sampler.Validate();
        result.BlockResults["Sampler"] = samplerResult;
        if (!samplerResult.IsValid)
        {
            result.IsValid = false;
            result.FirstInvalidBlock = "Sampler";
            result.Errors.AddRange(samplerResult.Errors);
            return result;
        }

        var processorResult = Processor.Validate();
        result.BlockResults["Processor"] = processorResult;
        if (!processorResult.IsValid)
        {
            result.IsValid = false;
            result.FirstInvalidBlock = "Processor";
            result.Errors.AddRange(processorResult.Errors);
            return result;
        }

        var postProcessResult = PostProcess.Validate();
        result.BlockResults["PostProcess"] = postProcessResult;
        if (!postProcessResult.IsValid)
        {
            result.IsValid = false;
            result.FirstInvalidBlock = "PostProcess";
            result.Errors.AddRange(postProcessResult.Errors);
            return result;
        }

        var outputResult = Output.Validate();
        result.BlockResults["Output"] = outputResult;
        if (!outputResult.IsValid)
        {
            result.IsValid = false;
            result.FirstInvalidBlock = "Output";
            result.Errors.AddRange(outputResult.Errors);
            return result;
        }

        // Collect all warnings
        foreach (var block in result.BlockResults.Values)
        {
            result.Warnings.AddRange(block.Warnings);
        }

        result.IsValid = true;
        return result;
    }
}

/// <summary>
/// Input Block Configuration
/// </summary>
public class InputBlockConfig : IBlockConfig
{
    public string Prompt { get; set; } = "";
    public string NegativePrompt { get; set; } = "blurry, bad quality, watermark, nsfw";
    public GenerationType GenerationType { get; set; } = GenerationType.Image;

    // Advanced Input Options
    public bool UsePromptWeighting { get; set; } = true;
    public bool UseClipSkip { get; set; } = false;
    public int ClipSkipLayers { get; set; } = 1;
    public string? EmbeddingPath { get; set; }

    public BlockValidationResult Validate()
    {
        var result = new BlockValidationResult { BlockName = "Input" };

        if (string.IsNullOrWhiteSpace(Prompt))
        {
            result.Errors.Add("Prompt is required");
            result.Status = BlockStatus.Invalid;
            return result;
        }

        if (Prompt.Length < 3)
        {
            result.Errors.Add("Prompt is too short (minimum 3 characters)");
            result.Status = BlockStatus.Invalid;
            return result;
        }

        if (Prompt.Length > 5000)
        {
            result.Warnings.Add("Prompt is very long and may be truncated");
        }

        result.Status = result.Warnings.Count > 0 ? BlockStatus.Warning : BlockStatus.Valid;
        return result;
    }
}

/// <summary>
/// Model Block Configuration - เหมือน ComfyUI
/// </summary>
public class ModelBlockConfig : IBlockConfig
{
    // Checkpoint/Base Model
    public string? CheckpointId { get; set; }
    public string? CheckpointPath { get; set; }
    public ModelPrecision Precision { get; set; } = ModelPrecision.FP16;

    // VAE Settings
    public bool UseCustomVae { get; set; } = false;
    public string? VaePath { get; set; }
    public string? VaeId { get; set; }
    public bool TiledVae { get; set; } = false;
    public int VaeTileSize { get; set; } = 512;

    // LoRA Configuration (สามารถใส่หลาย LoRA ได้)
    public List<LoraConfig> LoRAs { get; set; } = new();
    public int MaxLoraCount { get; set; } = 5;

    // ControlNet Configuration
    public List<ControlNetConfig> ControlNets { get; set; } = new();

    // Textual Inversion / Embeddings
    public List<string> TextualInversions { get; set; } = new();

    // IP-Adapter
    public bool UseIPAdapter { get; set; } = false;
    public string? IPAdapterModel { get; set; }
    public string? IPAdapterImagePath { get; set; }
    public double IPAdapterScale { get; set; } = 1.0;

    // Refiner (for SDXL)
    public bool UseRefiner { get; set; } = false;
    public string? RefinerModelId { get; set; }
    public double RefinerSwitchAt { get; set; } = 0.8;

    public BlockValidationResult Validate()
    {
        var result = new BlockValidationResult { BlockName = "Model" };

        if (string.IsNullOrWhiteSpace(CheckpointId) && string.IsNullOrWhiteSpace(CheckpointPath))
        {
            result.Errors.Add("ยังไม่ได้เลือก Model - กรุณาเลือก Model จาก Model Manager / No model selected. Please select a checkpoint model from Model Manager.");
            result.Status = BlockStatus.Invalid;
            return result;
        }

        if (UseCustomVae && string.IsNullOrWhiteSpace(VaePath) && string.IsNullOrWhiteSpace(VaeId))
        {
            result.Errors.Add("Custom VAE enabled but no VAE path specified");
            result.Status = BlockStatus.Invalid;
            return result;
        }

        // Validate LoRAs
        foreach (var lora in LoRAs)
        {
            if (string.IsNullOrWhiteSpace(lora.Path) && string.IsNullOrWhiteSpace(lora.Id))
            {
                result.Warnings.Add($"LoRA '{lora.Name}' has no path specified");
            }
            if (lora.Weight < 0 || lora.Weight > 2)
            {
                result.Warnings.Add($"LoRA '{lora.Name}' weight {lora.Weight} is unusual (recommended 0-1)");
            }
        }

        if (LoRAs.Count > MaxLoraCount)
        {
            result.Warnings.Add($"Using {LoRAs.Count} LoRAs may require high VRAM");
        }

        // Validate ControlNets
        foreach (var cn in ControlNets)
        {
            if (!cn.Validate().IsValid)
            {
                result.Warnings.Add($"ControlNet '{cn.Name}' configuration is incomplete");
            }
        }

        if (UseIPAdapter && string.IsNullOrWhiteSpace(IPAdapterImagePath))
        {
            result.Errors.Add("IP-Adapter enabled but no reference image specified");
            result.Status = BlockStatus.Invalid;
            return result;
        }

        result.Status = result.Warnings.Count > 0 ? BlockStatus.Warning : BlockStatus.Valid;
        return result;
    }
}

/// <summary>
/// Sampler/Scheduler Block Configuration
/// </summary>
public class SamplerBlockConfig : IBlockConfig
{
    // Sampler Selection
    public SamplerType Sampler { get; set; } = SamplerType.DPMPlusPlus2MKarras;
    public SchedulerType Scheduler { get; set; } = SchedulerType.Normal;

    // Generation Parameters
    public int Steps { get; set; } = 30;
    public double CfgScale { get; set; } = 7.5;
    public long Seed { get; set; } = -1; // -1 = random

    // Image Size
    public int Width { get; set; } = 1024;
    public int Height { get; set; } = 1024;

    // Batch Settings
    public int BatchSize { get; set; } = 1;
    public int BatchCount { get; set; } = 1;

    // Advanced Sampling
    public double Denoise { get; set; } = 1.0;
    public bool UseKarras { get; set; } = true;
    public double SigmaMin { get; set; } = 0.0;
    public double SigmaMax { get; set; } = 0.0;
    public double Rho { get; set; } = 7.0;

    // Tiling/Hi-res
    public bool EnableTiling { get; set; } = false;
    public bool HiResFix { get; set; } = false;
    public double HiResScale { get; set; } = 1.5;
    public int HiResSteps { get; set; } = 15;
    public double HiResDenoise { get; set; } = 0.5;
    public SamplerType HiResSampler { get; set; } = SamplerType.DPMPlusPlus2MKarras;

    // Video Specific
    public int FrameCount { get; set; } = 16;
    public int Fps { get; set; } = 8;
    public double MotionScale { get; set; } = 1.0;

    public BlockValidationResult Validate()
    {
        var result = new BlockValidationResult { BlockName = "Sampler" };

        if (Steps < 1 || Steps > 150)
        {
            result.Errors.Add("Steps must be between 1 and 150");
            result.Status = BlockStatus.Invalid;
            return result;
        }

        if (CfgScale < 1 || CfgScale > 30)
        {
            result.Warnings.Add($"CFG Scale {CfgScale} is unusual (recommended 5-15)");
        }

        if (Width < 64 || Width > 4096 || Height < 64 || Height > 4096)
        {
            result.Errors.Add("Image dimensions must be between 64 and 4096");
            result.Status = BlockStatus.Invalid;
            return result;
        }

        if (Width % 8 != 0 || Height % 8 != 0)
        {
            result.Errors.Add("Image dimensions must be multiples of 8");
            result.Status = BlockStatus.Invalid;
            return result;
        }

        // VRAM warnings
        var pixels = Width * Height;
        if (pixels > 2048 * 2048)
        {
            result.Warnings.Add("Large image size may require high VRAM (>12GB)");
        }

        if (BatchSize > 4)
        {
            result.Warnings.Add("Large batch size may cause out of memory errors");
        }

        if (Denoise < 0 || Denoise > 1)
        {
            result.Errors.Add("Denoise must be between 0 and 1");
            result.Status = BlockStatus.Invalid;
            return result;
        }

        result.Status = result.Warnings.Count > 0 ? BlockStatus.Warning : BlockStatus.Valid;
        return result;
    }
}

/// <summary>
/// Processor Block Configuration
/// </summary>
public class ProcessorBlockConfig : IBlockConfig
{
    public ProcessorType ProcessorType { get; set; } = ProcessorType.Diffusers;
    public DistributionStrategy Strategy { get; set; } = DistributionStrategy.Auto;

    // GPU Pool Settings
    public List<string> PreferredWorkers { get; set; } = new();
    public double MinVramGb { get; set; } = 8.0;
    public int MaxConcurrent { get; set; } = 1;
    public int TimeoutSeconds { get; set; } = 300;

    // ComfyUI Settings
    public string ComfyUIUrl { get; set; } = "http://127.0.0.1:8188";
    public string? WorkflowPath { get; set; }

    // Diffusers Settings
    public bool UseSafetensors { get; set; } = true;
    public bool EnableAttentionSlicing { get; set; } = true;
    public bool EnableVaeSlicing { get; set; } = true;
    public bool UseXformers { get; set; } = true;
    public string Device { get; set; } = "cuda";

    // Status (set by validation)
    public bool IsAvailable { get; set; } = false;
    public string StatusMessage { get; set; } = "Not checked";

    public BlockValidationResult Validate()
    {
        var result = new BlockValidationResult { BlockName = "Processor" };

        if (!IsAvailable)
        {
            result.Errors.Add($"{ProcessorType} processor is not available: {StatusMessage}");
            result.Status = BlockStatus.Invalid;
            return result;
        }

        if (MinVramGb < 4)
        {
            result.Warnings.Add("Minimum VRAM is very low, may not work with most models");
        }

        if (TimeoutSeconds < 30)
        {
            result.Warnings.Add("Timeout is very short, complex generations may fail");
        }

        result.Status = result.Warnings.Count > 0 ? BlockStatus.Warning : BlockStatus.Valid;
        return result;
    }
}

/// <summary>
/// Post-Processing Block Configuration
/// </summary>
public class PostProcessBlockConfig : IBlockConfig
{
    // Upscaling
    public bool EnableUpscale { get; set; } = false;
    public UpscalerType Upscaler { get; set; } = UpscalerType.RealESRGAN4x;
    public double UpscaleScale { get; set; } = 2.0;

    // Face Restoration
    public bool EnableFaceRestore { get; set; } = false;
    public FaceRestoreType FaceRestorer { get; set; } = FaceRestoreType.CodeFormer;
    public double FaceRestoreStrength { get; set; } = 0.8;

    // Color Correction
    public bool EnableColorCorrection { get; set; } = false;
    public double Brightness { get; set; } = 0;
    public double Contrast { get; set; } = 0;
    public double Saturation { get; set; } = 0;

    // Watermark
    public bool AddWatermark { get; set; } = false;
    public string? WatermarkText { get; set; }
    public string? WatermarkImagePath { get; set; }
    public WatermarkPosition WatermarkPosition { get; set; } = WatermarkPosition.BottomRight;

    public BlockValidationResult Validate()
    {
        var result = new BlockValidationResult { BlockName = "PostProcess" };

        if (EnableUpscale && UpscaleScale < 1)
        {
            result.Errors.Add("Upscale scale must be at least 1.0");
            result.Status = BlockStatus.Invalid;
            return result;
        }

        if (EnableUpscale && UpscaleScale > 4)
        {
            result.Warnings.Add("Large upscale factor requires significant VRAM and time");
        }

        if (EnableFaceRestore && FaceRestoreStrength < 0 || FaceRestoreStrength > 1)
        {
            result.Warnings.Add("Face restore strength should be between 0 and 1");
        }

        result.Status = result.Warnings.Count > 0 ? BlockStatus.Warning : BlockStatus.Valid;
        return result;
    }
}

/// <summary>
/// Output Block Configuration
/// </summary>
public class OutputBlockConfig : IBlockConfig
{
    public string OutputDirectory { get; set; } = "";
    public string FileNamePattern { get; set; } = "postx_{date}_{time}_{seed}";
    public ImageFormat ImageFormat { get; set; } = ImageFormat.PNG;
    public int JpegQuality { get; set; } = 95;
    public VideoFormat VideoFormat { get; set; } = VideoFormat.MP4;
    public bool SaveMetadata { get; set; } = true;
    public bool AutoOpen { get; set; } = false;

    public BlockValidationResult Validate()
    {
        var result = new BlockValidationResult { BlockName = "Output" };

        if (string.IsNullOrWhiteSpace(OutputDirectory))
        {
            // Use default directory
            OutputDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                "PostXAgent");
        }

        try
        {
            if (!Directory.Exists(OutputDirectory))
            {
                Directory.CreateDirectory(OutputDirectory);
            }
        }
        catch
        {
            result.Errors.Add($"Cannot create output directory: {OutputDirectory}");
            result.Status = BlockStatus.Invalid;
            return result;
        }

        if (JpegQuality < 1 || JpegQuality > 100)
        {
            result.Warnings.Add("JPEG quality should be between 1 and 100");
        }

        result.Status = result.Warnings.Count > 0 ? BlockStatus.Warning : BlockStatus.Valid;
        return result;
    }
}

#region Sub-Configurations

/// <summary>
/// LoRA Configuration
/// </summary>
public class LoraConfig
{
    public string Name { get; set; } = "";
    public string? Id { get; set; }
    public string? Path { get; set; }
    public double Weight { get; set; } = 1.0;
    public double ClipWeight { get; set; } = 1.0;
    public bool Enabled { get; set; } = true;
}

/// <summary>
/// ControlNet Configuration
/// </summary>
public class ControlNetConfig
{
    public string Name { get; set; } = "";
    public ControlNetType Type { get; set; } = ControlNetType.Canny;
    public string? ModelPath { get; set; }
    public string? InputImagePath { get; set; }
    public double Weight { get; set; } = 1.0;
    public double GuidanceStart { get; set; } = 0.0;
    public double GuidanceEnd { get; set; } = 1.0;
    public ControlNetPreprocessor Preprocessor { get; set; } = ControlNetPreprocessor.Auto;
    public bool Enabled { get; set; } = true;

    public BlockValidationResult Validate()
    {
        var result = new BlockValidationResult { BlockName = $"ControlNet:{Name}" };

        if (string.IsNullOrWhiteSpace(InputImagePath))
        {
            result.Errors.Add("ControlNet requires an input image");
            result.Status = BlockStatus.Invalid;
            return result;
        }

        if (!File.Exists(InputImagePath))
        {
            result.Errors.Add($"ControlNet input image not found: {InputImagePath}");
            result.Status = BlockStatus.Invalid;
            return result;
        }

        result.Status = BlockStatus.Valid;
        return result;
    }
}

#endregion

#region Validation Results

public interface IBlockConfig
{
    BlockValidationResult Validate();
}

public class BlockValidationResult
{
    public string BlockName { get; set; } = "";
    public BlockStatus Status { get; set; } = BlockStatus.Pending;
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public bool IsValid => Status != BlockStatus.Invalid && Errors.Count == 0;
}

public class PipelineValidationResult
{
    public bool IsValid { get; set; }
    public string? FirstInvalidBlock { get; set; }
    public Dictionary<string, BlockValidationResult> BlockResults { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

public enum BlockStatus
{
    Pending,    // Not yet validated
    Valid,      // All checks passed
    Warning,    // Passed with warnings
    Invalid,    // Failed validation
    Processing  // Currently generating
}

#endregion

#region Enums

public enum GenerationType
{
    Image,
    Video,
    Img2Img,
    Inpainting,
    Outpainting
}

public enum ModelPrecision
{
    FP32,
    FP16,
    BF16,
    INT8
}

public enum SamplerType
{
    Euler,
    EulerA,
    Heun,
    DPM2,
    DPM2A,
    DPMPlusPlus2M,
    DPMPlusPlus2MKarras,
    DPMPlusPlus2MSDE,
    DPMPlusPlusSDE,
    DPMPlusPlusSDEKarras,
    LMS,
    PLMS,
    DDIM,
    DDPM,
    UniPC,
    LCM,
    TCD
}

public enum SchedulerType
{
    Normal,
    Karras,
    Exponential,
    SGMUniform,
    Simple,
    DDIM_Uniform,
    Beta,
    Linear
}

public enum ProcessorType
{
    Diffusers,
    ComfyUI,
    GpuPool,
    Combined,
    A1111
}

public enum DistributionStrategy
{
    Auto,
    RoundRobin,
    LeastLoaded,
    Priority,
    Random
}

public enum UpscalerType
{
    None,
    RealESRGAN4x,
    RealESRGAN4xAnime,
    ESRGAN4x,
    Lanczos,
    Bicubic,
    UltraSharp4x,
    Remacri4x
}

public enum FaceRestoreType
{
    None,
    CodeFormer,
    GFPGAN,
    RestoreFormer
}

public enum WatermarkPosition
{
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight,
    Center
}

public enum ImageFormat
{
    PNG,
    JPEG,
    WEBP,
    TIFF,
    BMP
}

public enum VideoFormat
{
    MP4,
    GIF,
    WEBM,
    AVI,
    MOV
}

public enum ControlNetType
{
    Canny,
    Depth,
    OpenPose,
    Scribble,
    Lineart,
    LineartAnime,
    SoftEdge,
    Shuffle,
    Tile,
    Inpaint,
    NormalMap,
    MLSD,
    Seg,
    Reference,
    IPAdapter
}

public enum ControlNetPreprocessor
{
    Auto,
    None,
    Canny,
    DepthMidas,
    DepthZoe,
    OpenPose,
    OpenPoseFace,
    OpenPoseFull,
    Scribble,
    LineartCoarse,
    LineartRealistic,
    LineartAnime,
    SoftEdge,
    Shuffle,
    Tile,
    NormalBae,
    MLSD,
    Seg
}

#endregion
