using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using MyPostXAgent.Core.Models;
using Newtonsoft.Json;

namespace MyPostXAgent.Core.Services.Workflow;

/// <summary>
/// จัดการไฟล์ .mpflow สำหรับ Export/Import Workflows แบบเข้ารหัส
/// Compatible with PostXAgent .mpflow format
/// </summary>
public class MpflowManager
{
    private readonly ILogger<MpflowManager>? _logger;
    private readonly string _storagePath;

    // File signature for .mpflow files - must match PostXAgent
    private static readonly byte[] FileSignature = Encoding.UTF8.GetBytes("MPFLOW");
    private const int FileVersion = 1;
    private const int KeySize = 256;
    private const int BlockSize = 128;
    private const int SaltSize = 32;
    private const int IvSize = 16;
    private const int Iterations = 100000;

    public MpflowManager(ILogger<MpflowManager>? logger = null, string? storagePath = null)
    {
        _logger = logger;
        _storagePath = storagePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MyPostXAgent",
            "workflows"
        );

        Directory.CreateDirectory(_storagePath);
    }

    /// <summary>
    /// Export workflow เป็นไฟล์ .mpflow แบบเข้ารหัส
    /// </summary>
    public async Task<string> ExportWorkflowAsync(
        LearnedWorkflow workflow,
        string outputPath,
        string? password = null,
        CancellationToken ct = default)
    {
        // Ensure .mpflow extension
        if (!outputPath.EndsWith(".mpflow", StringComparison.OrdinalIgnoreCase))
        {
            outputPath += ".mpflow";
        }

        var container = new MpflowContainer
        {
            Version = FileVersion,
            ExportedAt = DateTime.UtcNow,
            Workflow = workflow,
            ExportedFrom = "MyPostXAgent",
            IsPasswordProtected = !string.IsNullOrEmpty(password)
        };

        var json = JsonConvert.SerializeObject(container, Formatting.None, new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore
        });

        var compressedData = CompressData(Encoding.UTF8.GetBytes(json));
        byte[] finalData;

        if (!string.IsNullOrEmpty(password))
        {
            finalData = EncryptData(compressedData, password);
        }
        else
        {
            finalData = compressedData;
        }

        // Write file with signature and version
        await using var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
        await fs.WriteAsync(FileSignature, ct);
        fs.WriteByte((byte)FileVersion);
        fs.WriteByte((byte)(string.IsNullOrEmpty(password) ? 0 : 1)); // Encrypted flag
        await fs.WriteAsync(BitConverter.GetBytes(finalData.Length), ct);
        await fs.WriteAsync(finalData, ct);

        _logger?.LogInformation("Exported workflow {Id} to {Path}", workflow.Id, outputPath);

        return outputPath;
    }

    /// <summary>
    /// Export หลาย workflows เป็นไฟล์ .mpflow
    /// </summary>
    public async Task<string> ExportWorkflowsAsync(
        IEnumerable<LearnedWorkflow> workflows,
        string outputPath,
        string? password = null,
        CancellationToken ct = default)
    {
        if (!outputPath.EndsWith(".mpflow", StringComparison.OrdinalIgnoreCase))
        {
            outputPath += ".mpflow";
        }

        var container = new MpflowContainerMultiple
        {
            Version = FileVersion,
            ExportedAt = DateTime.UtcNow,
            Workflows = workflows.ToList(),
            ExportedFrom = "MyPostXAgent",
            IsPasswordProtected = !string.IsNullOrEmpty(password)
        };

        var json = JsonConvert.SerializeObject(container, Formatting.None, new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore
        });

        var compressedData = CompressData(Encoding.UTF8.GetBytes(json));
        byte[] finalData;

        if (!string.IsNullOrEmpty(password))
        {
            finalData = EncryptData(compressedData, password);
        }
        else
        {
            finalData = compressedData;
        }

        await using var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
        await fs.WriteAsync(FileSignature, ct);
        fs.WriteByte((byte)FileVersion);
        fs.WriteByte((byte)(string.IsNullOrEmpty(password) ? 0 : 1));
        fs.WriteByte(1); // Multiple workflows flag
        await fs.WriteAsync(BitConverter.GetBytes(finalData.Length), ct);
        await fs.WriteAsync(finalData, ct);

        _logger?.LogInformation("Exported {Count} workflows to {Path}", container.Workflows.Count, outputPath);

        return outputPath;
    }

    /// <summary>
    /// Import workflow จากไฟล์ .mpflow
    /// </summary>
    public async Task<LearnedWorkflow> ImportWorkflowAsync(
        string filePath,
        string? password = null,
        CancellationToken ct = default)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("mpflow file not found", filePath);
        }

        await using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);

        // Validate signature
        var sig = new byte[FileSignature.Length];
        await fs.ReadExactlyAsync(sig, ct);
        if (!sig.SequenceEqual(FileSignature))
        {
            throw new InvalidDataException("Invalid mpflow file signature");
        }

        var version = fs.ReadByte();
        if (version > FileVersion)
        {
            throw new InvalidDataException($"Unsupported mpflow version: {version}");
        }

        var encrypted = fs.ReadByte() == 1;

        var lengthBytes = new byte[4];
        await fs.ReadExactlyAsync(lengthBytes, ct);
        var dataLength = BitConverter.ToInt32(lengthBytes);

        var data = new byte[dataLength];
        await fs.ReadExactlyAsync(data, ct);

        if (encrypted)
        {
            if (string.IsNullOrEmpty(password))
            {
                throw new InvalidOperationException("Password required for encrypted mpflow file");
            }
            data = DecryptData(data, password);
        }

        var jsonBytes = DecompressData(data);
        var json = Encoding.UTF8.GetString(jsonBytes);

        var container = JsonConvert.DeserializeObject<MpflowContainer>(json)
            ?? throw new InvalidDataException("Invalid mpflow content");

        var workflow = container.Workflow;
        workflow.Steps.ForEach(s => s.LearnedFrom = LearnedSource.Imported);

        // Generate new ID
        workflow.Id = Guid.NewGuid().ToString();
        workflow.CreatedAt = DateTime.UtcNow;
        workflow.UpdatedAt = DateTime.UtcNow;

        _logger?.LogInformation("Imported workflow {Name} from {Path}", workflow.Name, filePath);

        return workflow;
    }

    /// <summary>
    /// Import หลาย workflows จากไฟล์ .mpflow
    /// </summary>
    public async Task<List<LearnedWorkflow>> ImportWorkflowsAsync(
        string filePath,
        string? password = null,
        CancellationToken ct = default)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("mpflow file not found", filePath);
        }

        await using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);

        var sig = new byte[FileSignature.Length];
        await fs.ReadExactlyAsync(sig, ct);
        if (!sig.SequenceEqual(FileSignature))
        {
            throw new InvalidDataException("Invalid mpflow file signature");
        }

        var version = fs.ReadByte();
        if (version > FileVersion)
        {
            throw new InvalidDataException($"Unsupported mpflow version: {version}");
        }

        var encrypted = fs.ReadByte() == 1;
        var multiple = fs.ReadByte() == 1;

        var lengthBytes = new byte[4];
        await fs.ReadExactlyAsync(lengthBytes, ct);
        var dataLength = BitConverter.ToInt32(lengthBytes);

        var data = new byte[dataLength];
        await fs.ReadExactlyAsync(data, ct);

        if (encrypted)
        {
            if (string.IsNullOrEmpty(password))
            {
                throw new InvalidOperationException("Password required for encrypted mpflow file");
            }
            data = DecryptData(data, password);
        }

        var jsonBytes = DecompressData(data);
        var json = Encoding.UTF8.GetString(jsonBytes);

        List<LearnedWorkflow> workflows;

        if (multiple)
        {
            var container = JsonConvert.DeserializeObject<MpflowContainerMultiple>(json)
                ?? throw new InvalidDataException("Invalid mpflow content");
            workflows = container.Workflows;
        }
        else
        {
            var container = JsonConvert.DeserializeObject<MpflowContainer>(json)
                ?? throw new InvalidDataException("Invalid mpflow content");
            workflows = new List<LearnedWorkflow> { container.Workflow };
        }

        foreach (var workflow in workflows)
        {
            workflow.Steps.ForEach(s => s.LearnedFrom = LearnedSource.Imported);
            workflow.Id = Guid.NewGuid().ToString();
            workflow.CreatedAt = DateTime.UtcNow;
            workflow.UpdatedAt = DateTime.UtcNow;
        }

        _logger?.LogInformation("Imported {Count} workflows from {Path}", workflows.Count, filePath);

        return workflows;
    }

    /// <summary>
    /// ตรวจสอบข้อมูลไฟล์ .mpflow
    /// </summary>
    public async Task<MpflowFileInfo> GetFileInfoAsync(string filePath, CancellationToken ct = default)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("mpflow file not found", filePath);
        }

        await using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);

        var sig = new byte[FileSignature.Length];
        await fs.ReadExactlyAsync(sig, ct);
        var isValid = sig.SequenceEqual(FileSignature);

        if (!isValid)
        {
            return new MpflowFileInfo
            {
                FilePath = filePath,
                IsValid = false
            };
        }

        var version = fs.ReadByte();
        var encrypted = fs.ReadByte() == 1;

        return new MpflowFileInfo
        {
            FilePath = filePath,
            IsValid = true,
            Version = version,
            IsEncrypted = encrypted,
            FileSize = new FileInfo(filePath).Length
        };
    }

    /// <summary>
    /// ตรวจสอบ password
    /// </summary>
    public async Task<bool> ValidatePasswordAsync(string filePath, string password, CancellationToken ct = default)
    {
        try
        {
            await ImportWorkflowAsync(filePath, password, ct);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// บันทึก workflow ลง storage
    /// </summary>
    public async Task SaveWorkflowAsync(LearnedWorkflow workflow, CancellationToken ct = default)
    {
        workflow.UpdatedAt = DateTime.UtcNow;

        var filePath = Path.Combine(_storagePath, $"{workflow.Id}.json");
        var json = JsonConvert.SerializeObject(workflow, Formatting.Indented);
        await File.WriteAllTextAsync(filePath, json, ct);

        _logger?.LogInformation("Saved workflow {Id}: {Name}", workflow.Id, workflow.Name);
    }

    /// <summary>
    /// โหลด workflow จาก storage
    /// </summary>
    public async Task<LearnedWorkflow?> LoadWorkflowAsync(string id, CancellationToken ct = default)
    {
        var filePath = Path.Combine(_storagePath, $"{id}.json");
        if (!File.Exists(filePath))
        {
            return null;
        }

        var json = await File.ReadAllTextAsync(filePath, ct);
        return JsonConvert.DeserializeObject<LearnedWorkflow>(json);
    }

    /// <summary>
    /// โหลด workflows ทั้งหมด
    /// </summary>
    public async Task<List<LearnedWorkflow>> GetAllWorkflowsAsync(CancellationToken ct = default)
    {
        var workflows = new List<LearnedWorkflow>();

        if (!Directory.Exists(_storagePath))
        {
            return workflows;
        }

        foreach (var file in Directory.GetFiles(_storagePath, "*.json"))
        {
            try
            {
                var json = await File.ReadAllTextAsync(file, ct);
                var workflow = JsonConvert.DeserializeObject<LearnedWorkflow>(json);
                if (workflow != null)
                {
                    workflows.Add(workflow);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to load workflow from {File}", file);
            }
        }

        return workflows;
    }

    /// <summary>
    /// ลบ workflow
    /// </summary>
    public Task DeleteWorkflowAsync(string id, CancellationToken ct = default)
    {
        var filePath = Path.Combine(_storagePath, $"{id}.json");
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        _logger?.LogInformation("Deleted workflow {Id}", id);
        return Task.CompletedTask;
    }

    #region Private Methods

    private byte[] EncryptData(byte[] data, string password)
    {
        using var aes = Aes.Create();
        aes.KeySize = KeySize;
        aes.BlockSize = BlockSize;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key = DeriveKey(password, salt);
        var iv = RandomNumberGenerator.GetBytes(IvSize);

        aes.Key = key;
        aes.IV = iv;

        using var encryptor = aes.CreateEncryptor();
        var encrypted = encryptor.TransformFinalBlock(data, 0, data.Length);

        var result = new byte[SaltSize + IvSize + encrypted.Length];
        Buffer.BlockCopy(salt, 0, result, 0, SaltSize);
        Buffer.BlockCopy(iv, 0, result, SaltSize, IvSize);
        Buffer.BlockCopy(encrypted, 0, result, SaltSize + IvSize, encrypted.Length);

        return result;
    }

    private byte[] DecryptData(byte[] data, string password)
    {
        if (data.Length < SaltSize + IvSize)
        {
            throw new InvalidDataException("Invalid encrypted data");
        }

        var salt = new byte[SaltSize];
        var iv = new byte[IvSize];
        var encrypted = new byte[data.Length - SaltSize - IvSize];

        Buffer.BlockCopy(data, 0, salt, 0, SaltSize);
        Buffer.BlockCopy(data, SaltSize, iv, 0, IvSize);
        Buffer.BlockCopy(data, SaltSize + IvSize, encrypted, 0, encrypted.Length);

        using var aes = Aes.Create();
        aes.KeySize = KeySize;
        aes.BlockSize = BlockSize;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        aes.Key = DeriveKey(password, salt);
        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor();
        return decryptor.TransformFinalBlock(encrypted, 0, encrypted.Length);
    }

    private byte[] DeriveKey(string password, byte[] salt)
    {
        using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256);
        return pbkdf2.GetBytes(KeySize / 8);
    }

    private byte[] CompressData(byte[] data)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.Optimal))
        {
            gzip.Write(data, 0, data.Length);
        }
        return output.ToArray();
    }

    private byte[] DecompressData(byte[] data)
    {
        using var input = new MemoryStream(data);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        gzip.CopyTo(output);
        return output.ToArray();
    }

    #endregion
}

