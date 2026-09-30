using System;
using System.Collections.Generic;

namespace Roost.Core
{
    public sealed class ReminderTick
    {
        public ReminderTick()
        {
            Fired = new List<TodoItem>();
        }

        // 这次新冒出来的提醒（已加入 Active）。
        public List<TodoItem> Fired { get; private set; }

        // 程序没运行或休眠期间错过的提醒条数，已记为处理过，只用于汇总泡。
        public int MissedCount { get; set; }
    }

    // 维护正在冒泡、等用户回应的提醒，处理「完成 / 稍后提醒 / 知道了」（PRD 10.2、10.4）。
    // 时间都由调用方传入，测试用虚构时钟推进。
    public sealed class ReminderService
    {
        private readonly TodoService todos;
        private readonly List<string> active = new List<string>();
        private DateTime? lastCheck;

        public ReminderService(TodoService todos)
        {
            if (todos == null) throw new ArgumentNullException("todos");
            this.todos = todos;
            lastCheck = ReminderRules.ParseCheck(todos.Data.Settings.LastReminderCheck);
        }

        // 按冒泡先后排列。
        public List<TodoItem> Active
        {
            get
            {
                List<TodoItem> items = new List<TodoItem>();
                foreach (string id in active)
                {
                    TodoItem item = todos.Find(id);
                    if (item != null) items.Add(item);
                }
                return items;
            }
        }

        public ReminderTick Tick(DateTime now)
        {
            Prune(now);
            RoostSettings settings = todos.Data.Settings;
            ReminderTick tick = new ReminderTick();
            List<string> missed = new List<string>();
            List<TodoItem> due = new List<TodoItem>();
            foreach (TodoItem item in todos.Data.Todos)
            {
                if (active.Contains(item.Id)) continue;
                ReminderVerdict verdict = ReminderRules.Classify(item, settings, now, lastCheck);
                if (verdict == ReminderVerdict.Fire) due.Add(item);
                else if (verdict == ReminderVerdict.Missed) missed.Add(item.Id);
            }
            due.Sort(delegate(TodoItem left, TodoItem right)
            {
                int comparison = ReminderRules.FireMoment(left, settings).Value.CompareTo(ReminderRules.FireMoment(right, settings).Value);
                return comparison != 0 ? comparison : string.CompareOrdinal(left.Id, right.Id);
            });
            foreach (TodoItem item in due)
            {
                active.Add(item.Id);
                tick.Fired.Add(item);
            }
            if (missed.Count > 0) todos.MarkRemindersDone(missed);
            tick.MissedCount = missed.Count;
            lastCheck = now;
            // 只改内存；退出、休眠或其他保存时顺带写入，避免每次检查都写文件和备份。
            settings.LastReminderCheck = ReminderRules.FormatSnooze(now);
            return tick;
        }

        // 下一次需要检查的时间（没有待提醒的返回 null）。
        public DateTime? NextFire(DateTime now)
        {
            DateTime? next = null;
            foreach (TodoItem item in todos.Data.Todos)
            {
                if (active.Contains(item.Id)) continue;
                DateTime? fire = ReminderRules.FireMoment(item, todos.Data.Settings);
                if (fire.HasValue && fire.Value > now && (!next.HasValue || fire.Value < next.Value)) next = fire;
            }
            return next;
        }

        public void Complete(string id)
        {
            active.Remove(id);
            TodoItem item = todos.Find(id);
            if (item != null && !item.IsDeleted && !item.IsCompleted) todos.ToggleCompleted(id);
        }

        public void Snooze(string id, TimeSpan delay, DateTime now)
        {
            active.Remove(id);
            todos.SnoozeReminder(id, now.Add(delay));
        }

        public void Dismiss(string id)
        {
            active.Remove(id);
            todos.MarkRemindersDone(new string[] { id });
        }

        // 已完成、已删除、或改了时间还没到点的，不再留在提醒泡里。
        public void Prune(DateTime now)
        {
            RoostSettings settings = todos.Data.Settings;
            active.RemoveAll(delegate(string id)
            {
                TodoItem item = todos.Find(id);
                DateTime? fire = item == null ? null : ReminderRules.FireMoment(item, settings);
                return !fire.HasValue || fire.Value > now;
            });
        }
    }
}
