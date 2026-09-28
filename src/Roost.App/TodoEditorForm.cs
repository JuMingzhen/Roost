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
            Font = Theme.Body;
            BackColor = Theme.Paper;
            ForeColor = Theme.Text;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(440, 470);

            Label heading = new Label { Text = Text, Location = new Point(24, 20), Size = new Size(392, 28), Font = Theme.DialogTitle, BackColor = Theme.Paper };
            Label titleLabel = LabelAt("标题（必填）", 24, 60);
            TextField titleField = new TextField { Bounds = new Rectangle(24, 82, 392, 36) };
            titleBox = titleField.Box;
            Label notesLabel = LabelAt("备注", 24, 128);
            TextField notesField = new TextField { Bounds = new Rectangle(24, 150, 392, 72) };
            notesBox = notesField.Box;
            notesBox.Multiline = true;
            notesField.LayoutBox();

            Label whenLabel = LabelAt("什么时候", 24, 234);
            hasDate = new ToggleSwitch("日期") { Location = new Point(24, 258), Size = new Size(100, 30) };
            datePicker = new DateTimePicker { Location = new Point(136, 260), Width = 160, Format = DateTimePickerFormat.Custom, CustomFormat = "yyyy-MM-dd  ddd" };
            hasTime = new ToggleSwitch("具体时刻") { Location = new Point(24, 296), Size = new Size(110, 30) };
            timePicker = new DateTimePicker { Location = new Point(136, 298), Width = 100, Format = DateTimePickerFormat.Custom, CustomFormat = "HH:mm", ShowUpDown = true };
            hasDate.CheckedChanged += delegate { UpdateDateControls(); };
            hasTime.CheckedChanged += delegate { UpdateDateControls(); };

            CardPanel starCard = new CardPanel { Location = new Point(24, 342), Size = new Size(392, 44) };
            starred = new ToggleSwitch("星标重要：排在同组最前") { Location = new Point(12, 7), Size = new Size(368, 30), BackColor = Theme.Card };
            starCard.Controls.Add(starred);

            Panel footer = new Panel { Location = new Point(0, 402), Size = new Size(440, 68), BackColor = Theme.Sidebar };
            RoundButton cancel = new RoundButton("取消", ButtonKind.Secondary) { DialogResult = DialogResult.Cancel, Location = new Point(236, 16), Size = new Size(84, 36) };
            RoundButton save = new RoundButton("保存", ButtonKind.Primary) { Location = new Point(328, 16), Size = new Size(88, 36) };
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
            footer.Controls.AddRange(new Control[] { cancel, save });

            Controls.AddRange(new Control[] { heading, titleLabel, titleField, notesLabel, notesField, whenLabel, hasDate, datePicker, hasTime, timePicker, starCard, footer });

            if (item != null)
            {
                // 删单条待办在这里（清单行上不放删除按钮，PRD 7.4）。返回 Abort 表示删除。
                RoundButton delete = new RoundButton("删除这条", ButtonKind.Danger) { Glyph = Theme.Icons.Delete, DialogResult = DialogResult.Abort, Location = new Point(24, 16), Size = new Size(108, 36) };
                footer.Controls.Add(delete);
                titleBox.Text = item.Title;
                notesBox.Text = item.Notes;
                DateTime date;
                if (TodoRules.TryGetDueDate(item, out date)) { hasDate.Checked = true; datePicker.Value = date; }
                TimeSpan time;
                if (TodoRules.TryGetDueTime(item, out time)) { hasTime.Checked = true; timePicker.Value = DateTime.Today.Add(time); }
                starred.Checked = item.IsStarred;
            }
            UpdateDateControls();
            DpiScale.Apply(this);
        }

        private void UpdateDateControls()
        {
            datePicker.Enabled = hasDate.Checked;
            hasTime.Enabled = hasDate.Checked;
            timePicker.Enabled = hasDate.Checked && hasTime.Checked;
            if (!hasDate.Checked) hasTime.Checked = false;
        }

        private Label LabelAt(string text, int x, int y)
        {
            return new Label { Text = text, Location = new Point(x, y), Size = new Size(200, 20), Font = Theme.CaptionBold, ForeColor = Theme.TextMuted, BackColor = Theme.Paper };
        }
    }
}
