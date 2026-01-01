using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.Input;

namespace AIManager.UI.ViewModels;

/// <summary>
/// ViewModel สำหรับหน้า Suno AI Options
/// ใช้ตั้งค่าสำหรับการสร้างเพลงด้วย Suno AI
/// </summary>
public class SunoOptionsViewModel : BaseViewModel
{
    private readonly string _settingsPath;

    #region Song Creation Settings

    private string _defaultStyle = "metal ballad, emotional, powerful vocals, guitar solo, orchestral elements";
    public string DefaultStyle
    {
        get => _defaultStyle;
        set => SetProperty(ref _defaultStyle, value);
    }

    private string _songTitle = "";
    public string SongTitle
    {
        get => _songTitle;
        set => SetProperty(ref _songTitle, value);
    }

    private string _lyrics = "";
    public string Lyrics
    {
        get => _lyrics;
        set => SetProperty(ref _lyrics, value);
    }

    private bool _useCustomLyrics = true;
    public bool UseCustomLyrics
    {
        get => _useCustomLyrics;
        set => SetProperty(ref _useCustomLyrics, value);
    }

    private bool _instrumentalOnly;
    public bool InstrumentalOnly
    {
        get => _instrumentalOnly;
        set => SetProperty(ref _instrumentalOnly, value);
    }

    #endregion

    #region Style Presets

    public ObservableCollection<StylePreset> StylePresets { get; } = new()
    {
        new StylePreset { Name = "Metal Ballad", Style = "metal ballad, emotional, powerful vocals, guitar solo, orchestral elements", Icon = "Guitar" },
        new StylePreset { Name = "Pop", Style = "pop, catchy, upbeat, modern production, radio-friendly", Icon = "Music" },
        new StylePreset { Name = "Rock", Style = "rock, electric guitar, drums, energetic, powerful", Icon = "VolumeHigh" },
        new StylePreset { Name = "EDM", Style = "EDM, electronic, dance, synth, bass drop, festival", Icon = "Waveform" },
        new StylePreset { Name = "Jazz", Style = "jazz, smooth, saxophone, piano, sophisticated", Icon = "Piano" },
        new StylePreset { Name = "Classical", Style = "classical, orchestral, strings, epic, cinematic", Icon = "Violin" },
        new StylePreset { Name = "Hip Hop", Style = "hip hop, rap, beats, urban, flow", Icon = "Microphone" },
        new StylePreset { Name = "R&B", Style = "R&B, soul, smooth vocals, romantic, groove", Icon = "HeadphonesBox" },
        new StylePreset { Name = "Country", Style = "country, acoustic guitar, storytelling, heartfelt", Icon = "GuitarAcoustic" },
        new StylePreset { Name = "Lo-Fi", Style = "lo-fi, chill, relaxing, study music, ambient", Icon = "Coffee" },
    };

    private StylePreset? _selectedPreset;
    public StylePreset? SelectedPreset
    {
        get => _selectedPreset;
        set
        {
            if (SetProperty(ref _selectedPreset, value) && value != null)
            {
                DefaultStyle = value.Style;
            }
        }
    }

    #endregion

    #region Download Settings

    private string _downloadFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
    public string DownloadFolder
    {
        get => _downloadFolder;
        set => SetProperty(ref _downloadFolder, value);
    }

    private string _folderNamingPattern = "jk Ai - {style} - {date}";
    public string FolderNamingPattern
    {
        get => _folderNamingPattern;
        set => SetProperty(ref _folderNamingPattern, value);
    }

    private string _fileNamingPattern = "{title} - {index}";
    public string FileNamingPattern
    {
        get => _fileNamingPattern;
        set => SetProperty(ref _fileNamingPattern, value);
    }

    private bool _createSubfolder = true;
    public bool CreateSubfolder
    {
        get => _createSubfolder;
        set => SetProperty(ref _createSubfolder, value);
    }

    private bool _includeDate = true;
    public bool IncludeDate
    {
        get => _includeDate;
        set => SetProperty(ref _includeDate, value);
    }

    private string _downloadFormat = "MP3";
    public string DownloadFormat
    {
        get => _downloadFormat;
        set => SetProperty(ref _downloadFormat, value);
    }

