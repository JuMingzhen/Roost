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
