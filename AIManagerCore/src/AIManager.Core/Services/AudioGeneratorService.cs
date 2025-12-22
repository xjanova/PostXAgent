using System.Net.Http.Json;
using System.Text.Json;
using AIManager.Core.Models;
using Microsoft.Extensions.Logging;

namespace AIManager.Core.Services;

/// <summary>
/// Audio Generator Service
/// รองรับ Text-to-Speech จากหลาย providers
/// </summary>
public class AudioGeneratorService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AudioGeneratorService>? _logger;

    // TTS Providers
    private const string EDGE_TTS_API = "https://api.edge-tts.com"; // Edge TTS (Free)
    private const string GOOGLE_TTS_API = "https://texttospeech.googleapis.com/v1";
    private const string ELEVENLABS_API = "https://api.elevenlabs.io/v1";
    private const string OPENAI_TTS_API = "https://api.openai.com/v1/audio/speech";

    public AudioGeneratorService(ILogger<AudioGeneratorService>? logger = null)
    {
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromMinutes(5);
        _logger = logger;
    }

    #region Voice Definitions

    /// <summary>
    /// Available Thai voices
    /// </summary>
    public static readonly List<VoiceOption> ThaiVoices = new()
    {
        // Edge TTS (Free)
        new VoiceOption { Id = "th-TH-PremwadeeNeural", Name = "Premwadee", Gender = "Female", Provider = "edge", IsFree = true, Language = "th" },
        new VoiceOption { Id = "th-TH-NiwatNeural", Name = "Niwat", Gender = "Male", Provider = "edge", IsFree = true, Language = "th" },

        // Google TTS
        new VoiceOption { Id = "th-TH-Standard-A", Name = "Thai Female A", Gender = "Female", Provider = "google", IsFree = false, Language = "th" },

        // ElevenLabs (Premium)
        new VoiceOption { Id = "21m00Tcm4TlvDq8ikWAM", Name = "Rachel", Gender = "Female", Provider = "elevenlabs", IsFree = false, IsPremium = true, Language = "multi" },
        new VoiceOption { Id = "AZnzlk1XvdvUeBnXmlld", Name = "Domi", Gender = "Female", Provider = "elevenlabs", IsFree = false, IsPremium = true, Language = "multi" },
        new VoiceOption { Id = "EXAVITQu4vr4xnSDxMaL", Name = "Bella", Gender = "Female", Provider = "elevenlabs", IsFree = false, IsPremium = true, Language = "multi" },
        new VoiceOption { Id = "ErXwobaYiN019PkySvjV", Name = "Antoni", Gender = "Male", Provider = "elevenlabs", IsFree = false, IsPremium = true, Language = "multi" },

        // OpenAI TTS
        new VoiceOption { Id = "alloy", Name = "Alloy", Gender = "Neutral", Provider = "openai", IsFree = false, Language = "multi" },
        new VoiceOption { Id = "echo", Name = "Echo", Gender = "Male", Provider = "openai", IsFree = false, Language = "multi" },
        new VoiceOption { Id = "fable", Name = "Fable", Gender = "Male", Provider = "openai", IsFree = false, Language = "multi" },
        new VoiceOption { Id = "onyx", Name = "Onyx", Gender = "Male", Provider = "openai", IsFree = false, Language = "multi" },
        new VoiceOption { Id = "nova", Name = "Nova", Gender = "Female", Provider = "openai", IsFree = false, Language = "multi" },
        new VoiceOption { Id = "shimmer", Name = "Shimmer", Gender = "Female", Provider = "openai", IsFree = false, Language = "multi" }
    };

    /// <summary>
    /// Available English voices
    /// </summary>
    public static readonly List<VoiceOption> EnglishVoices = new()
    {
        // Edge TTS (Free)
        new VoiceOption { Id = "en-US-JennyNeural", Name = "Jenny", Gender = "Female", Provider = "edge", IsFree = true, Language = "en" },
        new VoiceOption { Id = "en-US-GuyNeural", Name = "Guy", Gender = "Male", Provider = "edge", IsFree = true, Language = "en" },
        new VoiceOption { Id = "en-US-AriaNeural", Name = "Aria", Gender = "Female", Provider = "edge", IsFree = true, Language = "en" },
        new VoiceOption { Id = "en-GB-SoniaNeural", Name = "Sonia (UK)", Gender = "Female", Provider = "edge", IsFree = true, Language = "en" },

        // Google TTS
        new VoiceOption { Id = "en-US-Standard-C", Name = "US Female C", Gender = "Female", Provider = "google", IsFree = false, Language = "en" },
        new VoiceOption { Id = "en-US-Standard-D", Name = "US Male D", Gender = "Male", Provider = "google", IsFree = false, Language = "en" }
    };

    public List<VoiceOption> GetAvailableVoices(string language, bool includePremium = false)
    {
        var voices = language == "th" ? ThaiVoices : EnglishVoices;

        if (!includePremium)
        {
            voices = voices.Where(v => !v.IsPremium).ToList();
        }

        // Add multi-language voices
        voices.AddRange(ThaiVoices.Where(v => v.Language == "multi"));

        return voices.DistinctBy(v => v.Id).ToList();
    }

    #endregion

    #region Text-to-Speech Generation

    /// <summary>
    /// Generate audio from video script
    /// </summary>
    public async Task<GeneratedAudio> GenerateFromScriptAsync(
        VideoScript script,
        string voiceId,
        UserPackage package,
        CancellationToken ct = default)
    {
        var voice = ThaiVoices.Concat(EnglishVoices).FirstOrDefault(v => v.Id == voiceId)
            ?? ThaiVoices.First(v => v.IsFree);

        if (voice.IsPremium && !package.CanUsePremiumVoices)
        {
            voice = ThaiVoices.First(v => v.IsFree);
            _logger?.LogWarning("Premium voice not available for package, falling back to free voice");
        }

        _logger?.LogInformation("Generating audio with voice: {VoiceName} ({Provider})", voice.Name, voice.Provider);

        var audio = new GeneratedAudio
        {
            Type = AudioType.TextToSpeech,
            VoiceId = voice.Id,
            VoiceName = voice.Name,
            Provider = voice.Provider
        };

        // Generate narration for each scene
        var tempDir = Path.Combine(Path.GetTempPath(), "aimanager_audio", Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        double currentTime = 0;
        var audioFiles = new List<string>();

        foreach (var scene in script.Scenes)
        {
            if (string.IsNullOrEmpty(scene.Narration)) continue;

            var segment = new NarrationSegment
            {
                SceneNumber = scene.SceneNumber,
                Text = scene.Narration,
                StartTime = currentTime
            };

            try
            {
                var audioFile = Path.Combine(tempDir, $"scene_{scene.SceneNumber}.mp3");
                await GenerateSpeechAsync(scene.Narration, voice, audioFile, ct);

                segment.AudioFilePath = audioFile;
                audioFiles.Add(audioFile);

                // Estimate duration (rough calculation based on text length)
                var estimatedDuration = EstimateSpeechDuration(scene.Narration);
                segment.EndTime = currentTime + estimatedDuration;
                currentTime = segment.EndTime;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to generate audio for scene {SceneNumber}", scene.SceneNumber);
                segment.EndTime = currentTime + scene.DurationSeconds;
                currentTime = segment.EndTime;
            }

            audio.NarrationSegments.Add(segment);
        }

        audio.DurationSeconds = currentTime;

        // Merge all audio files
        if (audioFiles.Count > 0)
        {
            audio.LocalPath = Path.Combine(tempDir, "full_narration.mp3");
            await MergeAudioFilesAsync(audioFiles, audio.LocalPath, ct);
        }

        return audio;
    }

    /// <summary>
    /// Generate speech from text
    /// </summary>
    public async Task GenerateSpeechAsync(
        string text,
        VoiceOption voice,
        string outputPath,
        CancellationToken ct = default)
    {
        switch (voice.Provider)
        {
            case "edge":
                await GenerateWithEdgeTtsAsync(text, voice.Id, outputPath, ct);
                break;
            case "google":
                await GenerateWithGoogleTtsAsync(text, voice.Id, outputPath, ct);
                break;
            case "elevenlabs":
                await GenerateWithElevenLabsAsync(text, voice.Id, outputPath, ct);
                break;
            case "openai":
                await GenerateWithOpenAITtsAsync(text, voice.Id, outputPath, ct);
                break;
            default:
                throw new ArgumentException($"Unknown TTS provider: {voice.Provider}");
        }
    }

    private async Task GenerateWithEdgeTtsAsync(string text, string voiceId, string outputPath, CancellationToken ct)
    {
        // Edge TTS via command line (edge-tts package)
        // This requires edge-tts to be installed: pip install edge-tts

        var tempSsml = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.txt");
        await File.WriteAllTextAsync(tempSsml, text, ct);

        try
        {
            var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "edge-tts",
                    Arguments = $"--voice \"{voiceId}\" --file \"{tempSsml}\" --write-media \"{outputPath}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            process.Start();
            await process.WaitForExitAsync(ct);

            if (process.ExitCode != 0)
            {
                var error = await process.StandardError.ReadToEndAsync(ct);
                throw new Exception($"Edge TTS failed: {error}");
            }
        }
        finally
        {
            if (File.Exists(tempSsml)) File.Delete(tempSsml);
        }
    }

    private async Task GenerateWithGoogleTtsAsync(string text, string voiceId, string outputPath, CancellationToken ct)
    {
        var apiKey = Environment.GetEnvironmentVariable("GOOGLE_API_KEY");
        if (string.IsNullOrEmpty(apiKey))
        {
            throw new Exception("GOOGLE_API_KEY not configured");
        }

        var request = new
        {
            input = new { text },
            voice = new
            {
                languageCode = voiceId.StartsWith("th") ? "th-TH" : "en-US",
                name = voiceId
            },
            audioConfig = new { audioEncoding = "MP3" }
        };

        var response = await _httpClient.PostAsJsonAsync(
            $"{GOOGLE_TTS_API}/text:synthesize?key={apiKey}",
            request, ct);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"Google TTS failed: {json}");
        }

        var audioContent = json.GetProperty("audioContent").GetString();
        var audioBytes = Convert.FromBase64String(audioContent ?? "");
        await File.WriteAllBytesAsync(outputPath, audioBytes, ct);
    }

    private async Task GenerateWithElevenLabsAsync(string text, string voiceId, string outputPath, CancellationToken ct)
    {
        var apiKey = Environment.GetEnvironmentVariable("ELEVENLABS_API_KEY");
        if (string.IsNullOrEmpty(apiKey))
        {
            throw new Exception("ELEVENLABS_API_KEY not configured");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{ELEVENLABS_API}/text-to-speech/{voiceId}");
        request.Headers.Add("xi-api-key", apiKey);
        request.Content = JsonContent.Create(new
        {
            text,
            model_id = "eleven_multilingual_v2",
            voice_settings = new
            {
                stability = 0.5,
                similarity_boost = 0.75
            }
        });

        var response = await _httpClient.SendAsync(request, ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            throw new Exception($"ElevenLabs TTS failed: {error}");
        }

        var audioBytes = await response.Content.ReadAsByteArrayAsync(ct);
        await File.WriteAllBytesAsync(outputPath, audioBytes, ct);
    }

    private async Task GenerateWithOpenAITtsAsync(string text, string voiceId, string outputPath, CancellationToken ct)
    {
        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrEmpty(apiKey))
        {
            throw new Exception("OPENAI_API_KEY not configured");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, OPENAI_TTS_API);
        request.Headers.Add("Authorization", $"Bearer {apiKey}");
        request.Content = JsonContent.Create(new
        {
            model = "tts-1",
            input = text,
            voice = voiceId,
            response_format = "mp3"
        });

        var response = await _httpClient.SendAsync(request, ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            throw new Exception($"OpenAI TTS failed: {error}");
        }

        var audioBytes = await response.Content.ReadAsByteArrayAsync(ct);
        await File.WriteAllBytesAsync(outputPath, audioBytes, ct);
    }

    #endregion

    #region Background Music

    /// <summary>
    /// Available background music (royalty-free)
    /// </summary>
    public static readonly List<BackgroundMusicOption> AvailableMusic = new()
    {
        new BackgroundMusicOption { Id = "upbeat", Name = "Upbeat Pop", Mood = "happy", BPM = 120 },
        new BackgroundMusicOption { Id = "corporate", Name = "Corporate Motivational", Mood = "professional", BPM = 100 },
        new BackgroundMusicOption { Id = "chill", Name = "Chill Lofi", Mood = "relaxed", BPM = 85 },
        new BackgroundMusicOption { Id = "cinematic", Name = "Cinematic Epic", Mood = "dramatic", BPM = 90 },
        new BackgroundMusicOption { Id = "electronic", Name = "Electronic Dance", Mood = "energetic", BPM = 128 },
        new BackgroundMusicOption { Id = "acoustic", Name = "Acoustic Guitar", Mood = "warm", BPM = 95 },
        new BackgroundMusicOption { Id = "ambient", Name = "Ambient Calm", Mood = "peaceful", BPM = 70 }
    };

    /// <summary>
    /// Get recommended music based on content
    /// </summary>
    public BackgroundMusicOption GetRecommendedMusic(ContentConcept concept)
    {
        var mood = concept.Tone.ToLowerInvariant();

        return mood switch
        {
            "professional" or "formal" => AvailableMusic.First(m => m.Id == "corporate"),
            "funny" or "humorous" or "playful" => AvailableMusic.First(m => m.Id == "upbeat"),
            "calm" or "relaxed" or "peaceful" => AvailableMusic.First(m => m.Id == "chill"),
            "dramatic" or "exciting" or "intense" => AvailableMusic.First(m => m.Id == "cinematic"),
            "energetic" or "dynamic" => AvailableMusic.First(m => m.Id == "electronic"),
            "warm" or "friendly" or "casual" => AvailableMusic.First(m => m.Id == "acoustic"),
            _ => AvailableMusic.First(m => m.Id == "corporate")
        };
    }

    #endregion

    #region Helper Methods

    private double EstimateSpeechDuration(string text)
    {
        // Thai: ~4 characters per second
        // English: ~15 characters per second (including spaces)
        var charCount = text.Length;
        var hasThaiChars = text.Any(c => c >= 0x0E00 && c <= 0x0E7F);

        return hasThaiChars
            ? charCount / 4.0
            : charCount / 15.0;
    }

    private async Task MergeAudioFilesAsync(List<string> inputFiles, string outputPath, CancellationToken ct)
    {
        // Using ffmpeg to merge audio files
        var listFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}_list.txt");
        var listContent = string.Join("\n", inputFiles.Select(f => $"file '{f.Replace("\\", "/")}'"));
        await File.WriteAllTextAsync(listFile, listContent, ct);

        try
        {
            var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = $"-f concat -safe 0 -i \"{listFile}\" -c copy \"{outputPath}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            process.Start();
            await process.WaitForExitAsync(ct);

            if (process.ExitCode != 0 && !File.Exists(outputPath))
            {
                // Fallback: just copy the first file
                if (inputFiles.Count > 0)
                {
                    File.Copy(inputFiles[0], outputPath, true);
                }
            }
        }
        catch
        {
            // Fallback: just copy the first file
            if (inputFiles.Count > 0 && File.Exists(inputFiles[0]))
            {
                File.Copy(inputFiles[0], outputPath, true);
            }
        }
        finally
        {
            if (File.Exists(listFile)) File.Delete(listFile);
        }
    }

    #endregion
}

/// <summary>
/// Voice Option
/// </summary>
public class VoiceOption
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Gender { get; set; } = "";
    public string Provider { get; set; } = "";
    public string Language { get; set; } = "";
    public bool IsFree { get; set; }
    public bool IsPremium { get; set; }
}

/// <summary>
/// Background Music Option
/// </summary>
public class BackgroundMusicOption
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Mood { get; set; } = "";
    public int BPM { get; set; }
    public string? FilePath { get; set; }
}
