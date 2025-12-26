using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using MyPostXAgent.UI.ViewModels;

namespace MyPostXAgent.UI.Views.Pages;

public partial class PostsPage : Page
{
    public PostsPage()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<PostsViewModel>();
    }
}
