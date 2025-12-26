using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using AIManager.Core.WebAutomation.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace AIManager.Core.WebAutomation;

/// <summary>
/// จัดการไฟล์ .mpflow สำหรับ Export/Import Workflows แบบเข้ารหัส
/// .mpflow format: AES-256 encrypted + GZip compressed workflow data
/// </summary>
public class MpflowManager
{
    private readonly ILogger<MpflowManager> _logger;
    private readonly WorkflowStorage _workflowStorage;

    // File signature for .mpflow files
    private static readonly byte[] FileSignature = Encoding.UTF8.GetBytes("MPFLOW");
    private const int FileVersion = 1;
    private const int KeySize = 256;
    private const int BlockSize = 128;
    private const int SaltSize = 32;
    private const int IvSize = 16;
    private const int Iterations = 100000;

    public MpflowManager(ILogger<MpflowManager> logger, WorkflowStorage workflowStorage)
    {
        _logger = logger;
        _workflowStorage = workflowStorage;
    }

    /// <summary>
    /// Export workflow เป็นไฟล์ .mpflow แบบเข้ารหัส
    /// </summary>
    public async Task<string> ExportWorkflowAsync(
        string workflowId,
        string outputPath,
        string? password = null,
        CancellationToken ct = default)
    {
        var workflow = await _workflowStorage.LoadWorkflowAsync(workflowId, ct)
            ?? throw new ArgumentException($"Workflow {workflowId} not found");

        return await ExportWorkflowAsync(workflow, outputPath, password, ct);
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
            ExportedFrom = "PostXAgent",
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

        _logger.LogInformation("Exported workflow {Id} to {Path}", workflow.Id, outputPath);

        return outputPath;
    }

    /// <summary>
    /// Export หลาย workflows เป็นไฟล์ .mpflow แบบเข้ารหัส
    /// </summary>
    public async Task<string> ExportWorkflowsAsync(
        IEnumerable<LearnedWorkflow> workflows,
        string outputPath,
        string? password = null,
        CancellationToken ct = default)
    {
        // Ensure .mpflow extension
        if (!outputPath.EndsWith(".mpflow", StringComparison.OrdinalIgnoreCase))
        {
            outputPath += ".mpflow";
        }

        var container = new MpflowContainerMultiple
        {
            Version = FileVersion,
            ExportedAt = DateTime.UtcNow,
            Workflows = workflows.ToList(),
            ExportedFrom = "PostXAgent",
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
        fs.WriteByte(1); // Multiple workflows flag
        await fs.WriteAsync(BitConverter.GetBytes(finalData.Length), ct);
        await fs.WriteAsync(finalData, ct);

        _logger.LogInformation("Exported {Count} workflows to {Path}", container.Workflows.Count, outputPath);

        return outputPath;
    }

    /// <summary>
    /// Import workflow จากไฟล์ .mpflow
    /// </summary>
    public async Task<LearnedWorkflow> ImportWorkflowAsync(
        string filePath,
        string? password = null,
        bool saveToStorage = true,
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

        // Read version
        var version = fs.ReadByte();
        if (version > FileVersion)
        {
            throw new InvalidDataException($"Unsupported mpflow version: {version}");
        }

        // Read encrypted flag
        var encrypted = fs.ReadByte() == 1;

        // Read data length
        var lengthBytes = new byte[4];
        await fs.ReadExactlyAsync(lengthBytes, ct);
        var dataLength = BitConverter.ToInt32(lengthBytes);

        // Read data
        var data = new byte[dataLength];
        await fs.ReadExactlyAsync(data, ct);

        // Decrypt if needed
        if (encrypted)
        {
            if (string.IsNullOrEmpty(password))
            {
                throw new InvalidOperationException("Password required for encrypted mpflow file");
            }
            data = DecryptData(data, password);
        }

        // Decompress
        var jsonBytes = DecompressData(data);
        var json = Encoding.UTF8.GetString(jsonBytes);

        // Parse
        var container = JsonConvert.DeserializeObject<MpflowContainer>(json)
            ?? throw new InvalidDataException("Invalid mpflow content");

        var workflow = container.Workflow;
        workflow.Steps.ForEach(s => s.LearnedFrom = LearnedSource.Imported);

        if (saveToStorage)
        {
            // Generate new ID to avoid conflicts
            workflow.Id = Guid.NewGuid().ToString();
            workflow.CreatedAt = DateTime.UtcNow;
            workflow.UpdatedAt = DateTime.UtcNow;
            await _workflowStorage.SaveWorkflowAsync(workflow, ct);
        }

        _logger.LogInformation("Imported workflow {Name} from {Path}", workflow.Name, filePath);

        return workflow;
    }

    /// <summary>
    /// Import หลาย workflows จากไฟล์ .mpflow
    /// </summary>
    public async Task<List<LearnedWorkflow>> ImportWorkflowsAsync(
        string filePath,
        string? password = null,
        bool saveToStorage = true,
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

        // Read version
        var version = fs.ReadByte();
        if (version > FileVersion)
        {
            throw new InvalidDataException($"Unsupported mpflow version: {version}");
        }

        // Read encrypted flag
        var encrypted = fs.ReadByte() == 1;

        // Read multiple flag
        var multiple = fs.ReadByte() == 1;

        // Read data length
        var lengthBytes = new byte[4];
        await fs.ReadExactlyAsync(lengthBytes, ct);
        var dataLength = BitConverter.ToInt32(lengthBytes);

        // Read data
        var data = new byte[dataLength];
        await fs.ReadExactlyAsync(data, ct);

        // Decrypt if needed
        if (encrypted)
        {
            if (string.IsNullOrEmpty(password))
            {
                throw new InvalidOperationException("Password required for encrypted mpflow file");
            }
            data = DecryptData(data, password);
        }

        // Decompress
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

            if (saveToStorage)
            {
                workflow.Id = Guid.NewGuid().ToString();
                workflow.CreatedAt = DateTime.UtcNow;
                workflow.UpdatedAt = DateTime.UtcNow;
                await _workflowStorage.SaveWorkflowAsync(workflow, ct);
            }
        }

        _logger.LogInformation("Imported {Count} workflows from {Path}", workflows.Count, filePath);

        return workflows;
    }

    /// <summary>
    /// ตรวจสอบว่าไฟล์ .mpflow ต้องใช้ password หรือไม่
    /// </summary>
    public async Task<MpflowFileInfo> GetFileInfoAsync(string filePath, CancellationToken ct = default)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("mpflow file not found", filePath);
        }

        await using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);

        // Validate signature
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
    /// ตรวจสอบ password ว่าถูกต้องหรือไม่
    /// </summary>
    public async Task<bool> ValidatePasswordAsync(string filePath, string password, CancellationToken ct = default)
    {
        try
        {
            await ImportWorkflowAsync(filePath, password, saveToStorage: false, ct);
            return true;
        }
        catch
        {
            return false;
        }
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

        // Combine: salt + iv + encrypted data
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
