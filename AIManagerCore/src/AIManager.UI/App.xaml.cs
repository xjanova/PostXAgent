using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using AIManager.Core.Orchestrator;
using AIManager.Core.Models;
using AIManager.Core.Services;
using AIManager.UI.ViewModels;
using AIManager.UI.Views;
using Microsoft.Extensions.Logging;

namespace AIManager.UI;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;
    private OllamaServiceManager? _ollamaService;
    private static readonly DebugLogger _logger = DebugLogger.Instance;
    private Views.SplashScreen? _splashScreen;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Setup global exception handlers
        SetupExceptionHandlers();

        // Show splash screen first
        _splashScreen = new Views.SplashScreen();
        _splashScreen.Show();

        _logger.LogInfo("App", "═══════════════════════════════════════════════════════");
        _logger.LogInfo("App", "PostXAgent AI Manager starting...");
        _logger.LogInfo("App", $"Version: 1.0.0");
        _logger.LogInfo("App", $"Working Directory: {Environment.CurrentDirectory}");
        _logger.LogInfo("App", "═══════════════════════════════════════════════════════");

        try
        {
            // Initialize with real progress tracking
            await InitializeWithProgressAsync();

            _logger.LogInfo("App", "Startup completed");
        }
        catch (Exception ex)
        {
            _logger.LogCritical("App", "Startup failed", ex);
            _splashScreen?.Close();
            MessageBox.Show($"Failed to start application:\n{ex.Message}", "Startup Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private async Task InitializeWithProgressAsync()
    {
        var totalSteps = 8;
        var currentStep = 0;

        void ReportProgress(string message)
        {
            currentStep++;
            var percentage = (currentStep * 100.0) / totalSteps;
            _splashScreen?.UpdateProgress(percentage, message);
            _logger.LogInfo("Startup", $"[{currentStep}/{totalSteps}] {message}");
        }

        // Step 1: Initialize exception handlers
        _splashScreen?.UpdateProgress(5, "Setting up exception handlers...");
        await Task.Delay(100); // Allow UI to update

        // Step 2: Configure services
        ReportProgress("Configuring services...");
        var services = new ServiceCollection();
        ConfigureServices(services);
        await Task.Delay(200);

        // Step 3: Build service provider
        ReportProgress("Building service provider...");
        Services = services.BuildServiceProvider();
        _logger.LogInfo("App", "Services configured successfully");
        await Task.Delay(100);

        // Step 4: Initialize logging service
        ReportProgress("Initializing logging service...");
        var loggingService = Services.GetService<LoggingService>();
        await Task.Delay(100);

        // Step 5: Initialize Ollama service manager
        ReportProgress("Initializing AI service manager...");
        _ollamaService = Services.GetRequiredService<OllamaServiceManager>();
        _ollamaService.StatusChanged += (s, status) =>
        {
            _logger.LogDebug("Ollama", $"Status changed: {status}");
        };
        await Task.Delay(100);

        // Step 6: Start Ollama service
        ReportProgress("Starting Ollama AI service...");
        try
        {
            _logger.LogInfo("Ollama", "Starting Ollama service...");
            var started = await _ollamaService.StartAsync();
            if (started)
            {
                _logger.LogInfo("Ollama", "Started successfully");
            }
            else
            {
                _logger.LogWarning("Ollama", "Failed to start - check if installed");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Ollama", "Error starting Ollama", ex);
            // Continue anyway - Ollama is optional
        }

        // Step 7: Initialize content generators
        ReportProgress("Initializing content generators...");
        var contentGenerator = Services.GetService<ContentGeneratorService>();
        var imageGenerator = Services.GetService<ImageGeneratorService>();
        await Task.Delay(100);

        // Step 8: Loading main window
        ReportProgress("Loading main window...");
        await Task.Delay(200);

        // Complete splash and show main window
        await _splashScreen!.CompleteAsync();

        // Create and show main window
        var mainWindow = new MainWindow();
        MainWindow = mainWindow;
        mainWindow.Show();

        // Close splash
        await Task.Delay(300);
        _splashScreen.Close();
        _splashScreen = null;
    }

    private void SetupExceptionHandlers()
    {
        // Handle exceptions on the UI thread
        DispatcherUnhandledException += (s, e) =>
        {
            _logger.LogCritical("UI", "Unhandled UI exception", e.Exception);

            var result = MessageBox.Show(
                $"An unexpected error occurred:\n\n{e.Exception.Message}\n\nDo you want to continue?",
                "Application Error",
                MessageBoxButton.YesNo,
                MessageBoxImage.Error);

            e.Handled = result == MessageBoxResult.Yes;

            if (!e.Handled)
            {
                _logger.LogCritical("UI", "Application terminated due to unhandled exception");
            }
        };

        // Handle exceptions on background threads
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            var ex = e.ExceptionObject as Exception;
            _logger.LogCritical("AppDomain", "Unhandled domain exception", ex);

            if (e.IsTerminating)
            {
                _logger.LogCritical("AppDomain", "Application is terminating!");
                _logger.Flush();
            }
        };

        // Handle task exceptions
        TaskScheduler.UnobservedTaskException += (s, e) =>
        {
            _logger.LogError("Task", "Unobserved task exception", e.Exception);
            e.SetObserved(); // Prevent app crash
        };

        _logger.LogInfo("App", "Exception handlers configured");
    }

    private void ConfigureServices(IServiceCollection services)
    {
        _logger.LogDebug("App", "Configuring services...");

        // Logging
        services.AddLogging(builder =>
        {
            builder.AddDebug();
            builder.SetMinimumLevel(LogLevel.Debug);
        });

        // Configuration
        var config = new OrchestratorConfig
        {
            NumCores = 0, // Auto-detect
            ApiPort = 5000,
            WebSocketPort = 5001,
            SignalRPort = 5002,
            RedisConnectionString = "localhost:6379"
        };
        services.AddSingleton(config);
        _logger.LogDebug("App", $"Orchestrator config: Cores={config.NumCores}, API={config.ApiPort}");

        // Core services
        services.AddSingleton<ProcessOrchestrator>();
        services.AddSingleton<OllamaServiceManager>();
        services.AddSingleton<LoggingService>();
        services.AddSingleton<PlatformSetupService>();
        services.AddSingleton<AccountPoolManager>();
        services.AddSingleton<HuggingFaceModelManager>();
        services.AddSingleton<GpuRentalService>();
        services.AddSingleton<ContentGeneratorService>();
        services.AddSingleton<ImageGeneratorService>();

        // ViewModels
        services.AddTransient<MainViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<WorkersViewModel>();
        services.AddTransient<TasksViewModel>();
        services.AddTransient<SettingsViewModel>();

        _logger.LogDebug("App", "Services configuration completed");
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        _logger.LogInfo("App", "Application shutting down...");

        try
        {
            // Gracefully stop Ollama
            if (_ollamaService != null)
            {
                _logger.LogInfo("Ollama", "Stopping Ollama service...");
                await _ollamaService.StopAsync();
                _ollamaService.Dispose();
                _logger.LogInfo("Ollama", "Stopped successfully");
            }

            if (Services is IDisposable disposable)
            {
                disposable.Dispose();
            }

            _logger.LogInfo("App", "Shutdown completed");
            _logger.LogInfo("App", "═══════════════════════════════════════════════════════");
        }
        catch (Exception ex)
        {
            _logger.LogError("App", "Error during shutdown", ex);
        }
        finally
        {
            // Ensure logs are flushed before exit
            _logger.Flush();
            DebugLogger.Instance.Dispose();
        }

        base.OnExit(e);
    }
}
