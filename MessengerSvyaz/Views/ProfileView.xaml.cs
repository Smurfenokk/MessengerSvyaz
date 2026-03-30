using System.Windows;
using System.Windows.Controls;

namespace MessengerSvyaz.Views;

public partial class ProfileView : UserControl
{
    public ProfileView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is MessengerSvyaz.ViewModels.ProfileViewModel vm)
        {
            CurrentPasswordBox.PasswordChanged += (_, _) => vm.CurrentPassword = CurrentPasswordBox.Password;
            NewPasswordBox.PasswordChanged += (_, _) => vm.NewPassword = NewPasswordBox.Password;
            ConfirmPasswordBox.PasswordChanged += (_, _) => vm.ConfirmPassword = ConfirmPasswordBox.Password;
        }
    }
}