using System.Net;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace AIManager.Core.Services;

/// <summary>
/// Lightweight HTTP server that serves local video/image files temporarily
/// so platforms that require URLs (Instagram, TikTok) can download them.
/// </summary>
public class LocalFileServerService : IDisposable
{
    private readonly ILogger<LocalFileServerService> _logger;
    private readonly HttpListener _listener;
    private readonly ConcurrentDictionary<string, FileMapping> _fileMappings = new();
    private readonly int _port;
    private bool _isRunning;
    private CancellationTokenSource? _cts;
    private Task? _listenerTask;
    private Timer? _cleanupTimer;

    public int Port => _port;
    public bool IsRunning => _isRunning;

    public LocalFileServerService(ILogger<LocalFileServerService> logger, int port = 5010)
    {
        _logger = logger;
        _port = port;
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://localhost:{_port}/");
    }

    /// <summary>
    /// Register a local file and get a temporary URL that can be used by external services.
    /// </summary>
    /// <param name="localPath">Absolute path to the local file.</param>
    /// <param name="expiry">How long the URL should be valid. Defaults to 1 hour.</param>
    /// <returns>A temporary URL serving the file.</returns>
    public string RegisterFile(string localPath, TimeSpan? expiry = null)
    {
        if (!File.Exists(localPath))
        {
            throw new FileNotFoundException($"File not found: {localPath}", localPath);
        }

        var token = Guid.NewGuid().ToString("N");
        var ext = Path.GetExtension(localPath);
        var mapping = new FileMapping
        {
            LocalPath = localPath,
            ExpiresAt = DateTime.UtcNow.Add(expiry ?? TimeSpan.FromHours(1)),
            ContentType = GetContentType(ext)
        };
        _fileMappings[token] = mapping;

        _logger.LogInformation("Registered file {Path} with token {Token}, expires at {Expiry}",
            localPath, token, mapping.ExpiresAt);

        return $"http://localhost:{_port}/files/{token}{ext}";
    }

