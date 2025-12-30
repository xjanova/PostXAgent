using System.Collections.ObjectModel;
using System.Windows;
using Microsoft.Win32;
using MyPostXAgent.Core.Models;
using MyPostXAgent.Core.Services;

namespace MyPostXAgent.UI.ViewModels;

/// <summary>
/// ViewModel for Video Editor Page - AI Video Creation
/// </summary>
public class VideoEditorViewModel : BaseViewModel
{
    private readonly LocalizationService _localizationService;

    // Video Projects
    public ObservableCollection<VideoProject> Projects { get; } = new();
    public ObservableCollection<VideoClip> Timeline { get; } = new();
    public ObservableCollection<string> AvailableTransitions { get; } = new();
    public ObservableCollection<string> AvailableEffects { get; } = new();

    private VideoProject? _selectedProject;
    public VideoProject? SelectedProject
    {
        get => _selectedProject;
        set
        {
            if (SetProperty(ref _selectedProject, value))
            {
                LoadProjectTimeline();
            }
        }
    }

    private VideoClip? _selectedClip;
    public VideoClip? SelectedClip
    {
        get => _selectedClip;
        set => SetProperty(ref _selectedClip, value);
    }

    // AI Generation Settings
    private string _aiPrompt = "";
    public string AIPrompt
    {
        get => _aiPrompt;
        set => SetProperty(ref _aiPrompt, value);
    }

    private AspectRatio _selectedAspectRatio = AspectRatio.Portrait_9_16;
    public AspectRatio SelectedAspectRatio
    {
        get => _selectedAspectRatio;
        set => SetProperty(ref _selectedAspectRatio, value);
    }

    private VideoQuality _selectedQuality = VideoQuality.FHD_1080p;
    public VideoQuality SelectedQuality
    {
        get => _selectedQuality;
        set => SetProperty(ref _selectedQuality, value);
    }

    private int _videoDuration = 15;
    public int VideoDuration
    {
        get => _videoDuration;
        set => SetProperty(ref _videoDuration, value);
    }

    private bool _generateMusic = true;
    public bool GenerateMusic
    {
        get => _generateMusic;
        set => SetProperty(ref _generateMusic, value);
    }

    private string _musicStyle = "Upbeat";
    public string MusicStyle
    {
        get => _musicStyle;
        set => SetProperty(ref _musicStyle, value);
    }

    // Video Preview
    private string _previewUrl = "";
    public string PreviewUrl
    {
        get => _previewUrl;
        set => SetProperty(ref _previewUrl, value);
    }

    private bool _isPlaying;
    public bool IsPlaying
    {
        get => _isPlaying;
        set => SetProperty(ref _isPlaying, value);
    }

    private double _currentPosition;
    public double CurrentPosition
    {
        get => _currentPosition;
        set => SetProperty(ref _currentPosition, value);
    }

    private double _totalDuration;
    public double TotalDuration
    {
        get => _totalDuration;
        set => SetProperty(ref _totalDuration, value);
    }

    // Generation Progress
    private bool _isGenerating;
    public bool IsGenerating
    {
        get => _isGenerating;
        set => SetProperty(ref _isGenerating, value);
    }

    private int _generationProgress;
    public int GenerationProgress
    {
        get => _generationProgress;
        set => SetProperty(ref _generationProgress, value);
    }

    private string _generationStatus = "";
    public string GenerationStatus
    {
        get => _generationStatus;
        set => SetProperty(ref _generationStatus, value);
    }

    // Dialog States
    private bool _showNewProjectDialog;
    public bool ShowNewProjectDialog
    {
        get => _showNewProjectDialog;
        set => SetProperty(ref _showNewProjectDialog, value);
    }

    private string _newProjectName = "";
    public string NewProjectName
    {
        get => _newProjectName;
        set => SetProperty(ref _newProjectName, value);
    }

    // Commands
    public RelayCommand NewProjectCommand { get; }
    public RelayCommand<VideoProject> OpenProjectCommand { get; }
    public RelayCommand<VideoProject> DeleteProjectCommand { get; }
    public RelayCommand ImportMediaCommand { get; }
    public RelayCommand GenerateAIVideoCommand { get; }
    public RelayCommand GenerateAIMusicCommand { get; }
    public RelayCommand<VideoClip> AddToTimelineCommand { get; }
    public RelayCommand<VideoClip> RemoveFromTimelineCommand { get; }
    public RelayCommand<VideoClip> MoveUpCommand { get; }
    public RelayCommand<VideoClip> MoveDownCommand { get; }
    public RelayCommand PlayPauseCommand { get; }
    public RelayCommand StopCommand { get; }
    public RelayCommand ExportVideoCommand { get; }
    public RelayCommand ConfirmNewProjectCommand { get; }
    public RelayCommand CancelNewProjectCommand { get; }
    public RelayCommand RefreshCommand { get; }

