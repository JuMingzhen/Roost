using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using Roost.Core;

namespace Roost.App
{
    // 设置窗口：左侧导航，右侧一页一组分组卡片（效果图「设置窗口」）。
    // 坐标按 100% 缩放写，构造完成后用 DpiScale 整体放大一次。
    internal sealed class SettingsForm : Form
    {
        private const int NavWidth = 184;
        private const int PageWidth = 576;
        private const int PageHeight = 560;
        private const int CardWidth = 520;
        private const int PageMargin = 28;

        private readonly SegmentedControl sizeBox;
        private readonly Slider opacityBar;
        private readonly Label opacityValue;
        private readonly DateTimePicker dayStart;
        private readonly RoostSettings settings;
        private readonly Panel[] pages;
        private readonly RoundButton[] navButtons;
        private readonly int aiPageIndex;
        private readonly ComboBox presetBox;
        private readonly Label presetDescription;
        private readonly LinkLabel keyLink;
        private readonly TextBox urlBox;
        private readonly TextBox modelBox;
        private readonly TextField keyField;
        private readonly TextBox keyBox;
        private readonly CheckBox showKey;
        private readonly Label keyStatus;
        private readonly Label keyHint;
        private readonly RoundButton changeKeyButton;
        private readonly RoundButton deleteKeyButton;
        private readonly RoundButton cancelKeyButton;
        private readonly CardPanel checkBanner;
        private readonly Label checkInfo;
        private readonly RoundButton checkButton;
        private readonly LinkLabel clearLink;
        private readonly RoundButton selfTestButton;
        private bool editingKey;
        private readonly Label aiStatus;
        private CancellationTokenSource checking;

        internal event EventHandler SettingsSaved;

