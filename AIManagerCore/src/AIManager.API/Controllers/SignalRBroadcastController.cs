using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using AIManager.API.Hubs;

namespace AIManager.API.Controllers;

/// <summary>
/// Controller for broadcasting messages via SignalR from external services (Laravel)
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class SignalRBroadcastController : ControllerBase
{
    private readonly IHubContext<AIManagerHub> _hubContext;
    private readonly ILogger<SignalRBroadcastController> _logger;

    public SignalRBroadcastController(
        IHubContext<AIManagerHub> hubContext,
        ILogger<SignalRBroadcastController> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    /// <summary>
    /// Broadcast a message to all connected clients
    /// </summary>
    [HttpPost("broadcast")]
    public async Task<IActionResult> Broadcast([FromBody] BroadcastRequest request)
    {
        if (string.IsNullOrEmpty(request.Method))
        {
            return BadRequest(new { success = false, message = "Method is required" });
        }

        try
        {
            await _hubContext.Clients.All.SendAsync(request.Method, request.Data);

            _logger.LogInformation("Broadcast message sent: {Method}", request.Method);

            return Ok(new
            {
                success = true,
                message = "Message broadcast successfully",
                method = request.Method
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast message: {Method}", request.Method);
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// Broadcast a message to a specific group
    /// </summary>
    [HttpPost("broadcast-group")]
    public async Task<IActionResult> BroadcastToGroup([FromBody] GroupBroadcastRequest request)
    {
        if (string.IsNullOrEmpty(request.GroupName) || string.IsNullOrEmpty(request.Method))
        {
            return BadRequest(new { success = false, message = "GroupName and Method are required" });
        }

        try
        {
            await _hubContext.Clients.Group(request.GroupName).SendAsync(request.Method, request.Data);

            _logger.LogInformation("Group broadcast sent: {Method} to {Group}",
                request.Method, request.GroupName);

            return Ok(new
            {
                success = true,
                message = "Group message broadcast successfully",
                method = request.Method,
                group = request.GroupName
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast to group: {Group}", request.GroupName);
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// Broadcast payment notification
    /// </summary>
    [HttpPost("payment-notification")]
    public async Task<IActionResult> PaymentNotification([FromBody] PaymentNotificationRequest request)
    {
        try
        {
            await _hubContext.Clients.All.SendAsync(request.EventType, new
            {
                paymentId = request.PaymentId,
                amount = request.Amount,
                bankName = request.BankName,
                status = request.Status,
                timestamp = DateTime.UtcNow
            });

            _logger.LogInformation("Payment notification sent: {EventType} - {PaymentId}",
                request.EventType, request.PaymentId);

            return Ok(new { success = true, message = "Payment notification sent" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send payment notification");
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// Broadcast mobile device status update
    /// </summary>
    [HttpPost("mobile-status")]
    public async Task<IActionResult> MobileDeviceStatus([FromBody] MobileStatusRequest request)
    {
        try
        {
            await _hubContext.Clients.All.SendAsync(request.EventType, new
            {
                deviceId = request.DeviceId,
                deviceName = request.DeviceName,
                isOnline = request.IsOnline,
                syncStatus = request.SyncStatus,
                batteryLevel = request.BatteryLevel,
                timestamp = DateTime.UtcNow
            });

            _logger.LogDebug("Mobile status broadcast: {DeviceId} - {Status}",
                request.DeviceId, request.SyncStatus);

            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast mobile status");
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }
}

#region Request Models

public class BroadcastRequest
{
    public string Method { get; set; } = string.Empty;
    public object? Data { get; set; }
}

public class GroupBroadcastRequest
{
    public string GroupName { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public object? Data { get; set; }
}

public class PaymentNotificationRequest
{
    public string EventType { get; set; } = "PaymentReceived"; // PaymentReceived, PaymentApproved, PaymentRejected
    public string PaymentId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string BankName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public class MobileStatusRequest
{
    public string EventType { get; set; } = "MobileDeviceHeartbeat";
    public string DeviceId { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public bool IsOnline { get; set; }
    public string SyncStatus { get; set; } = string.Empty;
    public int? BatteryLevel { get; set; }
}

#endregion
