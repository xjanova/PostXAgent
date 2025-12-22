using System.Net.Http.Json;
using System.Text.Json;
using AIManager.Core.Models;
using Microsoft.Extensions.Logging;

namespace AIManager.Core.Services;

/// <summary>
/// Cloud Drive Integration Service
/// รองรับ Google Drive, OneDrive, Dropbox
/// </summary>
public class CloudDriveService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<CloudDriveService>? _logger;
    private readonly Dictionary<string, CloudDriveConnection> _connections = new();

    // OAuth URLs
    private const string GOOGLE_AUTH_URL = "https://accounts.google.com/o/oauth2/v2/auth";
    private const string GOOGLE_TOKEN_URL = "https://oauth2.googleapis.com/token";
    private const string GOOGLE_DRIVE_API = "https://www.googleapis.com/drive/v3";
    private const string GOOGLE_UPLOAD_API = "https://www.googleapis.com/upload/drive/v3";

    private const string ONEDRIVE_AUTH_URL = "https://login.microsoftonline.com/common/oauth2/v2.0/authorize";
    private const string ONEDRIVE_TOKEN_URL = "https://login.microsoftonline.com/common/oauth2/v2.0/token";
    private const string ONEDRIVE_API = "https://graph.microsoft.com/v1.0/me/drive";

    private const string DROPBOX_AUTH_URL = "https://www.dropbox.com/oauth2/authorize";
    private const string DROPBOX_TOKEN_URL = "https://api.dropboxapi.com/oauth2/token";
    private const string DROPBOX_API = "https://api.dropboxapi.com/2";
    private const string DROPBOX_CONTENT_API = "https://content.dropboxapi.com/2";

    public CloudDriveService(ILogger<CloudDriveService>? logger = null)
    {
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromMinutes(10);
        _logger = logger;
    }

    #region OAuth Flow

    /// <summary>
    /// สร้าง OAuth URL สำหรับการ authorize
    /// </summary>
    public string GetAuthorizationUrl(CloudDriveType driveType, string clientId, string redirectUri, string state)
    {
        return driveType switch
        {
            CloudDriveType.GoogleDrive => BuildGoogleAuthUrl(clientId, redirectUri, state),
            CloudDriveType.OneDrive => BuildOneDriveAuthUrl(clientId, redirectUri, state),
            CloudDriveType.Dropbox => BuildDropboxAuthUrl(clientId, redirectUri, state),
            _ => throw new ArgumentException($"Unsupported drive type: {driveType}")
        };
    }

    private string BuildGoogleAuthUrl(string clientId, string redirectUri, string state)
    {
        var scopes = Uri.EscapeDataString("https://www.googleapis.com/auth/drive.file https://www.googleapis.com/auth/userinfo.email");
        return $"{GOOGLE_AUTH_URL}?client_id={clientId}&redirect_uri={Uri.EscapeDataString(redirectUri)}&response_type=code&scope={scopes}&state={state}&access_type=offline&prompt=consent";
    }

    private string BuildOneDriveAuthUrl(string clientId, string redirectUri, string state)
    {
        var scopes = Uri.EscapeDataString("Files.ReadWrite User.Read offline_access");
        return $"{ONEDRIVE_AUTH_URL}?client_id={clientId}&redirect_uri={Uri.EscapeDataString(redirectUri)}&response_type=code&scope={scopes}&state={state}";
    }

    private string BuildDropboxAuthUrl(string clientId, string redirectUri, string state)
    {
        return $"{DROPBOX_AUTH_URL}?client_id={clientId}&redirect_uri={Uri.EscapeDataString(redirectUri)}&response_type=code&state={state}&token_access_type=offline";
    }

    /// <summary>
    /// Exchange authorization code for tokens
    /// </summary>
    public async Task<CloudDriveConnection> ExchangeCodeForTokensAsync(
        CloudDriveType driveType,
        string code,
        string clientId,
        string clientSecret,
        string redirectUri,
        string userId,
        CancellationToken ct = default)
    {
        return driveType switch
        {
            CloudDriveType.GoogleDrive => await ExchangeGoogleCodeAsync(code, clientId, clientSecret, redirectUri, userId, ct),
            CloudDriveType.OneDrive => await ExchangeOneDriveCodeAsync(code, clientId, clientSecret, redirectUri, userId, ct),
            CloudDriveType.Dropbox => await ExchangeDropboxCodeAsync(code, clientId, clientSecret, redirectUri, userId, ct),
            _ => throw new ArgumentException($"Unsupported drive type: {driveType}")
        };
    }

    private async Task<CloudDriveConnection> ExchangeGoogleCodeAsync(
        string code, string clientId, string clientSecret, string redirectUri, string userId, CancellationToken ct)
    {
        var request = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["code"] = code,
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["redirect_uri"] = redirectUri,
            ["grant_type"] = "authorization_code"
        });

        var response = await _httpClient.PostAsync(GOOGLE_TOKEN_URL, request, ct);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = json.TryGetProperty("error_description", out var desc) ? desc.GetString() : "Token exchange failed";
            throw new Exception($"Google OAuth error: {error}");
        }

        var connection = new CloudDriveConnection
        {
            UserId = userId,
            DriveType = CloudDriveType.GoogleDrive,
            AccessToken = json.GetProperty("access_token").GetString() ?? "",
            RefreshToken = json.TryGetProperty("refresh_token", out var rt) ? rt.GetString() ?? "" : "",
            TokenExpiresAt = DateTime.UtcNow.AddSeconds(json.GetProperty("expires_in").GetInt32()),
            IsConnected = true
        };

        // Get user email
        connection.Email = await GetGoogleUserEmailAsync(connection.AccessToken, ct);

        // Create default folder
        connection.FolderId = await CreateGoogleFolderAsync(connection.AccessToken, "AIManager_Content", ct);
        connection.FolderName = "AIManager_Content";

        _connections[userId] = connection;
        return connection;
    }

    private async Task<CloudDriveConnection> ExchangeOneDriveCodeAsync(
        string code, string clientId, string clientSecret, string redirectUri, string userId, CancellationToken ct)
    {
        var request = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["code"] = code,
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["redirect_uri"] = redirectUri,
            ["grant_type"] = "authorization_code"
        });

        var response = await _httpClient.PostAsync(ONEDRIVE_TOKEN_URL, request, ct);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception("OneDrive OAuth failed");
        }

        var connection = new CloudDriveConnection
        {
            UserId = userId,
            DriveType = CloudDriveType.OneDrive,
            AccessToken = json.GetProperty("access_token").GetString() ?? "",
            RefreshToken = json.TryGetProperty("refresh_token", out var rt) ? rt.GetString() ?? "" : "",
            TokenExpiresAt = DateTime.UtcNow.AddSeconds(json.GetProperty("expires_in").GetInt32()),
            IsConnected = true
        };

        // Get user email
        connection.Email = await GetOneDriveUserEmailAsync(connection.AccessToken, ct);

        // Create default folder
        connection.FolderId = await CreateOneDriveFolderAsync(connection.AccessToken, "AIManager_Content", ct);
        connection.FolderName = "AIManager_Content";

        _connections[userId] = connection;
        return connection;
    }

    private async Task<CloudDriveConnection> ExchangeDropboxCodeAsync(
        string code, string clientId, string clientSecret, string redirectUri, string userId, CancellationToken ct)
    {
        var request = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["code"] = code,
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["redirect_uri"] = redirectUri,
            ["grant_type"] = "authorization_code"
        });

        var response = await _httpClient.PostAsync(DROPBOX_TOKEN_URL, request, ct);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception("Dropbox OAuth failed");
        }

        var connection = new CloudDriveConnection
        {
            UserId = userId,
            DriveType = CloudDriveType.Dropbox,
            AccessToken = json.GetProperty("access_token").GetString() ?? "",
            RefreshToken = json.TryGetProperty("refresh_token", out var rt) ? rt.GetString() ?? "" : "",
            TokenExpiresAt = DateTime.UtcNow.AddSeconds(json.TryGetProperty("expires_in", out var exp) ? exp.GetInt32() : 14400),
            IsConnected = true
        };

        // Get user email
        connection.Email = await GetDropboxUserEmailAsync(connection.AccessToken, ct);

        // Create default folder
        connection.FolderId = await CreateDropboxFolderAsync(connection.AccessToken, "/AIManager_Content", ct);
        connection.FolderName = "AIManager_Content";

        _connections[userId] = connection;
        return connection;
    }

    #endregion

    #region Token Refresh

    /// <summary>
    /// Refresh access token if expired
    /// </summary>
    public async Task<CloudDriveConnection> RefreshTokenIfNeededAsync(
        CloudDriveConnection connection,
        string clientId,
        string clientSecret,
        CancellationToken ct = default)
    {
        if (!connection.NeedsRefresh) return connection;

        _logger?.LogInformation("Refreshing {DriveType} token for user {UserId}", connection.DriveType, connection.UserId);

        return connection.DriveType switch
        {
            CloudDriveType.GoogleDrive => await RefreshGoogleTokenAsync(connection, clientId, clientSecret, ct),
            CloudDriveType.OneDrive => await RefreshOneDriveTokenAsync(connection, clientId, clientSecret, ct),
            CloudDriveType.Dropbox => await RefreshDropboxTokenAsync(connection, clientId, clientSecret, ct),
            _ => connection
        };
    }

    private async Task<CloudDriveConnection> RefreshGoogleTokenAsync(
        CloudDriveConnection connection, string clientId, string clientSecret, CancellationToken ct)
    {
        var request = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["refresh_token"] = connection.RefreshToken,
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["grant_type"] = "refresh_token"
        });

        var response = await _httpClient.PostAsync(GOOGLE_TOKEN_URL, request, ct);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);

        if (response.IsSuccessStatusCode)
        {
            connection.AccessToken = json.GetProperty("access_token").GetString() ?? "";
            connection.TokenExpiresAt = DateTime.UtcNow.AddSeconds(json.GetProperty("expires_in").GetInt32());
        }

        return connection;
    }

    private async Task<CloudDriveConnection> RefreshOneDriveTokenAsync(
        CloudDriveConnection connection, string clientId, string clientSecret, CancellationToken ct)
    {
        var request = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["refresh_token"] = connection.RefreshToken,
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["grant_type"] = "refresh_token"
        });

        var response = await _httpClient.PostAsync(ONEDRIVE_TOKEN_URL, request, ct);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);

        if (response.IsSuccessStatusCode)
        {
            connection.AccessToken = json.GetProperty("access_token").GetString() ?? "";
            connection.TokenExpiresAt = DateTime.UtcNow.AddSeconds(json.GetProperty("expires_in").GetInt32());
            if (json.TryGetProperty("refresh_token", out var rt))
            {
                connection.RefreshToken = rt.GetString() ?? connection.RefreshToken;
            }
        }

        return connection;
    }

    private async Task<CloudDriveConnection> RefreshDropboxTokenAsync(
        CloudDriveConnection connection, string clientId, string clientSecret, CancellationToken ct)
    {
        var request = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["refresh_token"] = connection.RefreshToken,
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["grant_type"] = "refresh_token"
        });

        var response = await _httpClient.PostAsync(DROPBOX_TOKEN_URL, request, ct);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);

        if (response.IsSuccessStatusCode)
        {
            connection.AccessToken = json.GetProperty("access_token").GetString() ?? "";
            connection.TokenExpiresAt = DateTime.UtcNow.AddSeconds(json.TryGetProperty("expires_in", out var exp) ? exp.GetInt32() : 14400);
        }

        return connection;
    }

    #endregion

    #region File Operations

    /// <summary>
    /// Upload file to cloud drive
    /// </summary>
    public async Task<CloudDriveFile> UploadFileAsync(
        CloudDriveConnection connection,
        string localFilePath,
        string fileName,
        string? folderId = null,
        CancellationToken ct = default)
    {
        folderId ??= connection.FolderId;

        return connection.DriveType switch
        {
            CloudDriveType.GoogleDrive => await UploadToGoogleDriveAsync(connection.AccessToken, localFilePath, fileName, folderId, ct),
            CloudDriveType.OneDrive => await UploadToOneDriveAsync(connection.AccessToken, localFilePath, fileName, folderId, ct),
            CloudDriveType.Dropbox => await UploadToDropboxAsync(connection.AccessToken, localFilePath, fileName, folderId, ct),
            _ => throw new ArgumentException($"Unsupported drive type: {connection.DriveType}")
        };
    }

    private async Task<CloudDriveFile> UploadToGoogleDriveAsync(
        string accessToken, string localFilePath, string fileName, string folderId, CancellationToken ct)
    {
        var fileBytes = await File.ReadAllBytesAsync(localFilePath, ct);
        var mimeType = GetMimeType(fileName);

        // Create metadata
        var metadata = new
        {
            name = fileName,
            parents = new[] { folderId }
        };

        // Multipart upload
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(JsonSerializer.Serialize(metadata), System.Text.Encoding.UTF8, "application/json"), "metadata");
        content.Add(new ByteArrayContent(fileBytes), "file", fileName);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{GOOGLE_UPLOAD_API}/files?uploadType=multipart");
        request.Headers.Add("Authorization", $"Bearer {accessToken}");
        request.Content = content;

        var response = await _httpClient.SendAsync(request, ct);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"Google Drive upload failed: {json}");
        }

        return new CloudDriveFile
        {
            Id = json.GetProperty("id").GetString() ?? "",
            Name = fileName,
            MimeType = mimeType,
            SizeBytes = fileBytes.Length,
            DownloadUrl = $"https://drive.google.com/uc?id={json.GetProperty("id").GetString()}&export=download",
            ParentFolderId = folderId,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow
        };
    }

    private async Task<CloudDriveFile> UploadToOneDriveAsync(
        string accessToken, string localFilePath, string fileName, string folderId, CancellationToken ct)
    {
        var fileBytes = await File.ReadAllBytesAsync(localFilePath, ct);

        var uploadPath = string.IsNullOrEmpty(folderId) || folderId == "root"
            ? $"{ONEDRIVE_API}/root:/{fileName}:/content"
            : $"{ONEDRIVE_API}/items/{folderId}:/{fileName}:/content";

        using var request = new HttpRequestMessage(HttpMethod.Put, uploadPath);
        request.Headers.Add("Authorization", $"Bearer {accessToken}");
        request.Content = new ByteArrayContent(fileBytes);
        request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(GetMimeType(fileName));

        var response = await _httpClient.SendAsync(request, ct);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"OneDrive upload failed: {json}");
        }

        return new CloudDriveFile
        {
            Id = json.GetProperty("id").GetString() ?? "",
            Name = fileName,
            MimeType = GetMimeType(fileName),
            SizeBytes = fileBytes.Length,
            DownloadUrl = json.TryGetProperty("@microsoft.graph.downloadUrl", out var url) ? url.GetString() ?? "" : "",
            ParentFolderId = folderId,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow
        };
    }

    private async Task<CloudDriveFile> UploadToDropboxAsync(
        string accessToken, string localFilePath, string fileName, string folderPath, CancellationToken ct)
    {
        var fileBytes = await File.ReadAllBytesAsync(localFilePath, ct);
        var path = $"{folderPath}/{fileName}";

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{DROPBOX_CONTENT_API}/files/upload");
        request.Headers.Add("Authorization", $"Bearer {accessToken}");
        request.Headers.Add("Dropbox-API-Arg", JsonSerializer.Serialize(new
        {
            path,
            mode = "overwrite",
            autorename = true
        }));
        request.Content = new ByteArrayContent(fileBytes);
        request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

        var response = await _httpClient.SendAsync(request, ct);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"Dropbox upload failed: {json}");
        }

        // Get shareable link
        var shareLink = await CreateDropboxShareLinkAsync(accessToken, path, ct);

        return new CloudDriveFile
        {
            Id = json.GetProperty("id").GetString() ?? "",
            Name = fileName,
            MimeType = GetMimeType(fileName),
            SizeBytes = fileBytes.Length,
            DownloadUrl = shareLink,
            ParentFolderId = folderPath,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Download file from cloud drive
    /// </summary>
    public async Task<byte[]> DownloadFileAsync(
        CloudDriveConnection connection,
        string fileId,
        CancellationToken ct = default)
    {
        return connection.DriveType switch
        {
            CloudDriveType.GoogleDrive => await DownloadFromGoogleDriveAsync(connection.AccessToken, fileId, ct),
            CloudDriveType.OneDrive => await DownloadFromOneDriveAsync(connection.AccessToken, fileId, ct),
            CloudDriveType.Dropbox => await DownloadFromDropboxAsync(connection.AccessToken, fileId, ct),
            _ => throw new ArgumentException($"Unsupported drive type: {connection.DriveType}")
        };
    }

    private async Task<byte[]> DownloadFromGoogleDriveAsync(string accessToken, string fileId, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{GOOGLE_DRIVE_API}/files/{fileId}?alt=media");
        request.Headers.Add("Authorization", $"Bearer {accessToken}");

        var response = await _httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new Exception("Failed to download from Google Drive");
        }

        return await response.Content.ReadAsByteArrayAsync(ct);
    }

    private async Task<byte[]> DownloadFromOneDriveAsync(string accessToken, string fileId, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{ONEDRIVE_API}/items/{fileId}/content");
        request.Headers.Add("Authorization", $"Bearer {accessToken}");

        var response = await _httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new Exception("Failed to download from OneDrive");
        }

        return await response.Content.ReadAsByteArrayAsync(ct);
    }

    private async Task<byte[]> DownloadFromDropboxAsync(string accessToken, string path, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{DROPBOX_CONTENT_API}/files/download");
        request.Headers.Add("Authorization", $"Bearer {accessToken}");
        request.Headers.Add("Dropbox-API-Arg", JsonSerializer.Serialize(new { path }));

        var response = await _httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new Exception("Failed to download from Dropbox");
        }

        return await response.Content.ReadAsByteArrayAsync(ct);
    }

    /// <summary>
    /// List files in folder
    /// </summary>
    public async Task<List<CloudDriveFile>> ListFilesAsync(
        CloudDriveConnection connection,
        string? folderId = null,
        CancellationToken ct = default)
    {
        folderId ??= connection.FolderId;

        return connection.DriveType switch
        {
            CloudDriveType.GoogleDrive => await ListGoogleDriveFilesAsync(connection.AccessToken, folderId, ct),
            CloudDriveType.OneDrive => await ListOneDriveFilesAsync(connection.AccessToken, folderId, ct),
            CloudDriveType.Dropbox => await ListDropboxFilesAsync(connection.AccessToken, folderId, ct),
            _ => new List<CloudDriveFile>()
        };
    }

    private async Task<List<CloudDriveFile>> ListGoogleDriveFilesAsync(string accessToken, string folderId, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"{GOOGLE_DRIVE_API}/files?q='{folderId}'+in+parents&fields=files(id,name,mimeType,size,createdTime,modifiedTime)");
        request.Headers.Add("Authorization", $"Bearer {accessToken}");

        var response = await _httpClient.SendAsync(request, ct);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);

        var files = new List<CloudDriveFile>();
        if (json.TryGetProperty("files", out var filesArray))
        {
            foreach (var file in filesArray.EnumerateArray())
            {
                files.Add(new CloudDriveFile
                {
                    Id = file.GetProperty("id").GetString() ?? "",
                    Name = file.GetProperty("name").GetString() ?? "",
                    MimeType = file.TryGetProperty("mimeType", out var mt) ? mt.GetString() ?? "" : "",
                    SizeBytes = file.TryGetProperty("size", out var sz) ? long.Parse(sz.GetString() ?? "0") : 0,
                    DownloadUrl = $"https://drive.google.com/uc?id={file.GetProperty("id").GetString()}&export=download",
                    ParentFolderId = folderId
                });
            }
        }

        return files;
    }

    private async Task<List<CloudDriveFile>> ListOneDriveFilesAsync(string accessToken, string folderId, CancellationToken ct)
    {
        var listUrl = folderId == "root"
            ? $"{ONEDRIVE_API}/root/children"
            : $"{ONEDRIVE_API}/items/{folderId}/children";

        using var request = new HttpRequestMessage(HttpMethod.Get, listUrl);
        request.Headers.Add("Authorization", $"Bearer {accessToken}");

        var response = await _httpClient.SendAsync(request, ct);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);

        var files = new List<CloudDriveFile>();
        if (json.TryGetProperty("value", out var items))
        {
            foreach (var item in items.EnumerateArray())
            {
                if (item.TryGetProperty("file", out _))
                {
                    files.Add(new CloudDriveFile
                    {
                        Id = item.GetProperty("id").GetString() ?? "",
                        Name = item.GetProperty("name").GetString() ?? "",
                        MimeType = item.TryGetProperty("file", out var f) && f.TryGetProperty("mimeType", out var mt) ? mt.GetString() ?? "" : "",
                        SizeBytes = item.TryGetProperty("size", out var sz) ? sz.GetInt64() : 0,
                        DownloadUrl = item.TryGetProperty("@microsoft.graph.downloadUrl", out var url) ? url.GetString() ?? "" : "",
                        ParentFolderId = folderId
                    });
                }
            }
        }

        return files;
    }

    private async Task<List<CloudDriveFile>> ListDropboxFilesAsync(string accessToken, string folderPath, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{DROPBOX_API}/files/list_folder");
        request.Headers.Add("Authorization", $"Bearer {accessToken}");
        request.Content = JsonContent.Create(new { path = folderPath });

        var response = await _httpClient.SendAsync(request, ct);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);

        var files = new List<CloudDriveFile>();
        if (json.TryGetProperty("entries", out var entries))
        {
            foreach (var entry in entries.EnumerateArray())
            {
                if (entry.GetProperty(".tag").GetString() == "file")
                {
                    files.Add(new CloudDriveFile
                    {
                        Id = entry.GetProperty("id").GetString() ?? "",
                        Name = entry.GetProperty("name").GetString() ?? "",
                        SizeBytes = entry.TryGetProperty("size", out var sz) ? sz.GetInt64() : 0,
                        ParentFolderId = folderPath
                    });
                }
            }
        }

        return files;
    }

    #endregion

    #region Helper Methods

    private async Task<string> GetGoogleUserEmailAsync(string accessToken, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://www.googleapis.com/oauth2/v2/userinfo");
        request.Headers.Add("Authorization", $"Bearer {accessToken}");

        var response = await _httpClient.SendAsync(request, ct);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);

        return json.TryGetProperty("email", out var email) ? email.GetString() ?? "" : "";
    }

    private async Task<string> GetOneDriveUserEmailAsync(string accessToken, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://graph.microsoft.com/v1.0/me");
        request.Headers.Add("Authorization", $"Bearer {accessToken}");

        var response = await _httpClient.SendAsync(request, ct);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);

        return json.TryGetProperty("mail", out var email) ? email.GetString() ?? "" :
               json.TryGetProperty("userPrincipalName", out var upn) ? upn.GetString() ?? "" : "";
    }

    private async Task<string> GetDropboxUserEmailAsync(string accessToken, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{DROPBOX_API}/users/get_current_account");
        request.Headers.Add("Authorization", $"Bearer {accessToken}");

        var response = await _httpClient.SendAsync(request, ct);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);

        return json.TryGetProperty("email", out var email) ? email.GetString() ?? "" : "";
    }

    private async Task<string> CreateGoogleFolderAsync(string accessToken, string folderName, CancellationToken ct)
    {
        var metadata = new
        {
            name = folderName,
            mimeType = "application/vnd.google-apps.folder"
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{GOOGLE_DRIVE_API}/files");
        request.Headers.Add("Authorization", $"Bearer {accessToken}");
        request.Content = JsonContent.Create(metadata);

        var response = await _httpClient.SendAsync(request, ct);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);

        return json.GetProperty("id").GetString() ?? "";
    }

    private async Task<string> CreateOneDriveFolderAsync(string accessToken, string folderName, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{ONEDRIVE_API}/root/children");
        request.Headers.Add("Authorization", $"Bearer {accessToken}");
        request.Content = JsonContent.Create(new Dictionary<string, object>
        {
            ["name"] = folderName,
            ["folder"] = new { },
            ["@microsoft.graph.conflictBehavior"] = "rename"
        });

        var response = await _httpClient.SendAsync(request, ct);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);

        return json.GetProperty("id").GetString() ?? "";
    }

    private async Task<string> CreateDropboxFolderAsync(string accessToken, string folderPath, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{DROPBOX_API}/files/create_folder_v2");
        request.Headers.Add("Authorization", $"Bearer {accessToken}");
        request.Content = JsonContent.Create(new { path = folderPath, autorename = true });

        var response = await _httpClient.SendAsync(request, ct);

        // Folder might already exist - that's OK
        return folderPath;
    }

    private async Task<string> CreateDropboxShareLinkAsync(string accessToken, string path, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{DROPBOX_API}/sharing/create_shared_link_with_settings");
        request.Headers.Add("Authorization", $"Bearer {accessToken}");
        request.Content = JsonContent.Create(new
        {
            path,
            settings = new { requested_visibility = "public" }
        });

        var response = await _httpClient.SendAsync(request, ct);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);

        return json.TryGetProperty("url", out var url) ? url.GetString() ?? "" : "";
    }

    private string GetMimeType(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".mp4" => "video/mp4",
            ".webm" => "video/webm",
            ".mov" => "video/quicktime",
            ".mp3" => "audio/mpeg",
            ".wav" => "audio/wav",
            ".ogg" => "audio/ogg",
            ".pdf" => "application/pdf",
            ".json" => "application/json",
            _ => "application/octet-stream"
        };
    }

    #endregion

    #region Setup Instructions

    /// <summary>
    /// Get setup instructions for each cloud drive type
    /// </summary>
    public static CloudDriveSetupInstructions GetSetupInstructions(CloudDriveType driveType, string language = "th")
    {
        return driveType switch
        {
            CloudDriveType.GoogleDrive => GetGoogleDriveInstructions(language),
            CloudDriveType.OneDrive => GetOneDriveInstructions(language),
            CloudDriveType.Dropbox => GetDropboxInstructions(language),
            _ => new CloudDriveSetupInstructions()
        };
    }

    private static CloudDriveSetupInstructions GetGoogleDriveInstructions(string language)
    {
        var isThai = language == "th";
        return new CloudDriveSetupInstructions
        {
            DriveType = CloudDriveType.GoogleDrive,
            Title = isThai ? "การตั้งค่า Google Drive" : "Google Drive Setup",
            Steps = isThai ? new List<string>
            {
                "1. ไปที่ Google Cloud Console (https://console.cloud.google.com/)",
                "2. สร้าง Project ใหม่ หรือเลือก Project ที่มีอยู่",
                "3. ไปที่ APIs & Services > Library และเปิดใช้งาน Google Drive API",
                "4. ไปที่ APIs & Services > Credentials",
                "5. คลิก Create Credentials > OAuth Client ID",
                "6. เลือก Application Type: Web Application",
                "7. ตั้งชื่อ และเพิ่ม Authorized Redirect URI",
                "8. คัดลอก Client ID และ Client Secret มาใส่ในระบบ",
                "9. ไปที่ OAuth Consent Screen และตั้งค่า Scopes ที่จำเป็น"
            } : new List<string>
            {
                "1. Go to Google Cloud Console (https://console.cloud.google.com/)",
                "2. Create a new Project or select existing one",
                "3. Go to APIs & Services > Library and enable Google Drive API",
                "4. Go to APIs & Services > Credentials",
                "5. Click Create Credentials > OAuth Client ID",
                "6. Select Application Type: Web Application",
                "7. Set name and add Authorized Redirect URI",
                "8. Copy Client ID and Client Secret to the system",
                "9. Go to OAuth Consent Screen and configure required Scopes"
            },
            RequiredScopes = new List<string>
            {
                "https://www.googleapis.com/auth/drive.file",
                "https://www.googleapis.com/auth/userinfo.email"
            },
            ConsoleUrl = "https://console.cloud.google.com/apis/credentials"
        };
    }

    private static CloudDriveSetupInstructions GetOneDriveInstructions(string language)
    {
        var isThai = language == "th";
        return new CloudDriveSetupInstructions
        {
            DriveType = CloudDriveType.OneDrive,
            Title = isThai ? "การตั้งค่า OneDrive" : "OneDrive Setup",
            Steps = isThai ? new List<string>
            {
                "1. ไปที่ Azure Portal (https://portal.azure.com/)",
                "2. ไปที่ Azure Active Directory > App Registrations",
                "3. คลิก New Registration",
                "4. ตั้งชื่อ App และเลือก Supported Account Types",
                "5. เพิ่ม Redirect URI (Web)",
                "6. ไปที่ Certificates & Secrets และสร้าง Client Secret",
                "7. ไปที่ API Permissions และเพิ่ม Microsoft Graph permissions",
                "8. คัดลอก Application (client) ID และ Client Secret มาใส่ในระบบ"
            } : new List<string>
            {
                "1. Go to Azure Portal (https://portal.azure.com/)",
                "2. Go to Azure Active Directory > App Registrations",
                "3. Click New Registration",
                "4. Set App name and select Supported Account Types",
                "5. Add Redirect URI (Web)",
                "6. Go to Certificates & Secrets and create Client Secret",
                "7. Go to API Permissions and add Microsoft Graph permissions",
                "8. Copy Application (client) ID and Client Secret to the system"
            },
            RequiredScopes = new List<string>
            {
                "Files.ReadWrite",
                "User.Read",
                "offline_access"
            },
            ConsoleUrl = "https://portal.azure.com/#blade/Microsoft_AAD_RegisteredApps/ApplicationsListBlade"
        };
    }

    private static CloudDriveSetupInstructions GetDropboxInstructions(string language)
    {
        var isThai = language == "th";
        return new CloudDriveSetupInstructions
        {
            DriveType = CloudDriveType.Dropbox,
            Title = isThai ? "การตั้งค่า Dropbox" : "Dropbox Setup",
            Steps = isThai ? new List<string>
            {
                "1. ไปที่ Dropbox App Console (https://www.dropbox.com/developers/apps)",
                "2. คลิก Create App",
                "3. เลือก Scoped Access",
                "4. เลือก Full Dropbox หรือ App Folder ตามต้องการ",
                "5. ตั้งชื่อ App",
                "6. ไปที่ Settings และเพิ่ม Redirect URI",
                "7. ไปที่ Permissions และเลือก permissions ที่จำเป็น",
                "8. คัดลอก App Key และ App Secret มาใส่ในระบบ"
            } : new List<string>
            {
                "1. Go to Dropbox App Console (https://www.dropbox.com/developers/apps)",
                "2. Click Create App",
                "3. Select Scoped Access",
                "4. Choose Full Dropbox or App Folder as needed",
                "5. Set App name",
                "6. Go to Settings and add Redirect URI",
                "7. Go to Permissions and select required permissions",
                "8. Copy App Key and App Secret to the system"
            },
            RequiredScopes = new List<string>
            {
                "files.content.write",
                "files.content.read",
                "sharing.write",
                "account_info.read"
            },
            ConsoleUrl = "https://www.dropbox.com/developers/apps"
        };
    }

    #endregion
}

/// <summary>
/// Setup Instructions for Cloud Drive
/// </summary>
public class CloudDriveSetupInstructions
{
    public CloudDriveType DriveType { get; set; }
    public string Title { get; set; } = "";
    public List<string> Steps { get; set; } = new();
    public List<string> RequiredScopes { get; set; } = new();
    public string ConsoleUrl { get; set; } = "";
}