        internal SettingsForm(RoostSettings settings)
        {
            this.settings = settings;
            Text = "Roost 设置";
            Font = Theme.Body;
            BackColor = Theme.Paper;
            ForeColor = Theme.Text;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            ClientSize = new Size(NavWidth + PageWidth, PageHeight);

            string[] names = new string[] { "外观", "快捷键", "AI 模型", "通用", "关于" };
            string[] glyphs = new string[] { Theme.Icons.Appearance, Theme.Icons.Keyboard, Theme.Icons.Sparkle, Theme.Icons.General, Theme.Icons.Info };
            aiPageIndex = 2;
            Panel nav = new Panel { Bounds = new Rectangle(0, 0, NavWidth, PageHeight), BackColor = Theme.Sidebar };
            pages = new Panel[names.Length];
            navButtons = new RoundButton[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                int index = i;
                navButtons[i] = new RoundButton(names[i], ButtonKind.Nav) { Glyph = glyphs[i], Bounds = new Rectangle(10, 14 + i * 40, NavWidth - 20, 36) };
                navButtons[i].Click += delegate { ShowPage(index); };
                nav.Controls.Add(navButtons[i]);
                pages[i] = new Panel { Bounds = new Rectangle(NavWidth, 0, PageWidth, PageHeight), BackColor = Theme.Paper, Visible = false };
            }

            // 外观
            Panel appearance = pages[0];
            int y = PageHeader(appearance, "外观", "宠物的大小和整体透明度。");
            CardPanel appearanceCard = Card(appearance, y, 2 * 64 + 1);
            Row(appearanceCard, 0, "宠物尺寸", "按 100% 缩放为 96 / 128 / 160 像素");
            sizeBox = new SegmentedControl("小", "中", "大") { Location = new Point(CardWidth - 16 - 150, 17), Size = new Size(150, 30) };
            sizeBox.SelectedIndex = Math.Max(0, Math.Min(2, settings.SizeTier - 1));
            appearanceCard.Controls.Add(sizeBox);
            Divider(appearanceCard, 64);
            Row(appearanceCard, 65, "整体透明度", "最低 35%，避免调到看不见");
            opacityBar = new Slider { Location = new Point(CardWidth - 16 - 220, 65 + 18), Size = new Size(170, 28), Minimum = 35, Maximum = 100, Value = (int)Math.Round(LayoutRules.ClampOpacity(settings.Opacity) * 100), AccessibleName = "整体透明度" };
            opacityValue = new Label { Location = new Point(CardWidth - 16 - 44, 65 + 21), Size = new Size(44, 22), TextAlign = ContentAlignment.MiddleRight, BackColor = Theme.Card, ForeColor = Theme.TextMuted };
            opacityBar.ValueChanged += delegate { opacityValue.Text = opacityBar.Value + "%"; };
            opacityValue.Text = opacityBar.Value + "%";
            appearanceCard.Controls.Add(opacityBar);
            appearanceCard.Controls.Add(opacityValue);
            SaveButton(appearance);

            // 快捷键
            Panel shortcuts = pages[1];
            y = PageHeader(shortcuts, "快捷键", "修改快捷键和冲突提示将在后续版本提供。");
            CardPanel keysCard = Card(shortcuts, y, 2 * 56 + 1);
            Row(keysCard, 0, "一键隐藏 / 显示", null, 56);
            KeyCap(keysCard, 0, "Ctrl + Alt + H");
            Divider(keysCard, 56);
            Row(keysCard, 57, "唤出「跟宠物说」", null, 56);
            KeyCap(keysCard, 57, "Ctrl + Alt + 空格");

            // AI 模型
            Panel aiPage = pages[aiPageIndex];
            y = PageHeader(aiPage, "AI 模型", "用一句话改计划需要一个模型。不配置也能正常用本地待办。");
            CardPanel aiCard = Card(aiPage, y, 88 + 1 + 52 + 1 + 52 + 1 + 64);
            RowLabel(aiCard, 0, "服务商", 18);
            presetBox = new ComboBox { Location = new Point(112, 14), Width = 200, DropDownStyle = ComboBoxStyle.DropDownList, ForeColor = Theme.Text };
            foreach (AiPreset preset in AiPresets.All) presetBox.Items.Add(preset.Name);
            presetBox.Items.Add("自定义（OpenAI 兼容）");
            aiCard.Controls.Add(presetBox);
            keyLink = new LinkLabel { Text = "去申请 key", Location = new Point(324, 18), AutoSize = true, BackColor = Theme.Card, LinkColor = Theme.Accent, ActiveLinkColor = Theme.AccentPressed, VisitedLinkColor = Theme.Accent, LinkBehavior = LinkBehavior.HoverUnderline };
            keyLink.LinkClicked += delegate
            {
                AiPreset preset = SelectedPreset();
                if (preset != null) Process.Start(preset.KeyUrl);
            };
            aiCard.Controls.Add(keyLink);
            presetDescription = new Label { Location = new Point(112, 46), Size = new Size(CardWidth - 128, 36), ForeColor = Theme.TextMuted, BackColor = Theme.Card, Font = Theme.Caption };
            aiCard.Controls.Add(presetDescription);
            Divider(aiCard, 88);
            RowLabel(aiCard, 89, "服务地址", 16);
            TextField urlField = Field(aiCard, new Rectangle(112, 89 + 10, CardWidth - 128, 32));
            urlBox = urlField.Box;
            Divider(aiCard, 142);
            RowLabel(aiCard, 143, "模型名", 16);
            TextField modelField = Field(aiCard, new Rectangle(112, 143 + 10, CardWidth - 128, 32));
            modelBox = modelField.Box;
            Divider(aiCard, 196);
            int keyTop = 197;
            RowLabel(aiCard, keyTop, "API Key", 22);
            keyStatus = new Label { Location = new Point(112, keyTop + 12), Size = new Size(200, 22), BackColor = Theme.Card, ForeColor = Theme.Text };
            keyHint = new Label { Text = "保存在 Windows 凭据管理器", Location = new Point(112, keyTop + 34), Size = new Size(200, 20), BackColor = Theme.Card, ForeColor = Theme.TextMuted, Font = Theme.Caption };
            changeKeyButton = new RoundButton("更换", ButtonKind.Secondary) { Location = new Point(CardWidth - 16 - 64 - 8 - 64, keyTop + 16), Size = new Size(64, 32) };
            changeKeyButton.Click += delegate { editingKey = true; UpdateKeyState(); keyBox.Focus(); };
            deleteKeyButton = new RoundButton("删除", ButtonKind.Danger) { Location = new Point(CardWidth - 16 - 64, keyTop + 16), Size = new Size(64, 32) };
            deleteKeyButton.Click += delegate { DeleteKey(); };
            keyField = Field(aiCard, new Rectangle(112, keyTop + 16, CardWidth - 128 - 64 - 8 - 58, 32));
            keyBox = keyField.Box;
            keyBox.UseSystemPasswordChar = true;
            showKey = new CheckBox { Text = "显示", Location = new Point(keyField.Right + 8, keyTop + 22), Size = new Size(56, 22), BackColor = Theme.Card, ForeColor = Theme.TextMuted };
            showKey.CheckedChanged += delegate { keyBox.UseSystemPasswordChar = !showKey.Checked; };
            cancelKeyButton = new RoundButton("取消", ButtonKind.Secondary) { Location = new Point(CardWidth - 16 - 64, keyTop + 16), Size = new Size(64, 32) };
            cancelKeyButton.Click += delegate { editingKey = false; UpdateKeyState(); };
            aiCard.Controls.AddRange(new Control[] { keyStatus, keyHint, changeKeyButton, deleteKeyButton, showKey, cancelKeyButton });

            y = aiCard.Bottom + 12;
            checkBanner = new CardPanel { Location = new Point(PageMargin, y), Size = new Size(CardWidth, 36), BorderColor = Color.Empty };
            checkInfo = new Label { Location = new Point(14, 7), Size = new Size(CardWidth - 28, 22), TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true };
            checkBanner.Controls.Add(checkInfo);
            aiPage.Controls.Add(checkBanner);
            y = checkBanner.Bottom + 14;
            checkButton = new RoundButton("保存并检测", ButtonKind.Primary) { Location = new Point(PageMargin, y), Size = new Size(116, 36) };
            checkButton.Click += delegate { SaveAi(); };
            selfTestButton = new RoundButton("用测试题检验…", ButtonKind.Secondary) { Location = new Point(PageMargin + 116 + 10, y), Size = new Size(132, 36) };
            selfTestButton.Click += delegate { OpenSelfTest(); };
            clearLink = new LinkLabel { Text = "清除全部 AI 配置", AutoSize = true, BackColor = Theme.Paper, LinkColor = Theme.TextMuted, ActiveLinkColor = Theme.Danger, LinkBehavior = LinkBehavior.HoverUnderline };
            clearLink.Location = new Point(PageMargin + CardWidth - 110, y + 9);
            clearLink.LinkClicked += delegate { ClearAi(); };
            aiPage.Controls.AddRange(new Control[] { checkButton, selfTestButton, clearLink });
            aiStatus = new Label { Location = new Point(PageMargin, y + 46), Size = new Size(CardWidth, 44), BackColor = Theme.Paper };
            aiPage.Controls.Add(aiStatus);

            // 通用
            Panel general = pages[3];
            y = PageHeader(general, "通用", "「今天」从几点算起，以及开机自启。");
            CardPanel generalCard = Card(general, y, 64 + 1 + 64);
            Row(generalCard, 0, "一天起点", "这个时间之前算作前一天，默认 04:00");
            dayStart = new DateTimePicker { Location = new Point(CardWidth - 16 - 100, 18), Width = 100, Format = DateTimePickerFormat.Custom, CustomFormat = "HH:mm", ShowUpDown = true, Value = DateTime.Today.AddMinutes(settings.DayStartMinutes) };
            generalCard.Controls.Add(dayStart);
            Divider(generalCard, 64);
            Row(generalCard, 65, "开机自启", "将在 M4 提供");
            ToggleSwitch startup = new ToggleSwitch(string.Empty) { Location = new Point(CardWidth - 16 - 40, 65 + 20), Size = new Size(40, 24), Enabled = false, AccessibleName = "开机自启" };
            generalCard.Controls.Add(startup);
            SaveButton(general);

            // 关于
            Panel about = pages[4];
            y = PageHeader(about, "关于", "一只住在桌面上的像素小猫，帮你记着今天要做的事。");
            CardPanel aboutCard = Card(about, y, 150);
            aboutCard.Controls.Add(new Label { Text = "Roost v1 开发版", Location = new Point(16, 16), Size = new Size(CardWidth - 32, 22), Font = Theme.BodyBold, BackColor = Theme.Card });
            aboutCard.Controls.Add(new Label { Text = "像素猫素材：Desktop Cat\r\nCopyright (c) 2025 Administrator · MIT License", Location = new Point(16, 44), Size = new Size(CardWidth - 32, 44), ForeColor = Theme.TextMuted, BackColor = Theme.Card });
            LinkLabel repository = new LinkLabel { Text = "https://github.com/JuMingzhen/Roost", Location = new Point(16, 104), AutoSize = true, BackColor = Theme.Card, LinkColor = Theme.Accent, ActiveLinkColor = Theme.AccentPressed, VisitedLinkColor = Theme.Accent };
            repository.LinkClicked += delegate { Process.Start(repository.Text); };
            aboutCard.Controls.Add(repository);

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

            Controls.Add(nav);
            Controls.AddRange(pages);
            ShowPage(0);
            DpiScale.Apply(this);
        }

