using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using AIManager.Core.Orchestrator;
using AIManager.Core.Models;
using AIManager.UI.ViewModels;
using Microsoft.Extensions.Logging;

namespace AIManager.UI;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();
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

        // ViewModels
        services.AddTransient<MainViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<WorkersViewModel>();
        services.AddTransient<TasksViewModel>();
        services.AddTransient<SettingsViewModel>();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (Services is IDisposable disposable)
        {
            disposable.Dispose();
        }
        base.OnExit(e);
    }
}
