using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using AIManager.API.Hubs;
using System.Collections.Concurrent;

namespace AIManager.API.Controllers;

/// <summary>
/// Payment Gateway Controller - รับการชำระเงินจาก SMS Detection
/// ทำงานร่วมกับ Mobile App เพื่อรับยอดเงินโดยไม่ต้องขอ API ธนาคาร
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class PaymentGatewayController : ControllerBase
{
    private readonly IHubContext<AIManagerHub> _hubContext;
    private readonly ILogger<PaymentGatewayController> _logger;
    private static readonly ConcurrentDictionary<string, SmsPayment> _payments = new();
    private static readonly ConcurrentDictionary<string, PaymentOrder> _pendingOrders = new();

    public PaymentGatewayController(
        IHubContext<AIManagerHub> hubContext,
        ILogger<PaymentGatewayController> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    #region Payment Processing

    /// <summary>
    /// Submit payment detected from SMS
    /// </summary>
    [HttpPost("submit")]
    public async Task<IActionResult> SubmitPayment([FromBody] SmsPaymentSubmission request)
    {
        var payment = new SmsPayment
        {
            Id = Guid.NewGuid().ToString(),
            DeviceId = request.DeviceId,
            BankName = request.BankName,
            AccountNumber = request.AccountNumber,
            Amount = request.Amount,
            Currency = request.Currency ?? "THB",
            Type = request.Type,
            TransactionTime = request.TransactionTime,
            Reference = request.Reference,
            SenderName = request.SenderName,
            RawMessage = request.RawMessage,
            ConfidenceScore = request.ConfidenceScore,
            Status = PaymentStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        _payments[payment.Id] = payment;

        _logger.LogInformation("Payment submitted: {PaymentId} - {Amount} {Currency} from {Bank}",
            payment.Id, payment.Amount, payment.Currency, payment.BankName);

        // Try to match with pending order
        var matchedOrder = TryMatchPayment(payment);
        if (matchedOrder != null)
        {
            payment.MatchedOrderId = matchedOrder.Id;
            payment.Status = PaymentStatus.Matched;

            // Notify about matched payment
            await _hubContext.Clients.All.SendAsync("PaymentMatched", new
            {
                payment,
                order = matchedOrder
            });
        }

        // Notify all clients about new payment
        await _hubContext.Clients.All.SendAsync("PaymentReceived", payment);

        return Ok(new
        {
            success = true,
            message = matchedOrder != null ? "พบคำสั่งซื้อที่ตรงกัน" : "รอการตรวจสอบ",
            data = new
            {
                paymentId = payment.Id,
                status = payment.Status.ToString(),
                matchedOrderId = payment.MatchedOrderId
            }
        });
    }

    /// <summary>
    /// Approve payment
    /// </summary>
    [HttpPost("{paymentId}/approve")]
    public async Task<IActionResult> ApprovePayment(string paymentId, [FromBody] ApprovePaymentRequest? request = null)
    {
        if (!_payments.TryGetValue(paymentId, out var payment))
        {
            return NotFound(new { success = false, message = "ไม่พบรายการชำระเงิน" });
        }

        if (payment.Status == PaymentStatus.Approved)
        {
            return BadRequest(new { success = false, message = "รายการนี้ได้รับการอนุมัติแล้ว" });
        }

        payment.Status = PaymentStatus.Approved;
        payment.ApprovedAt = DateTime.UtcNow;
        payment.ApprovedBy = request?.ApprovedBy ?? "system";
        payment.Notes = request?.Notes;

        _logger.LogInformation("Payment approved: {PaymentId} by {ApprovedBy}",
            paymentId, payment.ApprovedBy);

        // If matched to an order, update order status
        if (!string.IsNullOrEmpty(payment.MatchedOrderId) &&
            _pendingOrders.TryGetValue(payment.MatchedOrderId, out var order))
        {
            order.Status = OrderStatus.Paid;
            order.PaidAt = DateTime.UtcNow;
            order.PaymentId = paymentId;

            await _hubContext.Clients.All.SendAsync("OrderPaid", order);
        }

        await _hubContext.Clients.All.SendAsync("PaymentApproved", payment);

        return Ok(new
        {
            success = true,
            message = "อนุมัติการชำระเงินเรียบร้อย",
            data = payment
        });
    }

    /// <summary>
    /// Reject payment
    /// </summary>
    [HttpPost("{paymentId}/reject")]
    public async Task<IActionResult> RejectPayment(string paymentId, [FromBody] RejectPaymentRequest request)
    {
        if (!_payments.TryGetValue(paymentId, out var payment))
        {
            return NotFound(new { success = false, message = "ไม่พบรายการชำระเงิน" });
        }

        payment.Status = PaymentStatus.Rejected;
        payment.RejectedAt = DateTime.UtcNow;
        payment.RejectedBy = request.RejectedBy;
        payment.RejectReason = request.Reason;

        _logger.LogInformation("Payment rejected: {PaymentId} - Reason: {Reason}",
            paymentId, request.Reason);

        await _hubContext.Clients.All.SendAsync("PaymentRejected", payment);

        return Ok(new
        {
            success = true,
            message = "ปฏิเสธการชำระเงินเรียบร้อย",
            data = payment
        });
    }

    /// <summary>
    /// Verify payment manually
    /// </summary>
    [HttpPost("{paymentId}/verify")]
    public async Task<IActionResult> VerifyPayment(string paymentId)
    {
        if (!_payments.TryGetValue(paymentId, out var payment))
        {
            return NotFound(new { success = false, message = "ไม่พบรายการชำระเงิน" });
        }

        payment.Status = PaymentStatus.Verified;
        payment.VerifiedAt = DateTime.UtcNow;

        await _hubContext.Clients.All.SendAsync("PaymentVerified", payment);

        return Ok(new
        {
            success = true,
            message = "ตรวจสอบการชำระเงินเรียบร้อย",
            data = payment
        });
    }

    #endregion

    #region Order Management

    /// <summary>
    /// Create payment order (waiting for SMS payment)
    /// </summary>
    [HttpPost("orders")]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request)
    {
        var order = new PaymentOrder
        {
            Id = Guid.NewGuid().ToString(),
            OrderNumber = GenerateOrderNumber(),
            UserId = request.UserId,
            Amount = request.Amount,
            Currency = request.Currency ?? "THB",
            Description = request.Description,
            Status = OrderStatus.Pending,
            ExpiresAt = DateTime.UtcNow.AddMinutes(request.ExpiryMinutes ?? 30),
            CreatedAt = DateTime.UtcNow,
            Metadata = request.Metadata
        };

        _pendingOrders[order.Id] = order;

        _logger.LogInformation("Order created: {OrderId} - {Amount} {Currency}",
            order.Id, order.Amount, order.Currency);

        await _hubContext.Clients.All.SendAsync("OrderCreated", order);

        return Ok(new
        {
            success = true,
            message = "สร้างคำสั่งซื้อสำเร็จ รอการชำระเงิน",
            data = order
        });
    }

    /// <summary>
    /// Get order status
    /// </summary>
    [HttpGet("orders/{orderId}")]
    public IActionResult GetOrder(string orderId)
    {
        if (!_pendingOrders.TryGetValue(orderId, out var order))
        {
            return NotFound(new { success = false, message = "ไม่พบคำสั่งซื้อ" });
        }

        return Ok(new { success = true, data = order });
    }

    /// <summary>
    /// Match payment to order manually
    /// </summary>
    [HttpPost("orders/{orderId}/match")]
    public async Task<IActionResult> MatchPaymentToOrder(string orderId, [FromBody] MatchPaymentRequest request)
    {
        if (!_pendingOrders.TryGetValue(orderId, out var order))
        {
            return NotFound(new { success = false, message = "ไม่พบคำสั่งซื้อ" });
        }

        if (!_payments.TryGetValue(request.PaymentId, out var payment))
        {
            return NotFound(new { success = false, message = "ไม่พบรายการชำระเงิน" });
        }

        // Match them
        payment.MatchedOrderId = orderId;
        payment.Status = PaymentStatus.Matched;
        order.PaymentId = payment.Id;
        order.Status = OrderStatus.Matched;

        await _hubContext.Clients.All.SendAsync("PaymentMatched", new { payment, order });

        return Ok(new
        {
            success = true,
            message = "จับคู่การชำระเงินกับคำสั่งซื้อสำเร็จ",
            data = new { payment, order }
        });
    }

    #endregion

    #region Query Endpoints

    /// <summary>
    /// Get all payments
    /// </summary>
    [HttpGet("payments")]
    public IActionResult GetPayments(
        [FromQuery] string? status = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] int page = 1,
        [FromQuery] int limit = 50)
    {
        var query = _payments.Values.AsEnumerable();

        if (!string.IsNullOrEmpty(status) && Enum.TryParse<PaymentStatus>(status, true, out var statusEnum))
        {
            query = query.Where(p => p.Status == statusEnum);
        }

        if (from.HasValue)
        {
            query = query.Where(p => p.CreatedAt >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(p => p.CreatedAt <= to.Value);
        }

        var total = query.Count();
        var payments = query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * limit)
            .Take(limit)
            .ToList();

        return Ok(new
        {
            success = true,
            data = payments,
            pagination = new
            {
                page,
                limit,
                total,
                totalPages = (int)Math.Ceiling(total / (double)limit)
            }
        });
    }

    /// <summary>
    /// Get payment statistics
    /// </summary>
    [HttpGet("stats")]
    public IActionResult GetStats([FromQuery] DateTime? date = null)
    {
        var targetDate = date?.Date ?? DateTime.Today;
        var payments = _payments.Values.Where(p => p.CreatedAt.Date == targetDate).ToList();

        return Ok(new
        {
            success = true,
            data = new
            {
                date = targetDate,
                totalPayments = payments.Count,
                pendingPayments = payments.Count(p => p.Status == PaymentStatus.Pending),
                approvedPayments = payments.Count(p => p.Status == PaymentStatus.Approved),
                rejectedPayments = payments.Count(p => p.Status == PaymentStatus.Rejected),
                totalAmountApproved = payments.Where(p => p.Status == PaymentStatus.Approved).Sum(p => p.Amount),
                totalAmountPending = payments.Where(p => p.Status == PaymentStatus.Pending).Sum(p => p.Amount),
                byBank = payments.GroupBy(p => p.BankName).Select(g => new
                {
                    bank = g.Key,
                    count = g.Count(),
                    total = g.Sum(p => p.Amount)
                }).ToList()
            }
        });
    }

    /// <summary>
    /// Get pending orders
    /// </summary>
    [HttpGet("orders")]
    public IActionResult GetOrders([FromQuery] string? status = null)
    {
        var query = _pendingOrders.Values.AsEnumerable();

        if (!string.IsNullOrEmpty(status) && Enum.TryParse<OrderStatus>(status, true, out var statusEnum))
        {
            query = query.Where(o => o.Status == statusEnum);
        }

        // Remove expired orders
        var now = DateTime.UtcNow;
        foreach (var order in _pendingOrders.Values.Where(o => o.ExpiresAt < now && o.Status == OrderStatus.Pending))
        {
            order.Status = OrderStatus.Expired;
        }

        var orders = query.OrderByDescending(o => o.CreatedAt).ToList();

        return Ok(new { success = true, data = orders });
    }

    #endregion

    #region Helper Methods

    private PaymentOrder? TryMatchPayment(SmsPayment payment)
    {
        // Find pending order with matching amount
        var matchingOrder = _pendingOrders.Values
            .Where(o => o.Status == OrderStatus.Pending)
            .Where(o => o.ExpiresAt > DateTime.UtcNow)
            .Where(o => Math.Abs(o.Amount - payment.Amount) < 0.01m) // Match amount
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefault();

        return matchingOrder;
    }

    private string GenerateOrderNumber()
    {
        return $"ORD-{DateTime.Now:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpper()}";
    }

    #endregion
}

