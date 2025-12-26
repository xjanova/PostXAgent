using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using AIManager.Mobile.Models;

namespace AIManager.Mobile.Services;

/// <summary>
/// Service for dispatching payment webhooks to configured websites
/// Implements sequential dispatching - stops when match found
/// </summary>
public class WebhookDispatchService : IWebhookDispatchService
{
    private readonly IWebsiteConfigService _websiteConfigService;
    private readonly IBankAccountService? _bankAccountService;
    private readonly HttpClient _httpClient;
    private readonly ConcurrentDictionary<string, UnmatchedPayment> _unmatchedPayments = new();
    private readonly ConcurrentDictionary<string, PaymentInfo> _pendingPayments = new();

    // Statistics
    private int _totalDispatched;
    private int _totalMatched;
    private int _totalUnmatched;
    private int _totalFailed;
    private readonly ConcurrentDictionary<string, int> _matchesByWebsite = new();
    private DateTime? _lastDispatchTime;
    private DateTime? _lastSmsCheck;

    public event EventHandler<WebhookDispatchResult>? PaymentMatched;
    public event EventHandler<UnmatchedPayment>? PaymentUnmatched;
    public event EventHandler<DispatchProgressEventArgs>? DispatchProgress;

    public WebhookDispatchService(
        IWebsiteConfigService websiteConfigService,
        IBankAccountService? bankAccountService = null)
    {
        _websiteConfigService = websiteConfigService;
        _bankAccountService = bankAccountService;
        _httpClient = new HttpClient();
    }

    public async Task<WebhookDispatchResult> DispatchPaymentAsync(PaymentInfo payment)
    {
        var result = new WebhookDispatchResult
        {
            PaymentId = payment.Id
        };

        // Get enabled websites by priority
        var websites = await _websiteConfigService.GetEnabledWebsitesByPriorityAsync();

        if (!websites.Any())
        {
            result.Attempts.Add(new WebhookAttempt
            {
                Success = false,
                Error = "ไม่มีเว็บไซต์ที่เปิดใช้งาน"
            });

            await AddToUnmatchedAsync(payment, result);
            return result;
        }

        _totalDispatched++;
        _lastDispatchTime = DateTime.UtcNow;
        _pendingPayments[payment.Id] = payment;

        // Create webhook payload
        var payload = await CreatePayloadAsync(payment);

        // Dispatch to websites sequentially (by priority)
        for (int i = 0; i < websites.Count; i++)
        {
            var website = websites[i];

            // Notify progress
            MainThread.BeginInvokeOnMainThread(() =>
            {
                DispatchProgress?.Invoke(this, new DispatchProgressEventArgs
                {
                    PaymentId = payment.Id,
                    CurrentWebsiteIndex = i + 1,
                    TotalWebsites = websites.Count,
                    CurrentWebsiteName = website.Name,
                    Status = $"กำลังส่งไปยัง {website.Name}...",
                    IsComplete = false
                });
            });

            var attempt = await SendWebhookAsync(website, payload);
            result.Attempts.Add(attempt);

            if (attempt.Success && attempt.Matched)
            {
                // Match found! Stop dispatching to other websites
                result.IsMatched = true;
                result.MatchedWebsiteId = website.Id;
                result.MatchedWebsiteName = website.Name;

                // Parse matched order from response if available
                // (This would be set in SendWebhookAsync)

                _totalMatched++;
                _matchesByWebsite.AddOrUpdate(website.Name, 1, (_, count) => count + 1);
                _pendingPayments.TryRemove(payment.Id, out _);

                // Notify match
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    PaymentMatched?.Invoke(this, result);
                    DispatchProgress?.Invoke(this, new DispatchProgressEventArgs
                    {
                        PaymentId = payment.Id,
                        CurrentWebsiteIndex = i + 1,
                        TotalWebsites = websites.Count,
                        CurrentWebsiteName = website.Name,
                        Status = $"จับคู่สำเร็จที่ {website.Name}!",
                        IsComplete = true
                    });
                });

                result.CompletedAt = DateTime.UtcNow;
                return result;
            }

