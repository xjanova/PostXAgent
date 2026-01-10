using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AIManager.Core.Models;
using AIManager.Core.Services;
using Microsoft.Win32;

namespace AIManager.UI.Views.Dialogs;

/// <summary>
/// Advanced Model Settings Dialog - ComfyUI-style configuration
/// ตั้งค่า Model, LoRA, VAE, Sampler ได้ครบถ้วน
/// </summary>
public partial class AdvancedModelSettingsDialog : Window
{
    private readonly HuggingFaceModelService _modelService;
    private readonly ObservableCollection<LoraConfigViewModel> _loras = new();
    private PipelineConfiguration _config;
    private readonly List<ModelInfo> _availableModels = new();

    public PipelineConfiguration Configuration => _config;
    public bool Applied { get; private set; }

    public AdvancedModelSettingsDialog(PipelineConfiguration? existingConfig = null)
    {
        InitializeComponent();

        _modelService = new HuggingFaceModelService();
        _config = existingConfig ?? new PipelineConfiguration();

        LoraList.ItemsSource = _loras;

        Loaded += async (s, e) =>
        {
            await LoadAvailableModelsAsync();
            LoadCurrentSettings();
            ValidateAll();
        };
    }

    private async Task LoadAvailableModelsAsync()
    {
        try
        {
            var models = await _modelService.GetDownloadedModelsAsync();
            _availableModels.Clear();
            _availableModels.AddRange(models);

            CmbCheckpoint.ItemsSource = models.Select(m => new
            {
                Id = m.Id,
                DisplayName = $"{m.Name} ({m.Type})"
            }).ToList();

            // Also load VAE models
            var vaeModels = models.Where(m => m.Type == ModelType.VAE).ToList();
            CmbVae.ItemsSource = vaeModels.Select(m => new { Id = m.Id, DisplayName = m.Name }).ToList();
            CmbVae.DisplayMemberPath = "DisplayName";
            CmbVae.SelectedValuePath = "Id";

            // Load refiner models (SDXL Refiner)
            var refinerModels = models.Where(m => m.Name.Contains("refiner", StringComparison.OrdinalIgnoreCase)).ToList();
            CmbRefinerModel.ItemsSource = refinerModels.Select(m => new { Id = m.Id, DisplayName = m.Name }).ToList();
            CmbRefinerModel.DisplayMemberPath = "DisplayName";
            CmbRefinerModel.SelectedValuePath = "Id";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to load models: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void LoadCurrentSettings()
    {
        // Model Settings
        if (!string.IsNullOrEmpty(_config.Model.CheckpointId))
        {
            CmbCheckpoint.SelectedValue = _config.Model.CheckpointId;
        }

        CmbPrecision.SelectedIndex = _config.Model.Precision switch
        {
            ModelPrecision.FP16 => 0,
            ModelPrecision.FP32 => 1,
            ModelPrecision.BF16 => 2,
            ModelPrecision.INT8 => 3,
            _ => 0
        };

        TxtClipSkip.Text = _config.Input.ClipSkipLayers.ToString();

        // VAE
        ChkCustomVae.IsChecked = _config.Model.UseCustomVae;
        if (_config.Model.UseCustomVae)
        {
            CmbVae.SelectedValue = _config.Model.VaeId;
        }
        ChkTiledVae.IsChecked = _config.Model.TiledVae;
        TxtVaeTileSize.Text = _config.Model.VaeTileSize.ToString();

        // LoRAs
        _loras.Clear();
        foreach (var lora in _config.Model.LoRAs)
        {
            _loras.Add(new LoraConfigViewModel
            {
                Name = lora.Name,
                Path = lora.Path ?? "",
                Weight = lora.Weight,
                ClipWeight = lora.ClipWeight,
                Enabled = lora.Enabled
            });
        }
        UpdateLoraVisibility();

        // Sampler
        SetSamplerSelection(_config.Sampler.Sampler);
        SetSchedulerSelection(_config.Sampler.Scheduler);
        TxtSteps.Text = _config.Sampler.Steps.ToString();
        TxtCfg.Text = _config.Sampler.CfgScale.ToString("F1");
        TxtSeed.Text = _config.Sampler.Seed.ToString();
        TxtDenoise.Text = _config.Sampler.Denoise.ToString("F1");

        // Image Size
        TxtWidth.Text = _config.Sampler.Width.ToString();
        TxtHeight.Text = _config.Sampler.Height.ToString();

        // Hi-Res Fix
        ChkHiResFix.IsChecked = _config.Sampler.HiResFix;
        TxtHiResScale.Text = _config.Sampler.HiResScale.ToString("F1");
        TxtHiResSteps.Text = _config.Sampler.HiResSteps.ToString();
        TxtHiResDenoise.Text = _config.Sampler.HiResDenoise.ToString("F1");

        // Refiner
        ChkRefiner.IsChecked = _config.Model.UseRefiner;
        if (!string.IsNullOrEmpty(_config.Model.RefinerModelId))
        {
            CmbRefinerModel.SelectedValue = _config.Model.RefinerModelId;
        }
        TxtRefinerSwitch.Text = ((int)(_config.Model.RefinerSwitchAt * 100)).ToString();
    }

    private void SetSamplerSelection(SamplerType sampler)
    {
        var index = sampler switch
        {
            SamplerType.DPMPlusPlus2MKarras => 0,
            SamplerType.DPMPlusPlusSDEKarras => 1,
            SamplerType.EulerA => 2,
            SamplerType.Euler => 3,
            SamplerType.Heun => 4,
            SamplerType.LMS => 5,
            SamplerType.DDIM => 6,
            SamplerType.UniPC => 7,
            SamplerType.LCM => 8,
            _ => 0
        };
        CmbSampler.SelectedIndex = index;
    }

    private void SetSchedulerSelection(SchedulerType scheduler)
    {
        var index = scheduler switch
        {
            SchedulerType.Normal => 0,
            SchedulerType.Karras => 1,
            SchedulerType.Exponential => 2,
            SchedulerType.SGMUniform => 3,
            SchedulerType.Simple => 4,
            SchedulerType.DDIM_Uniform => 5,
            _ => 0
        };
        CmbScheduler.SelectedIndex = index;
    }

    private SamplerType GetSelectedSampler()
    {
        return CmbSampler.SelectedIndex switch
        {
            0 => SamplerType.DPMPlusPlus2MKarras,
            1 => SamplerType.DPMPlusPlusSDEKarras,
            2 => SamplerType.EulerA,
            3 => SamplerType.Euler,
            4 => SamplerType.Heun,
            5 => SamplerType.LMS,
            6 => SamplerType.DDIM,
            7 => SamplerType.UniPC,
            8 => SamplerType.LCM,
            _ => SamplerType.DPMPlusPlus2MKarras
        };
    }

    private SchedulerType GetSelectedScheduler()
    {
        return CmbScheduler.SelectedIndex switch
        {
            0 => SchedulerType.Normal,
            1 => SchedulerType.Karras,
            2 => SchedulerType.Exponential,
            3 => SchedulerType.SGMUniform,
            4 => SchedulerType.Simple,
            5 => SchedulerType.DDIM_Uniform,
            _ => SchedulerType.Normal
        };
    }

    private void ValidateAll()
    {
        var result = BuildConfiguration().Validate();

        if (result.IsValid)
        {
            ValidationIcon.Kind = MaterialDesignThemes.Wpf.PackIconKind.CheckCircle;
            ValidationIcon.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129));
            ValidationText.Text = "Ready";
            ValidationText.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129));
            TxtValidationMessage.Text = result.Warnings.Count > 0
                ? $"{result.Warnings.Count} warning(s)"
                : "";
            BtnApply.IsEnabled = true;
            ModelStatusDot.Fill = new SolidColorBrush(Color.FromRgb(16, 185, 129));
        }
        else
        {
            ValidationIcon.Kind = MaterialDesignThemes.Wpf.PackIconKind.AlertCircle;
            ValidationIcon.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68));
            ValidationText.Text = "Invalid";
            ValidationText.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68));
            TxtValidationMessage.Text = result.Errors.FirstOrDefault() ?? "Configuration is invalid";
            BtnApply.IsEnabled = false;
            ModelStatusDot.Fill = new SolidColorBrush(Color.FromRgb(239, 68, 68));
        }
    }

    private PipelineConfiguration BuildConfiguration()
    {
        var config = new PipelineConfiguration();

        // Model
        config.Model.CheckpointId = CmbCheckpoint.SelectedValue as string;
        config.Model.Precision = CmbPrecision.SelectedIndex switch
        {
            0 => ModelPrecision.FP16,
            1 => ModelPrecision.FP32,
            2 => ModelPrecision.BF16,
            3 => ModelPrecision.INT8,
            _ => ModelPrecision.FP16
        };

        // VAE
        config.Model.UseCustomVae = ChkCustomVae.IsChecked == true;
        if (config.Model.UseCustomVae)
        {
            config.Model.VaeId = CmbVae.SelectedValue as string;
        }
        config.Model.TiledVae = ChkTiledVae.IsChecked == true;
        if (int.TryParse(TxtVaeTileSize.Text, out var tileSize))
        {
            config.Model.VaeTileSize = tileSize;
        }

        // LoRAs
        config.Model.LoRAs.Clear();
        foreach (var lora in _loras)
        {
            config.Model.LoRAs.Add(new LoraConfig
            {
                Name = lora.Name,
                Path = lora.Path,
                Weight = lora.Weight,
                ClipWeight = lora.ClipWeight,
                Enabled = lora.Enabled
            });
        }

        // Input
        if (int.TryParse(TxtClipSkip.Text, out var clipSkip))
        {
            config.Input.ClipSkipLayers = clipSkip;
            config.Input.UseClipSkip = clipSkip > 1;
        }

        // Sampler
        config.Sampler.Sampler = GetSelectedSampler();
        config.Sampler.Scheduler = GetSelectedScheduler();

        if (int.TryParse(TxtSteps.Text, out var steps))
            config.Sampler.Steps = steps;
        if (double.TryParse(TxtCfg.Text, out var cfg))
            config.Sampler.CfgScale = cfg;
        if (long.TryParse(TxtSeed.Text, out var seed))
            config.Sampler.Seed = seed;
        if (double.TryParse(TxtDenoise.Text, out var denoise))
            config.Sampler.Denoise = denoise;

        // Size
        if (int.TryParse(TxtWidth.Text, out var width))
            config.Sampler.Width = width;
        if (int.TryParse(TxtHeight.Text, out var height))
            config.Sampler.Height = height;

        // Hi-Res
        config.Sampler.HiResFix = ChkHiResFix.IsChecked == true;
        if (double.TryParse(TxtHiResScale.Text, out var hiResScale))
            config.Sampler.HiResScale = hiResScale;
        if (int.TryParse(TxtHiResSteps.Text, out var hiResSteps))
            config.Sampler.HiResSteps = hiResSteps;
        if (double.TryParse(TxtHiResDenoise.Text, out var hiResDenoise))
            config.Sampler.HiResDenoise = hiResDenoise;

        // Refiner
        config.Model.UseRefiner = ChkRefiner.IsChecked == true;
        config.Model.RefinerModelId = CmbRefinerModel.SelectedValue as string;
        if (int.TryParse(TxtRefinerSwitch.Text, out var switchAt))
            config.Model.RefinerSwitchAt = switchAt / 100.0;

        return config;
    }

    #region Event Handlers

    private void Checkpoint_Changed(object sender, SelectionChangedEventArgs e)
    {
        ValidateAll();
    }

    private void CustomVae_Changed(object sender, RoutedEventArgs e)
    {
        VaeSettings.Visibility = ChkCustomVae.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        ValidateAll();
    }

    private void HiResFix_Changed(object sender, RoutedEventArgs e)
    {
        HiResSettings.Visibility = ChkHiResFix.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Refiner_Changed(object sender, RoutedEventArgs e)
    {
        RefinerSettings.Visibility = ChkRefiner.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
    }

    private void BrowseCheckpoint_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select Checkpoint Model",
            Filter = "SafeTensors|*.safetensors|PyTorch|*.pt;*.pth;*.bin|All Files|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            _config.Model.CheckpointPath = dialog.FileName;
            _config.Model.CheckpointId = Path.GetFileNameWithoutExtension(dialog.FileName);

            // Add to combo
            var item = new { Id = _config.Model.CheckpointId, DisplayName = Path.GetFileName(dialog.FileName) };
            var items = CmbCheckpoint.ItemsSource as IEnumerable<object>;
            CmbCheckpoint.ItemsSource = items != null ? items.Append(item).ToList() : new List<object> { item };
            CmbCheckpoint.SelectedValue = _config.Model.CheckpointId;
        }
    }

    private void BrowseVae_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select VAE Model",
            Filter = "SafeTensors|*.safetensors|PyTorch|*.pt;*.pth|All Files|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            _config.Model.VaePath = dialog.FileName;
            _config.Model.VaeId = Path.GetFileNameWithoutExtension(dialog.FileName);
            ValidateAll();
        }
    }

    private void AddLora_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select LoRA",
            Filter = "SafeTensors|*.safetensors|PyTorch|*.pt;*.pth|All Files|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            _loras.Add(new LoraConfigViewModel
            {
                Name = Path.GetFileNameWithoutExtension(dialog.FileName),
                Path = dialog.FileName,
                Weight = 1.0,
                ClipWeight = 1.0,
                Enabled = true
            });
            UpdateLoraVisibility();
            ValidateAll();
        }
    }

    private void RemoveLora_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is LoraConfigViewModel lora)
        {
            _loras.Remove(lora);
            UpdateLoraVisibility();
            ValidateAll();
        }
    }

    private void UpdateLoraVisibility()
    {
        TxtNoLoras.Visibility = _loras.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SetSize_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string sizeStr)
        {
            var parts = sizeStr.Split(',');
            if (parts.Length == 2)
            {
                TxtWidth.Text = parts[0];
                TxtHeight.Text = parts[1];
                ValidateAll();
            }
        }
    }

    private void ResetDefaults_Click(object sender, RoutedEventArgs e)
    {
        _config = new PipelineConfiguration();
        LoadCurrentSettings();
        ValidateAll();
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        _config = BuildConfiguration();
        var result = _config.Validate();

        if (!result.IsValid)
        {
            var message = "Cannot apply settings:\n\n" + string.Join("\n", result.Errors);
            MessageBox.Show(message, "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (result.Warnings.Count > 0)
        {
            var warningMessage = "The following warnings were found:\n\n" +
                                 string.Join("\n", result.Warnings) +
                                 "\n\nContinue anyway?";

            if (MessageBox.Show(warningMessage, "Warnings", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                return;
            }
        }

        Applied = true;
        DialogResult = true;
        Close();
    }

    #endregion
}

/// <summary>
/// ViewModel for LoRA configuration display
/// </summary>
public class LoraConfigViewModel : System.ComponentModel.INotifyPropertyChanged
{
    private string _name = "";
    private string _path = "";
    private double _weight = 1.0;
    private double _clipWeight = 1.0;
    private bool _enabled = true;

    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(); }
    }

    public string Path
    {
        get => _path;
        set { _path = value; OnPropertyChanged(); }
    }

    public double Weight
    {
        get => _weight;
        set { _weight = value; OnPropertyChanged(); }
    }

    public double ClipWeight
    {
        get => _clipWeight;
        set { _clipWeight = value; OnPropertyChanged(); }
    }

    public bool Enabled
    {
        get => _enabled;
        set { _enabled = value; OnPropertyChanged(); }
    }

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
    }
}
