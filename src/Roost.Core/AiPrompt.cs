using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Roost.Core
{
    public sealed class AiRequestContext
    {
        public const int CompletedLookbackDays = 7;
        public const int MaxNotesLength = 100;
        public const int CalendarDays = 14;

        private static readonly string[] Weekdays = { "星期日", "星期一", "星期二", "星期三", "星期四", "星期五", "星期六" };

        private readonly Dictionary<string, TodoItem> byAlias;

        public DateTime Now { get; private set; }
        public int DayStartMinutes { get; private set; }
        public List<KeyValuePair<string, TodoItem>> Items { get; private set; }

        private AiRequestContext(DateTime now, int dayStartMinutes)
        {
            Now = now;
            DayStartMinutes = dayStartMinutes;
            Items = new List<KeyValuePair<string, TodoItem>>();
            byAlias = new Dictionary<string, TodoItem>(StringComparer.OrdinalIgnoreCase);
        }

        public static AiRequestContext Create(IEnumerable<TodoItem> todos, DateTime now, int dayStartMinutes)
        {
            AiRequestContext context = new AiRequestContext(now, dayStartMinutes);
            List<TodoItem> all = new List<TodoItem>(todos);
            foreach (TodoItem item in TodoRules.SortAndFilter(all, now, dayStartMinutes, false))
                context.Add(item);

            DateTime cutoffUtc = now.ToUniversalTime().AddDays(-CompletedLookbackDays);
            List<TodoItem> recent = new List<TodoItem>();
            foreach (TodoItem item in all)
            {
                if (item == null || item.IsDeleted || !item.IsCompleted) continue;
                DateTime completed;
                if (DateTime.TryParse(item.CompletedAtUtc, null, DateTimeStyles.RoundtripKind, out completed) &&
                    completed.ToUniversalTime() >= cutoffUtc)
                    recent.Add(item);
            }
            recent.Sort(delegate(TodoItem left, TodoItem right)
            {
                return string.CompareOrdinal(right.CompletedAtUtc, left.CompletedAtUtc);
            });
            foreach (TodoItem item in recent) context.Add(item);
            return context;
        }

        public TodoItem Resolve(string alias)
        {
            TodoItem item;
            return alias != null && byAlias.TryGetValue(alias.Trim(), out item) ? item : null;
        }

        public static string WeekdayName(DateTime date)
        {
            return Weekdays[(int)date.DayOfWeek];
        }

        public string SystemPrompt()
        {
            DateTime today = TodoRules.LogicalDate(Now, DayStartMinutes);
            StringBuilder text = new StringBuilder();
            text.AppendLine("你是桌面待办应用 Roost 的助手。把用户的一句话转换成对待办清单的操作。只输出一个 JSON 对象，不要输出任何其他文字。");
            text.AppendLine();
            text.AppendLine("## 时间");
            text.AppendFormat("现在是 {0} {1}。", Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture), WeekdayName(Now)).AppendLine();
            text.AppendFormat("一天从 {0} 开始，此前仍算前一天。因此「今天」是 {1}，「明天」是 {2}。",
                DateTime.Today.AddMinutes(DayStartMinutes).ToString("HH:mm", CultureInfo.InvariantCulture),
                Date(today), Date(today.AddDays(1))).AppendLine();
            text.AppendLine("一周从星期一开始；「这周X」指本周，「下周X」指下一周。日期对照表：");
            DateTime monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
            for (int offset = 0; offset < CalendarDays; offset++)
            {
                DateTime day = today.AddDays(offset);
                int week = (int)((day - monday).TotalDays) / 7;
                string weekLabel = week == 0 ? "本周" : (week == 1 ? "下周" : "下下周");
                string relative = offset == 0 ? "，今天" : (offset == 1 ? "，明天" : (offset == 2 ? "，后天" : string.Empty));
                text.AppendFormat("- {0}（{1}{2}{3}）", Date(day), weekLabel, WeekdayName(day).Substring(2), relative).AppendLine();
            }
            text.AppendLine("上午/早上指 AM；下午/晚上指 PM，例如「下午三点」是 15:00，「晚上八点」是 20:00，「中午」是 12:00。");
            text.AppendLine();
            text.AppendLine("## 当前待办");
            if (Items.Count == 0) text.AppendLine("（清单为空）");
            foreach (KeyValuePair<string, TodoItem> entry in Items)
            {
                TodoItem item = entry.Value;
                text.AppendFormat("- id={0}｜{1}｜标题：{2}｜时间：{3}｜{4}",
                    entry.Key,
                    item.IsCompleted ? "已完成" : "未完成",
                    item.Title,
                    DueText(item),
                    item.IsStarred ? "有星标" : "无星标");
                string notes = item.Notes ?? string.Empty;
                if (notes.Length > 0)
                    text.Append("｜备注：").Append(notes.Length <= MaxNotesLength ? notes : notes.Substring(0, MaxNotesLength) + "…");
                text.AppendLine();
            }
            text.AppendLine();
            text.AppendLine("## 输出格式");
            text.AppendLine("正常情况输出 {\"status\":\"ok\",\"operations\":[...]}，operations 中每一项是下列之一：");
            text.AppendLine("- {\"op\":\"add\",\"title\":\"标题\",\"notes\":\"备注，可省略\",\"date\":\"YYYY-MM-DD 或 null\",\"time\":\"HH:mm 或 null\",\"starred\":true 或 false}");
            text.AppendLine("- {\"op\":\"update\",\"id\":\"t1\", 只写要改的字段：\"title\"、\"notes\"、\"date\"、\"time\"}");
            text.AppendLine("- {\"op\":\"complete\",\"id\":\"t1\"}：标记完成");
            text.AppendLine("- {\"op\":\"uncomplete\",\"id\":\"t1\"}：取消完成");
            text.AppendLine("- {\"op\":\"star\",\"id\":\"t1\"} / {\"op\":\"unstar\",\"id\":\"t1\"}：设置 / 取消星标");
            text.AppendLine("- {\"op\":\"delete\",\"id\":\"t1\"}：删除");
            text.AppendLine("听不懂、或这句话不是在安排待办时，输出 {\"status\":\"unclear\",\"message\":\"简短说明原因\"}。");
            text.AppendLine("一句话对应到清单里多条待办、无法确定是哪条时，输出 {\"status\":\"ambiguous\",\"message\":\"简短说明\",\"candidates\":[\"t1\",\"t2\"]}，不要输出任何操作。");
            text.AppendLine();
            text.AppendLine("## 规则");
            text.AppendLine("1. 只改用户明确提到的待办，没提到的待办一律不动。");
            text.AppendLine("2. 「做完了」「搞定了」「完成了」是 complete，绝对不能当成 delete。只有用户明确说删除、不要了、取消这件事时才用 delete。");
            text.AppendLine("3. 相对时间必须换算成具体日期（YYYY-MM-DD）和时刻（HH:mm）。只说日期没说时刻时，time 为 null。没有提到时间的新待办，date 和 time 都为 null。");
            text.AppendLine("4. 修改时间时，date 和 time 都要给出；只改时刻时也要写上原来的日期。要去掉时间，写 \"date\":null,\"time\":null。");
            text.AppendLine("5. 用户说「很重要」「重要」「优先」时设置星标：新增的待办写 \"starred\":true，已有待办用 star。说「不急了」「不重要了」时用 unstar。");
            text.AppendLine("6. 用户要操作的待办不在清单里时，仍然输出这个操作，但 \"id\" 写 null，并用 \"ref\" 写出用户说的名称。");
            text.AppendLine("7. 一句话里可以有多个操作，按用户说的顺序输出。");
            text.AppendLine("8. 待办标题要简短，去掉时间和「很重要」这类修饰。");
            return text.ToString();
        }

        private void Add(TodoItem item)
        {
            string alias = "t" + (Items.Count + 1).ToString(CultureInfo.InvariantCulture);
            Items.Add(new KeyValuePair<string, TodoItem>(alias, item));
            byAlias[alias] = item;
        }

        private static string Date(DateTime date)
        {
            return date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + " " + WeekdayName(date);
        }

        private static string DueText(TodoItem item)
        {
            if (string.IsNullOrEmpty(item.DueDate)) return "无";
            return string.IsNullOrEmpty(item.DueTime) ? item.DueDate : item.DueDate + " " + item.DueTime;
        }
    }
}
