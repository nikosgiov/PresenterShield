using System;
using System.Windows;
using System.Windows.Interop;
using PresenterShield.Services;

namespace PresenterShield.Views
{
    public partial class BorderWindow : Window
    {
        public IntPtr TargetHwnd { get; }

        public BorderWindow(IntPtr targetHwnd)
        {
            InitializeComponent();
            TargetHwnd = targetHwnd;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            var hwnd = new WindowInteropHelper(this).Handle;

            // Apply essential extended window styles
            int exStyle = (int)NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE);
            exStyle |= (int)NativeMethods.WS_EX_TRANSPARENT; // Click-through
            exStyle |= (int)NativeMethods.WS_EX_TOOLWINDOW;  // Hide from Alt-Tab
            exStyle |= (int)NativeMethods.WS_EX_NOACTIVATE;  // Don't take focus

            NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE, new IntPtr(exStyle));

            // Exclude from capture so it stays invisible on stream
            NativeMethods.SetWindowDisplayAffinity(hwnd, NativeMethods.WDA_EXCLUDEFROMCAPTURE);
            
            UpdatePosition();
        }

        public void UpdatePosition()
        {
            if (TargetHwnd == IntPtr.Zero) return;

            // Get the target window's rect, taking into account DWM drop shadows
            if (NativeMethods.DwmGetWindowAttribute(TargetHwnd, NativeMethods.DWMWA_EXTENDED_FRAME_BOUNDS, out NativeMethods.RECT rect, System.Runtime.InteropServices.Marshal.SizeOf(typeof(NativeMethods.RECT))) == 0)
            {
                int width = rect.Right - rect.Left;
                int height = rect.Bottom - rect.Top;

                var hwnd = new WindowInteropHelper(this).Handle;
                
                // Use SetWindowPos to position correctly regardless of WPF DPI scaling
                NativeMethods.SetWindowPos(hwnd, NativeMethods.HWND_TOPMOST, rect.Left, rect.Top, width, height, NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);
            }
            else if (NativeMethods.GetWindowRect(TargetHwnd, out rect))
            {
                // Fallback to GetWindowRect if DwmGetWindowAttribute fails
                int width = rect.Right - rect.Left;
                int height = rect.Bottom - rect.Top;

                var hwnd = new WindowInteropHelper(this).Handle;
                NativeMethods.SetWindowPos(hwnd, NativeMethods.HWND_TOPMOST, rect.Left, rect.Top, width, height, NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);
            }
        }
    }
}
