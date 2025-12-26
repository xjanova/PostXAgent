using System.Text.Json.Serialization;

namespace AIManager.Mobile.Models;

/// <summary>
/// Configuration for a bank account that receives payments
/// </summary>
public class BankAccountConfig
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Bank/Payment provider type
    /// </summary>
    public BankType BankType { get; set; }

    /// <summary>
    /// Display name (e.g., "บัญชีหลัก", "PromptPay ร้าน")
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Account number or PromptPay ID
    /// </summary>
    public string AccountNumber { get; set; } = string.Empty;

    /// <summary>
    /// Account holder name
    /// </summary>
    public string AccountName { get; set; } = string.Empty;

    /// <summary>
    /// Whether this account is enabled for receiving payments
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Whether this is the primary account (shown first)
    /// </summary>
    public bool IsPrimary { get; set; }

    /// <summary>
    /// QR Code image path (optional)
    /// </summary>
    public string? QrCodePath { get; set; }

    /// <summary>
    /// Additional notes
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Created timestamp
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Get bank display info
    /// </summary>
    public BankDisplayInfo GetDisplayInfo() => BankInfo.GetDisplayInfo(BankType);

    /// <summary>
    /// Validate the configuration
    /// </summary>
    public bool IsValid()
    {
        return !string.IsNullOrWhiteSpace(AccountNumber) &&
               !string.IsNullOrWhiteSpace(AccountName) &&
               BankType != BankType.Unknown;
    }

    /// <summary>
    /// Get masked account number for display
    /// </summary>
    public string GetMaskedAccountNumber()
    {
        if (string.IsNullOrEmpty(AccountNumber) || AccountNumber.Length < 4)
            return AccountNumber;

        // For PromptPay phone numbers
        if (BankType == BankType.PromptPay && AccountNumber.Length == 10)
        {
            return $"xxx-xxx-{AccountNumber[^4..]}";
        }

        // For bank accounts
        return $"xxx-x-{AccountNumber[^4..]}-x";
    }
}

/// <summary>
/// Supported bank types
/// </summary>
public enum BankType
{
    Unknown = 0,

    // Thai Banks
    KBank = 1,          // กสิกรไทย
    SCB = 2,            // ไทยพาณิชย์
    BBL = 3,            // กรุงเทพ
    KTB = 4,            // กรุงไทย
    TTB = 5,            // ทหารไทยธนชาต
    BAY = 6,            // กรุงศรีอยุธยา
    GSB = 7,            // ออมสิน
    BAAC = 8,           // ธ.ก.ส.
    UOB = 9,            // ยูโอบี
    CIMB = 10,          // ซีไอเอ็มบี
    LH = 11,            // แลนด์ แอนด์ เฮ้าส์
    TISCO = 12,         // ทิสโก้
    KK = 13,            // เกียรตินาคินภัทร

    // E-Wallets & Payment Providers
    PromptPay = 50,     // พร้อมเพย์
    TrueMoney = 51,     // ทรูมันนี่
    LinePay = 52,       // LINE Pay
    ShopeePay = 53,     // ShopeePay
}

