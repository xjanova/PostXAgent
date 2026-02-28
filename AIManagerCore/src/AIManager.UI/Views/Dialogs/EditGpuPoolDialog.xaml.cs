using System.Windows;
using System.Windows.Controls;
using AIManager.UI.Views.Pages;
using MaterialDesignThemes.Wpf;

namespace AIManager.UI.Views.Dialogs
{
    /// <summary>
    /// Dialog for editing an existing GPU pool
    /// </summary>
    public partial class EditGpuPoolDialog : UserControl
    {
        private readonly GpuPoolViewModel _pool;

        public EditGpuPoolDialog(GpuPoolViewModel pool)
        {
            InitializeComponent();
            _pool = pool;

            // Populate fields with existing data
            TxtPoolName.Text = pool.Name;
            TxtActiveAccounts.Text = pool.ActiveAccounts.ToString();
            TxtTotalCredits.Text = $"${pool.TotalCredits:F2}";
            TxtInstancesToday.Text = pool.InstancesToday.ToString();

            // Select the current rotation strategy
            SelectRotationStrategy(pool.RotationStrategy);
        }

        private void SelectRotationStrategy(string strategy)
        {
            foreach (ComboBoxItem item in CmbRotationStrategy.Items)
            {
                if (item.Tag?.ToString() == strategy)
                {
                    CmbRotationStrategy.SelectedItem = item;
                    break;
                }
            }

            // Default to first item if not found
            if (CmbRotationStrategy.SelectedItem == null && CmbRotationStrategy.Items.Count > 0)
            {
                CmbRotationStrategy.SelectedIndex = 0;
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            // Validate inputs
            if (string.IsNullOrWhiteSpace(TxtPoolName.Text))
            {
                MessageBox.Show("Please enter a pool name.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtPoolName.Focus();
                return;
            }

            // Update the pool
            _pool.Name = TxtPoolName.Text;
            _pool.RotationStrategy = (CmbRotationStrategy.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "most_credits";

            DialogHost.CloseDialogCommand.Execute(_pool, this);
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogHost.CloseDialogCommand.Execute(null, this);
        }
    }
}
