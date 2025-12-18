using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using AIManager.Core.Orchestrator;
using AIManager.Core.Models;
using AIManager.Core.Services;
using AIManager.UI.ViewModels;
using Microsoft.Extensions.Logging;

namespace AIManager.UI;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;
    private OllamaServiceManager? _ollamaService;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();

        // Auto-start Ollama service
        _ollamaService = Services.GetRequiredService<OllamaServiceManager>();
        _ollamaService.StatusChanged += (s, status) =>
        {
            System.Diagnostics.Debug.WriteLine($"[Ollama] Status: {status}");
        };

        // Start Ollama in background
        _ = Task.Run(async () =>
        {
            var started = await _ollamaService.StartAsync();
            if (started)
            {
                System.Diagnostics.Debug.WriteLine("[Ollama] Started successfully");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[Ollama] Failed to start - check if installed");
            }
        });
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // Logging
        services.AddLogging(builder =>
        {
            builder.AddDebug();
            builder.SetMinimumLevel(LogLevel.Debug);
        });

        // Configuration
        services.AddSingleton(new OrchestratorConfig
        {
            NumCores = 0, // Auto-detect
            ApiPort = 5000,
            WebSocketPort = 5001,
            SignalRPort = 5002,
            RedisConnectionString = "localhost:6379"
        });

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
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        // Gracefully stop Ollama
        if (_ollamaService != null)
        {
            await _ollamaService.StopAsync();
            _ollamaService.Dispose();
        }

        if (Services is IDisposable disposable)
        {
            disposable.Dispose();
        }
        base.OnExit(e);
    }
}
