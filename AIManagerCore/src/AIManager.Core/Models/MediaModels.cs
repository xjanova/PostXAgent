using System.Text.Json.Serialization;

namespace AIManager.Core.Models;

/// <summary>
/// Configuration for AI video generation
/// รวมการตั้งค่าทั้งหมดสำหรับการสร้างวีดีโอด้วย AI
/// </summary>
public class VideoGenerationConfig
{
    /// <summary>
    /// โหมดการสร้างวีดีโอ (Text-to-Video, Image-to-Video, etc.)
    /// </summary>
    public VideoGenerationMode Mode { get; set; } = VideoGenerationMode.TextToVideo;

    /// <summary>
    /// Prompt/คำอธิบายสำหรับสร้างวีดีโอ
    /// </summary>
    public string Prompt { get; set; } = string.Empty;

    /// <summary>
    /// Negative prompt (สิ่งที่ไม่ต้องการให้ปรากฏ)
    /// </summary>
    public string? NegativePrompt { get; set; }

    /// <summary>
    /// ความยาววีดีโอ (วินาที)
    /// </summary>
    public int Duration { get; set; } = 5;

    /// <summary>
    /// อัตราส่วนของวีดีโอ
    /// </summary>
    public AspectRatio AspectRatio { get; set; } = AspectRatio.Landscape_16_9;

    /// <summary>
    /// คุณภาพของวีดีโอ
    /// </summary>
    public VideoQuality Quality { get; set; } = VideoQuality.High_1080p;

    /// <summary>
    /// FPS (Frames Per Second)
    /// </summary>
    public int Fps { get; set; } = 30;

    /// <summary>
    /// URL ของรูปภาพต้นแบบ (สำหรับ Image-to-Video mode)
    /// </summary>
    public string? SourceImageUrl { get; set; }

    /// <summary>
    /// URL ของวีดีโอต้นแบบ (สำหรับ Video-to-Video mode)
    /// </summary>
    public string? SourceVideoUrl { get; set; }

    /// <summary>
    /// Seed สำหรับการสร้างแบบสุ่ม (เพื่อ reproducibility)
    /// </summary>
    public int? Seed { get; set; }

    /// <summary>
    /// จำนวนวีดีโอที่ต้องการสร้าง
    /// </summary>
    public int NumberOfOutputs { get; set; } = 1;

    /// <summary>
    /// ความเข้มของการแปลง (0.0 - 1.0)
    /// </summary>
    public double Strength { get; set; } = 0.8;

    /// <summary>
    /// สไตล์ของวีดีโอ (optional)
    /// </summary>
    public string? Style { get; set; }

    /// <summary>
    /// การตั้งค่าเฉพาะ provider (Freepik, Runway, etc.)
    /// </summary>
    public Dictionary<string, object>? ProviderSpecific { get; set; }

    /// <summary>
    /// ตัวเลือกสำหรับ Freepik Pikaso AI
    /// </summary>
    [JsonIgnore]
    public FreepikOptions? FreepikOptions
    {
        get => ProviderSpecific?.ContainsKey("freepik") == true
            ? System.Text.Json.JsonSerializer.Deserialize<FreepikOptions>(
                System.Text.Json.JsonSerializer.Serialize(ProviderSpecific["freepik"]))
            : null;
        set
        {
            ProviderSpecific ??= new Dictionary<string, object>();
            if (value != null)
            {
                ProviderSpecific["freepik"] = value;
            }
        }
    }
}

/// <summary>
/// ตัวเลือกเฉพาะสำหรับ Freepik Pikaso AI
/// </summary>
public class FreepikOptions
{
    /// <summary>
    /// สไตล์แอนิเมชัน
    /// </summary>
    public string? AnimationStyle { get; set; } // smooth, dynamic, dramatic

    /// <summary>
    /// การเคลื่อนไหวของกล้อง
    /// </summary>
    public string? CameraMovement { get; set; } // static, pan, zoom, rotate, orbit

    /// <summary>
    /// ความเข้มของการเคลื่อนไหว (1-10)
    /// </summary>
    public int? MotionIntensity { get; set; }

    /// <summary>
    /// Color palette
    /// </summary>
    public string? ColorPalette { get; set; } // vibrant, pastel, monochrome, warm, cool

    /// <summary>
    /// แสงสว่าง
    /// </summary>
    public string? Lighting { get; set; } // natural, studio, dramatic, soft, neon

