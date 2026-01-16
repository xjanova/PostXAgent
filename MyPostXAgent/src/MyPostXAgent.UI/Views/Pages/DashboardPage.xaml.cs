using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using MyPostXAgent.UI.ViewModels;

namespace MyPostXAgent.UI.Views.Pages;

/// <summary>
/// Dashboard Page
/// </summary>
public partial class DashboardPage : Page
{
    public DashboardPage()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<DashboardViewModel>();
    }

    #region Quick Action Navigation

    private void NavigateToPage(string pageName)
    {
        // Get MainWindow and call NavigateToPage
        if (Application.Current.MainWindow is MainWindow mainWindow)
        {
            mainWindow.NavigateToPage(pageName);
        }
    }

    private void AIContentCard_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        NavigateToPage("AIContent");
    }

    private void VideoEditorCard_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        NavigateToPage("VideoEditor");
    }

    private void AddAccountCard_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        NavigateToPage("Accounts");
    }

    private void ImportFlowCard_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        NavigateToPage("Flows");
    }

    private void CreatePostButton_Click(object sender, RoutedEventArgs e)
    {
        NavigateToPage("Posts");
    }

    #endregion
}
