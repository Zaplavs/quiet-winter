using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace QuietWinter
{
    internal sealed partial class WinterRenderer
    {
        private readonly Font clockTitle = new Font("Segoe UI", 9, FontStyle.Regular);
        private readonly Font clockDigits = new Font("Segoe UI", 17, FontStyle.Bold);
        private void DrawCountdown(Graphics g, SnowWorld world)
        {
            GraphicsState state = g.Save();
            float s = world.Scale;
            g.TranslateTransform(world.Width - 273 * s, 34 * s);
            g.ScaleTransform(s, s);
            RoundFill(g, Color.FromArgb(214, 19, 35, 47), 0, 0, 251, 71, 14);
            using (Pen p = new Pen(Color.FromArgb(90, 156, 195, 182)))
            using (GraphicsPath shape = Rounded(new RectangleF(.5f, .5f, 250, 70), 14)) g.DrawPath(p, shape);
            using (SolidBrush muted = new SolidBrush(Color.FromArgb(177, 203, 209)))
            using (SolidBrush mint = new SolidBrush(Color.FromArgb(211, 237, 219)))
            {
                g.DrawString("ДО НОВОГО ГОДА", clockTitle, muted, 17, 10);
                g.DrawString(HolidayClock.Text(world.LocalTime), clockDigits, mint, 14, 29);
            }
            g.Restore(state);
        }
        private static PointF[] Star(float x, float y, float radius)
        {
            PointF[] points = new PointF[10];
            for (int i = 0; i < 10; i++)
            {
                double a = i * Math.PI / 5 - Math.PI / 2;
                float r = i % 2 == 0 ? radius : radius * .45f;
                points[i] = new PointF(x + (float)Math.Cos(a) * r, y + (float)Math.Sin(a) * r);
            }
            return points;
        }
        private static void DrawFriends(Graphics g, SnowWorld world)
        {
            GraphicsState state = g.Save();
            g.TranslateTransform(92 * world.Scale, world.Ground.Y - 2);
            g.ScaleTransform(world.Scale, world.Scale);
            using (SolidBrush shadow = new SolidBrush(Color.FromArgb(42, 18, 42, 50))) g.FillEllipse(shadow, -52, -4, 184, 10);
            RoundFill(g, Color.FromArgb(115, 83, 54), -6, -24, 13, 24, 3);
            Color[] greens = { Color.FromArgb(36, 95, 81), Color.FromArgb(44, 116, 94), Color.FromArgb(53, 137, 108) };
            for (int i = 0; i < 3; i++)
            {
                float top = -81 - i * 21, bottom = -13 - i * 25, half = 52 - i * 12;
                using (SolidBrush b = new SolidBrush(greens[i]))
                    g.FillPolygon(b, new[] { new PointF(0, top), new PointF(half, bottom), new PointF(half - 12, bottom + 5),
                        new PointF(-half + 12, bottom + 5), new PointF(-half, bottom) });
                using (Pen snow = new Pen(Color.FromArgb(231, 239, 237), 3.3f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                {
                    g.DrawLine(snow, -half + 4, bottom - 1, -half * .35f, bottom + 2);
                    g.DrawLine(snow, half * .16f, bottom + 2, half - 4, bottom);
                }
            }
            float[] bulbX = { -10, 13, -21, 4, 28, -30, -7, 16, 35 };
            float[] bulbY = { -96, -82, -65, -58, -44, -28, -22, -21, -23 };
            Color[] colors = { Color.FromArgb(255, 208, 114), Color.FromArgb(242, 122, 110), Color.FromArgb(184, 221, 232) };
            for (int i = 0; i < bulbX.Length; i++)
            {
                float pulse = .7f + .3f * (float)Math.Sin(world.Time * 1.2 + i);
                Glow(g, bulbX[i], bulbY[i], 2.5f * pulse, colors[i % 3]);
                using (SolidBrush b = new SolidBrush(colors[i % 3])) g.FillEllipse(b, bulbX[i] - 2.5f, bulbY[i] - 2.5f, 5, 5);
            }
            Glow(g, 0, -127, 7, Color.FromArgb(255, 219, 141));
            using (SolidBrush gold = new SolidBrush(Color.FromArgb(255, 218, 137))) g.FillPolygon(gold, Star(0, -127, 10));
            // A waving snowman, painted with the same renderer as the desktop overlay.
            g.TranslateTransform(112, 0);
            using (LinearGradientBrush body = new LinearGradientBrush(new Point(-20, -90), new Point(22, 0),
                Color.FromArgb(254, 255, 250), Color.FromArgb(180, 211, 224)))
            {
                g.FillEllipse(body, -22, -40, 46, 42);
                g.FillEllipse(body, -18, -66, 37, 36);
                g.FillEllipse(body, -15, -91, 31, 30);
            }
            float wave = (float)Math.Sin(world.Time * 2.3) * 5;
            using (Pen twig = new Pen(Color.FromArgb(115, 78, 56), 2.8f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                g.DrawLine(twig, -15, -52, -37, -68);
                g.DrawLine(twig, -37, -68, -39, -79);
                g.DrawLine(twig, -36, -68, -46, -69);
                g.DrawLine(twig, 15, -51, 34, -70 + wave);
                g.DrawLine(twig, 34, -70 + wave, 33, -83 + wave);
                g.DrawLine(twig, 34, -70 + wave, 44, -75 + wave);
            }
            RoundFill(g, Color.FromArgb(196, 72, 67), -18, -65, 36, 8, 4);
            RoundFill(g, Color.FromArgb(222, 95, 83), 8, -61, 8, 22, 3);
            using (SolidBrush carrot = new SolidBrush(Color.FromArgb(237, 149, 62)))
                g.FillPolygon(carrot, new[] { new PointF(4, -78), new PointF(24, -74), new PointF(4, -73) });
            using (SolidBrush coal = new SolidBrush(Color.FromArgb(43, 65, 75)))
            {
                bool blink = world.Time % 6 < .16f;
                g.FillEllipse(coal, -7, -83, 3.2f, blink ? 1 : 3.5f);
                g.FillEllipse(coal, 5, -83, 3.2f, blink ? 1 : 3.5f);
                for (int i = 0; i < 3; i++) g.FillEllipse(coal, 0, -53 + i * 15, 4, 4);
                for (int i = 0; i < 4; i++) g.FillEllipse(coal, -6 + i * 3.6f, -72 + (float)Math.Sin(i * Math.PI / 3) * 2, 1.8f, 1.8f);
            }
            RoundFill(g, Color.FromArgb(38, 60, 67), -15, -107, 28, 20, 4);
            RoundFill(g, Color.FromArgb(42, 69, 75), -22, -92, 43, 6, 3);
            RoundFill(g, Color.FromArgb(204, 90, 75), -15, -95, 28, 5, 1);
            using (Pen snow = new Pen(Color.FromArgb(246, 252, 255), 3) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawLine(snow, -12, -107, 9, -107);
            g.Restore(state);
        }
    }
}