/// <summary>
/// Bank display information
/// </summary>
public class BankDisplayInfo
{
    public BankType Type { get; set; }
    public string NameTh { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string ShortName { get; set; } = string.Empty;
    public string IconName { get; set; } = string.Empty;
    public string PrimaryColor { get; set; } = "#000000";
    public string SecondaryColor { get; set; } = "#FFFFFF";
    public string[] SmsKeywords { get; set; } = Array.Empty<string>();
    public bool IsEWallet { get; set; }
}

/// <summary>
/// Static bank information
/// </summary>
public static class BankInfo
{
    private static readonly Dictionary<BankType, BankDisplayInfo> Banks = new()
    {
        // Thai Banks
        {
            BankType.KBank, new BankDisplayInfo
            {
                Type = BankType.KBank,
                NameTh = "ธนาคารกสิกรไทย",
                NameEn = "Kasikornbank",
                ShortName = "KBANK",
                IconName = "bank_kbank.png",
                PrimaryColor = "#138F2D",
                SecondaryColor = "#FFFFFF",
                SmsKeywords = new[] { "KBANK", "K-BANK", "KPLUS", "K PLUS", "กสิกร" }
            }
        },
        {
            BankType.SCB, new BankDisplayInfo
            {
                Type = BankType.SCB,
                NameTh = "ธนาคารไทยพาณิชย์",
                NameEn = "Siam Commercial Bank",
                ShortName = "SCB",
                IconName = "bank_scb.png",
                PrimaryColor = "#4E2A84",
                SecondaryColor = "#FFFFFF",
                SmsKeywords = new[] { "SCB", "SCBEASY", "SCB EASY", "ไทยพาณิชย์" }
            }
        },
        {
            BankType.BBL, new BankDisplayInfo
            {
                Type = BankType.BBL,
                NameTh = "ธนาคารกรุงเทพ",
                NameEn = "Bangkok Bank",
                ShortName = "BBL",
                IconName = "bank_bbl.png",
                PrimaryColor = "#1E4598",
                SecondaryColor = "#FFFFFF",
                SmsKeywords = new[] { "BBL", "BUALUANG", "Bangkok Bank", "กรุงเทพ" }
            }
        },
        {
            BankType.KTB, new BankDisplayInfo
            {
                Type = BankType.KTB,
                NameTh = "ธนาคารกรุงไทย",
                NameEn = "Krungthai Bank",
                ShortName = "KTB",
                IconName = "bank_ktb.png",
                PrimaryColor = "#1BA5E0",
                SecondaryColor = "#FFFFFF",
                SmsKeywords = new[] { "KTB", "KRUNGTHAI", "กรุงไทย" }
            }
        },
        {
            BankType.TTB, new BankDisplayInfo
            {
                Type = BankType.TTB,
                NameTh = "ธนาคารทหารไทยธนชาต",
                NameEn = "TMBThanachart Bank",
                ShortName = "TTB",
                IconName = "bank_ttb.png",
                PrimaryColor = "#0066B3",
                SecondaryColor = "#FFFFFF",
                SmsKeywords = new[] { "TMB", "TTB", "TMBTTB", "ทหารไทย", "ธนชาต" }
            }
        },
        {
            BankType.BAY, new BankDisplayInfo
            {
                Type = BankType.BAY,
                NameTh = "ธนาคารกรุงศรีอยุธยา",
                NameEn = "Bank of Ayudhya",
                ShortName = "BAY",
                IconName = "bank_bay.png",
                PrimaryColor = "#FEC80B",
                SecondaryColor = "#000000",
                SmsKeywords = new[] { "BAY", "KRUNGSRI", "กรุงศรี" }
            }
        },
        {
            BankType.GSB, new BankDisplayInfo
            {
                Type = BankType.GSB,
                NameTh = "ธนาคารออมสิน",
                NameEn = "Government Savings Bank",
                ShortName = "GSB",
                IconName = "bank_gsb.png",
                PrimaryColor = "#EB198D",
                SecondaryColor = "#FFFFFF",
                SmsKeywords = new[] { "GSB", "MYMO", "ออมสิน" }
            }
        },
        {
            BankType.BAAC, new BankDisplayInfo
            {
                Type = BankType.BAAC,
                NameTh = "ธนาคารเพื่อการเกษตรและสหกรณ์การเกษตร",
                NameEn = "Bank for Agriculture and Agricultural Cooperatives",
                ShortName = "BAAC",
                IconName = "bank_baac.png",
                PrimaryColor = "#4CAF50",
                SecondaryColor = "#FFFFFF",
                SmsKeywords = new[] { "BAAC", "ธกส", "A-Mobile" }
            }
        },
        {
            BankType.UOB, new BankDisplayInfo
            {
                Type = BankType.UOB,
                NameTh = "ธนาคารยูโอบี",
                NameEn = "United Overseas Bank",
                ShortName = "UOB",
                IconName = "bank_uob.png",
                PrimaryColor = "#0033A0",
                SecondaryColor = "#FFFFFF",
                SmsKeywords = new[] { "UOB", "ยูโอบี" }
            }
        },
        {
            BankType.CIMB, new BankDisplayInfo
            {
                Type = BankType.CIMB,
                NameTh = "ธนาคารซีไอเอ็มบี ไทย",
                NameEn = "CIMB Thai Bank",
                ShortName = "CIMB",
                IconName = "bank_cimb.png",
                PrimaryColor = "#EC1C24",
                SecondaryColor = "#FFFFFF",
                SmsKeywords = new[] { "CIMB", "ซีไอเอ็มบี" }
            }
        },
        {
            BankType.LH, new BankDisplayInfo
            {
                Type = BankType.LH,
                NameTh = "ธนาคารแลนด์ แอนด์ เฮ้าส์",
                NameEn = "Land and Houses Bank",
                ShortName = "LH",
                IconName = "bank_lh.png",
                PrimaryColor = "#F7941D",
                SecondaryColor = "#FFFFFF",
                SmsKeywords = new[] { "LH", "LHBANK", "แลนด์" }
            }
        },
        {
            BankType.TISCO, new BankDisplayInfo
            {
                Type = BankType.TISCO,
                NameTh = "ธนาคารทิสโก้",
                NameEn = "TISCO Bank",
                ShortName = "TISCO",
                IconName = "bank_tisco.png",
                PrimaryColor = "#002B5C",
                SecondaryColor = "#FFFFFF",
                SmsKeywords = new[] { "TISCO", "ทิสโก้" }
            }
        },
        {
            BankType.KK, new BankDisplayInfo
            {
                Type = BankType.KK,
                NameTh = "ธนาคารเกียรตินาคินภัทร",
                NameEn = "Kiatnakin Phatra Bank",
                ShortName = "KKP",
                IconName = "bank_kk.png",
                PrimaryColor = "#003366",
                SecondaryColor = "#FFFFFF",
                SmsKeywords = new[] { "KKP", "KIATNAKIN", "เกียรตินาคิน" }
            }
        },

        // E-Wallets
        {
            BankType.PromptPay, new BankDisplayInfo
            {
                Type = BankType.PromptPay,
                NameTh = "พร้อมเพย์",
                NameEn = "PromptPay",
                ShortName = "PP",
                IconName = "ewallet_promptpay.png",
                PrimaryColor = "#003D7E",
                SecondaryColor = "#FFFFFF",
                SmsKeywords = new[] { "PromptPay", "พร้อมเพย์", "PP" },
                IsEWallet = true
            }
        },
        {
            BankType.TrueMoney, new BankDisplayInfo
            {
                Type = BankType.TrueMoney,
                NameTh = "ทรูมันนี่ วอลเล็ท",
                NameEn = "TrueMoney Wallet",
                ShortName = "TMN",
                IconName = "ewallet_truemoney.png",
                PrimaryColor = "#F7931E",
                SecondaryColor = "#FFFFFF",
                SmsKeywords = new[] { "TrueMoney", "TRUE MONEY", "ทรูมันนี่" },
                IsEWallet = true
            }
        },
        {
            BankType.LinePay, new BankDisplayInfo
            {
                Type = BankType.LinePay,
                NameTh = "LINE Pay / Rabbit LINE Pay",
                NameEn = "LINE Pay",
                ShortName = "LINE",
                IconName = "ewallet_linepay.png",
                PrimaryColor = "#00C300",
                SecondaryColor = "#FFFFFF",
                SmsKeywords = new[] { "LINE", "LINEPAY", "Rabbit LINE Pay" },
                IsEWallet = true
            }
        },
        {
            BankType.ShopeePay, new BankDisplayInfo
            {
                Type = BankType.ShopeePay,
                NameTh = "ShopeePay",
                NameEn = "ShopeePay",
                ShortName = "SPAY",
                IconName = "ewallet_shopeepay.png",
                PrimaryColor = "#EE4D2D",
                SecondaryColor = "#FFFFFF",
                SmsKeywords = new[] { "ShopeePay", "SHOPEE" },
                IsEWallet = true
            }
        }
    };