#region Models

public class SmsPaymentSubmission
{
    public string DeviceId { get; set; } = string.Empty;
    public string BankName { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? Currency { get; set; }
    public string Type { get; set; } = "incoming";
    public DateTime TransactionTime { get; set; }
    public string? Reference { get; set; }
    public string? SenderName { get; set; }
    public string RawMessage { get; set; } = string.Empty;
    public double ConfidenceScore { get; set; }
}

public class SmsPayment
{
    public string Id { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public string BankName { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "THB";
    public string Type { get; set; } = string.Empty;
    public DateTime TransactionTime { get; set; }
    public string? Reference { get; set; }
    public string? SenderName { get; set; }
    public string RawMessage { get; set; } = string.Empty;
    public double ConfidenceScore { get; set; }
    public PaymentStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? MatchedOrderId { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTime? RejectedAt { get; set; }
    public string? RejectedBy { get; set; }
    public string? RejectReason { get; set; }
    public string? Notes { get; set; }
}

public enum PaymentStatus
{
    Pending,
    Verified,
    Matched,
    Approved,
    Rejected,
    Expired
}

public class ApprovePaymentRequest
{
    public string? ApprovedBy { get; set; }
    public string? Notes { get; set; }
}

public class RejectPaymentRequest
{
    public string RejectedBy { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}

public class CreateOrderRequest
{
    public int UserId { get; set; }
    public decimal Amount { get; set; }
    public string? Currency { get; set; }
    public string? Description { get; set; }
    public int? ExpiryMinutes { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}

public class PaymentOrder
{
    public string Id { get; set; } = string.Empty;
    public string OrderNumber { get; set; } = string.Empty;
    public int UserId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "THB";
    public string? Description { get; set; }
    public OrderStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? PaidAt { get; set; }
    public string? PaymentId { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}

public enum OrderStatus
{
    Pending,
    Matched,
    Paid,
    Expired,
    Cancelled
}

public class MatchPaymentRequest
{
    public string PaymentId { get; set; } = string.Empty;
}

#endregion
