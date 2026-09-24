using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
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
        private readonly TabControl tabs;
        private readonly TabPage aiPage;
        private readonly ComboBox presetBox;
        private readonly Label presetDescription;
        private readonly LinkLabel keyLink;
        private readonly TextBox urlBox;
        private readonly TextBox modelBox;
        private readonly TextBox keyBox;
        private readonly Label keyHint;
        private readonly Button checkButton;
        private readonly Button clearButton;
        private readonly Button selfTestButton;
        private readonly Label aiStatus;
        private CancellationTokenSource checking;

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

            tabs = new TabControl { Dock = DockStyle.Fill };
            TabPage appearance = new TabPage("外观");
            TabPage shortcuts = new TabPage("快捷键");
            aiPage = new TabPage("AI 与自启");
            TabPage about = new TabPage("关于");
            tabs.TabPages.AddRange(new TabPage[] { appearance, shortcuts, aiPage, about });

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
            shortcuts.Controls.Add(new Label { Text = "唤出「跟宠物说」：Ctrl + Alt + 空格", Location = new Point(28, 66), AutoSize = true });
            shortcuts.Controls.Add(new Label { Text = "快捷键修改与冲突提示将在设置完善阶段提供。", Location = new Point(28, 104), AutoSize = true, ForeColor = Color.DimGray });

            aiPage.Controls.Add(new Label { Text = "快捷预设", Location = new Point(24, 22), AutoSize = true });
            presetBox = new ComboBox { Location = new Point(110, 18), Width = 200, DropDownStyle = ComboBoxStyle.DropDownList };
            foreach (AiPreset preset in AiPresets.All) presetBox.Items.Add(preset.Name);
            presetBox.Items.Add("自定义（OpenAI 兼容）");
            aiPage.Controls.Add(presetBox);
            presetDescription = new Label { Location = new Point(24, 50), Size = new Size(460, 20), ForeColor = Color.DimGray };
            keyLink = new LinkLabel { Text = "去申请 key", Location = new Point(24, 72), AutoSize = true };
            keyLink.LinkClicked += delegate
            {
                AiPreset preset = SelectedPreset();
                if (preset != null) Process.Start(preset.KeyUrl);
            };
            aiPage.Controls.Add(presetDescription);
            aiPage.Controls.Add(keyLink);
            aiPage.Controls.Add(new Label { Text = "服务地址", Location = new Point(24, 106), AutoSize = true });
            urlBox = new TextBox { Location = new Point(110, 102), Width = 370 };
            aiPage.Controls.Add(urlBox);
            aiPage.Controls.Add(new Label { Text = "模型名", Location = new Point(24, 142), AutoSize = true });
            modelBox = new TextBox { Location = new Point(110, 138), Width = 370 };
            aiPage.Controls.Add(modelBox);
            aiPage.Controls.Add(new Label { Text = "API Key", Location = new Point(24, 178), AutoSize = true });
            keyBox = new TextBox { Location = new Point(110, 174), Width = 370, UseSystemPasswordChar = true };
            aiPage.Controls.Add(keyBox);
            keyHint = new Label { Location = new Point(110, 200), Size = new Size(380, 20), ForeColor = Color.DimGray };
            aiPage.Controls.Add(keyHint);
            checkButton = new Button { Text = "保存并检测", Location = new Point(110, 228), Size = new Size(120, 34) };
            checkButton.Click += delegate { SaveAi(); };
            clearButton = new Button { Text = "清除 AI 配置", Location = new Point(240, 228), Size = new Size(120, 34) };
            clearButton.Click += delegate { ClearAi(); };
            selfTestButton = new Button { Text = "用测试题检验…", Location = new Point(370, 228), Size = new Size(120, 34) };
            selfTestButton.Click += delegate { OpenSelfTest(); };
            aiPage.Controls.Add(checkButton);
            aiPage.Controls.Add(clearButton);
            aiPage.Controls.Add(selfTestButton);
            aiStatus = new Label { Location = new Point(24, 272), Size = new Size(460, 44) };
            aiPage.Controls.Add(aiStatus);
            aiPage.Controls.Add(new Label { Text = "key 只保存在 Windows 凭据管理器里。不配置模型也能完整使用本地待办。", Location = new Point(24, 320), Size = new Size(460, 20), ForeColor = Color.DimGray });
            CheckBox startup = new CheckBox { Text = "开机自启（M4）", Location = new Point(24, 356), AutoSize = true, Enabled = false };
            aiPage.Controls.Add(startup);

            AiPreset current = AiPresets.Find(settings.AiPresetId);
            presetBox.SelectedIndex = current != null ? Array.IndexOf(AiPresets.All, current) : (settings.AiConfigured ? AiPresets.All.Length : 0);
            if (settings.AiConfigured)
            {
                urlBox.Text = settings.AiBaseUrl;
                modelBox.Text = settings.AiModel;
            }
            else
            {
                FillPreset();
            }
            UpdatePresetInfo();
            presetBox.SelectedIndexChanged += delegate { FillPreset(); UpdatePresetInfo(); };
            UpdateKeyHint();
            FormClosed += delegate { if (checking != null) checking.Cancel(); };

            about.Controls.Add(new Label { Text = "Roost M1\r\n\r\n像素猫素材：Desktop Cat\r\nCopyright (c) 2025 Administrator\r\nMIT License", Location = new Point(28, 28), Size = new Size(420, 130) });
            LinkLabel repository = new LinkLabel { Text = "https://github.com/JuMingzhen/Roost", Location = new Point(28, 172), AutoSize = true };
            repository.LinkClicked += delegate { Process.Start(repository.Text); };
            about.Controls.Add(repository);

            Controls.Add(tabs);
            DpiScale.Apply(this);
        }

        internal void ShowAiTab()
        {
            tabs.SelectedTab = aiPage;
        }

        private AiPreset SelectedPreset()
        {
            int index = presetBox.SelectedIndex;
            return index >= 0 && index < AiPresets.All.Length ? AiPresets.All[index] : null;
        }

        private void FillPreset()
        {
            AiPreset preset = SelectedPreset();
            if (preset == null) return;
            urlBox.Text = preset.BaseUrl;
            modelBox.Text = preset.Model;
        }

        private void UpdatePresetInfo()
        {
            AiPreset preset = SelectedPreset();
            presetDescription.Text = preset == null ? "填写任意 OpenAI 兼容服务的地址、key 和模型名。" : preset.Description;
            keyLink.Visible = preset != null;
        }

        private void UpdateKeyHint()
        {
            string saved = null;
            try { saved = CredentialStore.Read(CredentialStore.ApiKeyTarget); }
            catch (System.ComponentModel.Win32Exception) { }
            keyHint.Text = string.IsNullOrEmpty(saved) ? "尚未保存 key。" : "已保存 key；留空则继续使用已保存的 key。";
            selfTestButton.Enabled = settings.AiConfigured && !string.IsNullOrEmpty(saved);
        }

        private void OpenSelfTest()
        {
            string key = null;
            try { key = CredentialStore.Read(CredentialStore.ApiKeyTarget); }
            catch (System.ComponentModel.Win32Exception) { }
            if (!settings.AiConfigured || string.IsNullOrEmpty(key))
            {
                ShowAiStatus("请先保存模型配置，再用测试题检验。", Color.Firebrick);
                return;
            }
            AiEndpoint endpoint = new AiEndpoint { BaseUrl = settings.AiBaseUrl, Model = settings.AiModel, ApiKey = key };
            AiSelfTestForm test;
            try
            {
                test = new AiSelfTestForm(endpoint, AiSelfTestForm.SuitePath);
            }
            catch (IOException)
            {
                ShowAiStatus("找不到测试题文件，请确认应用文件完整。", Color.Firebrick);
                return;
            }
            using (test) test.ShowDialog(this);
        }

        private async void SaveAi()
        {
            string url = urlBox.Text.Trim();
            string model = modelBox.Text.Trim();
            string key = keyBox.Text.Trim();
            string savedKey = null;
            try { savedKey = CredentialStore.Read(CredentialStore.ApiKeyTarget); }
            catch (System.ComponentModel.Win32Exception) { }
            string effectiveKey = key.Length > 0 ? key : savedKey;
            if (url.Length == 0 || model.Length == 0 || string.IsNullOrEmpty(effectiveKey))
            {
                ShowAiStatus("请填写服务地址、模型名和 API Key。", Color.Firebrick);
                return;
            }
            if (!settings.AiPrivacyAcknowledged)
            {
                AiPreset preset = SelectedPreset();
                DialogResult consent = MessageBox.Show(this,
                    "改计划时，你的待办内容会发送给你选择的模型厂商" + (preset == null ? string.Empty : "（" + preset.Name + "）") + "。\r\n\r\n" +
                    "Roost 不经手、不保存这些数据；费用由你的模型账户承担。\r\n\r\n确定保存吗？",
                    "隐私告知", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                if (consent != DialogResult.Yes) return;
            }

            checkButton.Enabled = false;
            clearButton.Enabled = false;
            ShowAiStatus("正在检测服务地址、key 和模型名……", Color.DimGray);
            checking = new CancellationTokenSource();
            AiException failure = null;
            try
            {
                await new AiClient().CheckAsync(new AiEndpoint { BaseUrl = url, Model = model, ApiKey = effectiveKey }, checking.Token);
            }
            catch (AiException exception)
            {
                failure = exception;
            }
            if (IsDisposed) return;
            checkButton.Enabled = true;
            clearButton.Enabled = true;
            if (failure != null && failure.Kind == AiFailureKind.Cancelled) return;
            if (failure != null && !failure.Retryable)
            {
                ShowAiStatus("✗ 检测未通过，配置没有保存：" + failure.Message, Color.Firebrick);
                return;
            }
            if (failure != null)
            {
                DialogResult keep = MessageBox.Show(this, "暂时无法完成检测：" + failure.Message + "\r\n\r\n仍要保存这份配置吗？", "Roost", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (keep != DialogResult.Yes)
                {
                    ShowAiStatus("配置没有保存。", Color.DimGray);
                    return;
                }
            }

            try
            {
                if (key.Length > 0) CredentialStore.Write(CredentialStore.ApiKeyTarget, key);
            }
            catch (System.ComponentModel.Win32Exception)
            {
                ShowAiStatus("✗ 无法把 key 写入 Windows 凭据管理器，配置没有保存。", Color.Firebrick);
                return;
            }
            AiPreset chosen = SelectedPreset();
            settings.AiPresetId = chosen == null ? AiPresets.CustomId : chosen.Id;
            settings.AiBaseUrl = url;
            settings.AiModel = model;
            settings.AiPrivacyAcknowledged = true;
            keyBox.Text = string.Empty;
            UpdateKeyHint();
            ShowAiStatus(failure == null ? "✓ 检测通过，已保存。可以点「用测试题检验」看看它改计划靠不靠谱。" : "已保存，但尚未通过检测。", failure == null ? Color.SeaGreen : Color.DarkOrange);
            EventHandler handler = SettingsSaved;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        private void ClearAi()
        {
            if (MessageBox.Show(this, "清除服务地址、模型名和已保存的 key 吗？", "Roost", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            try { CredentialStore.Delete(CredentialStore.ApiKeyTarget); }
            catch (System.ComponentModel.Win32Exception) { }
            settings.AiBaseUrl = null;
            settings.AiModel = null;
            settings.AiPresetId = null;
            keyBox.Text = string.Empty;
            UpdateKeyHint();
            ShowAiStatus("已清除 AI 配置。", Color.DimGray);
            EventHandler handler = SettingsSaved;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        private void ShowAiStatus(string message, Color color)
        {
            aiStatus.Text = message;
            aiStatus.ForeColor = color;
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

