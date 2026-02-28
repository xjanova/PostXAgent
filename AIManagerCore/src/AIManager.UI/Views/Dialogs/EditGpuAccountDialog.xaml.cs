using System.Windows;
using System.Windows.Controls;
using AIManager.UI.Views.Pages;
using MaterialDesignThemes.Wpf;

namespace AIManager.UI.Views.Dialogs
{
    /// <summary>
    /// Dialog for editing an existing GPU provider account
    /// </summary>
    public partial class EditGpuAccountDialog : UserControl
    {
        private readonly GpuAccountViewModel _account;

        public EditGpuAccountDialog(GpuAccountViewModel account)
        {
            InitializeComponent();
            _account = account;

            // Populate fields with existing data
            TxtAccountName.Text = account.Name;
            TxtEmail.Text = account.Email;

            // Update title based on provider
            UpdateTitleForProvider(account.Provider);
        }

        private void UpdateTitleForProvider(string provider)
        {
            switch (provider)
            {
                case "vast_ai":
                    TxtTitle.Text = "Edit Vast.ai Account";
                    ProviderIcon.Foreground = new System.Windows.Media.SolidColorBrush(
                        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#6366F1")!);
                    break;
                case "runpod":
                    TxtTitle.Text = "Edit RunPod Account";
                    ProviderIcon.Foreground = new System.Windows.Media.SolidColorBrush(
                        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#8B5CF6")!);
                    break;
                case "lambda":
                    TxtTitle.Text = "Edit Lambda Labs Account";
                    ProviderIcon.Foreground = new System.Windows.Media.SolidColorBrush(
                        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#F59E0B")!);
                    break;
                case "lightning":
                    TxtTitle.Text = "Edit Lightning.ai Account";
                    ProviderIcon.Foreground = new System.Windows.Media.SolidColorBrush(
                        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#3B82F6")!);
                    break;
                default:
                    TxtTitle.Text = "Edit GPU Provider Account";
                    break;
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

            // Update the account
            _account.Name = TxtAccountName.Text;
            _account.Email = TxtEmail.Text;

            // TODO: If API key is provided, update it via API call

            DialogHost.CloseDialogCommand.Execute(_account, this);
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogHost.CloseDialogCommand.Execute(null, this);
        }
    }
}
