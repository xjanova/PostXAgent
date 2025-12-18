using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace AIManager.Core.Services;

/// <summary>
/// Ollama Chat Service สำหรับการสนทนากับ AI
/// - รองรับ streaming responses
/// - จัดการ conversation history
/// - รองรับหลาย models
/// </summary>
public class OllamaChatService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OllamaChatService>? _logger;
    private readonly List<ChatMessage> _conversationHistory = new();

    public string BaseUrl { get; set; } = "http://localhost:11434";
    public string CurrentModel { get; set; } = "llama3.2:3b";
    public bool KeepHistory { get; set; } = true;

    /// <summary>
    /// Event สำหรับ streaming response
    /// </summary>
    public event EventHandler<string>? OnStreamToken;

    /// <summary>
    /// Event เมื่อ generation เสร็จสิ้น
    /// </summary>
    public event EventHandler<ChatResponse>? OnResponseComplete;

    public OllamaChatService(ILogger<OllamaChatService>? logger = null)
    {
        _httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        _logger = logger;

        var envUrl = Environment.GetEnvironmentVariable("OLLAMA_BASE_URL");
        if (!string.IsNullOrEmpty(envUrl))
        {
            BaseUrl = envUrl;
        }

        var envModel = Environment.GetEnvironmentVariable("OLLAMA_MODEL");
        if (!string.IsNullOrEmpty(envModel) && envModel != "auto")
        {
            CurrentModel = envModel;
        }
    }

    /// <summary>
    /// ส่งข้อความและรับการตอบกลับแบบ streaming
    /// </summary>
    public async Task<string> ChatStreamAsync(
        string message,
        CancellationToken ct = default)
    {
        // Add user message to history
        _conversationHistory.Add(new ChatMessage { Role = "user", Content = message });

        var request = new
        {
            model = CurrentModel,
            messages = _conversationHistory.Select(m => new { role = m.Role, content = m.Content }).ToArray(),
            stream = true
        };

        var response = new StringBuilder();

        try
        {
            using var httpResponse = await _httpClient.PostAsJsonAsync(
                $"{BaseUrl}/api/chat",
                request,
                ct);

            if (!httpResponse.IsSuccessStatusCode)
            {
                var error = await httpResponse.Content.ReadAsStringAsync(ct);
                _logger?.LogError("Ollama chat failed: {Error}", error);
                throw new Exception($"Ollama error: {error}");
            }

            using var stream = await httpResponse.Content.ReadAsStreamAsync(ct);
            using var reader = new StreamReader(stream);

            while (!reader.EndOfStream && !ct.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(ct);
                if (string.IsNullOrEmpty(line)) continue;

                try
                {
                    var json = JsonDocument.Parse(line);
                    var root = json.RootElement;

                    if (root.TryGetProperty("message", out var msgElement) &&
                        msgElement.TryGetProperty("content", out var contentElement))
                    {
                        var token = contentElement.GetString() ?? "";
                        response.Append(token);
                        OnStreamToken?.Invoke(this, token);
                    }

                    if (root.TryGetProperty("done", out var doneElement) && doneElement.GetBoolean())
                    {
                        var chatResponse = new ChatResponse
                        {
                            Content = response.ToString(),
                            Model = CurrentModel,
                            TotalDuration = root.TryGetProperty("total_duration", out var td) ? td.GetInt64() : 0,
                            EvalCount = root.TryGetProperty("eval_count", out var ec) ? ec.GetInt32() : 0
                        };

                        // Add assistant response to history
                        if (KeepHistory)
                        {
                            _conversationHistory.Add(new ChatMessage
                            {
                                Role = "assistant",
                                Content = response.ToString()
                            });
                        }

                        OnResponseComplete?.Invoke(this, chatResponse);
                        break;
                    }
                }
                catch (JsonException)
                {
                    // Skip invalid JSON lines
                }
            }

            return response.ToString();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Chat stream error");
            throw;
        }
    }

    /// <summary>
    /// ส่งข้อความและรอรับการตอบกลับทั้งหมด (ไม่ stream)
    /// </summary>
    public async Task<ChatResponse> ChatAsync(
        string message,
        CancellationToken ct = default)
    {
        _conversationHistory.Add(new ChatMessage { Role = "user", Content = message });

        var request = new
        {
            model = CurrentModel,
            messages = _conversationHistory.Select(m => new { role = m.Role, content = m.Content }).ToArray(),
            stream = false
        };

        var response = await _httpClient.PostAsJsonAsync($"{BaseUrl}/api/chat", request, ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            throw new Exception($"Ollama error: {error}");
        }

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        var content = json.GetProperty("message").GetProperty("content").GetString() ?? "";

        var result = new ChatResponse
        {
            Content = content,
            Model = CurrentModel,
            TotalDuration = json.TryGetProperty("total_duration", out var td) ? td.GetInt64() : 0,
            EvalCount = json.TryGetProperty("eval_count", out var ec) ? ec.GetInt32() : 0
        };

        if (KeepHistory)
        {
            _conversationHistory.Add(new ChatMessage { Role = "assistant", Content = content });
        }

        return result;
    }

    /// <summary>
    /// Generate text (ไม่มี chat history)
    /// </summary>
    public async Task<string> GenerateAsync(
        string prompt,
        CancellationToken ct = default)
    {
        var request = new
        {
            model = CurrentModel,
            prompt,
            stream = false
        };

        var response = await _httpClient.PostAsJsonAsync($"{BaseUrl}/api/generate", request, ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            throw new Exception($"Ollama error: {error}");
        }

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        return json.GetProperty("response").GetString() ?? "";
    }

    /// <summary>
    /// ดึงรายการ models ที่มี
    /// </summary>
    public async Task<List<OllamaModelInfo>> GetModelsAsync(CancellationToken ct = default)
    {
        var response = await _httpClient.GetAsync($"{BaseUrl}/api/tags", ct);

        if (!response.IsSuccessStatusCode)
            return new List<OllamaModelInfo>();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        var models = new List<OllamaModelInfo>();

        if (json.TryGetProperty("models", out var modelsArray))
        {
            foreach (var model in modelsArray.EnumerateArray())
            {
                models.Add(new OllamaModelInfo
                {
                    Name = model.GetProperty("name").GetString() ?? "",
                    Size = model.TryGetProperty("size", out var s) ? s.GetInt64() : 0,
                    ModifiedAt = model.TryGetProperty("modified_at", out var m) ? m.GetString() : ""
                });
            }
        }

        return models;
    }

    /// <summary>
    /// ล้างประวัติการสนทนา
    /// </summary>
    public void ClearHistory()
    {
        _conversationHistory.Clear();
        _logger?.LogInformation("Chat history cleared");
    }

    /// <summary>
    /// ดึงประวัติการสนทนา
    /// </summary>
    public IReadOnlyList<ChatMessage> GetHistory() => _conversationHistory.AsReadOnly();

    /// <summary>
    /// ตั้งค่า system prompt
    /// </summary>
    public void SetSystemPrompt(string systemPrompt)
    {
        // Remove existing system message
        _conversationHistory.RemoveAll(m => m.Role == "system");

        // Add new system message at the beginning
        _conversationHistory.Insert(0, new ChatMessage
        {
            Role = "system",
            Content = systemPrompt
        });
    }

    /// <summary>
    /// Chat พร้อมรูปภาพ (ใช้ vision models เช่น llava, llama3.2-vision)
    /// </summary>
    public async Task<string> ChatWithImageAsync(
        string message,
        string imageBase64,
        CancellationToken ct = default)
    {
        // Add user message with image to history
        _conversationHistory.Add(new ChatMessage
        {
            Role = "user",
            Content = message,
            Images = new List<string> { imageBase64 },
            Type = MessageType.Image
        });

        var request = new
        {
            model = CurrentModel,
            messages = _conversationHistory.Select(m => new
            {
                role = m.Role,
                content = m.Content,
                images = m.Images.Count > 0 ? m.Images.ToArray() : null
            }).ToArray(),
            stream = true
        };

        var response = new StringBuilder();

        try
        {
            using var httpResponse = await _httpClient.PostAsJsonAsync(
                $"{BaseUrl}/api/chat",
                request,
                ct);

            if (!httpResponse.IsSuccessStatusCode)
            {
                var error = await httpResponse.Content.ReadAsStringAsync(ct);
                throw new Exception($"Ollama error: {error}");
            }

            using var stream = await httpResponse.Content.ReadAsStreamAsync(ct);
            using var reader = new StreamReader(stream);

            while (!reader.EndOfStream && !ct.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(ct);
                if (string.IsNullOrEmpty(line)) continue;

                try
                {
                    var json = JsonDocument.Parse(line);
                    var root = json.RootElement;

                    if (root.TryGetProperty("message", out var msgElement) &&
                        msgElement.TryGetProperty("content", out var contentElement))
                    {
                        var token = contentElement.GetString() ?? "";
                        response.Append(token);
                        OnStreamToken?.Invoke(this, token);
                    }

                    if (root.TryGetProperty("done", out var doneElement) && doneElement.GetBoolean())
                    {
                        if (KeepHistory)
                        {
                            _conversationHistory.Add(new ChatMessage
                            {
                                Role = "assistant",
                                Content = response.ToString()
                            });
                        }
                        break;
                    }
                }
                catch (JsonException) { }
            }

            return response.ToString();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Chat with image error");
            throw;
        }
    }

    /// <summary>
    /// ค้นหาเว็บและนำผลมาถามต่อ AI
    /// </summary>
    public async Task<WebSearchResult> SearchWebAsync(
        string query,
        CancellationToken ct = default)
    {
        var result = new WebSearchResult { Query = query };

        try
        {
            // Use DuckDuckGo Instant Answer API (free, no API key needed)
            var encodedQuery = Uri.EscapeDataString(query);
            var url = $"https://api.duckduckgo.com/?q={encodedQuery}&format=json&no_html=1";

            var response = await _httpClient.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
            {
                result.Error = "Search failed";
                return result;
            }

            var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);

            // Get abstract/answer
            if (json.TryGetProperty("Abstract", out var abstractEl))
            {
                result.Abstract = abstractEl.GetString() ?? "";
            }

            if (json.TryGetProperty("AbstractSource", out var sourceEl))
            {
                result.Source = sourceEl.GetString() ?? "";
            }

            if (json.TryGetProperty("AbstractURL", out var urlEl))
            {
                result.Url = urlEl.GetString() ?? "";
            }

            // Get related topics
            if (json.TryGetProperty("RelatedTopics", out var topicsEl))
            {
                foreach (var topic in topicsEl.EnumerateArray().Take(5))
                {
                    if (topic.TryGetProperty("Text", out var textEl))
                    {
                        result.RelatedTopics.Add(textEl.GetString() ?? "");
                    }
                }
            }

            // Get answer (for simple queries)
            if (json.TryGetProperty("Answer", out var answerEl) && !string.IsNullOrEmpty(answerEl.GetString()))
            {
                result.Answer = answerEl.GetString() ?? "";
            }

            result.Success = !string.IsNullOrEmpty(result.Abstract) ||
                            !string.IsNullOrEmpty(result.Answer) ||
                            result.RelatedTopics.Count > 0;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Web search error");
            result.Error = ex.Message;
        }

        return result;
    }

    /// <summary>
    /// Chat พร้อม web search context
    /// </summary>
    public async Task<string> ChatWithWebSearchAsync(
        string message,
        CancellationToken ct = default)
    {
        // First, search the web
        var searchResult = await SearchWebAsync(message, ct);

        string context = "";
        if (searchResult.Success)
        {
            context = $"\n\n[Web Search Results for '{message}']\n";
            if (!string.IsNullOrEmpty(searchResult.Answer))
            {
                context += $"Answer: {searchResult.Answer}\n";
            }
            if (!string.IsNullOrEmpty(searchResult.Abstract))
            {
                context += $"Summary: {searchResult.Abstract}\n";
                context += $"Source: {searchResult.Source} ({searchResult.Url})\n";
            }
            if (searchResult.RelatedTopics.Count > 0)
            {
                context += "Related:\n";
                foreach (var topic in searchResult.RelatedTopics)
                {
                    context += $"- {topic}\n";
                }
            }
        }

        // Add context to message
        var enhancedMessage = !string.IsNullOrEmpty(context)
            ? $"{message}\n\nUse this information to help answer:{context}"
            : message;

        // Now chat with the context
        return await ChatStreamAsync(enhancedMessage, ct);
    }

    /// <summary>
    /// ตรวจสอบว่า model รองรับ vision หรือไม่
    /// </summary>
    public bool IsVisionModel()
    {
        var visionModels = new[] { "llava", "llama3.2-vision", "bakllava", "moondream" };
        return visionModels.Any(v => CurrentModel.Contains(v, StringComparison.OrdinalIgnoreCase));
    }
}

