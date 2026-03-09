using Hardcodet.Wpf.TaskbarNotification;
using System.Drawing;
using System.Windows;

namespace PresenterShield
{
    public partial class App : Application
    {
        private TaskbarIcon? _notifyIcon;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            _notifyIcon = (TaskbarIcon)FindResource("NotifyIcon");
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _notifyIcon?.Dispose();
            base.OnExit(e);
        }

        private void ShowMainWindow_Click(object sender, RoutedEventArgs e)
        {
            if (MainWindow == null)
            {
                MainWindow = new Views.MainWindow();
            }
            
            MainWindow.Show();
            
            if (MainWindow.WindowState == WindowState.Minimized)
            {
                MainWindow.WindowState = WindowState.Normal;
            }
            
            MainWindow.Activate();
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Current.Shutdown();
        }
    }
}
