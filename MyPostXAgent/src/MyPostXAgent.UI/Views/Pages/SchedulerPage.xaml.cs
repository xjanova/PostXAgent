using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using MyPostXAgent.UI.ViewModels;

namespace MyPostXAgent.UI.Views.Pages;

/// <summary>
/// Scheduler Page - จัดตารางโพสต์
/// </summary>
public partial class SchedulerPage : Page
{
    public SchedulerPage()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<SchedulerViewModel>();
    }
}