    public static BankDisplayInfo GetDisplayInfo(BankType type)
    {
        return Banks.TryGetValue(type, out var info)
            ? info
            : new BankDisplayInfo { Type = type, NameTh = "ไม่ทราบ", NameEn = "Unknown" };
    }

    public static IReadOnlyList<BankDisplayInfo> GetAllBanks()
    {
        return Banks.Values.Where(b => !b.IsEWallet).OrderBy(b => b.NameTh).ToList();
    }

    public static IReadOnlyList<BankDisplayInfo> GetAllEWallets()
    {
        return Banks.Values.Where(b => b.IsEWallet).OrderBy(b => b.NameTh).ToList();
    }

    public static IReadOnlyList<BankDisplayInfo> GetAll()
    {
        return Banks.Values.OrderBy(b => b.IsEWallet).ThenBy(b => b.NameTh).ToList();
    }

    public static BankType? DetectFromSms(string sender, string body)
    {
        var text = $"{sender} {body}".ToUpperInvariant();

        foreach (var (type, info) in Banks)
        {
            if (info.SmsKeywords.Any(k => text.Contains(k.ToUpperInvariant())))
            {
                return type;
            }
        }

        return null;
    }
}

/// <summary>
/// Bank accounts availability status for websites
/// </summary>
public class BankAccountsStatus
{
    public bool IsReady { get; set; }
    public int EnabledAccountsCount { get; set; }
    public List<BankAccountPublicInfo> Accounts { get; set; } = new();
    public string? Message { get; set; }
}

/// <summary>
/// Public info about bank account (safe to share with websites)
/// </summary>
public class BankAccountPublicInfo
{
    public string Id { get; set; } = string.Empty;
    public BankType BankType { get; set; }
    public string BankNameTh { get; set; } = string.Empty;
    public string BankNameEn { get; set; } = string.Empty;
    public string BankShortName { get; set; } = string.Empty;
    public string BankColor { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;  // Full number for transfer
    public string AccountName { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
    public bool IsEWallet { get; set; }
    public string? QrCodeBase64 { get; set; }
}
