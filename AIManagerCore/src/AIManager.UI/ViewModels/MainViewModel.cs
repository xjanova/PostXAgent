using CommunityToolkit.Mvvm.Input;
using AIManager.Core.Orchestrator;

namespace AIManager.UI.ViewModels;

/// <summary>
/// Main window ViewModel
/// </summary>
public class MainViewModel : BaseViewModel
{
    private readonly ProcessOrchestrator _orchestrator;

    private bool _isServerRunning;
    public bool IsServerRunning
    {
        get => _isServerRunning;
        set => SetProperty(ref _isServerRunning, value);
    }

    private string _serverStatus = "Stopped";
    public string ServerStatus
    {
        get => _serverStatus;
        set => SetProperty(ref _serverStatus, value);
    }

    private string _currentPage = "Dashboard";
    public string CurrentPage
    {
        get => _currentPage;
        set => SetProperty(ref _currentPage, value);
    }

    public IAsyncRelayCommand StartServerCommand { get; }
    public IAsyncRelayCommand StopServerCommand { get; }
    public IRelayCommand<string> NavigateToCommand { get; }

    public MainViewModel(ProcessOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
        Title = "AI Manager Dashboard";

        StartServerCommand = new AsyncRelayCommand(StartServerAsync);
        StopServerCommand = new AsyncRelayCommand(StopServerAsync);
        NavigateToCommand = new RelayCommand<string>(NavigateTo);

        _orchestrator.StatsUpdated += (s, e) =>
        {
            IsServerRunning = _orchestrator.IsRunning;
            ServerStatus = IsServerRunning ? "Running" : "Stopped";
        };
    }

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

    private void NavigateTo(string? page)
    {
        if (page != null)
            CurrentPage = page;
    }
}
