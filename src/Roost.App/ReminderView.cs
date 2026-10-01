using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Roost.Core;

namespace Roost.App
{
    // 提醒泡（PRD 10.2、10.4，效果图「M3 提醒」）：单条、多条合并（收起 / 展开）、错过汇总。
    // 一直保留到用户回应。内容每次按状态重建，尺寸按 100% 缩放写，用 DpiScale.Px 换成物理像素。
    internal sealed class ReminderView : Panel
    {
        private const int SingleWidth = 270;
        private const int ExpandedWidth = 330;
        private const int Pad = 14;
        private const int MaxRows = 5;

        private List<TodoItem> items = new List<TodoItem>();
        private Func<TodoItem, string> describe;
        private int missed;
        private bool expanded;
        private string snoozeOpenId;

        internal event Action<string> CompleteRequested;
        internal event Action<string, int> SnoozeRequested;
        internal event Action<string> DismissRequested;
        internal event Action DismissAllRequested;
        internal event Action MissedDismissed;
        // 用户展开合并泡或打开某条的「稍后」选项，需要按新状态重建并重新排位置。
        internal event Action SizeChangedByUser;

        internal ReminderView()
        {
            BackColor = Theme.Card;
            Font = Theme.Body;
        }

        internal string ModeForTest
        {
            get
            {
                if (items.Count == 0) return missed > 0 ? "missed" : "none";
                if (items.Count == 1) return missed > 0 ? "single+missed" : "single";
                return (expanded ? "expanded" : "collapsed") + (missed > 0 ? "+missed" : string.Empty);
            }
        }

        internal string HeadlineForTest { get; private set; }

        internal void ExpandForTest()
        {
            SetExpanded(true);
        }

        // 按当前提醒重建内容，返回需要的大小（物理像素）；没有要显示的返回 Size.Empty。
        internal Size ShowState(List<TodoItem> active, int missedCount, Func<TodoItem, string> describeItem)
        {
            items = active;
            missed = missedCount;
            describe = describeItem;
            if (items.Count < 2) expanded = false;
            bool snoozeStillThere = false;
            foreach (TodoItem item in items) if (item.Id == snoozeOpenId) snoozeStillThere = true;
            if (!snoozeStillThere) snoozeOpenId = null;
            return Rebuild();
        }

        private Size Rebuild()
        {
            SuspendLayout();
            foreach (Control old in new List<Control>(ControlsArray())) old.Dispose();
            Controls.Clear();
            HeadlineForTest = null;
            if (items.Count == 0 && missed == 0)
            {
                ResumeLayout();
                return Size.Empty;
            }
            int width = DpiScale.Px(items.Count >= 2 && expanded ? ExpandedWidth : SingleWidth);
            int inner = width - 2 * DpiScale.Px(Pad);
            int y = DpiScale.Px(Pad);
            if (missed > 0)
            {
                y = BuildMissed(y, inner);
                if (items.Count > 0)
                {
                    Controls.Add(new Panel { Bounds = new Rectangle(DpiScale.Px(Pad), y + DpiScale.Px(4), inner, Math.Max(1, DpiScale.Px(1))), BackColor = Theme.Line });
                    y += DpiScale.Px(14);
                }
            }
            if (items.Count == 1) y = BuildSingle(y, inner, items[0]);
            else if (items.Count >= 2 && !expanded) y = BuildCollapsed(y, inner);
            else if (items.Count >= 2) y = BuildExpanded(y, inner);
            ResumeLayout();
            return new Size(width, y + DpiScale.Px(Pad));
        }

        private Control[] ControlsArray()
        {
            Control[] array = new Control[Controls.Count];
            Controls.CopyTo(array, 0);
            return array;
        }

