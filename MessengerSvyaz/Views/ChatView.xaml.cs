using System.Windows.Controls;
using System.Windows.Input;
using MessengerSvyaz.ViewModels;
namespace MessengerSvyaz.Views;

public partial class ChatView : UserControl
{
    public ChatView()
    {
        InitializeComponent();
    }

    private void MessageBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if (DataContext is ChatViewModel vm && vm.SendCommand.CanExecute(null))
            {
                vm.SendCommand.Execute(null);
            }
        }
    }

    private void Messages_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (e.VerticalOffset == 0 && e.VerticalChange < 0)
        {
            if (DataContext is ChatViewModel vm && vm.LoadMoreMessagesCommand.CanExecute(null))
            {
                vm.LoadMoreMessagesCommand.Execute(null);
            }
        }
    }
}
