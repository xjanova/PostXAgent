using AIManager.Mobile.Models;
using AIManager.Mobile.ViewModels;

namespace AIManager.Mobile.Views;

public partial class BankAccountsPage : ContentPage
{
    private readonly BankAccountsViewModel _viewModel;

    public BankAccountsPage(BankAccountsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.InitializeAsync();
    }

    private async void OnEnabledToggled(object sender, ToggledEventArgs e)
    {
        if (sender is Switch toggle && toggle.BindingContext is BankAccountConfig account)
        {
            // The binding already updates IsEnabled, just need to save
            // This is handled by the ViewModel through property change
        }
    }
}
