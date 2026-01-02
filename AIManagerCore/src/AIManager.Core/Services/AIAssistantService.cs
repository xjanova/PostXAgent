using System.Timers;
using Microsoft.Extensions.Logging;

namespace AIManager.Core.Services;

/// <summary>
/// AI Assistant Service - ให้คำแนะนำและรายงานสถานะระบบแบบอัตโนมัติ
/// แสดงผลผ่าน Status Bar ด้านล่างของ UI
/// </summary>
public class AIAssistantService : IDisposable
{
    private readonly ILogger<AIAssistantService>? _logger;
    private readonly System.Timers.Timer _messageTimer;
    private readonly System.Timers.Timer _healthReportTimer;
    private readonly Queue<AIMessage> _messageQueue = new();
    private readonly Random _random = new();
    private readonly object _lock = new();

    private AIMessage? _currentMessage;
    private bool _isRunning;
    private int _tasksCompletedToday;
    private int _tasksFailed;
    private DateTime _lastHealthReport = DateTime.MinValue;

    // Events
    public event Action<AIMessage>? OnNewMessage;
    public event Action? OnMessageCleared;

    // System state (updated externally)
    public double MemoryUsageMB { get; set; }
    public double CpuUsagePercent { get; set; }
    public int ActiveWorkers { get; set; }
    public int PendingTasks { get; set; }
    public bool IsOllamaOnline { get; set; }
    public bool IsComfyUIOnline { get; set; }
    public bool IsBackendOnline { get; set; }
    public string? CurrentModel { get; set; }

    public AIAssistantService(ILogger<AIAssistantService>? logger = null)
    {
        _logger = logger;

        // Message rotation timer (every 15 seconds)
        _messageTimer = new System.Timers.Timer(15000);
        _messageTimer.Elapsed += OnMessageTimerElapsed;

        // Health report timer (every 5 minutes)
        _healthReportTimer = new System.Timers.Timer(300000);
        _healthReportTimer.Elapsed += OnHealthReportTimerElapsed;
    }

    /// <summary>
    /// เริ่มการทำงานของ AI Assistant
    /// </summary>
    public void Start()
    {
        if (_isRunning) return;
        _isRunning = true;

        _messageTimer.Start();
        _healthReportTimer.Start();

        // ส่งข้อความต้อนรับ
        EnqueueMessage(new AIMessage
        {
            Type = AIMessageType.Greeting,
            Text = GetGreetingMessage(),
            Priority = AIMessagePriority.Normal,
            Icon = "Robot"
        });

        _logger?.LogInformation("AI Assistant started");
    }

    /// <summary>
    /// หยุดการทำงาน
    /// </summary>
    public void Stop()
    {
        _isRunning = false;
        _messageTimer.Stop();
        _healthReportTimer.Stop();
        _logger?.LogInformation("AI Assistant stopped");
    }

    /// <summary>
    /// เพิ่มข้อความเข้าคิว
    /// </summary>
    public void EnqueueMessage(AIMessage message)
    {
        lock (_lock)
        {
            // ถ้าเป็น High priority ให้แสดงทันที
            if (message.Priority == AIMessagePriority.High)
            {
                _currentMessage = message;
                OnNewMessage?.Invoke(message);
                return;
            }

            _messageQueue.Enqueue(message);
        }
    }

    /// <summary>
    /// แจ้งว่า Task สำเร็จ
    /// </summary>
    public void NotifyTaskCompleted(string platform, string taskType)
    {
        _tasksCompletedToday++;

        // แสดงข้อความชื่นชมเป็นระยะ
        if (_tasksCompletedToday % 5 == 0)
        {
            EnqueueMessage(new AIMessage
            {
                Type = AIMessageType.Celebration,
                Text = GetCelebrationMessage(_tasksCompletedToday),
                Priority = AIMessagePriority.Normal,
                Icon = "PartyPopper"
            });
        }
    }

