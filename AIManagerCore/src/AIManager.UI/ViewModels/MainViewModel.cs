using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AIManager.Core.Orchestrator;

namespace AIManager.UI.ViewModels;

/// <summary>
/// Main window ViewModel
/// </summary>
public partial class MainViewModel : BaseViewModel
{
    private readonly ProcessOrchestrator _orchestrator;

    [ObservableProperty]
    private bool _isServerRunning;

    [ObservableProperty]
    private string _serverStatus = "Stopped";

    [ObservableProperty]
    private string _currentPage = "Dashboard";

    public MainViewModel(ProcessOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
        Title = "AI Manager Dashboard";

        _orchestrator.StatsUpdated += (s, e) =>
        {
            IsServerRunning = _orchestrator.IsRunning;
            ServerStatus = IsServerRunning ? "Running" : "Stopped";
        };
    }

    [RelayCommand]
    private async Task StartServerAsync()
    {
        if (IsBusy) return;

        try
        {
            IsBusy = true;
            ServerStatus = "Starting...";
            await _orchestrator.StartAsync();
            IsServerRunning = true;
            ServerStatus = "Running";
        }
        catch (Exception ex)
        {
            ServerStatus = $"Error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task StopServerAsync()
    {
        if (IsBusy) return;

        try
        {
            IsBusy = true;
            ServerStatus = "Stopping...";
            await _orchestrator.StopAsync();
            IsServerRunning = false;
            ServerStatus = "Stopped";
        }
        catch (Exception ex)
        {
            ServerStatus = $"Error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void NavigateTo(string page)
    {
        CurrentPage = page;
    }
}
