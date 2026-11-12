using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace QuietWinter
{
    // Media generation never captures a screen or enumerates real windows.
    // All pixels come from this fictional desktop and the production renderer.
    internal static class DemoScene
    {
        internal const int Width = 1600, Height = 900, Fps = 30, Seconds = 14;
        private static SnowWorld World()
        {
            SnowWorld world = new SnowWorld(Width, Height, 846, new Preferences(), 24122026);
            world.LocalTime = new DateTime(2026, 12, 24, 19, 30, 0);
            world.DiggerX = 515;
            world.Ledges.Add(new SnowSurface(new IntPtr(1), 177, 313, 654, 24));
            world.Ledges.Add(new SnowSurface(new IntPtr(2), 967, 413, 414, 24));
            foreach (SnowSurface ledge in world.Ledges)
                for (int i = 0; i < ledge.Heights.Length; i++) ledge.Heights[i] = 10 + (float)Math.Sin(i * .17) * 2 + (float)Math.Sin(i * .39) * 1.4f;
            for (int i = 0; i < world.Ground.Heights.Length; i++) world.Ground.Heights[i] = 21 + (float)Math.Sin(i * .095) * 4 + (float)Math.Sin(i * .23) * 2.5f;
            world.Ground.Clear(0, world.DiggerX + 60, 1);
            return world;
        }
        private static Bitmap Backdrop()
        {
            Bitmap bitmap = new Bitmap(Width, Height, PixelFormat.Format24bppRgb);
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (LinearGradientBrush bg = new LinearGradientBrush(new Point(0, 0), new Point(Width, Height), Color.FromArgb(12, 29, 45), Color.FromArgb(31, 67, 70))) g.FillRectangle(bg, 0, 0, Width, Height);
                using (SolidBrush moon = new SolidBrush(Color.FromArgb(220, 230, 212))) g.FillEllipse(moon, 1210, 164, 60, 60);
                using (SolidBrush hill = new SolidBrush(Color.FromArgb(20, 46, 59)))
                    g.FillClosedCurve(hill, new[] { new PointF(-100, 600), new PointF(330, 560), new PointF(800, 700),
                        new PointF(1300, 510), new PointF(1700, 610), new PointF(1700, 1000), new PointF(-100, 1000) });
                using (SolidBrush hill = new SolidBrush(Color.FromArgb(27, 61, 68)))
                    g.FillClosedCurve(hill, new[] { new PointF(-100, 770), new PointF(230, 690), new PointF(780, 810),
                        new PointF(1270, 690), new PointF(1700, 740), new PointF(1700, 1000), new PointF(-100, 1000) });
                using (Font brand = new Font("Segoe UI", 12, FontStyle.Bold))
                using (Font title = new Font("Segoe UI", 37, FontStyle.Bold))
                using (Font subtitle = new Font("Segoe UI", 14))
                using (SolidBrush mint = new SolidBrush(Color.FromArgb(189, 225, 208)))
                using (SolidBrush white = new SolidBrush(Color.FromArgb(239, 246, 244)))
                using (SolidBrush muted = new SolidBrush(Color.FromArgb(157, 184, 193)))
                {
                    g.DrawString("ТИХАЯ ЗИМА  /  QUIET WINTER", brand, mint, 176, 91);
                    g.DrawString("Маленькая зима. Прямо на экране.", title, white, 170, 125);
                    g.DrawString("Снег, сугробы и экскаватор в новогодней шапке.", subtitle, muted, 177, 205);
                }
                Verification.DrawPreviewWindow(g, new Rectangle(175, 313, 660, 329), "Проводник · Зимняя коллекция", true);
                Verification.DrawPreviewWindow(g, new Rectangle(965, 413, 420, 259), "Зимние планы", false);
                WinterRenderer.RoundFill(g, Color.FromArgb(14, 26, 39), 0, 847, Width, 53, 1);
                for (int i = 0; i < 6; i++) WinterRenderer.RoundFill(g, Color.FromArgb(96 + i * 9, 153 + i * 4, 162 + i * 4), 674 + i * 44, 861, 26, 26, 6);
                using (Font f = new Font("Segoe UI", 8))
                using (SolidBrush b = new SolidBrush(Color.FromArgb(131, 157, 167))) g.DrawString("Демонстрационная сцена · вымышленные окна", f, b, 1225, 869);
            }
            return bitmap;
        }
        internal static void Export(string folder, string ffmpeg)
        {
            Directory.CreateDirectory(folder);
            string destination = Path.GetFullPath(Path.Combine(folder, "native-demo.mp4"));
            ProcessStartInfo start = new ProcessStartInfo(ffmpeg ?? "ffmpeg.exe",
                "-hide_banner -loglevel error -y -f rawvideo -pixel_format bgr24 -video_size 1600x900 -framerate 30 -i pipe:0 " +
                "-an -c:v libx264 -preset fast -crf 18 -pix_fmt yuv420p -movflags +faststart \"" + destination + "\"");
            start.UseShellExecute = false; start.CreateNoWindow = true;
            start.RedirectStandardInput = true; start.RedirectStandardError = true;
            StringBuilder errors = new StringBuilder();
            using (Process encoder = Process.Start(start))
            using (Bitmap background = Backdrop())
            using (Bitmap frame = new Bitmap(Width, Height, PixelFormat.Format24bppRgb))
            using (Graphics g = Graphics.FromImage(frame))
            using (WinterRenderer renderer = new WinterRenderer())
            {
                encoder.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e) { if (e.Data != null) errors.AppendLine(e.Data); };
                encoder.BeginErrorReadLine();
                SnowWorld world = World();
                byte[] pixels = new byte[Width * Height * 3];
                for (int index = 0; index < Seconds * Fps; index++)
                {
                    float t = index / (float)Fps;
                    if (index == 4 * Fps) world.SnowBurst();
                    if (t >= 8 && t < 12) world.Sweep(205 + (t - 8) / 4 * 585, 309);
                    world.Step(1f / Fps);
                    g.DrawImageUnscaled(background, 0, 0);
                    renderer.Draw(g, world, false);
                    if (t >= 8 && t < 12) DrawMitten(g, 205 + (t - 8) / 4 * 585, 309);
                    if (index == 90) frame.Save(Path.Combine(folder, "screenshot.png"), ImageFormat.Png);
                    g.Flush(FlushIntention.Sync);
                    BitmapData data = frame.LockBits(new Rectangle(0, 0, Width, Height), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
                    try { Marshal.Copy(data.Scan0, pixels, 0, pixels.Length); }
                    finally { frame.UnlockBits(data); }
                    encoder.StandardInput.BaseStream.Write(pixels, 0, pixels.Length);
                }
                encoder.StandardInput.Close();
                encoder.WaitForExit();
                if (encoder.ExitCode != 0) throw new IOException("FFmpeg: " + errors);
            }
            File.WriteAllText(Path.Combine(folder, "provenance.txt"),
                "Generated offline by DemoScene.cs using SnowWorld and WinterRenderer.\n" +
                "Fictional desktop; no screen capture, user files, window titles, audio input or network content.\n" +
                "1600x900, 30 fps, 14 seconds. Calendar fixed to 2026-12-24 19:30. Seed 24122026.\n");
        }
        private static void DrawMitten(Graphics g, float x, float y)
        {
            GraphicsState state = g.Save();
            g.TranslateTransform(x, y);
            using (SolidBrush cuff = new SolidBrush(Color.FromArgb(239, 242, 233))) g.FillEllipse(cuff, -13, 8, 28, 11);
            WinterRenderer.RoundFill(g, Color.FromArgb(211, 84, 75), -12, -16, 25, 33, 11);
            WinterRenderer.RoundFill(g, Color.FromArgb(232, 109, 94), 7, -5, 15, 18, 7);
            using (Font f = new Font("Segoe UI", 9, FontStyle.Bold))
            using (SolidBrush b = new SolidBrush(Color.FromArgb(217, 233, 230))) g.DrawString("Shift", f, b, -15, -43);
            g.Restore(state);
        }
    }
}