    /// <summary>
    /// End frame effect
    /// </summary>
    public string? EndFrame { get; set; } // zoom_in, zoom_out, fade, still
}

/// <summary>
/// Configuration for AI music generation
/// รวมการตั้งค่าทั้งหมดสำหรับการสร้างเพลงด้วย AI
/// </summary>
public class MusicGenerationConfig
{
    /// <summary>
    /// Prompt/คำอธิบายสำหรับสร้างเพลง
    /// </summary>
    public string Prompt { get; set; } = string.Empty;

    /// <summary>
    /// ความยาวของเพลง (วินาที)
    /// </summary>
    public int Duration { get; set; } = 30;

    /// <summary>
    /// ประเภทเพลง
    /// </summary>
    public MusicGenre? Genre { get; set; }

    /// <summary>
    /// อารมณ์ของเพลง
    /// </summary>
    public MusicMood? Mood { get; set; }

    /// <summary>
    /// เป็นเพลงบรรเลงหรือไม่ (ไม่มีเสียงร้อง)
    /// </summary>
    public bool Instrumental { get; set; } = false;

    /// <summary>
    /// เนื้อเพลง (ถ้าต้องการ)
    /// </summary>
    public string? Lyrics { get; set; }

    /// <summary>
    /// BPM (Beats Per Minute)
    /// </summary>
    public int? Bpm { get; set; }

    /// <summary>
    /// Key signature (e.g., "C Major", "A Minor")
    /// </summary>
    public string? KeySignature { get; set; }

    /// <summary>
    /// จำนวนเพลงที่ต้องการสร้าง
    /// </summary>
    public int NumberOfOutputs { get; set; } = 1;
}

/// <summary>
/// Configuration for media processing (video/audio)
/// รวมการตั้งค่าทั้งหมดสำหรับการประมวลผลมีเดีย
/// </summary>
public class MediaProcessingConfig
{
    /// <summary>
    /// Path ของไฟล์วีดีโอ input
    /// </summary>
    public string? VideoPath { get; set; }

    /// <summary>
    /// Path ของไฟล์เสียง input
    /// </summary>
    public string? AudioPath { get; set; }

    /// <summary>
    /// รูปแบบไฟล์ output (mp4, mov, webm, etc.)
    /// </summary>
    public string OutputFormat { get; set; } = "mp4";

    /// <summary>
    /// คุณภาพของวีดีโอ output
    /// </summary>
    public VideoQuality? OutputQuality { get; set; }

    /// <summary>
    /// ผสมเสียงหรือไม่
    /// </summary>
    public bool MixAudio { get; set; } = false;

    /// <summary>
    /// ระดับเสียง (0.0 - 1.0)
    /// </summary>
    public double AudioVolume { get; set; } = 1.0;

    /// <summary>
    /// รายการวีดีโอที่จะต่อกัน
    /// </summary>
    public List<string>? VideosToConcat { get; set; }

    /// <summary>
    /// สร้าง thumbnail หรือไม่
    /// </summary>
    public bool GenerateThumbnail { get; set; } = true;

    /// <summary>
    /// เวลาในวีดีโอที่จะใช้สร้าง thumbnail (วินาที)
    /// </summary>
    public double ThumbnailTimeOffset { get; set; } = 1.0;

    /// <summary>
    /// Codec สำหรับ encode วีดีโอ
    /// </summary>
    public string VideoCodec { get; set; } = "libx264";

    /// <summary>
    /// Codec สำหรับ encode เสียง
    /// </summary>
    public string AudioCodec { get; set; } = "aac";

    /// <summary>
    /// Bitrate ของวีดีโอ (bits per second)
    /// </summary>
    public long? VideoBitrate { get; set; }

    /// <summary>
    /// Bitrate ของเสียง (bits per second)
    /// </summary>
    public long? AudioBitrate { get; set; }

    /// <summary>
    /// CRF (Constant Rate Factor) สำหรับ video quality (0-51, lower = better)
    /// </summary>
    public int Crf { get; set; } = 23;

    /// <summary>
    /// FFmpeg preset (ultrafast, fast, medium, slow, veryslow)
    /// </summary>
    public string Preset { get; set; } = "medium";
}

/// <summary>
/// Result from media generation (video/music)
/// ผลลัพธ์จากการสร้างมีเดีย
/// </summary>
public class MediaGenerationResult
{
    /// <summary>
    /// URL ของวีดีโอที่สร้าง (ถ้ามี)
    /// </summary>
    public string? VideoUrl { get; set; }