/// <summary>
/// Container สำหรับ .mpflow file (single workflow)
/// </summary>
public class MpflowContainer
{
    [JsonProperty("version")]
    public int Version { get; set; }

    [JsonProperty("exported_at")]
    public DateTime ExportedAt { get; set; }

    [JsonProperty("exported_from")]
    public string ExportedFrom { get; set; } = "";

    [JsonProperty("is_password_protected")]
    public bool IsPasswordProtected { get; set; }

    [JsonProperty("workflow")]
    public LearnedWorkflow Workflow { get; set; } = new();
}

/// <summary>
/// Container สำหรับ .mpflow file (multiple workflows)
/// </summary>
public class MpflowContainerMultiple
{
    [JsonProperty("version")]
    public int Version { get; set; }

    [JsonProperty("exported_at")]
    public DateTime ExportedAt { get; set; }

    [JsonProperty("exported_from")]
    public string ExportedFrom { get; set; } = "";

    [JsonProperty("is_password_protected")]
    public bool IsPasswordProtected { get; set; }

    [JsonProperty("workflows")]
    public List<LearnedWorkflow> Workflows { get; set; } = new();
}

/// <summary>
/// ข้อมูลไฟล์ .mpflow
/// </summary>
public class MpflowFileInfo
{
    public string FilePath { get; set; } = "";
    public bool IsValid { get; set; }
    public int Version { get; set; }
    public bool IsEncrypted { get; set; }
    public long FileSize { get; set; }
}
