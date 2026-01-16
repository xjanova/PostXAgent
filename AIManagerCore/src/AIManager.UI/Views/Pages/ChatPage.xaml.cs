using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using AIManager.Core.Services;
using Microsoft.Win32;

namespace AIManager.UI.Views.Pages;

public partial class ChatPage : Page
{
    private readonly OllamaChatService _chatService;
    private readonly AIKnowledgeService _knowledgeService;
    private CancellationTokenSource? _currentCts;
    private TextBlock? _currentResponseBlock;
    private bool _isGenerating;
    private string? _attachedImageBase64;
    private string? _attachedImagePath;
    private DispatcherTimer? _thinkingTimer;
    private int _thinkingDots;
    private bool _systemPromptLoaded = false;

    public ChatPage()
    {
        InitializeComponent();
        _chatService = new OllamaChatService();

        // ดึง CoreDatabaseService จาก DI container
        var coreDb = App.Services?.GetService(typeof(CoreDatabaseService)) as CoreDatabaseService;
        _knowledgeService = new AIKnowledgeService(coreDb);

        // Subscribe to streaming events
        _chatService.OnStreamToken += OnStreamToken;
        _chatService.OnResponseComplete += OnResponseComplete;

        Loaded += ChatPage_Loaded;
    }

    private async void ChatPage_Loaded(object sender, RoutedEventArgs e)
    {
        // โหลด models และ system prompt พร้อมกัน
        await Task.WhenAll(
            LoadModelsAsync(),
            LoadSystemPromptAsync()
        );
    }

    /// <summary>
    /// โหลด System Prompt จาก AIKnowledgeService (อ่านเอกสารจริงจากไฟล์)
    /// </summary>
    private async Task LoadSystemPromptAsync()
    {
        if (_systemPromptLoaded) return;

        try
        {
            // โหลด system prompt ที่มีความรู้จากเอกสารและ database
            var systemPrompt = await _knowledgeService.GetSystemPromptAsync();
            _chatService.SetSystemPrompt(systemPrompt);
            _systemPromptLoaded = true;
        }
        catch (Exception ex)
        {
            // ถ้าโหลดไม่ได้ ใช้ fallback prompt
            System.Diagnostics.Debug.WriteLine($"Failed to load system prompt: {ex.Message}");
            SetFallbackSystemPrompt();
        }
    }

    /// <summary>
    /// Fallback System Prompt กรณีโหลดจากไฟล์ไม่ได้
    /// </summary>
    private void SetFallbackSystemPrompt()
    {
        var fallbackPrompt = @"คุณคือ AI Assistant ของระบบ AIManager - ส่วนหนึ่งของ PostXAgent

ระบบนี้ใช้สำหรับจัดการการตลาดโซเชียลมีเดียในประเทศไทย รองรับ:
- สร้างเนื้อหาด้วย AI (ข้อความ, รูปภาพ)
- โพสต์อัตโนมัติไปยัง 9 แพลตฟอร์ม
- Web Automation และ Workflow
- Multi-GPU Image Generation

ตอบเป็นภาษาไทย ช่วยเหลือผู้ใช้ในการใช้งานระบบ";

        _chatService.SetSystemPrompt(fallbackPrompt);
        _systemPromptLoaded = true;
    }

    /// <summary>
    /// รีโหลด System Prompt (เมื่อต้องการอัพเดทข้อมูลใหม่)
    /// </summary>
    public async Task RefreshSystemPromptAsync()
    {
        _knowledgeService.InvalidateCache();
        _systemPromptLoaded = false;
        await LoadSystemPromptAsync();
    }

