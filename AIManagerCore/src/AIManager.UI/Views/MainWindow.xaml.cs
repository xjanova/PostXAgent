using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using AIManager.Core.Orchestrator;
using AIManager.UI.Views.Pages;

namespace AIManager.UI.Views;

public partial class MainWindow : Window
{
    private readonly ProcessOrchestrator _orchestrator;
    private readonly DispatcherTimer _clockTimer;
    private Button? _selectedNavButton;

    public MainWindow()
    {
        InitializeComponent();

        _orchestrator = App.Services.GetRequiredService<ProcessOrchestrator>();

        // Setup clock
        _clockTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _clockTimer.Tick += (s, e) => ClockDisplay.Text = DateTime.Now.ToString("HH:mm:ss");
        _clockTimer.Start();

        // Subscribe to orchestrator events
        _orchestrator.StatsUpdated += OnStatsUpdated;
        _orchestrator.TaskCompleted += OnTaskCompleted;
        _orchestrator.TaskFailed += OnTaskFailed;
        _orchestrator.WorkerStatusChanged += OnWorkerStatusChanged;

        // Navigate to dashboard
        NavigateTo("Dashboard");
        UpdateServerStatus(false);

        Closed += (s, e) =>
        {
            _clockTimer.Stop();
            _orchestrator.Dispose();
        };
    }

    private void NavButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string page)
        {
            NavigateTo(page);
            UpdateSelectedNav(button);
        }
    }

    private void NavigateTo(string pageName)
    {
        PageTitle.Text = pageName;

        Page? page = pageName switch
        {
            "Dashboard" => new DashboardPage(_orchestrator),
            "Workers" => new WorkersPage(_orchestrator),
            "Tasks" => new TasksPage(_orchestrator),
            "Platforms" => new PlatformsPage(),
            "AIProviders" => new AIProvidersPage(),
            "Logs" => new LogsPage(),
            "Settings" => new SettingsPage(),
            _ => new DashboardPage(_orchestrator)
        };

        ContentFrame.Navigate(page);
    }

    private void UpdateSelectedNav(Button button)
    {
        if (_selectedNavButton != null)
        {
            _selectedNavButton.Background = Brushes.Transparent;
        }

        button.Background = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255));
        _selectedNavButton = button;
    }

    private async void StartButton_Click(object sender, RoutedEventArgs e)
    {
        BtnStart.IsEnabled = false;
        StatusText.Text = "Starting...";

        try
        {
            await _orchestrator.StartAsync();
            UpdateServerStatus(true);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to start: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
            UpdateServerStatus(false);
        }

        BtnStart.IsEnabled = true;
    }

    private async void StopButton_Click(object sender, RoutedEventArgs e)
    {
        BtnStop.IsEnabled = false;
        StatusText.Text = "Stopping...";

        try
        {
            await _orchestrator.StopAsync();
            UpdateServerStatus(false);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to stop: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }

        BtnStop.IsEnabled = true;
    }

    private void UpdateServerStatus(bool isRunning)
    {
        Dispatcher.Invoke(() =>
        {
            if (isRunning)
            {
                StatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(76, 175, 80)); // Green
                StatusText.Text = "Server Running";
                BtnStart.Visibility = Visibility.Collapsed;
                BtnStop.Visibility = Visibility.Visible;
            }
            else
            {
                StatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(244, 67, 54)); // Red
                StatusText.Text = "Server Stopped";
                BtnStart.Visibility = Visibility.Visible;
                BtnStop.Visibility = Visibility.Collapsed;
            }
        });
    }

    private void OnStatsUpdated(object? sender, StatsEventArgs e)
    {
        // Update will be handled by individual pages
    }

    private void OnTaskCompleted(object? sender, TaskEventArgs e)
    {
        // Show notification or update UI
    }

    private void OnTaskFailed(object? sender, TaskEventArgs e)
    {
        // Show error notification
    }

    private void OnWorkerStatusChanged(object? sender, WorkerEventArgs e)
    {
        // Update worker status display
    }
}
