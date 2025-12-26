using AIManager.Mobile.Models;
using AIManager.Mobile.ViewModels;

namespace AIManager.Mobile.Views;

public partial class WebsitesPage : ContentPage
{
    private readonly WebsitesViewModel _viewModel;

    public WebsitesPage(WebsitesViewModel viewModel)
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
        if (sender is Switch toggle && toggle.BindingContext is WebsiteConfig website)
        {
            await _viewModel.ToggleEnabledCommand.ExecuteAsync(website);
        }
    }

    private async void OnCopyApiKey(object sender, EventArgs e)
    {
        if (!string.IsNullOrEmpty(_viewModel.EditApiKey))
        {
            await Clipboard.SetTextAsync(_viewModel.EditApiKey);
            await DisplayAlert("คัดลอกแล้ว", "API Key ถูกคัดลอกแล้ว", "ตกลง");
        }
    }

    private async void OnCopySecretKey(object sender, EventArgs e)
    {
        if (!string.IsNullOrEmpty(_viewModel.EditSecretKey))
        {
            await Clipboard.SetTextAsync(_viewModel.EditSecretKey);
            await DisplayAlert("คัดลอกแล้ว", "Secret Key ถูกคัดลอกแล้ว", "ตกลง");
        }
    }
}

// Extension for async command execution
public static class CommandExtensions
{
    public static async Task ExecuteAsync(this System.Windows.Input.ICommand command, object? parameter = null)
    {
        if (command.CanExecute(parameter))
        {
            if (command is Command cmd)
            {
                cmd.Execute(parameter);
            }
            else
            {
                command.Execute(parameter);
            }
        }
        await Task.CompletedTask;
    }
}
