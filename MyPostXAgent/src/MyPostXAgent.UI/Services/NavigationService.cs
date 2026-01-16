namespace MyPostXAgent.UI.Services;

/// <summary>
/// Service สำหรับ navigation ระหว่างหน้า
/// </summary>
public class NavigationService
{
    public event EventHandler<string>? NavigationRequested;

    /// <summary>
    /// Navigate ไปยังหน้าที่ระบุ
    /// </summary>
    public void NavigateTo(string pageName)
    {
        NavigationRequested?.Invoke(this, pageName);
    }
}
