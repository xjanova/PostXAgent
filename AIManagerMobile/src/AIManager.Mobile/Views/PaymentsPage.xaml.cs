using AIManager.Mobile.ViewModels;

namespace AIManager.Mobile.Views;

public partial class PaymentsPage : ContentPage
{
    private readonly PaymentsViewModel _viewModel;

    public PaymentsPage(PaymentsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.LoadPaymentsCommand.Execute(null);
    }
}
