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
            text = new Label { Location = new Point(10, 8), AutoSize = false, ForeColor = Color.FromArgb(60, 50, 30) };
            close = new Button { Text = "×", Size = new Size(26, 24), FlatStyle = FlatStyle.Flat, TabStop = false };
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
            int textWidth = BubbleWidth - 10 - 34;
            Size measured = TextRenderer.MeasureText(message, Font, new Size(textWidth, int.MaxValue), TextFormatFlags.WordBreak);
            text.Text = message;
            text.Size = new Size(textWidth, Math.Min(MaxTextHeight, measured.Height + 2));
            close.Location = new Point(BubbleWidth - 32, 4);
            int height = text.Bottom + 8;
            action.Visible = !string.IsNullOrEmpty(actionText);
            if (action.Visible)
            {
                action.Text = actionText;
                action.Location = new Point(10, text.Bottom + 2);
                height = action.Bottom + 8;
            }
            Visible = true;
            return new Size(BubbleWidth, Math.Max(40, height));
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
