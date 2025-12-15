using System.Windows;
using System.Windows.Controls;

namespace AIManager.UI.Views.Pages;

public partial class SettingsPage : Page
{
    public SettingsPage()
    {
        InitializeComponent();
        LoadSettings();
    }

    private void LoadSettings()
    {
        TxtApiPort.Text = Environment.GetEnvironmentVariable("AI_MANAGER_API_PORT") ?? "5000";
        TxtWsPort.Text = Environment.GetEnvironmentVariable("AI_MANAGER_WS_PORT") ?? "5001";
        TxtSignalRPort.Text = Environment.GetEnvironmentVariable("AI_MANAGER_SIGNALR_PORT") ?? "5002";
        TxtRedisConnection.Text = Environment.GetEnvironmentVariable("REDIS_CONNECTION_STRING") ?? "localhost:6379";
        TxtOllamaUrl.Text = Environment.GetEnvironmentVariable("OLLAMA_BASE_URL") ?? "http://localhost:11434";
        TxtOllamaModel.Text = Environment.GetEnvironmentVariable("OLLAMA_MODEL") ?? "llama2";
    }

    private void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("Settings saved! Restart the application for changes to take effect.",
            "Settings", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ResetDefaults_Click(object sender, RoutedEventArgs e)
    {
        TxtApiPort.Text = "5000";
        TxtWsPort.Text = "5001";
        TxtSignalRPort.Text = "5002";
        TxtRedisConnection.Text = "localhost:6379";
        TxtOllamaUrl.Text = "http://localhost:11434";
        TxtOllamaModel.Text = "llama2";
    }
}
