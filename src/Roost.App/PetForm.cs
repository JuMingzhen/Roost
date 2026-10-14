using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;
using Roost.Core;

namespace Roost.App
{
    internal sealed class PetForm : Form
    {
        private readonly TodoService todos;
        private readonly SpriteView sprite;
        private readonly Panel listPanel;
        private readonly FloatingWindow listWindow;
        private readonly FloatingWindow bubbleWindow;
        private readonly FlowLayoutPanel rows;
        private readonly Label heading;
        private readonly Label countLabel;
        private readonly RoundButton editButton;
        private readonly RoundButton addButton;
        private readonly RoundButton selectAllButton;
        private readonly RoundButton doneButton;
        private readonly TextField talkField;
        private readonly SegmentedControl filter;
        private readonly RoundButton moreButton;
        private readonly CardPanel undoPanel;
        private readonly Label undoLabel;
        private readonly RoundButton undoButton;
        private readonly Panel editBar;
        private readonly RoundButton starSelectedButton;
        private readonly RoundButton deleteSelectedButton;
        private readonly TextBox talkBox;
        private readonly RoundButton talkButton;
        private readonly BubbleView bubble;
        private readonly Timer bubbleTimer;
        private readonly NotifyIcon tray;
        private readonly RoundButton settingsButton;
        private readonly IntPtr trayIconHandle;
        private readonly IntPtr reminderTrayIconHandle;
        private readonly ReminderService reminders;
        private readonly ReminderView reminderView;
        private readonly FloatingWindow reminderWindow;
        private readonly Timer reminderTimer;
        private Size reminderSize;
        // 错过提醒的条数，显示在汇总泡里，点「知道了」清零。
        private int missedCount;
        private Func<DateTime> clock = delegate { return DateTime.Now; };
        private readonly ToolStripMenuItem showHideItem;
        private readonly Timer fullscreenTimer;
        private readonly Timer undoTimer;
        private readonly Timer completionTimer;
        private readonly uint showExistingMessage;
        private Point petAnchor;
        private Point mouseDownScreen;
        private Point anchorOnMouseDown;
        private bool dragStarted;
        private bool allowExit;
        private bool userHidden;
        private bool fullscreenHidden;
        private bool screenLocked;
        private Action pendingUndo;
        private SettingsForm settingsForm;
        private bool hotKeyRegistered;
        private bool talkHotKeyRegistered;
        private Size bubbleSize;
        private bool hovering;
        private bool celebrating;
        private System.Threading.CancellationTokenSource talkCancel;
        private bool editing;
        // 清单窗口未显示时子控件的 Visible 读出来都是 false，所以显示状态另外记。
        private bool moreShown;
        private bool undoShown;
        private readonly HashSet<string> selectedIds = new HashSet<string>();
        private int listHeight;
        private const int ListWidth = 340;
        private const int ListPadding = 14;
        private const int MaxRowsHeight = 420;

        internal int AnimationFrameCountForTest { get { return sprite.FrameCount; } }
        internal Rectangle SpriteBoundsForTest { get { return sprite.Bounds; } }
        internal Rectangle ListBoundsForTest { get { return listWindow.Visible ? new Rectangle(listWindow.Left - Left, listWindow.Top - Top, listWindow.Width, listWindow.Height) : Rectangle.Empty; } }
        internal Control ListPanelForTest { get { return listPanel; } }
        internal Form ListWindowForTest { get { return listWindow; } }
        internal bool ListRoundedForTest { get { return listWindow.RoundedForTest; } }
        internal bool HotKeyRegisteredForTest { get { return hotKeyRegistered; } }
        internal bool TalkHotKeyRegisteredForTest { get { return talkHotKeyRegistered; } }
        internal bool TrayIconCustomForTest { get { return trayIconHandle != IntPtr.Zero; } }
        internal bool PetMenuHasSettingsForTest
        {
            get
            {
                if (sprite.ContextMenuStrip == null) return false;
                foreach (ToolStripItem item in sprite.ContextMenuStrip.Items) if (item.Text == "设置") return true;
                return false;
            }
        }

        internal void ClickSettingsButtonForTest()
        {
            settingsButton.PerformClick();
        }
        internal string BubbleMessageForTest { get { return bubble.MessageForTest; } }
        internal Control BubbleForTest { get { return bubble; } }
        internal ContextMenuStrip MenuForTest { get { return sprite.ContextMenuStrip; } }
        internal Control ReminderViewForTest { get { return reminderView; } }
        internal Form ReminderWindowForTest { get { return reminderWindow; } }
        internal string ReminderModeForTest { get { return reminderView.ModeForTest; } }
        internal string ReminderHeadlineForTest { get { return reminderView.HeadlineForTest; } }
        internal int ReminderCountForTest { get { return reminders.Active.Count; } }
        internal string TrayTextForTest { get { return tray.Text; } }
        internal bool TrayAlertForTest { get { return reminderTrayIconHandle != IntPtr.Zero && tray.Icon != null && tray.Icon.Handle == reminderTrayIconHandle; } }

        // 测试用虚构时钟推进时间。
        internal void SetClockForTest(Func<DateTime> value)
        {
            clock = value;
        }

        internal void TickRemindersForTest()
        {
            TickReminders();
        }

        internal void ExpandRemindersForTest()
        {
            reminderView.ExpandForTest();
        }

        internal string ReminderIdForTest(int index)
        {
            return reminders.Active[index].Id;
        }

        internal void CompleteReminderForTest(string id) { CompleteReminder(id); }
        internal void SnoozeReminderForTest(string id, int minutes) { SnoozeReminder(id, minutes); }
        internal void DismissReminderForTest(string id) { DismissReminder(id); }
        internal void DismissAllRemindersForTest() { DismissAllReminders(); }
        internal void DismissMissedForTest() { missedCount = 0; UpdateReminderView(); }

        internal void ShowBubbleForTest(string message, string actionText)
        {
            ShowBubble(message, actionText, null);
        }
        internal PetState PetStateForTest { get { return sprite.State; } }
        internal bool ThinkingForTest { get { return talkCancel != null; } }
        internal string TalkTextForTest { get { return talkBox.Text; } }
        internal bool UndoVisibleForTest { get { return undoShown; } }
        internal string UndoLabelForTest { get { return undoLabel.Text; } }
        internal bool EditingForTest { get { return editing; } }

        internal void SetEditingForTest(bool value)
        {
            if (value) EnterEditMode(); else ExitEditMode();
        }

        internal void SelectAllForTest()
        {
            ToggleSelectAll();
        }

        internal void DeleteSelectedForTest()
        {
            DeleteSelected();
        }

        internal void StarSelectedForTest()
        {
            StarSelected();
        }

