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

        try
        {
            // Configure services
            System.Diagnostics.Debug.WriteLine("App: Creating service collection...");
            var services = new ServiceCollection();

            System.Diagnostics.Debug.WriteLine("App: Configuring services...");
            ConfigureServices(services);

            System.Diagnostics.Debug.WriteLine("App: Building service provider...");
            Services = services.BuildServiceProvider();

            // Initialize database
            System.Diagnostics.Debug.WriteLine("App: Initializing database...");
            var dbService = Services.GetRequiredService<DatabaseService>();
            await dbService.InitializeAsync();

            // Initialize AI providers
            System.Diagnostics.Debug.WriteLine("App: Initializing AI providers...");
            var aiService = Services.GetRequiredService<Core.Services.AI.AIContentService>();
            await aiService.InitializeProvidersAsync();

            // Check license/demo status
            System.Diagnostics.Debug.WriteLine("App: Checking license...");
            await CheckLicenseStatusAsync();

            System.Diagnostics.Debug.WriteLine("App: Startup completed successfully!");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"App: Startup failed: {ex.GetType().Name} - {ex.Message}");
            System.Windows.MessageBox.Show(
                $"เกิดข้อผิดพลาดในการเริ่มต้นโปรแกรม:\n\n{ex.Message}\n\nโปรแกรมจะปิดอัตโนมัติ",
                "MyPostXAgent Startup Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown();
        }
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

    private bool _isHandlingException = false;

    private void SetupExceptionHandling()
    {
        // UI Thread exceptions
        DispatcherUnhandledException += (s, e) =>
        {
            // Prevent infinite recursion in exception handler
            if (_isHandlingException)
            {
                e.Handled = false; // Let it crash
                return;
            }

            try
            {
                _isHandlingException = true;

                // Special handling for StackOverflow - can't show MessageBox
                if (e.Exception is StackOverflowException)
                {
                    System.Diagnostics.Debug.WriteLine("FATAL: StackOverflowException detected. Exiting.");
                    Environment.FailFast("StackOverflowException detected", e.Exception);
                }

                MessageBox.Show(
                    $"เกิดข้อผิดพลาด: {e.Exception.Message}",
                    "MyPostXAgent Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                e.Handled = true;
            }
            finally
            {
                _isHandlingException = false;
            }
        };

        // Non-UI Thread exceptions
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            if (_isHandlingException)
                return;

            try
            {
                _isHandlingException = true;

                var ex = e.ExceptionObject as Exception;

                // Special handling for StackOverflow
                if (ex is StackOverflowException)
                {
                    System.Diagnostics.Debug.WriteLine("FATAL: StackOverflowException in AppDomain. Exiting.");
                    return; // Can't do anything
                }

                MessageBox.Show(
                    $"เกิดข้อผิดพลาดร้ายแรง: {ex?.Message}",
                    "MyPostXAgent Critical Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                _isHandlingException = false;
            }
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
