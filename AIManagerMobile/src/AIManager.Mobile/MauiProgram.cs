using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using AIManager.Mobile.Services;
using AIManager.Mobile.ViewModels;
using AIManager.Mobile.Views;

namespace AIManager.Mobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                fonts.AddFont("MaterialIcons-Regular.ttf", "MaterialIcons");
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        // Register Services
        builder.Services.AddSingleton<IAIManagerApiService, AIManagerApiService>();
        builder.Services.AddSingleton<ISmsListenerService, SmsListenerService>();
        builder.Services.AddSingleton<IPaymentDetectionService, PaymentDetectionService>();
        builder.Services.AddSingleton<ISignalRService, SignalRService>();
        builder.Services.AddSingleton<ISettingsService, SettingsService>();
        builder.Services.AddSingleton<IConnectionSyncService, ConnectionSyncService>();

        // Multi-Website SMS Gateway Services
        builder.Services.AddSingleton<IWebsiteConfigService, WebsiteConfigService>();
        builder.Services.AddSingleton<ISmsClassifierService, SmsClassifierService>();
        builder.Services.AddSingleton<IWebhookDispatchService, WebhookDispatchService>();
        builder.Services.AddSingleton<IBankAccountService, BankAccountService>();

        // Register ViewModels
        builder.Services.AddTransient<DashboardViewModel>();
        builder.Services.AddTransient<TasksViewModel>();
        builder.Services.AddTransient<SmsMonitorViewModel>();
        builder.Services.AddTransient<PaymentsViewModel>();
        builder.Services.AddTransient<WorkersViewModel>();
        builder.Services.AddTransient<SettingsViewModel>();
        builder.Services.AddTransient<WebsitesViewModel>();
        builder.Services.AddTransient<BankAccountsViewModel>();

        // Register Pages
        builder.Services.AddTransient<DashboardPage>();
        builder.Services.AddTransient<TasksPage>();
        builder.Services.AddTransient<SmsMonitorPage>();
        builder.Services.AddTransient<PaymentsPage>();
        builder.Services.AddTransient<WorkersPage>();
        builder.Services.AddTransient<SettingsPage>();
        builder.Services.AddTransient<WebsitesPage>();
        builder.Services.AddTransient<BankAccountsPage>();

        // HTTP Client
        builder.Services.AddHttpClient("AIManagerApi", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        return builder.Build();
    }
}