    /// <summary>
    /// แจ้งว่า Task ล้มเหลว
    /// </summary>
    public void NotifyTaskFailed(string platform, string error)
    {
        _tasksFailed++;

        EnqueueMessage(new AIMessage
        {
            Type = AIMessageType.Warning,
            Text = $"พบปัญหา {platform}: {TruncateError(error)}",
            Priority = AIMessagePriority.High,
            Icon = "AlertCircle"
        });
    }

    /// <summary>
    /// แจ้งสถานะ Service เปลี่ยน
    /// </summary>
    public void NotifyServiceStatusChanged(string service, bool isOnline)
    {
        var status = isOnline ? "พร้อมใช้งาน" : "ออฟไลน์";
        var icon = isOnline ? "CheckCircle" : "AlertCircle";
        var priority = isOnline ? AIMessagePriority.Low : AIMessagePriority.High;

        EnqueueMessage(new AIMessage
        {
            Type = isOnline ? AIMessageType.SystemStatus : AIMessageType.Warning,
            Text = $"{service} {status}",
            Priority = priority,
            Icon = icon
        });
    }

    private void OnMessageTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        lock (_lock)
        {
            // ถ้ามีข้อความในคิว ให้แสดง
            if (_messageQueue.Count > 0)
            {
                _currentMessage = _messageQueue.Dequeue();
                OnNewMessage?.Invoke(_currentMessage);
                return;
            }

            // ถ้าไม่มี ให้สร้างข้อความใหม่ตามสถานะ
            var newMessage = GenerateContextualMessage();
            if (newMessage != null)
            {
                _currentMessage = newMessage;
                OnNewMessage?.Invoke(newMessage);
            }
        }
    }

    private void OnHealthReportTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        var report = GenerateHealthReport();
        EnqueueMessage(new AIMessage
        {
            Type = AIMessageType.SystemStatus,
            Text = report,
            Priority = AIMessagePriority.Normal,
            Icon = "ChartLine"
        });
        _lastHealthReport = DateTime.Now;
    }

    private AIMessage? GenerateContextualMessage()
    {
        // ตรวจสอบปัญหาก่อน
        if (MemoryUsageMB > 8000)
        {
            return new AIMessage
            {
                Type = AIMessageType.Warning,
                Text = $"RAM สูง ({MemoryUsageMB:N0} MB) แนะนำปิดโปรแกรมที่ไม่ใช้",
                Priority = AIMessagePriority.Normal,
                Icon = "Memory"
            };
        }

        if (CpuUsagePercent > 90)
        {
            return new AIMessage
            {
                Type = AIMessageType.Warning,
                Text = $"CPU ทำงานหนัก ({CpuUsagePercent:N0}%) รอสักครู่...",
                Priority = AIMessagePriority.Normal,
                Icon = "Cpu"
            };
        }

        if (!IsOllamaOnline && !IsComfyUIOnline)
        {
            return new AIMessage
            {
                Type = AIMessageType.Suggestion,
                Text = "AI Services ออฟไลน์ - ไปที่ Settings เพื่อเปิดใช้งาน",
                Priority = AIMessagePriority.Normal,
                Icon = "Cog"
            };
        }

        // ข้อความทั่วไป
        if (PendingTasks > 0)
        {
            return new AIMessage
            {
                Type = AIMessageType.SystemStatus,
                Text = $"มี {PendingTasks} งานรอดำเนินการ | {ActiveWorkers} workers ทำงานอยู่",
                Priority = AIMessagePriority.Low,
                Icon = "ListCheck"
            };
        }

        // Tips แบบสุ่ม
        return GetRandomTip();
    }

    private string GenerateHealthReport()
    {
        var parts = new List<string>();

        // Service status
        if (IsOllamaOnline) parts.Add("Ollama OK");
        if (IsComfyUIOnline) parts.Add("ComfyUI OK");
        if (IsBackendOnline) parts.Add("Backend OK");

        // Tasks
        if (_tasksCompletedToday > 0)
            parts.Add($"{_tasksCompletedToday} tasks done");

        if (_tasksFailed > 0)
            parts.Add($"{_tasksFailed} errors");

        // Memory
        parts.Add($"RAM {MemoryUsageMB:N0}MB");

        return $"รายงาน: {string.Join(" | ", parts)}";
    }

    private string GetGreetingMessage()
    {
        var hour = DateTime.Now.Hour;
        var greeting = hour switch
        {
            < 12 => "สวัสดีตอนเช้า",
            < 17 => "สวัสดีตอนบ่าย",
            < 21 => "สวัสดีตอนเย็น",
            _ => "สวัสดีตอนดึก"
        };

        if (PendingTasks > 0)
            return $"{greeting}! มี {PendingTasks} งานรอดำเนินการ";

        return $"{greeting}! ระบบพร้อมใช้งานแล้ว";
    }

    private string GetCelebrationMessage(int count)
    {
        var messages = new[]
        {
            $"ยอดเยี่ยม! ทำไปแล้ว {count} tasks วันนี้",
            $"ดีมาก! {count} งานสำเร็จแล้ว",
            $"เก่งมาก! ครบ {count} tasks แล้ว",
            $"สุดยอด! {count} งานเสร็จสมบูรณ์"
        };
        return messages[_random.Next(messages.Length)];
    }

    private AIMessage GetRandomTip()
    {
        var tips = new[]
        {
            ("ใช้ Qwen 2.5 สำหรับภาษาไทย ผลลัพธ์ดีกว่า Llama", "Lightbulb"),
            ("กด Ctrl+N เพื่อสร้าง Task ใหม่อย่างรวดเร็ว", "Keyboard"),
            ("ตั้งเวลาโพสต์ล่วงหน้าได้ใน Scheduler", "Clock"),
            ("ดู Analytics ได้ที่ Dashboard", "ChartBar"),
            ("เชื่อมต่อ Social Accounts ได้ที่ Platforms", "Share"),
            ("ComfyUI ช่วยสร้างรูปภาพได้ ลองเปิดใช้งานดู", "Image"),
            ("ใช้ AI Chat สำหรับถามคำถามเกี่ยวกับระบบ", "MessageCircle"),
            ("Workflow Editor ช่วยสร้าง automation ได้", "GitBranch"),
            ("ระบบพร้อมใช้งาน รอคำสั่งจากคุณ", "CheckCircle")
        };

        var (text, icon) = tips[_random.Next(tips.Length)];
        return new AIMessage
        {
            Type = AIMessageType.Tip,
            Text = text,
            Priority = AIMessagePriority.Low,
            Icon = icon
        };
    }

    private static string TruncateError(string error)
    {
        if (string.IsNullOrEmpty(error)) return "Unknown error";
        return error.Length > 50 ? error[..47] + "..." : error;
    }

    public void Dispose()
    {
        Stop();
        _messageTimer.Dispose();
        _healthReportTimer.Dispose();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// ข้อความจาก AI Assistant
/// </summary>
public class AIMessage
{
    public AIMessageType Type { get; set; }
    public string Text { get; set; } = "";
    public AIMessagePriority Priority { get; set; } = AIMessagePriority.Normal;
    public string Icon { get; set; } = "Robot";
    public DateTime Timestamp { get; set; } = DateTime.Now;
}

/// <summary>
/// ประเภทข้อความ AI Assistant
/// </summary>
public enum AIMessageType
{
    Greeting,       // ทักทาย
    Tip,            // เคล็ดลับ
    Warning,        // คำเตือน
    Celebration,    // ยินดี
    Suggestion,     // แนะนำ
    SystemStatus    // สถานะระบบ
}

/// <summary>
/// ความสำคัญของข้อความ AI Assistant
/// </summary>
public enum AIMessagePriority
{
    Low,
    Normal,
    High
}