    /// <summary>
    /// Start the HTTP file server.
    /// </summary>
    public Task StartAsync(CancellationToken ct = default)
    {
        if (_isRunning)
        {
            _logger.LogWarning("LocalFileServer is already running on port {Port}", _port);
            return Task.CompletedTask;
        }

        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        try
        {
            _listener.Start();
            _isRunning = true;
            _logger.LogInformation("LocalFileServer started on port {Port}", _port);

            // Start cleanup timer (every 5 minutes)
            _cleanupTimer = new Timer(_ => CleanupExpiredMappings(), null,
                TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));

            // Start listening in background
            _listenerTask = Task.Run(() => ListenLoop(_cts.Token), _cts.Token);

            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start LocalFileServer on port {Port}", _port);
            _isRunning = false;
            throw;
        }
    }

    /// <summary>
    /// Stop the HTTP file server.
    /// </summary>
    public void Stop()
    {
        if (!_isRunning) return;

        _logger.LogInformation("Stopping LocalFileServer...");

        _cts?.Cancel();
        _cleanupTimer?.Dispose();
        _cleanupTimer = null;

        try
        {
            _listener.Stop();
        }
        catch (ObjectDisposedException)
        {
            // Already disposed
        }

        _isRunning = false;
        _fileMappings.Clear();

        _logger.LogInformation("LocalFileServer stopped");
    }

    /// <summary>
    /// Remove a specific file registration by its token URL.
    /// </summary>
    public bool UnregisterFile(string url)
    {
        // Extract token from URL: http://localhost:5010/files/{token}.ext
        var uri = new Uri(url);
        var segments = uri.AbsolutePath.Split('/');
        if (segments.Length < 3) return false;

        var fileNameWithExt = segments[^1];
        var token = Path.GetFileNameWithoutExtension(fileNameWithExt);
        return _fileMappings.TryRemove(token, out _);
    }

    private async Task ListenLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _isRunning)
        {
            try
            {
                var context = await _listener.GetContextAsync().WaitAsync(ct);
                // Handle each request in a separate task
                _ = Task.Run(() => HandleRequestAsync(context), ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (HttpListenerException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in listener loop");
            }
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext context)
    {
        var request = context.Request;
        var response = context.Response;

        try
        {
            var path = request.Url?.AbsolutePath ?? "";

            // Only serve files from /files/ path
            if (!path.StartsWith("/files/"))
            {
                response.StatusCode = 404;
                response.Close();
                return;
            }

            // Extract token from path: /files/{token}.ext
            var fileNameWithExt = path.Substring("/files/".Length);
            var token = Path.GetFileNameWithoutExtension(fileNameWithExt);

            if (!_fileMappings.TryGetValue(token, out var mapping))
            {
                _logger.LogWarning("Unknown file token: {Token}", token);
                response.StatusCode = 404;
                var notFoundBytes = System.Text.Encoding.UTF8.GetBytes("File not found");
                response.ContentLength64 = notFoundBytes.Length;
                await response.OutputStream.WriteAsync(notFoundBytes);
                response.Close();
                return;
            }

            // Check expiry
            if (DateTime.UtcNow > mapping.ExpiresAt)
            {
                _logger.LogWarning("File token expired: {Token}", token);
                _fileMappings.TryRemove(token, out _);
                response.StatusCode = 410; // Gone
                var goneBytes = System.Text.Encoding.UTF8.GetBytes("File link expired");
                response.ContentLength64 = goneBytes.Length;
                await response.OutputStream.WriteAsync(goneBytes);
                response.Close();
                return;
            }

            // Check file still exists
            if (!File.Exists(mapping.LocalPath))
            {
                _logger.LogWarning("File no longer exists: {Path}", mapping.LocalPath);
                _fileMappings.TryRemove(token, out _);
                response.StatusCode = 404;
                response.Close();
                return;
            }

            var fileInfo = new FileInfo(mapping.LocalPath);
            var fileLength = fileInfo.Length;

            response.ContentType = mapping.ContentType;
            response.AddHeader("Accept-Ranges", "bytes");
            response.AddHeader("Access-Control-Allow-Origin", "*");

            // Handle Range requests for video streaming
            var rangeHeader = request.Headers["Range"];
            if (!string.IsNullOrEmpty(rangeHeader) && rangeHeader.StartsWith("bytes="))
            {
                await HandleRangeRequestAsync(response, mapping.LocalPath, fileLength, rangeHeader);
            }
            else
            {
                // Full file response
                response.StatusCode = 200;
                response.ContentLength64 = fileLength;

                using var fileStream = new FileStream(mapping.LocalPath, FileMode.Open,
                    FileAccess.Read, FileShare.Read, bufferSize: 81920, useAsync: true);
                await fileStream.CopyToAsync(response.OutputStream);
            }

            response.Close();

            _logger.LogDebug("Served file {Path} ({Size} bytes)", mapping.LocalPath, fileLength);
        }
        catch (HttpListenerException)
        {
            // Client disconnected
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling request");
            try
            {
                response.StatusCode = 500;
                response.Close();
            }
            catch
            {
                // Ignore close errors
            }
        }
    }

    private static async Task HandleRangeRequestAsync(
        HttpListenerResponse response, string filePath, long fileLength, string rangeHeader)
    {
        // Parse "bytes=start-end"
        var rangeSpec = rangeHeader.Substring("bytes=".Length);
        var parts = rangeSpec.Split('-');

        long start = 0;
        long end = fileLength - 1;

        if (!string.IsNullOrEmpty(parts[0]))
        {
            start = long.Parse(parts[0]);
        }

        if (parts.Length > 1 && !string.IsNullOrEmpty(parts[1]))
        {
            end = long.Parse(parts[1]);
        }

        // Clamp
        if (start < 0) start = 0;
        if (end >= fileLength) end = fileLength - 1;
        if (start > end)
        {
            response.StatusCode = 416; // Range Not Satisfiable
            response.AddHeader("Content-Range", $"bytes */{fileLength}");
            return;
        }

        var contentLength = end - start + 1;

        response.StatusCode = 206; // Partial Content
        response.ContentLength64 = contentLength;
        response.AddHeader("Content-Range", $"bytes {start}-{end}/{fileLength}");

        using var fileStream = new FileStream(filePath, FileMode.Open,
            FileAccess.Read, FileShare.Read, bufferSize: 81920, useAsync: true);
        fileStream.Seek(start, SeekOrigin.Begin);

        var buffer = new byte[81920];
        var remaining = contentLength;
        while (remaining > 0)
        {
            var toRead = (int)Math.Min(buffer.Length, remaining);
            var read = await fileStream.ReadAsync(buffer.AsMemory(0, toRead));
            if (read == 0) break;
            await response.OutputStream.WriteAsync(buffer.AsMemory(0, read));
            remaining -= read;
        }
    }

    private void CleanupExpiredMappings()
    {
        var now = DateTime.UtcNow;
        var expiredTokens = _fileMappings
            .Where(kvp => now > kvp.Value.ExpiresAt)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var token in expiredTokens)
        {
            _fileMappings.TryRemove(token, out _);
        }

        if (expiredTokens.Count > 0)
        {
            _logger.LogInformation("Cleaned up {Count} expired file mappings", expiredTokens.Count);
        }
    }

    private static string GetContentType(string ext) => ext.ToLower() switch
    {
        ".mp4" => "video/mp4",
        ".webm" => "video/webm",
        ".avi" => "video/x-msvideo",
        ".mov" => "video/quicktime",
        ".mkv" => "video/x-matroska",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".gif" => "image/gif",
        ".webp" => "image/webp",
        ".mp3" => "audio/mpeg",
        ".wav" => "audio/wav",
        ".ogg" => "audio/ogg",
        ".aac" => "audio/aac",
        _ => "application/octet-stream"
    };

    public void Dispose()
    {
        Stop();
        _cts?.Dispose();
        _listener.Close();
    }

    private class FileMapping
    {
        public string LocalPath { get; set; } = "";
        public DateTime ExpiresAt { get; set; }
        public string ContentType { get; set; } = "application/octet-stream";
    }
}
