using System.Windows;
using MessengerSvyaz.ViewModels;

namespace MessengerSvyaz.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }
}