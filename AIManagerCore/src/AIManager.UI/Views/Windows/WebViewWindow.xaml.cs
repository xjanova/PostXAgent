using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;

namespace AIManager.UI.Views.Windows;

/// <summary>
/// Floating WebView Window for Web Learning
/// </summary>
public partial class WebViewWindow : Window
{
    private bool _isRecording;
    private readonly DispatcherTimer _recordingTimer;
    private DateTime _recordingStartTime;

    // Events for communication with parent page
    public event EventHandler<string>? NavigationCompleted;
    public event EventHandler<WebViewStepEventArgs>? StepRecorded;
    public event EventHandler? RecordingStarted;
    public event EventHandler? RecordingStopped;
    public event EventHandler? WindowClosing;

    // Recording Script
    private const string RecordingScript = @"
        (function() {
            if (window.__postXAgentRecorder) return;
            window.__postXAgentRecorder = true;

            function sendStep(action, element, value) {
                var selector = getSelector(element);
                var text = element.innerText?.substring(0, 100) || element.value?.substring(0, 100) || '';
                window.chrome.webview.postMessage(JSON.stringify({
                    type: 'step',
                    data: {
                        action: action,
                        selector: selector,
                        tagName: element.tagName,
                        text: text,
                        value: value || '',
                        url: window.location.href,
                        timestamp: Date.now()
                    }
                }));
            }

            function getSelector(element) {
                if (element.id) return '#' + element.id;
                if (element.getAttribute('data-testid')) return '[data-testid=""' + element.getAttribute('data-testid') + '""]';
                if (element.getAttribute('aria-label')) return '[aria-label=""' + element.getAttribute('aria-label') + '""]';
                if (element.name) return element.tagName.toLowerCase() + '[name=""' + element.name + '""]';

                var path = [];
                while (element && element.nodeType === Node.ELEMENT_NODE) {
                    var selector = element.tagName.toLowerCase();
                    if (element.className && typeof element.className === 'string') {
                        var classes = element.className.trim().split(/\s+/).filter(c => !c.match(/^[a-z]{20,}$/i));
                        if (classes.length > 0) {
                            selector += '.' + classes.slice(0, 2).join('.');
                        }
                    }
                    path.unshift(selector);
                    element = element.parentNode;
                    if (path.length > 4) break;
                }
                return path.join(' > ');
            }

            document.addEventListener('click', function(e) {
                var target = e.target.closest('button, a, [role=""button""], input[type=""submit""], input[type=""button""]');
                if (target) {
                    sendStep('click', target, '');
                }
            }, true);

            document.addEventListener('change', function(e) {
                if (e.target.matches('input, textarea, select')) {
                    sendStep('input', e.target, e.target.value);
                }
            }, true);

            document.addEventListener('submit', function(e) {
                sendStep('submit', e.target, '');
            }, true);

            console.log('PostXAgent Recorder initialized');
        })();
    ";

    public WebViewWindow()
    {
        InitializeComponent();

        _recordingTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _recordingTimer.Tick += RecordingTimer_Tick;

        InitializeWebView();
    }

    public WebViewWindow(string? initialUrl, string? platform) : this()
    {
        if (!string.IsNullOrEmpty(initialUrl))
        {
            UrlTextBox.Text = initialUrl;
        }

        if (!string.IsNullOrEmpty(platform))
        {
            Title = $"Web Learning - {platform}";
        }
    }

    private async void InitializeWebView()
    {
        try
        {
            await WebBrowser.EnsureCoreWebView2Async();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to initialize WebView2: {ex.Message}\n\nPlease install WebView2 Runtime.",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void WebBrowser_CoreWebView2InitializationCompleted(object? sender, CoreWebView2InitializationCompletedEventArgs e)
    {
        if (!e.IsSuccess)
        {
            MessageBox.Show("Failed to initialize WebView2. Please install WebView2 Runtime.",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        WebBrowser.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;
        WebBrowser.CoreWebView2.Settings.IsScriptEnabled = true;
        WebBrowser.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = true;
        WebBrowser.CoreWebView2.Settings.IsWebMessageEnabled = true;

        // Navigate to initial URL
        var url = UrlTextBox.Text.Trim();
        if (!string.IsNullOrEmpty(url))
        {
            if (!url.StartsWith("http://") && !url.StartsWith("https://"))
            {
                url = "https://" + url;
            }
            WebBrowser.CoreWebView2.Navigate(url);
        }
    }

    private void CoreWebView2_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            var json = e.WebMessageAsJson;
            var message = System.Text.Json.JsonSerializer.Deserialize<WebViewMessage>(json);

            if (message?.Type == "step" && message.Data != null && _isRecording)
            {
                StepRecorded?.Invoke(this, new WebViewStepEventArgs
                {
                    Action = message.Data.Action ?? "unknown",
                    Selector = message.Data.Selector ?? "",
                    TagName = message.Data.TagName ?? "",
                    Text = message.Data.Text ?? "",
                    Value = message.Data.Value ?? "",
                    Url = message.Data.Url ?? "",
                    Timestamp = message.Data.Timestamp
                });
            }
        }
        catch
        {
            // Ignore parse errors
        }
    }

    private void WebBrowser_NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        LoadingIndicator.Visibility = Visibility.Visible;
    }

    private void WebBrowser_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        LoadingIndicator.Visibility = Visibility.Collapsed;

        if (e.IsSuccess && WebBrowser.CoreWebView2 != null)
        {
            UrlTextBox.Text = WebBrowser.CoreWebView2.Source;
            TxtPageTitle.Text = WebBrowser.CoreWebView2.DocumentTitle;

            NavigationCompleted?.Invoke(this, WebBrowser.CoreWebView2.Source ?? "");

            // Re-inject recording script if recording
            if (_isRecording)
            {
                _ = InjectRecordingScriptAsync();
            }
        }
    }

