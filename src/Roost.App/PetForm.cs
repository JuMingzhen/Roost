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
        private readonly FlowLayoutPanel rows;
        private readonly CheckBox onlyToday;
        private readonly Button moreButton;
        private readonly Panel undoPanel;
        private readonly Label undoLabel;
        private readonly Button undoButton;
        private readonly NotifyIcon tray;
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
        private string lastDeletedId;
        private SettingsForm settingsForm;
        private bool hotKeyRegistered;

        internal int AnimationFrameCountForTest { get { return sprite.FrameCount; } }
        internal Rectangle SpriteBoundsForTest { get { return sprite.Bounds; } }
        internal Rectangle ListBoundsForTest { get { return listPanel.Bounds; } }
        internal bool HotKeyRegisteredForTest { get { return hotKeyRegistered; } }
        internal string[] TrayLabelsForTest
        {
            get { return new string[] { showHideItem.Text, "设置", "退出" }; }
        }

        internal bool HitTestForTest(Point clientPoint)
        {
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
            sprite.MouseEnter += delegate { if (!dragStarted) SetPetState(PetState.Hover); };
            sprite.MouseLeave += delegate { if (!dragStarted && sprite.State == PetState.Hover) SetPetState(PetState.Idle); };

            listPanel = new Panel { BackColor = Color.FromArgb(250, 246, 239), Padding = new Padding(12) };
            Label heading = new Label { Text = "今天要做", Location = new Point(14, 12), Size = new Size(120, 26), Font = new Font(Font.FontFamily, 11F, FontStyle.Bold) };
            Button add = new Button { Text = "+", Location = new Point(294, 8), Size = new Size(40, 34), FlatStyle = FlatStyle.Flat };
            add.FlatAppearance.BorderSize = 0;
            add.Click += delegate { OpenEditor(null); };
            onlyToday = new CheckBox { Text = "只显示今日", Location = new Point(14, 47), AutoSize = true, Checked = todos.Data.Settings.OnlyToday };
            onlyToday.CheckedChanged += delegate
            {
                todos.Data.Settings.OnlyToday = onlyToday.Checked;
                SaveSettingsAndRefresh();
            };
            Button aiEntry = new Button { Text = "跟宠物说（配置模型后可用）", Location = new Point(136, 42), Size = new Size(198, 30), FlatStyle = FlatStyle.Flat, ForeColor = Color.DimGray };
            aiEntry.FlatAppearance.BorderColor = Color.Silver;
            aiEntry.Click += delegate { OpenSettings(); };
            rows = new FlowLayoutPanel { Location = new Point(14, 82), Size = new Size(320, 316), FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
            moreButton = new Button { Location = new Point(14, 402), Size = new Size(320, 28), FlatStyle = FlatStyle.Flat, Visible = false };
            moreButton.FlatAppearance.BorderSize = 0;
            moreButton.Click += delegate
            {
                todos.Data.Settings.ListExpanded = !todos.Data.Settings.ListExpanded;
                SaveSettingsAndRefresh();
            };
            undoPanel = new Panel { Location = new Point(14, 398), Size = new Size(320, 38), BackColor = Color.FromArgb(60, 60, 60), Visible = false };
            undoLabel = new Label { Text = "已删除", ForeColor = Color.White, Location = new Point(12, 9), AutoSize = true };
            undoButton = new Button { Text = "撤销", Location = new Point(236, 4), Size = new Size(72, 30), FlatStyle = FlatStyle.Flat, ForeColor = Color.White };
            undoButton.Click += delegate { UndoDelete(); };
            undoPanel.Controls.AddRange(new Control[] { undoLabel, undoButton });
            listPanel.Controls.AddRange(new Control[] { heading, add, onlyToday, aiEntry, rows, moreButton, undoPanel });
            Controls.Add(listPanel);
            Controls.Add(sprite);

            ContextMenuStrip menu = new ContextMenuStrip();
            showHideItem = new ToolStripMenuItem("隐藏宠物");
            showHideItem.Click += delegate { ToggleFromUser(); };
            ToolStripMenuItem settingsItem = new ToolStripMenuItem("设置");
            settingsItem.Click += delegate { OpenSettings(); };
            ToolStripMenuItem exitItem = new ToolStripMenuItem("退出");
            exitItem.Click += delegate { ExitApplication(); };
            menu.Items.AddRange(new ToolStripItem[] { showHideItem, settingsItem, new ToolStripSeparator(), exitItem });
            tray = new NotifyIcon { Text = "Roost", Icon = SystemIcons.Application, ContextMenuStrip = menu, Visible = true };
            tray.DoubleClick += delegate { ShowFromUser(); };

            fullscreenTimer = new Timer { Interval = 1000 };
            fullscreenTimer.Tick += delegate { CheckFullscreen(); };
            undoTimer = new Timer { Interval = 5000 };
            undoTimer.Tick += delegate { HideUndo(); };
            completionTimer = new Timer { Interval = 3000 };
            completionTimer.Tick += delegate
            {
                completionTimer.Stop();
                if (!dragStarted) SetPetState(PetState.Idle);
                RefreshList();
            };

            int size = PetSize();
            Rectangle work = Screen.PrimaryScreen.WorkingArea;
            petAnchor = todos.Data.Settings.HasSavedPosition
                ? new Point(todos.Data.Settings.PetX, todos.Data.Settings.PetY)
                : new Point(work.Right - size - 24, work.Bottom - size - 24);
            ApplyLayout();
            RefreshList();
            Opacity = LayoutRules.ClampOpacity(todos.Data.Settings.Opacity);
            fullscreenTimer.Start();
            SystemEvents.SessionSwitch += SessionSwitch;
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
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            NativeMethods.UnregisterHotKey(Handle, NativeMethods.HOTKEY_ID);
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
            tray.Visible = false;
            SystemEvents.SessionSwitch -= SessionSwitch;
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
            if (message.Msg == NativeMethods.WM_DISPLAYCHANGE || message.Msg == NativeMethods.WM_DPICHANGED)
            {
                BeginInvoke((MethodInvoker)delegate { RecoverAndSavePosition(); });
            }
            base.WndProc(ref message);
        }

        private int PetSize()
        {
            int[] sizes = new int[] { 96, 128, 160 };
            int index = Math.Max(0, Math.Min(2, todos.Data.Settings.SizeTier - 1));
            return sizes[index];
        }

        private void ApplyLayout()
        {
            int size = PetSize();
            Screen screen = Screen.FromPoint(petAnchor);
            petAnchor = LayoutRules.RecoverPetPosition(petAnchor, new Size(size, size), screen.WorkingArea);
            PetLayout layout = LayoutRules.Compute(petAnchor, new Size(size, size), new Size(348, 446), screen.WorkingArea, todos.Data.Settings.ListVisible, 8);
            Bounds = layout.WindowBounds;
            sprite.Bounds = layout.PetBounds;
            listPanel.Bounds = layout.ListBounds;
            listPanel.Visible = todos.Data.Settings.ListVisible;
            ApplyHitRegion();
        }

        private void ApplyHitRegion()
        {
            Region hit = sprite.CreateHitRegion(sprite.Bounds);
            if (listPanel.Visible) hit.Union(listPanel.Bounds);
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
            if (!dragStarted && LayoutRules.IsDrag(mouseDownScreen, current, 5))
            {
                dragStarted = true;
                SetPetState(PetState.Drag);
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
                petAnchor = LayoutRules.SnapPetPosition(petAnchor, new Size(size, size), work, 12);
                SavePosition();
                ApplyLayout();
                dragStarted = false;
                SetPetState(PetState.Idle);
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
            rows.Controls.Clear();
            List<TodoItem> sorted = TodoRules.SortAndFilter(todos.Data.Todos, DateTime.Now, todos.Data.Settings.DayStartMinutes, todos.Data.Settings.OnlyToday);
            VisibleTodoResult visible = TodoRules.VisibleItems(sorted, todos.Data.Settings.ListExpanded, 5);
            if (visible.Items.Count == 0)
            {
                Label empty = new Label
                {
                    Text = todos.Data.Todos.Count == 0
                        ? "清单还是空的。\r\n点右上角「+」新建待办；\r\n点宠物可以折叠清单；\r\nAI 配置入口在设置中。"
                        : "这个筛选下没有待办。",
                    Size = new Size(300, 110),
                    ForeColor = Color.DimGray,
                    TextAlign = ContentAlignment.MiddleCenter
                };
                rows.Controls.Add(empty);
            }
            foreach (TodoItem item in visible.Items)
            {
                TodoRowControl row = new TodoRowControl(item, TodoRules.TimeLabel(item, DateTime.Now, todos.Data.Settings.DayStartMinutes));
                row.EditRequested += delegate(TodoItem value) { OpenEditor(value); };
                row.StarRequested += delegate(TodoItem value) { todos.ToggleStarred(value.Id); RefreshList(); };
                row.CompleteRequested += delegate(TodoItem value) { ToggleComplete(value, row); };
                row.DeleteRequested += delegate(TodoItem value) { Delete(value); };
                rows.Controls.Add(row);
            }
            moreButton.Visible = visible.HiddenCount > 0 || (todos.Data.Settings.ListExpanded && sorted.Count > 5);
            moreButton.Text = todos.Data.Settings.ListExpanded ? "收起" : string.Format("还有 {0} 条", visible.HiddenCount);
            moreButton.BringToFront();
            rows.ResumeLayout();
        }

        private void OpenEditor(TodoItem item)
        {
            using (TodoEditorForm editor = new TodoEditorForm(item))
            {
                if (editor.ShowDialog(this) != DialogResult.OK) return;
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
                SetPetState(PetState.Celebrate);
                completionTimer.Stop();
                completionTimer.Start();
            }
            else
            {
                completionTimer.Stop();
                SetPetState(PetState.Idle);
                RefreshList();
            }
        }

        private void Delete(TodoItem item)
        {
            todos.Delete(item.Id);
            lastDeletedId = item.Id;
            undoPanel.Visible = true;
            undoPanel.BringToFront();
            undoTimer.Stop();
            undoTimer.Start();
            RefreshList();
            undoPanel.Visible = true;
            undoPanel.BringToFront();
        }

        private void UndoDelete()
        {
            if (!string.IsNullOrEmpty(lastDeletedId)) todos.UndoDelete(lastDeletedId);
            HideUndo();
            RefreshList();
        }

        private void HideUndo()
        {
            undoTimer.Stop();
            undoPanel.Visible = false;
            lastDeletedId = null;
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

        private void SetPetState(PetState state)
        {
            sprite.State = state;
            ApplyHitRegion();
        }

        private void OpenSettings()
        {
            if (settingsForm != null && !settingsForm.IsDisposed)
            {
                settingsForm.Activate();
                return;
            }
            settingsForm = new SettingsForm(todos.Data.Settings);
            settingsForm.SettingsSaved += delegate
            {
                todos.SaveSettings();
                Opacity = LayoutRules.ClampOpacity(todos.Data.Settings.Opacity);
                ApplyLayout();
                RefreshList();
            };
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
            if (foreground == IntPtr.Zero || foreground == Handle) return false;
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
                screenLocked = false;
                sprite.Paused = !Visible;
            }
        }

        private void ShowFirstRunGuide()
        {
            DialogResult result = MessageBox.Show(
                this,
                "欢迎来到 Roost！\r\n\r\n• 点「+」新建待办\r\n• 点宠物折叠或展开清单\r\n• 托盘菜单里可以打开设置\r\n• AI 配置和开机自启已有入口，将在后续里程碑启用\r\n\r\n现在打开设置看看吗？",
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