    public VideoEditorViewModel(LocalizationService localizationService)
    {
        _localizationService = localizationService;
        Title = LocalizationStrings.Nav.VideoEditor(_localizationService.IsThaiLanguage);

        // Initialize collections
        InitializeTransitions();
        InitializeEffects();

        // Commands
        NewProjectCommand = new RelayCommand(() => ShowNewProjectDialog = true);
        OpenProjectCommand = new RelayCommand<VideoProject>(OpenProject);
        DeleteProjectCommand = new RelayCommand<VideoProject>(async p => await DeleteProjectAsync(p));
        ImportMediaCommand = new RelayCommand(ImportMedia);
        GenerateAIVideoCommand = new RelayCommand(async () => await GenerateAIVideoAsync());
        GenerateAIMusicCommand = new RelayCommand(async () => await GenerateAIMusicAsync());
        AddToTimelineCommand = new RelayCommand<VideoClip>(AddToTimeline);
        RemoveFromTimelineCommand = new RelayCommand<VideoClip>(RemoveFromTimeline);
        MoveUpCommand = new RelayCommand<VideoClip>(MoveClipUp);
        MoveDownCommand = new RelayCommand<VideoClip>(MoveClipDown);
        PlayPauseCommand = new RelayCommand(TogglePlayPause);
        StopCommand = new RelayCommand(StopPlayback);
        ExportVideoCommand = new RelayCommand(async () => await ExportVideoAsync());
        ConfirmNewProjectCommand = new RelayCommand(async () => await CreateNewProjectAsync());
        CancelNewProjectCommand = new RelayCommand(() => { ShowNewProjectDialog = false; NewProjectName = ""; });
        RefreshCommand = new RelayCommand(async () => await LoadProjectsAsync());

        // Subscribe to language changes
        _localizationService.LanguageChanged += OnLanguageChanged;

        // Load initial data
        _ = LoadProjectsAsync();
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        Title = LocalizationStrings.Nav.VideoEditor(_localizationService.IsThaiLanguage);
    }

    private void InitializeTransitions()
    {
        AvailableTransitions.Add("None");
        AvailableTransitions.Add("Fade");
        AvailableTransitions.Add("Dissolve");
        AvailableTransitions.Add("Wipe Left");
        AvailableTransitions.Add("Wipe Right");
        AvailableTransitions.Add("Zoom In");
        AvailableTransitions.Add("Zoom Out");
        AvailableTransitions.Add("Slide Left");
        AvailableTransitions.Add("Slide Right");
    }

    private void InitializeEffects()
    {
        AvailableEffects.Add("None");
        AvailableEffects.Add("Brightness");
        AvailableEffects.Add("Contrast");
        AvailableEffects.Add("Saturation");
        AvailableEffects.Add("Blur");
        AvailableEffects.Add("Sharpen");
        AvailableEffects.Add("Vintage");
        AvailableEffects.Add("Black & White");
        AvailableEffects.Add("Sepia");
        AvailableEffects.Add("Vignette");
    }

