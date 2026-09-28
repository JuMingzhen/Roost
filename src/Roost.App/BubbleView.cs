using System;
using System.Drawing;
using System.Windows.Forms;

namespace Roost.App
{
    internal sealed class BubbleView : Panel
    {
        internal const int BubbleWidth = 250;
        private const int MaxTextHeight = 150;
        private readonly Label text;
        private readonly Button close;
        private readonly LinkLabel action;
        private Action actionHandler;

        internal event EventHandler Dismissed;

        internal BubbleView()
        {
            BackColor = Color.FromArgb(255, 251, 230);
            BorderStyle = BorderStyle.FixedSingle;
            Visible = false;
            text = new Label { Location = new Point(DpiScale.Px(10), DpiScale.Px(8)), AutoSize = false, ForeColor = Color.FromArgb(60, 50, 30) };
            close = new Button { Text = "×", Size = DpiScale.Px(new Size(26, 24)), FlatStyle = FlatStyle.Flat, TabStop = false };
            close.FlatAppearance.BorderSize = 0;
            close.Click += delegate { Dismiss(); };
            action = new LinkLabel { AutoSize = true, Visible = false };
            action.LinkClicked += delegate
            {
                Action handler = actionHandler;
                Dismiss();
                if (handler != null) handler();
            };
            Controls.AddRange(new Control[] { text, close, action });
        }

        internal string MessageForTest { get { return Visible ? text.Text : null; } }

        internal Size Show(string message, string actionText, Action onAction)
        {
            actionHandler = onAction;
            int width = DpiScale.Px(BubbleWidth);
            int textWidth = width - DpiScale.Px(10 + 34);
            Size measured = TextRenderer.MeasureText(message, Font, new Size(textWidth, int.MaxValue), TextFormatFlags.WordBreak);
            text.Text = message;
            text.Size = new Size(textWidth, Math.Min(DpiScale.Px(MaxTextHeight), measured.Height + 2));
            close.Location = new Point(width - DpiScale.Px(32), DpiScale.Px(4));
            int height = text.Bottom + DpiScale.Px(8);
            action.Visible = !string.IsNullOrEmpty(actionText);
            if (action.Visible)
            {
                action.Text = actionText;
                action.Location = new Point(DpiScale.Px(10), text.Bottom + DpiScale.Px(2));
                height = action.Bottom + DpiScale.Px(8);
            }
            Visible = true;
            return new Size(width, Math.Max(DpiScale.Px(40), height));
        }

        internal void Dismiss()
        {
            if (!Visible) return;
            Visible = false;
            actionHandler = null;
            EventHandler handler = Dismissed;
            if (handler != null) handler(this, EventArgs.Empty);
        }
    }
}
