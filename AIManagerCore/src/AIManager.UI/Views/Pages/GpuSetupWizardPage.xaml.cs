using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using AIManager.Core.Models;
using AIManager.Core.Services;
using AIManager.Core.WebAutomation;
using AIManager.Core.WebAutomation.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Web.WebView2.Core;

namespace AIManager.UI.Views.Pages;

/// <summary>
/// GPU Setup Wizard Page with WebView and AI Guide
/// </summary>
public partial class GpuSetupWizardPage : Page
{
    private readonly GpuSetupGuideService _guideService;
    private readonly ColabGpuPoolService _poolService;
    private readonly GpuWorkflowRecordingService _recordingService;
    private readonly GpuAutoSignupService _autoSignupService;
    private readonly ObservableCollection<ChatMessage> _chatHistory = new();
    private readonly ObservableCollection<StepProgressItem> _stepsProgress = new();
    private bool _isWebViewInitialized;
    private GpuProviderType? _selectedProvider;
    private DispatcherTimer? _recordingBlinkTimer;

    public GpuSetupWizardPage()
    {
        InitializeComponent();

        // Get services from DI
        _guideService = App.Services.GetService<GpuSetupGuideService>() ?? new GpuSetupGuideService();
        _poolService = App.Services.GetService<ColabGpuPoolService>() ?? new ColabGpuPoolService();
        _recordingService = App.Services.GetService<GpuWorkflowRecordingService>() ?? new GpuWorkflowRecordingService();
        _autoSignupService = App.Services.GetService<GpuAutoSignupService>() ?? new GpuAutoSignupService();

        // Setup data bindings
        ChatHistory.ItemsSource = _chatHistory;
        StepsProgress.ItemsSource = _stepsProgress;

        // Load providers
        LoadProviders();

        // Subscribe to guide events
        _guideService.OnGuidanceMessage += OnGuidanceMessage;
        _guideService.OnStepCompleted += OnStepCompleted;
        _guideService.OnSetupCompleted += OnSetupCompleted;

        // Subscribe to recording events
        _recordingService.OnStepRecorded += OnStepRecorded;
        _recordingService.OnRecordingCompleted += OnRecordingCompleted;
        _recordingService.OnReplayStepExecuted += OnReplayStepExecuted;
        _recordingService.OnReplayCompleted += OnReplayCompleted;
        _recordingService.OnReplayError += OnReplayError;

        // Subscribe to auto signup events
        _autoSignupService.OnProgress += OnAutoSignupProgress;
        _autoSignupService.OnAccountCreated += OnAutoSignupAccountCreated;
        _autoSignupService.OnError += OnAutoSignupError;
        _autoSignupService.OnBatchCompleted += OnAutoSignupBatchCompleted;

        // Initialize WebView
        InitializeWebViewAsync();
    }

    #region Initialization

    private void LoadProviders()
    {
        var providers = GpuProviderInfo.GetAllProviders();
        ProvidersList.ItemsSource = providers;

        // Setup slider binding
        SliderAccountCount.ValueChanged += (s, e) =>
        {
            TxtAccountCount.Text = ((int)e.NewValue).ToString();
        };

        // Setup radio button events
        RbGmailPlus.Checked += (s, e) => TxtBaseEmail.Visibility = Visibility.Visible;
        RbGmailPlus.Unchecked += (s, e) => TxtBaseEmail.Visibility = Visibility.Collapsed;
    }

    private async void InitializeWebViewAsync()
    {
        try
        {
            // Initialize WebView2
            var env = await CoreWebView2Environment.CreateAsync();
            await WebView.EnsureCoreWebView2Async(env);

            // Setup event handlers
            WebView.NavigationStarting += WebView_NavigationStarting;
            WebView.NavigationCompleted += WebView_NavigationCompleted;
            WebView.SourceChanged += WebView_SourceChanged;

            // Setup message handler for recording
            WebView.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;

            _isWebViewInitialized = true;
        }
        catch (Exception ex)
        {
            AddChatMessage("Error", $"Failed to initialize WebView: {ex.Message}", isError: true);
        }
    }

    private void CoreWebView2_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        if (!_recordingService.IsRecording) return;

