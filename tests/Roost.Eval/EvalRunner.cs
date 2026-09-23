using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using Roost.Core;

internal static class EvalRunner
{
    private const double RequiredAccuracy = 0.90;
    private static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = 16 * 1024 * 1024 };

    private static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        if (args.Length >= 2 && args[0] == "--self-test") return SelfTest(args[1]);
        if (args.Length >= 3 && args[0] == "run") return Run(args);
        if (args.Length >= 2 && args[0] == "save-key") return SaveKey(args[1]);
        if (args.Length >= 2 && args[0] == "has-key") return string.IsNullOrEmpty(CredentialStore.Read(EvalTarget(args[1]))) ? 1 : 0;
        Console.Error.WriteLine("Usage: Roost.Eval.exe --self-test <cases.json>");
        Console.Error.WriteLine("       Roost.Eval.exe run <cases.json> <preset|custom> <report.json> [baseUrl] [model]");
        return 64;
    }

    private static string EvalTarget(string preset)
    {
        return "Roost/Eval/" + preset;
    }

    private static int SaveKey(string preset)
    {
        string key = Environment.GetEnvironmentVariable("ROOST_EVAL_KEY");
        if (string.IsNullOrEmpty(key)) return 64;
        CredentialStore.Write(EvalTarget(preset), key);
        return 0;
    }

    private static int Run(string[] args)
    {
        Dictionary<string, object> suite = Load(args[1]);
        string presetId = args[2];
        string reportPath = args[3];
        AiPreset preset = AiPresets.Find(presetId);
        string baseUrl = args.Length > 4 && args[4].Length > 0 ? args[4] : (preset == null ? null : preset.BaseUrl);
        string model = args.Length > 5 && args[5].Length > 0 ? args[5] : (preset == null ? null : preset.Model);
        if (string.IsNullOrEmpty(baseUrl) || string.IsNullOrEmpty(model))
        {
            Console.Error.WriteLine("未知预设，请同时给出服务地址和模型名。");
            return 64;
        }
        string key = Environment.GetEnvironmentVariable("ROOST_EVAL_KEY");
        if (string.IsNullOrEmpty(key)) key = CredentialStore.Read(EvalTarget(presetId));
        if (string.IsNullOrEmpty(key))
        {
            Console.Error.WriteLine("没有 key：请用 eval-ai.ps1 运行，它会提示输入。");
            return 64;
        }

        AiEndpoint endpoint = new AiEndpoint { BaseUrl = baseUrl, Model = model, ApiKey = key };
        AiClient client = new AiClient(TimeSpan.FromSeconds(90));
        Func<AiRequestContext, Dictionary<string, object>, string, string> responder = delegate(AiRequestContext context, Dictionary<string, object> item, string input)
        {
            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    return client.CompleteAsync(endpoint, context.SystemPrompt(), input, 2048, CancellationToken.None).GetAwaiter().GetResult();
                }
                catch (AiException exception)
                {
                    if (!exception.Retryable || attempt >= 2) throw;
                    Thread.Sleep(3000 * (attempt + 1));
                }
            }
        };

        Dictionary<string, object> report;
        try
        {
            report = Evaluate(suite, responder, true);
        }
        catch (AiException exception)
        {
            Console.Error.WriteLine("评测中止：" + exception.Message);
            return 2;
        }
        report["preset"] = presetId;
        report["baseUrl"] = baseUrl;
        report["model"] = model;
        string full = Path.GetFullPath(reportPath);
        Directory.CreateDirectory(Path.GetDirectoryName(full));
        File.WriteAllText(full, Json.Serialize(report), new UTF8Encoding(false));
        Console.WriteLine("RESULT preset={0} model={1} passed={2}/{3} accuracy={4:P1} critical={5} medianMs={6} p90Ms={7} meetsBar={8}",
            presetId, model, report["passed"], report["total"], report["accuracy"], report["criticalCount"],
            report["medianLatencyMs"], report["p90LatencyMs"], report["meetsBar"]);
        return (bool)report["meetsBar"] ? 0 : 1;
    }

    private static int SelfTest(string path)
    {
        Dictionary<string, object> suite = Load(path);
        Dictionary<string, object> perfect = Evaluate(suite, Oracle(false), false);
        Dictionary<string, object> sabotaged = Evaluate(suite, Oracle(true), false);
        int total = (int)perfect["total"];
        bool ok = total >= 50 && total <= 100 &&
                  (int)perfect["passed"] == total && (int)perfect["criticalCount"] == 0 && (bool)perfect["meetsBar"] &&
                  (int)sabotaged["criticalCount"] > 0 && !(bool)sabotaged["meetsBar"];
        Console.WriteLine("SELFTEST cases={0} oraclePassed={1} oracleCritical={2} saboteurCritical={3} saboteurPassed={4} -> {5}",
            total, perfect["passed"], perfect["criticalCount"], sabotaged["criticalCount"], sabotaged["passed"], ok ? "PASS" : "FAIL");
        if (!ok)
        {
            foreach (Dictionary<string, object> result in (List<Dictionary<string, object>>)perfect["cases"])
                if (!(bool)result["pass"]) Console.WriteLine("  oracle failed {0}: {1}", result["id"], result["reason"]);
        }
        return ok ? 0 : 1;
    }

    // 按期望结果生成回复：sabotage 时模拟三类致命错误，用来确认评分器能抓到它们。
    private static Func<AiRequestContext, Dictionary<string, object>, string, string> Oracle(bool sabotage)
    {
        return delegate(AiRequestContext context, Dictionary<string, object> item, string input)
        {
            string expect = (string)item["expect"];
            if (expect == "ambiguous")
            {
                List<object> candidates = new List<object>();
                foreach (object target in Array(item, "mentions")) candidates.Add(Alias(context, (string)target));
                if (sabotage)
                    return Json.Serialize(Reply("ok", new object[] { Op("delete", Alias(context, (string)Array(item, "mentions")[0])) }));
                Dictionary<string, object> reply = Reply("ambiguous", null);
                reply["candidates"] = candidates.ToArray();
                return Json.Serialize(reply);
            }
            if (expect != "ok")
            {
                if (sabotage && (string)item["id"] == "e01") return Json.Serialize(Reply("ok", new object[] { Op("complete", Alias(context, "gym")) }));
                return Json.Serialize(Reply("unclear", null));
            }

            List<object> operations = new List<object>();
            foreach (Dictionary<string, object> expected in Array(item, "ops").Cast<Dictionary<string, object>>())
            {
                string kind = (string)expected["op"];
                if (kind == "add")
                {
                    Dictionary<string, object> add = Op("add", null);
                    add.Remove("id");
                    add["title"] = (string)Array(expected, "title")[0];
                    add["date"] = expected["date"];
                    add["time"] = expected["time"];
                    add["starred"] = expected["starred"];
                    operations.Add(add);
                    continue;
                }
                string alias = Alias(context, (string)expected["target"]);
                if (sabotage && kind == "complete") kind = "delete";
                Dictionary<string, object> operation = Op(kind, alias);
                if (kind == "update")
                {
                    if (expected.ContainsKey("title")) operation["title"] = (string)Array(expected, "title")[0];
                    if (expected.ContainsKey("notes")) operation["notes"] = (string)Array(expected, "notes")[0];
                    if (expected.ContainsKey("date")) operation["date"] = expected["date"];
                    if (expected.ContainsKey("time")) operation["time"] = expected["time"];
                }
                operations.Add(operation);
            }
            if (sabotage && (string)item["id"] == "t01") operations.Add(Op("update", Alias(context, "meeting_product")));
            if (sabotage && (string)item["id"] == "t01") ((Dictionary<string, object>)operations[operations.Count - 1])["time"] = "16:00";
            return "```json\n" + Json.Serialize(Reply("ok", operations.ToArray())) + "\n```";
        };
    }

    private static Dictionary<string, object> Evaluate(Dictionary<string, object> suite, Func<AiRequestContext, Dictionary<string, object>, string, string> responder, bool verbose)
    {
        Dictionary<string, object> fixture = (Dictionary<string, object>)suite["fixture"];
        int dayStart = Convert.ToInt32(fixture["dayStartMinutes"], CultureInfo.InvariantCulture);
        List<Dictionary<string, object>> results = new List<Dictionary<string, object>>();
        List<string> criticalCases = new List<string>();
        List<long> latencies = new List<long>();
        int passed = 0, critical = 0;
        foreach (Dictionary<string, object> item in Array(suite, "cases").Cast<Dictionary<string, object>>())
        {
            List<TodoItem> todos = BuildTodos(fixture);
            DateTime now = ParseLocal(item.ContainsKey("now") ? (string)item["now"] : (string)fixture["now"]);
            AiRequestContext context = AiRequestContext.Create(todos, now, dayStart);
            string input = (string)item["input"];
            Stopwatch clock = Stopwatch.StartNew();
            string reply = responder(context, item, input);
            clock.Stop();
            latencies.Add(clock.ElapsedMilliseconds);
            AiPlan plan = AiPlanParser.Parse(reply, context);
            List<string> criticals;
            string reason = Check(item, plan, todos, out criticals);
            bool pass = reason == null && criticals.Count == 0;
            if (pass) passed++;
            if (criticals.Count > 0) { critical++; criticalCases.Add((string)item["id"]); }

            Dictionary<string, object> result = new Dictionary<string, object>();
            result["id"] = item["id"];
            result["category"] = item["category"];
            result["input"] = input;
            result["pass"] = pass;
            result["reason"] = reason ?? (criticals.Count > 0 ? "致命错误" : null);
            result["critical"] = criticals.ToArray();
            result["status"] = plan.Status.ToString();
            result["operations"] = plan.Operations.Select(delegate(AiOperation operation)
            {
                return (operation.IsValid ? string.Empty : "[无效] ") + operation.Summary;
            }).ToArray();
            result["latencyMs"] = clock.ElapsedMilliseconds;
            if (!pass) result["reply"] = reply.Length <= 600 ? reply : reply.Substring(0, 600) + "…";
            results.Add(result);
            if (verbose) Console.WriteLine("{0} {1} {2}{3}", pass ? "PASS" : "FAIL", item["id"], input, pass ? string.Empty : "  -> " + result["reason"] + (criticals.Count > 0 ? "（" + string.Join("；", criticals.ToArray()) + "）" : string.Empty));
        }

        latencies.Sort();
        int total = results.Count;
        double accuracy = total == 0 ? 0 : (double)passed / total;
        Dictionary<string, object> report = new Dictionary<string, object>();
        report["timestampUtc"] = DateTime.UtcNow.ToString("o");
        report["total"] = total;
        report["passed"] = passed;
        report["accuracy"] = Math.Round(accuracy, 4);
        report["criticalCount"] = critical;
        report["criticalCases"] = criticalCases.ToArray();
        report["requiredAccuracy"] = RequiredAccuracy;
        report["meetsBar"] = critical == 0 && accuracy >= RequiredAccuracy;
        report["medianLatencyMs"] = latencies.Count == 0 ? 0 : latencies[latencies.Count / 2];
        report["p90LatencyMs"] = latencies.Count == 0 ? 0 : latencies[Math.Min(latencies.Count - 1, (int)Math.Ceiling(latencies.Count * 0.9) - 1)];
        report["maxLatencyMs"] = latencies.Count == 0 ? 0 : latencies[latencies.Count - 1];
        report["cases"] = results;
        return report;
    }

    private static string Check(Dictionary<string, object> item, AiPlan plan, List<TodoItem> todos, out List<string> criticals)
    {
        criticals = new List<string>();
        string expect = (string)item["expect"];
        List<Dictionary<string, object>> expectedOps = Array(item, "ops").Cast<Dictionary<string, object>>().ToList();
        List<AiOperation> actual = plan.Operations.Where(delegate(AiOperation operation) { return operation.IsValid; }).ToList();

        HashSet<string> allowed = new HashSet<string>();
        HashSet<string> done = new HashSet<string>();
        foreach (object target in Array(item, "mentions")) allowed.Add((string)target);
        foreach (Dictionary<string, object> expected in expectedOps)
        {
            if (!expected.ContainsKey("target")) continue;
            allowed.Add((string)expected["target"]);
            if ((string)expected["op"] == "complete") done.Add((string)expected["target"]);
        }
        foreach (object target in Array(item, "doneTargets")) done.Add((string)target);

        foreach (AiOperation operation in actual)
        {
            if (operation.Kind == AiOperationKind.Add) continue;
            if (operation.Kind == AiOperationKind.Delete && done.Contains(operation.TargetId))
                AddOnce(criticals, "把「做完了」当成了删除");
            if (!allowed.Contains(operation.TargetId))
                AddOnce(criticals, (string)item["category"] == "不存在" ? "对不存在的待办产生了改动" : "改动了用户没有提到的待办");
        }

        if (expect == "none")
            return actual.Count == 0 ? null : "应当不产生任何改动";
        if (expect == "unclear")
            return plan.Status == AiPlanStatus.Unclear ? null : "应当回复没听懂，实际为 " + plan.Status;
        if (expect == "ambiguous")
        {
            if (plan.Status != AiPlanStatus.Ambiguous) return "应当判定为有歧义，实际为 " + plan.Status;
            return null;
        }
        if (plan.Status != AiPlanStatus.Ok) return "应当产生改动，实际为 " + plan.Status + "：" + plan.Message;

        List<AiOperation> remaining = new List<AiOperation>(actual);
        foreach (Dictionary<string, object> expected in expectedOps)
        {
            AiOperation match = remaining.FirstOrDefault(delegate(AiOperation operation) { return Matches(expected, operation, todos); });
            if (match == null) return "缺少或不符合期望的操作：" + Describe(expected);
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

        TodoItem original = todos.First(delegate(TodoItem item) { return item.Id == actual.TargetId; });
        if (expected.ContainsKey("title") ? !(actual.HasTitle && ContainsAny(actual.Title, Array(expected, "title"))) : (actual.HasTitle && actual.Title != original.Title)) return false;
        if (expected.ContainsKey("notes") ? !(actual.HasNotes && ContainsAny(actual.Notes, Array(expected, "notes"))) : (actual.HasNotes && actual.Notes != (original.Notes ?? string.Empty))) return false;
        bool expectDue = expected.ContainsKey("date") || expected.ContainsKey("time");
        if (expectDue) return actual.HasDue && actual.DueDate == (string)expected["date"] && actual.DueTime == (string)expected["time"];
        return !actual.HasDue || (actual.DueDate == original.DueDate && actual.DueTime == original.DueTime);
    }

    private static List<TodoItem> BuildTodos(Dictionary<string, object> fixture)
    {
        List<TodoItem> todos = new List<TodoItem>();
        foreach (Dictionary<string, object> raw in Array(fixture, "todos").Cast<Dictionary<string, object>>())
        {
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

    private static Dictionary<string, object> Load(string path)
    {
        return (Dictionary<string, object>)Json.DeserializeObject(File.ReadAllText(path, Encoding.UTF8));
    }

    private static object[] Array(Dictionary<string, object> source, string key)
    {
        object value;
        if (!source.TryGetValue(key, out value) || value == null) return new object[0];
        return value as object[] ?? ((System.Collections.ArrayList)value).ToArray();
    }

    private static DateTime ParseLocal(string value)
    {
        return DateTime.ParseExact(value, "yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal);
    }

    private static string Alias(AiRequestContext context, string id)
    {
        foreach (KeyValuePair<string, TodoItem> entry in context.Items)
            if (entry.Value.Id == id) return entry.Key;
        return null;
    }

    private static Dictionary<string, object> Reply(string status, object[] operations)
    {
        Dictionary<string, object> reply = new Dictionary<string, object> { { "status", status } };
        if (operations != null) reply["operations"] = operations;
        else reply["message"] = "（模拟）";
        return reply;
    }

    private static Dictionary<string, object> Op(string kind, string alias)
    {
        return new Dictionary<string, object> { { "op", kind }, { "id", alias } };
    }

    private static bool ContainsAny(string text, object[] keywords)
    {
        if (string.IsNullOrEmpty(text)) return false;
        foreach (object keyword in keywords)
            if (text.IndexOf((string)keyword, StringComparison.OrdinalIgnoreCase) >= 0) return true;
        return false;
    }

    private static string Describe(Dictionary<string, object> expected)
    {
        return Json.Serialize(expected);
    }

    private static void AddOnce(List<string> list, string value)
    {
        if (!list.Contains(value)) list.Add(value);
    }
}
