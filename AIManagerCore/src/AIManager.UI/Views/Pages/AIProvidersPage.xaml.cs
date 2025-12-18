using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using AIManager.Core.Services;

namespace AIManager.UI.Views.Pages;

public partial class AIProvidersPage : Page
{
    private readonly HuggingFaceModelManager? _hfManager;
    private readonly GpuRentalService? _gpuService;

    public AIProvidersPage()
    {
        InitializeComponent();

        // Get services from DI
        _hfManager = App.Services?.GetService<HuggingFaceModelManager>();
        _gpuService = App.Services?.GetService<GpuRentalService>();

        LoadProviders();
        LoadGpuProviders();
        LoadHuggingFaceStatus();
    }

    private void Tab_Changed(object sender, RoutedEventArgs e)
    {
        // Guard against null during XAML initialization
        if (ProvidersPanel == null || HuggingFacePanel == null || GpuRentalPanel == null) return;

        if (TabProviders?.IsChecked == true)
        {
            ProvidersPanel.Visibility = Visibility.Visible;
            HuggingFacePanel.Visibility = Visibility.Collapsed;
            GpuRentalPanel.Visibility = Visibility.Collapsed;
        }
        else if (TabHuggingFace?.IsChecked == true)
        {
            ProvidersPanel.Visibility = Visibility.Collapsed;
            HuggingFacePanel.Visibility = Visibility.Visible;
            GpuRentalPanel.Visibility = Visibility.Collapsed;
        }
        else if (TabGpuRental?.IsChecked == true)
        {
            ProvidersPanel.Visibility = Visibility.Collapsed;
            HuggingFacePanel.Visibility = Visibility.Collapsed;
            GpuRentalPanel.Visibility = Visibility.Visible;
        }
    }

    private void LoadProviders()
    {
        var ollamaModel = Environment.GetEnvironmentVariable("OLLAMA_MODEL") ?? "llama2";
        var isLlama4 = ollamaModel.StartsWith("llama4");
        var hfToken = Environment.GetEnvironmentVariable("HF_TOKEN");

        var providers = new[]
        {
            new AIProviderInfo
            {
                Name = isLlama4 ? $"Llama 4 ({ollamaModel})" : "Ollama (Local)",
                Description = isLlama4
                    ? "Meta's latest Llama 4 model via Ollama - 400B params, Mixture of Experts"
                    : $"Self-hosted LLM ({ollamaModel})",
                Icon = "Robot",
                IsConfigured = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OLLAMA_BASE_URL")),
                IsFree = true
            },
            new AIProviderInfo
            {
                Name = "HuggingFace",
                Description = "Access to 500k+ models, Gated models (Llama, Flux), and GPU Spaces",
                Icon = "Github",
                IsConfigured = !string.IsNullOrEmpty(hfToken),
                IsFree = true
            },
            new AIProviderInfo
            {
                Name = "Google Gemini",
                Description = "Google's multimodal AI model",
                Icon = "Google",
                IsConfigured = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GOOGLE_API_KEY")),
                IsFree = true
            },
            new AIProviderInfo
            {
                Name = "OpenAI GPT-4",
                Description = "OpenAI's most capable model",
                Icon = "Brain",
                IsConfigured = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OPENAI_API_KEY")),
                IsFree = false
            },
            new AIProviderInfo
            {
                Name = "Anthropic Claude",
                Description = "Anthropic's helpful AI assistant",
                Icon = "MessageReply",
                IsConfigured = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")),
                IsFree = false
            },
            new AIProviderInfo
            {
                Name = "Stable Diffusion",
                Description = "Free, self-hosted image generation",
                Icon = "Image",
                IsConfigured = false,
                IsFree = true
            },
            new AIProviderInfo
            {
                Name = "DALL-E 3",
                Description = "OpenAI's image generation model",
                Icon = "ImageArea",
                IsConfigured = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OPENAI_API_KEY")),
                IsFree = false
            }
        };

        ProvidersList.ItemsSource = providers;
    }