    public async Task LoadProjectsAsync()
    {
        try
        {
            IsBusy = true;
            // TODO: Load from database
            await Task.Delay(100);

            // Sample projects for demo
            Application.Current.Dispatcher.Invoke(() =>
            {
                Projects.Clear();
                Projects.Add(new VideoProject
                {
                    Id = 1,
                    Name = "TikTok Promo",
                    AspectRatio = AspectRatio.Portrait_9_16,
                    Duration = TimeSpan.FromSeconds(30),
                    CreatedAt = DateTime.Now.AddDays(-2)
                });
                Projects.Add(new VideoProject
                {
                    Id = 2,
                    Name = "YouTube Intro",
                    AspectRatio = AspectRatio.Landscape_16_9,
                    Duration = TimeSpan.FromSeconds(60),
                    CreatedAt = DateTime.Now.AddDays(-5)
                });
            });
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void LoadProjectTimeline()
    {
        Timeline.Clear();
        if (SelectedProject == null) return;

        // Load clips for selected project
        // TODO: Load from database
    }

    private void OpenProject(VideoProject? project)
    {
        if (project == null) return;
        SelectedProject = project;
    }

    private async Task DeleteProjectAsync(VideoProject? project)
    {
        if (project == null) return;

        var isThai = _localizationService.IsThaiLanguage;
        var result = MessageBox.Show(
            isThai ? $"ต้องการลบโปรเจค '{project.Name}' หรือไม่?" : $"Delete project '{project.Name}'?",
            isThai ? "ยืนยันการลบ" : "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            // TODO: Delete from database
            Projects.Remove(project);
            if (SelectedProject == project)
            {
                SelectedProject = null;
            }
        }
    }

    private async Task CreateNewProjectAsync()
    {
        if (string.IsNullOrWhiteSpace(NewProjectName))
        {
            var isThai = _localizationService.IsThaiLanguage;
            MessageBox.Show(
                isThai ? "กรุณาใส่ชื่อโปรเจค" : "Please enter project name",
                isThai ? "ข้อมูลไม่ครบ" : "Missing Information",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var project = new VideoProject
        {
            Id = Projects.Count + 1,
            Name = NewProjectName,
            AspectRatio = SelectedAspectRatio,
            Duration = TimeSpan.Zero,
            CreatedAt = DateTime.Now
        };

        // TODO: Save to database
        Projects.Insert(0, project);
        SelectedProject = project;

        ShowNewProjectDialog = false;
        NewProjectName = "";
    }

    private void ImportMedia()
    {
        var dialog = new OpenFileDialog
        {
            Title = _localizationService.IsThaiLanguage ? "เลือกไฟล์สื่อ" : "Select Media Files",
            Filter = "Video Files|*.mp4;*.avi;*.mov;*.mkv|Image Files|*.jpg;*.jpeg;*.png;*.gif|Audio Files|*.mp3;*.wav;*.aac|All Files|*.*",
            Multiselect = true
        };

        if (dialog.ShowDialog() == true)
        {
            foreach (var file in dialog.FileNames)
            {
                var clip = new VideoClip
                {
                    Id = Timeline.Count + 1,
                    Name = System.IO.Path.GetFileName(file),
                    FilePath = file,
                    Duration = TimeSpan.FromSeconds(5), // Default duration
                    ClipType = DetermineClipType(file)
                };
                Timeline.Add(clip);
            }
        }
    }

    private ClipType DetermineClipType(string filePath)
    {
        var ext = System.IO.Path.GetExtension(filePath).ToLower();
        return ext switch
        {
            ".mp4" or ".avi" or ".mov" or ".mkv" => ClipType.Video,
            ".jpg" or ".jpeg" or ".png" or ".gif" => ClipType.Image,
            ".mp3" or ".wav" or ".aac" => ClipType.Audio,
            _ => ClipType.Video
        };
    }

    private async Task GenerateAIVideoAsync()
    {
        if (string.IsNullOrWhiteSpace(AIPrompt))
        {
            var isThai = _localizationService.IsThaiLanguage;
            MessageBox.Show(
                isThai ? "กรุณาใส่คำอธิบายสำหรับสร้างวิดีโอ" : "Please enter a prompt for video generation",
                isThai ? "ข้อมูลไม่ครบ" : "Missing Information",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        try
        {
            IsGenerating = true;
            GenerationProgress = 0;
            GenerationStatus = _localizationService.IsThaiLanguage ? "กำลังเริ่มสร้างวิดีโอ..." : "Starting video generation...";

            // Simulate AI video generation progress
            for (int i = 0; i <= 100; i += 10)
            {
                await Task.Delay(500);
                GenerationProgress = i;
                GenerationStatus = i switch
                {
                    <= 20 => _localizationService.IsThaiLanguage ? "กำลังวิเคราะห์ prompt..." : "Analyzing prompt...",
                    <= 40 => _localizationService.IsThaiLanguage ? "กำลังสร้าง keyframes..." : "Generating keyframes...",
                    <= 60 => _localizationService.IsThaiLanguage ? "กำลังสร้าง motion..." : "Creating motion...",
                    <= 80 => _localizationService.IsThaiLanguage ? "กำลัง render วิดีโอ..." : "Rendering video...",
                    _ => _localizationService.IsThaiLanguage ? "กำลังจัดเตรียมไฟล์..." : "Preparing file..."
                };
            }

            // Add generated clip to timeline
            var generatedClip = new VideoClip
            {
                Id = Timeline.Count + 1,
                Name = $"AI Video - {DateTime.Now:HHmmss}",
                FilePath = "", // TODO: Actual file path
                Duration = TimeSpan.FromSeconds(VideoDuration),
                ClipType = ClipType.Video,
                IsAIGenerated = true
            };
            Timeline.Add(generatedClip);

            var isThai = _localizationService.IsThaiLanguage;
            MessageBox.Show(
                isThai ? "สร้างวิดีโอ AI สำเร็จ!" : "AI Video generated successfully!",
                isThai ? "สำเร็จ" : "Success",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsGenerating = false;
            GenerationProgress = 0;
            GenerationStatus = "";
        }
    }

    private async Task GenerateAIMusicAsync()
    {
        try
        {
            IsGenerating = true;
            GenerationProgress = 0;
            GenerationStatus = _localizationService.IsThaiLanguage ? "กำลังสร้างเพลง..." : "Generating music...";

            // Simulate AI music generation
            for (int i = 0; i <= 100; i += 20)
            {
                await Task.Delay(300);
                GenerationProgress = i;
            }

            // Add generated music to timeline
            var musicClip = new VideoClip
            {
                Id = Timeline.Count + 1,
                Name = $"AI Music - {MusicStyle}",
                FilePath = "", // TODO: Actual file path
                Duration = TimeSpan.FromSeconds(VideoDuration),
                ClipType = ClipType.Audio,
                IsAIGenerated = true
            };
            Timeline.Add(musicClip);

            var isThai = _localizationService.IsThaiLanguage;
            MessageBox.Show(
                isThai ? "สร้างเพลง AI สำเร็จ!" : "AI Music generated successfully!",
                isThai ? "สำเร็จ" : "Success",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        finally
        {
            IsGenerating = false;
            GenerationProgress = 0;
            GenerationStatus = "";
        }
    }

    private void AddToTimeline(VideoClip? clip)
    {
        if (clip == null) return;
        if (!Timeline.Contains(clip))
        {
            Timeline.Add(clip);
        }
    }

    private void RemoveFromTimeline(VideoClip? clip)
    {
        if (clip == null) return;
        Timeline.Remove(clip);
    }

    private void MoveClipUp(VideoClip? clip)
    {
        if (clip == null) return;
        var index = Timeline.IndexOf(clip);
        if (index > 0)
        {
            Timeline.Move(index, index - 1);
        }
    }

    private void MoveClipDown(VideoClip? clip)
    {
        if (clip == null) return;
        var index = Timeline.IndexOf(clip);
        if (index < Timeline.Count - 1)
        {
            Timeline.Move(index, index + 1);
        }
    }

    private void TogglePlayPause()
    {
        IsPlaying = !IsPlaying;
    }

    private void StopPlayback()
    {
        IsPlaying = false;
        CurrentPosition = 0;
    }

    private async Task ExportVideoAsync()
    {
        if (Timeline.Count == 0)
        {
            var isThai = _localizationService.IsThaiLanguage;
            MessageBox.Show(
                isThai ? "ไม่มีคลิปใน Timeline" : "No clips in timeline",
                isThai ? "ข้อมูลไม่ครบ" : "Missing Data",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = _localizationService.IsThaiLanguage ? "บันทึกวิดีโอ" : "Save Video",
            Filter = "MP4 Video|*.mp4|AVI Video|*.avi|MOV Video|*.mov",
            DefaultExt = ".mp4"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                IsGenerating = true;
                GenerationStatus = _localizationService.IsThaiLanguage ? "กำลัง export วิดีโอ..." : "Exporting video...";

                // Simulate export progress
                for (int i = 0; i <= 100; i += 5)
                {
                    await Task.Delay(100);
                    GenerationProgress = i;
                }

                var isThai = _localizationService.IsThaiLanguage;
                MessageBox.Show(
                    isThai ? $"บันทึกวิดีโอสำเร็จ!\n{dialog.FileName}" : $"Video exported successfully!\n{dialog.FileName}",
                    isThai ? "สำเร็จ" : "Success",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            finally
            {
                IsGenerating = false;
                GenerationProgress = 0;
                GenerationStatus = "";
            }
        }
    }
}

// Supporting Models
public class VideoProject
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public AspectRatio AspectRatio { get; set; }
    public TimeSpan Duration { get; set; }
    public string ThumbnailPath { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class VideoClip
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string FilePath { get; set; } = "";
    public TimeSpan Duration { get; set; }
    public TimeSpan StartTime { get; set; }
    public ClipType ClipType { get; set; }
    public string Transition { get; set; } = "None";
    public string Effect { get; set; } = "None";
    public bool IsAIGenerated { get; set; }
}

public enum ClipType
{
    Video,
    Image,
    Audio,
    Text
}
