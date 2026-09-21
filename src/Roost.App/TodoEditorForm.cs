using System;
using System.Drawing;
using System.Windows.Forms;
using Roost.Core;

namespace Roost.App
{
    internal sealed class TodoEditorForm : Form
    {
        private readonly TextBox titleBox;
        private readonly TextBox notesBox;
        private readonly CheckBox hasDate;
        private readonly DateTimePicker datePicker;
        private readonly CheckBox hasTime;
        private readonly DateTimePicker timePicker;
        private readonly CheckBox starred;

        internal string TodoTitle { get { return titleBox.Text.Trim(); } }
        internal string TodoNotes { get { return notesBox.Text.Trim(); } }
        internal string TodoDate { get { return hasDate.Checked ? datePicker.Value.ToString("yyyy-MM-dd") : null; } }
        internal string TodoTime { get { return hasDate.Checked && hasTime.Checked ? timePicker.Value.ToString("HH:mm") : null; } }
        internal bool TodoStarred { get { return starred.Checked; } }

        internal TodoEditorForm(TodoItem item)
        {
            Text = item == null ? "新建待办" : "编辑待办";
            Font = new Font("Microsoft YaHei UI", 9F);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(420, 385);

            Label titleLabel = LabelAt("标题 *", 22, 18, 100);
            titleBox = TextAt(22, 42, 376, 28);
            Label notesLabel = LabelAt("备注", 22, 82, 100);
            notesBox = TextAt(22, 106, 376, 86);
            notesBox.Multiline = true;

            hasDate = new CheckBox { Text = "日期", Location = new Point(22, 212), AutoSize = true };
            datePicker = new DateTimePicker { Location = new Point(92, 207), Width = 150, Format = DateTimePickerFormat.Custom, CustomFormat = "yyyy-MM-dd" };
            hasTime = new CheckBox { Text = "具体时刻", Location = new Point(22, 252), AutoSize = true };
            timePicker = new DateTimePicker { Location = new Point(120, 247), Width = 122, Format = DateTimePickerFormat.Custom, CustomFormat = "HH:mm", ShowUpDown = true };
            starred = new CheckBox { Text = "星标重要", Location = new Point(280, 212), AutoSize = true };
            hasDate.CheckedChanged += delegate { UpdateDateControls(); };
            hasTime.CheckedChanged += delegate { UpdateDateControls(); };

            Button cancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Location = new Point(232, 326), Size = new Size(78, 34) };
            Button save = new Button { Text = "保存", Location = new Point(320, 326), Size = new Size(78, 34) };
            save.Click += delegate
            {
                if (TodoTitle.Length == 0)
                {
                    MessageBox.Show(this, "标题不能为空。", "Roost", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    titleBox.Focus();
                    return;
                }
                DialogResult = DialogResult.OK;
            };
            AcceptButton = save;
            CancelButton = cancel;

            Controls.AddRange(new Control[] { titleLabel, titleBox, notesLabel, notesBox, hasDate, datePicker, hasTime, timePicker, starred, cancel, save });

            if (item != null)
            {
                titleBox.Text = item.Title;
                notesBox.Text = item.Notes;
                DateTime date;
                if (TodoRules.TryGetDueDate(item, out date)) { hasDate.Checked = true; datePicker.Value = date; }
                TimeSpan time;
                if (TodoRules.TryGetDueTime(item, out time)) { hasTime.Checked = true; timePicker.Value = DateTime.Today.Add(time); }
                starred.Checked = item.IsStarred;
            }
            UpdateDateControls();
        }

        private void UpdateDateControls()
        {
            datePicker.Enabled = hasDate.Checked;
            hasTime.Enabled = hasDate.Checked;
            timePicker.Enabled = hasDate.Checked && hasTime.Checked;
            if (!hasDate.Checked) hasTime.Checked = false;
        }

        private Label LabelAt(string text, int x, int y, int width)
        {
            return new Label { Text = text, Location = new Point(x, y), Width = width, Height = 20 };
        }

        private TextBox TextAt(int x, int y, int width, int height)
        {
            return new TextBox { Location = new Point(x, y), Size = new Size(width, height) };
        }
    }
}