    private void LoadGpuProviders()
    {
        var freeProviders = GpuRentalService.AllProviders
            .Where(p => p.IsFree)
            .Select(p => new GpuProviderInfo
            {
                Id = p.Id,
                Name = p.Name,
                Description = p.Description,
                GpuType = p.GpuType,
                Features = p.Features,
                SetupUrl = p.SetupUrl,
                DocumentationUrl = p.DocumentationUrl
            })
            .ToList();

        var paidProviders = GpuRentalService.AllProviders
            .Where(p => !p.IsFree)
            .Select(p => new GpuProviderInfo
            {
                Id = p.Id,
                Name = p.Name,
                Description = p.Description,
                GpuType = p.GpuType,
                Features = p.Features,
                SetupUrl = p.SetupUrl,
                DocumentationUrl = p.DocumentationUrl,
                HourlyRate = p.HourlyRate,
                MonthlyRate = p.MonthlyRate
            })
            .ToList();

        FreeGpuList.ItemsSource = freeProviders;
        PaidGpuList.ItemsSource = paidProviders;
    }

    private async void LoadHuggingFaceStatus()
    {
        if (_hfManager == null) return;

        var token = Environment.GetEnvironmentVariable("HF_TOKEN");
        if (!string.IsNullOrEmpty(token))
        {
            _hfManager.HuggingFaceToken = token;
            var status = await _hfManager.ValidateTokenAsync();
            UpdateHFStatusUI(status);
        }
    }

    private void UpdateHFStatusUI(HuggingFaceTokenStatus status)
    {
        Dispatcher.Invoke(() =>
        {
            if (status.IsValid)
            {
                HFStatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(76, 175, 80)); // Green
                HFStatusBorder.Background = new SolidColorBrush(Color.FromRgb(232, 245, 233));
                TxtHFStatus.Text = status.Message ?? $"Connected as {status.Username}";
            }
            else
            {
                HFStatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(244, 67, 54)); // Red
                HFStatusBorder.Background = new SolidColorBrush(Color.FromRgb(255, 235, 238));
                TxtHFStatus.Text = status.Message ?? "Invalid token";
            }
        });
    }

    private async void ValidateHFToken_Click(object sender, RoutedEventArgs e)
    {
        if (_hfManager == null)
        {
            MessageBox.Show("HuggingFace manager not available", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var token = TxtHFToken.Password;
        if (string.IsNullOrEmpty(token))
        {
            MessageBox.Show("Please enter a token", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        BtnValidateHF.IsEnabled = false;
        TxtHFStatus.Text = "Validating...";

        try
        {
            _hfManager.HuggingFaceToken = token;
            var status = await _hfManager.ValidateTokenAsync();
            UpdateHFStatusUI(status);
        }
        finally
        {
            BtnValidateHF.IsEnabled = true;
        }
    }

    private void SaveHFToken_Click(object sender, RoutedEventArgs e)
    {
        var token = TxtHFToken.Password;
        if (string.IsNullOrEmpty(token))
        {
            MessageBox.Show("Please enter a token first", "Save Token", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Save to environment (session)
        Environment.SetEnvironmentVariable("HF_TOKEN", token);

        if (_hfManager != null)
        {
            _hfManager.HuggingFaceToken = token;
        }

        MessageBox.Show(
            "Token saved for this session!\n\n" +
            "For permanent storage, add to your .env file:\n" +
            "HF_TOKEN=your_token_here",
            "Token Saved",
            MessageBoxButton.OK,
            MessageBoxImage.Information);

        // Refresh providers list
        LoadProviders();
    }

    private void GetHFToken_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://huggingface.co/settings/tokens",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to open browser: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SetupGpuProvider_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string providerId)
        {
            var provider = GpuRentalService.AllProviders.FirstOrDefault(p => p.Id == providerId);
            if (provider != null)
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = provider.SetupUrl,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to open browser: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }

    private void OpenGpuDocs_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string url && !string.IsNullOrEmpty(url))
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open browser: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    public class AIProviderInfo
    {
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string Icon { get; set; } = "";
        public bool IsConfigured { get; set; }
        public bool IsFree { get; set; }
        public string PriceTag => IsFree ? "FREE" : "PAID";
        public string PriceColor => IsFree ? "#4CAF50" : "#FF9800";
    }

    public class GpuProviderInfo
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string GpuType { get; set; } = "";
        public string[] Features { get; set; } = Array.Empty<string>();
        public string SetupUrl { get; set; } = "";
        public string DocumentationUrl { get; set; } = "";
        public decimal HourlyRate { get; set; }
        public decimal MonthlyRate { get; set; }

        public string PriceDisplay => MonthlyRate > 0
            ? $"${MonthlyRate}/mo"
            : HourlyRate > 0
                ? $"${HourlyRate}/hr"
                : "FREE";
    }
}
