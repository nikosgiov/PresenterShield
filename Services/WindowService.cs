using PresenterShield.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace PresenterShield.Services
{
    public class WindowService
    {
        public List<WindowModel> GetOpenWindows()
        {
            var windows = new List<WindowModel>();
            NativeMethods.EnumWindows((hWnd, lParam) =>
            {
                if (IsEligibleWindow(hWnd))
                {
                    string title = GetWindowTitle(hWnd);
                    if (!string.IsNullOrWhiteSpace(title))
                    {
                        string className = GetWindowClassName(hWnd);
                        NativeMethods.GetWindowThreadProcessId(hWnd, out uint processId);
                        windows.Add(new WindowModel(hWnd, processId, title, className));
                    }
                }
                return true;
            }, IntPtr.Zero);

            return windows;
        }

        private bool IsEligibleWindow(IntPtr hWnd)
        {
            if (!NativeMethods.IsWindowVisible(hWnd))
                return false;

            // Filter out current process windows
            NativeMethods.GetWindowThreadProcessId(hWnd, out uint processId);
            if (processId == Process.GetCurrentProcess().Id)
                return false;

            // Ignore shell windows or hidden ones
            string title = GetWindowTitle(hWnd);
            if (title == "Program Manager" || title == "Settings") // quick hack for UWP background
                return false;

            return true;
        }

        private string GetWindowTitle(IntPtr hWnd)
        {
            int length = NativeMethods.GetWindowTextLength(hWnd);
            if (length == 0) return string.Empty;
            
            StringBuilder sb = new StringBuilder(length + 1);
            NativeMethods.GetWindowText(hWnd, sb, sb.Capacity);
            return sb.ToString();
        }

        private string GetWindowClassName(IntPtr hWnd)
        {
            StringBuilder sb = new StringBuilder(256);
            NativeMethods.GetClassName(hWnd, sb, sb.Capacity);
            return sb.ToString();
        }

        public void ApplyPrivacyOverlay(WindowModel window, byte opacity)
        {
            // 1. Exclude from capture (Remote Injection required because SetWindowDisplayAffinity checks process owner)
            bool affinitySet = ShellcodeInjector.SetWindowDisplayAffinityRemote(window.Handle, window.ProcessId, NativeMethods.WDA_EXCLUDEFROMCAPTURE);
            if (!affinitySet)
            {
                // Fallback attempt (in case it IS our process somehow)
                NativeMethods.SetWindowDisplayAffinity(window.Handle, NativeMethods.WDA_EXCLUDEFROMCAPTURE);
            }

            // 2. Hide from Taskbar / Alt-Tab
            long exStyle = (long)NativeMethods.GetWindowLongPtr(window.Handle, NativeMethods.GWL_EXSTYLE);
            exStyle = (exStyle & ~NativeMethods.WS_EX_APPWINDOW) | NativeMethods.WS_EX_TOOLWINDOW;
            
            // 3. Set opacity (requires LAYERED style)
            exStyle |= NativeMethods.WS_EX_LAYERED;
            NativeMethods.SetWindowLongPtr(window.Handle, NativeMethods.GWL_EXSTYLE, new IntPtr(exStyle));
            NativeMethods.SetLayeredWindowAttributes(window.Handle, 0, opacity, NativeMethods.LWA_ALPHA);

            // 4. Set Always On Top
            NativeMethods.SetWindowPos(window.Handle, NativeMethods.HWND_TOPMOST, 0, 0, 0, 0,
                NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_SHOWWINDOW);
        }

        public void RemovePrivacyOverlay(WindowModel window)
        {
            // 1. Restore capture visibility
            bool affinityRestored = ShellcodeInjector.SetWindowDisplayAffinityRemote(window.Handle, window.ProcessId, NativeMethods.WDA_NONE);
            if (!affinityRestored)
            {
                NativeMethods.SetWindowDisplayAffinity(window.Handle, NativeMethods.WDA_NONE);
            }

            // 2. Restore Taskbar / Alt-Tab
            long exStyle = (long)NativeMethods.GetWindowLongPtr(window.Handle, NativeMethods.GWL_EXSTYLE);
            exStyle = (exStyle & ~NativeMethods.WS_EX_TOOLWINDOW) | NativeMethods.WS_EX_APPWINDOW;
            
            // Remove transparency if we added it (Assuming window was not transparent before)
            exStyle &= ~NativeMethods.WS_EX_LAYERED;
            
            NativeMethods.SetWindowLongPtr(window.Handle, NativeMethods.GWL_EXSTYLE, new IntPtr(exStyle));

            // 3. Remove Always On Top
            NativeMethods.SetWindowPos(window.Handle, NativeMethods.HWND_NOTOPMOST, 0, 0, 0, 0,
                NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
        }
    }
}