        internal void SendTalkForTest(string text)
        {
            UpdateTalkState();
            talkBox.Text = text;
            SendTalk();
        }

        internal void RunUndoForTest()
        {
            RunUndo();
        }

        internal void CancelTalkForTest()
        {
            if (talkCancel != null) talkCancel.Cancel();
        }
        internal string[] TrayLabelsForTest
        {
            get { return new string[] { showHideItem.Text, "设置", "退出" }; }
        }

        // 点在 Roost 的任一窗口上（宠物、清单或气泡）时为 true；坐标相对宠物窗口。
        internal bool HitTestForTest(Point clientPoint)
        {
            Point screen = new Point(Left + clientPoint.X, Top + clientPoint.Y);
            if (listWindow.Visible && listWindow.Bounds.Contains(screen)) return true;
            if (bubbleWindow.Visible && bubbleWindow.Bounds.Contains(screen)) return true;
            if (reminderWindow.Visible && reminderWindow.Bounds.Contains(screen)) return true;
            return Region != null && Region.IsVisible(clientPoint);
        }

        internal void ToggleVisibilityForTest()
        {
            ToggleFromUser();
        }

        internal void EvaluateFullscreenForTest(bool fullscreen)
        {
            ApplyFullscreenState(fullscreen);
        }

        internal void DisableFullscreenDetectionForTest()
        {
            fullscreenTimer.Stop();
            if (!Visible) ShowFromUser();
        }

        internal void CloseForTest()
        {
            allowExit = true;
            Close();
        }

        internal PetForm(TodoService todos, uint showExistingMessage, string assetRoot)
        {
            this.todos = todos;
            this.showExistingMessage = showExistingMessage;
            Text = "Roost";
            Name = "RoostPetWindow";
            AutoScaleMode = AutoScaleMode.None;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            BackColor = Color.Magenta;
            TransparencyKey = Color.Magenta;
            Font = new Font("Microsoft YaHei UI", 9F);

            sprite = new SpriteView(assetRoot);
            sprite.MouseDown += SpriteMouseDown;
            sprite.MouseMove += SpriteMouseMove;
            sprite.MouseUp += SpriteMouseUp;
            sprite.MouseEnter += delegate { hovering = true; UpdatePetState(); };
            sprite.MouseLeave += delegate { hovering = false; UpdatePetState(); };

            // 清单按效果图重做：尺寸按 100% 缩放写在 LayoutList 里，用 DpiScale.Px 换成物理像素。
            listPanel = new Panel { BackColor = Theme.Paper, Font = Theme.Body };
            heading = new Label { Text = "今天要做", Font = Theme.ListTitle, ForeColor = Theme.Text, BackColor = Theme.Paper, AutoEllipsis = true };
            countLabel = new Label { Font = Theme.Caption, ForeColor = Theme.TextMuted, BackColor = Theme.Paper, AutoEllipsis = true };
            editButton = new RoundButton("编辑", ButtonKind.Ghost) { Glyph = Theme.Icons.Edit };
            editButton.Click += delegate { EnterEditMode(); };
            settingsButton = new RoundButton(string.Empty, ButtonKind.Ghost) { Glyph = Theme.Icons.Settings, AccessibleName = "设置" };
            settingsButton.Click += delegate { OpenSettings(); };
            addButton = new RoundButton(string.Empty, ButtonKind.Primary) { Glyph = Theme.Icons.Add, Pill = true, AccessibleName = "新建待办" };
            addButton.Click += delegate { OpenEditor(null); };
            ToolTip tips = new ToolTip();
            tips.SetToolTip(settingsButton, "设置");
            tips.SetToolTip(addButton, "新建待办");
            tips.SetToolTip(editButton, "多选删除或加星标");
            selectAllButton = new RoundButton("全选", ButtonKind.Ghost) { Visible = false };
            selectAllButton.Click += delegate { ToggleSelectAll(); };
            doneButton = new RoundButton("完成", ButtonKind.Soft) { Visible = false, Font = Theme.BodyBold };
            doneButton.Click += delegate { ExitEditMode(); };

            talkField = new TextField { Pill = true, PaddingLeft = 16, PaddingRight = 42 };
            talkBox = talkField.Box;
            talkBox.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode != Keys.Enter) return;
                e.SuppressKeyPress = true;
                if (talkCancel == null) SendTalk();
            };
            talkBox.MouseDown += delegate { if (!AiReady()) PromptConfigureAi(); };
            talkBox.HandleCreated += delegate { NativeMethods.SetCueBanner(talkBox.Handle, "跟小猫说一句（Ctrl+Alt+空格）"); };
            talkButton = new RoundButton(string.Empty, ButtonKind.Soft) { Glyph = Theme.Icons.Send, Pill = true, AccessibleName = "发送", TabStop = false };
            talkButton.Click += delegate
            {
                if (talkCancel != null) talkCancel.Cancel();
                else SendTalk();
            };
            talkField.Controls.Add(talkButton);

