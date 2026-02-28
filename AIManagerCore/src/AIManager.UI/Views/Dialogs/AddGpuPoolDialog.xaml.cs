using System.Windows;
using System.Windows.Controls;
using AIManager.UI.Views.Pages;
using MaterialDesignThemes.Wpf;

namespace AIManager.UI.Views.Dialogs
{
    /// <summary>
    /// Dialog for creating a new GPU account pool
    /// </summary>
    public partial class AddGpuPoolDialog : UserControl
    {
        private static int _nextId = 1;

        /// <summary>
        /// Gets the pool name entered by the user
        /// </summary>
        public string PoolName => TxtPoolName.Text;

        /// <summary>
        /// Gets the selected rotation strategy
        /// </summary>
        public string RotationStrategy => (CmbRotationStrategy.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "most_credits";

        /// <summary>
        /// Gets the minimum credits threshold
        /// </summary>
        public decimal MinCreditsThreshold
        {
            get
            {
                if (decimal.TryParse(TxtMinCredits.Text, out var value))
                    return value;
                return 0.50m;
            }
        }

        /// <summary>
        /// Gets the cooldown period in minutes
        /// </summary>
        public int CooldownMinutes
        {
            get
            {
                if (int.TryParse(TxtCooldownMinutes.Text, out var value))
                    return value;
                return 60;
            }
        }

        /// <summary>
        /// Gets whether auto failover is enabled
        /// </summary>
        public bool AutoFailover => ChkAutoFailover.IsChecked ?? true;

        public AddGpuPoolDialog()
        {
            InitializeComponent();
        }

        private void Create_Click(object sender, RoutedEventArgs e)
        {
            // Validate inputs
            if (string.IsNullOrWhiteSpace(TxtPoolName.Text))
            {
                MessageBox.Show("Please enter a pool name.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtPoolName.Focus();
                return;
            }

            if (!decimal.TryParse(TxtMinCredits.Text, out var minCredits) || minCredits < 0)
            {
                MessageBox.Show("Please enter a valid minimum credits threshold.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtMinCredits.Focus();
                return;
            }

            if (!int.TryParse(TxtCooldownMinutes.Text, out var cooldown) || cooldown < 0)
            {
                MessageBox.Show("Please enter a valid cooldown period.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtCooldownMinutes.Focus();
                return;
            }

            // Create the view model to return
            var pool = new GpuPoolViewModel
            {
                Id = _nextId++,
                Name = PoolName,
                RotationStrategy = RotationStrategy,
                ActiveAccounts = 0,
                TotalCredits = 0,
                InstancesToday = 0
            };

            DialogHost.CloseDialogCommand.Execute(pool, this);
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogHost.CloseDialogCommand.Execute(null, this);
        }
    }
}
