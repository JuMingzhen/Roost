using System;
using System.Collections.Generic;
using System.Globalization;

namespace Roost.Core
{
    public static class TodoRules
    {
        public static DateTime LogicalDate(DateTime now, int dayStartMinutes)
        {
            DateTime start = now.Date.AddMinutes(dayStartMinutes);
            return now < start ? now.Date.AddDays(-1) : now.Date;
        }

        public static DateTime LogicalDayStart(DateTime now, int dayStartMinutes)
        {
            return LogicalDate(now, dayStartMinutes).AddMinutes(dayStartMinutes);
        }

        public static bool IsOverdue(TodoItem item, DateTime now, int dayStartMinutes)
        {
            DateTime dueDate;
            if (!TryGetDueDate(item, out dueDate))
            {
                return false;
            }

            TimeSpan dueTime;
            if (TryGetDueTime(item, out dueTime))
            {
                return now > dueDate.Add(dueTime);
            }

            return now >= dueDate.AddDays(1).AddMinutes(dayStartMinutes);
        }

        public static bool IsInLogicalToday(TodoItem item, DateTime now, int dayStartMinutes)
        {
            DateTime dueDate;
            if (!TryGetDueDate(item, out dueDate))
            {
                return item.IsStarred;
            }

            if (IsOverdue(item, now, dayStartMinutes))
            {
                return true;
            }

            TimeSpan dueTime;
            if (TryGetDueTime(item, out dueTime))
            {
                DateTime start = LogicalDayStart(now, dayStartMinutes);
                DateTime due = dueDate.Add(dueTime);
                return due >= start && due < start.AddDays(1);
            }

            return dueDate.Date == LogicalDate(now, dayStartMinutes);
        }

        public static List<TodoItem> SortAndFilter(
            IEnumerable<TodoItem> source,
            DateTime now,
            int dayStartMinutes,
            bool onlyToday)
        {
            List<TodoItem> result = new List<TodoItem>();
            foreach (TodoItem item in source)
            {
                if (item == null || item.IsDeleted || item.IsCompleted)
                {
                    continue;
                }
                if (onlyToday && !IsInLogicalToday(item, now, dayStartMinutes))
                {
                    continue;
                }
                result.Add(item);
            }

            result.Sort(delegate(TodoItem left, TodoItem right)
            {
                int leftGroup = Group(left, now, dayStartMinutes);
                int rightGroup = Group(right, now, dayStartMinutes);
                int comparison = leftGroup.CompareTo(rightGroup);
                if (comparison != 0) return comparison;

                comparison = right.IsStarred.CompareTo(left.IsStarred);
                if (comparison != 0) return comparison;

                if (leftGroup <= 1)
                {
                    comparison = EffectiveDue(left, dayStartMinutes).CompareTo(EffectiveDue(right, dayStartMinutes));
                }
                else
                {
                    comparison = ParseUtc(right.CreatedAtUtc).CompareTo(ParseUtc(left.CreatedAtUtc));
                }
                if (comparison != 0) return comparison;
                return string.CompareOrdinal(left.Id, right.Id);
            });
            return result;
        }

        public static VisibleTodoResult VisibleItems(List<TodoItem> sorted, bool expanded, int limit)
        {
            int count = expanded ? sorted.Count : Math.Min(limit, sorted.Count);
            List<TodoItem> visible = sorted.GetRange(0, count);
            return new VisibleTodoResult(visible, sorted.Count - count);
        }

        public static string TimeLabel(TodoItem item, DateTime now, int dayStartMinutes)
        {
            DateTime dueDate;
            if (!TryGetDueDate(item, out dueDate)) return string.Empty;

            if (IsOverdue(item, now, dayStartMinutes))
            {
                DateTime logicalToday = LogicalDate(now, dayStartMinutes);
                int days = Math.Max(0, (logicalToday - dueDate.Date).Days);
                return days <= 0 ? "已过期" : string.Format("已过期 {0} 天", days);
            }

            int delta = (dueDate.Date - LogicalDate(now, dayStartMinutes)).Days;
            string dateText = delta == 0 ? "今天" : (delta == 1 ? "明天" : dueDate.ToString("M月d日"));
            TimeSpan dueTime;
            return TryGetDueTime(item, out dueTime)
                ? dateText + " " + DateTime.Today.Add(dueTime).ToString("HH:mm")
                : dateText;
        }

        public static bool TryGetDueDate(TodoItem item, out DateTime value)
        {
            return DateTime.TryParseExact(
                item == null ? null : item.DueDate,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out value);
        }

        public static bool TryGetDueTime(TodoItem item, out TimeSpan value)
        {
            return TimeSpan.TryParseExact(
                item == null ? null : item.DueTime,
                "hh\\:mm",
                CultureInfo.InvariantCulture,
                out value);
        }

        private static int Group(TodoItem item, DateTime now, int dayStartMinutes)
        {
            if (IsOverdue(item, now, dayStartMinutes)) return 0;
            DateTime ignored;
            return TryGetDueDate(item, out ignored) ? 1 : 2;
        }

        private static DateTime EffectiveDue(TodoItem item, int dayStartMinutes)
        {
            DateTime date;
            if (!TryGetDueDate(item, out date)) return DateTime.MaxValue;
            TimeSpan time;
            return TryGetDueTime(item, out time)
                ? date.Add(time)
                : date.AddDays(1).AddMinutes(dayStartMinutes);
        }

        private static DateTime ParseUtc(string value)
        {
            DateTime result;
            return DateTime.TryParse(value, null, DateTimeStyles.RoundtripKind, out result)
                ? result.ToUniversalTime()
                : DateTime.MinValue;
        }
    }
}

