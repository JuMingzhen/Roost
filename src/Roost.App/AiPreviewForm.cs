using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Roost.Core;

namespace Roost.App
{
    internal sealed class AiPreviewForm : Form
    {
        private readonly List<KeyValuePair<CheckBox, AiOperation>> choices;
        private readonly RoundButton confirm;

        internal AiPreviewForm(AiPlan plan)
        {
            choices = new List<KeyValuePair<CheckBox, AiOperation>>();
            Text = "确认 AI 的改动";
            Font = Theme.Body;
            BackColor = Theme.Paper;
            ForeColor = Theme.Text;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false;
            MinimizeBox = false;
            TopMost = true;
            ClientSize = new Size(520, 480);

            List<AiOperation> deletes = new List<AiOperation>();
            List<AiOperation> others = new List<AiOperation>();
            foreach (AiOperation operation in plan.Operations)
            {
                if (operation.IsValid && operation.Kind == AiOperationKind.Delete) deletes.Add(operation);
                else others.Add(operation);
            }

            Label headline = new Label { Text = plan.Headline(), Location = new Point(24, 20), Size = new Size(472, 28), Font = Theme.DialogTitle, BackColor = Theme.Paper, AutoEllipsis = true };
            Label hint = new Label
            {
                Text = deletes.Count > 0 ? "包含删除，请仔细核对。去掉勾选的改动不会生效。" : "点「确认」之前，不会改动任何待办。去掉勾选可以跳过某一条。",
                Location = new Point(24, 50),
                Size = new Size(472, 20),
                Font = Theme.Caption,
                ForeColor = deletes.Count > 0 ? Theme.Danger : Theme.TextMuted,
                BackColor = Theme.Paper
            };
            FlowLayoutPanel list = new FlowLayoutPanel
            {
                Location = new Point(24, 80),
                Size = new Size(472, 320),
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                BackColor = Theme.Paper
            };

            // 删除排在最上面，并单独成组（PRD 8.4）。
            if (deletes.Count > 0) list.Controls.Add(Section(string.Format("删除 {0} 条", deletes.Count), Theme.Danger));
            foreach (AiOperation operation in deletes) list.Controls.Add(CreateRow(operation));
            if (others.Count > 0) list.Controls.Add(Section(deletes.Count > 0 ? string.Format("其他改动 {0} 条", others.Count) : string.Format("改动 {0} 条", others.Count), Theme.TextMuted));
            foreach (AiOperation operation in others) list.Controls.Add(CreateRow(operation));

            Panel footer = new Panel { Location = new Point(0, 412), Size = new Size(520, 68), BackColor = Theme.Sidebar };
            RoundButton cancel = new RoundButton("取消", ButtonKind.Secondary) { DialogResult = DialogResult.Cancel, Location = new Point(300, 16), Size = new Size(88, 36) };
            confirm = new RoundButton("确认", ButtonKind.Primary) { DialogResult = DialogResult.OK, Location = new Point(396, 16), Size = new Size(100, 36) };
            footer.Controls.AddRange(new Control[] { cancel, confirm });
            CancelButton = cancel;
            Controls.AddRange(new Control[] { headline, hint, list, footer });
            UpdateConfirm();
            DpiScale.Apply(this);
        }

        private static Label Section(string text, Color color)
        {
            return new Label { Text = text, Size = new Size(440, 24), Margin = new Padding(0, 6, 0, 2), Font = Theme.CaptionBold, ForeColor = color, BackColor = Theme.Paper, TextAlign = ContentAlignment.BottomLeft };
        }

        internal List<string> RowTextsForTest
        {
            get
            {
                List<string> texts = new List<string>();
                foreach (KeyValuePair<CheckBox, AiOperation> choice in choices) texts.Add(choice.Key.Text);
                return texts;
            }
        }

        internal void UncheckForTest(int index)
        {
            choices[index].Key.Checked = false;
        }

        internal List<AiOperation> SelectedOperations
        {
            get
            {
                List<AiOperation> selected = new List<AiOperation>();
                foreach (KeyValuePair<CheckBox, AiOperation> choice in choices)
                    if (choice.Key.Checked && choice.Value.IsValid) selected.Add(choice.Value);
                return selected;
            }
        }

        private Control CreateRow(AiOperation operation)
        {
            bool delete = operation.IsValid && operation.Kind == AiOperationKind.Delete;
            Color fill = delete ? Theme.DangerSoft : (operation.IsValid ? Theme.Card : Theme.Sidebar);
            CardPanel row = new CardPanel { Width = 440, Margin = new Padding(0, 0, 0, 8), BackColor = fill, BorderColor = delete ? Theme.DangerLine : Theme.Line };
            CheckBox box = new CheckBox
            {
                Text = operation.IsValid ? operation.Summary : "无效：" + operation.Summary,
                Checked = operation.IsValid,
                Enabled = operation.IsValid,
                Location = new Point(14, 10),
                Size = new Size(414, 24),
                AutoEllipsis = true,
                BackColor = fill,
                ForeColor = delete ? Theme.DangerText : (operation.IsValid ? Theme.Text : Theme.TextFaint),
                Font = delete || operation.IsValid ? Theme.BodyBold : Theme.Body
            };
            box.CheckedChanged += delegate { UpdateConfirm(); };
            row.Controls.Add(box);
            choices.Add(new KeyValuePair<CheckBox, AiOperation>(box, operation));
            int height = 44;

            string note = null;
            Color noteColor = Theme.TextMuted;
            if (!operation.IsValid) note = operation.InvalidReason + " 这条不会生效。";
            else if (delete) note = "确认后可以在 10 秒内撤销。";
            else if (operation.PastTimeWarning) { note = "⚠ 这个时间已经过去了，确认前请核对。"; noteColor = Theme.Warning; }
            if (note != null)
            {
                row.Controls.Add(new Label { Text = note, Location = new Point(34, 36), Size = new Size(394, 20), Font = Theme.Caption, ForeColor = delete ? Theme.DangerText : noteColor, BackColor = fill, AutoEllipsis = true });
                height = 64;
            }
            row.Height = height;
            new ToolTip().SetToolTip(box, box.Text);
            return row;
        }

        private void UpdateConfirm()
        {
            confirm.Enabled = SelectedOperations.Count > 0;
        }
    }
}
