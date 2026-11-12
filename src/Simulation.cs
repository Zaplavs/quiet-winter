using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace QuietWinter
{
    [DataContract]
    internal sealed class Preferences
    {
        [DataMember] public int Density = 1;
        [DataMember] public int Wind = 12;
        [DataMember] public bool Excavator = true;
        [DataMember] public bool WindowSnow = true;
        [DataMember] public bool Garland = true;
        [DataMember] public bool PauseFullscreen = true;
        [DataMember] public bool LowPower = false;
        [DataMember] public bool WinterFriends = true;
        [DataMember] public bool CursorBroom = true;
        [DataMember] public bool Countdown = true;
        [OnDeserializing]
        private void Defaults(StreamingContext context)
        {
            Density = 1; Wind = 12; Excavator = true; WindowSnow = true; Garland = true; PauseFullscreen = true; LowPower = false;
            WinterFriends = true; CursorBroom = true; Countdown = true;
        }
        internal static string Folder { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QuietWinter"); } }
        internal static string FilePath { get { return Path.Combine(Folder, "settings.json"); } }
        internal void Validate() { Density = Math.Max(0, Math.Min(2, Density)); Wind = Math.Max(-100, Math.Min(100, Wind)); }
        internal static Preferences Load(string path)
        {
            try
            {
                using (FileStream file = File.OpenRead(path))
                {
                    Preferences result = (Preferences)new DataContractJsonSerializer(typeof(Preferences)).ReadObject(file);
                    result.Validate();
                    return result;
                }
            }
            catch (Exception)
            { return new Preferences(); }
        }
        internal bool Save(string path)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                string temp = path + ".tmp";
                using (FileStream file = File.Create(temp)) new DataContractJsonSerializer(typeof(Preferences)).WriteObject(file, this);
                if (File.Exists(path)) File.Replace(temp, path, null);
                else File.Move(temp, path);
                return true;
            }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }
    }
    internal sealed class SnowSurface
    {
        internal const int Step = 6;
        internal IntPtr Id;
        internal float X, Y, Width, Limit;
        internal float[] Heights;
        internal List<PointF> Visible = new List<PointF>();
        internal SnowSurface(IntPtr id, float x, float y, float width, float limit)
        {
            Id = id; X = x; Y = y; Width = width; Limit = limit;
            Heights = new float[Math.Max(3, (int)Math.Ceiling(width / Step) + 1)];
            Visible.Add(new PointF(x, x + width));
        }
        internal void Move(float x, float y, float width)
        {
            if (Math.Abs(width - Width) > Step)
            {
                float[] old = Heights;
                Heights = new float[Math.Max(3, (int)Math.Ceiling(width / Step) + 1)];
                for (int i = 0; i < Heights.Length; i++) Heights[i] = old[Math.Min(old.Length - 1, (int)((float)i / (Heights.Length - 1) * (old.Length - 1)))];
            }
            X = x; Y = y; Width = width;
        }
        internal float HeightAt(float x)
        {
            float index = Math.Max(0, Math.Min(Heights.Length - 1.001f, (x - X) / Step));
            int i = (int)index;
            return Heights[i] + (Heights[Math.Min(i + 1, Heights.Length - 1)] - Heights[i]) * (index - i);
        }
        internal bool Contains(float x)
        {
            if (x < X || x > X + Width) return false;
            foreach (PointF span in Visible) if (x >= span.X && x <= span.Y) return true;
            return false;
        }
        internal void Deposit(float x, float amount)
        {
            int center = (int)((x - X) / Step);
            for (int j = -3; j <= 3; j++)
            {
                int i = center + j;
                if (i < 0 || i >= Heights.Length) continue;
                Heights[i] = Math.Min(Limit, Heights[i] + amount * (float)Math.Exp(-j * j / 3.5));
            }
        }
        internal float Clear(float from, float to, float keep)
        {
            float removed = 0;
            int start = Math.Max(0, (int)Math.Floor((Math.Min(from, to) - X) / Step));
            int end = Math.Min(Heights.Length - 1, (int)Math.Ceiling((Math.Max(from, to) - X) / Step));
            for (int i = start; i <= end; i++)
            {
                if (Heights[i] > keep) { removed += Heights[i] - keep; Heights[i] = keep; }
            }
            return removed;
        }
        internal void Exclude(float left, float right)
        {
            List<PointF> result = new List<PointF>();
            foreach (PointF span in Visible)
            {
                if (right <= span.X || left >= span.Y) result.Add(span);
                else
                {
                    if (left > span.X) result.Add(new PointF(span.X, left));
                    if (right < span.Y) result.Add(new PointF(right, span.Y));
                }
            }
            Visible = result;
        }
    }
    internal struct Flake
    {
        internal float X, Y, Speed, Radius, Phase, Depth;
        internal bool Crystal;
    }
    internal struct Spark
    {
        internal float X, Y, Vx, Vy, Life, MaxLife, Size;
    }
    internal sealed class SnowWorld
    {
        internal readonly int Width, Height;
        internal readonly float Scale;
        internal readonly Preferences Settings;
        internal readonly SnowSurface Ground;
        internal readonly List<SnowSurface> Ledges = new List<SnowSurface>();
        internal readonly List<Flake> Flakes = new List<Flake>();
        internal readonly List<Spark> Spray = new List<Spark>();
        internal float Time, DiggerX, DiggerDirection = 1, DiggerWait, Scoop, Cleared, Swept, BurstRemaining;
        internal DateTime LocalTime = DateTime.Now;
        internal int Passes;
        internal bool ForceDigger;
        private readonly Random random;
        private float wind;
        internal SnowWorld(int width, int height, int groundY, Preferences settings, int seed)
        {
            Width = width; Height = height; Settings = settings; random = new Random(seed);
            Scale = Math.Max(0.72f, Math.Min(1.7f, height / 1080f));
            Ground = new SnowSurface(IntPtr.Zero, 0, groundY, width, 48 * Scale);
            for (int i = 0; i < Ground.Heights.Length; i++)
                Ground.Heights[i] = (4.5f + (float)Math.Sin(i * .115) * 1.7f + (float)Math.Sin(i * .042) * 1.8f) * Scale;
            DiggerX = -125 * Scale;
            SyncFlakes();
        }
        internal int TargetCount
        {
            get
            {
                double area = Width * (double)Height / (1920 * 1080);
                return Math.Max(65, Math.Min(1500, (int)((Settings.Density == 0 ? 140 : Settings.Density == 1 ? 310 : 620) * area)));
            }
        }
        internal void SyncFlakes()
        {
            while (Flakes.Count > TargetCount) Flakes.RemoveAt(Flakes.Count - 1);
            while (Flakes.Count < TargetCount) Flakes.Add(NewFlake(true));
        }
        private float R(float min, float max) { return min + (float)random.NextDouble() * (max - min); }
        private Flake NewFlake(bool anywhere)
        {
            float depth = R(.25f, 1);
            return new Flake { X = R(-30, Width + 30), Y = anywhere ? R(0, Ground.Y) : R(-80, -8),
                Depth = depth, Radius = (.65f + depth * 2) * Scale,
                Speed = (20 + depth * 52) * Scale, Phase = R(0, 6.28f), Crystal = random.Next(12) == 0 && depth > .65f };
        }
        internal void UpdateWindows(List<WindowInfo> windows, Rectangle monitor)
        {
            if (!Settings.WindowSnow) { Ledges.Clear(); return; }
            Dictionary<IntPtr, SnowSurface> previous = new Dictionary<IntPtr, SnowSurface>();
            foreach (SnowSurface surface in Ledges) previous[surface.Id] = surface;
            Ledges.Clear();
            List<Rectangle> occluders = new List<Rectangle>();
            foreach (WindowInfo window in windows)
            {
                Rectangle b = window.Bounds;
                if (!b.IntersectsWith(monitor)) continue;
                float x = b.Left - monitor.Left + 3, y = b.Top - monitor.Top + 1, w = b.Width - 6;
                if (y > 12 && y < Ground.Y - 25 && w > 100)
                {
                    SnowSurface surface;
                    if (!previous.TryGetValue(window.Handle, out surface))
                    {
                        surface = new SnowSurface(window.Handle, x, y, w, 23 * Scale);
                        for (int i = 0; i < surface.Heights.Length; i++) surface.Heights[i] = (2 + (float)Math.Sin(i * .16) * .8f) * Scale;
                    }
                    surface.Move(x, y, w);
                    surface.Visible.Clear();
                    surface.Visible.Add(new PointF(Math.Max(0, x), Math.Min(Width, x + w)));
                    foreach (Rectangle front in occluders)
                        if (front.Top < b.Top + 3 && front.Bottom > b.Top - 28 * Scale)
                            surface.Exclude(front.Left - monitor.Left - 2, front.Right - monitor.Left + 2);
                    if (surface.Visible.Count > 0) Ledges.Add(surface);
                }
                occluders.Add(b);
            }
            Ledges.Sort(delegate(SnowSurface a, SnowSurface b) { return a.Y.CompareTo(b.Y); });
        }
        internal void Step(float dt)
        {
            dt = Math.Max(0, Math.Min(.06f, dt));
            Time += dt;
            BurstRemaining = Math.Max(0, BurstRemaining - dt);
            wind += (Settings.Wind * .85f * Scale - wind) * Math.Min(1, dt * 2);
            float weather = Settings.Density == 2 ? 1.45f : 1;
            for (int i = 0; i < Flakes.Count; i++)
            {
                Flake f = Flakes[i];
                float oldY = f.Y;
                f.X += (wind * f.Depth + (float)Math.Sin(Time * .7f + f.Phase) * (9 + 14 * f.Depth) * Scale) * dt;
                f.Y += f.Speed * weather * dt;
                if (BurstRemaining > 0)
                {
                    f.X += (float)Math.Sin(Time * 2.4f + f.Phase) * 150 * Scale * f.Depth * dt;
                    f.Y += f.Speed * 1.2f * dt;
                }
                if (f.X < -40) f.X = Width + 35;
                if (f.X > Width + 40) f.X = -35;
                bool settled = false;
                foreach (SnowSurface ledge in Ledges)
                {
                    float top = ledge.Y - ledge.HeightAt(f.X);
                    if (oldY <= top + f.Radius && f.Y >= top - f.Radius && ledge.Contains(f.X))
                    {
                        ledge.Deposit(f.X, .30f * f.Radius); settled = true; break;
                    }
                }
                if (!settled && f.Y >= Ground.Y - Ground.HeightAt(f.X))
                {
                    Ground.Deposit(f.X, .42f * f.Radius); settled = true;
                }
                Flakes[i] = settled || f.Y > Height + 10 ? NewFlake(false) : f;
            }
            StepDigger(dt);
            for (int i = Spray.Count - 1; i >= 0; i--)
            {
                Spark p = Spray[i];
                p.Life -= dt;
                p.X += p.Vx * dt; p.Y += p.Vy * dt; p.Vy += 220 * Scale * dt;
                if (p.Life <= 0 || p.Y > Ground.Y + 8) Spray.RemoveAt(i);
                else Spray[i] = p;
            }
        }
        private void StepDigger(float dt)
        {
            if (!Settings.Excavator && !ForceDigger) { Scoop *= .95f; return; }
            if (DiggerWait > 0 && !ForceDigger) { DiggerWait -= dt; return; }
            DiggerWait = 0;
            float old = DiggerX;
            DiggerX += DiggerDirection * (Settings.Density == 2 ? 82 : 63) * Scale * dt;
            float bucket = DiggerX + DiggerDirection * 82 * Scale;
            float removed = Ground.Clear(Math.Min(old, DiggerX) + DiggerDirection * 82 * Scale - 15 * Scale,
                Math.Max(old, DiggerX) + DiggerDirection * 82 * Scale + 15 * Scale, .8f * Scale);
            Cleared += removed;
            Scoop = Math.Min(1, Math.Max(0, Scoop + removed * .012f - dt * .13f));
            if (removed > .1f && Spray.Count < 190)
            {
                for (int i = 0; i < Math.Min(5, (int)(removed / 2) + 1); i++)
                {
                    float life = R(.35f, .9f);
                    Spray.Add(new Spark { X = bucket + R(-5, 7) * Scale, Y = Ground.Y - R(8, 18) * Scale,
                        Vx = DiggerDirection * R(25, 110) * Scale, Vy = R(-125, -45) * Scale,
                        Life = life, MaxLife = life, Size = R(1, 3.5f) * Scale });
                }
            }
            float border = 150 * Scale;
            if (DiggerX > Width + border || DiggerX < -border)
            {
                DiggerDirection *= -1;
                DiggerX = DiggerDirection > 0 ? -border : Width + border;
                Passes++;
                DiggerWait = ForceDigger ? 1 : 9;
                ForceDigger = false;
                Scoop = 0;
            }
        }
        internal void CallDigger()
        {
            ForceDigger = true; DiggerWait = 0;
        }
        internal void SnowBurst()
        {
            BurstRemaining = 4;
        }
        internal void Sweep(float x, float y)
        {
            if (!Settings.CursorBroom || x < 0 || x > Width || y < 0 || y > Height) return;
            SweepSurface(Ground, x, y);
            foreach (SnowSurface ledge in Ledges) SweepSurface(ledge, x, y);
        }
        private void SweepSurface(SnowSurface surface, float x, float y)
        {
            if (!surface.Contains(x) || y < surface.Y - surface.Limit - 8 * Scale || y > surface.Y + 10 * Scale) return;
            float top = surface.Y - surface.HeightAt(x);
            float removed = surface.Clear(x - 26 * Scale, x + 26 * Scale, .3f * Scale);
            Swept += removed;
            if (removed < .5f) return;
            for (int i = 0; i < 9 && Spray.Count < 190; i++)
            {
                float life = R(.35f, .7f);
                Spray.Add(new Spark { X = x + R(-22, 22) * Scale, Y = top - R(0, 5),
                    Vx = R(-95, 95) * Scale, Vy = R(-110, -35) * Scale,
                    Life = life, MaxLife = life, Size = R(1, 3) * Scale });
            }
        }
        internal void ResetSnow()
        {
            Array.Clear(Ground.Heights, 0, Ground.Heights.Length);
            foreach (SnowSurface surface in Ledges) Array.Clear(surface.Heights, 0, surface.Heights.Length);
            Spray.Clear(); Scoop = 0;
        }
    }
    internal static class HolidayClock
    {
        internal static string Text(DateTime local)
        {
            if (local.Month == 1 && local.Day == 1) return "С Новым годом!";
            TimeSpan remaining = new DateTime(local.Year + 1, 1, 1) - local;
            return remaining.Days + " д  " + remaining.Hours.ToString("00") + " ч  " + remaining.Minutes.ToString("00") + " м";
        }
    }
}