        internal void ShowKeyEditorForTest()
        {
            editingKey = true;
            UpdateKeyState();
        }

        internal string KeyStatusForTest { get { return keyStatus.Visible ? keyStatus.Text : null; } }

        internal int PageCountForTest { get { return pages.Length; } }

        internal string PageNameForTest(int index)
        {
            return navButtons[index].Text;
        }

        internal Control ShowPageForTest(int index)
        {
            ShowPage(index);
            return pages[index];
        }

        internal void ShowAiTab()
        {
            ShowPage(aiPageIndex);
        }

        private void ShowPage(int index)
        {
            for (int i = 0; i < pages.Length; i++)
            {
                pages[i].Visible = i == index;
                navButtons[i].Selected = i == index;
            }
        }

        private static int PageHeader(Panel page, string title, string description)
        {
            page.Controls.Add(new Label { Text = title, Location = new Point(PageMargin, 22), Size = new Size(CardWidth, 30), Font = Theme.PageTitle, ForeColor = Theme.Text, BackColor = Theme.Paper });
            page.Controls.Add(new Label { Text = description, Location = new Point(PageMargin, 54), Size = new Size(CardWidth, 20), Font = Theme.Caption, ForeColor = Theme.TextMuted, BackColor = Theme.Paper });
            return 88;
        }

