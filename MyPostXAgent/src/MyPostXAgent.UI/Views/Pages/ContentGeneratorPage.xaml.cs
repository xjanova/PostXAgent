using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using MyPostXAgent.UI.ViewModels;

namespace MyPostXAgent.UI.Views.Pages;

/// <summary>
/// AI Content Generator Page - สร้างเนื้อหาด้วย AI
/// </summary>
public partial class ContentGeneratorPage : Page
{
    public ContentGeneratorPage()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<ContentGeneratorViewModel>();
    }
}