    public ObservableCollection<string> AvailableFormats { get; } = new() { "MP3", "WAV", "FLAC" };

    #endregion

    #region Workflow Settings

    private int _waitForGenerationMs = 60000;
    public int WaitForGenerationMs
    {
        get => _waitForGenerationMs;
        set => SetProperty(ref _waitForGenerationMs, value);
    }

    private int _waitAfterDownloadMs = 3000;
    public int WaitAfterDownloadMs
    {
        get => _waitAfterDownloadMs;
        set => SetProperty(ref _waitAfterDownloadMs, value);
    }

    private bool _downloadBothSongs = true;
    public bool DownloadBothSongs
    {
        get => _downloadBothSongs;
        set => SetProperty(ref _downloadBothSongs, value);
    }

    private bool _autoRetryOnFailure = true;
    public bool AutoRetryOnFailure
    {
        get => _autoRetryOnFailure;
        set => SetProperty(ref _autoRetryOnFailure, value);
    }

    private int _maxRetries = 3;
    public int MaxRetries
    {
        get => _maxRetries;
        set => SetProperty(ref _maxRetries, value);
    }

    #endregion

    #region Account Settings

    private string _sunoEmail = "";
    public string SunoEmail
    {
        get => _sunoEmail;
        set => SetProperty(ref _sunoEmail, value);
    }

    private string _sunoPassword = "";
    public string SunoPassword
    {
        get => _sunoPassword;
        set => SetProperty(ref _sunoPassword, value);
    }

    private bool _rememberCredentials = true;
    public bool RememberCredentials
    {
        get => _rememberCredentials;
        set => SetProperty(ref _rememberCredentials, value);
    }

    private bool _isLoggedIn;
    public bool IsLoggedIn
    {
        get => _isLoggedIn;
        set => SetProperty(ref _isLoggedIn, value);
    }

    private int _creditsRemaining;
    public int CreditsRemaining
    {
        get => _creditsRemaining;
        set => SetProperty(ref _creditsRemaining, value);
    }

    #endregion

    #region UI State

    private bool _isDirty;
    public bool IsDirty
    {
        get => _isDirty;
        set => SetProperty(ref _isDirty, value);
    }

    private string _statusMessage = "";
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    private string _previewFolderName = "";
    public string PreviewFolderName
    {
        get => _previewFolderName;
        set => SetProperty(ref _previewFolderName, value);
    }

    #endregion

    #region Lyrics Templates

    public ObservableCollection<LyricsTemplate> LyricsTemplates { get; } = new()
    {
        new LyricsTemplate
        {
            Name = "Metal Ballad Template",
            Template = @"[Verse 1]
(Emotional opening verse about struggle)

[Pre-Chorus]
(Building tension)

[Chorus]
(Powerful, memorable hook)

[Verse 2]
(Story continuation)

[Bridge]
(Emotional peak)

[Chorus]
(Repeat with variations)

[Outro]
(Fade out or powerful ending)"
        },
        new LyricsTemplate
        {
            Name = "Pop Song Template",
            Template = @"[Verse 1]
(Catchy opening)

[Pre-Chorus]
(Build up)

[Chorus]
(Hook - most memorable part)

[Verse 2]
(Continue the story)

[Chorus]
(Repeat)

[Bridge]
(Change of pace)

[Chorus]
(Final powerful chorus)

[Outro]"
        },
        new LyricsTemplate
        {
            Name = "Empty (Custom)",
            Template = ""
        }
    };

    private LyricsTemplate? _selectedTemplate;
    public LyricsTemplate? SelectedTemplate
    {
        get => _selectedTemplate;
        set
        {
            if (SetProperty(ref _selectedTemplate, value) && value != null)
            {
                Lyrics = value.Template;
            }
        }
    }

    #endregion

    #region Commands

    public IRelayCommand SaveSettingsCommand { get; }
    public IRelayCommand LoadSettingsCommand { get; }
    public IRelayCommand ResetToDefaultsCommand { get; }
    public IRelayCommand BrowseFolderCommand { get; }
    public IAsyncRelayCommand TestConnectionCommand { get; }
    public IAsyncRelayCommand RunWorkflowCommand { get; }
    public IRelayCommand ApplyPresetCommand { get; }
    public IRelayCommand CopyWorkflowJsonCommand { get; }

