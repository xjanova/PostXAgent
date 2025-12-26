using System.Net.Http;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MyPostXAgent.Core.Services.Data;
using MyPostXAgent.Core.Services.License;

namespace MyPostXAgent.UI;

/// <summary>
/// Application Entry Point
/// </summary>
public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;
    public static string AppVersion => "1.0.0";

    // DEV MODE: Set to true to bypass license check
#if DEBUG
    public static bool BypassLicense => true;
#else
    public static bool BypassLicense => false;
#endif

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Setup global exception handlers
        SetupExceptionHandling();

        // Configure services
        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();

        // Initialize database
        var dbService = Services.GetRequiredService<DatabaseService>();
        await dbService.InitializeAsync();

        // Check license/demo status
        await CheckLicenseStatusAsync();
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // Logging
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        // HTTP Client
        services.AddHttpClient();

        // Core Services
        services.AddSingleton<DatabaseService>();
        services.AddSingleton<MachineIdGenerator>();
        services.AddSingleton<LicenseService>(sp =>
        {
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var machineIdGenerator = sp.GetRequiredService<MachineIdGenerator>();
            var logger = sp.GetService<ILogger<LicenseService>>();
            return new LicenseService(
                httpClientFactory.CreateClient(),
                machineIdGenerator,
                "https://api.postxagent.com",
                AppVersion,
                logger);
        });
        services.AddSingleton<DemoManager>();

        // AI Services
        services.AddSingleton<Core.Services.AI.AIContentService>();

        // ViewModels
        services.AddTransient<ViewModels.MainViewModel>();
        services.AddTransient<ViewModels.LicenseViewModel>();
        services.AddTransient<ViewModels.DashboardViewModel>();
        services.AddTransient<ViewModels.AccountsViewModel>();
        services.AddTransient<ViewModels.PostsViewModel>();
        services.AddTransient<ViewModels.SettingsViewModel>();
        services.AddTransient<ViewModels.SchedulerViewModel>();
        services.AddTransient<ViewModels.ContentGeneratorViewModel>();
    }

    private async Task CheckLicenseStatusAsync()
    {
        // DEV MODE: Bypass license check
        if (BypassLicense)
        {
            return;
        }

        var dbService = Services.GetRequiredService<DatabaseService>();
        var licenseService = Services.GetRequiredService<LicenseService>();

        // Load saved license info
        var licenseInfo = await dbService.GetLicenseInfoAsync();
        var demoInfo = await dbService.GetDemoInfoAsync();

        licenseService.SetLicenseInfo(licenseInfo);
        licenseService.SetDemoInfo(demoInfo);

        // Check if we need to show license activation window
        if (!licenseService.IsLicensed())
        {
            // Show license activation window
            var licenseWindow = new Views.LicenseActivationWindow();
            var result = licenseWindow.ShowDialog();

            if (result != true)
            {
                // User cancelled - exit app
                Shutdown();
                return;
            }
        }
    }

    private void SetupExceptionHandling()
    {
        // UI Thread exceptions
        DispatcherUnhandledException += (s, e) =>
        {
            MessageBox.Show(
                $"เกิดข้อผิดพลาด: {e.Exception.Message}",
                "MyPostXAgent Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            e.Handled = true;
        };

        // Non-UI Thread exceptions
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            var ex = e.ExceptionObject as Exception;
            MessageBox.Show(
                $"เกิดข้อผิดพลาดร้ายแรง: {ex?.Message}",
                "MyPostXAgent Critical Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        };

        // Task exceptions
        TaskScheduler.UnobservedTaskException += (s, e) =>
        {
            e.SetObserved();
        };
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // Cleanup
        if (Services is IDisposable disposable)
        {
            disposable.Dispose();
        }

        base.OnExit(e);
    }
}
