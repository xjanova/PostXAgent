using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;
using AIManager.Core.Services;
using AIManager.Core.Models;

namespace AIManager.UI.Views.Pages;

/// <summary>
/// Content Creator Page - สร้างเนื้อหาด้วย AI สำหรับ Social Media
/// </summary>
public partial class ContentCreatorPage : Page
{
    private readonly ContentGeneratorService _contentGenerator;
    private readonly DebugLogger _logger = DebugLogger.Instance;
    private CancellationTokenSource? _cancellationTokenSource;
    private GeneratedContent? _currentContent;
    private string _selectedProvider = "auto";

    // Platform character limits
    private readonly Dictionary<string, int> _platformLimits = new()
    {
        { "Facebook", 63206 },
        { "Instagram", 2200 },
        { "Twitter/X", 280 },
        { "TikTok", 2200 },
        { "LinkedIn", 3000 },
        { "YouTube", 5000 },
        { "Threads", 500 },
        { "LINE", 2000 }
    };

    // Platform icons
    private readonly Dictionary<string, (PackIconKind icon, string color)> _platformIcons = new()
    {
        { "Facebook", (PackIconKind.Facebook, "#1877F2") },
        { "Instagram", (PackIconKind.Instagram, "#E4405F") },
        { "Twitter/X", (PackIconKind.Twitter, "#1DA1F2") },
        { "TikTok", (PackIconKind.MusicNote, "#FF0050") },
        { "LinkedIn", (PackIconKind.Linkedin, "#0A66C2") },
        { "YouTube", (PackIconKind.Youtube, "#FF0000") },
        { "Threads", (PackIconKind.At, "#000000") },
        { "LINE", (PackIconKind.Message, "#00B900") }
    };

    public ContentCreatorPage()
    {
        InitializeComponent();
        _contentGenerator = new ContentGeneratorService();
        _logger.LogInfo("ContentCreator", "Page initialized");

        Loaded += async (s, e) => await CheckProvidersAsync();

        // Update character count when content changes
        TxtGeneratedContent.TextChanged += (s, e) => UpdateCharacterCount();
    }

