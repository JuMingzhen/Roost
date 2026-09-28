using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Roost.Core;

namespace Roost.App
{
    // 清单里的一条待办：白色圆角小卡片。平时左边是圆形对勾，编辑模式下换成方形勾选框。
    // 行上不放删除按钮（PRD 7.4）：删单条在编辑表单里，删多条在编辑模式里。
    internal sealed class TodoRowControl : Control
    {
        internal const int RowWidth = 312;
        internal const int RowHeight = 52;

        private readonly TodoItem item;
        private readonly string timeLabel;
        private readonly CheckCircle complete;
        private readonly RoundButton star;
        private readonly bool editing;
        private bool completed;
        private bool selected;
        private bool hover;

        internal event Action<TodoItem> EditRequested;
        internal event Action<TodoItem> CompleteRequested;
        internal event Action<TodoItem> StarRequested;
        internal event Action<TodoItem> SelectionChanged;

        internal TodoRowControl(TodoItem item, string timeLabel, bool editing, bool selected)
        {
            this.item = item;
            this.timeLabel = timeLabel;
            this.editing = editing;
            this.selected = selected;
            completed = item.IsCompleted;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Size = new Size(RowWidth, RowHeight);
            Margin = new Padding(0, 0, 0, 6);
            BackColor = Theme.Card;
            Font = Theme.Body;
            Cursor = Cursors.Hand;
            AccessibleName = item.Title;
            AccessibleRole = editing ? AccessibleRole.CheckButton : AccessibleRole.PushButton;

            complete = new CheckCircle { Location = new Point(4, 10), Size = new Size(32, 32), Square = editing, Checked = editing ? selected : completed };
            complete.AccessibleName = editing ? "选择" : (completed ? "取消完成" : "完成");
            complete.Click += delegate { if (editing) ToggleSelected(); else Raise(CompleteRequested); };
            star = new RoundButton(string.Empty, ButtonKind.Ghost)
            {
                Glyph = item.IsStarred ? Theme.Icons.StarFilled : Theme.Icons.Star,
                GlyphColor = item.IsStarred ? Theme.Star : Theme.TextFaint,
                Location = new Point(RowWidth - 36, 12),
                Size = new Size(28, 28),
                TabStop = false,
                AccessibleName = item.IsStarred ? "取消星标" : "加星标",
                Visible = item.IsStarred && !editing
            };
            star.Click += delegate { Raise(StarRequested); };
            star.MouseEnter += delegate { SetHover(true); };
            star.MouseLeave += delegate { SetHover(ContainsCursor()); };
            complete.MouseEnter += delegate { SetHover(true); };
            complete.MouseLeave += delegate { SetHover(ContainsCursor()); };
            Controls.Add(complete);
            Controls.Add(star);
            UpdateFill();
        }

        internal TodoItem Item { get { return item; } }

        internal bool IsSelected { get { return selected; } }

        internal void MarkCompleted()
        {
            completed = true;
            complete.Checked = true;
            complete.AccessibleName = "取消完成";
            UpdateFill();
        }

        protected override void OnMouseEnter(EventArgs e) { SetHover(true); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { SetHover(ContainsCursor()); base.OnMouseLeave(e); }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            if (editing) ToggleSelected(); else Raise(EditRequested);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Color fill = BackColor;
            Color border = selected ? Theme.Accent : (hover && !completed ? Theme.InputBorder : Theme.Line);
            Theme.PaintRoundedBox(e.Graphics, this, fill, border, DpiScale.Px(Theme.RadiusCard));
            int left = complete.Right + DpiScale.Px(6);
            int right = (star.Visible ? star.Left : Width - DpiScale.Px(8)) - DpiScale.Px(4);
            string time = completed && !editing ? "已完成 · 再点圆圈可取消" : timeLabel;
            // 没有时间标签的行，标题垂直居中。
            Rectangle titleBounds = string.IsNullOrEmpty(time)
                ? new Rectangle(left, 0, Math.Max(0, right - left), Height)
                : new Rectangle(left, DpiScale.Px(7), Math.Max(0, right - left), DpiScale.Px(22));
            Rectangle timeBounds = new Rectangle(left, DpiScale.Px(28), Math.Max(0, right - left), DpiScale.Px(18));
            TextFormatFlags flags = TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix;
            bool faded = completed && !editing;
            if (faded)
                using (Font struck = new Font(Theme.Body, FontStyle.Strikeout))
                    TextRenderer.DrawText(e.Graphics, item.Title, struck, titleBounds, Theme.TextFaint, flags);
            else
                TextRenderer.DrawText(e.Graphics, item.Title, Theme.BodyBold, titleBounds, Theme.Text, flags);
            Color timeColor = faded ? Theme.TextFaint : TimeColor(timeLabel);
            Font timeFont = !faded && timeColor != Theme.TextMuted ? Theme.CaptionBold : Theme.Caption;
            TextRenderer.DrawText(e.Graphics, time, timeFont, timeBounds, timeColor, flags);
        }

        // 过期用红色、今天用陶土色，其余用次要文字色（颜色之外还有文字本身区分，不只靠颜色）。
        internal static Color TimeColor(string label)
        {
            if (string.IsNullOrEmpty(label)) return Theme.TextMuted;
            if (label.StartsWith("已过期", StringComparison.Ordinal)) return Theme.Danger;
            if (label.StartsWith("今天", StringComparison.Ordinal)) return Theme.Accent;
            return Theme.TextMuted;
        }

        private void ToggleSelected()
        {
            selected = !selected;
            complete.Checked = selected;
            UpdateFill();
            Raise(SelectionChanged);
        }

        // 子控件（对勾、星标）按 BackColor 铺底，所以卡片底色变化时同步 BackColor。
        private void UpdateFill()
        {
            BackColor = selected ? Theme.AccentSoft : (completed && !editing ? Theme.Sidebar : Theme.Card);
            Invalidate(true);
        }

        private void SetHover(bool value)
        {
            if (hover == value) return;
            hover = value;
            if (!editing && !completed) star.Visible = item.IsStarred || hover;
            Invalidate();
        }

        private bool ContainsCursor()
        {
            return IsHandleCreated && ClientRectangle.Contains(PointToClient(Cursor.Position));
        }

        private void Raise(Action<TodoItem> action)
        {
            if (action != null) action(item);
        }
    }
}
