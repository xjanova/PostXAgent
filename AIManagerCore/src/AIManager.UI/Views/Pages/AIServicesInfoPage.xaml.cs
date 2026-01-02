using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AIManager.Core.Services;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.DependencyInjection;

namespace AIManager.UI.Views.Pages;

/// <summary>
/// AI Services Info Page - แสดงข้อมูล API และการตั้งค่า
/// </summary>
public partial class AIServicesInfoPage : Page
{
    private readonly GpuSetupGuideService _guideService;

    private static readonly SolidColorBrush GreenBrush = new(Color.FromRgb(16, 185, 129));
    private static readonly SolidColorBrush RedBrush = new(Color.FromRgb(239, 68, 68));
    private static readonly SolidColorBrush PurpleBrush = new(Color.FromRgb(139, 92, 246));

    public AIServicesInfoPage()
    {
        InitializeComponent();

        _guideService = App.Services.GetService<GpuSetupGuideService>() ?? new GpuSetupGuideService();

        Loaded += async (s, e) =>
        {
            LoadAIServices();
            LoadEndpointsTable();
            await CheckOllamaStatusAsync();
        };
    }

    #region Load Data

    private void LoadAIServices()
    {
        var services = GpuSetupGuideService.GetAllAIServices();

        foreach (var service in services)
        {
            var card = CreateServiceCard(service);
            ServicesPanel.Children.Add(card);
        }
    }

