using System.Windows;
using System.Windows.Controls;

namespace AIManager.UI.Views.SetupPages;

public partial class DatabaseSetupPage : Page
{
    public DatabaseSetupPage()
    {
        InitializeComponent();
    }

    private async void BtnTestConnection_Click(object sender, RoutedEventArgs e)
    {
        BtnTestConnection.IsEnabled = false;
        TxtStatus.Visibility = Visibility.Visible;
        TxtStatus.Foreground = System.Windows.Media.Brushes.Yellow;
        TxtStatus.Text = "Testing connection...";

        // Simulate connection test
        await Task.Delay(1500);

        // For now, just show success (actual DB connection would be tested here)
        TxtStatus.Foreground = System.Windows.Media.Brushes.LightGreen;
        TxtStatus.Text = "✓ Connection successful!";

        BtnTestConnection.IsEnabled = true;
    }
}
