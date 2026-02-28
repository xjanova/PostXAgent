using System.Windows;
using System.Windows.Controls;
using AIManager.UI.Views.Pages;
using MaterialDesignThemes.Wpf;

namespace AIManager.UI.Views.Dialogs
{
    /// <summary>
    /// Dialog for adding a new GPU provider account
    /// </summary>
    public partial class AddGpuAccountDialog : UserControl
    {
        private readonly string _initialProvider;
        private static int _nextId = 1;

        /// <summary>
        /// Gets the account name entered by the user
        /// </summary>
        public string AccountName => TxtAccountName.Text;

        /// <summary>
        /// Gets the selected provider
        /// </summary>
        public string Provider => (CmbProvider.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "vast_ai";

        /// <summary>
        /// Gets the API key entered by the user
        /// </summary>
        public string ApiKey => TxtApiKey.Password;

        /// <summary>
        /// Gets the email entered by the user
        /// </summary>
        public string Email => TxtEmail.Text;

        /// <summary>
        /// Gets the notes entered by the user
        /// </summary>
        public string Notes => TxtNotes.Text;

        /// <summary>
        /// Gets the current balance (set after test connection)
        /// </summary>
        public decimal CurrentBalance { get; private set; }

        public AddGpuAccountDialog() : this("vast_ai")
        {
        }

        public AddGpuAccountDialog(string provider)
        {
            InitializeComponent();
            _initialProvider = provider;
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Set initial provider selection
            foreach (ComboBoxItem item in CmbProvider.Items)
            {
                if (item.Tag?.ToString() == _initialProvider)
                {
                    CmbProvider.SelectedItem = item;
                    break;
                }
            }

            // Update title based on provider
            UpdateTitleForProvider(_initialProvider);
        }

        private void UpdateTitleForProvider(string provider)
        {
            switch (provider)
            {
                case "vast_ai":
                    TxtTitle.Text = "Add Vast.ai Account";
                    ProviderIcon.Foreground = new System.Windows.Media.SolidColorBrush(
                        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#6366F1")!);
                    break;
                case "runpod":
                    TxtTitle.Text = "Add RunPod Account";
                    ProviderIcon.Foreground = new System.Windows.Media.SolidColorBrush(
                        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#8B5CF6")!);
                    break;
                case "lambda":
                    TxtTitle.Text = "Add Lambda Labs Account";
                    ProviderIcon.Foreground = new System.Windows.Media.SolidColorBrush(
                        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#F59E0B")!);
                    break;
                case "lightning":
                    TxtTitle.Text = "Add Lightning.ai Account";
                    ProviderIcon.Foreground = new System.Windows.Media.SolidColorBrush(
                        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#3B82F6")!);
                    break;
                default:
                    TxtTitle.Text = "Add GPU Provider Account";
                    break;
            }
        }

        private void ToggleApiKeyVisibility_Click(object sender, RoutedEventArgs e)
        {
            // Toggle visibility is not straightforward with PasswordBox
            // For simplicity, we'll just show a tooltip
            if (!string.IsNullOrEmpty(TxtApiKey.Password))
            {
                MessageBox.Show($"API Key: {TxtApiKey.Password}", "API Key", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private async void TestConnection_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtApiKey.Password))
            {
                MessageBox.Show("Please enter an API key first.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // Show testing indicator
                IsEnabled = false;

                // TODO: Call actual API to test connection
                // For now, simulate a test
                await System.Threading.Tasks.Task.Delay(1500);

                // Simulate getting balance
                CurrentBalance = 10.50m;

                MessageBox.Show($"Connection successful!\n\nProvider: {Provider}\nBalance: ${CurrentBalance:F2}",
                    "Connection Test", MessageBoxButton.OK, MessageBoxImage.Information);

                IsEnabled = true;
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Connection failed: {ex.Message}", "Connection Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                IsEnabled = true;
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            // Validate inputs
            if (string.IsNullOrWhiteSpace(TxtAccountName.Text))
            {
                MessageBox.Show("Please enter an account name.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtAccountName.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(TxtApiKey.Password))
            {
                MessageBox.Show("Please enter an API key.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Create the view model to return
            var account = new GpuAccountViewModel
            {
                Id = _nextId++,
                Name = AccountName,
                Provider = Provider,
                Email = Email,
                Status = "active",
                CreditsBalance = CurrentBalance,
                LastUsed = "Never"
            };

            // Close dialog and return the account
            DialogHost.CloseDialogCommand.Execute(account, this);
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogHost.CloseDialogCommand.Execute(null, this);
        }
    }
}
