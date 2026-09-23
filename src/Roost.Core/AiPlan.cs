using System;
using System.Collections.Generic;
using System.Text;
using System.Web.Script.Serialization;

namespace Roost.Core
{
    public enum AiOperationKind
    {
        Add,
        Update,
        Complete,
        Uncomplete,
        Star,
        Unstar,
        Delete
    }

    public enum AiPlanStatus
    {
        Ok,
        Unclear,
        Ambiguous
    }

    public sealed class AiOperation
    {
        public AiOperationKind Kind { get; set; }
        public string TargetId { get; set; }
        public string TargetAlias { get; set; }
        public string TargetTitle { get; set; }
        public string Title { get; set; }
        public bool HasTitle { get; set; }
        public string Notes { get; set; }
        public bool HasNotes { get; set; }
        public string DueDate { get; set; }
        public string DueTime { get; set; }
        public bool HasDue { get; set; }
        public bool Starred { get; set; }
        public string InvalidReason { get; set; }
        public bool PastTimeWarning { get; set; }
        public string Summary { get; set; }

        public bool IsValid { get { return InvalidReason == null; } }
    }

    public sealed class AiPlan
    {
        public AiPlanStatus Status { get; set; }
        public string Message { get; set; }
        public List<AiOperation> Operations { get; private set; }
        public List<string> CandidateTitles { get; private set; }

        public AiPlan()
        {
            Operations = new List<AiOperation>();
            CandidateTitles = new List<string>();
        }

        public int ValidCount
        {
            get
            {
                int count = 0;
                foreach (AiOperation operation in Operations) if (operation.IsValid) count++;
                return count;
            }
        }

        public string Headline()
        {
            int add = 0, update = 0, delete = 0, other = 0, invalid = 0;
            foreach (AiOperation operation in Operations)
            {
                if (!operation.IsValid) { invalid++; continue; }
                if (operation.Kind == AiOperationKind.Add) add++;
                else if (operation.Kind == AiOperationKind.Update) update++;
                else if (operation.Kind == AiOperationKind.Delete) delete++;
                else other++;
            }
            List<string> parts = new List<string>();
            if (delete > 0) parts.Add(string.Format("删除 {0} 条", delete));
            if (add > 0) parts.Add(string.Format("新增 {0} 条", add));
            if (update > 0) parts.Add(string.Format("修改 {0} 条", update));
            if (other > 0) parts.Add(string.Format("状态变更 {0} 条", other));
            if (invalid > 0) parts.Add(string.Format("无效 {0} 条", invalid));
            return string.Join("，", parts.ToArray());
        }
    }

    public static class AiPlanParser
    {
        public const int MaxShownReplyLength = 200;
        private const int MaxTitleLength = 200;

        public static AiPlan Parse(string content, AiRequestContext context)
        {
            Dictionary<string, object> root = ExtractJson(content);
            if (root == null) return Unclear(TruncateReply(content));

            string status = GetString(root, "status");
            string message = GetString(root, "message");
            if (status == "ambiguous")
            {
                AiPlan ambiguous = new AiPlan { Status = AiPlanStatus.Ambiguous, Message = message };
                object[] candidates = root.ContainsKey("candidates") ? root["candidates"] as object[] : null;
                if (candidates != null)
                {
                    foreach (object candidate in candidates)
                    {
                        TodoItem item = context.Resolve(candidate as string);
                        if (item != null && !ambiguous.CandidateTitles.Contains(item.Title))
                            ambiguous.CandidateTitles.Add(item.Title);
                    }
                }
                return ambiguous;
            }
            if (status != "ok")
            {
                return Unclear(string.IsNullOrEmpty(message) ? TruncateReply(content) : TruncateReply(message));
            }

            object[] rawOperations = root.ContainsKey("operations") ? root["operations"] as object[] : null;
            if (rawOperations == null || rawOperations.Length == 0)
                return Unclear("没有识别出需要改动的待办。");

            AiPlan plan = new AiPlan { Status = AiPlanStatus.Ok, Message = message };
            HashSet<string> deletedTargets = new HashSet<string>();
            HashSet<string> seen = new HashSet<string>();
            foreach (object raw in rawOperations)
            {
                AiOperation operation = ReadOperation(raw as Dictionary<string, object>, context);
                if (operation.IsValid && operation.TargetId != null)
                {
                    string key = operation.Kind + "|" + operation.TargetId;
                    if (deletedTargets.Contains(operation.TargetId))
                        operation.InvalidReason = "同一句话里这条待办已被删除。";
                    else if (!seen.Add(key))
                        operation.InvalidReason = "重复的操作。";
                    else if (operation.Kind == AiOperationKind.Delete)
                        deletedTargets.Add(operation.TargetId);
                }
                operation.Summary = Describe(operation, context);
                plan.Operations.Add(operation);
            }
            return plan;
        }

