using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MessengerSvyaz.ViewModels;

namespace MessengerSvyaz.Views;

public partial class LoginView : UserControl
{
    public LoginView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is LoginViewModel vm)
        {
            PasswordBox.PasswordChanged += (_, _) => vm.Password = PasswordBox.Password;
            RegPasswordBox.PasswordChanged += (_, _) => vm.RegPassword = RegPasswordBox.Password;
        }
    }

    private void InputBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if (DataContext is LoginViewModel vm)
            {
                if (vm.IsRegistering)
                {
                    if (sender == RegKeyBox)
                        vm.RegisterCommand.Execute(null);
                    else
                        (FocusManager.GetFocusScope(sender as TextBox) as FrameworkElement)?.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
                }
                else
                {
                    if (sender == PasswordBox)
                        vm.LoginCommand.Execute(null);
                    else
                        (FocusManager.GetFocusScope(sender as TextBox) as FrameworkElement)?.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
                }
            }
        }
    }
}