    #region Recording

    public async Task StartRecordingAsync()
    {
        _isRecording = true;
        _recordingStartTime = DateTime.Now;

        RecordingStatusBorder.Visibility = Visibility.Visible;
        RecordingBorder.Visibility = Visibility.Visible;
        TxtRecordingTime.Text = "00:00";

        _recordingTimer.Start();

        await InjectRecordingScriptAsync();

        RecordingStarted?.Invoke(this, EventArgs.Empty);
    }

    public void StopRecording()
    {
        _isRecording = false;
        _recordingTimer.Stop();

        RecordingStatusBorder.Visibility = Visibility.Collapsed;
        RecordingBorder.Visibility = Visibility.Collapsed;

        RecordingStopped?.Invoke(this, EventArgs.Empty);
    }

    private async Task InjectRecordingScriptAsync()
    {
        if (WebBrowser.CoreWebView2 == null) return;

        try
        {
            await WebBrowser.CoreWebView2.ExecuteScriptAsync(RecordingScript);
        }
        catch
        {
            // Ignore injection errors
        }
    }

    private void RecordingTimer_Tick(object? sender, EventArgs e)
    {
        var elapsed = DateTime.Now - _recordingStartTime;
        TxtRecordingTime.Text = $"{elapsed.Minutes:D2}:{elapsed.Seconds:D2}";
    }

    public bool IsRecording => _isRecording;

    #endregion

    #region Navigation

    public void Navigate(string url)
    {
        if (WebBrowser.CoreWebView2 == null) return;

        if (!url.StartsWith("http://") && !url.StartsWith("https://"))
        {
            url = "https://" + url;
        }

        UrlTextBox.Text = url;
        WebBrowser.CoreWebView2.Navigate(url);
    }

    public string CurrentUrl => WebBrowser.CoreWebView2?.Source ?? "";
    public string CurrentTitle => WebBrowser.CoreWebView2?.DocumentTitle ?? "";

    public async Task<string> GetPageContextAsync()
    {
        if (WebBrowser.CoreWebView2 == null) return "";

        try
        {
            var script = @"
                (function() {
                    var buttons = document.querySelectorAll('button, [role=""button""]');
                    var inputs = document.querySelectorAll('input, textarea');
                    return JSON.stringify({
                        title: document.title,
                        url: window.location.href,
                        buttonCount: buttons.length,
                        inputCount: inputs.length
                    });
                })();
            ";
            var result = await WebBrowser.CoreWebView2.ExecuteScriptAsync(script);
            return result.Trim('"').Replace("\\\"", "\"");
        }
        catch
        {
            return "";
        }
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        if (WebBrowser.CoreWebView2?.CanGoBack == true)
        {
            WebBrowser.CoreWebView2.GoBack();
        }
    }

    private void ForwardButton_Click(object sender, RoutedEventArgs e)
    {
        if (WebBrowser.CoreWebView2?.CanGoForward == true)
        {
            WebBrowser.CoreWebView2.GoForward();
        }
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        WebBrowser.CoreWebView2?.Reload();
    }

    private void GoButton_Click(object sender, RoutedEventArgs e)
    {
        Navigate(UrlTextBox.Text);
    }

    private void UrlTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Navigate(UrlTextBox.Text);
        }
    }

    #endregion

    #region Window Controls

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            MaximizeButton_Click(sender, e);
        }
        else
        {
            DragMove();
        }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        if (WindowState == WindowState.Maximized)
        {
            WindowState = WindowState.Normal;
            MaximizeIcon.Kind = MaterialDesignThemes.Wpf.PackIconKind.WindowMaximize;
        }
        else
        {
            WindowState = WindowState.Maximized;
            MaximizeIcon.Kind = MaterialDesignThemes.Wpf.PackIconKind.WindowRestore;
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ToggleTopmost_Click(object sender, RoutedEventArgs e)
    {
        Topmost = ToggleTopmost.IsChecked == true;
    }

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_isRecording)
        {
            var result = MessageBox.Show(
                "Recording is in progress. Do you want to stop recording and close?",
                "Recording Active",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.No)
            {
                e.Cancel = true;
                return;
            }

            StopRecording();
        }

        WindowClosing?.Invoke(this, EventArgs.Empty);
    }

    #endregion
}

#region Event Args and Models

public class WebViewStepEventArgs : EventArgs
{
    public string Action { get; set; } = "";
    public string Selector { get; set; } = "";
    public string TagName { get; set; } = "";
    public string Text { get; set; } = "";
    public string Value { get; set; } = "";
    public string Url { get; set; } = "";
    public long Timestamp { get; set; }
}

public class WebViewMessage
{
    public string? Type { get; set; }
    public WebViewStepData? Data { get; set; }
}

public class WebViewStepData
{
    public string? Action { get; set; }
    public string? Selector { get; set; }
    public string? TagName { get; set; }
    public string? Text { get; set; }
    public string? Value { get; set; }
    public string? Url { get; set; }
    public long Timestamp { get; set; }
}

#endregion