    private async Task CheckProvidersAsync()
    {
        try
        {
            TxtProviderStatus.Text = "Checking providers...";
            var providers = await _contentGenerator.CheckProvidersAsync();

            var available = providers.Where(p => p.IsAvailable).ToList();
            if (available.Any())
            {
                var freeAvailable = available.Where(p => p.IsFree).ToList();
                if (freeAvailable.Any())
                {
                    TxtProviderStatus.Text = $"{freeAvailable.Count} free provider(s) ready";
                    TxtProviderStatus.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129));
                }
                else
                {
                    TxtProviderStatus.Text = $"{available.Count} provider(s) ready (paid)";
                    TxtProviderStatus.Foreground = new SolidColorBrush(Color.FromRgb(245, 158, 11));
                }
            }
            else
            {
                TxtProviderStatus.Text = "No providers available. Configure in Settings.";
                TxtProviderStatus.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("ContentCreator", "Error checking providers", ex);
            TxtProviderStatus.Text = "Error checking providers";
            TxtProviderStatus.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68));
        }
    }

    private void CboProvider_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CboProvider.SelectedItem is ComboBoxItem item && item.Tag is string provider)
        {
            _selectedProvider = provider;
        }
    }

    private void Template_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string template)
        {
            var brandName = string.IsNullOrWhiteSpace(TxtBrandName.Text) ? "แบรนด์" : TxtBrandName.Text;
            var industry = (CboIndustry.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "E-Commerce";

            var prompts = new Dictionary<string, string>
            {
                { "promotion", $"สร้างโพสต์โปรโมชั่นพิเศษสำหรับ {brandName} ลด 20% ทุกสินค้า เน้นความคุ้มค่าและเร่งด่วน" },
                { "new_product", $"ประกาศเปิดตัวสินค้าใหม่ของ {brandName} เน้นคุณสมบัติเด่นและจุดขาย" },
                { "tips", $"แชร์ 5 เคล็ดลับที่เกี่ยวกับ {industry} สำหรับผู้ติดตาม {brandName}" },
                { "bts", $"เปิดเผยเบื้องหลังการทำงานของทีม {brandName} ให้ผู้ติดตามได้รู้จักเรามากขึ้น" },
                { "qa", $"โพสต์ Q&A ตอบคำถามที่พบบ่อยเกี่ยวกับสินค้า/บริการของ {brandName}" },
                { "testimonial", $"แชร์รีวิวจากลูกค้าที่พึงพอใจกับ {brandName} พร้อมขอบคุณที่ไว้วางใจ" }
            };

            if (prompts.TryGetValue(template, out var prompt))
            {
                TxtPrompt.Text = prompt;
            }
        }
    }

    private async void Generate_Click(object sender, RoutedEventArgs e)
    {
        await GenerateContentAsync();
    }

    private async void Regenerate_Click(object sender, RoutedEventArgs e)
    {
        await GenerateContentAsync();
    }

    private async Task GenerateContentAsync()
    {
        var prompt = TxtPrompt.Text.Trim();
        if (string.IsNullOrEmpty(prompt))
        {
            MessageBox.Show("Please enter a prompt.", "Missing Prompt", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource = new CancellationTokenSource();

        BtnGenerate.IsEnabled = false;
        BtnRegenerate.IsEnabled = false;
        PlaceholderPanel.Visibility = Visibility.Collapsed;
        ProgressOverlay.Visibility = Visibility.Visible;
        ContentPreview.Visibility = Visibility.Collapsed;

        TxtProgressStatus.Text = "Generating content...";
        TxtProgressProvider.Text = _selectedProvider == "auto" ? "Auto-selecting provider..." : $"Using {_selectedProvider}";

        try
        {
            _logger.LogInfo("ContentCreator", $"Generating content with prompt: {prompt.Substring(0, Math.Min(50, prompt.Length))}...");

            // Build brand info
            var brandInfo = new BrandInfo
            {
                Name = TxtBrandName.Text.Trim(),
                Industry = (CboIndustry.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "General",
                Tone = (CboTone.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Professional",
                TargetAudience = "General"
            };

            // Get platform
            var platform = GetSelectedPlatform();
            var language = RbThai.IsChecked == true ? "th" : "en";

            // Generate content
            _currentContent = await _contentGenerator.GenerateAsync(
                prompt,
                brandInfo,
                platform,
                language,
                _cancellationTokenSource.Token);

            // Display result
            DisplayContent(_currentContent, platform);

            _logger.LogInfo("ContentCreator", $"Content generated successfully using {_currentContent.Provider}");
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("ContentCreator", "Generation cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError("ContentCreator", "Error generating content", ex);
            MessageBox.Show($"Error generating content: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            PlaceholderPanel.Visibility = Visibility.Visible;
        }
        finally
        {
            BtnGenerate.IsEnabled = true;
            BtnRegenerate.IsEnabled = _currentContent != null;
            ProgressOverlay.Visibility = Visibility.Collapsed;
        }
    }

    private string GetSelectedPlatform()
    {
        if (CboPlatform.SelectedItem is ComboBoxItem item)
        {
            var content = item.Content;
            if (content is StackPanel panel && panel.Children.Count > 1)
            {
                if (panel.Children[1] is TextBlock textBlock)
                {
                    return textBlock.Text;
                }
            }
        }
        return "Facebook";
    }

    private void DisplayContent(GeneratedContent content, string platform)
    {
        ContentPreview.Visibility = Visibility.Visible;

        // Set platform icon and name
        if (_platformIcons.TryGetValue(platform, out var iconInfo))
        {
            PlatformIcon.Kind = iconInfo.icon;
            PlatformIcon.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(iconInfo.color));
        }
        TxtPlatformName.Text = $"{platform} Post";

        // Set content
        TxtGeneratedContent.Text = content.Text;

        // Display hashtags
        if (content.Hashtags.Any())
        {
            HashtagsPanel.Visibility = Visibility.Visible;
            HashtagsWrap.Children.Clear();

            foreach (var hashtag in content.Hashtags)
            {
                var border = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(45, 45, 68)),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(8, 4, 8, 4),
                    Margin = new Thickness(0, 0, 5, 5),
                    Cursor = System.Windows.Input.Cursors.Hand
                };

                var textBlock = new TextBlock
                {
                    Text = $"#{hashtag}",
                    Foreground = new SolidColorBrush(Color.FromRgb(139, 92, 246)),
                    FontSize = 12
                };

                border.Child = textBlock;
                border.MouseLeftButtonUp += (s, e) =>
                {
                    // Add hashtag to content
                    if (!TxtGeneratedContent.Text.Contains($"#{hashtag}"))
                    {
                        TxtGeneratedContent.Text += $"\n#{hashtag}";
                    }
                };

                HashtagsWrap.Children.Add(border);
            }
        }
        else
        {
            HashtagsPanel.Visibility = Visibility.Collapsed;
        }

        // Show provider badge
        ProviderBadge.Visibility = Visibility.Visible;
        TxtProviderBadge.Text = GetProviderDisplayName(content.Provider);

        // Enable action buttons
        BtnCopy.IsEnabled = true;
        BtnSaveDraft.IsEnabled = true;
        BtnSchedule.IsEnabled = true;
        BtnPostNow.IsEnabled = true;

        UpdateCharacterCount();
    }

    private string GetProviderDisplayName(string provider)
    {
        return provider.ToLower() switch
        {
            "ollama" => "Ollama",
            "google" => "Gemini",
            "huggingface" => "HuggingFace",
            "openai" => "GPT-4",
            "anthropic" => "Claude",
            _ => provider
        };
    }

    private void UpdateCharacterCount()
    {
        var charCount = TxtGeneratedContent.Text.Length;
        TxtCharCount.Text = charCount.ToString();

        var platform = GetSelectedPlatform();
        if (_platformLimits.TryGetValue(platform, out var limit))
        {
            TxtCharLimit.Text = $"/ {limit}";

            if (charCount > limit)
            {
                TxtCharCount.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68));
            }
            else if (charCount > limit * 0.9)
            {
                TxtCharCount.Foreground = new SolidColorBrush(Color.FromRgb(245, 158, 11));
            }
            else
            {
                TxtCharCount.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129));
            }
        }
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var content = TxtGeneratedContent.Text;
            Clipboard.SetText(content);

            // Visual feedback
            var originalContent = BtnCopy.Content;
            BtnCopy.Content = "Copied!";
            Task.Delay(1500).ContinueWith(_ =>
            {
                Dispatcher.Invoke(() => BtnCopy.Content = originalContent);
            });

            _logger.LogInfo("ContentCreator", "Content copied to clipboard");
        }
        catch (Exception ex)
        {
            _logger.LogError("ContentCreator", "Error copying to clipboard", ex);
        }
    }

    private void SaveDraft_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Save to local drafts folder
            var draftsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PostXAgent", "Drafts");

            Directory.CreateDirectory(draftsPath);

            var platform = GetSelectedPlatform();
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var fileName = $"draft_{platform}_{timestamp}.txt";
            var filePath = Path.Combine(draftsPath, fileName);

            var content = $"Platform: {platform}\n";
            content += $"Created: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n";
            content += $"Provider: {_currentContent?.Provider ?? "Unknown"}\n";
            content += $"---\n\n";
            content += TxtGeneratedContent.Text;

            if (_currentContent?.Hashtags.Any() == true)
            {
                content += $"\n\n---\nHashtags: {string.Join(" ", _currentContent.Hashtags.Select(h => $"#{h}"))}";
            }

            File.WriteAllText(filePath, content);

            MessageBox.Show($"Draft saved to:\n{filePath}", "Draft Saved", MessageBoxButton.OK, MessageBoxImage.Information);

            _logger.LogInfo("ContentCreator", $"Draft saved: {fileName}");
        }
        catch (Exception ex)
        {
            _logger.LogError("ContentCreator", "Error saving draft", ex);
            MessageBox.Show($"Error saving draft: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Schedule_Click(object sender, RoutedEventArgs e)
    {
        // TODO: Open schedule dialog
        MessageBox.Show("Schedule feature coming soon!\n\nThis will allow you to schedule posts for specific times.",
            "Coming Soon", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void PostNow_Click(object sender, RoutedEventArgs e)
    {
        var platform = GetSelectedPlatform();
        var result = MessageBox.Show(
            $"Post this content to {platform} now?\n\nMake sure you have configured the {platform} account in Platforms settings.",
            "Confirm Post",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            // TODO: Implement actual posting via API
            PostContentAsync(platform, TxtGeneratedContent.Text);
        }
    }

    private async void PostContentAsync(string platform, string content)
    {
        LoadingOverlay.Visibility = Visibility.Visible;
        TxtLoadingMessage.Text = $"Posting to {platform}...";

        try
        {
            // Get credentials from settings file
            var credentials = await LoadPlatformCredentialsAsync(platform);

            if (credentials == null || !credentials.ContainsKey("access_token"))
            {
                var configureResult = MessageBox.Show(
                    $"No {platform} credentials found.\n\nWould you like to configure them now?",
                    "Credentials Required",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (configureResult == MessageBoxResult.Yes)
                {
                    // Navigate to Platforms page
                    if (Window.GetWindow(this) is MainWindow mainWindow)
                    {
                        mainWindow.NavigateToPage("Platforms");
                    }
                }

                LoadingOverlay.Visibility = Visibility.Collapsed;
                return;
            }

            // Post using the service
            var postService = new PostPublisherService();
            var result = await postService.PostContentAsync(platform, content, credentials);

            if (result.Success)
            {
                var successMessage = $"Content posted successfully to {platform}!";
                if (!string.IsNullOrEmpty(result.PostUrl))
                {
                    successMessage += $"\n\nPost URL: {result.PostUrl}";
                }

                MessageBox.Show(successMessage, "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                _logger.LogInfo("ContentCreator", $"Content posted to {platform}: {result.PostId}");
            }
            else
            {
                MessageBox.Show($"Failed to post to {platform}:\n\n{result.Error}", "Post Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                _logger.LogWarning("ContentCreator", $"Post failed: {result.Error}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("ContentCreator", $"Error posting to {platform}", ex);
            MessageBox.Show($"Error posting to {platform}: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            LoadingOverlay.Visibility = Visibility.Collapsed;
        }
    }

    private async Task<Dictionary<string, string>?> LoadPlatformCredentialsAsync(string platform)
    {
        try
        {
            var credentialsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PostXAgent", "credentials.json");

            if (!File.Exists(credentialsPath))
            {
                return null;
            }

            var json = await File.ReadAllTextAsync(credentialsPath);
            var allCredentials = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(json);

            if (allCredentials != null && allCredentials.TryGetValue(platform.ToLower(), out var platformCreds))
            {
                return platformCreds;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError("ContentCreator", "Error loading credentials", ex);
            return null;
        }
    }
}
