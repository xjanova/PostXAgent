using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using MyPostXAgent.UI.ViewModels;

namespace MyPostXAgent.UI;

/// <summary>
/// Main Window - Premium RGB Design
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();

        _viewModel = App.Services.GetRequiredService<MainViewModel>();
        DataContext = _viewModel;

        // Navigate to Dashboard after window is loaded
        Loaded += MainWindow_Loaded;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Navigate to Dashboard by default
        NavigateTo("Dashboard");
    }

    #region Window Controls

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            MaximizeButton_Click(sender, e);
        }
        else
        {
            DragMove();
        }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    #endregion

    #region Navigation

    private void NavigationMenu_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (NavigationMenu.SelectedItem is ListBoxItem item && item.Tag is string page)
        {
            NavigateTo(page);
        }
    }

    private void NavigateTo(string pageName)
    {
        var pageUri = pageName switch
        {
            "Dashboard" => new Uri("Views/Pages/DashboardPage.xaml", UriKind.Relative),
            "Posts" => new Uri("Views/Pages/PostsPage.xaml", UriKind.Relative),
            "Schedule" => new Uri("Views/Pages/SchedulerPage.xaml", UriKind.Relative),
            "Accounts" => new Uri("Views/Pages/AccountsPage.xaml", UriKind.Relative),
            "AIContent" => new Uri("Views/Pages/ContentGeneratorPage.xaml", UriKind.Relative),
            "VideoEditor" => new Uri("Views/Pages/VideoEditorPage.xaml", UriKind.Relative),
            "Groups" => new Uri("Views/Pages/GroupSearchPage.xaml", UriKind.Relative),
            "Comments" => new Uri("Views/Pages/CommentManagerPage.xaml", UriKind.Relative),
            "Flows" => new Uri("Views/Pages/FlowManagerPage.xaml", UriKind.Relative),
            "Settings" => new Uri("Views/Pages/SettingsPage.xaml", UriKind.Relative),
            _ => new Uri("Views/Pages/DashboardPage.xaml", UriKind.Relative)
        };

        try
        {
            if (MainFrame != null)
            {
                MainFrame.Navigate(pageUri);
            }
        }
        catch
        {
            // Page not yet implemented - show placeholder
            if (MainFrame != null)
            {
                MainFrame.Content = CreatePlaceholder(pageName);
            }
        }
    }

    private static UIElement CreatePlaceholder(string pageName)
    {
        return new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Children =
            {
                new TextBlock
                {
                    Text = $"{pageName}",
                    FontSize = 32,
                    FontWeight = FontWeights.Bold,
                    Opacity = 0.3,
                    HorizontalAlignment = HorizontalAlignment.Center
                },
                new TextBlock
                {
                    Text = "กำลังพัฒนา...",
                    FontSize = 16,
                    Opacity = 0.5,
                    Margin = new Thickness(0, 10, 0, 0),
                    HorizontalAlignment = HorizontalAlignment.Center
                }
            }
        };
    }

    #endregion
}