    private Border CreateServiceCard(AIServiceInfo service)
    {
        var card = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(16, 255, 255, 255)),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(16),
            Margin = new Thickness(0, 0, 0, 12)
        };

        var mainStack = new StackPanel();

        // Header
        var header = new StackPanel { Orientation = Orientation.Horizontal };
        var icon = new PackIcon
        {
            Kind = GetServiceIcon(service.Name),
            Width = 24,
            Height = 24,
            Foreground = GetServiceColor(service),
            VerticalAlignment = VerticalAlignment.Center
        };
        header.Children.Add(icon);

        header.Children.Add(new TextBlock
        {
            Text = service.Name,
            FontSize = 16,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        });

        // Badges
        if (service.IsFree)
        {
            var freeBadge = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(32, 16, 185, 129)),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(8, 2, 8, 2),
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            freeBadge.Child = new TextBlock
            {
                Text = "FREE",
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Foreground = GreenBrush
            };
            header.Children.Add(freeBadge);
        }

        var typeBadge = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(32, 255, 255, 255)),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(8, 2, 8, 2),
            Margin = new Thickness(4, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        typeBadge.Child = new TextBlock
        {
            Text = service.Type == AIServiceType.Local ? "Local" : "Cloud",
            FontSize = 10,
            Opacity = 0.7
        };
        header.Children.Add(typeBadge);

        mainStack.Children.Add(header);

        // Description
        mainStack.Children.Add(new TextBlock
        {
            Text = service.Description,
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.7,
            Margin = new Thickness(0, 8, 0, 0)
        });

        // Base URL
        var urlPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
        urlPanel.Children.Add(new TextBlock { Text = "Base URL: ", Opacity = 0.6, FontSize = 12 });
        urlPanel.Children.Add(new TextBlock
        {
            Text = service.BaseUrl,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 12,
            Foreground = PurpleBrush
        });
        mainStack.Children.Add(urlPanel);

        // Port (if local)
        if (service.DefaultPort.HasValue)
        {
            var portPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
            portPanel.Children.Add(new TextBlock { Text = "Port: ", Opacity = 0.6, FontSize = 12 });
            portPanel.Children.Add(new TextBlock
            {
                Text = service.DefaultPort.Value.ToString(),
                FontWeight = FontWeights.SemiBold,
                FontSize = 12
            });
            mainStack.Children.Add(portPanel);
        }

        // API Key URL (if required)
        if (service.RequiresApiKey && !string.IsNullOrEmpty(service.ApiKeyUrl))
        {
            var apiKeyPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
            apiKeyPanel.Children.Add(new TextBlock { Text = "API Key: ", Opacity = 0.6, FontSize = 12 });
            var link = new TextBlock
            {
                Text = "Get API Key",
                Foreground = PurpleBrush,
                FontSize = 12,
                Cursor = System.Windows.Input.Cursors.Hand,
                TextDecorations = TextDecorations.Underline
            };
            link.MouseLeftButtonUp += (s, e) => OpenUrl(service.ApiKeyUrl);
            apiKeyPanel.Children.Add(link);
            mainStack.Children.Add(apiKeyPanel);
        }

        // Setup Steps (collapsible)
        if (service.SetupSteps.Any())
        {
            var expander = new Expander
            {
                Header = "Setup Steps",
                Margin = new Thickness(0, 12, 0, 0)
            };
            var stepsStack = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
            foreach (var step in service.SetupSteps)
            {
                stepsStack.Children.Add(new TextBlock
                {
                    Text = step,
                    FontSize = 12,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 2, 0, 0)
                });
            }
            expander.Content = stepsStack;
            mainStack.Children.Add(expander);
        }

        // Recommended Models (for Ollama)
        if (service.RecommendedModels.Any())
        {
            var modelsExpander = new Expander
            {
                Header = "Recommended Models",
                Margin = new Thickness(0, 8, 0, 0)
            };
            var modelsStack = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
            foreach (var model in service.RecommendedModels)
            {
                var modelPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
                modelPanel.Children.Add(new TextBlock
                {
                    Text = model.Name,
                    FontFamily = new FontFamily("Consolas"),
                    FontWeight = FontWeights.SemiBold,
                    Width = 140
                });
                modelPanel.Children.Add(new TextBlock
                {
                    Text = model.Size,
                    Width = 60,
                    Opacity = 0.6
                });
                modelPanel.Children.Add(new TextBlock
                {
                    Text = model.Description,
                    Opacity = 0.7,
                    TextWrapping = TextWrapping.Wrap
                });
                modelsStack.Children.Add(modelPanel);
            }
            modelsExpander.Content = modelsStack;
            mainStack.Children.Add(modelsExpander);
        }

        card.Child = mainStack;
        return card;
    }

    private void LoadEndpointsTable()
    {
        var ollamaInfo = GpuSetupGuideService.GetOllamaInfo();
        EndpointsGrid.ItemsSource = ollamaInfo.Endpoints;
    }

    #endregion

    #region Ollama Status

    private async Task CheckOllamaStatusAsync()
    {
        TxtOllamaStatus.Text = "Checking...";
        OllamaStatusIndicator.Fill = new SolidColorBrush(Colors.Gray);

        try
        {
            var status = await _guideService.CheckOllamaStatusAsync();

            if (status.IsRunning)
            {
                OllamaStatusIndicator.Fill = GreenBrush;
                TxtOllamaStatus.Text = "Running";
                TxtOllamaModels.Text = status.InstalledModels.Any()
                    ? $"Models: {string.Join(", ", status.InstalledModels.Take(3))}"
                    : "No models installed";
            }
            else
            {
                OllamaStatusIndicator.Fill = RedBrush;
                TxtOllamaStatus.Text = "Not Running";
                TxtOllamaModels.Text = status.Error ?? "Start Ollama to use local AI";
            }
        }
        catch (Exception ex)
        {
            OllamaStatusIndicator.Fill = RedBrush;
            TxtOllamaStatus.Text = "Error";
            TxtOllamaModels.Text = ex.Message;
        }
    }

    private async void BtnRefreshOllama_Click(object sender, RoutedEventArgs e)
    {
        await CheckOllamaStatusAsync();
    }

    private void BtnOpenOllamaDownload_Click(object sender, RoutedEventArgs e)
    {
        OpenUrl("https://ollama.ai/download");
    }

    #endregion

    #region Helpers

    private static PackIconKind GetServiceIcon(string serviceName) => serviceName switch
    {
        "Ollama" => PackIconKind.Server,
        "Google Gemini" => PackIconKind.Google,
        "OpenAI" => PackIconKind.OpenInNew,
        "Anthropic Claude" => PackIconKind.Brain,
        "HuggingFace Inference" => PackIconKind.FaceAgent,
        _ => PackIconKind.Api
    };

    private static SolidColorBrush GetServiceColor(AIServiceInfo service)
    {
        if (service.Name == "Ollama") return PurpleBrush;
        if (service.Name.Contains("Google")) return new SolidColorBrush(Color.FromRgb(66, 133, 244));
        if (service.Name.Contains("OpenAI")) return new SolidColorBrush(Color.FromRgb(116, 178, 117));
        if (service.Name.Contains("Anthropic")) return new SolidColorBrush(Color.FromRgb(255, 152, 0));
        if (service.Name.Contains("HuggingFace")) return new SolidColorBrush(Color.FromRgb(255, 213, 0));
        return PurpleBrush;
    }

    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch { }
    }

    #endregion
}
