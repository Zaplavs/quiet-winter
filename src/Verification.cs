using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace QuietWinter
{
    [DataContract] internal sealed class CheckResult
    {
        [DataMember] internal string Name;
        [DataMember] internal bool Passed;
        [DataMember] internal string Detail;
    }
    internal static class Verification
    {
        internal static int Run(string destination)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(destination)));
            List<CheckResult> checks = new List<CheckResult>();
            Check(checks, "Snow settles on window tops", delegate
            {
                SnowWorld world = new SnowWorld(640, 480, 440, new Preferences { Excavator = false }, 42);
                SnowSurface ledge = new SnowSurface(new IntPtr(1), 80, 200, 320, 23);
                world.Ledges.Add(ledge); world.Flakes.Clear();
                world.Flakes.Add(new Flake { X = 200, Y = 190, Speed = 40, Radius = 2, Depth = 1 });
                for (int i = 0; i < 10; i++) world.Step(.033f);
                Require(ledge.HeightAt(200) > .1f, "Flake did not deposit snow on the ledge.");
            });
            Check(checks, "Snow settles above taskbar", delegate
            {
                SnowWorld world = new SnowWorld(640, 480, 440, new Preferences { Excavator = false }, 42);
                world.ResetSnow(); world.Flakes.Clear();
                world.Flakes.Add(new Flake { X = 200, Y = 435, Speed = 40, Radius = 2, Depth = 1 });
                for (int i = 0; i < 10; i++) world.Step(.033f);
                Require(world.Ground.HeightAt(200) > .1f, "No snow deposited at taskbar boundary.");
            });
            Check(checks, "Excavator removes accumulated snow and reverses", delegate
            {
                SnowWorld world = new SnowWorld(640, 480, 440, new Preferences { Excavator = true }, 42);
                for (int i = 0; i < world.Ground.Heights.Length; i++) world.Ground.Heights[i] = 26;
                for (int i = 0; i < 800; i++) world.Step(.05f);
                Require(world.Cleared > 1000, "Excavator did not clear the snow.");
                Require(world.Passes >= 1, "Excavator failed to reverse at the screen edge.");
                Require(world.Spray.Count <= 195, "Snow spray is not bounded.");
            });
            Check(checks, "Call excavator while automatic passes disabled", delegate
            {
                SnowWorld world = new SnowWorld(640, 480, 440, new Preferences { Excavator = false }, 42);
                float start = world.DiggerX;
                world.Step(.05f);
                Require(world.DiggerX == start, "Disabled excavator moved.");
                world.CallDigger(); world.Step(.05f);
                Require(world.DiggerX > start && world.ForceDigger, "Manual pass did not start.");
                for (int i = 0; i < 600; i++) world.Step(.05f);
                Require(!world.ForceDigger && world.Passes == 1, "Manual pass did not stop after one pass.");
            });
            Check(checks, "Window overlap hides covered snow", delegate
            {
                SnowWorld world = new SnowWorld(1000, 700, 660, new Preferences(), 42);
                world.UpdateWindows(new List<WindowInfo> {
                    new WindowInfo(new IntPtr(1), new Rectangle(200, 100, 300, 400)),
                    new WindowInfo(new IntPtr(2), new Rectangle(80, 200, 700, 400)) }, new Rectangle(0, 0, 1000, 700));
                SnowSurface rear = world.Ledges.Find(delegate(SnowSurface s) { return s.Id == new IntPtr(2); });
                Require(rear != null && !rear.Contains(300) && rear.Contains(150) && rear.Contains(600), "Covered ledge remained exposed.");
            });
            Check(checks, "Window snow moves and resizes with its window", delegate
            {
                SnowWorld world = new SnowWorld(1000, 700, 660, new Preferences(), 42);
                List<WindowInfo> windows = new List<WindowInfo> { new WindowInfo(new IntPtr(1), new Rectangle(100, 200, 400, 300)) };
                world.UpdateWindows(windows, new Rectangle(0, 0, 1000, 700));
                SnowSurface surface = world.Ledges[0]; surface.Deposit(250, 10);
                float max = Max(surface.Heights);
                windows[0].Bounds = new Rectangle(300, 150, 600, 300);
                world.UpdateWindows(windows, new Rectangle(0, 0, 1000, 700));
                Require(world.Ledges[0] == surface && surface.X == 303 && surface.Y == 151 && Math.Abs(Max(surface.Heights) - max) < 1,
                    "Accumulation was lost during window movement or resizing.");
            });
            Check(checks, "Secondary monitor with negative coordinates", delegate
            {
                SnowWorld world = new SnowWorld(1280, 1024, 984, new Preferences(), 42);
                world.UpdateWindows(new List<WindowInfo> { new WindowInfo(new IntPtr(1), new Rectangle(-1200, 200, 800, 500)) }, new Rectangle(-1280, 0, 1280, 1024));
                Require(world.Ledges.Count == 1 && world.Ledges[0].X == 83 && world.Ledges[0].Contains(200), "Physical monitor coordinates were not translated.");
            });
            Check(checks, "Fullscreen covers the monitor; maximized work area does not", delegate
            {
                Rectangle monitor = new Rectangle(0, 0, 1920, 1080);
                Require(Native.CoversMonitor(monitor, monitor), "Fullscreen was not detected.");
                Require(!Native.CoversMonitor(new Rectangle(-8, -8, 1936, 1048), monitor), "Maximized window was mistaken for fullscreen.");
                Rectangle secondary = new Rectangle(-1920, 0, 1920, 1080);
                Require(Native.CoversMonitor(secondary, secondary) && !Native.CoversMonitor(monitor, secondary), "Fullscreen monitor coordinates incorrect.");
            });
            Check(checks, "Shift broom clears ground and window snow and can be disabled", delegate
            {
                Preferences p = new Preferences { Excavator = false };
                SnowWorld world = new SnowWorld(800, 600, 560, p, 42);
                SnowSurface ledge = new SnowSurface(new IntPtr(1), 80, 200, 500, 24);
                for (int i = 0; i < ledge.Heights.Length; i++) ledge.Heights[i] = 18;
                world.Ledges.Add(ledge);
                world.Sweep(250, 195);
                Require(ledge.HeightAt(250) < 1 && world.Swept > 10, "Broom did not clear window snow.");
                world.Ground.Deposit(400, 20); world.Sweep(400, 550);
                Require(world.Ground.HeightAt(400) < 1 && world.Spray.Count > 0, "Broom did not clear ground or emit snow.");
                p.CursorBroom = false; ledge.Deposit(350, 15); float before = ledge.HeightAt(350);
                world.Sweep(350, 195);
                Require(ledge.HeightAt(350) == before, "Disabled broom changed snow.");
            });
            Check(checks, "Broom cannot clear covered window spans", delegate
            {
                SnowWorld world = new SnowWorld(800, 600, 560, new Preferences(), 42);
                SnowSurface ledge = new SnowSurface(new IntPtr(1), 80, 200, 500, 24);
                ledge.Deposit(300, 18); ledge.Exclude(200, 400); world.Ledges.Add(ledge);
                float before = ledge.HeightAt(300); world.Sweep(300, 195);
                Require(ledge.HeightAt(300) == before && world.Swept == 0, "Broom reached behind an overlapping window.");
            });
            Check(checks, "Snow burst finishes and preserves the chosen particle budget", delegate
            {
                Preferences p = new Preferences { Density = 0 };
                SnowWorld world = new SnowWorld(1920, 1080, 1040, p, 42);
                int count = world.Flakes.Count;
                world.SnowBurst(); world.SnowBurst();
                Require(world.BurstRemaining > 0, "Burst did not start.");
                for (int i = 0; i < 160; i++) world.Step(.05f);
                Require(world.BurstRemaining == 0 && world.Flakes.Count == count && p.Density == 0, "Burst did not end or changed preferences.");
            });
            Check(checks, "Holiday clock handles the year boundary and New Year's Day", delegate
            {
                Require(HolidayClock.Text(new DateTime(2026, 12, 31, 23, 59, 0)) == "0 д  00 ч  01 м", "Last minute countdown is wrong.");
                Require(HolidayClock.Text(new DateTime(2027, 1, 1, 17, 0, 0)) == "С Новым годом!", "New Year greeting is missing.");
                Require(HolidayClock.Text(new DateTime(2028, 12, 31, 0, 0, 0)) == "1 д  00 ч  00 м", "Leap-year countdown is wrong.");
            });
            Check(checks, "Density updates without unbounded particles", delegate
            {
                Preferences p = new Preferences { Density = 0 };
                SnowWorld world = new SnowWorld(1920, 1080, 1040, p, 42);
                int light = world.Flakes.Count;
                p.Density = 2; world.SyncFlakes();
                Require(world.Flakes.Count > light * 3 && world.Flakes.Count <= 1500, "Density did not update.");
                for (int i = 0; i < 2000; i++) world.Step(.05f);
                foreach (Flake f in world.Flakes) Require(!float.IsNaN(f.X) && !float.IsNaN(f.Y), "Invalid particle coordinates.");
                Require(Max(world.Ground.Heights) <= world.Ground.Limit, "Snow height exceeded its limit.");
            });
            Check(checks, "Preferences persist; malformed and partial settings recover", delegate
            {
                string path = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(destination)), "preferences-test.json");
                try
                {
                    Preferences p = new Preferences { Density = 2, Wind = -75, Garland = false, LowPower = true, WinterFriends = false, Countdown = false, CursorBroom = false };
                    Require(p.Save(path), "Settings could not be saved.");
                    p.Wind = -65; Require(p.Save(path), "Atomic settings replacement failed.");
                    Preferences read = Preferences.Load(path);
                    Require(read.Density == 2 && read.Wind == -65 && !read.Garland && read.LowPower && !read.WinterFriends && !read.Countdown && !read.CursorBroom, "Settings changed during serialization.");
                    File.WriteAllText(path, "{broken"); Require(Preferences.Load(path).Excavator, "Malformed settings did not recover.");
                    File.WriteAllText(path, "{}"); Require(Preferences.Load(path).Excavator && Preferences.Load(path).Density == 1, "Partial settings lost defaults.");
                    File.WriteAllText(path, "{\"Density\":0,\"Garland\":false}"); read = Preferences.Load(path);
                    Require(read.Density == 0 && !read.Garland && read.WinterFriends && read.CursorBroom && read.Countdown, "Version 1.0 settings did not migrate.");
                    File.WriteAllText(path, "{\"Density\":999,\"Wind\":-999}");
                    read = Preferences.Load(path); Require(read.Density == 2 && read.Wind == -100, "Settings values were not clamped.");
                }
                finally { if (File.Exists(path)) File.Delete(path); }
            });
            Check(checks, "Transparent rendering preserves alpha and draws the excavator", delegate
            {
                SnowWorld world = new SnowWorld(800, 600, 560, new Preferences(), 42);
                world.Flakes.Clear(); world.DiggerX = 350;
                using (Bitmap bitmap = new Bitmap(800, 600, PixelFormat.Format32bppPArgb))
                using (Graphics g = Graphics.FromImage(bitmap))
                using (WinterRenderer renderer = new WinterRenderer())
                {
                    renderer.Draw(g, world, true);
                    Require(bitmap.GetPixel(200, 200).A == 0, "Background is not transparent.");
                    int colored = 0;
                    for (int y = 480; y < 560; y++) for (int x = 310; x < 425; x++)
                    {
                        Color c = bitmap.GetPixel(x, y);
                        if (c.A > 150 && c.R > 150 && c.R > c.B * 1.4) colored++;
                    }
                    Require(colored > 200, "Excavator was not rendered.");
                    bitmap.Save(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(destination)), "overlay-alpha.png"), ImageFormat.Png);
                }
            });
            bool all = true;
            foreach (CheckResult c in checks) all &= c.Passed;
            using (FileStream f = File.Create(destination)) new DataContractJsonSerializer(typeof(List<CheckResult>)).WriteObject(f, checks);
            return all ? 0 : 1;
        }
        private static void Check(List<CheckResult> checks, string name, Action action)
        {
            try { action(); checks.Add(new CheckResult { Name = name, Passed = true, Detail = "OK" }); }
            catch (Exception e) { checks.Add(new CheckResult { Name = name, Passed = false, Detail = e.Message }); }
        }
        private static void Require(bool result, string message) { if (!result) throw new Exception(message); }
        private static float Max(float[] values) { float result = 0; foreach (float v in values) result = Math.Max(result, v); return result; }
        internal static void SettingsPreview(string destination)
        {
            using (Icon icon = WinterRenderer.CreateIcon())
            using (SettingsWindow window = new SettingsWindow(null, new Preferences(), icon))
            {
                window.StartPosition = System.Windows.Forms.FormStartPosition.Manual;
                window.Location = new Point(-30000, -30000);
                window.ShowInTaskbar = false;
                window.Show();
                window.Update();
                using (Bitmap image = new Bitmap(window.Width, window.Height))
                {
                    window.DrawToBitmap(image, new Rectangle(0, 0, window.Width, window.Height));
                    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(destination)));
                    image.Save(destination, ImageFormat.Png);
                }
                window.Hide();
            }
        }
        internal static void Preview(string destination)
        {
            Preferences prefs = new Preferences { Density = 1 };
            SnowWorld world = new SnowWorld(1440, 900, 849, prefs, 2026);
            world.Ledges.Add(new SnowSurface(new IntPtr(1), 227, 250, 575, 25));
            world.Ledges.Add(new SnowSurface(new IntPtr(2), 880, 385, 375, 25));
            foreach (SnowSurface s in world.Ledges) for (int i = 0; i < s.Heights.Length; i++) s.Heights[i] = 7 + (float)Math.Sin(i * .19) * 2 + (float)Math.Sin(i * .43) * 1.5f;
            for (int i = 0; i < world.Ground.Heights.Length; i++) world.Ground.Heights[i] = 15 + (float)Math.Sin(i * .09) * 5 + (float)Math.Sin(i * .21) * 3;
            world.DiggerX = 945; world.Scoop = .8f; world.Time = 5;
            world.Ground.Clear(0, 1000, 1);
            using (Bitmap image = new Bitmap(1440, 900, PixelFormat.Format32bppPArgb))
            using (Graphics g = Graphics.FromImage(image))
            using (WinterRenderer renderer = new WinterRenderer())
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (LinearGradientBrush bg = new LinearGradientBrush(new Point(0, 0), new Point(1440, 900), Color.FromArgb(12, 34, 49), Color.FromArgb(35, 65, 70))) g.FillRectangle(bg, 0, 0, 1440, 900);
                using (SolidBrush moon = new SolidBrush(Color.FromArgb(214, 226, 217))) g.FillEllipse(moon, 1135, 100, 60, 60);
                using (SolidBrush hill = new SolidBrush(Color.FromArgb(23, 53, 64))) g.FillClosedCurve(hill, new[] {
                    new PointF(-100, 620), new PointF(200, 590), new PointF(490, 680), new PointF(950, 570), new PointF(1500, 650), new PointF(1500, 1000), new PointF(-100, 1000) });
                using (SolidBrush hill = new SolidBrush(Color.FromArgb(28, 62, 70))) g.FillClosedCurve(hill, new[] {
                    new PointF(-100, 780), new PointF(260, 725), new PointF(680, 780), new PointF(1250, 685), new PointF(1500, 720), new PointF(1500, 1000), new PointF(-100, 1000) });
                using (Font title = new Font("Segoe UI", 29, FontStyle.Regular))
                using (SolidBrush text = new SolidBrush(Color.FromArgb(226, 240, 234))) g.DrawString("Время маленьких чудес.", title, text, 220, 125);
                DrawPreviewWindow(g, new Rectangle(225, 250, 580, 365), "Проводник", true);
                DrawPreviewWindow(g, new Rectangle(878, 385, 380, 250), "Зимние планы", false);
                WinterRenderer.RoundFill(g, Color.FromArgb(225, 16, 29, 42), 0, 850, 1440, 50, 1);
                for (int i = 0; i < 6; i++) WinterRenderer.RoundFill(g, Color.FromArgb(104 + i * 10, 157 + i * 5, 167 + i * 4), 595 + i * 43, 862, 25, 25, 6);
                renderer.Draw(g, world, false);
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(destination)));
                image.Save(destination, ImageFormat.Png);
            }
        }
        internal static void DrawPreviewWindow(Graphics g, Rectangle r, string title, bool folders)
        {
            WinterRenderer.RoundFill(g, Color.FromArgb(245, 23, 36, 48), r.X, r.Y, r.Width, r.Height, 12);
            WinterRenderer.RoundFill(g, Color.FromArgb(255, 32, 47, 59), r.X, r.Y, r.Width, 42, 10);
            using (Font f = new Font("Segoe UI", 10))
            using (SolidBrush text = new SolidBrush(Color.FromArgb(195, 214, 224)))
            {
                g.DrawString(title, f, text, r.X + 22, r.Y + 12);
                g.DrawString("—     □     ×", f, text, r.Right - 100, r.Y + 12);
                if (folders)
                {
                    string[] names = { "Фотографии", "Музыка", "Подарки", "Каникулы" };
                    for (int i = 0; i < 4; i++)
                    {
                        int x = r.X + 38 + i * 130, y = r.Y + 100;
                        WinterRenderer.RoundFill(g, Color.FromArgb(211, 178, 108), x, y, 30, 10, 3);
                        WinterRenderer.RoundFill(g, Color.FromArgb(231, 201, 134), x, y + 6, 57, 39, 4);
                        g.DrawString(names[i], f, text, x - 8, y + 59);
                    }
                }
                else
                {
                    string[] lines = { "Подарки для близких", "Мандарины и какао", "Вечер без спешки", "Пусть идёт снег" };
                    for (int i = 0; i < 4; i++)
                    {
                        WinterRenderer.RoundFill(g, Color.FromArgb(113, 170, 155), r.X + 26, r.Y + 77 + i * 37, 13, 13, 4);
                        g.DrawString(lines[i], f, text, r.X + 52, r.Y + 73 + i * 37);
                    }
                }
            }
        }
    }
}