    private async Task LoadModelsAsync()
    {
        try
        {
            TxtStatus.Text = "กำลังเชื่อมต่อ Ollama...";

            var models = await _chatService.GetModelsAsync();

            if (models.Count == 0)
            {
                TxtStatus.Text = "❌ ไม่พบ Ollama หรือไม่มี model";
                TxtStatus.Foreground = new SolidColorBrush(Colors.Red);
                return;
            }

            CboModel.Items.Clear();
            foreach (var model in models)
            {
                CboModel.Items.Add(new ComboBoxItem
                {
                    Content = $"{model.Name} ({model.SizeDisplay})",
                    Tag = model.Name
                });
            }

            // Select current model or first
            var currentModel = _chatService.CurrentModel;
            var selected = false;
            for (var i = 0; i < CboModel.Items.Count; i++)
            {
                if (CboModel.Items[i] is ComboBoxItem item &&
                    item.Tag?.ToString() == currentModel)
                {
                    CboModel.SelectedIndex = i;
                    selected = true;
                    break;
                }
            }
            if (!selected && CboModel.Items.Count > 0)
            {
                CboModel.SelectedIndex = 0;
            }

            TxtStatus.Text = $"✓ เชื่อมต่อแล้ว - {models.Count} models";
            TxtStatus.Foreground = new SolidColorBrush(Color.FromRgb(76, 175, 80));
        }
        catch (Exception ex)
        {
            TxtStatus.Text = $"❌ Error: {ex.Message}";
            TxtStatus.Foreground = new SolidColorBrush(Colors.Red);
        }
    }

