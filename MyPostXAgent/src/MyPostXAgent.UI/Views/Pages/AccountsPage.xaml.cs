using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using MyPostXAgent.UI.ViewModels;

namespace MyPostXAgent.UI.Views.Pages;

/// <summary>
/// Accounts Page - จัดการบัญชี Social Media
/// </summary>
public partial class AccountsPage : Page
{
    public AccountsPage()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<AccountsViewModel>();
    }
}
