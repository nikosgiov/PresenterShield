using PresenterShield.Services;
using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace PresenterShield.Views
{
    public partial class MirrorWindow : Window
    {
        private readonly ScreenMirrorService _mirrorService;
        private bool _isClosing = false;
        private System.Drawing.Rectangle _targetScreenBounds;

        public MirrorWindow(ScreenMirrorService mirrorService, System.Windows.Forms.Screen targetScreen)
        {
            InitializeComponent();
            _mirrorService = mirrorService;
            
            // Subscribe to the frame captured event
            _mirrorService.FrameCaptured += OnFrameCaptured;

            // Position the window on the designated target screen
            PositionOnScreen(targetScreen);
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            // Exclude from capture to prevent feedback loops and as a safety measure
            // Doing this here ensures we have a valid HWND
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;

            // 1. Make click-through, hide from Alt-Tab, and don't activate
            int exStyle = (int)NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE);
            exStyle |= (int)NativeMethods.WS_EX_TRANSPARENT; 
            exStyle |= (int)NativeMethods.WS_EX_TOOLWINDOW;
            exStyle |= (int)NativeMethods.WS_EX_NOACTIVATE;
            NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE, new IntPtr(exStyle));

            // 2. Note: We do NOT use WDA_EXCLUDEFROMCAPTURE here because this window 
            // is intended to be visible on the external HDMI/Projector output.

            // 3. Position precisely using Win32 (ignores WPF DPI scaling issues)
            NativeMethods.SetWindowPos(hwnd, NativeMethods.HWND_TOPMOST, 
                _targetScreenBounds.Left, _targetScreenBounds.Top, 
                _targetScreenBounds.Width, _targetScreenBounds.Height, 
                NativeMethods.SWP_SHOWWINDOW | NativeMethods.SWP_NOACTIVATE);
        }

        private void PositionOnScreen(System.Windows.Forms.Screen targetScreen)
        {
            this.WindowStartupLocation = WindowStartupLocation.Manual;
            
            // Set styles before showing
            this.WindowStyle = WindowStyle.None;
            this.ResizeMode = ResizeMode.NoResize;
            this.Background = System.Windows.Media.Brushes.Black;
            
            // We'll use SetWindowPos in OnSourceInitialized for the actual placement
            // to ensure we're not fighting WPF's DPI scaling logic.
            _targetScreenBounds = targetScreen.Bounds;
        }

        private void OnFrameCaptured(BitmapSource bitmapSource)
        {
            if (_isClosing) return;

            // Use the dispatcher to update the image source
            this.Dispatcher.InvokeAsync(() =>
            {
                if (!_isClosing)
                {
                    MirrorImage.Source = bitmapSource;
                }
            }, System.Windows.Threading.DispatcherPriority.Render);
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _isClosing = true;
            _mirrorService.FrameCaptured -= OnFrameCaptured;
        }

        // Allow closing the mirror with Escape key in case they get stuck
        protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Key == Key.Escape)
            {
                this.Close();
            }
        }
    }
}
