using System;
using System.Collections.Generic;

namespace Roost.Core
{
    public sealed class TodoService
    {
        private readonly TodoRepository repository;

        public RoostData Data { get; private set; }

        public TodoService(TodoRepository repository)
        {
            if (repository == null) throw new ArgumentNullException("repository");
            this.repository = repository;
            Data = repository.Load();
        }

        public TodoItem Create(string title, string notes, string dueDate, string dueTime, bool starred)
        {
            string normalizedTitle = NormalizeRequiredTitle(title);
            ValidateDateAndTime(dueDate, dueTime);
            TodoItem item = new TodoItem
            {
                Title = normalizedTitle,
                Notes = notes == null ? string.Empty : notes.Trim(),
                DueDate = EmptyToNull(dueDate),
                DueTime = EmptyToNull(dueTime),
                IsStarred = starred
            };
            Data.Todos.Add(item);
            Save();
            return item;
        }

        public void Update(string id, string title, string notes, string dueDate, string dueTime, bool starred)
        {
            TodoItem item = Required(id);
            ValidateDateAndTime(dueDate, dueTime);
            item.Title = NormalizeRequiredTitle(title);
            item.Notes = notes == null ? string.Empty : notes.Trim();
            item.DueDate = EmptyToNull(dueDate);
            item.DueTime = EmptyToNull(dueTime);
            item.IsStarred = starred;
            Touch(item);
            Save();
        }

        public bool ToggleCompleted(string id)
        {
            TodoItem item = Required(id);
            item.IsCompleted = !item.IsCompleted;
            item.CompletedAtUtc = item.IsCompleted ? DateTime.UtcNow.ToString("o") : null;
            Touch(item);
            Save();
            return item.IsCompleted;
        }

        public bool ToggleStarred(string id)
        {
            TodoItem item = Required(id);
            item.IsStarred = !item.IsStarred;
            Touch(item);
            Save();
            return item.IsStarred;
        }

        public void Delete(string id)
        {
            TodoItem item = Required(id);
            item.IsDeleted = true;
            item.DeletedAtUtc = DateTime.UtcNow.ToString("o");
            Touch(item);
            Save();
        }

        public void UndoDelete(string id)
        {
            TodoItem item = Find(id);
            if (item == null || !item.IsDeleted) throw new InvalidOperationException("没有可撤销的删除操作。");
            item.IsDeleted = false;
            item.DeletedAtUtc = null;
            Touch(item);
            Save();
        }

        public AiUndo ApplyAi(IEnumerable<AiOperation> operations)
        {
            AiUndo undo = new AiUndo();
            HashSet<string> captured = new HashSet<string>();
            foreach (AiOperation operation in operations)
            {
                if (operation == null || !operation.IsValid) continue;
                if (operation.Kind == AiOperationKind.Add)
                {
                    TodoItem created = new TodoItem
                    {
                        Title = operation.Title,
                        Notes = operation.Notes ?? string.Empty,
                        DueDate = operation.DueDate,
                        DueTime = operation.DueTime,
                        IsStarred = operation.Starred
                    };
                    Data.Todos.Add(created);
                    undo.CreatedIds.Add(created.Id);
                    continue;
                }

                TodoItem item = Find(operation.TargetId);
                if (item == null || item.IsDeleted) continue;
                if (captured.Add(item.Id)) undo.Before.Add(item.Clone());
                switch (operation.Kind)
                {
                    case AiOperationKind.Update:
                        if (operation.HasTitle) item.Title = operation.Title;
                        if (operation.HasNotes) item.Notes = operation.Notes ?? string.Empty;
                        if (operation.HasDue)
                        {
                            item.DueDate = operation.DueDate;
                            item.DueTime = operation.DueTime;
                        }
                        break;
                    case AiOperationKind.Complete:
                        item.IsCompleted = true;
                        item.CompletedAtUtc = DateTime.UtcNow.ToString("o");
                        break;
                    case AiOperationKind.Uncomplete:
                        item.IsCompleted = false;
                        item.CompletedAtUtc = null;
                        break;
                    case AiOperationKind.Star:
                        item.IsStarred = true;
                        break;
                    case AiOperationKind.Unstar:
                        item.IsStarred = false;
                        break;
                    case AiOperationKind.Delete:
                        item.IsDeleted = true;
                        item.DeletedAtUtc = DateTime.UtcNow.ToString("o");
                        break;
                }
                Touch(item);
            }
            Save();
            return undo;
        }

        public void UndoAi(AiUndo undo)
        {
            if (undo == null) throw new ArgumentNullException("undo");
            Data.Todos.RemoveAll(delegate(TodoItem item) { return undo.CreatedIds.Contains(item.Id); });
            foreach (TodoItem before in undo.Before)
            {
                int index = Data.Todos.FindIndex(delegate(TodoItem item) { return item.Id == before.Id; });
                if (index >= 0) Data.Todos[index] = before.Clone();
            }
            Save();
        }

        public void SaveSettings()
        {
            Save();
        }

        public TodoItem Find(string id)
        {
            foreach (TodoItem item in Data.Todos)
            {
                if (item.Id == id) return item;
            }
            return null;
        }

        private TodoItem Required(string id)
        {
            TodoItem item = Find(id);
            if (item == null || item.IsDeleted) throw new KeyNotFoundException("待办不存在。");
            return item;
        }

        private void Save()
        {
            repository.Save(Data);
        }

        private static void Touch(TodoItem item)
        {
            item.UpdatedAtUtc = DateTime.UtcNow.ToString("o");
        }

        private static string NormalizeRequiredTitle(string title)
        {
            string value = title == null ? string.Empty : title.Trim();
            if (value.Length == 0) throw new ArgumentException("标题不能为空。");
            return value;
        }

        private static string EmptyToNull(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static void ValidateDateAndTime(string dueDate, string dueTime)
        {
            string date = EmptyToNull(dueDate);
            string time = EmptyToNull(dueTime);
            if (time != null && date == null) throw new ArgumentException("设置时刻前必须先选择日期。");
            TodoItem probe = new TodoItem { DueDate = date, DueTime = time };
            DateTime ignoredDate;
            TimeSpan ignoredTime;
            if (date != null && !TodoRules.TryGetDueDate(probe, out ignoredDate))
                throw new ArgumentException("日期格式无效。");
            if (time != null && !TodoRules.TryGetDueTime(probe, out ignoredTime))
                throw new ArgumentException("时刻格式无效。");
        }
    }

    public sealed class AiUndo
    {
        public List<TodoItem> Before { get; private set; }
        public List<string> CreatedIds { get; private set; }

        public AiUndo()
        {
            Before = new List<TodoItem>();
            CreatedIds = new List<string>();
        }
    }
}
