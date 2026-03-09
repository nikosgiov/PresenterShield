using PresenterShield.Services;
using System;
using System.Windows;
using System.Windows.Interop;

namespace PresenterShield.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            this.SourceInitialized += MainWindow_SourceInitialized;
        }

        private void MainWindow_SourceInitialized(object? sender, EventArgs e)
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            NativeMethods.SetWindowDisplayAffinity(hwnd, NativeMethods.WDA_EXCLUDEFROMCAPTURE);
        }
    }
}