        private int BuildMissed(int y, int inner)
        {
            string headline = string.Format("你不在的时候有 {0} 件事到点了", missed);
            if (HeadlineForTest == null) HeadlineForTest = headline;
            y = AddText(headline, Theme.BodyBold, Theme.Text, y, inner) + DpiScale.Px(4);
            y = AddText("它们已排在清单最前面，标着「已过期」。", Theme.Caption, Theme.TextMuted, y, inner) + DpiScale.Px(8);
            RoundButton ok = new RoundButton("知道了", ButtonKind.Soft) { Font = Theme.CaptionBold };
            ok.Bounds = new Rectangle(DpiScale.Px(Pad), y, TextWidth("知道了", ok.Font) + DpiScale.Px(28), DpiScale.Px(28));
            ok.Click += delegate { Raise(MissedDismissed); };
            Controls.Add(ok);
            return ok.Bottom;
        }

        private int BuildSingle(int y, int inner, TodoItem item)
        {
            if (HeadlineForTest == null) HeadlineForTest = item.Title;
            y = AddHeader(y, inner, "到点了", Theme.CaptionBold, Theme.Accent, false) + DpiScale.Px(6);
            y = AddText(item.Title, Theme.CardTitle, Theme.Text, y, inner) + DpiScale.Px(2);
            y = AddText(describe(item), Theme.Caption, Theme.TextMuted, y, inner) + DpiScale.Px(10);

            int half = (inner - DpiScale.Px(8)) / 2;
            RoundButton complete = new RoundButton("完成", ButtonKind.Primary) { Glyph = Theme.Icons.Check };
            complete.Bounds = new Rectangle(DpiScale.Px(Pad), y, half, DpiScale.Px(32));
            complete.Click += delegate { Raise(CompleteRequested, item.Id); };
            RoundButton dismiss = new RoundButton("知道了", ButtonKind.Secondary);
            dismiss.Bounds = new Rectangle(DpiScale.Px(Pad) + inner - half, y, half, DpiScale.Px(32));
            dismiss.Click += delegate { Raise(DismissRequested, item.Id); };
            Controls.Add(complete);
            Controls.Add(dismiss);
            y = complete.Bottom + DpiScale.Px(10);

            Controls.Add(new Panel { Bounds = new Rectangle(DpiScale.Px(Pad), y, inner, Math.Max(1, DpiScale.Px(1))), BackColor = Theme.Line });
            y += DpiScale.Px(10);
            Label later = new Label { Text = "稍后提醒", Font = Theme.Caption, ForeColor = Theme.TextMuted, BackColor = Theme.Card, TextAlign = ContentAlignment.MiddleLeft };
            int right = DpiScale.Px(Pad) + inner;
            right = AddSnoozeChips(item.Id, right, y);
            later.Bounds = new Rectangle(DpiScale.Px(Pad), y, Math.Max(0, right - DpiScale.Px(Pad) - DpiScale.Px(6)), DpiScale.Px(26));
            Controls.Add(later);
            return y + DpiScale.Px(26);
        }

        private int BuildCollapsed(int y, int inner)
        {
            y = AddHeader(y, inner, string.Format("有 {0} 件事到点了", items.Count), Theme.CardTitle, Theme.Text, false) + DpiScale.Px(6);
            List<string> titles = new List<string>();
            foreach (TodoItem item in items) titles.Add(item.Title);
            y = AddText(string.Join("、", titles.ToArray()), Theme.Caption, Theme.TextMuted, y, inner, 2) + DpiScale.Px(10);
            RoundButton expand = new RoundButton("展开逐条处理", ButtonKind.Primary) { Glyph = Theme.Icons.ChevronDown };
            expand.Bounds = new Rectangle(DpiScale.Px(Pad), y, inner, DpiScale.Px(32));
            expand.Click += delegate { SetExpanded(true); };
            Controls.Add(expand);
            return expand.Bottom;
        }

        private int BuildExpanded(int y, int inner)
        {
            y = AddHeader(y, inner, string.Format("有 {0} 件事到点了", items.Count), Theme.CardTitle, Theme.Text, true) + DpiScale.Px(8);
            int shown = Math.Min(MaxRows, items.Count);
            for (int i = 0; i < shown; i++) y = BuildRow(y, inner, items[i]) + DpiScale.Px(6);
            if (items.Count > shown)
                y = AddText(string.Format("另外 {0} 条，处理完上面的再显示。", items.Count - shown), Theme.Caption, Theme.TextMuted, y, inner) + DpiScale.Px(6);
            return y - DpiScale.Px(6);
        }

