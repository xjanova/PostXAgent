using AIManager.Mobile.ViewModels;

namespace AIManager.Mobile.Views;

public partial class SmsMonitorPage : ContentPage
{
    private readonly SmsMonitorViewModel _viewModel;

    public SmsMonitorPage(SmsMonitorViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.InitializeCommand.Execute(null);
    }
}
