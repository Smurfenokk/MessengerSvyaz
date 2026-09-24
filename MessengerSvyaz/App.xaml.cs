using System.Windows;
using System.Windows.Threading;
using System;

namespace MessengerSvyaz;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            MessageBox.Show($"Критическая ошибка: {args.ExceptionObject}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        };
        
        DispatcherUnhandledException += (s, args) =>
        {
            MessageBox.Show($"Ошибка UI: {args.Exception.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };
        
        TaskScheduler.UnobservedTaskException += (s, args) =>
        {
            MessageBox.Show($"Ошибка задачи: {args.Exception.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            args.SetObserved();
        };

        base.OnStartup(e);
    }
}