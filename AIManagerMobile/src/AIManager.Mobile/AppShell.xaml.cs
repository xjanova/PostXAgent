using AIManager.Mobile.Views;

namespace AIManager.Mobile;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        // Register routes for navigation
        Routing.RegisterRoute(nameof(WebsitesPage), typeof(WebsitesPage));
        Routing.RegisterRoute(nameof(BankAccountsPage), typeof(BankAccountsPage));
    }
}