        private static CardPanel Card(Panel page, int y, int height)
        {
            CardPanel card = new CardPanel { Location = new Point(PageMargin, y), Size = new Size(CardWidth, height) };
            page.Controls.Add(card);
            return card;
        }

        // 一行设置：左边是名称和一行说明，右边放控件（由调用方添加）。
        private static void Row(CardPanel card, int top, string title, string description, int height = 64)
        {
            int titleTop = description == null ? top + (height - 22) / 2 : top + 12;
            card.Controls.Add(new Label { Text = title, Location = new Point(16, titleTop), Size = new Size(240, 22), Font = Theme.BodyBold, BackColor = Theme.Card, ForeColor = Theme.Text });
            if (description != null)
                card.Controls.Add(new Label { Text = description, Location = new Point(16, top + 34), Size = new Size(240, 20), Font = Theme.Caption, BackColor = Theme.Card, ForeColor = Theme.TextMuted });
        }

        private static void RowLabel(CardPanel card, int top, string title, int offset)
        {
            card.Controls.Add(new Label { Text = title, Location = new Point(16, top + offset), Size = new Size(88, 22), Font = Theme.BodyBold, BackColor = Theme.Card, ForeColor = Theme.Text });
        }

        private static void Divider(CardPanel card, int y)
        {
            card.Controls.Add(new Panel { Location = new Point(1, y), Size = new Size(CardWidth - 2, 1), BackColor = Theme.Line });
        }