            // If failed (not just no match), log and continue
            if (!attempt.Success)
            {
                await _websiteConfigService.UpdateWebsiteStatusAsync(
                    website.Id,
                    WebsiteConnectionStatus.Error,
                    false);
            }
        }

        // No match found in any website
        _totalUnmatched++;
        result.CompletedAt = DateTime.UtcNow;

        await AddToUnmatchedAsync(payment, result);

        // Notify unmatched
        MainThread.BeginInvokeOnMainThread(() =>
        {
            DispatchProgress?.Invoke(this, new DispatchProgressEventArgs
            {
                PaymentId = payment.Id,
                CurrentWebsiteIndex = websites.Count,
                TotalWebsites = websites.Count,
                Status = "ไม่พบ Order ที่ตรงกัน",
                IsComplete = true
            });
        });

        return result;
    }

    private async Task<WebhookAttempt> SendWebhookAsync(WebsiteConfig website, WebhookPayload payload)
    {
        var attempt = new WebhookAttempt
        {
            WebsiteId = website.Id,
            WebsiteName = website.Name,
            AttemptedAt = DateTime.UtcNow
        };

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var signature = website.GenerateSignature(json, payload.Timestamp);

            using var request = new HttpRequestMessage(HttpMethod.Post, website.WebhookUrl);
            request.Headers.Add("X-Api-Key", website.ApiKey);
            request.Headers.Add("X-Timestamp", payload.Timestamp.ToString());
            request.Headers.Add("X-Signature", signature);
            request.Headers.Add("X-Request-Id", payload.RequestId);
            request.Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(website.TimeoutSeconds));
            var response = await _httpClient.SendAsync(request, cts.Token);

            stopwatch.Stop();
            attempt.ResponseTimeMs = (int)stopwatch.ElapsedMilliseconds;

            if (response.IsSuccessStatusCode)
            {
                var responseJson = await response.Content.ReadAsStringAsync();
                var webhookResponse = JsonSerializer.Deserialize<WebhookResponse>(responseJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                attempt.Success = webhookResponse?.Success ?? false;
                attempt.Matched = webhookResponse?.Matched ?? false;

                if (!attempt.Success)
                {
                    attempt.Error = webhookResponse?.Error ?? "Unknown error";
                }

                // Update website status
                await _websiteConfigService.UpdateWebsiteStatusAsync(
                    website.Id,
                    WebsiteConnectionStatus.Connected,
                    true);
            }
            else
            {
                attempt.Success = false;
                attempt.Error = $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}";
            }
        }
        catch (TaskCanceledException)
        {
            stopwatch.Stop();
            attempt.ResponseTimeMs = (int)stopwatch.ElapsedMilliseconds;
            attempt.Success = false;
            attempt.Error = "Timeout";
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            attempt.ResponseTimeMs = (int)stopwatch.ElapsedMilliseconds;
            attempt.Success = false;
            attempt.Error = $"Connection error: {ex.Message}";
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            attempt.ResponseTimeMs = (int)stopwatch.ElapsedMilliseconds;
            attempt.Success = false;
            attempt.Error = $"Error: {ex.Message}";
            _totalFailed++;
        }

        return attempt;
    }

    private async Task<WebhookPayload> CreatePayloadAsync(PaymentInfo payment)
    {
        _lastSmsCheck = DateTime.UtcNow;

        var payload = new WebhookPayload
        {
            RequestId = Guid.NewGuid().ToString(),
            Event = "payment.received",
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Payment = new WebhookPaymentData
            {
                Amount = payment.Amount,
                Currency = payment.Currency,
                BankName = payment.BankName,
                AccountNumber = payment.AccountNumber,
                Reference = payment.Reference,
                SenderName = payment.SenderName,
                TransactionTime = payment.TransactionTime,
                RawSmsBody = payment.RawMessage,
                ConfidenceScore = payment.ConfidenceScore
            },
            Device = new WebhookDeviceInfo
            {
                DeviceId = DeviceInfo.Current.Model,
                DeviceName = DeviceInfo.Current.Name,
                AppVersion = AppInfo.Current.VersionString
            },
            Gateway = new WebhookGatewayStatus
            {
                IsOnline = true,
                LastSmsCheck = _lastSmsCheck,
                AppVersion = AppInfo.Current.VersionString
            }
        };

        // Add bank accounts info if service is available
        if (_bankAccountService != null)
        {
            var accountsStatus = await _bankAccountService.GetAccountsStatusAsync();
            payload.BankAccounts = new WebhookBankAccountsInfo
            {
                IsReady = accountsStatus.IsReady,
                EnabledCount = accountsStatus.EnabledAccountsCount,
                Accounts = accountsStatus.Accounts,
                NotReadyMessage = accountsStatus.IsReady ? null : accountsStatus.Message
            };
        }

        return payload;
    }

    private async Task AddToUnmatchedAsync(PaymentInfo payment, WebhookDispatchResult result)
    {
        var unmatched = new UnmatchedPayment
        {
            Id = Guid.NewGuid().ToString(),
            Payment = payment,
            DispatchResult = result,
            DetectedAt = DateTime.UtcNow
        };

        _unmatchedPayments[unmatched.Id] = unmatched;
        _pendingPayments.TryRemove(payment.Id, out _);

        MainThread.BeginInvokeOnMainThread(() =>
        {
            PaymentUnmatched?.Invoke(this, unmatched);
        });

        await Task.CompletedTask;
    }

    public Task<List<UnmatchedPayment>> GetUnmatchedPaymentsAsync()
    {
        var payments = _unmatchedPayments.Values
            .Where(p => !p.IsReviewed)
            .OrderByDescending(p => p.DetectedAt)
            .ToList();

        return Task.FromResult(payments);
    }

    public Task MarkAsReviewedAsync(string paymentId, string? notes = null)
    {
        if (_unmatchedPayments.TryGetValue(paymentId, out var payment))
        {
            payment.IsReviewed = true;
            payment.AdminNotes = notes;
        }

        return Task.CompletedTask;
    }

    public async Task<WebhookDispatchResult> RetryDispatchAsync(string paymentId)
    {
        if (_unmatchedPayments.TryRemove(paymentId, out var unmatched))
        {
            _totalUnmatched--;
            return await DispatchPaymentAsync(unmatched.Payment);
        }

        if (_pendingPayments.TryGetValue(paymentId, out var pending))
        {
            return await DispatchPaymentAsync(pending);
        }

        return new WebhookDispatchResult
        {
            PaymentId = paymentId,
            Attempts = new List<WebhookAttempt>
            {
                new() { Success = false, Error = "Payment not found" }
            }
        };
    }

    public Task<DispatchStatistics> GetStatisticsAsync()
    {
        var stats = new DispatchStatistics
        {
            TotalDispatched = _totalDispatched,
            TotalMatched = _totalMatched,
            TotalUnmatched = _totalUnmatched,
            TotalFailed = _totalFailed,
            MatchesByWebsite = _matchesByWebsite.ToDictionary(x => x.Key, x => x.Value),
            LastDispatchTime = _lastDispatchTime
        };

        return Task.FromResult(stats);
    }
}
