using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using Roost.Core;

namespace Roost.App
{
    internal sealed class SettingsForm : Form
    {
        private readonly ComboBox sizeBox;
        private readonly TrackBar opacityBar;
        private readonly DateTimePicker dayStart;
        private readonly RoostSettings settings;

        internal event EventHandler SettingsSaved;

        internal SettingsForm(RoostSettings settings)
        {
            this.settings = settings;
            Text = "Roost 设置";
            Font = new Font("Microsoft YaHei UI", 9F);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            ClientSize = new Size(520, 455);

            TabControl tabs = new TabControl { Dock = DockStyle.Fill };
            TabPage appearance = new TabPage("外观");
            TabPage shortcuts = new TabPage("快捷键");
            TabPage future = new TabPage("AI 与自启");
            TabPage about = new TabPage("关于");
            tabs.TabPages.AddRange(new TabPage[] { appearance, shortcuts, future, about });

            appearance.Controls.Add(new Label { Text = "宠物尺寸", Location = new Point(28, 32), AutoSize = true });
            sizeBox = new ComboBox { Location = new Point(142, 27), Width = 180, DropDownStyle = ComboBoxStyle.DropDownList };
            sizeBox.Items.AddRange(new object[] { "小（96 px）", "中（128 px）", "大（160 px）" });
            sizeBox.SelectedIndex = Math.Max(0, Math.Min(2, settings.SizeTier - 1));
            appearance.Controls.Add(sizeBox);
            appearance.Controls.Add(new Label { Text = "整体透明度（最低 35%）", Location = new Point(28, 86), AutoSize = true });
            opacityBar = new TrackBar { Location = new Point(28, 112), Width = 420, Minimum = 35, Maximum = 100, TickFrequency = 5, Value = (int)Math.Round(LayoutRules.ClampOpacity(settings.Opacity) * 100) };
            appearance.Controls.Add(opacityBar);
            appearance.Controls.Add(new Label { Text = "一天起点", Location = new Point(28, 184), AutoSize = true });
            dayStart = new DateTimePicker { Location = new Point(142, 179), Width = 120, Format = DateTimePickerFormat.Custom, CustomFormat = "HH:mm", ShowUpDown = true, Value = DateTime.Today.AddMinutes(settings.DayStartMinutes) };
            appearance.Controls.Add(dayStart);
            Button save = new Button { Text = "保存", Location = new Point(370, 330), Size = new Size(90, 36) };
            save.Click += delegate { SaveSettings(); };
            appearance.Controls.Add(save);

            shortcuts.Controls.Add(new Label { Text = "一键隐藏 / 显示：Ctrl + Alt + H", Location = new Point(28, 34), AutoSize = true });
            shortcuts.Controls.Add(new Label { Text = "快捷键修改与冲突提示将在设置完善阶段提供。", Location = new Point(28, 72), AutoSize = true, ForeColor = Color.DimGray });

            Button ai = new Button { Text = "配置 AI（M2）", Location = new Point(28, 34), Size = new Size(150, 36), Enabled = false };
            CheckBox startup = new CheckBox { Text = "开机自启（M4）", Location = new Point(28, 92), AutoSize = true, Enabled = false };
            future.Controls.Add(ai);
            future.Controls.Add(startup);
            future.Controls.Add(new Label { Text = "当前不配置模型也能完整使用本地待办。", Location = new Point(28, 142), AutoSize = true });

            about.Controls.Add(new Label { Text = "Roost M1\r\n\r\n像素猫素材：Desktop Cat\r\nCopyright (c) 2025 Administrator\r\nMIT License", Location = new Point(28, 28), Size = new Size(420, 130) });
            LinkLabel repository = new LinkLabel { Text = "https://github.com/JuMingzhen/Roost", Location = new Point(28, 172), AutoSize = true };
            repository.LinkClicked += delegate { Process.Start(repository.Text); };
            about.Controls.Add(repository);

            Controls.Add(tabs);
        }

        private void SaveSettings()
        {
            settings.SizeTier = sizeBox.SelectedIndex + 1;
            settings.Opacity = opacityBar.Value / 100.0;
            settings.DayStartMinutes = dayStart.Value.Hour * 60 + dayStart.Value.Minute;
            EventHandler handler = SettingsSaved;
            if (handler != null) handler(this, EventArgs.Empty);
            Close();
        }
    }
}

