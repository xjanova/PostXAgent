using System.Windows;

namespace AIManager.UI.Views.Pages
{
    /// <summary>
    /// Dialog for adding a new GPU worker to the pool
    /// </summary>
    public partial class AddWorkerDialog : Window
    {
        /// <summary>
        /// Gets the worker name entered by the user
        /// </summary>
        public string WorkerName => TxtWorkerName.Text;

        /// <summary>
        /// Gets the worker URL entered by the user
        /// </summary>
        public string WorkerUrl => TxtWorkerUrl.Text;

        public AddWorkerDialog()
        {
            InitializeComponent();
        }

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            // Validate inputs
            if (string.IsNullOrWhiteSpace(TxtWorkerName.Text))
            {
                MessageBox.Show("Please enter a worker name.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtWorkerName.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(TxtWorkerUrl.Text))
            {
                MessageBox.Show("Please enter a worker URL.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtWorkerUrl.Focus();
                return;
            }

            // Validate URL format
            if (!Uri.TryCreate(TxtWorkerUrl.Text, UriKind.Absolute, out var uri) ||
                (uri.Scheme != "http" && uri.Scheme != "https"))
            {
                MessageBox.Show("Please enter a valid HTTP or HTTPS URL.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtWorkerUrl.Focus();
                return;
            }

            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
