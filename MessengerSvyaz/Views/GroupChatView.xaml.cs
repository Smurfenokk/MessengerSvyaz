using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MessengerSvyaz.ViewModels;

namespace MessengerSvyaz.Views;

public partial class GroupChatView : UserControl
{
    public GroupChatView()
    {
        InitializeComponent();
    }

    private void MessageBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if (DataContext is GroupChatViewModel vm && vm.SendCommand.CanExecute(null))
            {
                vm.SendCommand.Execute(null);
            }
        }
    }

    private void Header_Click(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is GroupChatViewModel vm)
        {
            vm.OpenInfoCommand.Execute(null);
        }
    }

    private void ReactionButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is Models.GroupMessage msg && DataContext is GroupChatViewModel vm)
        {
            var emoji = btn.Tag?.ToString() ?? "";
            vm.AddReactionCommand.Execute(new object[] { msg.Id, emoji });
        }
    }

    private void Reaction_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border border && border.DataContext is System.Collections.Generic.KeyValuePair<string, System.Collections.Generic.List<string>> reaction)
        {
        }
    }

    private void Messages_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (e.VerticalOffset == 0 && e.VerticalChange < 0)
        {
            if (DataContext is GroupChatViewModel vm && vm.LoadMoreMessagesCommand.CanExecute(null))
            {
                vm.LoadMoreMessagesCommand.Execute(null);
            }
        }
    }
}
