using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace QuietWinter
{
    internal static class Native
    {
        internal const int WS_EX_LAYERED = 0x80000, WS_EX_TRANSPARENT = 0x20,
            WS_EX_TOOLWINDOW = 0x80, WS_EX_NOACTIVATE = 0x08000000;
        internal static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        [StructLayout(LayoutKind.Sequential)] internal struct POINT
        {
            public int x, y;
            public POINT(int x, int y) { this.x = x; this.y = y; }
        }
        [StructLayout(LayoutKind.Sequential)] internal struct SIZE
        {
            public int cx, cy;
            public SIZE(int x, int y) { cx = x; cy = y; }
        }
        [StructLayout(LayoutKind.Sequential)] internal struct RECT
        {
            public int Left, Top, Right, Bottom;
            public Rectangle Rect { get { return Rectangle.FromLTRB(Left, Top, Right, Bottom); } }
        }
        [StructLayout(LayoutKind.Sequential, Pack = 1)] internal struct BLENDFUNCTION
        {
            public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat;
        }
        [StructLayout(LayoutKind.Sequential)] internal struct BITMAPINFO
        {
            public uint biSize;
            public int biWidth, biHeight;
            public ushort biPlanes, biBitCount;
            public uint biCompression, biSizeImage;
            public int biXPelsPerMeter, biYPelsPerMeter;
            public uint biClrUsed, biClrImportant;
        }
        internal delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lparam);
        [DllImport("user32.dll")] internal static extern bool EnumWindows(EnumWindowsProc callback, IntPtr param);
        [DllImport("user32.dll")] internal static extern bool IsWindowVisible(IntPtr hwnd);
        [DllImport("user32.dll")] internal static extern bool IsIconic(IntPtr hwnd);
        [DllImport("user32.dll")] internal static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
        [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern int GetClassName(IntPtr hwnd, StringBuilder text, int count);
        [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] internal static extern int GetWindowLong(IntPtr hwnd, int index);
        [DllImport("user32.dll")] internal static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
        [DllImport("user32.dll")] internal static extern bool ShowWindow(IntPtr hwnd, int command);
        [DllImport("user32.dll")] internal static extern IntPtr GetDC(IntPtr hwnd);
        [DllImport("user32.dll")] internal static extern int ReleaseDC(IntPtr hwnd, IntPtr dc);
        [DllImport("gdi32.dll")] internal static extern IntPtr CreateCompatibleDC(IntPtr dc);
        [DllImport("gdi32.dll")] internal static extern IntPtr CreateDIBSection(IntPtr dc, ref BITMAPINFO info, uint usage, out IntPtr bits, IntPtr section, uint offset);
        [DllImport("gdi32.dll")] internal static extern IntPtr SelectObject(IntPtr dc, IntPtr item);
        [DllImport("gdi32.dll")] internal static extern bool DeleteObject(IntPtr item);
        [DllImport("gdi32.dll")] internal static extern bool DeleteDC(IntPtr dc);
        [DllImport("user32.dll", SetLastError = true)] internal static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr destDc,
            ref POINT position, ref SIZE size, IntPtr sourceDc, ref POINT source, uint color, ref BLENDFUNCTION blend, uint flags);
        [DllImport("user32.dll")] internal static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint key);
        [DllImport("user32.dll")] internal static extern bool UnregisterHotKey(IntPtr hwnd, int id);
        [DllImport("user32.dll")] internal static extern bool SetProcessDpiAwarenessContext(IntPtr context);
        [DllImport("user32.dll")] internal static extern bool SetProcessDPIAware();
        [DllImport("dwmapi.dll")] internal static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out RECT value, int size);
        [DllImport("dwmapi.dll", EntryPoint = "DwmGetWindowAttribute")] internal static extern int DwmGetWindowIntAttribute(IntPtr hwnd, int attribute, out int value, int size);
        [DllImport("user32.dll")] internal static extern bool DestroyIcon(IntPtr icon);
        [DllImport("user32.dll")] internal static extern bool GetCursorPos(out POINT point);
        [DllImport("user32.dll")] internal static extern short GetAsyncKeyState(int key);
        [DllImport("winmm.dll")] internal static extern uint timeBeginPeriod(uint period);
        [DllImport("winmm.dll")] internal static extern uint timeEndPeriod(uint period);

        internal static void EnableDpi()
        {
            try { if (SetProcessDpiAwarenessContext(new IntPtr(-4))) return; }
            catch (EntryPointNotFoundException) { }
            SetProcessDPIAware();
        }
        internal static string ClassName(IntPtr hwnd)
        {
            StringBuilder b = new StringBuilder(128);
            GetClassName(hwnd, b, b.Capacity);
            return b.ToString();
        }
        internal static bool IsShell(string name)
        {
            return name == "Progman" || name == "WorkerW" || name == "Shell_TrayWnd" ||
                name == "Shell_SecondaryTrayWnd" || name == "XamlExplorerHostIslandWindow";
        }
        internal static bool FullscreenOn(Rectangle screen)
        {
            IntPtr foreground = GetForegroundWindow();
            if (foreground == IntPtr.Zero || IsIconic(foreground) || IsShell(ClassName(foreground))) return false;
            uint pid;
            GetWindowThreadProcessId(foreground, out pid);
            if (pid == (uint)System.Diagnostics.Process.GetCurrentProcess().Id) return false;
            RECT rect;
            if (!GetWindowRect(foreground, out rect)) return false;
            return CoversMonitor(rect.Rect, screen);
        }
        internal static bool CoversMonitor(Rectangle window, Rectangle screen)
        {
            return window.Left <= screen.Left + 1 && window.Top <= screen.Top + 1 &&
                window.Right >= screen.Right - 1 && window.Bottom >= screen.Bottom - 1;
        }
        internal static List<WindowInfo> ReadWindows()
        {
            List<WindowInfo> windows = new List<WindowInfo>();
            uint ownPid = (uint)System.Diagnostics.Process.GetCurrentProcess().Id;
            EnumWindows(delegate(IntPtr hwnd, IntPtr ignored)
            {
                uint pid;
                GetWindowThreadProcessId(hwnd, out pid);
                if (pid == ownPid || !IsWindowVisible(hwnd) || IsIconic(hwnd)) return true;
                string name = ClassName(hwnd);
                if (IsShell(name) || name == "tooltips_class32" || name == "#32768") return true;
                int style = GetWindowLong(hwnd, -20);
                if ((style & WS_EX_TOOLWINDOW) != 0) return true;
                int cloaked;
                if (DwmGetWindowIntAttribute(hwnd, 14, out cloaked, 4) == 0 && cloaked != 0) return true;
                RECT r;
                if (DwmGetWindowAttribute(hwnd, 9, out r, Marshal.SizeOf(typeof(RECT))) != 0 && !GetWindowRect(hwnd, out r)) return true;
                Rectangle bounds = r.Rect;
                if (bounds.Width < 180 || bounds.Height < 85) return true;
                windows.Add(new WindowInfo(hwnd, bounds));
                return windows.Count < 60;
            }, IntPtr.Zero);
            return windows;
        }
    }
    internal sealed class WindowInfo
    {
        internal IntPtr Handle;
        internal Rectangle Bounds;
        internal WindowInfo(IntPtr handle, Rectangle bounds) { Handle = handle; Bounds = bounds; }
    }
    internal sealed class HotkeyWindow : NativeWindow, IDisposable
    {
        internal Action Pause, Quit, Burst;
        internal readonly bool PauseRegistered, QuitRegistered;
        internal HotkeyWindow()
        {
            CreateHandle(new CreateParams { Caption = "QuietWinter.Hotkeys", Parent = new IntPtr(-3) });
            PauseRegistered = Native.RegisterHotKey(Handle, 1, 0x4003, (uint)Keys.S);
            QuitRegistered = Native.RegisterHotKey(Handle, 2, 0x4003, (uint)Keys.Q);
            Native.RegisterHotKey(Handle, 3, 0x4003, (uint)Keys.B);
        }
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x312)
            {
                if (m.WParam.ToInt32() == 1 && Pause != null) Pause();
                if (m.WParam.ToInt32() == 2 && Quit != null) Quit();
                if (m.WParam.ToInt32() == 3 && Burst != null) Burst();
            }
            base.WndProc(ref m);
        }
        public void Dispose()
        {
            Native.UnregisterHotKey(Handle, 1);
            Native.UnregisterHotKey(Handle, 2);
            Native.UnregisterHotKey(Handle, 3);
            DestroyHandle();
        }
    }
}