#region Models

public class ChatMessage
{
    public string Role { get; set; } = ""; // system, user, assistant
    public string Content { get; set; } = "";
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public List<string> Images { get; set; } = new(); // Base64 encoded images
    public MessageType Type { get; set; } = MessageType.Text;
}

public enum MessageType
{
    Text,
    Image,
    WebSearch,
    File
}

public class ChatResponse
{
    public string Content { get; set; } = "";
    public string Model { get; set; } = "";
    public long TotalDuration { get; set; }
    public int EvalCount { get; set; }

    /// <summary>
    /// Tokens per second
    /// </summary>
    public double TokensPerSecond => TotalDuration > 0
        ? EvalCount / (TotalDuration / 1_000_000_000.0)
        : 0;
}

public class OllamaModelInfo
{
    public string Name { get; set; } = "";
    public long Size { get; set; }
    public string? ModifiedAt { get; set; }

    public string SizeDisplay => Size switch
    {
        > 1_000_000_000 => $"{Size / 1_000_000_000.0:F1} GB",
        > 1_000_000 => $"{Size / 1_000_000.0:F1} MB",
        _ => $"{Size / 1000.0:F1} KB"
    };
}

public class WebSearchResult
{
    public string Query { get; set; } = "";
    public bool Success { get; set; }
    public string Abstract { get; set; } = "";
    public string Source { get; set; } = "";
    public string Url { get; set; } = "";
    public string Answer { get; set; } = "";
    public List<string> RelatedTopics { get; set; } = new();
    public string? Error { get; set; }
}

#endregion