    private void CboModel_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CboModel.SelectedItem is ComboBoxItem item && item.Tag is string modelName)
        {
            _chatService.CurrentModel = modelName;
        }
    }

    private void TxtMessage_KeyDown(object sender, KeyEventArgs e)
    {
        // Enter to send (Shift+Enter for new line)
        if (e.Key == Key.Enter && Keyboard.Modifiers != ModifierKeys.Shift)
        {
            e.Handled = true;
            Send_Click(sender, e);
        }
    }

    private async void Send_Click(object sender, RoutedEventArgs e)
    {
        var message = TxtMessage.Text.Trim();
        if (string.IsNullOrEmpty(message) || _isGenerating) return;

        // Clear input
        TxtMessage.Text = "";

        // Add user message to UI (with image if attached)
        AddUserMessage(message, _attachedImagePath);

        // Prepare for response
        _isGenerating = true;
        BtnSend.Visibility = Visibility.Collapsed;
        BtnStop.Visibility = Visibility.Visible;

        // Show thinking animation first (no response bubble yet)
        StartThinkingAnimation();

        _currentCts = new CancellationTokenSource();

        try
        {
            // Check if we have an attached image
            if (!string.IsNullOrEmpty(_attachedImageBase64))
            {
                // Use vision model
                if (!_chatService.IsVisionModel())
                {
                    StopThinkingAnimation();
                    _currentResponseBlock = AddAssistantMessage("⚠️ Model ปัจจุบันไม่รองรับการวิเคราะห์รูปภาพ\nกรุณาเลือก vision model เช่น llava หรือ llama3.2-vision");
                    _currentResponseBlock.Foreground = new SolidColorBrush(Colors.Orange);
                    return;
                }

                // Animation will be stopped when first token arrives (in OnStreamToken)
                await _chatService.ChatWithImageAsync(message, _attachedImageBase64, _currentCts.Token);
            }
            else
            {
                // Animation will be stopped when first token arrives (in OnStreamToken)
                await _chatService.ChatStreamAsync(message, _currentCts.Token);
            }
        }
        catch (OperationCanceledException)
        {
            StopThinkingAnimation();
            if (_currentResponseBlock != null)
            {
                _currentResponseBlock.Text += "\n\n[หยุดการตอบกลับ]";
            }
            else
            {
                AddAssistantMessage("[หยุดการตอบกลับ]");
            }
        }
        catch (Exception ex)
        {
            StopThinkingAnimation();
            _currentResponseBlock = AddAssistantMessage($"❌ Error: {ex.Message}");
            _currentResponseBlock.Foreground = new SolidColorBrush(Colors.Red);
        }
        finally
        {
            _isGenerating = false;
            BtnSend.Visibility = Visibility.Visible;
            BtnStop.Visibility = Visibility.Collapsed;
            _currentCts?.Dispose();
            _currentCts = null;

            // Clear attached image after sending
            ClearAttachedImage();
        }
    }

    // Thinking animation elements
    private Border? _thinkingBorder;
    private StackPanel? _thinkingDotsPanel;
    private TextBlock? _thinkingTextBlock;
    private TextBlock? _thinkingTimeBlock;
    private DateTime _thinkingStartTime;
    private readonly string[] _thinkingMessages = new[]
    {
        "กำลังคิด",
        "กำลังประมวลผล",
        "กำลังวิเคราะห์",
        "กำลังค้นหาคำตอบ",
        "กำลังเตรียมคำตอบ",
        "รอสักครู่..."
    };
    private int _messageIndex = 0;

    private void StartThinkingAnimation()
    {
        _thinkingDots = 0;
        _messageIndex = 0;
        _thinkingStartTime = DateTime.Now;

        // Create standalone thinking animation panel in the chat area
        _thinkingBorder = new Border
        {
            CornerRadius = new CornerRadius(16, 16, 16, 4),
            Padding = new Thickness(16),
            Margin = new Thickness(0, 0, 60, 12),
            HorizontalAlignment = HorizontalAlignment.Left,
            MaxWidth = 380,
            Background = new LinearGradientBrush(
                Color.FromRgb(30, 30, 63),
                Color.FromRgb(37, 37, 82),
                45),
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = Color.FromRgb(167, 139, 250),
                BlurRadius = 20,
                ShadowDepth = 0,
                Opacity = 0.25
            }
        };

        var mainStack = new StackPanel();

        // Header with AI avatar
        var headerStack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };

        var avatarBorder = new Border
        {
            Width = 28,
            Height = 28,
            CornerRadius = new CornerRadius(8),
            Margin = new Thickness(0, 0, 10, 0),
            Background = new LinearGradientBrush(
                Color.FromRgb(167, 139, 250),
                Color.FromRgb(124, 77, 255),
                45)
        };
        var avatarIcon = new MaterialDesignThemes.Wpf.PackIcon
        {
            Kind = MaterialDesignThemes.Wpf.PackIconKind.Robot,
            Width = 16,
            Height = 16,
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        avatarBorder.Child = avatarIcon;
        headerStack.Children.Add(avatarBorder);

        headerStack.Children.Add(new TextBlock
        {
            Text = "AI Assistant",
            FontWeight = FontWeights.SemiBold,
            FontSize = 13,
            Foreground = new SolidColorBrush(Color.FromRgb(167, 139, 250)),
            VerticalAlignment = VerticalAlignment.Center
        });
        mainStack.Children.Add(headerStack);

        // Thinking animation content
        var thinkingContent = new Border
        {
            Background = new LinearGradientBrush(
                Color.FromArgb(50, 167, 139, 250),
                Color.FromArgb(30, 236, 72, 153),
                45),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(14, 10, 14, 10)
        };

        var thinkingMainStack = new StackPanel();

        // First row: Text + Dots
        var thinkingStack = new StackPanel { Orientation = Orientation.Horizontal };

        // Thinking text
        _thinkingTextBlock = new TextBlock
        {
            Text = _thinkingMessages[0],
            FontSize = 14,
            Foreground = new SolidColorBrush(Color.FromRgb(200, 200, 220)),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 0)
        };

        // Animated dots panel
        _thinkingDotsPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };

        // Create 3 animated dots with gradient
        for (int i = 0; i < 3; i++)
        {
            var dot = new Ellipse
            {
                Width = 10,
                Height = 10,
                Margin = new Thickness(3),
                Opacity = 0.3
            };
            dot.Fill = new LinearGradientBrush(
                Color.FromRgb(167, 139, 250),
                Color.FromRgb(236, 72, 153),
                45);
            _thinkingDotsPanel.Children.Add(dot);
        }

        thinkingStack.Children.Add(_thinkingTextBlock);
        thinkingStack.Children.Add(_thinkingDotsPanel);
        thinkingMainStack.Children.Add(thinkingStack);

        // Second row: Time elapsed (small text)
        _thinkingTimeBlock = new TextBlock
        {
            Text = "",
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(120, 120, 150)),
            Margin = new Thickness(0, 6, 0, 0)
        };
        thinkingMainStack.Children.Add(_thinkingTimeBlock);

        thinkingContent.Child = thinkingMainStack;
        mainStack.Children.Add(thinkingContent);

        _thinkingBorder.Child = mainStack;

        // Add thinking animation to messages panel
        MessagesPanel.Children.Add(_thinkingBorder);
        ChatScroller.ScrollToEnd();

        // Start animation timer
        _thinkingTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(150)
        };
        _thinkingTimer.Tick += ThinkingAnimation_Tick;
        _thinkingTimer.Start();
    }

    private void ThinkingAnimation_Tick(object? sender, EventArgs e)
    {
        if (!_isGenerating || _thinkingDotsPanel == null) return;

        _thinkingDots = (_thinkingDots + 1) % 18; // 18 frames for smooth animation

        // Animate dots with wave effect
        for (int i = 0; i < _thinkingDotsPanel.Children.Count; i++)
        {
            if (_thinkingDotsPanel.Children[i] is Ellipse dot)
            {
                // Create wave effect - each dot has different phase
                var phase = (_thinkingDots + i * 3) % 9;
                dot.Opacity = phase switch
                {
                    0 => 0.2,
                    1 => 0.4,
                    2 => 0.7,
                    3 => 1.0,
                    4 => 0.9,
                    5 => 0.7,
                    6 => 0.4,
                    7 => 0.3,
                    _ => 0.2
                };

                // Scale effect for bouncing animation
                var scale = phase switch
                {
                    2 => 1.1,
                    3 => 1.4,
                    4 => 1.3,
                    5 => 1.1,
                    _ => 1.0
                };
                dot.RenderTransform = new ScaleTransform(scale, scale);
                dot.RenderTransformOrigin = new Point(0.5, 0.5);
            }
        }

        // Change thinking message every ~2.7 seconds (18 ticks)
        if (_thinkingDots == 0)
        {
            _messageIndex = (_messageIndex + 1) % _thinkingMessages.Length;
            if (_thinkingTextBlock != null)
            {
                _thinkingTextBlock.Text = _thinkingMessages[_messageIndex];
            }
        }

        // Update elapsed time display (show after 2 seconds)
        var elapsed = DateTime.Now - _thinkingStartTime;
        if (_thinkingTimeBlock != null && elapsed.TotalSeconds >= 2)
        {
            var seconds = (int)elapsed.TotalSeconds;
            _thinkingTimeBlock.Text = $"⏱ รอแล้ว {seconds} วินาที...";
        }
    }

    private void StopThinkingAnimation()
    {
        _thinkingTimer?.Stop();
        _thinkingTimer = null;

        // Remove thinking animation panel from MessagesPanel
        if (_thinkingBorder != null)
        {
            MessagesPanel.Children.Remove(_thinkingBorder);
        }
        _thinkingBorder = null;
        _thinkingDotsPanel = null;
        _thinkingTextBlock = null;
        _thinkingTimeBlock = null;
    }

    private void Stop_Click(object sender, RoutedEventArgs e)
    {
        _currentCts?.Cancel();
        StopThinkingAnimation();
    }

    private void AttachImage_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "เลือกรูปภาพ",
            Filter = "Image Files|*.jpg;*.jpeg;*.png;*.gif;*.bmp|All Files|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                _attachedImagePath = dialog.FileName;
                var bytes = File.ReadAllBytes(dialog.FileName);
                _attachedImageBase64 = Convert.ToBase64String(bytes);

                // Show preview
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(dialog.FileName);
                bitmap.DecodePixelWidth = 80;
                bitmap.EndInit();

                PreviewImage.Source = bitmap;
                TxtImageName.Text = System.IO.Path.GetFileName(dialog.FileName);
                ImagePreviewPanel.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"ไม่สามารถโหลดรูปภาพได้: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void RemoveImage_Click(object sender, RoutedEventArgs e)
    {
        ClearAttachedImage();
    }

    private void ClearAttachedImage()
    {
        _attachedImageBase64 = null;
        _attachedImagePath = null;
        PreviewImage.Source = null;
        ImagePreviewPanel.Visibility = Visibility.Collapsed;
    }

    private async void WebSearch_Click(object sender, RoutedEventArgs e)
    {
        var message = TxtMessage.Text.Trim();
        if (string.IsNullOrEmpty(message) || _isGenerating) return;

        // Clear input
        TxtMessage.Text = "";

        // Add user message to UI
        AddUserMessage($"🔍 {message}");

        // Prepare for response
        _isGenerating = true;
        BtnSend.Visibility = Visibility.Collapsed;
        BtnStop.Visibility = Visibility.Visible;

        // Show thinking animation first (no response bubble yet)
        StartThinkingAnimation();

        _currentCts = new CancellationTokenSource();

        try
        {
            // Animation will be stopped when first token arrives (in OnStreamToken)
            await _chatService.ChatWithWebSearchAsync(message, _currentCts.Token);
        }
        catch (OperationCanceledException)
        {
            StopThinkingAnimation();
            if (_currentResponseBlock != null)
            {
                _currentResponseBlock.Text += "\n\n[หยุดการค้นหา]";
            }
            else
            {
                AddAssistantMessage("[หยุดการค้นหา]");
            }
        }
        catch (Exception ex)
        {
            StopThinkingAnimation();
            _currentResponseBlock = AddAssistantMessage($"❌ Error: {ex.Message}");
            _currentResponseBlock.Foreground = new SolidColorBrush(Colors.Red);
        }
        finally
        {
            _isGenerating = false;
            BtnSend.Visibility = Visibility.Visible;
            BtnStop.Visibility = Visibility.Collapsed;
            _currentCts?.Dispose();
            _currentCts = null;
        }
    }

    private void OnStreamToken(object? sender, string token)
    {
        Dispatcher.Invoke(() =>
        {
            // When first token arrives, stop animation and create response bubble
            if (_currentResponseBlock == null && _thinkingBorder != null)
            {
                StopThinkingAnimation();
                _currentResponseBlock = AddAssistantMessage("");
            }

            if (_currentResponseBlock != null)
            {
                _currentResponseBlock.Text += token;
                ChatScroller.ScrollToEnd();
            }
        });
    }

    private void OnResponseComplete(object? sender, ChatResponse response)
    {
        Dispatcher.Invoke(() =>
        {
            // Add stats to status
            if (response.TokensPerSecond > 0)
            {
                TxtStatus.Text = $"✓ เชื่อมต่อแล้ว | {response.TokensPerSecond:F1} tokens/s";
            }
        });
    }

    private void AddUserMessage(string message, string? imagePath = null)
    {
        // Futuristic user message bubble (Cyan-Purple gradient)
        var border = new Border
        {
            CornerRadius = new CornerRadius(16, 16, 4, 16),
            Padding = new Thickness(16),
            Margin = new Thickness(60, 0, 0, 12),
            HorizontalAlignment = HorizontalAlignment.Right,
            MaxWidth = 600,
            Background = new LinearGradientBrush(
                Color.FromRgb(26, 58, 74),
                Color.FromRgb(30, 42, 82),
                45),
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = Color.FromRgb(6, 182, 212),
                BlurRadius = 15,
                ShadowDepth = 0,
                Opacity = 0.15
            }
        };

        var stack = new StackPanel();

        // Header with avatar
        var headerStack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };

        var avatarBorder = new Border
        {
            Width = 28,
            Height = 28,
            CornerRadius = new CornerRadius(8),
            Margin = new Thickness(0, 0, 10, 0),
            Background = new LinearGradientBrush(
                Color.FromRgb(6, 182, 212),
                Color.FromRgb(34, 211, 238),
                45)
        };
        var avatarIcon = new MaterialDesignThemes.Wpf.PackIcon
        {
            Kind = MaterialDesignThemes.Wpf.PackIconKind.Account,
            Width = 16,
            Height = 16,
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        avatarBorder.Child = avatarIcon;
        headerStack.Children.Add(avatarBorder);

        headerStack.Children.Add(new TextBlock
        {
            Text = "You",
            FontWeight = FontWeights.SemiBold,
            FontSize = 13,
            Foreground = new SolidColorBrush(Color.FromRgb(6, 182, 212)),
            VerticalAlignment = VerticalAlignment.Center
        });
        stack.Children.Add(headerStack);

        // Show attached image if any
        if (!string.IsNullOrEmpty(imagePath) && File.Exists(imagePath))
        {
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(imagePath);
                bitmap.DecodePixelWidth = 200;
                bitmap.EndInit();

                var image = new Image
                {
                    Source = bitmap,
                    MaxWidth = 200,
                    MaxHeight = 150,
                    Stretch = Stretch.Uniform,
                    Margin = new Thickness(0, 0, 0, 8),
                    HorizontalAlignment = HorizontalAlignment.Left
                };

                var imageBorder = new Border
                {
                    CornerRadius = new CornerRadius(10),
                    ClipToBounds = true,
                    Child = image,
                    Effect = new System.Windows.Media.Effects.DropShadowEffect
                    {
                        Color = Colors.Black,
                        BlurRadius = 10,
                        ShadowDepth = 0,
                        Opacity = 0.3
                    }
                };

                stack.Children.Add(imageBorder);
            }
            catch
            {
                // Ignore image loading errors
            }
        }

        var content = new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 14,
            Foreground = new SolidColorBrush(Color.FromRgb(224, 224, 224)),
            LineHeight = 22
        };

        stack.Children.Add(content);
        border.Child = stack;

        MessagesPanel.Children.Add(border);
        ChatScroller.ScrollToEnd();
    }

    private TextBlock AddAssistantMessage(string message)
    {
        // Futuristic AI message bubble (Purple-Blue gradient)
        var border = new Border
        {
            CornerRadius = new CornerRadius(16, 16, 16, 4),
            Padding = new Thickness(16),
            Margin = new Thickness(0, 0, 60, 12),
            HorizontalAlignment = HorizontalAlignment.Left,
            MaxWidth = 600,
            Background = new LinearGradientBrush(
                Color.FromRgb(30, 30, 63),
                Color.FromRgb(37, 37, 82),
                45),
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = Color.FromRgb(167, 139, 250),
                BlurRadius = 15,
                ShadowDepth = 0,
                Opacity = 0.15
            }
        };

        var stack = new StackPanel();

        // Header with AI avatar
        var headerStack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };

        var avatarBorder = new Border
        {
            Width = 28,
            Height = 28,
            CornerRadius = new CornerRadius(8),
            Margin = new Thickness(0, 0, 10, 0),
            Background = new LinearGradientBrush(
                Color.FromRgb(167, 139, 250),
                Color.FromRgb(124, 77, 255),
                45)
        };
        var avatarIcon = new MaterialDesignThemes.Wpf.PackIcon
        {
            Kind = MaterialDesignThemes.Wpf.PackIconKind.Robot,
            Width = 16,
            Height = 16,
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        avatarBorder.Child = avatarIcon;
        headerStack.Children.Add(avatarBorder);

        headerStack.Children.Add(new TextBlock
        {
            Text = "AI Assistant",
            FontWeight = FontWeights.SemiBold,
            FontSize = 13,
            Foreground = new SolidColorBrush(Color.FromRgb(167, 139, 250)),
            VerticalAlignment = VerticalAlignment.Center
        });
        stack.Children.Add(headerStack);

        var content = new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 14,
            Foreground = new SolidColorBrush(Color.FromRgb(224, 224, 224)),
            LineHeight = 22
        };

        stack.Children.Add(content);
        border.Child = stack;

        MessagesPanel.Children.Add(border);
        ChatScroller.ScrollToEnd();

        return content;
    }

    private void ClearChat_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "ล้างประวัติการสนทนาทั้งหมด?",
            "Clear Chat",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            _chatService.ClearHistory();

            // Keep only welcome message with futuristic style
            MessagesPanel.Children.Clear();

            var welcomeBorder = new Border
            {
                CornerRadius = new CornerRadius(16, 16, 16, 4),
                Padding = new Thickness(16),
                Margin = new Thickness(0, 0, 60, 12),
                HorizontalAlignment = HorizontalAlignment.Left,
                MaxWidth = 600,
                Background = new LinearGradientBrush(
                    Color.FromRgb(30, 30, 63),
                    Color.FromRgb(37, 37, 82),
                    45),
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = Color.FromRgb(167, 139, 250),
                    BlurRadius = 15,
                    ShadowDepth = 0,
                    Opacity = 0.15
                }
            };

            var stack = new StackPanel();

            // Header with AI avatar
            var headerStack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };

            var avatarBorder = new Border
            {
                Width = 28,
                Height = 28,
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(0, 0, 10, 0),
                Background = new LinearGradientBrush(
                    Color.FromRgb(167, 139, 250),
                    Color.FromRgb(124, 77, 255),
                    45)
            };
            var avatarIcon = new MaterialDesignThemes.Wpf.PackIcon
            {
                Kind = MaterialDesignThemes.Wpf.PackIconKind.Robot,
                Width = 16,
                Height = 16,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            avatarBorder.Child = avatarIcon;
            headerStack.Children.Add(avatarBorder);

            headerStack.Children.Add(new TextBlock
            {
                Text = "AI Assistant",
                FontWeight = FontWeights.SemiBold,
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromRgb(167, 139, 250)),
                VerticalAlignment = VerticalAlignment.Center
            });
            stack.Children.Add(headerStack);

            // Welcome message content
            var welcomeText = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                FontSize = 14,
                Foreground = new SolidColorBrush(Color.FromRgb(224, 224, 224)),
                LineHeight = 22
            };
            welcomeText.Inlines.Add(new Run("สวัสดีครับ!") { FontWeight = FontWeights.SemiBold, Foreground = Brushes.White });
            welcomeText.Inlines.Add(new Run(" ผมคือ AI Assistant พร้อมช่วยเหลือคุณ\n\n"));
            welcomeText.Inlines.Add(new Run("คุณสามารถ:") { Foreground = new SolidColorBrush(Color.FromRgb(167, 139, 250)) });
            welcomeText.Inlines.Add(new Run("\n• ") { Foreground = new SolidColorBrush(Color.FromRgb(6, 182, 212)) });
            welcomeText.Inlines.Add(new Run("ถามคำถามเกี่ยวกับระบบ AIManager"));
            welcomeText.Inlines.Add(new Run("\n• ") { Foreground = new SolidColorBrush(Color.FromRgb(236, 72, 153)) });
            welcomeText.Inlines.Add(new Run("แนบรูปภาพเพื่อให้วิเคราะห์ (ต้องใช้ vision model)"));
            welcomeText.Inlines.Add(new Run("\n• ") { Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129)) });
            welcomeText.Inlines.Add(new Run("ค้นหาข้อมูลจากเว็บ (คลิกปุ่ม Search)"));

            stack.Children.Add(welcomeText);
            welcomeBorder.Child = stack;
            MessagesPanel.Children.Add(welcomeBorder);
        }
    }
}