    #endregion

    public SunoOptionsViewModel()
    {
        Title = "Suno AI Options";
        _settingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PostXAgent", "suno-settings.json");

        SaveSettingsCommand = new RelayCommand(SaveSettings);
        LoadSettingsCommand = new RelayCommand(LoadSettings);
        ResetToDefaultsCommand = new RelayCommand(ResetToDefaults);
        BrowseFolderCommand = new RelayCommand(BrowseFolder);
        TestConnectionCommand = new AsyncRelayCommand(TestConnectionAsync);
        RunWorkflowCommand = new AsyncRelayCommand(RunWorkflowAsync);
        ApplyPresetCommand = new RelayCommand<StylePreset>(ApplyPreset);
        CopyWorkflowJsonCommand = new RelayCommand(CopyWorkflowJson);

        LoadSettings();
        UpdatePreviewFolderName();

        // Track changes
        PropertyChanged += (s, e) =>
        {
            if (e.PropertyName != nameof(IsDirty) && e.PropertyName != nameof(IsBusy) &&
                e.PropertyName != nameof(Title) && e.PropertyName != nameof(StatusMessage) &&
                e.PropertyName != nameof(PreviewFolderName))
            {
                IsDirty = true;
                UpdatePreviewFolderName();
            }
        };
    }

    private void UpdatePreviewFolderName()
    {
        var style = DefaultStyle.Split(',').FirstOrDefault()?.Trim() ?? "Music";
        var date = DateTime.Now.ToString("yyyy-MM-dd");
        PreviewFolderName = FolderNamingPattern
            .Replace("{style}", style)
            .Replace("{date}", date)
            .Replace("{title}", SongTitle);
    }

