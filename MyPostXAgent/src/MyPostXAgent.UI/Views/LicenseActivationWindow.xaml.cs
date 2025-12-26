using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using MyPostXAgent.UI.ViewModels;

namespace MyPostXAgent.UI.Views;

/// <summary>
/// License Activation Window
/// </summary>
public partial class LicenseActivationWindow : Window
{
    private readonly LicenseViewModel _viewModel;

    public LicenseActivationWindow()
    {
        InitializeComponent();

        _viewModel = App.Services.GetRequiredService<LicenseViewModel>();
        DataContext = _viewModel;

        // Watch for activation success
        _viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(LicenseViewModel.ActivationSuccess) && _viewModel.ActivationSuccess)
            {
                DialogResult = true;
                Close();
            }
        };
    }
}
