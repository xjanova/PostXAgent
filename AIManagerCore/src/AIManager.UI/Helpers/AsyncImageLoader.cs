using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AIManager.UI.Helpers;

/// <summary>
/// Production-quality async image loader with caching and error handling
/// for HuggingFace model thumbnails
/// </summary>
public static class AsyncImageLoader
{
    private static readonly HttpClient _httpClient;
    private static readonly ConcurrentDictionary<string, BitmapImage?> _memoryCache = new();
    private static readonly string _diskCachePath;
    private static readonly SemaphoreSlim _loadSemaphore = new(10); // Max 10 concurrent loads

    static AsyncImageLoader()
    {
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("PostXAgent/1.0");

        // Setup disk cache directory
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _diskCachePath = Path.Combine(appData, "PostXAgent", "ImageCache");
        Directory.CreateDirectory(_diskCachePath);
    }

    /// <summary>
    /// Attached property for async image source loading
    /// </summary>
    public static readonly DependencyProperty SourceUrlProperty =
        DependencyProperty.RegisterAttached(
            "SourceUrl",
            typeof(string),
            typeof(AsyncImageLoader),
            new PropertyMetadata(null, OnSourceUrlChanged));

    public static string GetSourceUrl(DependencyObject obj) =>
        (string)obj.GetValue(SourceUrlProperty);

    public static void SetSourceUrl(DependencyObject obj, string value) =>
        obj.SetValue(SourceUrlProperty, value);

    /// <summary>
    /// Attached property for placeholder image while loading
    /// </summary>
    public static readonly DependencyProperty PlaceholderProperty =
        DependencyProperty.RegisterAttached(
            "Placeholder",
            typeof(ImageSource),
            typeof(AsyncImageLoader),
            new PropertyMetadata(null));

    public static ImageSource GetPlaceholder(DependencyObject obj) =>
        (ImageSource)obj.GetValue(PlaceholderProperty);

    public static void SetPlaceholder(DependencyObject obj, ImageSource value) =>
        obj.SetValue(PlaceholderProperty, value);

    /// <summary>
    /// Attached property for error placeholder
    /// </summary>
    public static readonly DependencyProperty ErrorPlaceholderProperty =
        DependencyProperty.RegisterAttached(
            "ErrorPlaceholder",
            typeof(ImageSource),
            typeof(AsyncImageLoader),
            new PropertyMetadata(null));

    public static ImageSource GetErrorPlaceholder(DependencyObject obj) =>
        (ImageSource)obj.GetValue(ErrorPlaceholderProperty);

    public static void SetErrorPlaceholder(DependencyObject obj, ImageSource value) =>
        obj.SetValue(ErrorPlaceholderProperty, value);

