using Microsoft.AspNetCore.SignalR.Client;
using AIManager.Mobile.Models;

namespace AIManager.Mobile.Services;

/// <summary>
/// SignalR service for real-time communication with AI Manager Core
/// </summary>
public class SignalRService : ISignalRService, IAsyncDisposable
{
    private HubConnection? _hubConnection;
    private ConnectionSettings _settings = new();
    private bool _isConnected;

    public bool IsConnected => _isConnected;

    public event EventHandler<bool>? ConnectionStateChanged;
    public event EventHandler<TaskItem>? TaskUpdated;
    public event EventHandler<WorkerInfo>? WorkerUpdated;
    public event EventHandler<PaymentInfo>? PaymentReceived;
    public event EventHandler<SystemStatus>? SystemStatusUpdated;

    public void SetConnectionSettings(ConnectionSettings settings)
    {
        _settings = settings;
    }

    public async Task ConnectAsync()
    {
        if (_hubConnection != null)
        {
            await DisconnectAsync();
        }

        _hubConnection = new HubConnectionBuilder()
            .WithUrl(_settings.SignalRHubUrl, options =>
            {
                if (!string.IsNullOrEmpty(_settings.ApiKey))
                {
                    options.Headers.Add("X-API-Key", _settings.ApiKey);
                }
            })
            .WithAutomaticReconnect(new[] { TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10) })
            .Build();

        // Register handlers
        _hubConnection.On<TaskItem>("TaskUpdated", task =>
        {
            MainThread.BeginInvokeOnMainThread(() => TaskUpdated?.Invoke(this, task));
        });

        _hubConnection.On<WorkerInfo>("WorkerUpdated", worker =>
        {
            MainThread.BeginInvokeOnMainThread(() => WorkerUpdated?.Invoke(this, worker));
        });

        _hubConnection.On<PaymentInfo>("PaymentReceived", payment =>
        {
            MainThread.BeginInvokeOnMainThread(() => PaymentReceived?.Invoke(this, payment));
        });

        _hubConnection.On<SystemStatus>("SystemStatusUpdated", status =>
        {
            MainThread.BeginInvokeOnMainThread(() => SystemStatusUpdated?.Invoke(this, status));
        });

        _hubConnection.Closed += async (error) =>
        {
            _isConnected = false;
            MainThread.BeginInvokeOnMainThread(() => ConnectionStateChanged?.Invoke(this, false));
            await Task.CompletedTask;
        };

        _hubConnection.Reconnected += async (connectionId) =>
        {
            _isConnected = true;
            MainThread.BeginInvokeOnMainThread(() => ConnectionStateChanged?.Invoke(this, true));
            await Task.CompletedTask;
        };

        _hubConnection.Reconnecting += async (error) =>
        {
            _isConnected = false;
            MainThread.BeginInvokeOnMainThread(() => ConnectionStateChanged?.Invoke(this, false));
            await Task.CompletedTask;
        };

        try
        {
            await _hubConnection.StartAsync();
            _isConnected = true;
            ConnectionStateChanged?.Invoke(this, true);
        }
        catch (Exception ex)
        {
            _isConnected = false;
            ConnectionStateChanged?.Invoke(this, false);
            System.Diagnostics.Debug.WriteLine($"SignalR connection failed: {ex.Message}");
        }
    }

    public async Task DisconnectAsync()
    {
        if (_hubConnection != null)
        {
            try
            {
                await _hubConnection.StopAsync();
                await _hubConnection.DisposeAsync();
            }
            catch
            {
                // Ignore errors during disconnect
            }
            finally
            {
                _hubConnection = null;
                _isConnected = false;
                ConnectionStateChanged?.Invoke(this, false);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
    }
}
