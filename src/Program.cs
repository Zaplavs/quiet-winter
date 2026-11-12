using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("Тихая зима")]
[assembly: AssemblyDescription("Снег, сугробы и маленький экскаватор на рабочем столе Windows")]
[assembly: AssemblyProduct("Quiet Winter")]
[assembly: AssemblyVersion("1.1.0.0")]

namespace QuietWinter
{
    internal static class Program
    {
        internal const string MutexName = "Local\\QuietWinter.v1";
        internal static string Arg(string[] args, string name)
        {
            int index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }
        [STAThread]
        private static int Main(string[] args)
        {
            Native.EnableDpi();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            try
            {
                string selfTest = Arg(args, "--self-test");
                if (selfTest != null) return Verification.Run(selfTest);
                string preview = Arg(args, "--render-preview");
                if (preview != null) { Verification.Preview(preview); return 0; }
                string settingsPreview = Arg(args, "--render-settings");
                if (settingsPreview != null) { Verification.SettingsPreview(settingsPreview); return 0; }
                string demo = Arg(args, "--export-demo");
                if (demo != null) { DemoScene.Export(demo, Arg(args, "--ffmpeg")); return 0; }
                string exportIcon = Arg(args, "--export-icon");
                if (exportIcon != null)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(exportIcon)));
                    using (Icon icon = WinterRenderer.CreateIcon())
                    using (FileStream output = File.Create(exportIcon)) icon.Save(output);
                    return 0;
                }
                if (Array.IndexOf(args, "--stop") >= 0) return Signal("Stop");
                if (Array.IndexOf(args, "--pause") >= 0) return Signal("Pause");
                bool first;
                using (Mutex mutex = new Mutex(true, MutexName, out first))
                {
                    if (!first) return Signal("Settings");
                    try
                    {
                        Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs e) { Error(e.Exception); Application.Exit(); };
                        using (WinterContext context = new WinterContext(args)) Application.Run(context);
                    }
                    finally { mutex.ReleaseMutex(); }
                }
                return 0;
            }
            catch (Exception ex) { Error(ex); return 1; }
        }
        private static int Signal(string action)
        {
            try { using (EventWaitHandle e = EventWaitHandle.OpenExisting("Local\\QuietWinter." + action)) e.Set(); return 0; }
            catch (WaitHandleCannotBeOpenedException) { return action == "Stop" ? 0 : 1; }
        }
        internal static void Error(Exception ex)
        {
            try { Directory.CreateDirectory(Preferences.Folder); File.AppendAllText(Path.Combine(Preferences.Folder, "error.log"), DateTime.Now.ToString("s") + " " + ex + Environment.NewLine); }
            catch { }
            MessageBox.Show("Не удалось запустить снег.\n\n" + ex.Message + "\n\nЖурнал: " + Path.Combine(Preferences.Folder, "error.log"),
                "Тихая зима", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
    internal sealed class WinterContext : ApplicationContext
    {
        private readonly Preferences settings;
        private readonly List<SnowOverlay> overlays = new List<SnowOverlay>();
        private readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
        private readonly Stopwatch watch = Stopwatch.StartNew();
        private readonly EventWaitHandle stopEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\QuietWinter.Stop");
        private readonly EventWaitHandle settingsEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\QuietWinter.Settings");
        private readonly EventWaitHandle pauseEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\QuietWinter.Pause");
        private readonly HotkeyWindow hotkeys = new HotkeyWindow();
        private readonly Icon icon = WinterRenderer.CreateIcon();
        private readonly NotifyIcon tray;
        private readonly ToolStripMenuItem pauseItem, diggerItem;
        private readonly ContextMenuStrip menu;
        private SettingsWindow settingsWindow;
        private bool sessionLocked, quitting;
        private volatile bool rebuild, sessionChanged;
        private double lastFrame, lastCheck, lastDiagnostics;
        private int frameCount;
        private readonly string diagnosticsPath;
        private readonly string settingsPath;
        private readonly double runFor;
        private bool preciseTimer;
        internal bool Paused { get; private set; }
        internal bool SaveFailed { get; private set; }
        internal WinterContext(string[] args)
        {
            settingsPath = Program.Arg(args, "--config") ?? Preferences.FilePath;
            bool firstRun = !File.Exists(settingsPath);
            settings = Preferences.Load(settingsPath);
            diagnosticsPath = Program.Arg(args, "--diagnostics");
            double.TryParse(Program.Arg(args, "--run-for"), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out runFor);
            menu = new ContextMenuStrip { Font = new Font("Segoe UI", 10), ShowImageMargin = false };
            ToolStripMenuItem heading = new ToolStripMenuItem("❄  Тихая зима") { Enabled = false };
            menu.Items.Add(heading);
            menu.Items.Add("Настройки…", null, delegate { OpenSettings(); });
            pauseItem = new ToolStripMenuItem("Пауза    Ctrl+Alt+S", null, delegate { TogglePause(); });
            menu.Items.Add(pauseItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Позвать экскаватор", null, delegate { CallDigger(); });
            menu.Items.Add("Снежный вихрь    Ctrl+Alt+B", null, delegate { SnowBurst(); });
            diggerItem = new ToolStripMenuItem("Экскаватор включён") { Checked = settings.Excavator };
            diggerItem.Click += delegate { settings.Excavator = !settings.Excavator; PreferencesChanged(); };
            menu.Items.Add(diggerItem);
            menu.Items.Add("Убрать все сугробы", null, delegate { foreach (SnowOverlay o in overlays) o.World.ResetSnow(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Выключить    Ctrl+Alt+Q", null, delegate { Quit(); });
            tray = new NotifyIcon { Icon = icon, Text = "Тихая зима · снег идёт", ContextMenuStrip = menu, Visible = true };
            tray.DoubleClick += delegate { OpenSettings(); };
            hotkeys.Pause = TogglePause; hotkeys.Quit = Quit; hotkeys.Burst = SnowBurst;
            CreateOverlays();
            SystemEvents.DisplaySettingsChanged += OnDisplayChanged;
            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
            SystemEvents.SessionSwitch += OnSessionSwitch;
            timer.Interval = settings.LowPower ? 50 : 33;
            SetTimerPrecision();
            timer.Tick += Tick;
            timer.Start();
            if (firstRun || Array.IndexOf(args, "--settings") >= 0) OpenSettings();
            if (firstRun) { SaveFailed = !settings.Save(settingsPath); UpdateSettings(); }
        }
        private void OnDisplayChanged(object sender, EventArgs e) { rebuild = true; }
        private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e) { if (e.Category == UserPreferenceCategory.Desktop) rebuild = true; }
        private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
        {
            if (e.Reason == SessionSwitchReason.SessionLock || e.Reason == SessionSwitchReason.RemoteDisconnect || e.Reason == SessionSwitchReason.ConsoleDisconnect)
            { sessionLocked = true; sessionChanged = true; }
            if (e.Reason == SessionSwitchReason.SessionUnlock || e.Reason == SessionSwitchReason.RemoteConnect || e.Reason == SessionSwitchReason.ConsoleConnect)
            { sessionLocked = false; sessionChanged = true; }
        }
        private void CreateOverlays()
        {
            foreach (SnowOverlay o in overlays) { o.Close(); o.Dispose(); }
            overlays.Clear();
            int seed = Environment.TickCount;
            foreach (Screen screen in Screen.AllScreens)
            {
                SnowOverlay overlay = new SnowOverlay(screen, settings, seed++);
                overlays.Add(overlay); overlay.Show(); overlay.RenderFrame(0);
            }
            RefreshWindows();
        }
        private void Tick(object sender, EventArgs e)
        {
            if (quitting) return;
            if (stopEvent.WaitOne(0)) { Quit(); return; }
            if (settingsEvent.WaitOne(0)) OpenSettings();
            if (pauseEvent.WaitOne(0)) TogglePause();
            double now = watch.Elapsed.TotalSeconds;
            if (runFor > 0 && now >= runFor) { Quit(); return; }
            if (rebuild) { rebuild = false; CreateOverlays(); }
            if (now - lastCheck > .3 || sessionChanged)
            {
                sessionChanged = false; lastCheck = now;
                RefreshWindows();
            }
            float dt = (float)Math.Max(0, Math.Min(.06, now - lastFrame));
            lastFrame = now;
            Native.POINT pointer = new Native.POINT(-100000, -100000);
            bool sweeping = settings.CursorBroom && (Native.GetAsyncKeyState(0x10) & 0x8000) != 0 && Native.GetCursorPos(out pointer);
            if (!sweeping) pointer = new Native.POINT(-100000, -100000);
            foreach (SnowOverlay overlay in overlays)
            {
                if (overlay.Sleeping) continue;
                if (sweeping) overlay.World.Sweep(pointer.x - overlay.Left, pointer.y - overlay.Top);
                overlay.RenderFrame(dt);
            }
            frameCount++;
            if (diagnosticsPath != null && now - lastDiagnostics > 1)
            { lastDiagnostics = now; WriteDiagnostics(); }
        }
        private void RefreshWindows()
        {
            List<WindowInfo> windows = settings.WindowSnow ? Native.ReadWindows() : new List<WindowInfo>();
            foreach (SnowOverlay o in overlays)
            {
                o.Sleep(Paused || sessionLocked || (settings.PauseFullscreen && Native.FullscreenOn(o.Monitor.Bounds)));
                o.World.UpdateWindows(windows, o.Monitor.Bounds);
                o.World.LocalTime = DateTime.Now;
            }
        }
        internal void OpenSettings()
        {
            if (settingsWindow == null || settingsWindow.IsDisposed) settingsWindow = new SettingsWindow(this, settings, icon);
            if (settingsWindow.WindowState == FormWindowState.Minimized) settingsWindow.WindowState = FormWindowState.Normal;
            settingsWindow.Show(); Native.ShowWindow(settingsWindow.Handle, 1); settingsWindow.Activate(); settingsWindow.RefreshState();
        }
        internal void TogglePause()
        {
            Paused = !Paused;
            pauseItem.Text = Paused ? "Продолжить снег    Ctrl+Alt+S" : "Пауза    Ctrl+Alt+S";
            tray.Text = Paused ? "Тихая зима · пауза" : "Тихая зима · снег идёт";
            RefreshWindows(); UpdateSettings();
        }
        internal void CallDigger()
        {
            if (Paused) TogglePause();
            foreach (SnowOverlay overlay in overlays) overlay.World.CallDigger();
        }
        internal void PreferencesChanged()
        {
            SaveFailed = !settings.Save(settingsPath);
            diggerItem.Checked = settings.Excavator;
            foreach (SnowOverlay o in overlays) o.World.SyncFlakes();
            timer.Interval = settings.LowPower ? 50 : 33;
            SetTimerPrecision();
            RefreshWindows(); UpdateSettings();
        }
        internal void SnowBurst()
        {
            if (Paused) TogglePause();
            foreach (SnowOverlay overlay in overlays) overlay.World.SnowBurst();
        }
        private void SetTimerPrecision()
        {
            if (!settings.LowPower && !preciseTimer) preciseTimer = Native.timeBeginPeriod(1) == 0;
            if (settings.LowPower && preciseTimer) { Native.timeEndPeriod(1); preciseTimer = false; }
        }
        private void UpdateSettings() { if (settingsWindow != null && !settingsWindow.IsDisposed) settingsWindow.RefreshState(); }
        private void WriteDiagnostics()
        {
            LiveReport report = new LiveReport { Seconds = watch.Elapsed.TotalSeconds, Frames = frameCount, Paused = Paused,
                Density = settings.Density, Wind = settings.Wind, Garland = settings.Garland, LowPower = settings.LowPower,
                PauseHotkey = hotkeys.PauseRegistered, QuitHotkey = hotkeys.QuitRegistered,
                WinterFriends = settings.WinterFriends, CursorBroom = settings.CursorBroom, Countdown = settings.Countdown };
            foreach (SnowOverlay o in overlays)
            {
                int style = Native.GetWindowLong(o.Handle, -20);
                report.Displays.Add(new DisplayReport { Width = o.Width, Height = o.Height, Left = o.Left, Top = o.Top,
                    Flakes = o.World.Flakes.Count, Ledges = o.World.Ledges.Count, Cleared = o.World.Cleared,
                    DiggerX = o.World.DiggerX, Passes = o.World.Passes, Sleeping = o.Sleeping, BurstRemaining = o.World.BurstRemaining,
                    ClickThrough = (style & Native.WS_EX_TRANSPARENT) != 0,
                    NoActivate = (style & Native.WS_EX_NOACTIVATE) != 0, Layered = (style & Native.WS_EX_LAYERED) != 0 });
            }
            try { using (FileStream f = File.Create(diagnosticsPath)) new DataContractJsonSerializer(typeof(LiveReport)).WriteObject(f, report); }
            catch (IOException) { }
        }
        internal void Quit()
        {
            if (quitting) return;
            quitting = true;
            if (diagnosticsPath != null) WriteDiagnostics();
            timer.Stop();
            if (preciseTimer) { Native.timeEndPeriod(1); preciseTimer = false; }
            tray.Visible = false;
            foreach (SnowOverlay o in overlays) { o.Close(); o.Dispose(); }
            overlays.Clear();
            if (settingsWindow != null && !settingsWindow.IsDisposed) settingsWindow.Close();
            ExitThread();
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                SystemEvents.DisplaySettingsChanged -= OnDisplayChanged;
                SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
                SystemEvents.SessionSwitch -= OnSessionSwitch;
                Quit(); timer.Dispose(); tray.Dispose(); menu.Dispose(); hotkeys.Dispose(); icon.Dispose();
                stopEvent.Dispose(); settingsEvent.Dispose(); pauseEvent.Dispose();
            }
            base.Dispose(disposing);
        }
    }
    [DataContract] internal sealed class LiveReport
    {
        [DataMember] internal double Seconds;
        [DataMember] internal int Frames, Density, Wind;
        [DataMember] internal bool Paused, Garland, LowPower, PauseHotkey, QuitHotkey, WinterFriends, CursorBroom, Countdown;
        [DataMember] internal List<DisplayReport> Displays = new List<DisplayReport>();
    }
    [DataContract] internal sealed class DisplayReport
    {
        [DataMember] internal int Width, Height, Left, Top, Flakes, Ledges, Passes;
        [DataMember] internal float Cleared, DiggerX, BurstRemaining;
        [DataMember] internal bool Sleeping, ClickThrough, NoActivate, Layered;
    }
}