    private static async void OnSourceUrlChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Image image) return;

        var url = e.NewValue as string;
        if (string.IsNullOrEmpty(url))
        {
            image.Source = GetErrorPlaceholder(image);
            return;
        }

        // Set placeholder while loading
        var placeholder = GetPlaceholder(image);
        if (placeholder != null)
        {
            image.Source = placeholder;
        }

        // Load image async
        var bitmap = await LoadImageAsync(url);

        // Check if URL hasn't changed while loading
        if (GetSourceUrl(image) == url)
        {
            image.Source = bitmap ?? GetErrorPlaceholder(image);
        }
    }

    /// <summary>
    /// Load image from URL with memory and disk caching
    /// </summary>
    public static async Task<BitmapImage?> LoadImageAsync(string url, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(url)) return null;

        // Check memory cache first
        if (_memoryCache.TryGetValue(url, out var cached))
        {
            return cached;
        }

        // Check disk cache
        var cacheFileName = GetCacheFileName(url);
        var cachePath = Path.Combine(_diskCachePath, cacheFileName);

        if (File.Exists(cachePath))
        {
            try
            {
                var bitmap = LoadFromDisk(cachePath);
                if (bitmap != null)
                {
                    _memoryCache[url] = bitmap;
                    return bitmap;
                }
            }
            catch
            {
                // Corrupt cache file, delete it
                try { File.Delete(cachePath); } catch { }
            }
        }

        // Download from URL
        await _loadSemaphore.WaitAsync(ct);
        try
        {
            // Double-check cache after acquiring semaphore
            if (_memoryCache.TryGetValue(url, out cached))
            {
                return cached;
            }

            var imageBytes = await DownloadImageAsync(url, ct);
            if (imageBytes == null || imageBytes.Length == 0)
            {
                _memoryCache[url] = null;
                return null;
            }

            // Save to disk cache
            await SaveToDiskCacheAsync(cachePath, imageBytes);

            // Create bitmap
            var bitmap = CreateBitmapFromBytes(imageBytes);
            _memoryCache[url] = bitmap;
            return bitmap;
        }
        catch (Exception)
        {
            _memoryCache[url] = null;
            return null;
        }
        finally
        {
            _loadSemaphore.Release();
        }
    }

    private static async Task<byte[]?> DownloadImageAsync(string url, CancellationToken ct)
    {
        try
        {
            using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);

            if (!response.IsSuccessStatusCode)
                return null;

            // Check content type
            var contentType = response.Content.Headers.ContentType?.MediaType ?? "";
            if (!contentType.StartsWith("image/"))
                return null;

            // Limit size to 10MB
            var contentLength = response.Content.Headers.ContentLength ?? 0;
            if (contentLength > 10 * 1024 * 1024)
                return null;

            return await response.Content.ReadAsByteArrayAsync(ct);
        }
        catch
        {
            return null;
        }
    }

    private static BitmapImage? LoadFromDisk(string path)
    {
        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(path, UriKind.Absolute);
            bitmap.DecodePixelWidth = 200; // Limit memory usage
            bitmap.EndInit();
            bitmap.Freeze(); // Make thread-safe
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    private static BitmapImage? CreateBitmapFromBytes(byte[] bytes)
    {
        try
        {
            using var stream = new MemoryStream(bytes);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
            bitmap.DecodePixelWidth = 200; // Limit memory usage
            bitmap.EndInit();
            bitmap.Freeze(); // Make thread-safe
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    private static async Task SaveToDiskCacheAsync(string path, byte[] data)
    {
        try
        {
            await File.WriteAllBytesAsync(path, data);
        }
        catch
        {
            // Ignore cache write errors
        }
    }

    private static string GetCacheFileName(string url)
    {
        // Create a hash-based filename from URL
        using var sha = System.Security.Cryptography.SHA256.Create();
        var hash = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(url));
        var hashString = Convert.ToHexString(hash)[..16];

        // Extract extension from URL
        var extension = ".png";
        if (url.Contains(".jpg", StringComparison.OrdinalIgnoreCase) ||
            url.Contains(".jpeg", StringComparison.OrdinalIgnoreCase))
        {
            extension = ".jpg";
        }
        else if (url.Contains(".webp", StringComparison.OrdinalIgnoreCase))
        {
            extension = ".webp";
        }

        return $"{hashString}{extension}";
    }

    /// <summary>
    /// Clear memory cache (call when memory is low)
    /// </summary>
    public static void ClearMemoryCache()
    {
        _memoryCache.Clear();
    }

    /// <summary>
    /// Clear all caches including disk
    /// </summary>
    public static void ClearAllCaches()
    {
        _memoryCache.Clear();
        try
        {
            if (Directory.Exists(_diskCachePath))
            {
                foreach (var file in Directory.GetFiles(_diskCachePath))
                {
                    try { File.Delete(file); } catch { }
                }
            }
        }
        catch { }
    }

    /// <summary>
    /// Get cache statistics
    /// </summary>
    public static (int MemoryCacheCount, long DiskCacheSizeBytes) GetCacheStats()
    {
        var memoryCount = _memoryCache.Count;
        long diskSize = 0;

        try
        {
            if (Directory.Exists(_diskCachePath))
            {
                foreach (var file in Directory.GetFiles(_diskCachePath))
                {
                    try
                    {
                        diskSize += new FileInfo(file).Length;
                    }
                    catch { }
                }
            }
        }
        catch { }

        return (memoryCount, diskSize);
    }
}
