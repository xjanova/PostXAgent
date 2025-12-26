using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AIManager.Mobile.ViewModels;

/// <summary>
/// Base ViewModel with common functionality
/// </summary>
public partial class BaseViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private string? _errorMessage;

    public bool IsNotBusy => !IsBusy;

    protected async Task ExecuteAsync(Func<Task> action, string? errorMessage = null)
    {
        if (IsBusy) return;

        try
        {
            IsBusy = true;
            ErrorMessage = null;
            await action();
        }
        catch (Exception ex)
        {
            ErrorMessage = errorMessage ?? ex.Message;
            System.Diagnostics.Debug.WriteLine($"Error: {ex}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    protected async Task<T?> ExecuteAsync<T>(Func<Task<T>> action, string? errorMessage = null)
    {
        if (IsBusy) return default;

        try
        {
            IsBusy = true;
            ErrorMessage = null;
            return await action();
        }
        catch (Exception ex)
        {
            ErrorMessage = errorMessage ?? ex.Message;
            System.Diagnostics.Debug.WriteLine($"Error: {ex}");
            return default;
        }
        finally
        {
            IsBusy = false;
        }
    }

    protected void ShowError(string message)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            if (Application.Current?.MainPage != null)
            {
                await Application.Current.MainPage.DisplayAlert("ข้อผิดพลาด", message, "ตกลง");
            }
        });
    }

    protected void ShowMessage(string title, string message)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            if (Application.Current?.MainPage != null)
            {
                await Application.Current.MainPage.DisplayAlert(title, message, "ตกลง");
            }
        });
    }

    protected async Task<bool> ConfirmAsync(string title, string message)
    {
        if (Application.Current?.MainPage == null) return false;
        return await Application.Current.MainPage.DisplayAlert(title, message, "ใช่", "ไม่");
    }
}
