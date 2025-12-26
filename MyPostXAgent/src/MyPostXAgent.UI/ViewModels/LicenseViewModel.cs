using System.Windows.Input;
using MyPostXAgent.Core.Services.Data;
using MyPostXAgent.Core.Services.License;

namespace MyPostXAgent.UI.ViewModels;

/// <summary>
/// ViewModel สำหรับ License Activation
/// </summary>
public class LicenseViewModel : BaseViewModel
{
    private readonly LicenseService _licenseService;
    private readonly DemoManager _demoManager;
    private readonly DatabaseService _databaseService;

    private string _licenseKey = "";
    public string LicenseKey
    {
        get => _licenseKey;
        set => SetProperty(ref _licenseKey, value);
    }

    private string _machineId = "";
    public string MachineId
    {
        get => _machineId;
        set => SetProperty(ref _machineId, value);
    }

    private string _statusMessage = "";
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    private bool _isActivating;
    public bool IsActivating
    {
        get => _isActivating;
        set => SetProperty(ref _isActivating, value);
    }

    private bool _isCheckingDemo;
    public bool IsCheckingDemo
    {
        get => _isCheckingDemo;
        set => SetProperty(ref _isCheckingDemo, value);
    }

    private bool _canStartDemo = true;
    public bool CanStartDemo
    {
        get => _canStartDemo;
        set => SetProperty(ref _canStartDemo, value);
    }

    private bool _activationSuccess;
    public bool ActivationSuccess
    {
        get => _activationSuccess;
        set => SetProperty(ref _activationSuccess, value);
    }

    public ICommand ActivateLicenseCommand { get; }
    public ICommand StartDemoCommand { get; }
    public ICommand OpenPurchasePageCommand { get; }

    public LicenseViewModel(
        LicenseService licenseService,
        DemoManager demoManager,
        DatabaseService databaseService)
    {
        _licenseService = licenseService;
        _demoManager = demoManager;
        _databaseService = databaseService;

        Title = "เปิดใช้งาน License";

        // Get machine ID
        MachineId = _licenseService.GetMachineIdentity().MachineId;

        // Initialize commands
        ActivateLicenseCommand = new AsyncRelayCommand(ActivateLicenseAsync, () => !IsActivating);
        StartDemoCommand = new AsyncRelayCommand(StartDemoAsync, () => !IsCheckingDemo && CanStartDemo);
        OpenPurchasePageCommand = new RelayCommand(OpenPurchasePage);
    }

    /// <summary>
    /// เปิดใช้งาน License Key
    /// </summary>
    private async Task ActivateLicenseAsync()
    {
        if (string.IsNullOrWhiteSpace(LicenseKey))
        {
            StatusMessage = "กรุณากรอก License Key";
            return;
        }

        IsActivating = true;
        StatusMessage = "กำลังตรวจสอบ License Key...";

        try
        {
            var result = await _licenseService.ActivateLicenseAsync(LicenseKey.Trim());

            if (result.Success)
            {
                // Save to database
                var licenseInfo = _licenseService.GetCurrentLicense();
                if (licenseInfo != null)
                {
                    await _databaseService.SaveLicenseInfoAsync(licenseInfo);
                }

                StatusMessage = result.Message ?? "เปิดใช้งาน License สำเร็จ!";
                ActivationSuccess = true;
            }
            else
            {
                StatusMessage = result.Error ?? "ไม่สามารถเปิดใช้งาน License ได้";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"เกิดข้อผิดพลาด: {ex.Message}";
        }
        finally
        {
            IsActivating = false;
        }
    }

    /// <summary>
    /// เริ่มใช้งาน Demo
    /// </summary>
    private async Task StartDemoAsync()
    {
        IsCheckingDemo = true;
        StatusMessage = "กำลังตรวจสอบสิทธิ์ Demo...";

        try
        {
            var (success, message) = await _demoManager.StartDemoAsync();

            if (success)
            {
                // Save to database
                var demoInfo = _demoManager.GetDemoInfo();
                if (demoInfo != null)
                {
                    await _databaseService.SaveDemoInfoAsync(demoInfo);
                }

                StatusMessage = message;
                ActivationSuccess = true;
            }
            else
            {
                StatusMessage = message;
                CanStartDemo = false;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"เกิดข้อผิดพลาด: {ex.Message}";
        }
        finally
        {
            IsCheckingDemo = false;
        }
    }

    /// <summary>
    /// ไปหน้าซื้อ License
    /// </summary>
    private void OpenPurchasePage()
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "https://postxagent.com/pricing",
                UseShellExecute = true
            });
        }
        catch
        {
            // Ignore
        }
    }
}
