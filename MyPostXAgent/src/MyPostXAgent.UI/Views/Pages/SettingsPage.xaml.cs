using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using MyPostXAgent.UI.ViewModels;

namespace MyPostXAgent.UI.Views.Pages;

/// <summary>
/// Settings Page - ตั้งค่าแอพพลิเคชัน
/// </summary>
public partial class SettingsPage : Page
{
    public SettingsPage()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<SettingsViewModel>();
    }
}
