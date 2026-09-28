using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace Roost.Core
{
    public sealed class AiEvalCase
    {
        public string Id { get; internal set; }
        public string Category { get; internal set; }
        public string Input { get; internal set; }
        public string Expect { get; internal set; }
        public bool SelfTest { get; internal set; }
        public Dictionary<string, object> Raw { get; internal set; }
    }

    public sealed class AiEvalResult
    {
        public AiEvalCase Case { get; internal set; }
        public bool Pass { get; internal set; }
        public string Reason { get; internal set; }
        public List<string> Critical { get; internal set; }
        public AiPlan Plan { get; internal set; }
        public string Reply { get; internal set; }
        public long LatencyMs { get; internal set; }
    }

    // AI 评测集（PRD 8.6）与模型测试题（PRD 9.6）共用的题目与判分规则。题目全部是虚构待办。
    public sealed class AiEvalSuite
    {
        public const string CriticalDoneAsDelete = "把「做完了」当成了删除";
        public const string CriticalUntouched = "改动了用户没有提到的待办";
        public const string CriticalNonexistent = "对不存在的待办产生了改动";

        private readonly Dictionary<string, object> fixture;

        public int DayStartMinutes { get; private set; }
        public List<AiEvalCase> Cases { get; private set; }

        private AiEvalSuite(Dictionary<string, object> root)
        {
            fixture = (Dictionary<string, object>)root["fixture"];
            DayStartMinutes = Convert.ToInt32(fixture["dayStartMinutes"], CultureInfo.InvariantCulture);
            Cases = new List<AiEvalCase>();
            foreach (object raw in Array(root, "cases"))
            {
                Dictionary<string, object> item = (Dictionary<string, object>)raw;
                Cases.Add(new AiEvalCase
                {
                    Id = (string)item["id"],
                    Category = (string)item["category"],
                    Input = (string)item["input"],
                    Expect = (string)item["expect"],
                    SelfTest = item.ContainsKey("selfTest") && (bool)item["selfTest"],
                    Raw = item
                });
            }
        }

        public static AiEvalSuite Load(string path)
        {
            JavaScriptSerializer serializer = new JavaScriptSerializer { MaxJsonLength = 16 * 1024 * 1024 };
            return new AiEvalSuite((Dictionary<string, object>)serializer.DeserializeObject(File.ReadAllText(path, Encoding.UTF8)));
        }

        public List<AiEvalCase> SelfTestCases()
        {
            return Cases.FindAll(delegate(AiEvalCase item) { return item.SelfTest; });
        }

        public List<TodoItem> BuildTodos()
        {
            List<TodoItem> todos = new List<TodoItem>();
            foreach (object value in Array(fixture, "todos"))
            {
                Dictionary<string, object> raw = (Dictionary<string, object>)value;
                todos.Add(new TodoItem
                {
                    Id = (string)raw["id"],
                    Title = (string)raw["title"],
                    DueDate = raw.ContainsKey("dueDate") ? (string)raw["dueDate"] : null,
                    DueTime = raw.ContainsKey("dueTime") ? (string)raw["dueTime"] : null,
                    IsStarred = raw.ContainsKey("starred") && (bool)raw["starred"],
                    IsCompleted = raw.ContainsKey("completed") && (bool)raw["completed"],
                    CompletedAtUtc = raw.ContainsKey("completedAtUtc") ? (string)raw["completedAtUtc"] : null,
                    IsDeleted = raw.ContainsKey("deleted") && (bool)raw["deleted"],
                    CreatedAtUtc = (string)raw["createdAtUtc"],
                    UpdatedAtUtc = (string)raw["createdAtUtc"]
                });
            }
            return todos;
        }

        public AiRequestContext CreateContext(AiEvalCase item, List<TodoItem> todos)
        {
            string now = item.Raw.ContainsKey("now") ? (string)item.Raw["now"] : (string)fixture["now"];
            DateTime local = DateTime.ParseExact(now, "yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal);
            return AiRequestContext.Create(todos, local, DayStartMinutes);
        }

        // 逐题提问并判分。可重试的失败（网络、超时、限流、服务错误）重试一次；其他 AiException 直接抛出，由调用方中止。
        public async Task<List<AiEvalResult>> RunAsync(
            IList<AiEvalCase> cases,
            Func<AiRequestContext, string, CancellationToken, Task<string>> responder,
            Action<int> progress,
            CancellationToken cancellation)
        {
            List<AiEvalResult> results = new List<AiEvalResult>();
            for (int index = 0; index < cases.Count; index++)
            {
                cancellation.ThrowIfCancellationRequested();
                AiEvalCase item = cases[index];
                List<TodoItem> todos = BuildTodos();
                AiRequestContext context = CreateContext(item, todos);
                Stopwatch clock = Stopwatch.StartNew();
                string reply = null;
                bool retry = false;
                try
                {
                    reply = await responder(context, item.Input, cancellation).ConfigureAwait(false);
                }
                catch (AiException exception)
                {
                    if (!exception.Retryable) throw;
                    retry = true;
                }
                if (retry)
                {
                    await Task.Delay(2000, cancellation).ConfigureAwait(false);
                    reply = await responder(context, item.Input, cancellation).ConfigureAwait(false);
                }
                clock.Stop();
                results.Add(Check(item, reply, todos, context, clock.ElapsedMilliseconds));
                if (progress != null) progress(index + 1);
            }
            return results;
        }

        public AiEvalResult Check(AiEvalCase item, string reply, List<TodoItem> todos, AiRequestContext context, long latencyMs)
        {
            AiPlan plan = AiPlanParser.Parse(reply, context);
            List<string> critical = new List<string>();
            string reason = Judge(item, plan, todos, critical);
            return new AiEvalResult
            {
                Case = item,
                Pass = reason == null && critical.Count == 0,
                Reason = reason ?? (critical.Count > 0 ? string.Join("；", critical.ToArray()) : null),
                Critical = critical,
                Plan = plan,
                Reply = reply,
                LatencyMs = latencyMs
            };
        }

        public string DescribeExpectation(AiEvalCase item)
        {
            switch (item.Expect)
            {
                case "none": return "不产生任何改动（清单里没有这条待办）";
                case "unclear": return "回复没听懂，不产生改动";
                case "ambiguous": return "指出有多条待办都可能，不产生改动";
            }
            Dictionary<string, string> titles = new Dictionary<string, string>();
            foreach (TodoItem todo in BuildTodos()) titles[todo.Id] = "「" + todo.Title + "」";
            List<string> parts = new List<string>();
            foreach (object value in Array(item.Raw, "ops"))
            {
                Dictionary<string, object> expected = (Dictionary<string, object>)value;
                string kind = (string)expected["op"];
                string target = expected.ContainsKey("target") ? titles[(string)expected["target"]] : null;
                switch (kind)
                {
                    case "add": parts.Add("新增一条关于「" + (string)Array(expected, "title")[0] + "」的待办"); break;
                    case "update": parts.Add("修改" + target); break;
                    case "complete": parts.Add("标记完成" + target); break;
                    case "uncomplete": parts.Add("取消完成" + target); break;
                    case "star": parts.Add("给" + target + "加星标"); break;
                    case "unstar": parts.Add("取消" + target + "的星标"); break;
                    case "delete": parts.Add("删除" + target); break;
                }
            }
            return string.Join("，", parts.ToArray());
        }

        private static string Judge(AiEvalCase item, AiPlan plan, List<TodoItem> todos, List<string> critical)
        {
            List<Dictionary<string, object>> expectedOps = new List<Dictionary<string, object>>();
            foreach (object value in Array(item.Raw, "ops")) expectedOps.Add((Dictionary<string, object>)value);
            List<AiOperation> actual = plan.Operations.FindAll(delegate(AiOperation operation) { return operation.IsValid; });

            HashSet<string> allowed = new HashSet<string>();
            HashSet<string> done = new HashSet<string>();
            foreach (object target in Array(item.Raw, "mentions")) allowed.Add((string)target);
            foreach (Dictionary<string, object> expected in expectedOps)
            {
                if (!expected.ContainsKey("target")) continue;
                allowed.Add((string)expected["target"]);
                if ((string)expected["op"] == "complete") done.Add((string)expected["target"]);
            }
            foreach (object target in Array(item.Raw, "doneTargets")) done.Add((string)target);

            foreach (AiOperation operation in actual)
            {
                if (operation.Kind == AiOperationKind.Add) continue;
                if (operation.Kind == AiOperationKind.Delete && done.Contains(operation.TargetId))
                    AddOnce(critical, CriticalDoneAsDelete);
                if (!allowed.Contains(operation.TargetId))
                    AddOnce(critical, item.Category == "不存在" ? CriticalNonexistent : CriticalUntouched);
            }

            if (item.Expect == "none")
                return actual.Count == 0 ? null : "应当不产生任何改动";
            if (item.Expect == "unclear")
                return plan.Status == AiPlanStatus.Unclear ? null : "应当回复没听懂";
            if (item.Expect == "ambiguous")
                return plan.Status == AiPlanStatus.Ambiguous ? null : "应当指出有歧义";
            if (plan.Status != AiPlanStatus.Ok) return "应当产生改动，模型却没有给出";

            List<AiOperation> remaining = new List<AiOperation>(actual);
            foreach (Dictionary<string, object> expected in expectedOps)
            {
                AiOperation match = remaining.Find(delegate(AiOperation operation) { return Matches(expected, operation, todos); });
                if (match == null) return "缺少或不符合期望的操作";
                remaining.Remove(match);
            }
            if (remaining.Count > 0) return "多出了操作：" + remaining[0].Summary;
            return null;
        }

        private static bool Matches(Dictionary<string, object> expected, AiOperation actual, List<TodoItem> todos)
        {
            string kind = (string)expected["op"];
            if (!string.Equals(actual.Kind.ToString(), kind, StringComparison.OrdinalIgnoreCase)) return false;
            if (kind == "add")
            {
                return ContainsAny(actual.Title, Array(expected, "title")) &&
                       actual.DueDate == (string)expected["date"] && actual.DueTime == (string)expected["time"] &&
                       actual.Starred == (bool)expected["starred"];
            }
            if (actual.TargetId != (string)expected["target"]) return false;
            if (kind != "update") return true;

            TodoItem original = todos.Find(delegate(TodoItem item) { return item.Id == actual.TargetId; });
            if (expected.ContainsKey("title") ? !(actual.HasTitle && ContainsAny(actual.Title, Array(expected, "title"))) : (actual.HasTitle && actual.Title != original.Title)) return false;
            if (expected.ContainsKey("notes") ? !(actual.HasNotes && ContainsAny(actual.Notes, Array(expected, "notes"))) : (actual.HasNotes && actual.Notes != (original.Notes ?? string.Empty))) return false;
            if (expected.ContainsKey("date") || expected.ContainsKey("time"))
                return actual.HasDue && actual.DueDate == (string)expected["date"] && actual.DueTime == (string)expected["time"];
            return !actual.HasDue || (actual.DueDate == original.DueDate && actual.DueTime == original.DueTime);
        }

        public static object[] Array(Dictionary<string, object> source, string key)
        {
            object value;
            if (!source.TryGetValue(key, out value) || value == null) return new object[0];
            return value as object[] ?? ((ArrayList)value).ToArray();
        }

        private static bool ContainsAny(string text, object[] keywords)
        {
            if (string.IsNullOrEmpty(text)) return false;
            foreach (object keyword in keywords)
                if (text.IndexOf((string)keyword, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        private static void AddOnce(List<string> list, string value)
        {
            if (!list.Contains(value)) list.Add(value);
        }
    }
}
