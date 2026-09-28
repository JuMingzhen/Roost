using System;
using System.Drawing;
using System.Windows.Forms;

namespace Roost.App
{
    // 宠物的气泡：白色圆角卡片（圆角和阴影由所在的独立窗口提供），右上角关闭，可带一个操作按钮。
    internal sealed class BubbleView : Panel
    {
        internal const int BubbleWidth = 250;
        private const int MaxTextHeight = 150;
        private readonly Label text;
        private readonly RoundButton close;
        private readonly RoundButton action;
        private Action actionHandler;
        // 气泡放在独立窗口里，窗口隐藏时 Visible 也会变成 false，所以另记是否正在显示。
        private bool open;

        internal event EventHandler Dismissed;

        internal BubbleView()
        {
            BackColor = Theme.Card;
            Visible = false;
            text = new Label { Location = new Point(DpiScale.Px(14), DpiScale.Px(12)), AutoSize = false, ForeColor = Theme.Text, BackColor = Theme.Card, Font = Theme.Body };
            close = new RoundButton(string.Empty, ButtonKind.Ghost) { Glyph = Theme.Icons.Close, Size = DpiScale.Px(new Size(26, 26)), TabStop = false, AccessibleName = "关闭" };
            close.Click += delegate { Dismiss(); };
            action = new RoundButton(string.Empty, ButtonKind.Soft) { Visible = false, Font = Theme.CaptionBold };
            action.Click += delegate
            {
                Action handler = actionHandler;
                Dismiss();
                if (handler != null) handler();
            };
            Controls.AddRange(new Control[] { text, close, action });
        }

        internal bool Open { get { return open; } }

        internal string MessageForTest { get { return open ? text.Text : null; } }

        internal Size Show(string message, string actionText, Action onAction)
        {
            actionHandler = onAction;
            int width = DpiScale.Px(BubbleWidth);
            int textWidth = width - DpiScale.Px(14 + 36);
            Size measured = TextRenderer.MeasureText(message, text.Font, new Size(textWidth, int.MaxValue), TextFormatFlags.WordBreak);
            text.Text = message;
            text.Size = new Size(textWidth, Math.Min(DpiScale.Px(MaxTextHeight), measured.Height + 2));
            close.Location = new Point(width - DpiScale.Px(34), DpiScale.Px(8));
            int height = text.Bottom + DpiScale.Px(12);
            // 气泡窗口还没显示时，子控件的 Visible 读出来是 false，所以用局部变量判断。
            bool hasAction = !string.IsNullOrEmpty(actionText);
            action.Visible = hasAction;
            if (hasAction)
            {
                action.Text = actionText;
                action.Size = new Size(TextRenderer.MeasureText(actionText, action.Font).Width + DpiScale.Px(28), DpiScale.Px(28));
                action.Location = new Point(DpiScale.Px(14), text.Bottom + DpiScale.Px(6));
                height = action.Bottom + DpiScale.Px(12);
            }
            open = true;
            Visible = true;
            return new Size(width, Math.Max(DpiScale.Px(40), height));
        }

        internal void Dismiss()
        {
            if (!open) return;
            open = false;
            Visible = false;
            actionHandler = null;
            EventHandler handler = Dismissed;
            if (handler != null) handler(this, EventArgs.Empty);
        }
    }
}
