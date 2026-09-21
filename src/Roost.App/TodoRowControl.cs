using System;
using System.Drawing;
using System.Windows.Forms;
using Roost.Core;

namespace Roost.App
{
    internal sealed class TodoRowControl : Panel
    {
        private readonly TodoItem item;
        private readonly Label title;
        private readonly Button complete;

        internal event Action<TodoItem> EditRequested;
        internal event Action<TodoItem> CompleteRequested;
        internal event Action<TodoItem> StarRequested;
        internal event Action<TodoItem> DeleteRequested;

        internal TodoRowControl(TodoItem item, string timeLabel)
        {
            this.item = item;
            Width = 316;
            Height = 60;
            Margin = new Padding(0, 0, 0, 6);
            BackColor = Color.FromArgb(255, 252, 247);

            complete = new Button { Text = item.IsCompleted ? "↶" : "✓", Location = new Point(6, 13), Size = new Size(34, 34), FlatStyle = FlatStyle.Flat, TabStop = false };
            complete.FlatAppearance.BorderSize = 0;
            complete.Click += delegate { Raise(CompleteRequested); };

            title = new Label { Text = Ellipsize(item.Title, 22), Location = new Point(46, 8), Size = new Size(174, 24), Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold), AutoEllipsis = true };
            title.Click += delegate { Raise(EditRequested); };
            Label time = new Label { Text = timeLabel, Location = new Point(46, 33), Size = new Size(174, 20), ForeColor = Color.DimGray, AutoEllipsis = true };
            time.Click += delegate { Raise(EditRequested); };
            Button star = new Button { Text = item.IsStarred ? "★" : "☆", Location = new Point(224, 13), Size = new Size(36, 34), FlatStyle = FlatStyle.Flat, TabStop = false, ForeColor = item.IsStarred ? Color.FromArgb(225, 150, 20) : Color.Gray };
            star.FlatAppearance.BorderSize = 0;
            star.Click += delegate { Raise(StarRequested); };
            Button delete = new Button { Text = "×", Location = new Point(270, 13), Size = new Size(36, 34), FlatStyle = FlatStyle.Flat, TabStop = false, ForeColor = Color.Firebrick };
            delete.FlatAppearance.BorderSize = 0;
            delete.Click += delegate { Raise(DeleteRequested); };
            Controls.AddRange(new Control[] { complete, title, time, star, delete });
            if (item.IsCompleted) MarkCompleted();
        }

        internal void MarkCompleted()
        {
            title.Font = new Font(title.Font, FontStyle.Strikeout);
            title.ForeColor = Color.Gray;
            complete.Text = "↶";
        }

        private void Raise(Action<TodoItem> action)
        {
            if (action != null) action(item);
        }

        private static string Ellipsize(string text, int maximum)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maximum) return text;
            return text.Substring(0, maximum - 1) + "…";
        }
    }
}

