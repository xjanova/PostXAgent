using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using AIManager.API.Hubs;
using System.Collections.Concurrent;

namespace AIManager.API.Controllers;

/// <summary>
/// Controller for managing mobile app connections and sync status
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class MobileConnectionController : ControllerBase
{
    private readonly IHubContext<AIManagerHub> _hubContext;
    private readonly ILogger<MobileConnectionController> _logger;
    private static readonly ConcurrentDictionary<string, MobileDeviceInfo> _connectedDevices = new();

    public MobileConnectionController(
        IHubContext<AIManagerHub> hubContext,
        ILogger<MobileConnectionController> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    /// <summary>
    /// Register a mobile device connection
    /// </summary>
    [HttpPost("register")]
    public async Task<IActionResult> RegisterDevice([FromBody] MobileDeviceRegistration request)
    {
        var deviceInfo = new MobileDeviceInfo
        {
            DeviceId = request.DeviceId,
            DeviceName = request.DeviceName,
            Platform = request.Platform,
            AppVersion = request.AppVersion,
            ConnectedAt = DateTime.UtcNow,
            LastSyncAt = DateTime.UtcNow,
            IsOnline = true,
            SmsMonitoringEnabled = request.SmsMonitoringEnabled,
            AutoApproveEnabled = request.AutoApproveEnabled
        };

        _connectedDevices[request.DeviceId] = deviceInfo;

        _logger.LogInformation("Mobile device registered: {DeviceId} ({DeviceName})",
            request.DeviceId, request.DeviceName);

        // Notify all clients about new device
        await _hubContext.Clients.All.SendAsync("MobileDeviceConnected", deviceInfo);

        return Ok(new
        {
            success = true,
            message = "อุปกรณ์ลงทะเบียนสำเร็จ",
            data = new
            {
                deviceInfo.DeviceId,
                syncStatus = "connected",
                serverTime = DateTime.UtcNow
            }
        });
    }

    /// <summary>
    /// Heartbeat to keep connection alive
    /// </summary>
    [HttpPost("heartbeat")]
    public async Task<IActionResult> Heartbeat([FromBody] HeartbeatRequest request)
    {
        if (_connectedDevices.TryGetValue(request.DeviceId, out var device))
        {
            device.LastHeartbeat = DateTime.UtcNow;
            device.IsOnline = true;
            device.BatteryLevel = request.BatteryLevel;
            device.NetworkType = request.NetworkType;
            device.PendingPayments = request.PendingPayments;
            device.TotalPaymentsToday = request.TotalPaymentsToday;

            // Notify dashboard about device status
            await _hubContext.Clients.All.SendAsync("MobileDeviceHeartbeat", new
            {
                device.DeviceId,
                device.DeviceName,
                device.IsOnline,
                device.LastHeartbeat,
                device.BatteryLevel,
                device.NetworkType,
                device.PendingPayments,
                device.TotalPaymentsToday,
                syncStatus = "synced"
            });

            return Ok(new
            {
                success = true,
                syncStatus = "synced",
                serverTime = DateTime.UtcNow,
                commands = GetPendingCommands(request.DeviceId)
            });
        }

        return NotFound(new { success = false, message = "Device not registered" });
    }

    /// <summary>
    /// Get all connected mobile devices
    /// </summary>
    [HttpGet("devices")]
    public IActionResult GetConnectedDevices()
    {
        var devices = _connectedDevices.Values
            .Select(d => new
            {
                d.DeviceId,
                d.DeviceName,
                d.Platform,
                d.AppVersion,
                d.IsOnline,
                d.ConnectedAt,
                d.LastSyncAt,
                d.LastHeartbeat,
                d.BatteryLevel,
                d.NetworkType,
                d.SmsMonitoringEnabled,
                d.AutoApproveEnabled,
                d.PendingPayments,
                d.TotalPaymentsToday,
                syncStatus = GetSyncStatus(d)
            })
            .ToList();

        return Ok(new
        {
            success = true,
            data = devices,
            totalDevices = devices.Count,
            onlineDevices = devices.Count(d => d.IsOnline)
        });
    }

    /// <summary>
    /// Get sync status for a specific device
    /// </summary>
    [HttpGet("status/{deviceId}")]
    public IActionResult GetDeviceStatus(string deviceId)
    {
        if (_connectedDevices.TryGetValue(deviceId, out var device))
        {
            return Ok(new
            {
                success = true,
                data = new
                {
                    device.DeviceId,
                    device.DeviceName,
                    device.IsOnline,
                    device.LastSyncAt,
                    device.LastHeartbeat,
                    syncStatus = GetSyncStatus(device),
                    device.SmsMonitoringEnabled,
                    device.AutoApproveEnabled,
                    device.PendingPayments,
                    device.TotalPaymentsToday
                }
            });
        }

        return NotFound(new { success = false, message = "Device not found" });
    }

    /// <summary>
    /// Disconnect a mobile device
    /// </summary>
    [HttpPost("disconnect")]
    public async Task<IActionResult> Disconnect([FromBody] DisconnectRequest request)
    {
        if (_connectedDevices.TryRemove(request.DeviceId, out var device))
        {
            device.IsOnline = false;

            await _hubContext.Clients.All.SendAsync("MobileDeviceDisconnected", new
            {
                device.DeviceId,
                device.DeviceName,
                disconnectedAt = DateTime.UtcNow
            });

            return Ok(new { success = true, message = "Device disconnected" });
        }

        return NotFound(new { success = false, message = "Device not found" });
    }

    /// <summary>
    /// Send command to mobile device
    /// </summary>
    [HttpPost("command")]
    public async Task<IActionResult> SendCommand([FromBody] MobileCommand command)
    {
        if (_connectedDevices.TryGetValue(command.DeviceId, out var device))
        {
            await _hubContext.Clients.All.SendAsync("MobileCommand", command);

            return Ok(new { success = true, message = "Command sent" });
        }

        return NotFound(new { success = false, message = "Device not found" });
    }

    private string GetSyncStatus(MobileDeviceInfo device)
    {
        if (!device.IsOnline) return "offline";
        if (!device.LastHeartbeat.HasValue) return "connecting";

        var timeSinceHeartbeat = DateTime.UtcNow - device.LastHeartbeat.Value;
        if (timeSinceHeartbeat.TotalSeconds < 30) return "synced";
        if (timeSinceHeartbeat.TotalMinutes < 5) return "syncing";
        return "disconnected";
    }

    private List<object> GetPendingCommands(string deviceId)
    {
        // Return any pending commands for this device
        return new List<object>();
    }
}

#region Models

public class MobileDeviceRegistration
{
    public string DeviceId { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public string Platform { get; set; } = "Android";
    public string AppVersion { get; set; } = "1.0.0";
    public bool SmsMonitoringEnabled { get; set; }
    public bool AutoApproveEnabled { get; set; }
}

public class HeartbeatRequest
{
    public string DeviceId { get; set; } = string.Empty;
    public int? BatteryLevel { get; set; }
    public string? NetworkType { get; set; }
    public int PendingPayments { get; set; }
    public decimal TotalPaymentsToday { get; set; }
}

public class DisconnectRequest
{
    public string DeviceId { get; set; } = string.Empty;
}

public class MobileCommand
{
    public string DeviceId { get; set; } = string.Empty;
    public string CommandType { get; set; } = string.Empty;
    public Dictionary<string, object>? Parameters { get; set; }
}

public class MobileDeviceInfo
{
    public string DeviceId { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public string AppVersion { get; set; } = string.Empty;
    public DateTime ConnectedAt { get; set; }
    public DateTime LastSyncAt { get; set; }
    public DateTime? LastHeartbeat { get; set; }
    public bool IsOnline { get; set; }
    public int? BatteryLevel { get; set; }
    public string? NetworkType { get; set; }
    public bool SmsMonitoringEnabled { get; set; }
    public bool AutoApproveEnabled { get; set; }
    public int PendingPayments { get; set; }
    public decimal TotalPaymentsToday { get; set; }
}

#endregion