        private int BuildRow(int y, int inner, TodoItem item)
        {
            bool open = item.Id == snoozeOpenId;
            int height = DpiScale.Px(open ? 50 + 34 : 50);
            CardPanel row = new CardPanel { Bounds = new Rectangle(DpiScale.Px(Pad), y, inner, height), BackColor = Theme.Paper, BorderColor = open ? Theme.Accent : Theme.Paper };
            int buttonTop = DpiScale.Px(10);
            int x = inner - DpiScale.Px(6);
            RoundButton dismiss = new RoundButton(string.Empty, ButtonKind.Secondary) { Glyph = Theme.Icons.Close, Pill = true, AccessibleName = "知道了" };
            x -= DpiScale.Px(30);
            dismiss.Bounds = new Rectangle(x, buttonTop, DpiScale.Px(30), DpiScale.Px(30));
            dismiss.Click += delegate { Raise(DismissRequested, item.Id); };
            RoundButton later = new RoundButton(open ? "稍后 ▴" : "稍后 ▾", open ? ButtonKind.Primary : ButtonKind.Soft) { Pill = true, Font = Theme.CaptionBold };
            int laterWidth = TextWidth(later.Text, later.Font) + DpiScale.Px(18);
            x -= DpiScale.Px(6) + laterWidth;
            later.Bounds = new Rectangle(x, buttonTop, laterWidth, DpiScale.Px(30));
            later.Click += delegate
            {
                snoozeOpenId = open ? null : item.Id;
                Raise(SizeChangedByUser);
            };
            RoundButton complete = new RoundButton(string.Empty, ButtonKind.Primary) { Glyph = Theme.Icons.Check, Pill = true, AccessibleName = "完成" };
            x -= DpiScale.Px(6) + DpiScale.Px(30);
            complete.Bounds = new Rectangle(x, buttonTop, DpiScale.Px(30), DpiScale.Px(30));
            complete.Click += delegate { Raise(CompleteRequested, item.Id); };
            int textLeft = DpiScale.Px(10);
            int textWidth = Math.Max(0, x - textLeft - DpiScale.Px(6));
            // 行里只放时间本身（「今天 15:00」），「X 分钟后开始」只在单条泡里显示。
            string time = describe(item);
            int dot = time.IndexOf(" · ", StringComparison.Ordinal);
            if (dot > 0) time = time.Substring(0, dot);
            Label title = new Label { Text = item.Title, Font = Theme.BodyBold, ForeColor = Theme.Text, BackColor = Theme.Paper, AutoEllipsis = true, UseMnemonic = false, Bounds = new Rectangle(textLeft, DpiScale.Px(7), textWidth, DpiScale.Px(20)) };
            Color timeColor = TodoRowControl.TimeColor(time);
            Label when = new Label { Text = time, Font = timeColor == Theme.TextMuted ? Theme.Caption : Theme.CaptionBold, ForeColor = timeColor, BackColor = Theme.Paper, AutoEllipsis = true, Bounds = new Rectangle(textLeft, DpiScale.Px(27), textWidth, DpiScale.Px(18)) };
            row.Controls.AddRange(new Control[] { title, when, complete, later, dismiss });
            if (open)
            {
                int chipsTop = DpiScale.Px(50);
                int right = inner - DpiScale.Px(6);
                foreach (int minutes in new int[] { 60, 10 })
                {
                    string text = minutes == 10 ? "10 分钟" : "1 小时";
                    RoundButton chip = new RoundButton(text, ButtonKind.Soft) { Pill = true, Font = Theme.CaptionBold };
                    int chipWidth = TextWidth(text, chip.Font) + DpiScale.Px(24);
                    right -= chipWidth;
                    chip.Bounds = new Rectangle(right, chipsTop, chipWidth, DpiScale.Px(26));
                    int delay = minutes;
                    chip.Click += delegate { Raise(SnoozeRequested, item.Id, delay); };
                    row.Controls.Add(chip);
                    right -= DpiScale.Px(6);
                }
            }
            Controls.Add(row);
            return row.Bottom;
        }

