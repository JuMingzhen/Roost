using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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
        if (args.Length >= 4 && args[0] == "run") return Run(args);
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
        AiEvalSuite suite = AiEvalSuite.Load(args[1]);
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
        List<AiEvalResult> results;
        try
        {
            results = suite.RunAsync(suite.Cases, delegate(AiRequestContext context, string input, CancellationToken token)
            {
                return client.CompleteAsync(endpoint, context.SystemPrompt(), input, 2048, token);
            }, delegate(int done)
            {
                Console.Write("\r{0}/{1}", done, suite.Cases.Count);
            }, CancellationToken.None).GetAwaiter().GetResult();
        }
        catch (AiException exception)
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine("评测中止：" + exception.Message);
            return 2;
        }
        Console.WriteLine();
        foreach (AiEvalResult result in results)
            if (!result.Pass) Console.WriteLine("FAIL {0} {1}  -> {2}", result.Case.Id, result.Case.Input, result.Reason);

        Dictionary<string, object> report = Report(results);
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
        AiEvalSuite suite = AiEvalSuite.Load(path);
        foreach (AiEvalCase item in suite.Cases)
            CaseByInput[Key(suite.CreateContext(item, suite.BuildTodos()), item.Input)] = item;
        Dictionary<string, object> perfect = Report(suite.RunAsync(suite.Cases, Oracle(false), null, CancellationToken.None).GetAwaiter().GetResult());
        Dictionary<string, object> sabotaged = Report(suite.RunAsync(suite.Cases, Oracle(true), null, CancellationToken.None).GetAwaiter().GetResult());
        List<AiEvalCase> selfTest = suite.SelfTestCases();
        Dictionary<string, object> selfPerfect = Report(suite.RunAsync(selfTest, Oracle(false), null, CancellationToken.None).GetAwaiter().GetResult());
        int total = (int)perfect["total"];
        bool ok = total >= 50 && total <= 100 &&
                  (int)perfect["passed"] == total && (int)perfect["criticalCount"] == 0 && (bool)perfect["meetsBar"] &&
                  (int)sabotaged["criticalCount"] > 0 && !(bool)sabotaged["meetsBar"] &&
                  selfTest.Count >= 12 && selfTest.Count <= 20 && (int)selfPerfect["passed"] == selfTest.Count;
        Console.WriteLine("SELFTEST cases={0} oraclePassed={1} oracleCritical={2} saboteurCritical={3} saboteurPassed={4} modelTest={5}/{6} -> {7}",
            total, perfect["passed"], perfect["criticalCount"], sabotaged["criticalCount"], sabotaged["passed"],
            selfPerfect["passed"], selfTest.Count, ok ? "PASS" : "FAIL");
        if (!ok)
        {
            foreach (Dictionary<string, object> result in (List<Dictionary<string, object>>)perfect["cases"])
                if (!(bool)result["pass"]) Console.WriteLine("  oracle failed {0}: {1}", result["id"], result["reason"]);
        }
        return ok ? 0 : 1;
    }

    private static Dictionary<string, object> Report(List<AiEvalResult> results)
    {
        List<Dictionary<string, object>> cases = new List<Dictionary<string, object>>();
        List<string> criticalCases = new List<string>();
        List<long> latencies = new List<long>();
        int passed = 0;
        foreach (AiEvalResult result in results)
        {
            if (result.Pass) passed++;
            if (result.Critical.Count > 0) criticalCases.Add(result.Case.Id);
            latencies.Add(result.LatencyMs);
            Dictionary<string, object> entry = new Dictionary<string, object>();
            entry["id"] = result.Case.Id;
            entry["category"] = result.Case.Category;
            entry["input"] = result.Case.Input;
            entry["pass"] = result.Pass;
            entry["reason"] = result.Reason;
            entry["critical"] = result.Critical.ToArray();
            entry["status"] = result.Plan.Status.ToString();
            entry["operations"] = result.Plan.Operations.Select(delegate(AiOperation operation)
            {
                return (operation.IsValid ? string.Empty : "[无效] ") + operation.Summary;
            }).ToArray();
            entry["latencyMs"] = result.LatencyMs;
            if (!result.Pass) entry["reply"] = result.Reply.Length <= 600 ? result.Reply : result.Reply.Substring(0, 600) + "…";
            cases.Add(entry);
        }
        latencies.Sort();
        int total = results.Count;
        double accuracy = total == 0 ? 0 : (double)passed / total;
        Dictionary<string, object> report = new Dictionary<string, object>();
        report["timestampUtc"] = DateTime.UtcNow.ToString("o");
        report["total"] = total;
        report["passed"] = passed;
        report["accuracy"] = Math.Round(accuracy, 4);
        report["criticalCount"] = criticalCases.Count;
        report["criticalCases"] = criticalCases.ToArray();
        report["requiredAccuracy"] = RequiredAccuracy;
        report["meetsBar"] = criticalCases.Count == 0 && accuracy >= RequiredAccuracy;
        report["medianLatencyMs"] = latencies.Count == 0 ? 0 : latencies[latencies.Count / 2];
        report["p90LatencyMs"] = latencies.Count == 0 ? 0 : latencies[Math.Min(latencies.Count - 1, (int)Math.Ceiling(latencies.Count * 0.9) - 1)];
        report["maxLatencyMs"] = latencies.Count == 0 ? 0 : latencies[latencies.Count - 1];
        report["cases"] = cases;
        return report;
    }

    // 按期望结果生成回复：sabotage 时模拟三类致命错误，用来确认判分规则能抓到它们。
    private static Func<AiRequestContext, string, CancellationToken, Task<string>> Oracle(bool sabotage)
    {
        return delegate(AiRequestContext context, string input, CancellationToken token)
        {
            return Task.FromResult(OracleReply(context, input, sabotage));
        };
    }

    private static readonly Dictionary<string, AiEvalCase> CaseByInput = new Dictionary<string, AiEvalCase>();

    private static string OracleReply(AiRequestContext context, string input, bool sabotage)
    {
        AiEvalCase item = FindCase(context, input);
        string expect = item.Expect;
        if (expect == "ambiguous")
        {
            object[] mentions = AiEvalSuite.Array(item.Raw, "mentions");
            if (sabotage) return Json.Serialize(Reply("ok", new object[] { Op("delete", Alias(context, (string)mentions[0])) }));
            Dictionary<string, object> reply = Reply("ambiguous", null);
            reply["candidates"] = mentions.Select(delegate(object target) { return (object)Alias(context, (string)target); }).ToArray();
            return Json.Serialize(reply);
        }
        if (expect != "ok")
        {
            if (sabotage && item.Id == "e01") return Json.Serialize(Reply("ok", new object[] { Op("complete", Alias(context, "gym")) }));
            return Json.Serialize(Reply("unclear", null));
        }

        List<object> operations = new List<object>();
        foreach (Dictionary<string, object> expected in AiEvalSuite.Array(item.Raw, "ops").Cast<Dictionary<string, object>>())
        {
            string kind = (string)expected["op"];
            if (kind == "add")
            {
                Dictionary<string, object> add = new Dictionary<string, object> { { "op", "add" } };
                add["title"] = (string)AiEvalSuite.Array(expected, "title")[0];
                add["date"] = expected["date"];
                add["time"] = expected["time"];
                add["starred"] = expected["starred"];
                operations.Add(add);
                continue;
            }
            if (sabotage && kind == "complete") kind = "delete";
            Dictionary<string, object> operation = Op(kind, Alias(context, (string)expected["target"]));
            if (kind == "update")
            {
                if (expected.ContainsKey("title")) operation["title"] = (string)AiEvalSuite.Array(expected, "title")[0];
                if (expected.ContainsKey("notes")) operation["notes"] = (string)AiEvalSuite.Array(expected, "notes")[0];
                if (expected.ContainsKey("date")) operation["date"] = expected["date"];
                if (expected.ContainsKey("time")) operation["time"] = expected["time"];
            }
            operations.Add(operation);
        }
        if (sabotage && item.Id == "t01")
        {
            Dictionary<string, object> collateral = Op("update", Alias(context, "meeting_product"));
            collateral["time"] = "16:00";
            operations.Add(collateral);
        }
        return "```json\n" + Json.Serialize(Reply("ok", operations.ToArray())) + "\n```";
    }

    private static AiEvalCase FindCase(AiRequestContext context, string input)
    {
        return CaseByInput[Key(context, input)];
    }

    private static string Key(AiRequestContext context, string input)
    {
        return context.Now.ToString("o") + "|" + input;
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
}