            filter = new SegmentedControl("全部", "只显示今日");
            filter.SelectedIndex = todos.Data.Settings.OnlyToday ? 1 : 0;
            filter.SelectedIndexChanged += delegate
            {
                todos.Data.Settings.OnlyToday = filter.SelectedIndex == 1;
                SaveSettingsAndRefresh();
            };
            rows = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Theme.Paper, Margin = Padding.Empty };
            moreButton = new RoundButton(string.Empty, ButtonKind.Ghost) { Visible = false, Font = Theme.Caption };
            moreButton.Click += delegate
            {
                todos.Data.Settings.ListExpanded = !todos.Data.Settings.ListExpanded;
                SaveSettingsAndRefresh();
            };
            undoPanel = new CardPanel { BackColor = Theme.Toast, BorderColor = Theme.Toast, Pill = true, Visible = false };
            undoLabel = new Label { Text = "已删除", ForeColor = Color.White, BackColor = Theme.Toast, Font = Theme.Caption, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft };
            undoButton = new RoundButton("撤销", ButtonKind.Toast) { Pill = true, Font = Theme.CaptionBold };
            undoButton.Click += delegate { RunUndo(); };
            undoPanel.Controls.AddRange(new Control[] { undoLabel, undoButton });
            editBar = new Panel { BackColor = Theme.Paper, Visible = false };
            starSelectedButton = new RoundButton("加星标", ButtonKind.Secondary) { Glyph = Theme.Icons.StarFilled, GlyphColor = Theme.Star };
            starSelectedButton.Click += delegate { StarSelected(); };
            deleteSelectedButton = new RoundButton("删除", ButtonKind.DangerFilled) { Glyph = Theme.Icons.Delete };
            deleteSelectedButton.Click += delegate { DeleteSelected(); };
            editBar.Controls.AddRange(new Control[] { starSelectedButton, deleteSelectedButton });
            listPanel.Controls.AddRange(new Control[] { heading, countLabel, editButton, settingsButton, addButton, selectAllButton, doneButton, talkField, filter, rows, moreButton, editBar, undoPanel });
            bubble = new BubbleView { Font = Font };
            bubble.Dismissed += delegate { bubbleTimer.Stop(); bubbleSize = Size.Empty; ApplyLayout(); };
            bubbleWindow = new FloatingWindow(bubble, NativeMethods.DWMWCP_ROUND) { Owner = this };
            reminders = new ReminderService(todos);
            reminderView = new ReminderView();
            reminderView.CompleteRequested += delegate(string id) { CompleteReminder(id); };
            reminderView.SnoozeRequested += delegate(string id, int minutes) { SnoozeReminder(id, minutes); };
            reminderView.DismissRequested += delegate(string id) { DismissReminder(id); };
            reminderView.DismissAllRequested += delegate { DismissAllReminders(); };
            reminderView.MissedDismissed += delegate { missedCount = 0; UpdateReminderView(); };
            reminderView.SizeChangedByUser += delegate { UpdateReminderView(); };
            reminderWindow = new FloatingWindow(reminderView, NativeMethods.DWMWCP_ROUND) { Owner = this };
            reminderTimer = new Timer { Interval = 60000 };
            reminderTimer.Tick += delegate { TickReminders(); };
            listWindow = new FloatingWindow(listPanel, NativeMethods.DWMWCP_ROUND) { Owner = this, KeyPreview = true };
            listWindow.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Escape && editing) ExitEditMode();
            };
            Controls.Add(sprite);

            ContextMenuStrip menu = new ContextMenuStrip();
            showHideItem = new ToolStripMenuItem("隐藏宠物");
            showHideItem.Click += delegate { ToggleFromUser(); };
            ToolStripMenuItem settingsItem = new ToolStripMenuItem("设置");
            settingsItem.Click += delegate { OpenSettings(); };
            ToolStripMenuItem exitItem = new ToolStripMenuItem("退出") { ForeColor = Theme.Danger };
            exitItem.Click += delegate { ExitApplication(); };
            menu.Items.AddRange(new ToolStripItem[] { showHideItem, settingsItem, new ToolStripSeparator(), exitItem });
            ThemedMenuRenderer.Apply(menu);
            sprite.ContextMenuStrip = menu;
            trayIconHandle = CreateTrayIconHandle(assetRoot, "idle.png");
            reminderTrayIconHandle = CreateTrayIconHandle(assetRoot, "notification.png");
            tray = new NotifyIcon
            {
                Text = "Roost（右键打开菜单）",
                Icon = trayIconHandle == IntPtr.Zero ? SystemIcons.Application : Icon.FromHandle(trayIconHandle),
                ContextMenuStrip = menu,
                Visible = true
            };
            tray.DoubleClick += delegate { ShowFromUser(); };

            fullscreenTimer = new Timer { Interval = 1000 };
            fullscreenTimer.Tick += delegate { CheckFullscreen(); };
            undoTimer = new Timer { Interval = 5000 };
            undoTimer.Tick += delegate { HideUndo(); };
            bubbleTimer = new Timer { Interval = 15000 };
            bubbleTimer.Tick += delegate { bubble.Dismiss(); };
            completionTimer = new Timer { Interval = 3000 };
            completionTimer.Tick += delegate
            {
                completionTimer.Stop();
                celebrating = false;
                UpdatePetState();
                RefreshList();
            };

            int size = PetSize();
            Rectangle work = Screen.PrimaryScreen.WorkingArea;
            petAnchor = todos.Data.Settings.HasSavedPosition
                ? new Point(todos.Data.Settings.PetX, todos.Data.Settings.PetY)
                : new Point(work.Right - size - DpiScale.Px(24), work.Bottom - size - DpiScale.Px(24));
            try { CredentialStore.MigrateLegacyApiKey(todos.Data.Settings); }
            catch (System.ComponentModel.Win32Exception) { }
            ApplyLayout();
            RefreshList();
            UpdateTalkState();
            ApplyOpacity();
            fullscreenTimer.Start();
            SystemEvents.SessionSwitch += SessionSwitch;
            SystemEvents.PowerModeChanged += PowerModeChanged;
            SystemEvents.TimeChanged += TimeChanged;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            string readyFile = Environment.GetEnvironmentVariable("ROOST_TEST_READY_FILE");
            if (!string.IsNullOrEmpty(readyFile)) File.WriteAllText(readyFile, "ready");
            bool skipFirstRun = Environment.GetEnvironmentVariable("ROOST_SKIP_FIRST_RUN") == "1";
            if (!skipFirstRun && !todos.Data.Settings.FirstRunCompleted && todos.Data.Todos.Count == 0)
            {
                BeginInvoke((MethodInvoker)delegate { ShowFirstRunGuide(); });
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            hotKeyRegistered = NativeMethods.RegisterHotKey(Handle, NativeMethods.HOTKEY_ID, NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT, (uint)Keys.H);
            if (!hotKeyRegistered && Environment.GetEnvironmentVariable("ROOST_SKIP_HOTKEY_WARNING") != "1")
            {
                BeginInvoke((MethodInvoker)delegate
                {
                    MessageBox.Show(this, "Ctrl+Alt+H 已被其他程序占用。一键隐藏暂不可用。", "Roost", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                });
            }
            talkHotKeyRegistered = NativeMethods.RegisterHotKey(Handle, NativeMethods.TALK_HOTKEY_ID, NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT, (uint)Keys.Space);
            if (!talkHotKeyRegistered && Environment.GetEnvironmentVariable("ROOST_SKIP_HOTKEY_WARNING") != "1")
            {
                BeginInvoke((MethodInvoker)delegate
                {
                    MessageBox.Show(this, "Ctrl+Alt+空格 已被其他程序占用。「跟宠物说」快捷键暂不可用，仍可直接点清单上的输入框。", "Roost", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                });
            }
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            NativeMethods.UnregisterHotKey(Handle, NativeMethods.HOTKEY_ID);
            NativeMethods.UnregisterHotKey(Handle, NativeMethods.TALK_HOTKEY_ID);
            base.OnHandleDestroyed(e);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!allowExit)
            {
                e.Cancel = true;
                HideFromUser();
                return;
            }
            fullscreenTimer.Stop();
            reminderTimer.Stop();
            // 记下最后一次检查提醒的时间，下次启动时据此判断错过的提醒（PRD 10.4）。
            todos.SaveSettings();
            listWindow.Close();
            bubbleWindow.Close();
            reminderWindow.Close();
            tray.Visible = false;
            if (trayIconHandle != IntPtr.Zero) NativeMethods.DestroyIcon(trayIconHandle);
            if (reminderTrayIconHandle != IntPtr.Zero) NativeMethods.DestroyIcon(reminderTrayIconHandle);
            SystemEvents.SessionSwitch -= SessionSwitch;
            SystemEvents.PowerModeChanged -= PowerModeChanged;
            SystemEvents.TimeChanged -= TimeChanged;
            base.OnFormClosing(e);
        }

        protected override void WndProc(ref Message message)
        {
            if ((uint)message.Msg == showExistingMessage)
            {
                ShowFromUser();
                string showFile = Environment.GetEnvironmentVariable("ROOST_TEST_SHOW_FILE");
                if (!string.IsNullOrEmpty(showFile)) File.WriteAllText(showFile, "shown");
                return;
            }
            if (message.Msg == NativeMethods.WM_HOTKEY && message.WParam.ToInt32() == NativeMethods.HOTKEY_ID)
            {
                ToggleFromUser();
                return;
            }
            if (message.Msg == NativeMethods.WM_HOTKEY && message.WParam.ToInt32() == NativeMethods.TALK_HOTKEY_ID)
            {
                FocusTalk();
                return;
            }
            if (message.Msg == NativeMethods.WM_DISPLAYCHANGE || message.Msg == NativeMethods.WM_DPICHANGED)
            {
                BeginInvoke((MethodInvoker)delegate { RecoverAndSavePosition(); });
            }
            base.WndProc(ref message);
        }

        // 托盘图标用猫的待机帧（有提醒时用提醒帧），缩到托盘尺寸（最近邻，保持像素风）。
        private static IntPtr CreateTrayIconHandle(string assetRoot, string fileName)
        {
            string path = Path.Combine(assetRoot, fileName);
            if (!File.Exists(path)) return IntPtr.Zero;
            Size size = SystemInformation.SmallIconSize;
            using (Bitmap source = new Bitmap(path))
            using (Bitmap icon = new Bitmap(size.Width, size.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
            {
                using (Graphics graphics = Graphics.FromImage(icon))
                {
                    SpriteView.ConfigurePixelGraphics(graphics);
                    graphics.Clear(Color.Transparent);
                    graphics.DrawImage(source, new Rectangle(Point.Empty, size), 0, 0, source.Width, source.Height, GraphicsUnit.Pixel);
                }
                return icon.GetHicon();
            }
        }

        private int PetSize()
        {
            int[] sizes = new int[] { 96, 128, 160 };
            int index = Math.Max(0, Math.Min(2, todos.Data.Settings.SizeTier - 1));
            // 档位按 100% 缩放定义，随系统缩放同比放大（最近邻绘制，像素门禁覆盖这些尺寸）。
            return DpiScale.Px(sizes[index]);
        }

        private void ApplyLayout()
        {
            int size = PetSize();
            Screen screen = Screen.FromPoint(petAnchor);
            petAnchor = LayoutRules.RecoverPetPosition(petAnchor, new Size(size, size), screen.WorkingArea);
            // 提醒泡和临时气泡共用宠物旁边的气泡位置：提醒泡挨着宠物，临时气泡在它外侧，互不覆盖。
            int gap = DpiScale.Px(8);
            Size message = bubble.Open ? bubbleSize : Size.Empty;
            Size combined = message;
            if (!reminderSize.IsEmpty)
                combined = message.IsEmpty ? reminderSize : new Size(Math.Max(message.Width, reminderSize.Width), message.Height + gap + reminderSize.Height);
            PetLayout layout = LayoutRules.Compute(petAnchor, new Size(size, size), new Size(DpiScale.Px(ListWidth), listHeight), combined, screen.WorkingArea, todos.Data.Settings.ListVisible, gap);
            Bounds = layout.WindowBounds;
            sprite.Bounds = layout.PetBounds;
            ApplyHitRegion();
            PlaceFloating(listWindow, layout.ListBounds, todos.Data.Settings.ListVisible);
            Rectangle area = layout.BubbleBounds;
            bool areaAbove = !area.IsEmpty && area.Bottom <= layout.PetBounds.Top;
            Rectangle reminderRect = Rectangle.Empty, messageRect = Rectangle.Empty;
            if (!area.IsEmpty && !reminderSize.IsEmpty)
                reminderRect = AlignInArea(area, reminderSize, areaAbove ? area.Bottom - reminderSize.Height : area.Top, layout);
            if (!area.IsEmpty && !message.IsEmpty)
                messageRect = AlignInArea(area, message, reminderSize.IsEmpty || areaAbove ? area.Top : area.Bottom - message.Height, layout);
            PlaceFloating(bubbleWindow, messageRect, bubble.Open && !messageRect.IsEmpty);
            PlaceFloating(reminderWindow, reminderRect, !reminderRect.IsEmpty);
        }

        // 气泡在气泡区域里靠向清单那一侧对齐（没有清单时居中），和 LayoutRules 的气泡位置一致。
        private Rectangle AlignInArea(Rectangle area, Size size, int y, PetLayout layout)
        {
            int x;
            if (!todos.Data.Settings.ListVisible) x = area.Left + (area.Width - size.Width) / 2;
            else if (layout.ListOnLeft) x = area.Left;
            else x = area.Right - size.Width;
            return new Rectangle(new Point(x, y), size);
        }

        // 清单和气泡窗口跟随宠物窗口；宠物窗口隐藏时一起隐藏。
        private void PlaceFloating(FloatingWindow window, Rectangle relative, bool wanted)
        {
            if (!relative.IsEmpty) window.Bounds = new Rectangle(Left + relative.X, Top + relative.Y, relative.Width, relative.Height);
            bool show = wanted && Visible;
            if (show && !window.Visible) window.Show(this);
            else if (!show && window.Visible) window.Hide();
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (listWindow != null)
            {
                ApplyLayout();
                UpdateTrayIcon();
            }
        }

        private void ApplyOpacity()
        {
            double opacity = LayoutRules.ClampOpacity(todos.Data.Settings.Opacity);
            Opacity = opacity;
            listWindow.Opacity = opacity;
            bubbleWindow.Opacity = opacity;
        }

        private void ApplyHitRegion()
        {
            Region hit = sprite.CreateHitRegion(sprite.Bounds);
            Region previous = Region;
            Region = hit;
            if (previous != null) previous.Dispose();
        }

        private void SpriteMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            mouseDownScreen = Cursor.Position;
            anchorOnMouseDown = petAnchor;
            dragStarted = false;
            sprite.Capture = true;
        }

        private void SpriteMouseMove(object sender, MouseEventArgs e)
        {
            if (!sprite.Capture || e.Button != MouseButtons.Left) return;
            Point current = Cursor.Position;
            if (!dragStarted && LayoutRules.IsDrag(mouseDownScreen, current, DpiScale.Px(5)))
            {
                dragStarted = true;
                UpdatePetState();
            }
            if (dragStarted)
            {
                petAnchor = new Point(anchorOnMouseDown.X + current.X - mouseDownScreen.X, anchorOnMouseDown.Y + current.Y - mouseDownScreen.Y);
                ApplyLayout();
            }
        }

        private void SpriteMouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            sprite.Capture = false;
            if (dragStarted)
            {
                int size = PetSize();
                Rectangle work = Screen.FromPoint(petAnchor).WorkingArea;
                petAnchor = LayoutRules.SnapPetPosition(petAnchor, new Size(size, size), work, DpiScale.Px(12));
                SavePosition();
                ApplyLayout();
                dragStarted = false;
                UpdatePetState();
            }
            else
            {
                todos.Data.Settings.ListVisible = !todos.Data.Settings.ListVisible;
                todos.SaveSettings();
                ApplyLayout();
            }
        }

        private void RefreshList()
        {
            rows.SuspendLayout();
            foreach (Control old in rows.Controls) old.Dispose();
            rows.Controls.Clear();
            DateTime now = DateTime.Now;
            int dayStart = todos.Data.Settings.DayStartMinutes;
            List<TodoItem> sorted = TodoRules.SortAndFilter(todos.Data.Todos, now, dayStart, todos.Data.Settings.OnlyToday);
            // 编辑模式展开全部，方便一次选完。
            VisibleTodoResult visible = TodoRules.VisibleItems(sorted, editing || todos.Data.Settings.ListExpanded, 5);
            selectedIds.RemoveWhere(delegate(string id) { return !sorted.Exists(delegate(TodoItem item) { return item.Id == id; }); });
            if (visible.Items.Count == 0)
            {
                Label empty = new Label
                {
                    Text = todos.Data.Todos.Count == 0
                        ? "清单还是空的。\r\n点右上角「+」新建待办；\r\n点宠物可以折叠清单；\r\nAI 配置入口在设置中。"
                        : "这个筛选下没有待办。",
                    Size = new Size(TodoRowControl.RowWidth, 110),
                    ForeColor = Theme.TextMuted,
                    BackColor = Theme.Paper,
                    Font = Theme.Body,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Margin = Padding.Empty
                };
                DpiScale.Apply(empty);
                rows.Controls.Add(empty);
            }
            foreach (TodoItem item in visible.Items)
            {
                TodoRowControl row = new TodoRowControl(item, TodoRules.TimeLabel(item, now, dayStart), editing, selectedIds.Contains(item.Id));
                row.EditRequested += delegate(TodoItem value) { OpenEditor(value); };
                row.StarRequested += delegate(TodoItem value) { todos.ToggleStarred(value.Id); RefreshList(); };
                row.CompleteRequested += delegate(TodoItem value) { ToggleComplete(value, row); };
                row.SelectionChanged += delegate(TodoItem value)
                {
                    if (row.IsSelected) selectedIds.Add(value.Id); else selectedIds.Remove(value.Id);
                    UpdateEditHeader();
                };
                DpiScale.Apply(row);
                row.Margin = new Padding(0, 0, 0, DpiScale.Px(6));
                rows.Controls.Add(row);
            }
            rows.ResumeLayout();

            int open = 0, overdue = 0;
            foreach (TodoItem item in todos.Data.Todos)
            {
                if (item.IsDeleted || item.IsCompleted) continue;
                open++;
                if (TodoRowControl.TimeColor(TodoRules.TimeLabel(item, now, dayStart)) == Theme.Danger) overdue++;
            }
            countLabel.Text = open == 0 ? "没有未完成的待办" : (overdue == 0 ? string.Format("{0} 项未完成", open) : string.Format("{0} 项未完成 · {1} 项已过期", open, overdue));
            countLabel.ForeColor = overdue > 0 ? Theme.Danger : Theme.TextMuted;
            editButton.Enabled = sorted.Count > 0;
            moreShown = !editing && (visible.HiddenCount > 0 || (todos.Data.Settings.ListExpanded && sorted.Count > 5));
            moreButton.Visible = moreShown;
            moreButton.Text = todos.Data.Settings.ListExpanded ? "收起" : string.Format("还有 {0} 条", visible.HiddenCount);
            moreButton.Glyph = todos.Data.Settings.ListExpanded ? Theme.Icons.ChevronUp : Theme.Icons.ChevronDown;
            UpdateEditHeader();
            LayoutList();
            // 待办变了（新建、改时间、完成、删除、AI 改动、撤销），提醒也要重新判断。
            if (reminders != null) TickReminders();
        }

        // 按当前状态排好清单里的各块，算出清单高度（物理像素），再重排窗口。
        private void LayoutList()
        {
            int pad = DpiScale.Px(ListPadding);
            int width = DpiScale.Px(ListWidth);
            int inner = width - 2 * pad;
            int right = width - pad;
            int y = DpiScale.Px(14);

            heading.Bounds = new Rectangle(pad, y, DpiScale.Px(170), DpiScale.Px(26));
            countLabel.Bounds = new Rectangle(pad, y + DpiScale.Px(26), DpiScale.Px(172), DpiScale.Px(18));
            countLabel.Visible = !editing;
            int buttonTop = y + DpiScale.Px(4);
            addButton.Bounds = new Rectangle(right - DpiScale.Px(32), buttonTop, DpiScale.Px(32), DpiScale.Px(32));
            settingsButton.Bounds = new Rectangle(addButton.Left - DpiScale.Px(36), buttonTop, DpiScale.Px(32), DpiScale.Px(32));
            editButton.Bounds = new Rectangle(settingsButton.Left - DpiScale.Px(70), buttonTop, DpiScale.Px(66), DpiScale.Px(32));
            doneButton.Bounds = new Rectangle(right - DpiScale.Px(60), buttonTop, DpiScale.Px(60), DpiScale.Px(32));
            selectAllButton.Bounds = new Rectangle(doneButton.Left - DpiScale.Px(76), buttonTop, DpiScale.Px(72), DpiScale.Px(32));
            foreach (Control control in new Control[] { editButton, settingsButton, addButton }) control.Visible = !editing;
            foreach (Control control in new Control[] { selectAllButton, doneButton }) control.Visible = editing;
            y += DpiScale.Px(editing ? 44 : 54);

            talkField.Visible = !editing;
            filter.Visible = !editing;
            if (!editing)
            {
                talkField.Bounds = new Rectangle(pad, y, inner, DpiScale.Px(38));
                talkButton.Bounds = new Rectangle(talkField.Width - DpiScale.Px(34), DpiScale.Px(5), DpiScale.Px(28), DpiScale.Px(28));
                talkField.LayoutBox();
                y += DpiScale.Px(38 + 10);
                filter.Bounds = new Rectangle(pad, y, 1, DpiScale.Px(30));
                filter.LayoutOptions();
                y += DpiScale.Px(30 + 10);
            }

            int content = 0;
            foreach (Control row in rows.Controls) content += row.Height + row.Margin.Vertical;
            int maxRows = DpiScale.Px(MaxRowsHeight);
            bool scroll = content > maxRows;
            rows.AutoScroll = scroll;
            int rowWidth = scroll ? inner - SystemInformation.VerticalScrollBarWidth - DpiScale.Px(2) : inner;
            foreach (Control row in rows.Controls) row.Width = rowWidth;
            rows.Bounds = new Rectangle(pad, y, inner, Math.Min(content, maxRows));
            y += rows.Height;

            if (moreShown)
            {
                moreButton.Bounds = new Rectangle(pad, y, inner, DpiScale.Px(28));
                y += DpiScale.Px(28);
            }
            editBar.Visible = editing;
            if (editing)
            {
                y += DpiScale.Px(6);
                editBar.Bounds = new Rectangle(pad, y, inner, DpiScale.Px(36));
                int half = (inner - DpiScale.Px(8)) / 2;
                starSelectedButton.Bounds = new Rectangle(0, 0, half, editBar.Height);
                deleteSelectedButton.Bounds = new Rectangle(inner - half, 0, half, editBar.Height);
                y += editBar.Height;
            }
            if (undoShown)
            {
                y += DpiScale.Px(8);
                undoPanel.Bounds = new Rectangle(pad, y, inner, DpiScale.Px(40));
                int buttonWidth = TextRenderer.MeasureText(undoButton.Text, undoButton.Font).Width + DpiScale.Px(28);
                undoButton.Bounds = new Rectangle(undoPanel.Width - buttonWidth - DpiScale.Px(6), DpiScale.Px(6), buttonWidth, DpiScale.Px(28));
                // 文字块上下内缩，避免方角露出胶囊的圆角。
                undoLabel.Bounds = new Rectangle(DpiScale.Px(20), DpiScale.Px(8), Math.Max(0, undoButton.Left - DpiScale.Px(28)), undoPanel.Height - DpiScale.Px(16));
                y += undoPanel.Height;
            }
            listHeight = y + DpiScale.Px(14);
            ApplyLayout();
        }

        private void EnterEditMode()
        {
            if (editing) return;
            editing = true;
            selectedIds.Clear();
            RefreshList();
        }

        private void ExitEditMode()
        {
            if (!editing) return;
            editing = false;
            selectedIds.Clear();
            RefreshList();
        }

        private void UpdateEditHeader()
        {
            if (!editing)
            {
                heading.Text = "今天要做";
                return;
            }
            int total = 0;
            foreach (Control control in rows.Controls) if (control is TodoRowControl) total++;
            heading.Text = selectedIds.Count == 0 ? "选择待办" : string.Format("已选 {0} 项", selectedIds.Count);
            selectAllButton.Text = total > 0 && selectedIds.Count == total ? "全不选" : "全选";
            starSelectedButton.Enabled = selectedIds.Count > 0;
            deleteSelectedButton.Enabled = selectedIds.Count > 0;
            deleteSelectedButton.Text = selectedIds.Count == 0 ? "删除" : string.Format("删除 {0} 项", selectedIds.Count);
        }

        private void ToggleSelectAll()
        {
            List<string> all = new List<string>();
            foreach (Control control in rows.Controls)
            {
                TodoRowControl row = control as TodoRowControl;
                if (row != null) all.Add(row.Item.Id);
            }
            bool clear = all.Count > 0 && selectedIds.Count == all.Count;
            selectedIds.Clear();
            if (!clear) selectedIds.UnionWith(all);
            RefreshList();
        }

        private void DeleteSelected()
        {
            if (selectedIds.Count == 0) return;
            List<string> deleted = todos.DeleteMany(selectedIds);
            ExitEditMode();
            ShowUndo(string.Format("已删除 {0} 条", deleted.Count), "撤销", 5000, delegate { todos.UndoDeleteMany(deleted); });
        }

        private void StarSelected()
        {
            if (selectedIds.Count == 0) return;
            todos.StarMany(selectedIds);
            ExitEditMode();
        }

        private void OpenEditor(TodoItem item)
        {
            using (TodoEditorForm editor = new TodoEditorForm(item))
            {
                DialogResult result = editor.ShowDialog(this);
                if (result == DialogResult.Abort && item != null)
                {
                    Delete(item);
                    return;
                }
                if (result != DialogResult.OK) return;
                if (item == null)
                    todos.Create(editor.TodoTitle, editor.TodoNotes, editor.TodoDate, editor.TodoTime, editor.TodoStarred);
                else
                    todos.Update(item.Id, editor.TodoTitle, editor.TodoNotes, editor.TodoDate, editor.TodoTime, editor.TodoStarred);
            }
            RefreshList();
        }

        private void ToggleComplete(TodoItem item, TodoRowControl row)
        {
            bool completed = todos.ToggleCompleted(item.Id);
            if (completed)
            {
                row.MarkCompleted();
                Celebrate();
            }
            else
            {
                completionTimer.Stop();
                celebrating = false;
                UpdatePetState();
                RefreshList();
            }
        }

        private void Delete(TodoItem item)
        {
            todos.Delete(item.Id);
            string id = item.Id;
            RefreshList();
            ShowUndo("已删除", "撤销", 5000, delegate { todos.UndoDelete(id); });
        }

        private void ShowUndo(string label, string buttonText, int milliseconds, Action undo)
        {
            pendingUndo = undo;
            undoLabel.Text = label;
            undoButton.Text = buttonText;
            undoTimer.Stop();
            undoTimer.Interval = milliseconds;
            undoTimer.Start();
            undoShown = true;
            undoPanel.Visible = true;
            LayoutList();
        }

        private void RunUndo()
        {
            Action undo = pendingUndo;
            HideUndo();
            if (undo != null) undo();
            RefreshList();
        }

        private void HideUndo()
        {
            undoTimer.Stop();
            pendingUndo = null;
            if (!undoShown) return;
            undoShown = false;
            undoPanel.Visible = false;
            LayoutList();
        }

        private bool AiReady()
        {
            return todos.Data.Settings.AiConfigured && !string.IsNullOrEmpty(ReadApiKey());
        }

        private string ReadApiKey()
        {
            try { return CredentialStore.ReadApiKey(todos.Data.Settings); }
            catch (System.ComponentModel.Win32Exception) { return null; }
        }

        private void UpdateTalkState()
        {
            bool ready = AiReady();
            bool busy = talkCancel != null;
            talkBox.ReadOnly = !ready || busy;
            talkBox.ForeColor = ready ? Theme.Text : Theme.TextMuted;
            if (!ready && talkBox.Text.Length == 0) talkBox.Text = "配置模型后可用（点此设置）";
            else if (ready && talkBox.Text == "配置模型后可用（点此设置）") talkBox.Text = string.Empty;
            talkButton.Glyph = busy ? Theme.Icons.Close : Theme.Icons.Send;
            talkButton.AccessibleName = busy ? "取消" : "发送";
            talkButton.Enabled = ready;
        }

        private void PromptConfigureAi()
        {
            ShowBubble("还没有配置模型，暂时不能跟我说话。不配置也能正常使用本地待办。", "去设置", delegate { OpenSettings(true); });
        }

        private void FocusTalk()
        {
            ShowFromUser();
            if (!todos.Data.Settings.ListVisible)
            {
                todos.Data.Settings.ListVisible = true;
                todos.SaveSettings();
                ApplyLayout();
            }
            NativeMethods.SetForegroundWindow(listWindow.Handle);
            listWindow.Activate();
            talkBox.Focus();
            talkBox.SelectAll();
            if (!AiReady()) PromptConfigureAi();
        }

        private async void SendTalk()
        {
            if (talkCancel != null) return;
            if (!AiReady())
            {
                PromptConfigureAi();
                return;
            }
            string input = talkBox.Text.Trim();
            if (input.Length == 0) return;

            RoostSettings settings = todos.Data.Settings;
            AiRequestContext context = AiRequestContext.Create(todos.Data.Todos, DateTime.Now, settings.DayStartMinutes);
            AiEndpoint endpoint = new AiEndpoint { BaseUrl = settings.AiBaseUrl, Model = settings.AiModel, ApiKey = ReadApiKey() };
            bubble.Dismiss();
            talkCancel = new System.Threading.CancellationTokenSource();
            UpdateTalkState();
            UpdatePetState();
            string reply = null;
            AiException failure = null;
            try
            {
                reply = await new AiClient().CompleteAsync(endpoint, context.SystemPrompt(), input, 2048, talkCancel.Token);
            }
            catch (AiException exception)
            {
                failure = exception;
            }
            finally
            {
                talkCancel.Dispose();
                talkCancel = null;
                UpdateTalkState();
                UpdatePetState();
            }
            if (IsDisposed) return;

            if (failure != null)
            {
                if (failure.Kind == AiFailureKind.Cancelled) return;
                bool configProblem = failure.Kind == AiFailureKind.InvalidKey || failure.Kind == AiFailureKind.NotFound ||
                                     failure.Kind == AiFailureKind.Rejected || failure.Kind == AiFailureKind.Quota;
                if (configProblem) ShowBubble(failure.Message, "去设置", delegate { OpenSettings(true); });
                else ShowBubble(failure.Message + (failure.Retryable ? " 你说的话还在输入框里，按回车就能重试。" : string.Empty), null, null);
                return;
            }

            AiPlan plan = AiPlanParser.Parse(reply, context);
            if (plan.Status == AiPlanStatus.Unclear)
            {
                ShowBubble("我没听懂：" + plan.Message + " 换个说法再试试？", null, null);
                return;
            }
            if (plan.Status == AiPlanStatus.Ambiguous)
            {
                string matched = plan.CandidateTitles.Count == 0 ? string.Empty : " 匹配到：「" + string.Join("」「", plan.CandidateTitles.ToArray()) + "」。";
                ShowBubble("我不确定你说的是哪一条。" + matched + "请说得更具体一些，什么都没有改。", null, null);
                return;
            }

            List<AiOperation> selected;
            using (AiPreviewForm preview = new AiPreviewForm(plan))
            {
                if (preview.ShowDialog(this) != DialogResult.OK) return;
                selected = preview.SelectedOperations;
            }
            if (selected.Count == 0) return;
            AiUndo undo = todos.ApplyAi(selected);
            talkBox.Text = string.Empty;
            RefreshList();
            ShowUndo("已应用 AI 的改动", "撤销这次改动", 10000, delegate { todos.UndoAi(undo); });
            foreach (AiOperation operation in selected)
            {
                if (operation.Kind == AiOperationKind.Complete)
                {
                    Celebrate();
                    break;
                }
            }
        }

        // 检查到点的提醒，刷新提醒泡，并把计时器定到下一次提醒（最长 1 分钟检查一次，防止时钟或休眠漏掉）。
        private void TickReminders()
        {
            reminderTimer.Stop();
            DateTime now = clock();
            ReminderTick tick = reminders.Tick(now);
            missedCount += tick.MissedCount;
            UpdateReminderView();
            DateTime? next = reminders.NextFire(now);
            double wait = next.HasValue ? (next.Value - now).TotalMilliseconds + 200 : 60000;
            reminderTimer.Interval = (int)Math.Max(1000, Math.Min(60000, wait));
            reminderTimer.Start();
        }

        private void UpdateReminderView()
        {
            reminderSize = reminderView.ShowState(reminders.Active, missedCount, DescribeReminder);
            UpdatePetState();
            UpdateTrayIcon();
            ApplyLayout();
        }

        // 提醒泡里的时间说明，例如「今天 15:00 · 10 分钟后开始」「今天（只有日期）」。
        private string DescribeReminder(TodoItem item)
        {
            DateTime now = clock();
            string label = TodoRules.TimeLabel(item, now, todos.Data.Settings.DayStartMinutes);
            DateTime date;
            TimeSpan time;
            if (!TodoRules.TryGetDueTime(item, out time)) return label + "（只有日期）";
            if (!TodoRules.TryGetDueDate(item, out date)) return label;
            TimeSpan left = date.Add(time) - now;
            if (left.TotalMinutes <= 0) return label;
            int minutes = (int)Math.Ceiling(left.TotalMinutes);
            return minutes < 60
                ? string.Format("{0} · {1} 分钟后开始", label, minutes)
                : string.Format("{0} · {1} 小时 {2} 分钟后开始", label, minutes / 60, minutes % 60);
        }

        private void CompleteReminder(string id)
        {
            reminders.Complete(id);
            Celebrate();
            RefreshList();
        }

        private void SnoozeReminder(string id, int minutes)
        {
            reminders.Snooze(id, TimeSpan.FromMinutes(minutes), clock());
            TickReminders();
        }

        private void DismissReminder(string id)
        {
            reminders.Dismiss(id);
            TickReminders();
        }

        private void DismissAllReminders()
        {
            reminders.DismissAll();
            TickReminders();
        }

        // 宠物隐藏（手动或全屏）时有提醒：托盘图标换成提醒状态的猫，悬停提示条数（PRD 10.3）。
        private void UpdateTrayIcon()
        {
            if (tray == null || reminders == null) return;
            int count = reminders.Active.Count;
            bool alert = count > 0 && !Visible && reminderTrayIconHandle != IntPtr.Zero;
            IntPtr wanted = alert ? reminderTrayIconHandle : trayIconHandle;
            if (wanted != IntPtr.Zero && (tray.Icon == null || tray.Icon.Handle != wanted)) tray.Icon = Icon.FromHandle(wanted);
            tray.Text = alert ? string.Format("Roost：有 {0} 件事到点了（显示宠物查看）", count) : "Roost（右键打开菜单）";
        }

        private void PowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            if (!IsHandleCreated) return;
            if (e.Mode == PowerModes.Suspend) BeginInvoke((MethodInvoker)delegate { todos.SaveSettings(); });
            else if (e.Mode == PowerModes.Resume) BeginInvoke((MethodInvoker)delegate { TickReminders(); });
        }

        private void TimeChanged(object sender, EventArgs e)
        {
            if (IsHandleCreated) BeginInvoke((MethodInvoker)delegate { TickReminders(); });
        }

        private void ShowBubble(string message, string actionText, Action action)
        {
            bubbleSize = bubble.Show(message, actionText, action);
            ApplyLayout();
            bubbleTimer.Stop();
            bubbleTimer.Start();
        }

        private void Celebrate()
        {
            celebrating = true;
            UpdatePetState();
            completionTimer.Stop();
            completionTimer.Start();
        }

        private void SaveSettingsAndRefresh()
        {
            todos.SaveSettings();
            RefreshList();
        }

        private void SavePosition()
        {
            todos.Data.Settings.HasSavedPosition = true;
            todos.Data.Settings.PetX = petAnchor.X;
            todos.Data.Settings.PetY = petAnchor.Y;
            todos.SaveSettings();
        }

        private void RecoverAndSavePosition()
        {
            int size = PetSize();
            Rectangle work = Screen.FromPoint(petAnchor).WorkingArea;
            petAnchor = LayoutRules.RecoverPetPosition(petAnchor, new Size(size, size), work);
            SavePosition();
            ApplyLayout();
        }

        private void UpdatePetState()
        {
            // PRD 6.3 优先级：拖动 > 提醒 > 思考 > 庆祝 > 悬停 > 待机。
            PetState state;
            if (dragStarted) state = PetState.Drag;
            else if (reminders != null && reminders.Active.Count > 0) state = PetState.Reminder;
            else if (talkCancel != null) state = PetState.Thinking;
            else if (celebrating) state = PetState.Celebrate;
            else if (hovering) state = PetState.Hover;
            else state = PetState.Idle;
            if (sprite.State == state) return;
            sprite.State = state;
            ApplyHitRegion();
        }

        private void OpenSettings()
        {
            OpenSettings(false);
        }

        private void OpenSettings(bool aiTab)
        {
            if (settingsForm != null && !settingsForm.IsDisposed)
            {
                if (aiTab) settingsForm.ShowAiTab();
                settingsForm.Activate();
                return;
            }
            settingsForm = new SettingsForm(todos.Data.Settings);
            settingsForm.SettingsSaved += delegate
            {
                todos.SaveSettings();
                ApplyOpacity();
                ApplyLayout();
                RefreshList();
                UpdateTalkState();
            };
            if (aiTab) settingsForm.ShowAiTab();
            settingsForm.Show(this);
        }

        private void ToggleFromUser()
        {
            if (Visible && !userHidden) HideFromUser(); else ShowFromUser();
        }

        private void HideFromUser()
        {
            userHidden = true;
            fullscreenHidden = false;
            Hide();
            sprite.Paused = true;
            showHideItem.Text = "显示宠物";
        }

        private void ShowFromUser()
        {
            userHidden = false;
            fullscreenHidden = false;
            Show();
            WindowState = FormWindowState.Normal;
            TopMost = true;
            BringToFront();
            sprite.Paused = screenLocked;
            showHideItem.Text = "隐藏宠物";
        }

        private void CheckFullscreen()
        {
            ApplyFullscreenState(IsForegroundFullscreen());
        }

        private void ApplyFullscreenState(bool fullscreen)
        {
            if (fullscreen && Visible && !userHidden)
            {
                fullscreenHidden = true;
                Hide();
                sprite.Paused = true;
            }
            else if (!fullscreen && fullscreenHidden && !userHidden)
            {
                fullscreenHidden = false;
                Show();
                sprite.Paused = screenLocked;
            }
        }

        private bool IsForegroundFullscreen()
        {
            IntPtr foreground = NativeMethods.GetForegroundWindow();
            if (foreground == IntPtr.Zero || foreground == Handle || foreground == listWindow.Handle || foreground == bubbleWindow.Handle || foreground == reminderWindow.Handle) return false;
            NativeMethods.RECT window;
            if (!NativeMethods.GetWindowRect(foreground, out window)) return false;
            IntPtr monitor = NativeMethods.MonitorFromWindow(foreground, NativeMethods.MONITOR_DEFAULTTONEAREST);
            NativeMethods.MONITORINFO info = new NativeMethods.MONITORINFO();
            info.Size = Marshal.SizeOf(typeof(NativeMethods.MONITORINFO));
            if (!NativeMethods.GetMonitorInfo(monitor, ref info)) return false;
            StringBuilder className = new StringBuilder(128);
            NativeMethods.GetClassName(foreground, className, className.Capacity);
            string value = className.ToString();
            bool desktop = value == "Progman" || value == "WorkerW" || value == "Shell_TrayWnd";
            return FullscreenRules.IsFullscreen(window.ToRectangle(), info.Monitor.ToRectangle(), NativeMethods.IsWindowVisible(foreground), desktop);
        }

        private void SessionSwitch(object sender, SessionSwitchEventArgs e)
        {
            if (e.Reason == SessionSwitchReason.SessionLock)
            {
                screenLocked = true;
                sprite.Paused = true;
            }
            else if (e.Reason == SessionSwitchReason.SessionUnlock)
            {
                if (IsHandleCreated) BeginInvoke((MethodInvoker)delegate { TickReminders(); });
                screenLocked = false;
                sprite.Paused = !Visible;
            }
        }

        private void ShowFirstRunGuide()
        {
            DialogResult result = MessageBox.Show(
                this,
                "欢迎来到 Roost！\r\n\r\n• 点「+」新建待办\r\n• 点宠物折叠或展开清单\r\n• 托盘菜单里可以打开设置\r\n• 在设置的「AI 模型」里配置模型后，可以用一句话让宠物改计划；不配置也能完整使用本地待办\r\n\r\n现在打开设置看看吗？",
                "第一次使用",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Information);
            todos.Data.Settings.FirstRunCompleted = true;
            todos.SaveSettings();
            if (result == DialogResult.Yes) OpenSettings();
        }

        private void ExitApplication()
        {
            allowExit = true;
            Close();
        }
    }
}
