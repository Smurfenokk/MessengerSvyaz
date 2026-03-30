using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MessengerSvyaz.Models;
using MessengerSvyaz.ViewModels;

namespace MessengerSvyaz.Views;

public partial class DashboardView : UserControl
{
    public DashboardView()
    {
        InitializeComponent();
    }

    private void UserItem_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border border && border.Tag is User user)
        {
            if (DataContext is DashboardViewModel vm)
            {
                vm.StartChatCommand.Execute(user);
            }
        }
    }

    private void ChatItem_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border border && border.Tag is ChatPreview chat)
        {
            if (DataContext is DashboardViewModel vm)
            {
                vm.OpenChatCommand.Execute(chat);
            }
        }
    }

    private void SearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if (DataContext is DashboardViewModel vm)
            {
                vm.SearchCommand.Execute(null);
            }
        }
    }
}
