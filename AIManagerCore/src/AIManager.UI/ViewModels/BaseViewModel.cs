using CommunityToolkit.Mvvm.ComponentModel;

namespace AIManager.UI.ViewModels;

/// <summary>
/// Base class for all ViewModels
/// </summary>
public abstract partial class BaseViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _title = string.Empty;
}
