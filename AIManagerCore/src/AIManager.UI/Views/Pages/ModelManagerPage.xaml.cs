using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AIManager.Core.Services;

namespace AIManager.UI.Views.Pages;

public partial class ModelManagerPage : Page
{
    private readonly HuggingFaceModelManager _modelManager;
    private readonly SystemResourceDetector _resourceDetector;
    private CancellationTokenSource? _downloadCts;
    private ModelCategory _currentCategory = ModelCategory.LLM;

    public ObservableCollection<ModelViewModel> Models { get; } = new();

    public ModelManagerPage()
    {
        InitializeComponent();
        _modelManager = new HuggingFaceModelManager();
        _resourceDetector = new SystemResourceDetector();

        // Subscribe to download events
        _modelManager.OnProgress += OnDownloadProgress;
        _modelManager.OnComplete += OnDownloadComplete;
        _modelManager.DownloadProgress += OnLegacyDownloadProgress;

        ModelsList.ItemsSource = Models;
        DownloadBar.Visibility = Visibility.Collapsed;

        Loaded += async (s, e) =>
        {
            await RefreshResourcesAsync();
            await LoadModelsAsync();
        };
    }

    private async Task RefreshResourcesAsync()
    {
        try
        {
            var resources = await Task.Run(() => _resourceDetector.DetectResources());

            var totalRamGB = resources.TotalRamMB / 1024.0;
            var availableRamGB = resources.AvailableRamMB / 1024.0;
            var usedRamGB = totalRamGB - availableRamGB;
            var vramGB = resources.TotalVramMB / 1024.0;

            Dispatcher.Invoke(() =>
            {
                TxtRam.Text = $"{totalRamGB:F1} GB";
                TxtVram.Text = vramGB > 0 ? $"{vramGB:F1} GB" : "N/A";
                TxtGpu.Text = !string.IsNullOrEmpty(resources.GpuName) && resources.GpuName != "Unknown"
                    ? resources.GpuName : "No dedicated GPU";
                TxtCuda.Text = resources.HasNvidiaGpu ? "CUDA Available" : "";

                RamProgress.Value = totalRamGB > 0 ? (usedRamGB / totalRamGB) * 100 : 0;
                VramProgress.Value = vramGB > 0 ? 50 : 0; // Default to 50% as we don't have used VRAM info
            });
        }
        catch (Exception ex)
        {
            Dispatcher.Invoke(() =>
            {
                TxtRam.Text = "Error";
                TxtVram.Text = "Error";
                TxtGpu.Text = ex.Message;
            });
        }
    }

