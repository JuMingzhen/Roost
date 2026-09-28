using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Roost.App
{
    internal enum ButtonKind { Primary, Secondary, Ghost, Danger, DangerFilled, Soft, Toast, Nav, Segment }

    // 自绘按钮：圆角、悬停 / 按下 / 禁用状态，可带一个图标字形。继承 Button，保留回车确认、DialogResult 等行为。
    internal sealed class RoundButton : Button
    {
        private readonly ButtonKind kind;
        private bool hover;
        private bool pressed;
        private bool selected;
        private string glyph;

        internal RoundButton(string text, ButtonKind kind)
        {
            this.kind = kind;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Text = text;
            Font = kind == ButtonKind.Primary || kind == ButtonKind.DangerFilled ? Theme.BodyBold : Theme.Body;
            Cursor = Cursors.Hand;
            UseMnemonic = false;
            Radius = Theme.RadiusControl;
        }

        internal ButtonKind Kind { get { return kind; } }

        // 图标字形（Theme.Icons）。只有图标、没有文字的按钮要另设 AccessibleName。
        internal string Glyph
        {
            get { return glyph; }
            set { glyph = value; Invalidate(); }
        }

        internal Color GlyphColor { get; set; }

        // 圆角半径（100% 缩放下的像素）。Pill 为 true 时画成胶囊形（半径为高度的一半）。
        internal int Radius { get; set; }

        internal bool Pill { get; set; }

        internal bool Selected
        {
            get { return selected; }
            set
            {
                if (selected == value) return;
                selected = value;
                if (kind == ButtonKind.Nav || kind == ButtonKind.Segment) Font = value ? Theme.BodyBold : Theme.Body;
                Invalidate();
            }
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { pressed = true; Invalidate(); } base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Color outside = Parent != null ? Parent.BackColor : Theme.Paper;
            Color fill, border = Color.Empty, fore;
            Colors(outside, out fill, out border, out fore);
            float radius = Pill ? Height / 2F : DpiScale.Px(Radius);
            Theme.PaintRoundedBox(e.Graphics, this, fill, border, radius);

            bool left = kind == ButtonKind.Nav;
            int gap = string.IsNullOrEmpty(glyph) || string.IsNullOrEmpty(Text) ? 0 : DpiScale.Px(8);
            Size textSize = string.IsNullOrEmpty(Text) ? Size.Empty : TextRenderer.MeasureText(e.Graphics, Text, Font, Size.Empty, TextFormatFlags.NoPadding);
            Size glyphSize = Size.Empty;
            Font glyphFont = null;
            if (!string.IsNullOrEmpty(glyph))
            {
                glyphFont = Theme.IconFont(string.IsNullOrEmpty(Text) ? Font.SizeInPoints + 1.5F : Font.SizeInPoints);
                glyphSize = TextRenderer.MeasureText(e.Graphics, glyph, glyphFont, Size.Empty, TextFormatFlags.NoPadding);
            }
            int total = glyphSize.Width + gap + textSize.Width;
            int x = left ? DpiScale.Px(12) : (Width - total) / 2;
            if (glyphFont != null)
            {
                Color glyphColor = GlyphColor.IsEmpty || !Enabled ? fore : GlyphColor;
                TextRenderer.DrawText(e.Graphics, glyph, glyphFont, new Rectangle(x, 0, glyphSize.Width, Height), glyphColor, TextFormatFlags.NoPadding | TextFormatFlags.VerticalCenter);
                glyphFont.Dispose();
                x += glyphSize.Width + gap;
            }
            if (textSize.Width > 0)
                TextRenderer.DrawText(e.Graphics, Text, Font, new Rectangle(x, 0, Math.Max(0, Width - x), Height), fore, TextFormatFlags.NoPadding | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            if (Focused && ShowFocusCues)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                float inset = DpiScale.Px(2);
                using (GraphicsPath ring = Theme.RoundedRectangle(new RectangleF(inset, inset, Width - 2 * inset - 1, Height - 2 * inset - 1), Math.Max(1F, radius - inset)))
                using (Pen pen = new Pen(Theme.Accent, Math.Max(1F, DpiScale.Factor)))
                    e.Graphics.DrawPath(pen, ring);
            }
        }

        private void Colors(Color outside, out Color fill, out Color border, out Color fore)
        {
            border = Color.Empty;
            if (!Enabled)
            {
                bool flat = kind == ButtonKind.Ghost || kind == ButtonKind.Nav || kind == ButtonKind.Segment;
                fill = flat ? outside : Theme.Disabled;
                fore = Theme.DisabledText;
                return;
            }
            bool active = pressed || hover;
            switch (kind)
            {
                case ButtonKind.Primary:
                    fill = pressed ? Theme.AccentPressed : (hover ? Blend(Theme.Accent, Theme.AccentPressed) : Theme.Accent);
                    fore = Color.White;
                    break;
                case ButtonKind.DangerFilled:
                    fill = active ? Theme.DangerText : Theme.Danger;
                    fore = Color.White;
                    break;
                case ButtonKind.Secondary:
                    fill = pressed ? Theme.Line : (hover ? Theme.Sidebar : Theme.Card);
                    border = Theme.InputBorder;
                    fore = Theme.Text;
                    break;
                case ButtonKind.Danger:
                    fill = active ? Theme.DangerSoft : Theme.Card;
                    border = Theme.DangerLine;
                    fore = Theme.Danger;
                    break;
                case ButtonKind.Soft:
                    fill = active ? Blend(Theme.AccentTint, Theme.Line) : Theme.AccentTint;
                    fore = Theme.AccentPressed;
                    break;
                case ButtonKind.Toast:
                    fill = active ? Blend(Theme.ToastButton, Theme.TextMuted) : Theme.ToastButton;
                    fore = Theme.ToastText;
                    break;
                case ButtonKind.Nav:
                case ButtonKind.Segment:
                    fill = selected ? Theme.Card : (active ? Theme.SegmentTrack : outside);
                    fore = selected || kind == ButtonKind.Nav ? Theme.Text : Theme.TextMuted;
                    break;
                default:
                    fill = pressed ? Theme.Line : (hover ? Theme.SegmentTrack : outside);
                    fore = Theme.TextMuted;
                    break;
            }
        }

        private static Color Blend(Color a, Color b)
        {
            return Color.FromArgb((a.R + b.R) / 2, (a.G + b.G) / 2, (a.B + b.B) / 2);
        }
    }

    // 待办行左侧的圆形对勾：空心圆表示未完成，陶土色实心圆加对勾表示已完成。
    // Square 为 true 时画成方形勾选框（编辑模式的多选），和「完成」区分开。
    internal sealed class CheckCircle : Control
    {
        private bool isChecked;
        private bool hover;

        internal CheckCircle()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            Cursor = Cursors.Hand;
            TabStop = false;
        }

        internal bool Checked
        {
            get { return isChecked; }
            set { isChecked = value; Invalidate(); }
        }

        internal bool Square { get; set; }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent != null ? Parent.BackColor : Theme.Card);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            float diameter = DpiScale.Px(20);
            float stroke = Math.Max(1.5F, 1.6F * DpiScale.Factor);
            if (Square) diameter = DpiScale.Px(18);
            RectangleF circle = new RectangleF((Width - diameter) / 2F, (Height - diameter) / 2F, diameter, diameter);
            if (!isChecked) circle.Inflate(-stroke / 2F, -stroke / 2F);
            using (GraphicsPath shape = Square ? Theme.RoundedRectangle(circle, DpiScale.Px(4)) : new GraphicsPath())
            {
                if (!Square) shape.AddEllipse(circle);
                if (isChecked)
                {
                    using (SolidBrush brush = new SolidBrush(Theme.Accent)) e.Graphics.FillPath(brush, shape);
                    using (Font font = Theme.IconFont(7F))
                        TextRenderer.DrawText(e.Graphics, Theme.Icons.Check, font, Rectangle.Round(circle), Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                }
                else
                {
                    using (SolidBrush brush = new SolidBrush(hover ? Theme.AccentSoft : Theme.Card)) e.Graphics.FillPath(brush, shape);
                    using (Pen pen = new Pen(hover ? Theme.Accent : Theme.Ring, stroke)) e.Graphics.DrawPath(pen, shape);
                }
            }
        }
    }

    // 开关：左边是轨道和圆钮，右边是文字。
    internal sealed class ToggleSwitch : CheckBox
    {
        internal ToggleSwitch(string text)
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Text = text;
            Font = Theme.Body;
            Cursor = Cursors.Hand;
            AutoSize = false;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent != null ? Parent.BackColor : Theme.Card);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            float width = DpiScale.Px(36), height = DpiScale.Px(20);
            RectangleF track = new RectangleF(1, (Height - height) / 2F, width, height);
            Color trackColor = !Enabled ? Theme.Disabled : (Checked ? Theme.Accent : Theme.Ring);
            using (GraphicsPath path = Theme.RoundedRectangle(track, height / 2F))
            using (SolidBrush brush = new SolidBrush(trackColor)) e.Graphics.FillPath(brush, path);
            float knob = DpiScale.Px(14);
            float knobX = Checked ? track.Right - knob - DpiScale.Px(3) : track.Left + DpiScale.Px(3);
            using (SolidBrush brush = new SolidBrush(Color.White)) e.Graphics.FillEllipse(brush, knobX, track.Top + (height - knob) / 2F, knob, knob);
            e.Graphics.SmoothingMode = SmoothingMode.None;
            int textX = (int)track.Right + DpiScale.Px(10);
            TextRenderer.DrawText(e.Graphics, Text, Font, new Rectangle(textX, 0, Math.Max(0, Width - textX), Height), Enabled ? Theme.Text : Theme.DisabledText, TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
            if (Focused && ShowFocusCues)
                using (Pen pen = new Pen(Theme.Accent, Math.Max(1F, DpiScale.Factor))) e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        }
    }

    // 分段切换：浅色轨道里放几个选项，选中的一个是白底粗体。
    internal sealed class SegmentedControl : Panel
    {
        private readonly RoundButton[] options;
        private int selectedIndex;

        internal event EventHandler SelectedIndexChanged;

        internal SegmentedControl(params string[] labels)
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.SegmentTrack;
            options = new RoundButton[labels.Length];
            for (int i = 0; i < labels.Length; i++)
            {
                int index = i;
                options[i] = new RoundButton(labels[i], ButtonKind.Segment) { Font = Theme.Caption, TabStop = false };
                options[i].Click += delegate { SelectedIndex = index; };
                Controls.Add(options[i]);
            }
            options[0].Selected = true;
        }

        internal int SelectedIndex
        {
            get { return selectedIndex; }
            set
            {
                if (value == selectedIndex) return;
                selectedIndex = value;
                for (int i = 0; i < options.Length; i++)
                {
                    options[i].Selected = i == value;
                    options[i].Font = i == value ? Theme.CaptionBold : Theme.Caption;
                }
                EventHandler handler = SelectedIndexChanged;
                if (handler != null) handler(this, EventArgs.Empty);
            }
        }

        // 按最宽的选项定出总宽度（物理像素）；各选项在 OnResize 里等分。
        internal void LayoutOptions()
        {
            int widest = 0;
            foreach (RoundButton option in options) widest = Math.Max(widest, TextRenderer.MeasureText(option.Text, Theme.CaptionBold).Width);
            Width = options.Length * (widest + DpiScale.Px(24)) + DpiScale.Px(6) + (options.Length - 1) * DpiScale.Px(2);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            LayoutSegments();
        }

        internal void LayoutSegments()
        {
            int pad = Math.Max(2, Height / 10);
            int gap = Math.Max(1, pad / 2);
            int width = (Width - 2 * pad - (options.Length - 1) * gap) / options.Length;
            for (int i = 0; i < options.Length; i++)
                options[i].Bounds = new Rectangle(pad + i * (width + gap), pad, width, Height - 2 * pad);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Color outside = Parent != null ? Parent.BackColor : Theme.Paper;
            e.Graphics.Clear(outside);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath path = Theme.RoundedRectangle(new RectangleF(0, 0, Width - 1, Height - 1), DpiScale.Px(Theme.RadiusCard)))
            using (SolidBrush brush = new SolidBrush(Theme.SegmentTrack)) e.Graphics.FillPath(brush, path);
        }
    }

    // 圆角卡片容器：白底、细边。放在里面的控件用 Theme.Card 作底色。
    internal class CardPanel : Panel
    {
        internal CardPanel()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Card;
            BorderColor = Theme.Line;
            Radius = Theme.RadiusCard;
        }

        internal Color BorderColor { get; set; }

        internal int Radius { get; set; }

        internal bool Pill { get; set; }

        protected override void OnPaint(PaintEventArgs e)
        {
            Theme.PaintRoundedBox(e.Graphics, this, BackColor, BorderColor, Pill ? Height / 2F : DpiScale.Px(Radius));
        }
    }

    // 圆角输入框：外面画边框，里面放一个无边框的 TextBox。获得焦点时边框变成陶土色。
    internal sealed class TextField : CardPanel
    {
        private readonly TextBox box;

        internal TextField()
        {
            BorderColor = Theme.InputBorder;
            Radius = Theme.RadiusControl;
            box = new TextBox { BorderStyle = BorderStyle.None, BackColor = Theme.Card, ForeColor = Theme.Text, Font = Theme.Body };
            box.GotFocus += delegate { BorderColor = Theme.Accent; Invalidate(); };
            box.LostFocus += delegate { BorderColor = Theme.InputBorder; Invalidate(); };
            Controls.Add(box);
            Cursor = Cursors.IBeam;
            Click += delegate { box.Focus(); };
        }

        internal TextBox Box { get { return box; } }

        // 左右留白（100% 缩放下的像素）。右侧可以给内嵌按钮让位。
        internal int PaddingLeft { get; set; }
        internal int PaddingRight { get; set; }

        internal void LayoutBox()
        {
            int left = DpiScale.Px(PaddingLeft == 0 ? 10 : PaddingLeft);
            int right = DpiScale.Px(PaddingRight == 0 ? 10 : PaddingRight);
            if (box.Multiline)
            {
                int pad = DpiScale.Px(8);
                box.Bounds = new Rectangle(left, pad, Width - left - right, Height - 2 * pad);
            }
            else
            {
                int height = box.PreferredHeight;
                box.Bounds = new Rectangle(left, (Height - height) / 2, Width - left - right, height);
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            LayoutBox();
        }
    }

    // 滑块：浅色轨道、陶土色已选部分和白色圆钮。支持拖动、点击和方向键。
    internal sealed class Slider : Control
    {
        private int minimum;
        private int maximum = 100;
        private int value;
        private bool dragging;

        internal event EventHandler ValueChanged;

        internal Slider()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            TabStop = true;
            Cursor = Cursors.Hand;
        }

        internal int Minimum { get { return minimum; } set { minimum = value; Value = this.value; } }
        internal int Maximum { get { return maximum; } set { maximum = value; Value = this.value; } }

        internal int Value
        {
            get { return value; }
            set
            {
                int clamped = Math.Max(minimum, Math.Min(maximum, value));
                if (clamped == this.value) return;
                this.value = clamped;
                Invalidate();
                EventHandler handler = ValueChanged;
                if (handler != null) handler(this, EventArgs.Empty);
            }
        }

        private float Knob { get { return DpiScale.Px(16); } }

        protected override bool IsInputKey(Keys keyData)
        {
            return keyData == Keys.Left || keyData == Keys.Right || keyData == Keys.Up || keyData == Keys.Down || base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Left || e.KeyCode == Keys.Down) Value = value - 1;
            else if (e.KeyCode == Keys.Right || e.KeyCode == Keys.Up) Value = value + 1;
            else if (e.KeyCode == Keys.PageDown) Value = value - 5;
            else if (e.KeyCode == Keys.PageUp) Value = value + 5;
            else if (e.KeyCode == Keys.Home) Value = minimum;
            else if (e.KeyCode == Keys.End) Value = maximum;
            base.OnKeyDown(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            Focus();
            dragging = true;
            Capture = true;
            SetFromX(e.X);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (dragging) SetFromX(e.X);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            dragging = false;
            Capture = false;
        }

        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

        private void SetFromX(int x)
        {
            float left = Knob / 2F, right = Width - Knob / 2F;
            if (right <= left) return;
            float ratio = Math.Max(0F, Math.Min(1F, (x - left) / (right - left)));
            Value = minimum + (int)Math.Round(ratio * (maximum - minimum));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent != null ? Parent.BackColor : Theme.Card);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            float knob = Knob;
            float left = knob / 2F, right = Width - knob / 2F;
            float center = Height / 2F;
            float thickness = DpiScale.Px(4);
            float ratio = maximum > minimum ? (value - minimum) / (float)(maximum - minimum) : 0F;
            float x = left + ratio * (right - left);
            using (GraphicsPath track = Theme.RoundedRectangle(new RectangleF(left, center - thickness / 2F, right - left, thickness), thickness / 2F))
            using (SolidBrush brush = new SolidBrush(Theme.Line)) e.Graphics.FillPath(brush, track);
            if (x > left)
                using (GraphicsPath done = Theme.RoundedRectangle(new RectangleF(left, center - thickness / 2F, x - left, thickness), thickness / 2F))
                using (SolidBrush brush = new SolidBrush(Enabled ? Theme.Accent : Theme.Ring)) e.Graphics.FillPath(brush, done);
            RectangleF thumb = new RectangleF(x - knob / 2F, center - knob / 2F, knob, knob);
            using (SolidBrush brush = new SolidBrush(Color.White)) e.Graphics.FillEllipse(brush, thumb);
            float stroke = Math.Max(1.5F, 2F * DpiScale.Factor);
            thumb.Inflate(-stroke / 2F, -stroke / 2F);
            using (Pen pen = new Pen(Focused ? Theme.AccentPressed : Theme.Accent, stroke)) e.Graphics.DrawEllipse(pen, thumb);
        }
    }

    // 右键菜单与托盘菜单：白底、暖灰细边、选中项浅陶土色圆角底；Windows 11 上请求系统圆角。
    internal sealed class ThemedMenuRenderer : ToolStripProfessionalRenderer
    {
        private ThemedMenuRenderer() : base(new MenuColors())
        {
            RoundedEdges = false;
        }

        internal static void Apply(ContextMenuStrip menu)
        {
            menu.Renderer = new ThemedMenuRenderer();
            menu.Font = Theme.Body;
            menu.ShowImageMargin = false;
            menu.BackColor = Theme.Card;
            menu.Padding = new Padding(DpiScale.Px(4));
            menu.MinimumSize = new Size(DpiScale.Px(148), 0);
            foreach (ToolStripItem item in menu.Items)
                if (item is ToolStripMenuItem) item.Padding = new Padding(0, DpiScale.Px(5), 0, DpiScale.Px(5));
            menu.HandleCreated += delegate
            {
                int preference = NativeMethods.DWMWCP_ROUND;
                try { NativeMethods.DwmSetWindowAttribute(menu.Handle, NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE, ref preference, sizeof(int)); }
                catch (DllNotFoundException) { }
                catch (EntryPointNotFoundException) { }
            };
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (!e.Item.Selected || !e.Item.Enabled) return;
            Rectangle bounds = new Rectangle(DpiScale.Px(2), 0, e.Item.Width - DpiScale.Px(4), e.Item.Height);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath path = Theme.RoundedRectangle(bounds, DpiScale.Px(Theme.RadiusControl)))
            using (SolidBrush brush = new SolidBrush(Theme.AccentSoft)) e.Graphics.FillPath(brush, path);
            e.Graphics.SmoothingMode = SmoothingMode.None;
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            if (e.Item.Enabled && e.TextColor != Theme.Danger) e.TextColor = Theme.Text;
            // 没有图标栏时文字贴着左边，右移一些留出呼吸感。
            Rectangle text = e.TextRectangle;
            e.TextRectangle = new Rectangle(text.X + DpiScale.Px(8), text.Y, text.Width, text.Height);
            base.OnRenderItemText(e);
        }

        private sealed class MenuColors : ProfessionalColorTable
        {
            public override Color ToolStripDropDownBackground { get { return Theme.Card; } }
            public override Color MenuBorder { get { return Theme.Line; } }
            public override Color MenuItemBorder { get { return Color.Transparent; } }
            public override Color MenuItemSelected { get { return Theme.AccentSoft; } }
            public override Color ImageMarginGradientBegin { get { return Theme.Card; } }
            public override Color ImageMarginGradientMiddle { get { return Theme.Card; } }
            public override Color ImageMarginGradientEnd { get { return Theme.Card; } }
            public override Color SeparatorDark { get { return Theme.Line; } }
            public override Color SeparatorLight { get { return Theme.Card; } }
        }
    }
}
