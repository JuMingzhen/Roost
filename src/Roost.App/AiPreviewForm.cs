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
        private readonly Button confirm;

        internal AiPreviewForm(AiPlan plan)
        {
            choices = new List<KeyValuePair<CheckBox, AiOperation>>();
            Text = "确认 AI 的改动";
            Font = new Font("Microsoft YaHei UI", 9F);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false;
            MinimizeBox = false;
            TopMost = true;
            ClientSize = new Size(520, 420);

            bool hasDelete = false;
            foreach (AiOperation operation in plan.Operations)
                if (operation.IsValid && operation.Kind == AiOperationKind.Delete) hasDelete = true;

            Label headline = new Label { Text = plan.Headline(), Location = new Point(18, 14), Size = new Size(484, 24), Font = new Font(Font.FontFamily, 10.5F, FontStyle.Bold) };
            Label hint = new Label
            {
                Text = hasDelete ? "⚠ 包含删除操作，请仔细核对。去掉勾选的改动不会生效。" : "去掉勾选的改动不会生效。确认之前，清单不会有任何变化。",
                Location = new Point(18, 40),
                Size = new Size(484, 22),
                ForeColor = hasDelete ? Color.Firebrick : Color.DimGray
            };
            FlowLayoutPanel list = new FlowLayoutPanel
            {
                Location = new Point(18, 68),
                Size = new Size(484, 290),
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                BorderStyle = BorderStyle.FixedSingle
            };

            List<AiOperation> ordered = new List<AiOperation>();
            foreach (AiOperation operation in plan.Operations)
                if (operation.IsValid && operation.Kind == AiOperationKind.Delete) ordered.Add(operation);
            foreach (AiOperation operation in plan.Operations)
                if (!(operation.IsValid && operation.Kind == AiOperationKind.Delete)) ordered.Add(operation);
            foreach (AiOperation operation in ordered) list.Controls.Add(CreateRow(operation));

            Button cancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Location = new Point(316, 372), Size = new Size(88, 34) };
            confirm = new Button { Text = "确认", DialogResult = DialogResult.OK, Location = new Point(414, 372), Size = new Size(88, 34) };
            CancelButton = cancel;
            Controls.AddRange(new Control[] { headline, hint, list, cancel, confirm });
            UpdateConfirm();
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
            Panel row = new Panel { Width = 456, Margin = new Padding(4, 4, 4, 2) };
            if (delete) row.BackColor = Color.FromArgb(253, 231, 231);
            CheckBox box = new CheckBox
            {
                Text = operation.IsValid ? operation.Summary : "无效：" + operation.Summary,
                Checked = operation.IsValid,
                Enabled = operation.IsValid,
                Location = new Point(6, 4),
                Size = new Size(444, 24),
                AutoEllipsis = true,
                ForeColor = delete ? Color.Firebrick : (operation.IsValid ? Color.Black : Color.Gray),
                Font = delete ? new Font(Font, FontStyle.Bold) : Font
            };
            box.CheckedChanged += delegate { UpdateConfirm(); };
            row.Controls.Add(box);
            choices.Add(new KeyValuePair<CheckBox, AiOperation>(box, operation));
            int height = 30;

            string note = null;
            Color noteColor = Color.DimGray;
            if (!operation.IsValid) note = operation.InvalidReason + " 这条不会生效。";
            else if (operation.PastTimeWarning) { note = "⚠ 这个时间已经过去了，确认前请核对。"; noteColor = Color.DarkOrange; }
            if (note != null)
            {
                row.Controls.Add(new Label { Text = note, Location = new Point(26, 28), Size = new Size(420, 20), ForeColor = noteColor });
                height = 50;
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
