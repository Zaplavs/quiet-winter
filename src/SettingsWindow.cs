using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace QuietWinter
{
    internal static class Theme
    {
        internal static readonly Color Background = Color.FromArgb(17, 27, 38), Card = Color.FromArgb(28, 41, 54),
            Line = Color.FromArgb(48, 63, 76), Text = Color.FromArgb(238, 244, 246),
            Muted = Color.FromArgb(147, 165, 180), Mint = Color.FromArgb(185, 226, 208);
    }
    internal sealed class SoftButton : Control
    {
        internal bool Selected, Primary;
        private bool hover;
        internal SoftButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable, true);
            Cursor = Cursors.Hand;
            Font = new Font("Segoe UI", 10.5f, FontStyle.Regular);
            ForeColor = Theme.Text;
            TabStop = true;
            AccessibleRole = AccessibleRole.PushButton;
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            RectangleF r = new RectangleF(1, 1, Width - 2, Height - 2);
            Color color = Primary || Selected ? Theme.Mint : hover ? Color.FromArgb(39, 56, 70) : Theme.Card;
            WinterRenderer.RoundFill(g, color, r.X, r.Y, r.Width, r.Height, 10);
            if (Focused)
            {
                using (GraphicsPath p = WinterRenderer.Rounded(new RectangleF(3, 3, Width - 6, Height - 6), 8))
                using (Pen pen = new Pen(Theme.Muted)) g.DrawPath(pen, p);
            }
            TextRenderer.DrawText(g, Text, Font, ClientRectangle, Primary || Selected ? Theme.Background : ForeColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter) { OnClick(EventArgs.Empty); e.Handled = true; }
            base.OnKeyDown(e);
        }
    }
    internal sealed class Toggle : Control
    {
        private bool value;
        internal event EventHandler Changed;
        internal bool Value { get { return value; } set { this.value = value; Invalidate(); } }
        internal Toggle()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable, true);
            Size = new Size(46, 28); Cursor = Cursors.Hand; TabStop = true;
            AccessibleRole = AccessibleRole.CheckButton;
        }
        protected override void OnClick(EventArgs e)
        {
            value = !value; Invalidate(); if (Changed != null) Changed(this, EventArgs.Empty); base.OnClick(e);
        }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space) { OnClick(EventArgs.Empty); e.Handled = true; }
            base.OnKeyDown(e);
        }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            float h = Height - 6, w = Width - 2;
            WinterRenderer.RoundFill(g, value ? Theme.Mint : Color.FromArgb(60, 76, 90), 1, 3, w, h, h / 2);
            float d = h - 6, x = value ? Width - d - 5 : 5;
            using (SolidBrush b = new SolidBrush(value ? Theme.Background : Theme.Text)) g.FillEllipse(b, x, 6, d, d);
            if (Focused) ControlPaint.DrawFocusRectangle(g, ClientRectangle, Theme.Muted, Theme.Background);
        }
    }
    internal sealed class WindSlider : Control
    {
        internal int Value;
        internal event EventHandler Changed;
        private bool dragging;
        internal WindSlider()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable, true);
            Cursor = Cursors.Hand; TabStop = true; Height = 34;
            AccessibleRole = AccessibleRole.Slider;
        }
        private void Change(int x)
        {
            Value = Math.Max(-100, Math.Min(100, (int)((x - 10f) / (Width - 20) * 200 - 100)));
            if (Math.Abs(Value) < 5) Value = 0;
            Invalidate(); if (Changed != null) Changed(this, EventArgs.Empty);
        }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { Focus(); dragging = true; Capture = true; Change(e.X); } base.OnMouseDown(e); }
        protected override void OnMouseMove(MouseEventArgs e) { if (dragging) Change(e.X); base.OnMouseMove(e); }
        protected override void OnMouseUp(MouseEventArgs e) { dragging = false; Capture = false; base.OnMouseUp(e); }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            int next = Value;
            if (e.KeyCode == Keys.Left) next -= 5;
            if (e.KeyCode == Keys.Right) next += 5;
            if (e.KeyCode == Keys.Home || e.KeyCode == Keys.Space) next = 0;
            if (next != Value) { Value = Math.Max(-100, Math.Min(100, next)); Invalidate(); if (Changed != null) Changed(this, EventArgs.Empty); e.Handled = true; }
            base.OnKeyDown(e);
        }
        protected override bool IsInputKey(Keys key) { return key == Keys.Left || key == Keys.Right || base.IsInputKey(key); }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            float y = Height / 2f, center = Width / 2f, x = 10 + (Value + 100) / 200f * (Width - 20);
            using (Pen rail = new Pen(Theme.Line, 4) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawLine(rail, 10, y, Width - 10, y);
            using (Pen active = new Pen(Theme.Mint, 4) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawLine(active, center, y, x, y);
            using (SolidBrush b = new SolidBrush(Theme.Mint)) g.FillEllipse(b, x - 7, y - 7, 14, 14);
            if (Focused) ControlPaint.DrawFocusRectangle(g, ClientRectangle, Theme.Muted, Theme.Background);
        }
    }
    internal sealed class SettingsWindow : Form
    {
        private readonly WinterContext owner;
        private readonly Preferences settings;
        private readonly SoftButton[] densityButtons = new SoftButton[3];
        private readonly SoftButton pause;
        private readonly Label windValue, status;
        private readonly Toggle digger;
        protected override bool ShowWithoutActivation { get { return owner == null; } }
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams p = base.CreateParams;
                if (owner == null) p.ExStyle |= Native.WS_EX_NOACTIVATE | Native.WS_EX_TOOLWINDOW;
                return p;
            }
        }
        internal SettingsWindow(WinterContext context, Preferences preferences, Icon icon)
        {
            owner = context; settings = preferences;
            Text = "Тихая зима"; Icon = icon;
            ClientSize = new Size(760, 710);
            BackColor = Theme.Background; ForeColor = Theme.Text;
            Font = new Font("Segoe UI", 10);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false; StartPosition = FormStartPosition.CenterScreen;
            AutoScaleDimensions = new SizeF(96, 96); AutoScaleMode = AutoScaleMode.Dpi;
            Label brand = AddLabel("ТИХАЯ ЗИМА", 65, 27, 220, 23, 10, Theme.Mint, FontStyle.Bold);
            PictureBox logo = new PictureBox { Location = new Point(30, 22), Size = new Size(27, 27), Image = icon.ToBitmap(), SizeMode = PictureBoxSizeMode.Zoom };
            Controls.Add(logo);
            AddLabel("Пусть идёт снег.", 28, 65, 530, 48, 27, Theme.Text, FontStyle.Bold);
            AddLabel("Маленькая зима на вашем рабочем столе.", 31, 116, 520, 25, 11, Theme.Muted, FontStyle.Regular);
            AddLabel("СНЕГОПАД", 31, 158, 220, 22, 9, Theme.Muted, FontStyle.Bold);
            string[] labels = { "Тихий снег", "Снегопад", "Метель" };
            for (int i = 0; i < 3; i++)
            {
                int index = i;
                SoftButton b = new SoftButton { Text = labels[i], Location = new Point(30 + i * 240, 187), Size = new Size(220, 49), Selected = settings.Density == i, AccessibleName = labels[i] };
                b.Click += delegate { settings.Density = index; foreach (SoftButton button in densityButtons) { button.Selected = button == b; button.Invalidate(); } owner.PreferencesChanged(); };
                densityButtons[i] = b; Controls.Add(b);
            }
            AddLabel("Ветер", 31, 260, 120, 26, 11, Theme.Text, FontStyle.Regular);
            windValue = AddLabel("", 490, 262, 240, 23, 10, Theme.Mint, FontStyle.Regular);
            windValue.TextAlign = ContentAlignment.MiddleRight;
            WindSlider wind = new WindSlider { Location = new Point(30, 291), Width = 700, Value = settings.Wind, AccessibleName = "Сила и направление ветра" };
            wind.Changed += delegate { settings.Wind = wind.Value; UpdateWind(); owner.PreferencesChanged(); };
            Controls.Add(wind); UpdateWind();
            digger = AddToggle("Экскаватор", "Убирает снег над панелью задач", 30, 350, settings.Excavator,
                delegate(bool v) { settings.Excavator = v; owner.PreferencesChanged(); });
            AddToggle("Сугробы на окнах", "Снег на верхних краях окон", 30, 404, settings.WindowSnow,
                delegate(bool v) { settings.WindowSnow = v; owner.PreferencesChanged(); });
            AddToggle("Новогодняя гирлянда", "Тёплые огоньки вверху экрана", 30, 458, settings.Garland,
                delegate(bool v) { settings.Garland = v; owner.PreferencesChanged(); });
            AddToggle("Пауза в полном экране", "Скрывать снег в играх и видео", 400, 512, settings.PauseFullscreen,
                delegate(bool v) { settings.PauseFullscreen = v; owner.PreferencesChanged(); });
            AddToggle("Экономный режим", "До 20 кадров в секунду вместо 30", 30, 512, settings.LowPower,
                delegate(bool v) { settings.LowPower = v; owner.PreferencesChanged(); });
            AddToggle("Ёлочка и снеговик", "Огоньки, шарф и дружеский взмах рукой", 400, 350, settings.WinterFriends,
                delegate(bool v) { settings.WinterFriends = v; owner.PreferencesChanged(); });
            AddToggle("Варежка", "Зажмите Shift и смахните снег курсором", 400, 404, settings.CursorBroom,
                delegate(bool v) { settings.CursorBroom = v; owner.PreferencesChanged(); });
            AddToggle("До Нового года", "Небольшой праздничный обратный отсчёт", 400, 458, settings.Countdown,
                delegate(bool v) { settings.Countdown = v; owner.PreferencesChanged(); });
            SoftButton call = new SoftButton { Text = "Позвать экскаватор", Primary = true, Location = new Point(30, 603), Size = new Size(220, 46) };
            call.Click += delegate { owner.CallDigger(); };
            Controls.Add(call);
            SoftButton burst = new SoftButton { Text = "Снежный вихрь", Location = new Point(270, 603), Size = new Size(220, 46) };
            burst.Click += delegate { owner.SnowBurst(); };
            Controls.Add(burst);
            pause = new SoftButton { Text = "", Location = new Point(510, 603), Size = new Size(220, 46) };
            pause.Click += delegate { owner.TogglePause(); };
            Controls.Add(pause);
            status = AddLabel("", 31, 664, 700, 22, 8.5f, Theme.Muted, FontStyle.Regular);
            RefreshState();
            FormClosed += delegate { logo.Image.Dispose(); };
        }
        private Label AddLabel(string text, int x, int y, int width, int height, float font, Color color, FontStyle style)
        {
            Label label = new Label { Text = text, Location = new Point(x, y), Size = new Size(width, height),
                Font = new Font("Segoe UI", font, style), ForeColor = color, AutoSize = false, UseMnemonic = false };
            Controls.Add(label); return label;
        }
        private Toggle AddToggle(string title, string description, int x, int y, bool value, Action<bool> callback)
        {
            AddLabel(title, x + 1, y - 1, 267, 24, 10.5f, Theme.Text, FontStyle.Regular);
            AddLabel(description, x + 1, y + 22, 318, 20, 8.2f, Theme.Muted, FontStyle.Regular);
            Toggle toggle = new Toggle { Location = new Point(x + 284, y + 3), Value = value, AccessibleName = title };
            toggle.Changed += delegate { callback(toggle.Value); };
            Controls.Add(toggle); return toggle;
        }
        private void UpdateWind()
        {
            windValue.Text = settings.Wind == 0 ? "Штиль" : (settings.Wind < 0 ? "← Влево  " : "Вправо →  ") + Math.Abs(settings.Wind) + "%";
        }
        internal void RefreshState()
        {
            pause.Text = owner != null && owner.Paused ? "Продолжить снег" : "Пауза";
            pause.Invalidate();
            status.Text = owner != null && owner.SaveFailed ? "Не удалось сохранить настройки. Проверьте доступ к папке профиля." : "Ctrl + Alt + S — пауза    ·    Ctrl + Alt + B — вихрь    ·    Ctrl + Alt + Q — выключить";
            digger.Value = settings.Excavator;
        }
    }
}
