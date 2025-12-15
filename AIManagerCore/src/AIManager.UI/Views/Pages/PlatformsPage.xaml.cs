using System.Windows.Controls;

namespace AIManager.UI.Views.Pages;

public partial class PlatformsPage : Page
{
    public PlatformsPage()
    {
        InitializeComponent();
        LoadPlatforms();
    }

    private void LoadPlatforms()
    {
        var platforms = new[]
        {
            new PlatformInfo { Name = "Facebook", Icon = "Facebook", Color = "#1877F2", IsConnected = false },
            new PlatformInfo { Name = "Instagram", Icon = "Instagram", Color = "#DD2A7B", IsConnected = false },
            new PlatformInfo { Name = "TikTok", Icon = "Video", Color = "#00F2EA", IsConnected = false },
            new PlatformInfo { Name = "Twitter/X", Icon = "Twitter", Color = "#1DA1F2", IsConnected = false },
            new PlatformInfo { Name = "LINE", Icon = "Chat", Color = "#00C300", IsConnected = false },
            new PlatformInfo { Name = "YouTube", Icon = "Youtube", Color = "#FF0000", IsConnected = false },
            new PlatformInfo { Name = "Threads", Icon = "At", Color = "#000000", IsConnected = false },
            new PlatformInfo { Name = "LinkedIn", Icon = "Linkedin", Color = "#0A66C2", IsConnected = false },
            new PlatformInfo { Name = "Pinterest", Icon = "Pinterest", Color = "#E60023", IsConnected = false }
        };

        PlatformsList.ItemsSource = platforms;
    }

    public class PlatformInfo
    {
        public string Name { get; set; } = "";
        public string Icon { get; set; } = "";
        public string Color { get; set; } = "";
        public bool IsConnected { get; set; }
    }
}