        // 单条泡底部的两个稍后提醒按钮，从右往左排，返回最左边按钮的左边界。
        private int AddSnoozeChips(string id, int right, int y)
        {
            foreach (int minutes in new int[] { 60, 10 })
            {
                string text = minutes == 10 ? "10 分钟" : "1 小时";
                RoundButton chip = new RoundButton(text, ButtonKind.Soft) { Pill = true, Font = Theme.CaptionBold };
                int chipWidth = TextWidth(text, chip.Font) + DpiScale.Px(24);
                right -= chipWidth;
                chip.Bounds = new Rectangle(right, y, chipWidth, DpiScale.Px(26));
                int delay = minutes;
                chip.Click += delegate { Raise(SnoozeRequested, id, delay); };
                Controls.Add(chip);
                right -= DpiScale.Px(6);
            }
            return right + DpiScale.Px(6);
        }

        // 铃铛图标 + 标题；dismissAll 为 true 时右侧放「全部知道了」。
        private int AddHeader(int y, int inner, string text, Font font, Color color, bool dismissAll)
        {
            int height = Math.Max(font.Height, DpiScale.Px(22));
            Font iconFont = Theme.IconFont(10F);
            Label bell = new Label { Text = Theme.Icons.Bell, Font = iconFont, ForeColor = Theme.Accent, BackColor = Theme.Card, TextAlign = ContentAlignment.MiddleLeft, Bounds = new Rectangle(DpiScale.Px(Pad), y, DpiScale.Px(22), height) };
            bell.Disposed += delegate { iconFont.Dispose(); };
            int right = DpiScale.Px(Pad) + inner;
            if (dismissAll)
            {
                RoundButton all = new RoundButton("全部知道了", ButtonKind.Ghost) { Font = Theme.Caption };
                int allWidth = TextWidth(all.Text, all.Font) + DpiScale.Px(16);
                right -= allWidth;
                all.Bounds = new Rectangle(right, y + (height - DpiScale.Px(26)) / 2, allWidth, DpiScale.Px(26));
                all.Click += delegate { Raise(DismissAllRequested); };
                Controls.Add(all);
                right -= DpiScale.Px(6);
            }
            if (HeadlineForTest == null) HeadlineForTest = text;
            Label title = new Label { Text = text, Font = font, ForeColor = color, BackColor = Theme.Card, AutoEllipsis = true, UseMnemonic = false, TextAlign = ContentAlignment.MiddleLeft };
            title.Bounds = new Rectangle(bell.Right + DpiScale.Px(2), y, Math.Max(0, right - bell.Right - DpiScale.Px(2)), height);
            Controls.Add(bell);
            Controls.Add(title);
            return y + height;
        }

        // 自动换行的文字，最多 maxLines 行（超出部分省略）。返回下边界。
        private int AddText(string text, Font font, Color color, int y, int width, int maxLines = 3)
        {
            Size measured = TextRenderer.MeasureText(text, font, new Size(width, int.MaxValue), TextFormatFlags.WordBreak);
            int height = Math.Min(measured.Height, font.Height * maxLines) + 2;
            Label label = new Label { Text = text, Font = font, ForeColor = color, BackColor = Theme.Card, UseMnemonic = false, AutoEllipsis = true, Bounds = new Rectangle(DpiScale.Px(Pad), y, width, height) };
            Controls.Add(label);
            return label.Bottom;
        }

        private static int TextWidth(string text, Font font)
        {
            return TextRenderer.MeasureText(text, font).Width;
        }

        private void SetExpanded(bool value)
        {
            if (expanded == value) return;
            expanded = value;
            Raise(SizeChangedByUser);
        }

        // 事件都放到点击处理结束后再执行：处理方会重建内容，不能在按钮自己的 Click 里把它销毁。
        private void Raise(Action action)
        {
            if (action == null) return;
            if (IsHandleCreated) BeginInvoke(action); else action();
        }

        private void Raise(Action<string> action, string id)
        {
            if (action != null) Raise(delegate { action(id); });
        }

        private void Raise(Action<string, int> action, string id, int minutes)
        {
            if (action != null) Raise(delegate { action(id, minutes); });
        }
    }
}