    private async Task LoadModelsAsync()
    {
        LoadingOverlay.Visibility = Visibility.Visible;
        TxtLoadingMessage.Text = "Loading models...";

        try
        {
            var compatibleModels = await _modelManager.GetCompatibleModelsAsync();

            Dispatcher.Invoke(() =>
            {
                Models.Clear();

                var filteredModels = _currentCategory switch
                {
                    ModelCategory.LLM => compatibleModels.Where(m => m.Model.Category == ModelCategory.LLM),
                    ModelCategory.ImageGeneration => compatibleModels.Where(m => m.Model.Category == ModelCategory.ImageGeneration),
                    ModelCategory.VideoGeneration => compatibleModels.Where(m => m.Model.Category == ModelCategory.VideoGeneration),
                    ModelCategory.VAE => compatibleModels.Where(m => m.Model.Category == ModelCategory.VAE),
                    _ => compatibleModels
                };

                foreach (var info in filteredModels.OrderBy(m => m.Model.Priority))
                {
                    var vm = new ModelViewModel(info, DownloadModel, DeleteModel);
                    Models.Add(vm);
                }

                LoadingOverlay.Visibility = Visibility.Collapsed;
            });
        }
        catch (Exception ex)
        {
            Dispatcher.Invoke(() =>
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
                MessageBox.Show($"Failed to load models: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            });
        }
    }

    private async void DownloadModel(ModelViewModel vm)
    {
        if (_downloadCts != null)
        {
            MessageBox.Show("A download is already in progress.", "Info",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _downloadCts = new CancellationTokenSource();
        vm.IsDownloading = true;
        vm.Progress = 0;
        vm.ProgressText = "Starting...";

        DownloadBar.Visibility = Visibility.Visible;
        TxtDownloadModel.Text = $"Downloading: {vm.DisplayName}";
        DownloadProgress.Value = 0;

        try
        {
            var progress = new Progress<DownloadProgressEventArgs>(p =>
            {
                Dispatcher.Invoke(() =>
                {
                    vm.Progress = p.ProgressPercent;
                    vm.ProgressText = $"{p.DownloadedMB:F1} / {p.TotalMB:F1} MB ({p.ProgressPercent:F1}%)";

                    DownloadProgress.Value = p.ProgressPercent;
                    TxtDownloadProgress.Text = $"{p.ProgressPercent:F1}%";

                    // Calculate speed
                    var speedMB = p.DownloadedBytes / (DateTime.UtcNow - DateTime.UtcNow.AddSeconds(-1)).TotalSeconds / 1_000_000;
                    TxtDownloadSpeed.Text = $"{speedMB:F1} MB/s";
                });
            });

            var result = await _modelManager.DownloadModelAsync(vm.Model, _downloadCts.Token, progress);

            if (result.Success)
            {
                vm.IsInstalled = true;
                vm.StatusText = "Installed";
                vm.ActionText = "Delete";
                MessageBox.Show($"{vm.DisplayName} downloaded successfully!", "Success",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show($"Download failed: {result.Error}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (OperationCanceledException)
        {
            vm.ProgressText = "Cancelled";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Download failed: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            vm.IsDownloading = false;
            _downloadCts?.Dispose();
            _downloadCts = null;
            DownloadBar.Visibility = Visibility.Collapsed;

            await LoadModelsAsync();
        }
    }

    private async void DeleteModel(ModelViewModel vm)
    {
        var result = MessageBox.Show(
            $"Are you sure you want to delete {vm.DisplayName}?",
            "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            var success = _modelManager.DeleteModel(vm.Model);
            if (success)
            {
                await LoadModelsAsync();
            }
            else
            {
                MessageBox.Show("Failed to delete model.", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void OnDownloadProgress(object? sender, ModelDownloadProgress e)
    {
        Dispatcher.Invoke(() =>
        {
            DownloadProgress.Value = e.Progress;
            TxtDownloadProgress.Text = $"{e.Progress:F1}%";
            TxtDownloadSpeed.Text = e.SpeedDisplay;
        });
    }

    private void OnDownloadComplete(object? sender, ModelDownloadComplete e)
    {
        Dispatcher.Invoke(async () =>
        {
            DownloadBar.Visibility = Visibility.Collapsed;
            await LoadModelsAsync();
        });
    }

    private void OnLegacyDownloadProgress(object? sender, DownloadProgressEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            DownloadProgress.Value = e.ProgressPercent;
            TxtDownloadProgress.Text = $"{e.ProgressPercent:F1}%";
        });
    }

    private async void RefreshResources_Click(object sender, RoutedEventArgs e)
    {
        await RefreshResourcesAsync();
        await LoadModelsAsync();
    }

    private async void CategoryTab_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton rb && rb.Tag is string tag)
        {
            _currentCategory = tag switch
            {
                "LLM" => ModelCategory.LLM,
                "Image" => ModelCategory.ImageGeneration,
                "Video" => ModelCategory.VideoGeneration,
                "VAE" => ModelCategory.VAE,
                _ => ModelCategory.LLM
            };

            await LoadModelsAsync();
        }
    }

    private void CancelDownload_Click(object sender, RoutedEventArgs e)
    {
        _downloadCts?.Cancel();
    }
}

/// <summary>
/// ViewModel for Model display in list
/// </summary>
public class ModelViewModel : INotifyPropertyChanged
{
    private bool _isDownloading;
    private double _progress;
    private string _progressText = "";
    private bool _isInstalled;
    private string _statusText = "";
    private string _actionText = "Install";

    public HuggingFaceModel Model { get; }
    public ModelCompatibilityInfo Info { get; }

    private readonly Action<ModelViewModel> _downloadAction;
    private readonly Action<ModelViewModel> _deleteAction;

    public ModelViewModel(ModelCompatibilityInfo info, Action<ModelViewModel> downloadAction, Action<ModelViewModel> deleteAction)
    {
        Info = info;
        Model = info.Model;
        _downloadAction = downloadAction;
        _deleteAction = deleteAction;

        _isInstalled = info.IsInstalled;
        UpdateStatus();
    }

    public string DisplayName => Model.DisplayName;
    public string Description => Model.Description;
    public string[] Tags => Model.Tags;
    public string SizeDisplay => $"{Model.SizeGB:F1} GB";
    public string RequirementsDisplay => $"RAM: {Model.RequiredRamGB}GB, VRAM: {Model.RequiredVramGB}GB";

    public bool IsDownloading
    {
        get => _isDownloading;
        set { _isDownloading = value; OnPropertyChanged(); OnPropertyChanged(nameof(ProgressVisibility)); }
    }

    public double Progress
    {
        get => _progress;
        set { _progress = value; OnPropertyChanged(); }
    }

    public string ProgressText
    {
        get => _progressText;
        set { _progressText = value; OnPropertyChanged(); }
    }

    public bool IsInstalled
    {
        get => _isInstalled;
        set { _isInstalled = value; OnPropertyChanged(); UpdateStatus(); }
    }

    public string StatusText
    {
        get => _statusText;
        set { _statusText = value; OnPropertyChanged(); }
    }

    public string ActionText
    {
        get => _actionText;
        set { _actionText = value; OnPropertyChanged(); }
    }

    public bool ActionEnabled => Info.IsCompatible || IsInstalled;

    public Visibility ProgressVisibility => IsDownloading ? Visibility.Visible : Visibility.Collapsed;
    public Visibility DescriptionVisibility => !string.IsNullOrEmpty(Description) ? Visibility.Visible : Visibility.Collapsed;
    public Visibility IsRecommendedVisibility => Model.Priority == 1 && Info.IsCompatible ? Visibility.Visible : Visibility.Collapsed;
    public Visibility IncompatibleVisibility => !Info.IsCompatible ? Visibility.Visible : Visibility.Collapsed;

    public string StatusIcon => IsInstalled ? "Check" : (Info.IsCompatible ? "Download" : "AlertCircle");
    public Brush StatusBackground => IsInstalled
        ? new SolidColorBrush(Color.FromRgb(232, 245, 233))
        : (Info.IsCompatible
            ? new SolidColorBrush(Color.FromRgb(227, 242, 253))
            : new SolidColorBrush(Color.FromRgb(255, 235, 238)));
    public Brush StatusIconColor => IsInstalled
        ? new SolidColorBrush(Color.FromRgb(76, 175, 80))
        : (Info.IsCompatible
            ? new SolidColorBrush(Color.FromRgb(33, 150, 243))
            : new SolidColorBrush(Color.FromRgb(244, 67, 54)));
    public Brush StatusColor => IsInstalled
        ? new SolidColorBrush(Color.FromRgb(76, 175, 80))
        : new SolidColorBrush(Color.FromRgb(158, 158, 158));

    public ICommand ActionCommand => new RelayCommand(() =>
    {
        if (IsInstalled)
            _deleteAction(this);
        else
            _downloadAction(this);
    });

    private void UpdateStatus()
    {
        if (_isInstalled)
        {
            StatusText = "Installed";
            ActionText = "Delete";
        }
        else if (!Info.IsCompatible)
        {
            StatusText = Info.CompatibilityReason ?? "Not compatible";
            ActionText = "N/A";
        }
        else
        {
            StatusText = "Available";
            ActionText = "Install";
        }

        OnPropertyChanged(nameof(StatusIcon));
        OnPropertyChanged(nameof(StatusBackground));
        OnPropertyChanged(nameof(StatusIconColor));
        OnPropertyChanged(nameof(StatusColor));
        OnPropertyChanged(nameof(ActionEnabled));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>
/// Simple relay command implementation
/// </summary>
public class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;
    public void Execute(object? parameter) => _execute();
    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }
}
