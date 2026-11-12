using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace QuietWinter
{
    internal sealed partial class WinterRenderer : IDisposable
    {
        private readonly SolidBrush[] snow = {
            new SolidBrush(Color.FromArgb(86, 218, 237, 249)), new SolidBrush(Color.FromArgb(142, 230, 242, 252)),
            new SolidBrush(Color.FromArgb(207, 243, 250, 255)), new SolidBrush(Color.FromArgb(245, 252, 254, 255))
        };
        private readonly SolidBrush white = new SolidBrush(Color.FromArgb(248, 246, 251, 255));
        private readonly Pen frost = new Pen(Color.FromArgb(215, 255, 255, 255), 1.1f);
        internal void Draw(Graphics g, SnowWorld world, bool transparent)
        {
            if (transparent) g.Clear(Color.Transparent);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            DrawFlakes(g, world, false);
            foreach (SnowSurface surface in world.Ledges) DrawSurface(g, surface, false);
            DrawSurface(g, world.Ground, true);
            if (world.Settings.Garland) DrawGarland(g, world.Width, world.Scale, world.Time);
            if (world.Settings.WinterFriends) DrawFriends(g, world);
            if (world.Settings.Countdown) DrawCountdown(g, world);
            if ((world.Settings.Excavator || world.ForceDigger) && world.DiggerWait <= 0)
                DrawDigger(g, world.DiggerX, world.Ground.Y - 1, world.Scale, world.DiggerDirection, world.Time, world.Scoop);
            foreach (Spark p in world.Spray)
            {
                int alpha = (int)(235 * Math.Max(0, p.Life / p.MaxLife));
                using (SolidBrush b = new SolidBrush(Color.FromArgb(alpha, 250, 253, 255)))
                    g.FillEllipse(b, p.X, p.Y, p.Size * 1.5f, p.Size);
            }
            DrawFlakes(g, world, true);
        }
        private void DrawFlakes(Graphics g, SnowWorld world, bool near)
        {
            foreach (Flake f in world.Flakes)
            {
                if ((f.Depth > .7f) != near) continue;
                Brush b = snow[Math.Min(3, (int)(f.Depth * 4))];
                if (f.Crystal)
                {
                    float r = f.Radius * 1.8f;
                    using (Pen p = new Pen(Color.FromArgb(185, 247, 252, 255), Math.Max(.8f, world.Scale)))
                    {
                        for (int j = 0; j < 3; j++)
                        {
                            double a = j * Math.PI / 3 + world.Time * .13f + f.Phase;
                            float dx = (float)Math.Cos(a) * r, dy = (float)Math.Sin(a) * r;
                            g.DrawLine(p, f.X - dx, f.Y - dy, f.X + dx, f.Y + dy);
                        }
                    }
                }
                else g.FillEllipse(b, f.X - f.Radius, f.Y - f.Radius, f.Radius * 2, f.Radius * 2);
            }
        }
        internal void DrawSurface(Graphics g, SnowSurface surface, bool ground)
        {
            int count = surface.Heights.Length;
            PointF[] ridge = new PointF[count];
            for (int i = 0; i < count; i++)
            {
                float taper = ground ? 1 : Math.Min(1, Math.Min(i, count - 1 - i) / 1.8f);
                ridge[i] = new PointF(surface.X + Math.Min(surface.Width, i * SnowSurface.Step), surface.Y - surface.Heights[i] * taper);
            }
            using (GraphicsPath shape = new GraphicsPath())
            {
                shape.AddCurve(ridge, .32f);
                shape.AddLine(ridge[count - 1], new PointF(surface.X + surface.Width, surface.Y + (ground ? 3 : 1)));
                shape.AddLine(surface.X + surface.Width, surface.Y + (ground ? 3 : 1), surface.X, surface.Y + 1);
                shape.CloseFigure();
                foreach (PointF span in surface.Visible)
                {
                    if (span.Y <= span.X) continue;
                    GraphicsState state = g.Save();
                    g.SetClip(new RectangleF(span.X, surface.Y - surface.Limit - 6, span.Y - span.X, surface.Limit + 11), CombineMode.Intersect);
                    using (LinearGradientBrush b = new LinearGradientBrush(new PointF(0, surface.Y - surface.Limit - 2),
                        new PointF(0, surface.Y + 4), Color.FromArgb(250, 255, 255, 255), Color.FromArgb(247, 203, 223, 238)))
                        g.FillPath(b, shape);
                    g.DrawCurve(frost, ridge, .32f);
                    g.Restore(state);
                }
            }
        }
        internal static GraphicsPath Rounded(RectangleF r, float radius)
        {
            GraphicsPath p = new GraphicsPath();
            float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
        internal static void RoundFill(Graphics g, Color color, float x, float y, float w, float h, float radius)
        {
            using (GraphicsPath p = Rounded(new RectangleF(x, y, w, h), radius))
            using (SolidBrush b = new SolidBrush(color)) g.FillPath(b, p);
        }
        private static void Glow(Graphics g, float x, float y, float r, Color color)
        {
            for (int i = 4; i > 0; i--)
            {
                float size = r * (1 + i * .65f);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(7 + (4 - i) * 3, color)))
                    g.FillEllipse(b, x - size, y - size, size * 2, size * 2);
            }
        }
        private static void DrawGarland(Graphics g, int width, float scale, float time)
        {
            float spacing = 58 * scale;
            Color[] colors = { Color.FromArgb(255, 203, 117), Color.FromArgb(244, 129, 116), Color.FromArgb(167, 221, 191), Color.FromArgb(245, 221, 157) };
            using (Pen cable = new Pen(Color.FromArgb(160, 64, 88, 80), 1.25f * scale))
            {
                for (float x = -spacing; x < width; x += spacing)
                {
                    g.DrawBezier(cable, x, 1, x + spacing * .3f, 12 * scale, x + spacing * .7f, 12 * scale, x + spacing, 1);
                    float bx = x + spacing / 2, by = 10 * scale;
                    Color c = colors[((int)(x / spacing) + 10000) % colors.Length];
                    float pulse = .85f + (float)Math.Sin(time * .7f + x * .007f) * .15f;
                    Glow(g, bx, by + 5 * scale, 4 * scale * pulse, c);
                    g.DrawLine(cable, bx, by - 2 * scale, bx, by + 3 * scale);
                    RoundFill(g, Color.FromArgb(62, 77, 64), bx - 2 * scale, by, 4 * scale, 3 * scale, scale);
                    using (SolidBrush bulb = new SolidBrush(c)) g.FillEllipse(bulb, bx - 2.8f * scale, by + 2 * scale, 5.6f * scale, 8 * scale);
                    using (SolidBrush shine = new SolidBrush(Color.FromArgb(210, 255, 255, 240))) g.FillEllipse(shine, bx - scale, by + 3 * scale, 1.5f * scale, 3 * scale);
                }
            }
        }
        internal static void DrawDigger(Graphics g, float x, float y, float scale, float direction, float time, float scoop)
        {
            GraphicsState state = g.Save();
            g.TranslateTransform(x, y);
            g.ScaleTransform(scale * direction, scale);
            Color yellow = Color.FromArgb(240, 179, 55), light = Color.FromArgb(255, 207, 94), dark = Color.FromArgb(156, 103, 30);
            float bounce = (float)Math.Sin(time * 17) * .55f;
            using (SolidBrush shadow = new SolidBrush(Color.FromArgb(43, 28, 50, 68))) g.FillEllipse(shadow, -46, -4, 99, 7);
            RoundFill(g, Color.FromArgb(32, 43, 54), -43, -21, 88, 20, 10);
            using (Pen trackEdge = new Pen(Color.FromArgb(90, 110, 117), 2))
            using (GraphicsPath p = Rounded(new RectangleF(-43, -21, 88, 20), 10)) g.DrawPath(trackEdge, p);
            for (int i = 0; i < 5; i++)
            {
                float wx = -30 + i * 15;
                using (SolidBrush wheel = new SolidBrush(Color.FromArgb(114, 131, 135))) g.FillEllipse(wheel, wx - 5, -17, 11, 11);
                using (SolidBrush hub = new SolidBrush(Color.FromArgb(47, 62, 69))) g.FillEllipse(hub, wx - 2, -14, 5, 5);
            }
            using (Pen tread = new Pen(Color.FromArgb(155, 174, 176), 2))
            {
                for (int i = 0; i < 11; i++)
                {
                    float tx = -36 + (i * 7 + time * 32) % 78;
                    g.DrawLine(tread, tx, -3, tx + 2, -6);
                    g.DrawLine(tread, tx, -19, tx + 2, -17);
                }
            }
            g.TranslateTransform(0, bounce);
            RoundFill(g, dark, -34, -36, 66, 17, 4);
            RoundFill(g, yellow, -39, -47, 49, 25, 6);
            RoundFill(g, light, -39, -47, 49, 5, 3);
            RoundFill(g, Color.FromArgb(45, 61, 65), -35, -39, 12, 13, 2);
            using (Pen grille = new Pen(Color.FromArgb(100, 117, 112), 1))
                for (int i = 0; i < 4; i++) g.DrawLine(grille, -33, -36 + i * 2.4f, -25, -36 + i * 2.4f);
            RoundFill(g, Color.FromArgb(60, 74, 77), -26, -60, 5, 14, 2);
            RoundFill(g, Color.FromArgb(90, 100, 98), -28, -63, 8, 4, 1);
            using (SolidBrush cab = new SolidBrush(yellow))
                g.FillPolygon(cab, new[] { new PointF(-17, -73), new PointF(14, -73), new PointF(22, -40), new PointF(-17, -40) });
            using (LinearGradientBrush glass = new LinearGradientBrush(new Point(0, -70), new Point(10, -43), Color.FromArgb(153, 210, 217), Color.FromArgb(42, 83, 105)))
                g.FillPolygon(glass, new[] { new PointF(-12, -68), new PointF(10, -68), new PointF(16, -45), new PointF(-12, -45) });
            using (Pen reflection = new Pen(Color.FromArgb(145, 224, 248, 250), 2))
            { g.DrawLine(reflection, -8, -64, -2, -64); g.DrawLine(reflection, 7, -65, 11, -48); }
            using (Pen border = new Pen(Color.FromArgb(132, 92, 33), 2)) g.DrawLine(border, 2, -69, 2, -43);
            RoundFill(g, light, -20, -76, 37, 5, 2);
            // A tiny Santa hat rides on the cabin roof.
            using (SolidBrush red = new SolidBrush(Color.FromArgb(214, 70, 66)))
                g.FillPolygon(red, new[] { new PointF(-12, -77), new PointF(-5, -95), new PointF(11, -87), new PointF(5, -78) });
            RoundFill(g, Color.FromArgb(249, 249, 240), -14, -80, 23, 5, 2);
            using (SolidBrush pom = new SolidBrush(Color.FromArgb(255, 253, 244))) g.FillEllipse(pom, 8, -91, 7, 7);
            float elbowY = -87 + (float)Math.Sin(time * 2.2) * 2;
            PointF pivot = new PointF(17, -43), elbow = new PointF(43, elbowY), wrist = new PointF(77, -25);
            using (Pen outline = new Pen(dark, 11) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            using (Pen arm = new Pen(yellow, 8) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                g.DrawLine(outline, pivot, elbow); g.DrawLine(outline, elbow, wrist);
                g.DrawLine(arm, pivot, elbow); g.DrawLine(arm, elbow, wrist);
            }
            using (Pen cylinder = new Pen(Color.FromArgb(56, 67, 71), 4))
            using (Pen piston = new Pen(Color.FromArgb(206, 220, 218), 2))
            {
                g.DrawLine(cylinder, 23, -44, 34, -65);
                g.DrawLine(piston, 34, -65, 42, elbowY + 1);
                g.DrawLine(cylinder, 49, elbowY + 8, 61, -52);
                g.DrawLine(piston, 61, -52, 75, -29);
            }
            using (SolidBrush pin = new SolidBrush(Color.FromArgb(248, 223, 153)))
            {
                g.FillEllipse(pin, pivot.X - 3, pivot.Y - 3, 6, 6);
                g.FillEllipse(pin, elbow.X - 3, elbow.Y - 3, 6, 6);
                g.FillEllipse(pin, wrist.X - 3, wrist.Y - 3, 6, 6);
            }
            using (SolidBrush bucket = new SolidBrush(Color.FromArgb(202, 140, 45)))
                g.FillPolygon(bucket, new[] { new PointF(73, -29), new PointF(83, -28), new PointF(92, -5), new PointF(69, -5), new PointF(66, -12) });
            using (Pen lip = new Pen(Color.FromArgb(99, 106, 107), 3)) g.DrawLine(lip, 69, -5, 96, -5);
            using (Pen glint = new Pen(light, 2)) g.DrawLine(glint, 82, -26, 90, -9);
            if (scoop > .03f)
            {
                using (SolidBrush snow = new SolidBrush(Color.FromArgb(244, 250, 255)))
                {
                    g.FillEllipse(snow, 70, -13 - scoop * 9, 22 + scoop * 9, 10 + scoop * 7);
                    g.FillEllipse(snow, 82, -17 - scoop * 10, 12 + scoop * 8, 12 + scoop * 8);
                }
            }
            Glow(g, 22, -41, 5, Color.FromArgb(255, 230, 163));
            using (SolidBrush lamp = new SolidBrush(Color.FromArgb(255, 246, 207))) g.FillEllipse(lamp, 19, -44, 6, 5);
            float beacon = .45f + .55f * (float)Math.Pow(Math.Sin(time * 4), 2);
            Glow(g, -1, -83, 3 * beacon, Color.FromArgb(255, 171, 58));
            g.Restore(state);
        }
        internal static Icon CreateIcon()
        {
            using (Bitmap bitmap = new Bitmap(64, 64, PixelFormat.Format32bppArgb))
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                RoundFill(g, Color.FromArgb(27, 51, 64), 1, 1, 62, 62, 16);
                using (Pen p = new Pen(Color.FromArgb(192, 231, 223), 3.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                {
                    for (int i = 0; i < 6; i++)
                    {
                        double a = i * Math.PI / 3 - Math.PI / 2;
                        PointF end = new PointF(32 + (float)Math.Cos(a) * 20, 32 + (float)Math.Sin(a) * 20);
                        g.DrawLine(p, new PointF(32, 32), end);
                        PointF joint = new PointF(32 + (float)Math.Cos(a) * 12, 32 + (float)Math.Sin(a) * 12);
                        for (int side = -1; side <= 1; side += 2)
                        {
                            double branch = a + side * Math.PI / 3;
                            g.DrawLine(p, joint, new PointF(joint.X + (float)Math.Cos(branch) * 7, joint.Y + (float)Math.Sin(branch) * 7));
                        }
                    }
                }
                IntPtr handle = bitmap.GetHicon();
                try { using (Icon temp = Icon.FromHandle(handle)) return (Icon)temp.Clone(); }
                finally { Native.DestroyIcon(handle); }
            }
        }
        public void Dispose()
        {
            foreach (SolidBrush b in snow) b.Dispose();
            white.Dispose(); frost.Dispose();
            clockTitle.Dispose(); clockDigits.Dispose();
        }
    }
}