        try
        {
            var message = e.WebMessageAsJson;
            var actionData = System.Text.Json.JsonSerializer.Deserialize<RecordedActionMessage>(message);

            if (actionData == null) return;

            var element = new RecordedElement
            {
                TagName = actionData.TagName ?? "",
                Id = actionData.Id ?? "",
                ClassName = actionData.ClassName ?? "",
                Name = actionData.Name ?? "",
                Placeholder = actionData.Placeholder ?? "",
                TextContent = actionData.TextContent ?? "",
                XPath = actionData.XPath ?? "",
                CssSelector = actionData.CssSelector ?? ""
            };

            // Record based on action type
            switch (actionData.Action?.ToLower())
            {
                case "click":
                    _recordingService.RecordClick(element, WebView.Source?.ToString() ?? "");
                    break;

                case "input":
                case "type":
                    _recordingService.RecordType(element, actionData.Value ?? "", WebView.Source?.ToString() ?? "");
                    break;

                case "select":
                    _recordingService.RecordSelect(element, actionData.Value ?? "", WebView.Source?.ToString() ?? "");
                    break;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error processing web message: {ex.Message}");
        }
    }

    #endregion

    #region Provider Selection

    private void ProviderButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is GpuProviderType providerType)
        {
            StartSetup(providerType);
        }
    }

    private void StartSetup(GpuProviderType provider)
    {
        _selectedProvider = provider;

        // Start guide session
        var session = _guideService.StartSetup(provider);

        // Update UI
        var providerInfo = GpuProviderInfo.GetAllProviders()
            .FirstOrDefault(p => p.Type == provider);

        TxtProviderName.Text = $"Setting up: {providerInfo?.Name ?? provider.ToString()}";

        // Update steps progress
        UpdateStepsProgress();

        // Navigate to signup URL
        var flow = ProviderSetupFlow.GetSetupFlow(provider);
        if (flow.Steps.Any() && !string.IsNullOrEmpty(flow.Steps[0].Url))
        {
            NavigateToUrl(flow.Steps[0].Url);
        }

        // Show guidance card and action buttons
        CurrentGuidanceCard.Visibility = Visibility.Visible;
        BtnNext.Visibility = Visibility.Visible;
    }

    private void UpdateStepsProgress()
    {
        _stepsProgress.Clear();

        if (_selectedProvider == null) return;

        var flow = ProviderSetupFlow.GetSetupFlow(_selectedProvider.Value);
        var currentStep = _guideService.GetCurrentStep();

        foreach (var step in flow.Steps)
        {
            var isCompleted = _guideService.GetActiveSession()?.CompletedSteps.Contains(step.StepNumber) == true;
            var isCurrent = step.StepNumber == currentStep?.StepNumber;

            _stepsProgress.Add(new StepProgressItem
            {
                StepNumber = step.StepNumber.ToString(),
                Title = step.Title,
                IsCompleted = isCompleted,
                IsCurrent = isCurrent,
                Background = isCompleted ? Brushes.Green : (isCurrent ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#7C3AED")) : Brushes.Gray),
                BorderBrush = isCurrent ? Brushes.White : Brushes.Transparent,
                Foreground = isCurrent || isCompleted ? Brushes.White : new SolidColorBrush(Colors.Gray)
            });
        }
    }

    #endregion

    #region WebView Navigation

    private void NavigateToUrl(string url)
    {
        if (!_isWebViewInitialized) return;

        try
        {
            LoadingOverlay.Visibility = Visibility.Visible;
            WebView.Source = new Uri(url);
            TxtUrl.Text = url;
        }
        catch (Exception ex)
        {
            AddChatMessage("Error", $"Failed to navigate: {ex.Message}", isError: true);
        }
    }

    private void WebView_NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        LoadingOverlay.Visibility = Visibility.Visible;
    }

    private async void WebView_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        LoadingOverlay.Visibility = Visibility.Collapsed;

        if (e.IsSuccess)
        {
            // Inject recording script if recording
            if (_recordingService.IsRecording)
            {
                await InjectRecordingScriptAsync();

                // Record navigation
                _recordingService.RecordNavigate(WebView.Source?.ToString() ?? "");
            }

            // Auto-analyze page if in setup mode
            if (_guideService.IsSetupInProgress)
            {
                await AnalyzeCurrentPage();
            }
        }
        else
        {
            AddChatMessage("Navigation Error", $"Failed to load page: {e.WebErrorStatus}", isError: true);
        }
    }

    private async Task InjectRecordingScriptAsync()
    {
        var script = @"
            (function() {
                if (window.__postxRecorderInjected) return;
                window.__postxRecorderInjected = true;

                function getXPath(el) {
                    if (!el) return '';
                    if (el.id) return '//*[@id=""' + el.id + '""]';
                    if (el === document.body) return '/html/body';

                    var ix = 0;
                    var siblings = el.parentNode ? el.parentNode.childNodes : [];
                    for (var i = 0; i < siblings.length; i++) {
                        var sibling = siblings[i];
                        if (sibling === el) {
                            var parentPath = getXPath(el.parentNode);
                            return parentPath + '/' + el.tagName.toLowerCase() + '[' + (ix + 1) + ']';
                        }
                        if (sibling.nodeType === 1 && sibling.tagName === el.tagName) ix++;
                    }
                    return '';
                }

                function getCssSelector(el) {
                    if (el.id) return '#' + el.id;
                    if (el.className) {
                        var classes = el.className.split(' ').filter(c => c).slice(0, 2).join('.');
                        if (classes) return el.tagName.toLowerCase() + '.' + classes;
                    }
                    return el.tagName.toLowerCase();
                }

                function getElementInfo(el) {
                    return {
                        tagName: el.tagName.toLowerCase(),
                        id: el.id || '',
                        className: el.className || '',
                        name: el.name || '',
                        placeholder: el.placeholder || '',
                        textContent: (el.textContent || '').substring(0, 100).trim(),
                        xPath: getXPath(el),
                        cssSelector: getCssSelector(el)
                    };
                }

                // Click handler
                document.addEventListener('click', function(e) {
                    var info = getElementInfo(e.target);
                    info.action = 'click';
                    window.chrome.webview.postMessage(JSON.stringify(info));
                }, true);

                // Input handler (debounced)
                var inputTimeout = null;
                document.addEventListener('input', function(e) {
                    clearTimeout(inputTimeout);
                    inputTimeout = setTimeout(function() {
                        var info = getElementInfo(e.target);
                        info.action = 'input';
                        info.value = e.target.value || '';
                        window.chrome.webview.postMessage(JSON.stringify(info));
                    }, 500);
                }, true);

                // Select handler
                document.addEventListener('change', function(e) {
                    if (e.target.tagName.toLowerCase() === 'select') {
                        var info = getElementInfo(e.target);
                        info.action = 'select';
                        info.value = e.target.value || '';
                        window.chrome.webview.postMessage(JSON.stringify(info));
                    }
                }, true);

                console.log('PostX Recorder: Script injected');
            })();
        ";

        try
        {
            await WebView.ExecuteScriptAsync(script);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to inject recording script: {ex.Message}");
        }
    }

    private void WebView_SourceChanged(object? sender, CoreWebView2SourceChangedEventArgs e)
    {
        if (_isWebViewInitialized && WebView.Source != null)
        {
            TxtUrl.Text = WebView.Source.ToString();
        }
    }

    private void BtnBack_Click(object sender, RoutedEventArgs e)
    {
        if (_isWebViewInitialized && WebView.CanGoBack)
        {
            WebView.GoBack();
        }
    }

    private void BtnRefresh_Click(object sender, RoutedEventArgs e)
    {
        if (_isWebViewInitialized)
        {
            WebView.Reload();
        }
    }

    private void BtnGo_Click(object sender, RoutedEventArgs e)
    {
        NavigateFromUrlBar();
    }

    private void TxtUrl_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            NavigateFromUrlBar();
        }
    }

    private void NavigateFromUrlBar()
    {
        var url = TxtUrl.Text.Trim();

        if (string.IsNullOrEmpty(url)) return;

        // Add protocol if missing
        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            url = "https://" + url;
        }

        NavigateToUrl(url);
    }

    #endregion

    #region Page Analysis

    private async void BtnAnalyze_Click(object sender, RoutedEventArgs e)
    {
        await AnalyzeCurrentPage();
    }

    private async Task AnalyzeCurrentPage()
    {
        if (!_isWebViewInitialized || WebView.Source == null) return;

        try
        {
            // Get page content
            var content = await WebView.ExecuteScriptAsync("document.body.innerText");
            content = System.Text.RegularExpressions.Regex.Unescape(content);

            // Analyze with AI
            var result = await _guideService.AnalyzePageAsync(
                WebView.Source.ToString(),
                content);

            // Update guidance UI
            UpdateGuidanceFromAnalysis(result);
        }
        catch (Exception ex)
        {
            AddChatMessage("Analysis Error", ex.Message, isError: true);
        }
    }

    private void UpdateGuidanceFromAnalysis(PageAnalysisResult result)
    {
        GuidanceTitle.Text = result.CurrentStep;
        GuidanceMessage.Text = result.NextAction;

        if (result.Tips.Any())
        {
            GuidanceTips.ItemsSource = result.Tips;
            GuidanceTips.Visibility = Visibility.Visible;
        }
        else
        {
            GuidanceTips.Visibility = Visibility.Collapsed;
        }

        // Update icon based on login status
        if (result.IsLoggedIn)
        {
            GuidanceIcon.Kind = MaterialDesignThemes.Wpf.PackIconKind.CheckCircleOutline;
            GuidanceIcon.Foreground = Brushes.Green;
        }
        else
        {
            GuidanceIcon.Kind = MaterialDesignThemes.Wpf.PackIconKind.InformationOutline;
            GuidanceIcon.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#7C3AED"));
        }

        CurrentGuidanceCard.Visibility = Visibility.Visible;
    }

    #endregion

    #region Guidance Events

    private void OnGuidanceMessage(SetupGuidanceMessage message)
    {
        Dispatcher.Invoke(() =>
        {
            // Update guidance card
            GuidanceTitle.Text = message.Title;
            GuidanceMessage.Text = message.Message;

            if (message.Tips.Any())
            {
                GuidanceTips.ItemsSource = message.Tips;
                GuidanceTips.Visibility = Visibility.Visible;
            }

            // Set icon based on type
            (GuidanceIcon.Kind, GuidanceIcon.Foreground) = message.Type switch
            {
                SetupGuidanceType.Success => (MaterialDesignThemes.Wpf.PackIconKind.CheckCircle, Brushes.Green),
                SetupGuidanceType.Warning => (MaterialDesignThemes.Wpf.PackIconKind.AlertCircle, Brushes.Orange),
                SetupGuidanceType.Error => (MaterialDesignThemes.Wpf.PackIconKind.CloseCircle, Brushes.Red),
                _ => (MaterialDesignThemes.Wpf.PackIconKind.InformationOutline, new SolidColorBrush((Color)ColorConverter.ConvertFromString("#7C3AED")))
            };

            CurrentGuidanceCard.Visibility = Visibility.Visible;

            // Add to chat history
            AddChatMessage(message.Title, message.Message,
                message.Type == SetupGuidanceType.Error,
                message.Type == SetupGuidanceType.Success);

            // Navigate if URL provided
            if (!string.IsNullOrEmpty(message.ActionUrl))
            {
                NavigateToUrl(message.ActionUrl);
            }
        });
    }

    private void OnStepCompleted(SetupStep step)
    {
        Dispatcher.Invoke(() =>
        {
            UpdateStepsProgress();
            AddChatMessage($"Step {step.StepNumber}", $"{step.Title} - Completed!", isSuccess: true);
        });
    }

    private void OnSetupCompleted(GpuProviderType provider)
    {
        Dispatcher.Invoke(() =>
        {
            // Show complete button
            BtnNext.Visibility = Visibility.Collapsed;
            BtnComplete.Visibility = Visibility.Visible;

            AddChatMessage("Setup Complete",
                $"Successfully configured {provider}! Click 'Complete' to add this account to your GPU pool.",
                isSuccess: true);
        });
    }

    #endregion

    #region Action Buttons

    private void BtnNext_Click(object sender, RoutedEventArgs e)
    {
        var nextStep = _guideService.MoveToNextStep();

        if (nextStep != null)
        {
            UpdateStepsProgress();

            // Show skip button for optional steps
            BtnSkip.Visibility = nextStep.IsOptional ? Visibility.Visible : Visibility.Collapsed;

            // Navigate to step URL if available
            if (!string.IsNullOrEmpty(nextStep.Url))
            {
                NavigateToUrl(nextStep.Url);
            }
        }
    }

    private void BtnSkip_Click(object sender, RoutedEventArgs e)
    {
        var nextStep = _guideService.SkipCurrentStep();
        if (nextStep != null)
        {
            UpdateStepsProgress();
            BtnSkip.Visibility = nextStep.IsOptional ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private async void BtnComplete_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedProvider == null) return;

        // Get account email from session or prompt
        var email = await PromptForAccountEmail();
        if (string.IsNullOrEmpty(email)) return;

        try
        {
            // Create account based on provider type
            var account = new ColabGpuAccount
            {
                Email = email,
                DisplayName = email.Split('@')[0],
                Tier = ColabTier.Free,
                Status = ColabAccountStatus.Active,
                Priority = 100
            };

            await _poolService.AddAccountAsync(account);

            AddChatMessage("Account Added",
                $"Account {email} has been added to your GPU pool!",
                isSuccess: true);

            // Reset wizard
            ResetWizard();
        }
        catch (Exception ex)
        {
            AddChatMessage("Error", $"Failed to add account: {ex.Message}", isError: true);
        }
    }

    private async Task<string?> PromptForAccountEmail()
    {
        // Simple dialog for email input
        var dialog = new MaterialDesignThemes.Wpf.DialogHost();

        // For simplicity, return a placeholder - in real implementation, show a dialog
        var result = Microsoft.VisualBasic.Interaction.InputBox(
            "Enter the email address for this GPU account:",
            "Add Account",
            "");

        return string.IsNullOrWhiteSpace(result) ? null : result;
    }

    private void ResetWizard()
    {
        _guideService.CancelSetup();
        _selectedProvider = null;
        _stepsProgress.Clear();
        _chatHistory.Clear();

        TxtProviderName.Text = "Select a GPU Provider to start";
        CurrentGuidanceCard.Visibility = Visibility.Collapsed;
        BtnNext.Visibility = Visibility.Collapsed;
        BtnSkip.Visibility = Visibility.Collapsed;
        BtnComplete.Visibility = Visibility.Collapsed;

        // Navigate to blank page
        if (_isWebViewInitialized)
        {
            WebView.Source = new Uri("about:blank");
        }
    }

    #endregion

    #region Chat History

    private void AddChatMessage(string title, string message, bool isError = false, bool isSuccess = false)
    {
        var backgroundColor = isError
            ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFEBEE"))
            : isSuccess
                ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E8F5E9"))
                : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F3E5F5"));

        _chatHistory.Add(new ChatMessage
        {
            Title = title,
            Message = message,
            Background = backgroundColor,
            Timestamp = DateTime.Now
        });
    }

    #endregion

    #region Auto Signup

    private async void BtnStartAutoSignup_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedProvider == null)
        {
            AddChatMessage("Auto Signup", "Please select a GPU provider first.", isError: true);
            return;
        }

        // Get workflow for this provider
        var workflow = await _recordingService.FindWorkflowForProviderAsync(_selectedProvider.Value);
        if (workflow == null)
        {
            AddChatMessage("No Workflow",
                $"No saved workflow for {_selectedProvider.Value}. Please record one first using the Record button.",
                isError: true);
            return;
        }

        // Build config
        var config = new AutoSignupConfig
        {
            UseTemporaryEmail = RbTempEmail.IsChecked == true,
            BaseEmail = RbGmailPlus.IsChecked == true ? TxtBaseEmail.Text : null,
            RequiresEmailVerification = true,
            VerificationTimeout = TimeSpan.FromMinutes(5),
            DelayBetweenSignups = TimeSpan.FromSeconds(30)
        };

        var accountCount = (int)SliderAccountCount.Value;

        // Update UI
        BtnStartAutoSignup.IsEnabled = false;
        BtnCancelAutoSignup.Visibility = Visibility.Visible;
        AutoSignupProgress.Visibility = Visibility.Visible;
        TxtAutoSignupCount.Text = $"0/{accountCount}";
        ProgressAutoSignup.Maximum = accountCount;
        ProgressAutoSignup.Value = 0;

        AddChatMessage("Auto Signup Started",
            $"Creating {accountCount} accounts for {_selectedProvider.Value}...",
            isSuccess: true);

        try
        {
            var result = await _autoSignupService.StartBatchSignupAsync(
                _selectedProvider.Value,
                accountCount,
                config,
                async context => await ExecuteSignupWorkflowAsync(context, workflow));

            // Show results
            AddChatMessage("Auto Signup Complete",
                $"Created {result.SuccessCount} accounts ({result.SuccessRate:P0} success rate)",
                isSuccess: result.SuccessCount > 0);
        }
        catch (Exception ex)
        {
            AddChatMessage("Auto Signup Error", ex.Message, isError: true);
        }
        finally
        {
            BtnStartAutoSignup.IsEnabled = true;
            BtnCancelAutoSignup.Visibility = Visibility.Collapsed;
            AutoSignupProgress.Visibility = Visibility.Collapsed;
        }
    }

    private void BtnCancelAutoSignup_Click(object sender, RoutedEventArgs e)
    {
        _autoSignupService.CancelSignup();
        AddChatMessage("Cancelled", "Auto signup was cancelled.");
    }

    private async Task<bool> ExecuteSignupWorkflowAsync(
        AutoSignupContext context,
        LearnedWorkflow workflow)
    {
        // Replace workflow variables with context values
        foreach (var step in workflow.Steps)
        {
            // Replace email placeholder
            if (step.InputVariable == "{{email}}" || step.InputVariable == "{{content.email}}")
            {
                step.InputValue = context.Email;
            }
            // Replace password placeholder
            else if (step.InputVariable == "{{password}}" || step.InputVariable == "{{content.password}}")
            {
                step.InputValue = context.Password;
            }
            // Replace name placeholder
            else if (step.InputVariable == "{{name}}" || step.InputVariable == "{{content.name}}")
            {
                step.InputValue = context.DisplayName;
            }
        }

        // Execute each step
        foreach (var step in workflow.Steps)
        {
            var success = await ExecuteWorkflowStepAsync(step);
            if (!success) return false;

            // Check if we need verification code
            if (step.Description?.Contains("verification", StringComparison.OrdinalIgnoreCase) == true)
            {
                if (context.GetVerificationCode != null)
                {
                    var code = await context.GetVerificationCode();
                    if (!string.IsNullOrEmpty(code))
                    {
                        // Find and fill verification input
                        await WebView.ExecuteScriptAsync($@"
                            (function() {{
                                var inputs = document.querySelectorAll('input[type=""text""], input[type=""number""]');
                                for (var i of inputs) {{
                                    if (i.placeholder && (i.placeholder.toLowerCase().includes('code') ||
                                        i.placeholder.toLowerCase().includes('verify'))) {{
                                        i.value = '{code}';
                                        i.dispatchEvent(new Event('input', {{bubbles: true}}));
                                        return true;
                                    }}
                                }}
                                return false;
                            }})();
                        ");
                    }
                }
            }

            await Task.Delay(500);
        }

        return true;
    }

    // Auto Signup Event Handlers
    private void OnAutoSignupProgress(AutoSignupProgress progress)
    {
        Dispatcher.Invoke(() =>
        {
            TxtAutoSignupStatus.Text = progress.Message;
            TxtAutoSignupCount.Text = $"{progress.CurrentAccount}/{progress.TotalAccounts}";
            ProgressAutoSignup.Value = progress.CurrentAccount;
        });
    }

    private void OnAutoSignupAccountCreated(AutoSignupResult result)
    {
        Dispatcher.Invoke(() =>
        {
            AddChatMessage("Account Created",
                $"{result.Email} - {result.DisplayName}",
                isSuccess: true);
        });
    }

    private void OnAutoSignupError(string error)
    {
        Dispatcher.Invoke(() =>
        {
            AddChatMessage("Error", error, isError: true);
        });
    }

    private void OnAutoSignupBatchCompleted(AutoSignupBatchResult result)
    {
        Dispatcher.Invoke(() =>
        {
            var message = result.SuccessCount > 0
                ? $"Successfully created {result.SuccessCount} accounts. Failed: {result.FailureCount}"
                : "No accounts were created.";

            AddChatMessage("Batch Complete", message, isSuccess: result.SuccessCount > 0);
        });
    }

    #endregion

    #region Workflow Recording

    private void BtnStartRecording_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedProvider == null)
        {
            AddChatMessage("Recording", "Please select a GPU provider first before recording.", isError: true);
            return;
        }

        try
        {
            _recordingService.StartRecording(_selectedProvider.Value);

            // Update UI
            BtnStartRecording.Visibility = Visibility.Collapsed;
            BtnStopRecording.Visibility = Visibility.Visible;
            BtnReplay.IsEnabled = false;

            // Start blinking indicator
            StartRecordingBlink();

            TxtRecordingStatus.Text = "Recording...";
            TxtStepsCount.Text = "(0 steps)";

            AddChatMessage("Recording Started",
                $"Recording workflow for {_selectedProvider.Value}. Perform your actions in the browser.",
                isSuccess: true);
        }
        catch (Exception ex)
        {
            AddChatMessage("Recording Error", ex.Message, isError: true);
        }
    }

    private async void BtnStopRecording_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            StopRecordingBlink();

            var workflow = await _recordingService.StopRecordingAsync();

            // Update UI
            BtnStopRecording.Visibility = Visibility.Collapsed;
            BtnStartRecording.Visibility = Visibility.Visible;
            BtnReplay.IsEnabled = true;

            TxtRecordingStatus.Text = "Not Recording";
            RecordingIndicator.Visibility = Visibility.Collapsed;

            if (workflow != null)
            {
                AddChatMessage("Recording Saved",
                    $"Workflow '{workflow.Name}' saved with {workflow.Steps.Count} steps.",
                    isSuccess: true);
            }
            else
            {
                AddChatMessage("Recording Cancelled", "No steps were recorded.", isError: false);
            }
        }
        catch (Exception ex)
        {
            AddChatMessage("Recording Error", $"Failed to save: {ex.Message}", isError: true);
        }
    }

    private async void BtnReplay_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedProvider == null)
        {
            AddChatMessage("Replay", "Please select a GPU provider first.", isError: true);
            return;
        }

        try
        {
            var workflow = await _recordingService.FindWorkflowForProviderAsync(_selectedProvider.Value);

            if (workflow == null)
            {
                AddChatMessage("No Workflow Found",
                    $"No saved workflow found for {_selectedProvider.Value}. Record one first.",
                    isError: true);
                return;
            }

            // Confirm replay
            var result = MessageBox.Show(
                $"Replay workflow '{workflow.Name}' with {workflow.Steps.Count} steps?\n\n" +
                $"Success rate: {workflow.GetSuccessRate():P0}",
                "Confirm Replay",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            // Disable buttons during replay
            BtnStartRecording.IsEnabled = false;
            BtnReplay.IsEnabled = false;

            AddChatMessage("Replay Started",
                $"Replaying '{workflow.Name}'...",
                isSuccess: true);

            // Execute replay
            var success = await _recordingService.ReplayWorkflowAsync(
                workflow,
                async step => await ExecuteWorkflowStepAsync(step));

            // Re-enable buttons
            BtnStartRecording.IsEnabled = true;
            BtnReplay.IsEnabled = true;

            if (success)
            {
                AddChatMessage("Replay Complete",
                    "Workflow executed successfully!",
                    isSuccess: true);
            }
        }
        catch (Exception ex)
        {
            AddChatMessage("Replay Error", ex.Message, isError: true);
            BtnStartRecording.IsEnabled = true;
            BtnReplay.IsEnabled = true;
        }
    }

    private async void BtnManageWorkflows_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var workflows = await _recordingService.GetAllGpuWorkflowsAsync();

            if (!workflows.Any())
            {
                MessageBox.Show(
                    "No saved workflows found.\nRecord some workflows first.",
                    "Manage Workflows",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            // Show simple list
            var listText = string.Join("\n", workflows.Select(w =>
                $"- {w.Name} ({w.Steps.Count} steps, {w.SuccessCount} successes)"));

            var result = MessageBox.Show(
                $"Saved Workflows:\n\n{listText}\n\nDelete all workflows?",
                "Manage Workflows",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                foreach (var workflow in workflows)
                {
                    await _recordingService.DeleteWorkflowAsync(workflow.Id);
                }
                AddChatMessage("Workflows Deleted", "All workflows have been deleted.", isSuccess: true);
            }
        }
        catch (Exception ex)
        {
            AddChatMessage("Error", ex.Message, isError: true);
        }
    }

    private async Task<bool> ExecuteWorkflowStepAsync(AIManager.Core.WebAutomation.Models.WorkflowStep step)
    {
        if (!_isWebViewInitialized) return false;

        try
        {
            switch (step.Action)
            {
                case StepAction.Navigate:
                    if (!string.IsNullOrEmpty(step.InputValue))
                    {
                        NavigateToUrl(step.InputValue);
                        await Task.Delay(2000); // Wait for navigation
                    }
                    break;

                case StepAction.Click:
                    await ExecuteClickAsync(step.Selector);
                    break;

                case StepAction.Type:
                    await ExecuteTypeAsync(step.Selector, step.InputValue ?? "");
                    break;

                case StepAction.Wait:
                    var waitTime = int.TryParse(step.InputValue, out var ms) ? ms : 1000;
                    await Task.Delay(waitTime);
                    break;

                default:
                    // Log unsupported action but continue
                    AddChatMessage("Warning", $"Unsupported action: {step.Action}");
                    break;
            }

            return true;
        }
        catch (Exception ex)
        {
            AddChatMessage("Step Failed", $"{step.Description}: {ex.Message}", isError: true);
            return false;
        }
    }

    private async Task ExecuteClickAsync(ElementSelector selector)
    {
        var script = GenerateClickScript(selector);
        await WebView.ExecuteScriptAsync(script);
    }

    private async Task ExecuteTypeAsync(ElementSelector selector, string value)
    {
        var script = GenerateTypeScript(selector, value);
        await WebView.ExecuteScriptAsync(script);
    }

    private string GenerateClickScript(ElementSelector selector)
    {
        var findElementCode = selector.Type switch
        {
            SelectorType.Id => $"document.getElementById('{EscapeJs(selector.Value)}')",
            SelectorType.CSS => $"document.querySelector('{EscapeJs(selector.Value)}')",
            SelectorType.XPath => $"document.evaluate('{EscapeJs(selector.Value)}', document, null, XPathResult.FIRST_ORDERED_NODE_TYPE, null).singleNodeValue",
            SelectorType.Name => $"document.querySelector('[name=\"{EscapeJs(selector.Value)}\"]')",
            SelectorType.Text => $"Array.from(document.querySelectorAll('*')).find(el => el.textContent.trim() === '{EscapeJs(selector.TextContent ?? selector.Value)}')",
            _ => $"document.querySelector('{EscapeJs(selector.Value)}')"
        };

        return $@"
            (function() {{
                var el = {findElementCode};
                if (el) {{
                    el.click();
                    return true;
                }}
                return false;
            }})();
        ";
    }

    private string GenerateTypeScript(ElementSelector selector, string value)
    {
        var findElementCode = selector.Type switch
        {
            SelectorType.Id => $"document.getElementById('{EscapeJs(selector.Value)}')",
            SelectorType.CSS => $"document.querySelector('{EscapeJs(selector.Value)}')",
            SelectorType.Name => $"document.querySelector('[name=\"{EscapeJs(selector.Value)}\"]')",
            SelectorType.Placeholder => $"document.querySelector('[placeholder=\"{EscapeJs(selector.Value)}\"]')",
            _ => $"document.querySelector('{EscapeJs(selector.Value)}')"
        };

        return $@"
            (function() {{
                var el = {findElementCode};
                if (el) {{
                    el.focus();
                    el.value = '{EscapeJs(value)}';
                    el.dispatchEvent(new Event('input', {{ bubbles: true }}));
                    el.dispatchEvent(new Event('change', {{ bubbles: true }}));
                    return true;
                }}
                return false;
            }})();
        ";
    }

    private static string EscapeJs(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace("'", "\\'")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r");
    }

    private void StartRecordingBlink()
    {
        RecordingIndicator.Visibility = Visibility.Visible;

        _recordingBlinkTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };

        var visible = true;
        _recordingBlinkTimer.Tick += (s, e) =>
        {
            RecordingIndicator.Opacity = visible ? 1.0 : 0.3;
            visible = !visible;
        };

        _recordingBlinkTimer.Start();
    }

    private void StopRecordingBlink()
    {
        _recordingBlinkTimer?.Stop();
        _recordingBlinkTimer = null;
        RecordingIndicator.Opacity = 1.0;
    }

    // Recording event handlers
    private void OnStepRecorded(RecordedStep step)
    {
        Dispatcher.Invoke(() =>
        {
            TxtStepsCount.Text = $"({_recordingService.RecordedStepsCount} steps)";
        });
    }

    private void OnRecordingCompleted(LearnedWorkflow workflow)
    {
        Dispatcher.Invoke(() =>
        {
            TxtRecordingStatus.Text = "Not Recording";
        });
    }

    private void OnReplayStepExecuted(AIManager.Core.WebAutomation.Models.WorkflowStep step, int current, int total)
    {
        Dispatcher.Invoke(() =>
        {
            TxtRecordingStatus.Text = $"Replay: Step {current}/{total}";
            AddChatMessage($"Step {current}/{total}", step.Description ?? step.Action.ToString());
        });
    }

    private void OnReplayCompleted()
    {
        Dispatcher.Invoke(() =>
        {
            TxtRecordingStatus.Text = "Replay Complete";
        });
    }

    private void OnReplayError(string error)
    {
        Dispatcher.Invoke(() =>
        {
            AddChatMessage("Replay Error", error, isError: true);
        });
    }

    #endregion
}

#region Supporting Classes

public class ChatMessage
{
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Brush Background { get; set; } = Brushes.White;
    public DateTime Timestamp { get; set; }
}

public class StepProgressItem
{
    public string StepNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
    public bool IsCurrent { get; set; }
    public Brush Background { get; set; } = Brushes.Gray;
    public Brush BorderBrush { get; set; } = Brushes.Transparent;
    public Brush Foreground { get; set; } = Brushes.White;
}

public class RecordedActionMessage
{
    public string? Action { get; set; }
    public string? TagName { get; set; }
    public string? Id { get; set; }
    public string? ClassName { get; set; }
    public string? Name { get; set; }
    public string? Placeholder { get; set; }
    public string? TextContent { get; set; }
    public string? Value { get; set; }
    public string? XPath { get; set; }
    public string? CssSelector { get; set; }
}

#endregion
