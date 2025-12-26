using AIManager.Mobile.ViewModels;

namespace AIManager.Mobile.Views;

public partial class WorkersPage : ContentPage
{
    private readonly WorkersViewModel _viewModel;

    public WorkersPage(WorkersViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.LoadWorkersCommand.Execute(null);
    }
}
