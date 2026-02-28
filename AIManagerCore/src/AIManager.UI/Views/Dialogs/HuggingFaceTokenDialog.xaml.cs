using System.Diagnostics;
using System.Windows;

namespace AIManager.UI.Views.Dialogs;

/// <summary>
/// Dialog for setting HuggingFace API token
/// </summary>
public partial class HuggingFaceTokenDialog : Window
{
    private bool _isTokenVisible = false;
    private string _maskedToken = "";

    /// <summary>
    /// Gets the entered token (or null if cleared)
    /// </summary>
    public string? Token { get; private set; }

    public HuggingFaceTokenDialog(bool hasExistingToken = false)
    {
        InitializeComponent();

        // Show status if token is already configured
        if (hasExistingToken)
        {
            StatusPanel.Visibility = Visibility.Visible;
            BtnClear.Visibility = Visibility.Visible;
            TxtToken.Text = "hf_••••••••••••••••••••••••••••••••";
            _maskedToken = TxtToken.Text;
        }
    }

    private void HfLink_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://huggingface.co/settings/tokens",
                UseShellExecute = true
            });
        }
        catch
        {
            MessageBox.Show(
                "Could not open browser. Please visit:\nhttps://huggingface.co/settings/tokens",
                "Open Link",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }

    private void ToggleVisibility_Click(object sender, RoutedEventArgs e)
    {
        _isTokenVisible = !_isTokenVisible;

        if (_isTokenVisible)
        {
            VisibilityIcon.Kind = MaterialDesignThemes.Wpf.PackIconKind.EyeOff;
            // Only show actual token if user typed something new
            if (TxtToken.Text == _maskedToken && !string.IsNullOrEmpty(_maskedToken))
            {
                // Still masked - can't reveal old token (we don't have it)
                MessageBox.Show(
                    "Cannot reveal existing token. Enter a new token to see it.",
                    "Info",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                _isTokenVisible = false;
                VisibilityIcon.Kind = MaterialDesignThemes.Wpf.PackIconKind.Eye;
            }
        }
        else
        {
            VisibilityIcon.Kind = MaterialDesignThemes.Wpf.PackIconKind.Eye;
        }
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "Are you sure you want to clear the HuggingFace token?\n\nYou will no longer be able to access gated models like FLUX.1-dev.",
            "Clear Token",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            Token = null;
            DialogResult = true;
            Close();
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var token = TxtToken.Text.Trim();

        // If token is the masked placeholder, don't save (no change)
        if (token == _maskedToken)
        {
            DialogResult = false;
            Close();
            return;
        }

        // Validate token format
        if (string.IsNullOrWhiteSpace(token))
        {
            MessageBox.Show(
                "Please enter a valid token or use Clear to remove the existing one.",
                "Validation",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        // Basic validation for HuggingFace token format
        if (!token.StartsWith("hf_"))
        {
            var result = MessageBox.Show(
                "HuggingFace tokens typically start with 'hf_'.\n\nDo you want to save this token anyway?",
                "Token Format",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
                return;
        }

        Token = token;
        DialogResult = true;
        Close();
    }
}
