using System;
using System.Collections.Generic;
using System.Web.Script.Serialization;

namespace Roost.Core
{
    public sealed class TodoItem
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Notes { get; set; }
        public string DueDate { get; set; }
        public string DueTime { get; set; }
        public bool IsStarred { get; set; }
        public bool IsCompleted { get; set; }
        public bool IsDeleted { get; set; }
        public string CreatedAtUtc { get; set; }
        public string UpdatedAtUtc { get; set; }
        public string CompletedAtUtc { get; set; }
        public string DeletedAtUtc { get; set; }
        // 提醒状态（PRD 第 10 节），都针对 ReminderKey 记下的那个待办时间（日期 + 时刻）；待办的时间改了，旧状态自动作废。
        // SnoozeUntil 是本地时间 yyyy-MM-dd HH:mm:ss。
        public string ReminderKey { get; set; }
        public bool ReminderDone { get; set; }
        public string SnoozeUntil { get; set; }

        public TodoItem()
        {
            Id = Guid.NewGuid().ToString("N");
            Title = string.Empty;
            Notes = string.Empty;
            CreatedAtUtc = DateTime.UtcNow.ToString("o");
            UpdatedAtUtc = CreatedAtUtc;
        }

        public TodoItem Clone()
        {
            return (TodoItem)MemberwiseClone();
        }
    }

    public sealed class RoostSettings
    {
        public bool OnlyToday { get; set; }
        public bool ListExpanded { get; set; }
        public bool ListVisible { get; set; }
        public bool HasSavedPosition { get; set; }
        public int PetX { get; set; }
        public int PetY { get; set; }
        public int SizeTier { get; set; }
        public double Opacity { get; set; }
        public int DayStartMinutes { get; set; }
        public bool FirstRunCompleted { get; set; }
        public string ToggleHotKey { get; set; }
        public string TalkHotKey { get; set; }
        public string AiPresetId { get; set; }
        public string AiBaseUrl { get; set; }
        public string AiModel { get; set; }
        public bool AiPrivacyAcknowledged { get; set; }
        public string AiCheckTarget { get; set; }
        public string AiCheckModel { get; set; }
        public bool AiCheckPassed { get; set; }
        public string AiCheckUtc { get; set; }
        // 有时刻的待办提前几分钟提醒（0 / 5 / 10 / 15 / 30）；只有日期的待办在当天几点提醒（从 0 点起的分钟数）。
        public int ReminderLeadMinutes { get; set; }
        public int DateReminderMinutes { get; set; }
        // 上一次检查提醒的本地时间，用来判断程序没运行或休眠期间错过的提醒。只在退出、休眠或其他保存时顺带写入。
        public string LastReminderCheck { get; set; }

        [ScriptIgnore]
        public bool AiConfigured
        {
            get { return !string.IsNullOrWhiteSpace(AiBaseUrl) && !string.IsNullOrWhiteSpace(AiModel); }
        }

        public RoostSettings()
        {
            ListExpanded = false;
            ListVisible = true;
            SizeTier = 2;
            Opacity = 1.0;
            DayStartMinutes = 4 * 60;
            ToggleHotKey = "Ctrl+Alt+H";
            TalkHotKey = "Ctrl+Alt+Space";
            ReminderLeadMinutes = 10;
            DateReminderMinutes = 9 * 60;
        }
    }

    public sealed class RoostData
    {
        public int FormatVersion { get; set; }
        public List<TodoItem> Todos { get; set; }
        public RoostSettings Settings { get; set; }

        public RoostData()
        {
            FormatVersion = 1;
            Todos = new List<TodoItem>();
            Settings = new RoostSettings();
        }
    }

    public sealed class VisibleTodoResult
    {
        public List<TodoItem> Items { get; private set; }
        public int HiddenCount { get; private set; }

        public VisibleTodoResult(List<TodoItem> items, int hiddenCount)
        {
            Items = items;
            HiddenCount = hiddenCount;
        }
    }
}
