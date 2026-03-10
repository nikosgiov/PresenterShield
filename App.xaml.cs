using Hardcodet.Wpf.TaskbarNotification;
using System.Drawing;
using System.Windows;

namespace PresenterShield
{
    public partial class App : Application
    {
        private TaskbarIcon? _notifyIcon;
        private static System.Threading.Mutex? _mutex = null;

        protected override void OnStartup(StartupEventArgs e)
        {
            const string appName = "PresenterShield_SingleInstance_Mutex";
            bool createdNew;

            _mutex = new System.Threading.Mutex(true, appName, out createdNew);

            if (!createdNew)
            {
                // App is already running
                MessageBox.Show("An instance of PresenterShield is already running.", "PresenterShield", MessageBoxButton.OK, MessageBoxImage.Information);
                Current.Shutdown();
                return;
            }

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

        private void NotifyIcon_TrayMouseDoubleClick(object sender, RoutedEventArgs e)
        {
            if (MainWindow == null)
            {
                MainWindow = new Views.MainWindow();
            }

            if (MainWindow.IsVisible)
            {
                MainWindow.Hide();
            }
            else
            {
                MainWindow.Show();
                if (MainWindow.WindowState == WindowState.Minimized)
                {
                    MainWindow.WindowState = WindowState.Normal;
                }
                MainWindow.Activate();
            }
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            if (MainWindow?.DataContext is ViewModels.MainViewModel vm)
            {
                if (vm.StopSessionCommand.CanExecute(null))
                {
                    vm.StopSessionCommand.Execute(null);
                }
            }
            
            Current.Shutdown();
        }
    }
}