    /// <summary>
    /// Path ของไฟล์วีดีโอในระบบ
    /// </summary>
    public string? VideoPath { get; set; }

    /// <summary>
    /// URL ของเพลงที่สร้าง (ถ้ามี)
    /// </summary>
    public string? AudioUrl { get; set; }

    /// <summary>
    /// Path ของไฟล์เพลงในระบบ
    /// </summary>
    public string? AudioPath { get; set; }

    /// <summary>
    /// URL ของ thumbnail
    /// </summary>
    public string? ThumbnailUrl { get; set; }

    /// <summary>
    /// Path ของไฟล์ thumbnail ในระบบ
    /// </summary>
    public string? ThumbnailPath { get; set; }

    /// <summary>
    /// Metadata ของวีดีโอ
    /// </summary>
    public VideoMetadata? Metadata { get; set; }

    /// <summary>
    /// รายการ output หลายๆ ไฟล์ (ถ้ามี NumberOfOutputs > 1)
    /// </summary>
    public List<MediaOutput>? MultipleOutputs { get; set; }
}

/// <summary>
/// Output แต่ละไฟล์ (กรณีสร้างหลายไฟล์พร้อมกัน)
/// </summary>
public class MediaOutput
{
    public int Index { get; set; }
    public string? Url { get; set; }
    public string? Path { get; set; }
    public VideoMetadata? Metadata { get; set; }
}

/// <summary>
/// Video metadata information
/// ข้อมูลรายละเอียดของวีดีโอ
/// </summary>
public class VideoMetadata
{
    /// <summary>
    /// ความกว้าง (pixels)
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    /// ความสูง (pixels)
    /// </summary>
    public int Height { get; set; }

    /// <summary>
    /// ความยาว (วินาที)
    /// </summary>
    public double Duration { get; set; }

    /// <summary>
    /// FPS
    /// </summary>
    public int Fps { get; set; }

    /// <summary>
    /// ขนาดไฟล์ (bytes)
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    /// รูปแบบไฟล์ (mp4, mov, webm, etc.)
    /// </summary>
    public string Format { get; set; } = string.Empty;

    /// <summary>
    /// Video codec
    /// </summary>
    public string? Codec { get; set; }

    /// <summary>
    /// Bitrate (bits per second)
    /// </summary>
    public long? Bitrate { get; set; }

    /// <summary>
    /// อัตราส่วน (aspect ratio)
    /// </summary>
    public string? AspectRatio { get; set; }

    /// <summary>
    /// มี audio track หรือไม่
    /// </summary>
    public bool HasAudio { get; set; }

    /// <summary>
    /// Audio codec (ถ้ามี)
    /// </summary>
    public string? AudioCodec { get; set; }

    /// <summary>
    /// Audio bitrate (ถ้ามี)
    /// </summary>
    public long? AudioBitrate { get; set; }

    /// <summary>
    /// ข้อมูลเพิ่มเติม
    /// </summary>
    public Dictionary<string, object>? Extra { get; set; }

    /// <summary>
    /// วันเวลาที่สร้างไฟล์
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Audio metadata information
/// ข้อมูลรายละเอียดของไฟล์เสียง
/// </summary>
public class AudioMetadata
{
    /// <summary>
    /// ความยาว (วินาที)
    /// </summary>
    public double Duration { get; set; }

    /// <summary>
    /// ขนาดไฟล์ (bytes)
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    /// รูปแบบไฟล์ (mp3, wav, aac, etc.)
    /// </summary>
    public string Format { get; set; } = string.Empty;

    /// <summary>
    /// Codec
    /// </summary>
    public string? Codec { get; set; }

    /// <summary>
    /// Bitrate (bits per second)
    /// </summary>
    public long? Bitrate { get; set; }

    /// <summary>
    /// Sample rate (Hz)
    /// </summary>
    public int? SampleRate { get; set; }

    /// <summary>
    /// จำนวน channels (1 = mono, 2 = stereo)
    /// </summary>
    public int? Channels { get; set; }

    /// <summary>
    /// BPM (Beats Per Minute) ถ้าตรวจสอบได้
    /// </summary>
    public int? Bpm { get; set; }

    /// <summary>
    /// วันเวลาที่สร้างไฟล์
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
