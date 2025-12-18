using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using AIManager.Core.Services;
using Microsoft.Win32;

namespace AIManager.UI.Views.Pages;

public partial class ChatPage : Page
{
    private readonly OllamaChatService _chatService;
    private CancellationTokenSource? _currentCts;
    private TextBlock? _currentResponseBlock;
    private bool _isGenerating;
    private string? _attachedImageBase64;
    private string? _attachedImagePath;
    private DispatcherTimer? _thinkingTimer;
    private int _thinkingDots;

    public ChatPage()
    {
        InitializeComponent();
        _chatService = new OllamaChatService();

        // Subscribe to streaming events
        _chatService.OnStreamToken += OnStreamToken;
        _chatService.OnResponseComplete += OnResponseComplete;

        Loaded += ChatPage_Loaded;
    }

    private async void ChatPage_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadModelsAsync();
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

        // Create response bubble with thinking animation
        _currentResponseBlock = AddAssistantMessage("กำลังคิด");
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
                    if (_currentResponseBlock != null)
                    {
                        _currentResponseBlock.Text = "⚠️ Model ปัจจุบันไม่รองรับการวิเคราะห์รูปภาพ\nกรุณาเลือก vision model เช่น llava หรือ llama3.2-vision";
                        _currentResponseBlock.Foreground = new SolidColorBrush(Colors.Orange);
                    }
                    return;
                }

                StopThinkingAnimation();
                _currentResponseBlock!.Text = "";
                await _chatService.ChatWithImageAsync(message, _attachedImageBase64, _currentCts.Token);
            }
            else
            {
                StopThinkingAnimation();
                _currentResponseBlock!.Text = "";
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
        }
        catch (Exception ex)
        {
            StopThinkingAnimation();
            if (_currentResponseBlock != null)
            {
                _currentResponseBlock.Text = $"❌ Error: {ex.Message}";
                _currentResponseBlock.Foreground = new SolidColorBrush(Colors.Red);
            }
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

    private void StartThinkingAnimation()
    {
        _thinkingDots = 0;
        _thinkingTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(400)
        };
        _thinkingTimer.Tick += (s, e) =>
        {
            if (_currentResponseBlock != null && _isGenerating)
            {
                _thinkingDots = (_thinkingDots + 1) % 4;
                var dots = new string('.', _thinkingDots);
                _currentResponseBlock.Text = $"กำลังคิด{dots}";
            }
        };
        _thinkingTimer.Start();
    }

    private void StopThinkingAnimation()
    {
        _thinkingTimer?.Stop();
        _thinkingTimer = null;
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
                TxtImageName.Text = Path.GetFileName(dialog.FileName);
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

        // Create response bubble with thinking animation
        _currentResponseBlock = AddAssistantMessage("กำลังค้นหาเว็บ");
        StartThinkingAnimation();

        _currentCts = new CancellationTokenSource();

        try
        {
            StopThinkingAnimation();
            _currentResponseBlock!.Text = "";
            await _chatService.ChatWithWebSearchAsync(message, _currentCts.Token);
        }
        catch (OperationCanceledException)
        {
            StopThinkingAnimation();
            if (_currentResponseBlock != null)
            {
                _currentResponseBlock.Text += "\n\n[หยุดการค้นหา]";
            }
        }
        catch (Exception ex)
        {
            StopThinkingAnimation();
            if (_currentResponseBlock != null)
            {
                _currentResponseBlock.Text = $"❌ Error: {ex.Message}";
                _currentResponseBlock.Foreground = new SolidColorBrush(Colors.Red);
            }
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
        var border = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(232, 245, 233)), // Light green
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(15),
            Margin = new Thickness(80, 0, 0, 10),
            HorizontalAlignment = HorizontalAlignment.Right
        };

        var stack = new StackPanel();

        var header = new TextBlock
        {
            Text = "You",
            FontWeight = FontWeights.Bold,
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.FromRgb(56, 142, 60)),
            Margin = new Thickness(0, 0, 0, 5)
        };
        stack.Children.Add(header);

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
                    CornerRadius = new CornerRadius(8),
                    ClipToBounds = true,
                    Child = image
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
            FontSize = 14
        };

        stack.Children.Add(content);
        border.Child = stack;

        MessagesPanel.Children.Add(border);
        ChatScroller.ScrollToEnd();
    }

    private TextBlock AddAssistantMessage(string message)
    {
        var border = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(227, 242, 253)), // Light blue
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(15),
            Margin = new Thickness(0, 0, 80, 10),
            HorizontalAlignment = HorizontalAlignment.Left
        };

        var stack = new StackPanel();

        var header = new TextBlock
        {
            Text = "🤖 AI Assistant",
            FontWeight = FontWeights.Bold,
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.FromRgb(25, 118, 210)),
            Margin = new Thickness(0, 0, 0, 5)
        };

        var content = new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 14
        };

        stack.Children.Add(header);
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

            // Keep only welcome message
            MessagesPanel.Children.Clear();

            var welcomeBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(227, 242, 253)),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(15),
                Margin = new Thickness(0, 0, 80, 10),
                HorizontalAlignment = HorizontalAlignment.Left
            };

            var stack = new StackPanel();
            stack.Children.Add(new TextBlock
            {
                Text = "🤖 AI Assistant",
                FontWeight = FontWeights.Bold,
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(25, 118, 210)),
                Margin = new Thickness(0, 0, 0, 5)
            });
            stack.Children.Add(new TextBlock
            {
                Text = "สวัสดีครับ! ผมคือ AI Assistant พร้อมช่วยเหลือคุณ\nคุณสามารถถามคำถาม หรือให้ช่วยสร้างเนื้อหาสำหรับโซเชียลมีเดียได้เลยครับ",
                TextWrapping = TextWrapping.Wrap,
                FontSize = 14
            });
            welcomeBorder.Child = stack;
            MessagesPanel.Children.Add(welcomeBorder);
        }
    }
}
