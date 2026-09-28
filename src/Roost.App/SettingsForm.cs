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
        private readonly CheckBox showKey;
        private readonly Label keyStatus;
        private readonly Button changeKeyButton;
        private readonly Button deleteKeyButton;
        private readonly Button cancelKeyButton;
        private readonly Label checkInfo;
        private readonly Button checkButton;
        private readonly LinkLabel clearLink;
        private readonly Button selfTestButton;
        private bool editingKey;
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
            keyStatus = new Label { Location = new Point(110, 178), Size = new Size(200, 22) };
            changeKeyButton = new Button { Text = "更换 key", Location = new Point(314, 172), Size = new Size(80, 30) };
            changeKeyButton.Click += delegate { editingKey = true; UpdateKeyState(); keyBox.Focus(); };
            deleteKeyButton = new Button { Text = "删除 key", Location = new Point(400, 172), Size = new Size(80, 30) };
            deleteKeyButton.Click += delegate { DeleteKey(); };
            keyBox = new TextBox { Location = new Point(110, 174), Width = 236, UseSystemPasswordChar = true };
            showKey = new CheckBox { Text = "显示", Location = new Point(352, 177), AutoSize = true };
            showKey.CheckedChanged += delegate { keyBox.UseSystemPasswordChar = !showKey.Checked; };
            cancelKeyButton = new Button { Text = "取消", Location = new Point(420, 172), Size = new Size(60, 30) };
            cancelKeyButton.Click += delegate { editingKey = false; UpdateKeyState(); };
            aiPage.Controls.AddRange(new Control[] { keyStatus, changeKeyButton, deleteKeyButton, keyBox, showKey, cancelKeyButton });
            checkInfo = new Label { Location = new Point(110, 208), Size = new Size(380, 20), ForeColor = Color.DimGray };
            aiPage.Controls.Add(checkInfo);
            checkButton = new Button { Text = "保存并检测", Location = new Point(110, 236), Size = new Size(120, 34) };
            checkButton.Click += delegate { SaveAi(); };
            selfTestButton = new Button { Text = "用测试题检验…", Location = new Point(240, 236), Size = new Size(130, 34) };
            selfTestButton.Click += delegate { OpenSelfTest(); };
            aiPage.Controls.Add(checkButton);
            aiPage.Controls.Add(selfTestButton);
            aiStatus = new Label { Location = new Point(24, 278), Size = new Size(460, 44) };
            aiPage.Controls.Add(aiStatus);
            aiPage.Controls.Add(new Label { Text = "key 按厂商保存在 Windows 凭据管理器。不配置模型也能用本地待办。", Location = new Point(24, 326), Size = new Size(460, 20), ForeColor = Color.DimGray });
            clearLink = new LinkLabel { Text = "清除全部 AI 配置", Location = new Point(24, 352), AutoSize = true, LinkColor = Color.DimGray };
            clearLink.LinkClicked += delegate { ClearAi(); };
            aiPage.Controls.Add(clearLink);
            CheckBox startup = new CheckBox { Text = "开机自启（M4）", Location = new Point(24, 378), AutoSize = true, Enabled = false };
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
            presetBox.SelectedIndexChanged += delegate { FillPreset(); UpdatePresetInfo(); editingKey = false; UpdateKeyState(); };
            urlBox.TextChanged += delegate { if (SelectedPreset() == null) UpdateKeyState(); };
            modelBox.TextChanged += delegate { UpdateCheckInfo(); };
            UpdateKeyState();
            FormClosed += delegate { if (checking != null) checking.Cancel(); };

            about.Controls.Add(new Label { Text = "Roost M1\r\n\r\n像素猫素材：Desktop Cat\r\nCopyright (c) 2025 Administrator\r\nMIT License", Location = new Point(28, 28), Size = new Size(420, 130) });
            LinkLabel repository = new LinkLabel { Text = "https://github.com/JuMingzhen/Roost", Location = new Point(28, 172), AutoSize = true };
            repository.LinkClicked += delegate { Process.Start(repository.Text); };
            about.Controls.Add(repository);

            Controls.Add(tabs);
            DpiScale.Apply(this);
        }

        internal void ShowKeyEditorForTest()
        {
            editingKey = true;
            UpdateKeyState();
        }

        internal string KeyStatusForTest { get { return keyStatus.Visible ? keyStatus.Text : null; } }

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

        // 当前选中的厂商（自定义则按服务地址）对应的 key 存放位置。
        private string SelectedKeyTarget()
        {
            AiPreset preset = SelectedPreset();
            return CredentialStore.ApiKeyTargetFor(preset == null ? AiPresets.CustomId : preset.Id, urlBox.Text);
        }

        private static string ReadKey(string target)
        {
            if (target == null) return null;
            try { return CredentialStore.Read(target); }
            catch (System.ComponentModel.Win32Exception) { return null; }
        }

        // 已保存 key 时显示掩码和「更换 / 删除」；没有 key 或点了「更换」时显示输入框。
        private void UpdateKeyState()
        {
            string saved = ReadKey(SelectedKeyTarget());
            bool hasKey = !string.IsNullOrEmpty(saved);
            bool edit = !hasKey || editingKey;
            keyStatus.Text = hasKey ? "已保存：" + CredentialStore.MaskKey(saved) : string.Empty;
            keyStatus.Visible = !edit;
            changeKeyButton.Visible = !edit;
            deleteKeyButton.Visible = !edit;
            keyBox.Visible = edit;
            showKey.Visible = edit;
            cancelKeyButton.Visible = edit && hasKey;
            if (!edit)
            {
                keyBox.Text = string.Empty;
                showKey.Checked = false;
            }
            UpdateCheckInfo();
            selfTestButton.Enabled = settings.AiConfigured && !string.IsNullOrEmpty(ReadKey(CredentialStore.ApiKeyTargetFor(settings)));
        }

        private void UpdateCheckInfo()
        {
            string target = SelectedKeyTarget();
            bool known = target != null && target == settings.AiCheckTarget && modelBox.Text.Trim() == settings.AiCheckModel;
            DateTime checkedAt;
            if (known && DateTime.TryParse(settings.AiCheckUtc, null, System.Globalization.DateTimeStyles.RoundtripKind, out checkedAt))
            {
                checkInfo.Text = string.Format("上次检测：{0} {1}", checkedAt.ToLocalTime().ToString("M月d日 HH:mm"), settings.AiCheckPassed ? "通过" : "未完成（网络原因）");
                checkInfo.ForeColor = settings.AiCheckPassed ? Color.SeaGreen : Color.DarkOrange;
            }
            else
            {
                checkInfo.Text = editingKey || string.IsNullOrEmpty(ReadKey(target)) ? "填好后点「保存并检测」。" : "这个组合还没有检测过。";
                checkInfo.ForeColor = Color.DimGray;
            }
        }

        private void DeleteKey()
        {
            AiPreset preset = SelectedPreset();
            string name = preset == null ? "这个服务" : preset.Name;
            if (MessageBox.Show(this, "删除 " + name + " 的 API key 吗？服务地址和模型名会保留。", "Roost", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            string target = SelectedKeyTarget();
            try { if (target != null) CredentialStore.Delete(target); }
            catch (System.ComponentModel.Win32Exception)
            {
                ShowAiStatus("✗ 无法从 Windows 凭据管理器删除 key。", Color.Firebrick);
                return;
            }
            if (target == settings.AiCheckTarget) settings.AiCheckTarget = null;
            editingKey = false;
            UpdateKeyState();
            ShowAiStatus("已删除 " + name + " 的 key。", Color.DimGray);
            EventHandler handler = SettingsSaved;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        private void OpenSelfTest()
        {
            string key = ReadKey(CredentialStore.ApiKeyTargetFor(settings));
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
            string keyTarget = SelectedKeyTarget();
            string savedKey = ReadKey(keyTarget);
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
            clearLink.Enabled = false;
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
            clearLink.Enabled = true;
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
                if (key.Length > 0) CredentialStore.Write(keyTarget, key);
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
            settings.AiCheckTarget = keyTarget;
            settings.AiCheckModel = model;
            settings.AiCheckPassed = failure == null;
            settings.AiCheckUtc = DateTime.UtcNow.ToString("o");
            editingKey = false;
            UpdateKeyState();
            ShowAiStatus(failure == null ? "✓ 检测通过，已保存。可以点「用测试题检验」看看它改计划靠不靠谱。" : "已保存，但尚未通过检测。", failure == null ? Color.SeaGreen : Color.DarkOrange);
            EventHandler handler = SettingsSaved;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        private void ClearAi()
        {
            if (MessageBox.Show(this, "清除服务地址、模型名和当前厂商已保存的 key 吗？", "Roost", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            string keyTarget = SelectedKeyTarget();
            try { if (keyTarget != null) CredentialStore.Delete(keyTarget); }
            catch (System.ComponentModel.Win32Exception) { }
            settings.AiBaseUrl = null;
            settings.AiModel = null;
            settings.AiPresetId = null;
            settings.AiCheckTarget = null;
            editingKey = false;
            UpdateKeyState();
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

