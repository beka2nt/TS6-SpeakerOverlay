using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace TS6_SpeakerOverlay.Helpers
{
    public static class WindowHelper
    {
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_LAYERED = 0x00080000;
        private const int GWL_EXSTYLE = -20;
        
        // 新增：置顶相关常量
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOACTIVATE = 0x0010;

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hwnd, int index);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

        // 新增：强制设置窗口位置的 API
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        public static void EnableClickThrough(Window window)
        {
            var helper = new WindowInteropHelper(window);
            if (helper.Handle == IntPtr.Zero) return;
            int extendedStyle = GetWindowLong(helper.Handle, GWL_EXSTYLE);
            SetWindowLong(helper.Handle, GWL_EXSTYLE, extendedStyle | WS_EX_TRANSPARENT | WS_EX_LAYERED);
        }

        public static void DisableClickThrough(Window window)
        {
            var helper = new WindowInteropHelper(window);
            if (helper.Handle == IntPtr.Zero) return;
            int extendedStyle = GetWindowLong(helper.Handle, GWL_EXSTYLE);
            SetWindowLong(helper.Handle, GWL_EXSTYLE, extendedStyle & ~WS_EX_TRANSPARENT);
        }

        // 新增：暴力置顶方法
        public static void ForceTopMost(Window window)
        {
            if (!window.IsVisible) return;

            var helper = new WindowInteropHelper(window);
            if (helper.Handle == IntPtr.Zero) return;

            // 强制将窗口置于 Z 轴最顶层，且不改变大小和位置，不激活窗口
            SetWindowPos(helper.Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        }
    }
}
