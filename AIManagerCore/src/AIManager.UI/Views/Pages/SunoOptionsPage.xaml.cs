using System.Windows;
using System.Windows.Controls;
using AIManager.UI.ViewModels;
using Microsoft.Win32;

namespace AIManager.UI.Views.Pages;

/// <summary>
/// Interaction logic for SunoOptionsPage.xaml
/// </summary>
public partial class SunoOptionsPage : Page
{
    private readonly SunoOptionsViewModel _viewModel;

    public SunoOptionsPage()
    {
        InitializeComponent();
        _viewModel = new SunoOptionsViewModel();
        DataContext = _viewModel;
    }

    private void Tab_Changed(object sender, RoutedEventArgs e)
    {
        // Guard against null during initialization
        if (CreatePanel == null || DownloadPanel == null || WorkflowPanel == null || AccountPanel == null)
            return;

        if (sender is RadioButton rb)
        {
            // Hide all panels
            CreatePanel.Visibility = Visibility.Collapsed;
            DownloadPanel.Visibility = Visibility.Collapsed;
            WorkflowPanel.Visibility = Visibility.Collapsed;
            AccountPanel.Visibility = Visibility.Collapsed;

            // Show selected panel
            if (rb == TabCreate)
                CreatePanel.Visibility = Visibility.Visible;
            else if (rb == TabDownload)
                DownloadPanel.Visibility = Visibility.Visible;
            else if (rb == TabWorkflow)
                WorkflowPanel.Visibility = Visibility.Visible;
            else if (rb == TabAccount)
                AccountPanel.Visibility = Visibility.Visible;
        }
    }

    private void PresetButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is StylePreset preset)
        {
            _viewModel.ApplyPresetCommand.Execute(preset);
        }
    }

    private void BrowseFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select Download Folder",
            InitialDirectory = _viewModel.DownloadFolder
        };

        if (dialog.ShowDialog() == true)
        {
            _viewModel.DownloadFolder = dialog.FolderName;
        }
    }

    private async void TestConnection_Click(object sender, RoutedEventArgs e)
    {
        // Store password to ViewModel if needed
        _viewModel.SunoPassword = TxtPassword.Password;
        await _viewModel.TestConnectionCommand.ExecuteAsync(null);
    }

    private async void RunWorkflow_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.RunWorkflowCommand.ExecuteAsync(null);
    }

    private void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.SaveSettingsCommand.Execute(null);
    }

    private void ResetSettings_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "Are you sure you want to reset all settings to defaults?",
            "Confirm Reset",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            _viewModel.ResetToDefaultsCommand.Execute(null);
        }
    }

    private void CopyWorkflowJson_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var workflowContent = new
            {
                platform = "Suno",
                task = "create_music",
                content = new
                {
                    lyrics = _viewModel.Lyrics,
                    style = _viewModel.DefaultStyle,
                    song_title = _viewModel.SongTitle,
                    instrumental = _viewModel.InstrumentalOnly
                },
                settings = new
                {
                    download_folder = _viewModel.DownloadFolder,
                    folder_name = _viewModel.PreviewFolderName,
                    format = _viewModel.DownloadFormat,
                    download_both = _viewModel.DownloadBothSongs
                }
            };

            var json = System.Text.Json.JsonSerializer.Serialize(workflowContent,
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

            Clipboard.SetText(json);
            _viewModel.StatusMessage = "Workflow JSON copied to clipboard!";
        }
        catch (Exception ex)
        {
            _viewModel.StatusMessage = $"Error copying: {ex.Message}";
        }
    }
}