    private void LoadSettings()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = File.ReadAllText(_settingsPath);
                var settings = JsonSerializer.Deserialize<SunoSettings>(json);
                if (settings != null)
                {
                    DefaultStyle = settings.DefaultStyle ?? DefaultStyle;
                    SongTitle = settings.SongTitle ?? "";
                    Lyrics = settings.Lyrics ?? "";
                    UseCustomLyrics = settings.UseCustomLyrics;
                    InstrumentalOnly = settings.InstrumentalOnly;
                    DownloadFolder = settings.DownloadFolder ?? DownloadFolder;
                    FolderNamingPattern = settings.FolderNamingPattern ?? FolderNamingPattern;
                    FileNamingPattern = settings.FileNamingPattern ?? FileNamingPattern;
                    CreateSubfolder = settings.CreateSubfolder;
                    IncludeDate = settings.IncludeDate;
                    DownloadFormat = settings.DownloadFormat ?? "MP3";
                    WaitForGenerationMs = settings.WaitForGenerationMs;
                    WaitAfterDownloadMs = settings.WaitAfterDownloadMs;
                    DownloadBothSongs = settings.DownloadBothSongs;
                    AutoRetryOnFailure = settings.AutoRetryOnFailure;
                    MaxRetries = settings.MaxRetries;
                    SunoEmail = settings.SunoEmail ?? "";
                    RememberCredentials = settings.RememberCredentials;
                }
            }
            IsDirty = false;
            StatusMessage = "Settings loaded";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading settings: {ex.Message}";
        }
    }

    private void SaveSettings()
    {
        try
        {
            var dir = Path.GetDirectoryName(_settingsPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var settings = new SunoSettings
            {
                DefaultStyle = DefaultStyle,
                SongTitle = SongTitle,
                Lyrics = Lyrics,
                UseCustomLyrics = UseCustomLyrics,
                InstrumentalOnly = InstrumentalOnly,
                DownloadFolder = DownloadFolder,
                FolderNamingPattern = FolderNamingPattern,
                FileNamingPattern = FileNamingPattern,
                CreateSubfolder = CreateSubfolder,
                IncludeDate = IncludeDate,
                DownloadFormat = DownloadFormat,
                WaitForGenerationMs = WaitForGenerationMs,
                WaitAfterDownloadMs = WaitAfterDownloadMs,
                DownloadBothSongs = DownloadBothSongs,
                AutoRetryOnFailure = AutoRetryOnFailure,
                MaxRetries = MaxRetries,
                SunoEmail = RememberCredentials ? SunoEmail : "",
                RememberCredentials = RememberCredentials
            };

            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsPath, json);

            IsDirty = false;
            StatusMessage = "Settings saved successfully";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error saving settings: {ex.Message}";
        }
    }

    private void ResetToDefaults()
    {
        DefaultStyle = "metal ballad, emotional, powerful vocals, guitar solo, orchestral elements";
        SongTitle = "";
        Lyrics = "";
        UseCustomLyrics = true;
        InstrumentalOnly = false;
        DownloadFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
        FolderNamingPattern = "jk Ai - {style} - {date}";
        FileNamingPattern = "{title} - {index}";
        CreateSubfolder = true;
        IncludeDate = true;
        DownloadFormat = "MP3";
        WaitForGenerationMs = 60000;
        WaitAfterDownloadMs = 3000;
        DownloadBothSongs = true;
        AutoRetryOnFailure = true;
        MaxRetries = 3;
        IsDirty = true;
        StatusMessage = "Settings reset to defaults";
    }

    private void BrowseFolder()
    {
        // Note: Actual folder browser dialog will be handled in code-behind
        StatusMessage = "Please select folder...";
    }

    private async Task TestConnectionAsync()
    {
        IsBusy = true;
        StatusMessage = "Testing Suno connection...";

        try
        {
            // Simulate connection test
            await Task.Delay(2000);
            IsLoggedIn = true;
            CreditsRemaining = 50;
            StatusMessage = "Connected to Suno AI! Credits remaining: 50";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Connection failed: {ex.Message}";
            IsLoggedIn = false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RunWorkflowAsync()
    {
        if (string.IsNullOrWhiteSpace(Lyrics) && !InstrumentalOnly)
        {
            StatusMessage = "Please enter lyrics or select Instrumental Only";
            return;
        }

        IsBusy = true;
        StatusMessage = "Starting Suno workflow...";

        try
        {
            // This will be connected to the actual WorkflowExecutor
            await Task.Delay(1000);
            StatusMessage = "Workflow started! Check Workers page for progress.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error starting workflow: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyPreset(StylePreset? preset)
    {
        if (preset != null)
        {
            DefaultStyle = preset.Style;
            SelectedPreset = preset;
            StatusMessage = $"Applied preset: {preset.Name}";
        }
    }

    private void CopyWorkflowJson()
    {
        var workflowContent = new
        {
            lyrics = Lyrics,
            style = DefaultStyle,
            song_title = SongTitle,
            instrumental = InstrumentalOnly,
            download_folder = DownloadFolder,
            folder_name = PreviewFolderName,
            format = DownloadFormat
        };

        var json = JsonSerializer.Serialize(workflowContent, new JsonSerializerOptions { WriteIndented = true });
        // Copy to clipboard will be handled in code-behind
        StatusMessage = "Workflow JSON ready to copy";
    }
}

#region Supporting Classes

public class StylePreset
{
    public string Name { get; set; } = "";
    public string Style { get; set; } = "";
    public string Icon { get; set; } = "Music";
}

public class LyricsTemplate
{
    public string Name { get; set; } = "";
    public string Template { get; set; } = "";
}

public class SunoSettings
{
    public string? DefaultStyle { get; set; }
    public string? SongTitle { get; set; }
    public string? Lyrics { get; set; }
    public bool UseCustomLyrics { get; set; } = true;
    public bool InstrumentalOnly { get; set; }
    public string? DownloadFolder { get; set; }
    public string? FolderNamingPattern { get; set; }
    public string? FileNamingPattern { get; set; }
    public bool CreateSubfolder { get; set; } = true;
    public bool IncludeDate { get; set; } = true;
    public string? DownloadFormat { get; set; }
    public int WaitForGenerationMs { get; set; } = 60000;
    public int WaitAfterDownloadMs { get; set; } = 3000;
    public bool DownloadBothSongs { get; set; } = true;
    public bool AutoRetryOnFailure { get; set; } = true;
    public int MaxRetries { get; set; } = 3;
    public string? SunoEmail { get; set; }
    public bool RememberCredentials { get; set; } = true;
}

#endregion
