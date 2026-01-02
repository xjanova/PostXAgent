using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AIManager.Core.Models;
using AIManager.Core.Services;
using AIManager.Core.WebAutomation;
using Microsoft.Win32;

namespace AIManager.UI.Views.Pages;

public partial class WorkflowManagerPage : Page
{
    private readonly WorkflowManagerService _workflowService;
    private readonly WorkflowStorage _workflowStorage;
    private List<PlatformWorkflowInfo> _platforms = new();
    private PlatformWorkflowInfo? _selectedPlatform;
    private string _selectedWorkflowType = "";

    public WorkflowManagerPage()
    {
        InitializeComponent();

        // Initialize services
        _workflowStorage = new WorkflowStorage(null);
        _workflowService = new WorkflowManagerService(_workflowStorage);

        // Set default download folder
        var defaultDownloadPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "PostXAgent", "Downloads");
        TxtDownloadFolder.Text = _workflowService.GetDownloadFolder() ?? defaultDownloadPath;

        // Setup Ollama mode toggle
        RbKeywordOllama.Checked += (s, e) => OllamaSettingsPanel.Visibility = Visibility.Visible;
        RbKeywordOllama.Unchecked += (s, e) => OllamaSettingsPanel.Visibility = Visibility.Collapsed;
        RbKeywordPreset.Checked += (s, e) => OllamaSettingsPanel.Visibility = Visibility.Collapsed;
        RbKeywordAsk.Checked += (s, e) => OllamaSettingsPanel.Visibility = Visibility.Collapsed;
    }

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadPlatformsAsync();
        await UpdateStatsAsync();
    }

    private async Task LoadPlatformsAsync()
    {
        _platforms = await _workflowService.GetAllPlatformsWithWorkflowsAsync();
        PlatformGridView.ItemsSource = _platforms;
        PlatformListView.ItemsSource = _platforms;
    }

    private async Task UpdateStatsAsync()
    {
        var totalWorkflows = _platforms.Sum(p => p.WorkflowCount);
        TxtTotalWorkflows.Text = $"{totalWorkflows} Total Workflows";

        // Check Ollama status
        var ollamaStatus = await _workflowService.CheckOllamaStatusAsync();
        TxtOllamaStatus.Text = ollamaStatus ? "Ollama: Ready" : "Ollama: Offline";

        // Count pending files
        var downloadFolder = TxtDownloadFolder.Text;
        if (Directory.Exists(downloadFolder))
        {
            var fileCount = Directory.GetFiles(downloadFolder, "*.*", SearchOption.AllDirectories).Length;
            TxtDownloadCount.Text = $"{fileCount} Files Pending";
        }
    }

    #region View Toggle

    private void ViewMode_Changed(object sender, RoutedEventArgs e)
    {
        // Guard against null during XAML initialization
        if (PlatformGridView == null || PlatformListView == null)
            return;

        if (RbGridView.IsChecked == true)
        {
            PlatformGridView.Visibility = Visibility.Visible;
            PlatformListView.Visibility = Visibility.Collapsed;
        }
        else
        {
            PlatformGridView.Visibility = Visibility.Collapsed;
            PlatformListView.Visibility = Visibility.Visible;
        }
    }

    #endregion

    #region Download Folder

    private void BtnBrowseFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select Download Folder"
        };

        if (dialog.ShowDialog() == true)
        {
            TxtDownloadFolder.Text = dialog.FolderName;
            _workflowService.SetDownloadFolder(dialog.FolderName);
        }
    }

    private void BtnOpenDownloadFolder_Click(object sender, RoutedEventArgs e)
    {
        var folder = TxtDownloadFolder.Text;
        if (!Directory.Exists(folder))
        {
            Directory.CreateDirectory(folder);
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = folder,
            UseShellExecute = true
        });
    }

    #endregion

    #region Platform Actions

    private void PlatformCard_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border border && border.Tag is PlatformWorkflowInfo platform)
        {
            _selectedPlatform = platform;
            ShowWorkflowOptionsDialog("Play Workflow", platform);
        }
    }

    private void PlatformList_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (PlatformListView.SelectedItem is PlatformWorkflowInfo platform)
        {
            _selectedPlatform = platform;
            ShowWorkflowOptionsDialog("Play Workflow", platform);
        }
    }

    private void WorkflowType_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string workflowType)
        {
            _selectedWorkflowType = workflowType;
            // Find parent platform
            var parent = FindVisualParent<Border>(button);
            if (parent?.Tag is PlatformWorkflowInfo platform)
            {
                _selectedPlatform = platform;
                ShowWorkflowOptionsDialog($"{workflowType} - {platform.Name}", platform, workflowType);
            }
        }
    }

    private void BtnRecord_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is PlatformWorkflowInfo platform)
        {
            _selectedPlatform = platform;
            StartRecording(platform);
        }
    }

    private void BtnPlay_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is PlatformWorkflowInfo platform)
        {
            _selectedPlatform = platform;
            ShowWorkflowOptionsDialog($"Play Workflow - {platform.Name}", platform);
        }
    }

    private async void BtnExport_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is PlatformWorkflowInfo platform)
        {
            await ExportWorkflowAsync(platform);
        }
    }

    #endregion

    #region Import/Export

    private async void BtnImport_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Import Workflow Template",
            Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
            Multiselect = true
        };

        if (dialog.ShowDialog() == true)
        {
            foreach (var file in dialog.FileNames)
            {
                try
                {
                    var json = await File.ReadAllTextAsync(file);
                    await _workflowStorage.ImportWorkflowAsync(json);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to import {Path.GetFileName(file)}: {ex.Message}",
                        "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }

            await LoadPlatformsAsync();
            await UpdateStatsAsync();
            MessageBox.Show("Workflows imported successfully!", "Import Complete",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private async void BtnExportAll_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select Export Folder"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                await _workflowService.ExportAllWorkflowsAsync(dialog.FolderName);
                MessageBox.Show($"All workflows exported to:\n{dialog.FolderName}",
                    "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);

                Process.Start(new ProcessStartInfo
                {
                    FileName = dialog.FolderName,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed: {ex.Message}",
                    "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private async Task ExportWorkflowAsync(PlatformWorkflowInfo platform)
    {
        var dialog = new SaveFileDialog
        {
            Title = $"Export {platform.Name} Workflows",
            Filter = "JSON Files (*.json)|*.json",
            FileName = $"{platform.Name.ToLowerInvariant()}_workflows.json"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                await _workflowService.ExportPlatformWorkflowsAsync(platform.PlatformType, dialog.FileName);
                MessageBox.Show($"Workflows exported to:\n{dialog.FileName}",
                    "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed: {ex.Message}",
                    "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    #endregion

    #region Workflow Dialog

    private void ShowWorkflowOptionsDialog(string title, PlatformWorkflowInfo platform, string? workflowType = null)
    {
        DialogTitle.Text = title;
        DialogSubtitle.Text = $"Platform: {platform.Name}";

        if (!string.IsNullOrEmpty(workflowType))
        {
            // Pre-select workflow type
            for (int i = 0; i < CboWorkflowType.Items.Count; i++)
            {
                if (CboWorkflowType.Items[i] is ComboBoxItem item &&
                    item.Content.ToString()?.Contains(workflowType, StringComparison.OrdinalIgnoreCase) == true)
                {
                    CboWorkflowType.SelectedIndex = i;
                    break;
                }
            }
        }

        WorkflowOptionsDialog.IsOpen = true;
    }

    private void DialogCancel_Click(object sender, RoutedEventArgs e)
    {
        WorkflowOptionsDialog.IsOpen = false;
    }

    private async void DialogStart_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedPlatform == null)
        {
            MessageBox.Show("Please select a platform first.", "Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        WorkflowOptionsDialog.IsOpen = false;

        var options = new WorkflowRunOptions
        {
            Platform = _selectedPlatform.PlatformType,
            WorkflowType = GetSelectedWorkflowType(),
            KeywordMode = GetKeywordMode(),
            Prompt = TxtPrompt.Text,
            ContentType = (CboContentType.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "",
            OllamaModel = (CboOllamaModel.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "llama3.2:latest",
            OllamaContext = TxtOllamaContext.Text,
            AutoDownload = ChkAutoDownload.IsChecked == true,
            SaveToPool = ChkSaveToPool.IsChecked == true,
            NotifyOnComplete = ChkNotifyComplete.IsChecked == true,
            DownloadFolder = TxtDownloadFolder.Text
        };

        await RunWorkflowAsync(options);
    }

    private string GetSelectedWorkflowType()
    {
        if (CboWorkflowType.SelectedItem is ComboBoxItem item)
        {
            var text = item.Content?.ToString() ?? "";
            if (text.Contains("Signup")) return "Signup";
            if (text.Contains("Login")) return "Login";
            if (text.Contains("Video")) return "CreateVideo";
            if (text.Contains("Music")) return "CreateMusic";
            if (text.Contains("Image")) return "CreateImage";
            if (text.Contains("Post")) return "Post";
        }
        return "Signup";
    }

    private KeywordMode GetKeywordMode()
    {
        if (RbKeywordOllama.IsChecked == true) return KeywordMode.OllamaGenerate;
        if (RbKeywordAsk.IsChecked == true) return KeywordMode.AskDuringRun;
        return KeywordMode.Preset;
    }

    #endregion

    #region Workflow Execution

    private void StartRecording(PlatformWorkflowInfo platform)
    {
        // Navigate to WebLearningPage with platform URL for recording
        var mainWindow = Window.GetWindow(this) as MainWindow;
        if (mainWindow != null)
        {
            // Get platform start URL or use default
            var startUrl = !string.IsNullOrEmpty(platform.StartUrl)
                ? platform.StartUrl
                : GetDefaultPlatformUrl(platform.PlatformType);

            // Navigate to WebLearning with platform URL and name
            mainWindow.NavigateToPage("WebLearning", startUrl, platform.Name);
        }
    }

    private static string GetDefaultPlatformUrl(WorkflowPlatformType platformType)
    {
        return platformType switch
        {
            // Social Media
            WorkflowPlatformType.Facebook => "https://www.facebook.com",
            WorkflowPlatformType.Google => "https://accounts.google.com",
            WorkflowPlatformType.TikTok => "https://www.tiktok.com",
            WorkflowPlatformType.Instagram => "https://www.instagram.com",
            WorkflowPlatformType.Twitter => "https://twitter.com",
            WorkflowPlatformType.YouTube => "https://www.youtube.com",
            WorkflowPlatformType.LinkedIn => "https://www.linkedin.com",
            WorkflowPlatformType.Pinterest => "https://www.pinterest.com",
            WorkflowPlatformType.Threads => "https://www.threads.net",
            WorkflowPlatformType.Line => "https://line.me",

            // AI Services - Video
            WorkflowPlatformType.Freepik => "https://www.freepik.com/pikaso",
            WorkflowPlatformType.Runway => "https://runwayml.com",
            WorkflowPlatformType.PikaLabs => "https://pika.art",
            WorkflowPlatformType.LumaAI => "https://lumalabs.ai",

            // AI Services - Music
            WorkflowPlatformType.SunoAI => "https://suno.ai",
            WorkflowPlatformType.StableAudio => "https://stableaudio.com",
            WorkflowPlatformType.AudioCraft => "https://audiocraft.metademolab.com",

            // AI Services - Image
            WorkflowPlatformType.StableDiffusion => "https://stablediffusionweb.com",
            WorkflowPlatformType.Leonardo => "https://leonardo.ai",
            WorkflowPlatformType.MidJourney => "https://midjourney.com",
            WorkflowPlatformType.DallE => "https://labs.openai.com",

            // GPU Providers
            WorkflowPlatformType.GoogleColab => "https://colab.research.google.com",
            WorkflowPlatformType.Kaggle => "https://www.kaggle.com",
            WorkflowPlatformType.PaperSpace => "https://www.paperspace.com",
            WorkflowPlatformType.LightningAI => "https://lightning.ai",
            WorkflowPlatformType.HuggingFace => "https://huggingface.co",
            WorkflowPlatformType.SaturnCloud => "https://saturncloud.io",

            // Default
            _ => "https://www.google.com"
        };
    }

    private async Task RunWorkflowAsync(WorkflowRunOptions options)
    {
        try
        {
            // Generate prompt with Ollama if needed
            if (options.KeywordMode == KeywordMode.OllamaGenerate)
            {
                var generatedPrompt = await _workflowService.GeneratePromptWithOllamaAsync(
                    options.OllamaModel,
                    options.OllamaContext,
                    options.WorkflowType);

                if (!string.IsNullOrEmpty(generatedPrompt))
                {
                    options.Prompt = generatedPrompt;
                }
            }

            // Execute workflow
            var result = await _workflowService.ExecuteWorkflowAsync(options);

            if (result.Success)
            {
                if (options.NotifyOnComplete)
                {
                    MessageBox.Show($"Workflow completed successfully!\n\n{result.Message}",
                        "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }

                await UpdateStatsAsync();
            }
            else
            {
                MessageBox.Show($"Workflow failed:\n{result.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error running workflow: {ex.Message}",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    #endregion

    #region Helpers

    private static T? FindVisualParent<T>(DependencyObject child) where T : DependencyObject
    {
        var parent = System.Windows.Media.VisualTreeHelper.GetParent(child);
        while (parent != null)
        {
            if (parent is T typedParent)
                return typedParent;
            parent = System.Windows.Media.VisualTreeHelper.GetParent(parent);
        }
        return null;
    }

    #endregion
}