        private static void KeyCap(CardPanel card, int top, string keys)
        {
            Size size = TextRenderer.MeasureText(keys, Theme.Caption);
            CardPanel cap = new CardPanel { BackColor = Theme.Sidebar, BorderColor = Theme.InputBorder, Radius = 4, Size = new Size(size.Width + 20, 26) };
            cap.Location = new Point(CardWidth - 16 - cap.Width, top + 15);
            cap.Controls.Add(new Label { Text = keys, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Font = Theme.Caption, BackColor = Theme.Sidebar, ForeColor = Theme.Text });
            card.Controls.Add(cap);
        }

        private static TextField Field(CardPanel card, Rectangle bounds)
        {
            TextField field = new TextField { Bounds = bounds };
            card.Controls.Add(field);
            return field;
        }

        private void SaveButton(Panel page)
        {
            RoundButton save = new RoundButton("保存", ButtonKind.Primary) { Location = new Point(PageMargin + CardWidth - 96, PageHeight - 28 - 36), Size = new Size(96, 36) };
            save.Click += delegate { SaveSettings(); };
            page.Controls.Add(save);
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
            keyHint.Visible = !edit;
            changeKeyButton.Visible = !edit;
            deleteKeyButton.Visible = !edit;
            keyField.Visible = edit;
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
                SetBanner(settings.AiCheckPassed ? Theme.SuccessSoft : Theme.WarningSoft, settings.AiCheckPassed ? Theme.Success : Theme.Warning);
            }
            else
            {
                checkInfo.Text = editingKey || string.IsNullOrEmpty(ReadKey(target)) ? "填好后点「保存并检测」。" : "这个组合还没有检测过。";
                SetBanner(Theme.Sidebar, Theme.TextMuted);
            }
        }

        private void SetBanner(Color fill, Color text)
        {
            checkBanner.BackColor = fill;
            checkInfo.BackColor = fill;
            checkInfo.ForeColor = text;
            checkBanner.Invalidate();
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
                ShowAiStatus("✗ 无法从 Windows 凭据管理器删除 key。", Theme.Danger);
                return;
            }
            if (target == settings.AiCheckTarget) settings.AiCheckTarget = null;
            editingKey = false;
            UpdateKeyState();
            ShowAiStatus("已删除 " + name + " 的 key。", Theme.TextMuted);
            EventHandler handler = SettingsSaved;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        private void OpenSelfTest()
        {
            string key = ReadKey(CredentialStore.ApiKeyTargetFor(settings));
            if (!settings.AiConfigured || string.IsNullOrEmpty(key))
            {
                ShowAiStatus("请先保存模型配置，再用测试题检验。", Theme.Danger);
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
                ShowAiStatus("找不到测试题文件，请确认应用文件完整。", Theme.Danger);
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
                ShowAiStatus("请填写服务地址、模型名和 API Key。", Theme.Danger);
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
            ShowAiStatus("正在检测服务地址、key 和模型名……", Theme.TextMuted);
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
                ShowAiStatus("✗ 检测未通过，配置没有保存：" + failure.Message, Theme.Danger);
                return;
            }
            if (failure != null)
            {
                DialogResult keep = MessageBox.Show(this, "暂时无法完成检测：" + failure.Message + "\r\n\r\n仍要保存这份配置吗？", "Roost", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (keep != DialogResult.Yes)
                {
                    ShowAiStatus("配置没有保存。", Theme.TextMuted);
                    return;
                }
            }

            try
            {
                if (key.Length > 0) CredentialStore.Write(keyTarget, key);
            }
            catch (System.ComponentModel.Win32Exception)
            {
                ShowAiStatus("✗ 无法把 key 写入 Windows 凭据管理器，配置没有保存。", Theme.Danger);
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
            ShowAiStatus(failure == null ? "✓ 检测通过，已保存。可以点「用测试题检验」看看它改计划靠不靠谱。" : "已保存，但尚未通过检测。", failure == null ? Theme.Success : Theme.Warning);
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
            ShowAiStatus("已清除 AI 配置。", Theme.TextMuted);
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
