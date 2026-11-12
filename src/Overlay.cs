using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace QuietWinter
{
    internal sealed class SnowOverlay : Form
    {
        internal readonly SnowWorld World;
        internal readonly Screen Monitor;
        private readonly WinterRenderer renderer = new WinterRenderer();
        private Bitmap bitmap;
        private Graphics graphics;
        private IntPtr memoryDc, dib, oldBitmap;
        internal bool Sleeping;
        internal SnowOverlay(Screen monitor, Preferences settings, int seed)
        {
            Monitor = monitor;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            Bounds = monitor.Bounds;
            TopMost = true;
            Text = "QuietWinter.Overlay";
            AutoScaleMode = AutoScaleMode.None;
            int groundY = Math.Min(monitor.Bounds.Height, monitor.WorkingArea.Bottom - monitor.Bounds.Top);
            World = new SnowWorld(monitor.Bounds.Width, monitor.Bounds.Height, groundY, settings, seed);
        }
        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams p = base.CreateParams;
                p.ExStyle |= Native.WS_EX_LAYERED | Native.WS_EX_TRANSPARENT | Native.WS_EX_TOOLWINDOW | Native.WS_EX_NOACTIVATE;
                return p;
            }
        }
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            IntPtr screenDc = Native.GetDC(IntPtr.Zero);
            try
            {
                memoryDc = Native.CreateCompatibleDC(screenDc);
                Native.BITMAPINFO info = new Native.BITMAPINFO();
                info.biSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(Native.BITMAPINFO));
                info.biWidth = Width; info.biHeight = -Height; info.biPlanes = 1; info.biBitCount = 32;
                IntPtr bits;
                dib = Native.CreateDIBSection(screenDc, ref info, 0, out bits, IntPtr.Zero, 0);
                if (memoryDc == IntPtr.Zero || dib == IntPtr.Zero || bits == IntPtr.Zero) throw new Win32Exception();
                oldBitmap = Native.SelectObject(memoryDc, dib);
                bitmap = new Bitmap(Width, Height, Width * 4, PixelFormat.Format32bppPArgb, bits);
                graphics = Graphics.FromImage(bitmap);
            }
            finally { Native.ReleaseDC(IntPtr.Zero, screenDc); }
        }
        internal void RenderFrame(float dt)
        {
            if (Sleeping || IsDisposed) return;
            World.Step(dt);
            renderer.Draw(graphics, World, true);
            Native.POINT dest = new Native.POINT(Left, Top), source = new Native.POINT(0, 0);
            Native.SIZE size = new Native.SIZE(Width, Height);
            Native.BLENDFUNCTION blend = new Native.BLENDFUNCTION { SourceConstantAlpha = 255, AlphaFormat = 1 };
            if (!Native.UpdateLayeredWindow(Handle, IntPtr.Zero, ref dest, ref size, memoryDc, ref source, 0, ref blend, 2))
                throw new Win32Exception(System.Runtime.InteropServices.Marshal.GetLastWin32Error());
        }
        internal void Sleep(bool value)
        {
            if (Sleeping == value) return;
            Sleeping = value;
            if (value) Hide();
            else
            {
                Show();
                Native.SetWindowPos(Handle, Native.HWND_TOPMOST, 0, 0, 0, 0, 0x13);
            }
        }
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x84) { m.Result = new IntPtr(-1); return; }
            if (m.Msg == 0x21) { m.Result = new IntPtr(3); return; }
            base.WndProc(ref m);
        }
        protected override void OnPaint(PaintEventArgs e) { }
        protected override void OnPaintBackground(PaintEventArgs e) { }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (graphics != null) { graphics.Dispose(); graphics = null; }
                if (bitmap != null) { bitmap.Dispose(); bitmap = null; }
                if (memoryDc != IntPtr.Zero && oldBitmap != IntPtr.Zero) Native.SelectObject(memoryDc, oldBitmap);
                if (dib != IntPtr.Zero) { Native.DeleteObject(dib); dib = IntPtr.Zero; }
                if (memoryDc != IntPtr.Zero) { Native.DeleteDC(memoryDc); memoryDc = IntPtr.Zero; }
                renderer.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