        public static string TruncateReply(string text)
        {
            string value = text == null ? string.Empty : text.Trim();
            if (value.Length == 0) return "模型没有给出可以理解的回复。";
            return value.Length <= MaxShownReplyLength ? value : value.Substring(0, MaxShownReplyLength) + "…";
        }

        private static AiPlan Unclear(string message)
        {
            return new AiPlan { Status = AiPlanStatus.Unclear, Message = message };
        }

        private static Dictionary<string, object> ExtractJson(string content)
        {
            if (string.IsNullOrEmpty(content)) return null;
            int start = content.IndexOf('{');
            int end = content.LastIndexOf('}');
            if (start < 0 || end <= start) return null;
            try
            {
                return new JavaScriptSerializer().DeserializeObject(content.Substring(start, end - start + 1)) as Dictionary<string, object>;
            }
            catch (ArgumentException)
            {
                return null;
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }

        private static AiOperation ReadOperation(Dictionary<string, object> raw, AiRequestContext context)
        {
            AiOperation operation = new AiOperation();
            if (raw == null)
            {
                operation.InvalidReason = "无法识别的操作。";
                return operation;
            }

            string kind = GetString(raw, "op");
            switch (kind)
            {
                case "add": operation.Kind = AiOperationKind.Add; break;
                case "update": operation.Kind = AiOperationKind.Update; break;
                case "complete": operation.Kind = AiOperationKind.Complete; break;
                case "uncomplete": operation.Kind = AiOperationKind.Uncomplete; break;
                case "star": operation.Kind = AiOperationKind.Star; break;
                case "unstar": operation.Kind = AiOperationKind.Unstar; break;
                case "delete": operation.Kind = AiOperationKind.Delete; break;
                default:
                    operation.InvalidReason = "无法识别的操作。";
                    return operation;
            }

            if (operation.Kind == AiOperationKind.Add)
            {
                operation.HasTitle = true;
                operation.Title = Clean(GetString(raw, "title"), MaxTitleLength);
                operation.HasNotes = true;
                operation.Notes = Clean(GetString(raw, "notes"), int.MaxValue);
                operation.HasDue = true;
                operation.DueDate = EmptyToNull(GetString(raw, "date"));
                operation.DueTime = EmptyToNull(GetString(raw, "time"));
                operation.Starred = raw.ContainsKey("starred") && raw["starred"] is bool && (bool)raw["starred"];
                if (operation.Title.Length == 0) operation.InvalidReason = "新增的待办没有标题。";
                else ValidateDue(operation, context);
                return operation;
            }

            operation.TargetAlias = GetString(raw, "id");
            TodoItem target = context.Resolve(operation.TargetAlias);
            if (target == null)
            {
                string reference = Clean(GetString(raw, "ref"), MaxTitleLength);
                operation.TargetTitle = reference.Length > 0 ? reference : null;
                operation.InvalidReason = "清单里找不到这条待办。";
                return operation;
            }
            operation.TargetId = target.Id;
            operation.TargetTitle = target.Title;

            switch (operation.Kind)
            {
                case AiOperationKind.Uncomplete:
                    if (!target.IsCompleted) operation.InvalidReason = "这条待办还没有完成。";
                    return operation;
                case AiOperationKind.Complete:
                    if (target.IsCompleted) operation.InvalidReason = "这条待办已经完成了。";
                    return operation;
            }
            if (target.IsCompleted)
            {
                operation.InvalidReason = "这条待办已经完成了。";
                return operation;
            }
            if (operation.Kind == AiOperationKind.Star && target.IsStarred)
                operation.InvalidReason = "这条待办已经有星标。";
            else if (operation.Kind == AiOperationKind.Unstar && !target.IsStarred)
                operation.InvalidReason = "这条待办本来就没有星标。";
            else if (operation.Kind == AiOperationKind.Update)
                ReadUpdate(operation, raw, target, context);
            return operation;
        }

        private static void ReadUpdate(AiOperation operation, Dictionary<string, object> raw, TodoItem target, AiRequestContext context)
        {
            if (raw.ContainsKey("title"))
            {
                operation.HasTitle = true;
                operation.Title = Clean(GetString(raw, "title"), MaxTitleLength);
                if (operation.Title.Length == 0)
                {
                    operation.InvalidReason = "标题不能改成空的。";
                    return;
                }
            }
            if (raw.ContainsKey("notes"))
            {
                operation.HasNotes = true;
                operation.Notes = Clean(GetString(raw, "notes"), int.MaxValue);
            }
            if (raw.ContainsKey("date") || raw.ContainsKey("time"))
            {
                operation.HasDue = true;
                operation.DueDate = raw.ContainsKey("date") ? EmptyToNull(GetString(raw, "date")) : target.DueDate;
                operation.DueTime = raw.ContainsKey("time") ? EmptyToNull(GetString(raw, "time")) : null;
                ValidateDue(operation, context);
                if (!operation.IsValid) return;
            }

            bool changed =
                (operation.HasTitle && operation.Title != target.Title) ||
                (operation.HasNotes && operation.Notes != (target.Notes ?? string.Empty)) ||
                (operation.HasDue && (operation.DueDate != target.DueDate || operation.DueTime != target.DueTime));
            if (!changed) operation.InvalidReason = "没有要修改的内容。";
        }

        private static void ValidateDue(AiOperation operation, AiRequestContext context)
        {
            TodoItem probe = new TodoItem { DueDate = operation.DueDate, DueTime = operation.DueTime };
            DateTime date;
            TimeSpan time;
            if (operation.DueDate != null && !TodoRules.TryGetDueDate(probe, out date))
                operation.InvalidReason = "日期格式无效。";
            else if (operation.DueTime != null && !TodoRules.TryGetDueTime(probe, out time))
                operation.InvalidReason = "时刻格式无效。";
            else if (operation.DueTime != null && operation.DueDate == null)
                operation.InvalidReason = "有时刻却没有日期。";
            else if (operation.DueDate != null)
                operation.PastTimeWarning = TodoRules.IsOverdue(probe, context.Now, context.DayStartMinutes);
        }

        private static string Describe(AiOperation operation, AiRequestContext context)
        {
            string target = operation.TargetTitle == null ? "（未知待办）" : "「" + operation.TargetTitle + "」";
            switch (operation.Kind)
            {
                case AiOperationKind.Add:
                    return "新增：" + DescribeDue(operation.DueDate, operation.DueTime, context) + " " +
                           operation.Title + (operation.Starred ? " ★" : string.Empty);
                case AiOperationKind.Complete: return "标记完成：" + target;
                case AiOperationKind.Uncomplete: return "取消完成：" + target;
                case AiOperationKind.Star: return "加星标：" + target;
                case AiOperationKind.Unstar: return "取消星标：" + target;
                case AiOperationKind.Delete: return "删除：" + target;
            }

            List<string> changes = new List<string>();
            if (operation.HasTitle) changes.Add("标题改为「" + operation.Title + "」");
            if (operation.HasDue) changes.Add("时间改为 " + DescribeDue(operation.DueDate, operation.DueTime, context));
            if (operation.HasNotes) changes.Add(operation.Notes.Length == 0 ? "清空备注" : "备注改为「" + operation.Notes + "」");
            return "修改" + target + (changes.Count == 0 ? string.Empty : "：" + string.Join("；", changes.ToArray()));
        }

        public static string DescribeDue(string dueDate, string dueTime, AiRequestContext context)
        {
            TodoItem probe = new TodoItem { DueDate = dueDate, DueTime = dueTime };
            DateTime date;
            if (!TodoRules.TryGetDueDate(probe, out date)) return "无期限";
            StringBuilder text = new StringBuilder();
            text.Append(date.ToString("M月d日")).Append(' ').Append(AiRequestContext.WeekdayName(date));
            TimeSpan time;
            if (TodoRules.TryGetDueTime(probe, out time)) text.Append(' ').Append(DateTime.Today.Add(time).ToString("HH:mm"));
            int delta = (date.Date - TodoRules.LogicalDate(context.Now, context.DayStartMinutes)).Days;
            if (delta == 0) text.Append("（今天）");
            else if (delta == 1) text.Append("（明天）");
            else if (delta == 2) text.Append("（后天）");
            return text.ToString();
        }

        private static string GetString(Dictionary<string, object> raw, string key)
        {
            object value;
            if (!raw.TryGetValue(key, out value) || value == null) return null;
            return value as string ?? Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
        }

        private static string Clean(string value, int maximum)
        {
            string text = value == null ? string.Empty : value.Trim();
            return text.Length <= maximum ? text : text.Substring(0, maximum);
        }

        private static string EmptyToNull(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }
}
