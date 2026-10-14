using System;
using System.Globalization;

namespace Roost.Core
{
    public enum ReminderVerdict
    {
        // 不提醒：没到点、已处理、没有时间，或事情本身早就过了点。
        None,
        // 现在就冒泡提醒。
        Fire,
        // 程序没运行或休眠期间到点、事情也已过点：只计入「你不在的时候有 X 件事到点了」的汇总。
        Missed
    }

    // PRD 第 10 节的提醒规则。所有时间都是本地时间，「今天」和过期的判断与 TodoRules 共用一天起点（PRD 7.5）。
    public static class ReminderRules
    {
        public static readonly int[] LeadChoices = new int[] { 0, 5, 10, 15, 30 };

        // 到点后隔了这么久才检查到（休眠、没运行），算作错过。
        public static readonly TimeSpan Grace = TimeSpan.FromMinutes(2);

        private const string SnoozeFormat = "yyyy-MM-dd HH:mm:ss";

        // 待办本来的提醒时间：有时刻的提前几分钟；只有日期的在当天设定时刻（不早于一天起点）。
        public static DateTime? BaseMoment(TodoItem item, RoostSettings settings)
        {
            if (item == null || item.IsCompleted || item.IsDeleted) return null;
            DateTime date;
            if (!TodoRules.TryGetDueDate(item, out date)) return null;
            TimeSpan time;
            if (TodoRules.TryGetDueTime(item, out time)) return date.Add(time).AddMinutes(-settings.ReminderLeadMinutes);
            return date.AddMinutes(Math.Max(settings.DateReminderMinutes, settings.DayStartMinutes));
        }

        // 提醒状态对应的是待办本身的时间（日期 + 时刻），而不是提醒时间：
        // 改了待办的时间，旧状态作废、重新提醒；只改设置里的提前量，已处理的提醒不会再冒出来。
        public static string Key(TodoItem item)
        {
            return (item.DueDate ?? string.Empty) + " " + (item.DueTime ?? string.Empty);
        }

        public static string FormatSnooze(DateTime value)
        {
            return value.ToString(SnoozeFormat, CultureInfo.InvariantCulture);
        }

        // 下一次该冒泡的时间：稍后提醒优先；已经「知道了」的不再提醒。状态属于旧的提醒时间时视为作废。
        public static DateTime? FireMoment(TodoItem item, RoostSettings settings)
        {
            DateTime? baseMoment = BaseMoment(item, settings);
            if (!baseMoment.HasValue) return null;
            if (item.ReminderKey != Key(item)) return baseMoment;
            if (item.ReminderDone) return null;
            DateTime snooze;
            if (!string.IsNullOrEmpty(item.SnoozeUntil) &&
                DateTime.TryParseExact(item.SnoozeUntil, SnoozeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out snooze))
                return snooze;
            return baseMoment;
        }

        public static bool IsSnoozed(TodoItem item, RoostSettings settings)
        {
            DateTime? baseMoment = BaseMoment(item, settings);
            return baseMoment.HasValue && item.ReminderKey == Key(item) && !item.ReminderDone && !string.IsNullOrEmpty(item.SnoozeUntil);
        }

        // lastCheck 是上一次检查的时间（没有记录时传 null，视为刚检查过）。
        public static ReminderVerdict Classify(TodoItem item, RoostSettings settings, DateTime now, DateTime? lastCheck)
        {
            DateTime? fire = FireMoment(item, settings);
            if (!fire.HasValue || fire.Value > now) return ReminderVerdict.None;
            bool eventPassed = TodoRules.IsOverdue(item, now, settings.DayStartMinutes);
            bool unseen = lastCheck.HasValue && fire.Value > lastCheck.Value;
            bool late = fire.Value < now - Grace;
            if (unseen)
            {
                // 按时检查到的照常提醒；隔了很久才检查到、事情也过了点的，归入错过汇总。
                if (late && eventPassed) return ReminderVerdict.Missed;
                return ReminderVerdict.Fire;
            }
            // 上次检查时就已经到点：新建或改时间时提醒时间已过、上次退出前没回应的提醒、到点的稍后提醒。
            // 事情本身已经过了点的不再提醒（它已按过期规则排在清单最前），稍后提醒除外。
            if (eventPassed && !IsSnoozed(item, settings)) return ReminderVerdict.None;
            return ReminderVerdict.Fire;
        }

        public static DateTime? ParseCheck(string value)
        {
            DateTime parsed;
            return DateTime.TryParseExact(value, SnoozeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed)
                ? parsed
                : (DateTime?)null;
        }
    }
}
