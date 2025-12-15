using System.Windows.Controls;

namespace AIManager.UI.Views.Pages;

public partial class AIProvidersPage : Page
{
    public AIProvidersPage()
    {
        InitializeComponent();
        LoadProviders();
    }

    private void LoadProviders()
    {
        var providers = new[]
        {
            new AIProviderInfo
            {
                Name = "Ollama (Local)",
                Description = "Free, self-hosted LLM inference",
                Icon = "Robot",
                IsConfigured = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OLLAMA_BASE_URL")),
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
}
