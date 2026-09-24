using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Roost.Core;

internal static class TestRunner
{
    private static int passed;
    private static int failed;

    private static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--storage-worker")
        {
            return RunStorageWorker(args);
        }

        Run("排序：三组及组内星标", TestSorting);
        Run("只显示今日", TestOnlyToday);
        Run("超过五条折叠", TestCollapse);
        Run("04:00 边界", TestDayBoundary);
        Run("待办新建、编辑、完成、删除撤销和星标", TestTodoOperations);
        Run("布局翻转、找回、贴边和透明度", TestLayoutRules);
        Run("位置与清单设置重启后保持", TestSettingsPersistence);
        Run("全屏窗口判定", TestFullscreenRules);
        Run("原子写入中强杀不损坏主文件", TestCrashDuringWrite);
        Run("自动备份只保留最近七份", TestBackupRetention);
        Run("AI 发送范围：未完成 + 7 天内完成", TestAiContextScope);
        Run("AI 提示词按一天起点给出今天和明天", TestAiPromptDates);
        Run("AI 回复解析为预览操作", TestAiParseOperations);
        Run("AI 无效操作、没听懂和有歧义", TestAiParseRejections);
        Run("AI 预览不改动数据，确认后整批生效并可整批撤销", TestAiApplyAndUndo);
        Run("AI 请求格式与成功回复", TestAiClientSuccess);
        Run("AI 失败分类：key 无效、模型错误、限流、服务错误", TestAiClientStatusErrors);
        Run("AI 超时、取消与断网", TestAiClientTimeoutCancelNetwork);
        Run("AI 模型拒绝 temperature 时去掉该参数重试一次", TestAiClientTemperatureFallback);
        Run("API Key 写入 Windows 凭据管理器且不落盘", TestCredentialStore);

        Console.WriteLine("RESULT passed={0} failed={1}", passed, failed);
        return failed == 0 ? 0 : 1;
    }

    private static void TestSorting()
    {
        DateTime now = new DateTime(2026, 9, 21, 12, 0, 0);
        List<TodoItem> items = new List<TodoItem>
        {
            Item("overdue normal", "2026-09-20", "10:00", false, "2026-09-10T00:00:00Z"),
            Item("overdue star", "2026-09-20", "11:00", true, "2026-09-11T00:00:00Z"),
            Item("due late", "2026-09-22", "16:00", false, "2026-09-12T00:00:00Z"),
            Item("due star", "2026-09-23", "09:00", true, "2026-09-13T00:00:00Z"),
            Item("none old", null, null, false, "2026-09-14T00:00:00Z"),
            Item("none new", null, null, false, "2026-09-15T00:00:00Z"),
            Item("none star", null, null, true, "2026-09-01T00:00:00Z")
        };
        List<TodoItem> sorted = TodoRules.SortAndFilter(items, now, 240, false);
        Equal("overdue star", sorted[0].Title);
        Equal("overdue normal", sorted[1].Title);
        Equal("due star", sorted[2].Title);
        Equal("due late", sorted[3].Title);
        Equal("none star", sorted[4].Title);
        Equal("none new", sorted[5].Title);
        Equal("none old", sorted[6].Title);
    }

    private static void TestOnlyToday()
    {
        DateTime now = new DateTime(2026, 9, 21, 12, 0, 0);
        List<TodoItem> items = new List<TodoItem>
        {
            Item("today", "2026-09-21", null, false, "2026-09-01T00:00:00Z"),
            Item("past", "2026-09-20", null, false, "2026-09-01T00:00:00Z"),
            Item("future", "2026-09-22", null, false, "2026-09-01T00:00:00Z"),
            Item("star no due", null, null, true, "2026-09-01T00:00:00Z"),
            Item("plain no due", null, null, false, "2026-09-01T00:00:00Z")
        };
        List<string> titles = TodoRules.SortAndFilter(items, now, 240, true).Select(x => x.Title).ToList();
        True(titles.Contains("today"));
        True(titles.Contains("past"));
        True(titles.Contains("star no due"));
        False(titles.Contains("future"));
        False(titles.Contains("plain no due"));
    }

    private static void TestCollapse()
    {
        List<TodoItem> items = new List<TodoItem>();
        for (int i = 0; i < 8; i++) items.Add(Item("item " + i, null, null, false, DateTime.UtcNow.AddMinutes(i).ToString("o")));
        VisibleTodoResult collapsed = TodoRules.VisibleItems(items, false, 5);
        Equal(5, collapsed.Items.Count);
        Equal(3, collapsed.HiddenCount);
        VisibleTodoResult expanded = TodoRules.VisibleItems(items, true, 5);
        Equal(8, expanded.Items.Count);
        Equal(0, expanded.HiddenCount);
    }

    private static void TestDayBoundary()
    {
        TodoItem dateOnly = Item("boundary", "2026-09-20", null, false, "2026-09-01T00:00:00Z");
        DateTime before = new DateTime(2026, 9, 21, 3, 59, 0);
        DateTime at = new DateTime(2026, 9, 21, 4, 0, 0);
        Equal(new DateTime(2026, 9, 20), TodoRules.LogicalDate(before, 240));
        Equal(new DateTime(2026, 9, 21), TodoRules.LogicalDate(at, 240));
        False(TodoRules.IsOverdue(dateOnly, before, 240));
        True(TodoRules.IsInLogicalToday(dateOnly, before, 240));
        True(TodoRules.IsOverdue(dateOnly, at, 240));
        True(TodoRules.IsInLogicalToday(dateOnly, at, 240));
    }

    private static void TestLayoutRules()
    {
        Rectangle work = new Rectangle(0, 0, 1920, 1080);
        PetLayout rightEdge = LayoutRules.Compute(new Point(1800, 900), new Size(96, 96), new Size(340, 420), work, true, 8);
        True(rightEdge.ListOnLeft);
        True(rightEdge.ListAlignedAbove);
        True(work.Contains(rightEdge.WindowBounds));
        Equal(new Point(0, 0), LayoutRules.RecoverPetPosition(new Point(-500, -20), new Size(96, 96), work));
        Equal(new Point(1824, 984), LayoutRules.SnapPetPosition(new Point(1818, 980), new Size(96, 96), work, 12));
        Equal(LayoutRules.MinimumOpacity, LayoutRules.ClampOpacity(0.01));
        False(LayoutRules.IsDrag(new Point(10, 10), new Point(13, 14), 5));
        True(LayoutRules.IsDrag(new Point(10, 10), new Point(16, 10), 5));

        Size bubble = new Size(240, 90);
        Point[] anchors = { new Point(900, 500), new Point(1800, 900), new Point(20, 900), new Point(1800, 10), new Point(10, 10) };
        foreach (Point anchor in anchors)
        {
            PetLayout plain = LayoutRules.Compute(anchor, new Size(128, 128), new Size(348, 476), work, true, 8);
            PetLayout withBubble = LayoutRules.Compute(anchor, new Size(128, 128), new Size(348, 476), bubble, work, true, 8);
            Rectangle petOnScreen = Offset(withBubble.PetBounds, withBubble.WindowBounds);
            Equal(Offset(plain.PetBounds, plain.WindowBounds), petOnScreen);
            Rectangle bubbleOnScreen = Offset(withBubble.BubbleBounds, withBubble.WindowBounds);
            Equal(bubble, bubbleOnScreen.Size);
            True(work.Contains(withBubble.WindowBounds));
            False(bubbleOnScreen.IntersectsWith(petOnScreen));
            if (anchor.Y > 200 && anchor.Y < 800)
                False(bubbleOnScreen.IntersectsWith(Offset(withBubble.ListBounds, withBubble.WindowBounds)));
        }
        PetLayout hiddenList = LayoutRules.Compute(new Point(900, 500), new Size(96, 96), new Size(348, 476), bubble, work, false, 8);
        True(hiddenList.ListBounds.IsEmpty);
        Equal(new Rectangle(828, 402, 240, 90), Offset(hiddenList.BubbleBounds, hiddenList.WindowBounds));
    }

    private static Rectangle Offset(Rectangle relative, Rectangle window)
    {
        return new Rectangle(relative.X + window.X, relative.Y + window.Y, relative.Width, relative.Height);
    }

    private static void TestTodoOperations()
    {
        string root = NewTestDirectory();
        string path = Path.Combine(root, "data.json");
        TodoService service = new TodoService(new TodoRepository(path));
        TodoItem created = service.Create("  示例任务  ", null, null, null, false);
        Equal("示例任务", created.Title);
        True(string.IsNullOrEmpty(created.DueDate));

        service.Update(created.Id, "修改后", "虚构备注", "2026-09-22", "15:30", true);
        Equal("修改后", service.Find(created.Id).Title);
        True(service.Find(created.Id).IsStarred);

        True(service.ToggleCompleted(created.Id));
        False(service.ToggleCompleted(created.Id));
        False(service.ToggleStarred(created.Id));
        service.Delete(created.Id);
        True(service.Find(created.Id).IsDeleted);
        service.UndoDelete(created.Id);
        False(service.Find(created.Id).IsDeleted);

        RoostData reloaded = new TodoRepository(path).Load();
        Equal(1, reloaded.Todos.Count);
        Equal("修改后", reloaded.Todos[0].Title);
        False(reloaded.Todos[0].IsDeleted);

        bool rejected = false;
        try { service.Create("   ", null, null, null, false); }
        catch (ArgumentException) { rejected = true; }
        True(rejected);
    }

    private static void TestFullscreenRules()
    {
        Rectangle monitor = new Rectangle(0, 0, 1920, 1080);
        True(FullscreenRules.IsFullscreen(new Rectangle(0, 0, 1920, 1080), monitor, true, false));
        True(FullscreenRules.IsFullscreen(new Rectangle(-1, 0, 1922, 1080), monitor, true, false));
        False(FullscreenRules.IsFullscreen(new Rectangle(0, 0, 1910, 1080), monitor, true, false));
        False(FullscreenRules.IsFullscreen(monitor, monitor, false, false));
        False(FullscreenRules.IsFullscreen(monitor, monitor, true, true));
    }

    private static void TestSettingsPersistence()
    {
        string root = NewTestDirectory();
        string path = Path.Combine(root, "data.json");
        TodoService service = new TodoService(new TodoRepository(path));
        service.Data.Settings.HasSavedPosition = true;
        service.Data.Settings.PetX = 777;
        service.Data.Settings.PetY = 333;
        service.Data.Settings.OnlyToday = true;
        service.Data.Settings.ListVisible = false;
        service.SaveSettings();
        RoostData reloaded = new TodoRepository(path).Load();
        True(reloaded.Settings.HasSavedPosition);
        Equal(777, reloaded.Settings.PetX);
        Equal(333, reloaded.Settings.PetY);
        True(reloaded.Settings.OnlyToday);
        False(reloaded.Settings.ListVisible);
    }

    private static void TestCrashDuringWrite()
    {
        string root = NewTestDirectory();
        string dataPath = Path.Combine(root, "data.json");
        TodoRepository repository = new TodoRepository(dataPath);
        RoostData data = new RoostData();
        data.Todos.Add(Item("original", null, null, false, "2026-09-01T00:00:00Z"));
        repository.Save(data);

        string ready = Path.Combine(root, "ready");
        Process worker = Process.Start(new ProcessStartInfo
        {
            FileName = Process.GetCurrentProcess().MainModule.FileName,
            Arguments = string.Format("--storage-worker \"{0}\" \"{1}\"", dataPath, ready),
            UseShellExecute = false,
            CreateNoWindow = true
        });
        Stopwatch wait = Stopwatch.StartNew();
        while (!File.Exists(ready) && wait.ElapsedMilliseconds < 5000) Thread.Sleep(20);
        True(File.Exists(ready));
        worker.Kill();
        worker.WaitForExit();
        RoostData reloaded = new TodoRepository(dataPath).Load();
        Equal(1, reloaded.Todos.Count);
        Equal("original", reloaded.Todos[0].Title);
    }

    private static void TestBackupRetention()
    {
        string root = NewTestDirectory();
        TodoRepository repository = new TodoRepository(Path.Combine(root, "data.json"));
        RoostData data = new RoostData();
        for (int i = 0; i < 10; i++)
        {
            data.Settings.PetX = i;
            repository.Save(data);
            Thread.Sleep(2);
        }
        Equal(7, repository.BackupCount());
        Equal(9, repository.Load().Settings.PetX);
    }

    private static void TestAiContextScope()
    {
        DateTime now = new DateTime(2026, 9, 23, 10, 0, 0);
        TodoItem open = Item("open", null, null, false, "2026-09-01T00:00:00Z");
        TodoItem recent = Item("recent", null, null, false, "2026-09-01T00:00:00Z");
        recent.IsCompleted = true;
        recent.CompletedAtUtc = now.ToUniversalTime().AddDays(-6).ToString("o");
        TodoItem old = Item("old", null, null, false, "2026-09-01T00:00:00Z");
        old.IsCompleted = true;
        old.CompletedAtUtc = now.ToUniversalTime().AddDays(-8).ToString("o");
        TodoItem deleted = Item("deleted", null, null, false, "2026-09-01T00:00:00Z");
        deleted.IsDeleted = true;
        AiRequestContext context = AiRequestContext.Create(new[] { old, deleted, recent, open }, now, 240);
        Equal(2, context.Items.Count);
        Equal("t1", context.Items[0].Key);
        Equal("open", context.Resolve("t1").Title);
        Equal("recent", context.Resolve("t2").Title);
        True(context.Resolve("t3") == null);
        string prompt = context.SystemPrompt();
        True(prompt.Contains("open"));
        True(prompt.Contains("recent"));
        False(prompt.Contains("标题：old"));
        False(prompt.Contains("标题：deleted"));
    }

    private static void TestAiPromptDates()
    {
        AiRequestContext early = AiRequestContext.Create(new TodoItem[0], new DateTime(2026, 9, 23, 1, 0, 0), 240);
        string prompt = early.SystemPrompt();
        True(prompt.Contains("「今天」是 2026-09-22 星期二，「明天」是 2026-09-23 星期三"));
        AiRequestContext morning = AiRequestContext.Create(new TodoItem[0], new DateTime(2026, 9, 23, 4, 0, 0), 240);
        True(morning.SystemPrompt().Contains("「今天」是 2026-09-23 星期三，「明天」是 2026-09-24 星期四"));
        True(prompt.Contains("2026-09-28 星期一（下周一）"));
    }

    private static void TestAiParseOperations()
    {
        DateTime now = new DateTime(2026, 9, 23, 10, 0, 0);
        TodoItem report = Item("周报", "2026-09-24", null, false, "2026-09-01T00:00:00Z");
        TodoItem gym = Item("健身", null, null, false, "2026-09-01T00:00:00Z");
        TodoItem trash = Item("扔垃圾", null, null, false, "2026-09-01T00:00:00Z");
        AiRequestContext context = AiRequestContext.Create(new[] { report, gym, trash }, now, 240);
        string aliasReport = AliasOf(context, report), aliasGym = AliasOf(context, gym), aliasTrash = AliasOf(context, trash);
        string reply = "```json\n{\"status\":\"ok\",\"operations\":[" +
            "{\"op\":\"add\",\"title\":\"复盘会\",\"date\":\"2026-09-24\",\"time\":\"15:00\",\"starred\":true}," +
            "{\"op\":\"add\",\"title\":\"交电费\",\"date\":\"2026-09-23\",\"time\":\"08:00\"}," +
            "{\"op\":\"update\",\"id\":\"" + aliasReport + "\",\"date\":\"2026-09-25\",\"time\":null}," +
            "{\"op\":\"complete\",\"id\":\"" + aliasGym + "\"}," +
            "{\"op\":\"delete\",\"id\":\"" + aliasTrash + "\"}]}\n```";
        AiPlan plan = AiPlanParser.Parse(reply, context);
        Equal(AiPlanStatus.Ok, plan.Status);
        Equal(5, plan.ValidCount);
        Equal("新增：9月24日 星期四 15:00（明天） 复盘会 ★", plan.Operations[0].Summary);
        False(plan.Operations[0].PastTimeWarning);
        True(plan.Operations[1].PastTimeWarning);
        Equal("修改「周报」：时间改为 9月25日 星期五（后天）", plan.Operations[2].Summary);
        Equal(AiOperationKind.Complete, plan.Operations[3].Kind);
        Equal(gym.Id, plan.Operations[3].TargetId);
        Equal("删除：「扔垃圾」", plan.Operations[4].Summary);
        Equal("删除 1 条，新增 2 条，修改 1 条，状态变更 1 条", plan.Headline());
    }

    private static void TestAiParseRejections()
    {
        DateTime now = new DateTime(2026, 9, 23, 10, 0, 0);
        TodoItem done = Item("已完成的事", null, null, false, "2026-09-01T00:00:00Z");
        done.IsCompleted = true;
        done.CompletedAtUtc = now.ToUniversalTime().ToString("o");
        TodoItem meetingA = Item("产品会", "2026-09-24", "10:00", false, "2026-09-01T00:00:00Z");
        TodoItem meetingB = Item("周会", "2026-09-24", "14:00", true, "2026-09-01T00:00:00Z");
        AiRequestContext context = AiRequestContext.Create(new[] { done, meetingA, meetingB }, now, 240);
        string aliasDone = AliasOf(context, done), aliasA = AliasOf(context, meetingA), aliasB = AliasOf(context, meetingB);

        AiPlan plan = AiPlanParser.Parse("{\"status\":\"ok\",\"operations\":[" +
            "{\"op\":\"complete\",\"id\":\"" + aliasDone + "\"}," +
            "{\"op\":\"delete\",\"id\":null,\"ref\":\"体检\"}," +
            "{\"op\":\"add\",\"title\":\"只有时刻\",\"date\":null,\"time\":\"09:00\"}," +
            "{\"op\":\"star\",\"id\":\"" + aliasB + "\"}," +
            "{\"op\":\"delete\",\"id\":\"" + aliasA + "\"}," +
            "{\"op\":\"update\",\"id\":\"" + aliasA + "\",\"title\":\"新标题\"}," +
            "{\"op\":\"rename_all\"}]}", context);
        Equal(AiPlanStatus.Ok, plan.Status);
        Equal(1, plan.ValidCount);
        False(plan.Operations[0].IsValid);
        False(plan.Operations[1].IsValid);
        Equal("删除：「体检」", plan.Operations[1].Summary);
        False(plan.Operations[2].IsValid);
        False(plan.Operations[3].IsValid);
        True(plan.Operations[4].IsValid);
        False(plan.Operations[5].IsValid);
        False(plan.Operations[6].IsValid);

        AiPlan ambiguous = AiPlanParser.Parse("{\"status\":\"ambiguous\",\"message\":\"有两个会\",\"candidates\":[\"" + aliasA + "\",\"" + aliasB + "\",\"t99\"]}", context);
        Equal(AiPlanStatus.Ambiguous, ambiguous.Status);
        Equal(0, ambiguous.Operations.Count);
        Equal(2, ambiguous.CandidateTitles.Count);

        AiPlan prose = AiPlanParser.Parse("抱歉，我不太明白你的意思。", context);
        Equal(AiPlanStatus.Unclear, prose.Status);
        Equal("抱歉，我不太明白你的意思。", prose.Message);
        AiPlan empty = AiPlanParser.Parse("{\"status\":\"ok\",\"operations\":[]}", context);
        Equal(AiPlanStatus.Unclear, empty.Status);
        Equal(AiPlanParser.MaxShownReplyLength + 1, AiPlanParser.TruncateReply(new string('字', 500)).Length);
    }

    private static void TestAiApplyAndUndo()
    {
        string root = NewTestDirectory();
        string path = Path.Combine(root, "data.json");
        TodoService service = new TodoService(new TodoRepository(path));
        TodoItem report = service.Create("周报", "虚构备注", "2026-09-24", null, false);
        TodoItem gym = service.Create("健身", null, null, null, false);
        TodoItem trash = service.Create("扔垃圾", null, null, null, true);
        byte[] beforeParse = File.ReadAllBytes(path);

        DateTime now = new DateTime(2026, 9, 23, 10, 0, 0);
        AiRequestContext context = AiRequestContext.Create(service.Data.Todos, now, 240);
        AiPlan plan = AiPlanParser.Parse("{\"status\":\"ok\",\"operations\":[" +
            "{\"op\":\"add\",\"title\":\"复盘会\",\"date\":\"2026-09-24\",\"time\":\"15:00\",\"starred\":true}," +
            "{\"op\":\"update\",\"id\":\"" + AliasOf(context, report) + "\",\"title\":\"周报终稿\",\"date\":\"2026-09-25\",\"time\":\"18:00\"}," +
            "{\"op\":\"complete\",\"id\":\"" + AliasOf(context, gym) + "\"}," +
            "{\"op\":\"unstar\",\"id\":\"" + AliasOf(context, trash) + "\"}," +
            "{\"op\":\"delete\",\"id\":\"" + AliasOf(context, trash) + "\"}]}", context);
        Equal(5, plan.ValidCount);
        True(File.ReadAllBytes(path).SequenceEqual(beforeParse));
        Equal(3, service.Data.Todos.Count);
        Equal("周报", service.Find(report.Id).Title);

        List<AiOperation> confirmed = new List<AiOperation> { plan.Operations[0], plan.Operations[1], plan.Operations[2], plan.Operations[4] };
        AiUndo undo = service.ApplyAi(confirmed);
        RoostData applied = new TodoRepository(path).Load();
        Equal(4, applied.Todos.Count);
        TodoItem created = applied.Todos.First(delegate(TodoItem item) { return item.Title == "复盘会"; });
        True(created.IsStarred);
        Equal("15:00", created.DueTime);
        TodoItem updated = applied.Todos.First(delegate(TodoItem item) { return item.Id == report.Id; });
        Equal("周报终稿", updated.Title);
        Equal("2026-09-25", updated.DueDate);
        Equal("虚构备注", updated.Notes);
        True(applied.Todos.First(delegate(TodoItem item) { return item.Id == gym.Id; }).IsCompleted);
        TodoItem removed = applied.Todos.First(delegate(TodoItem item) { return item.Id == trash.Id; });
        True(removed.IsDeleted);
        True(removed.IsStarred);

        service.UndoAi(undo);
        RoostData restored = new TodoRepository(path).Load();
        Equal(3, restored.Todos.Count);
        Equal("周报", restored.Todos.First(delegate(TodoItem item) { return item.Id == report.Id; }).Title);
        Equal("2026-09-24", restored.Todos.First(delegate(TodoItem item) { return item.Id == report.Id; }).DueDate);
        False(restored.Todos.First(delegate(TodoItem item) { return item.Id == gym.Id; }).IsCompleted);
        False(restored.Todos.First(delegate(TodoItem item) { return item.Id == trash.Id; }).IsDeleted);
    }

    private static void TestAiClientSuccess()
    {
        const string key = "sk-test-FAKE-0000";
        FakeServer server = FakeServer.Start(200, "{\"choices\":[{\"message\":{\"role\":\"assistant\",\"content\":\"{\\\"status\\\":\\\"unclear\\\"}\"}}]}", 0);
        AiEndpoint endpoint = new AiEndpoint { BaseUrl = server.BaseUrl + "/v1/", Model = "fake-model", ApiKey = key };
        string content = new AiClient().CompleteAsync(endpoint, "系统", "明天开会", 512, CancellationToken.None).GetAwaiter().GetResult();
        Equal("{\"status\":\"unclear\"}", content);
        string request = server.WaitRequest();
        True(request.StartsWith("POST /v1/chat/completions "));
        True(request.Contains("Authorization: Bearer " + key));
        True(request.Contains("\"model\":\"fake-model\""));
        True(request.Contains("\"temperature\":0"));
        True(request.Contains("明天开会"));
        Equal("https://x.example/v1/chat/completions", new AiEndpoint { BaseUrl = "https://x.example/v1/chat/completions/" }.ChatCompletionsUrl);
    }

    private static void TestAiClientStatusErrors()
    {
        const string key = "sk-test-FAKE-0000";
        ExpectFailure(401, "{\"error\":{\"message\":\"Incorrect API key provided: " + key + "\"}}", AiFailureKind.InvalidKey, key);
        ExpectFailure(403, "{}", AiFailureKind.InvalidKey, key);
        ExpectFailure(404, "{\"error\":{\"message\":\"model not found\"}}", AiFailureKind.NotFound, key);
        ExpectFailure(400, "{\"error\":{\"message\":\"Model Not Exist\"}}", AiFailureKind.Rejected, key);
        ExpectFailure(402, "{\"error\":{\"message\":\"Insufficient Balance\"}}", AiFailureKind.Quota, key);
        ExpectFailure(429, "{\"error\":\"rate limited\"}", AiFailureKind.RateLimited, key);
        ExpectFailure(503, "upstream down", AiFailureKind.Server, key);
        ExpectFailure(200, "<html>not json</html>", AiFailureKind.BadResponse, key);
    }

    private static void TestAiClientTimeoutCancelNetwork()
    {
        AiClient fast = new AiClient(TimeSpan.FromMilliseconds(400));
        FakeServer slow = FakeServer.Start(200, "{}", 3000);
        AiException timeout = Catch(delegate
        {
            fast.CompleteAsync(new AiEndpoint { BaseUrl = slow.BaseUrl, Model = "m", ApiKey = "k" }, "s", "u", 16, CancellationToken.None).GetAwaiter().GetResult();
        });
        Equal(AiFailureKind.Timeout, timeout.Kind);
        True(timeout.Retryable);

        FakeServer slowAgain = FakeServer.Start(200, "{}", 3000);
        CancellationTokenSource cancel = new CancellationTokenSource(150);
        AiException cancelled = Catch(delegate
        {
            new AiClient().CompleteAsync(new AiEndpoint { BaseUrl = slowAgain.BaseUrl, Model = "m", ApiKey = "k" }, "s", "u", 16, cancel.Token).GetAwaiter().GetResult();
        });
        Equal(AiFailureKind.Cancelled, cancelled.Kind);

        TcpListener closed = new TcpListener(IPAddress.Loopback, 0);
        closed.Start();
        int port = ((IPEndPoint)closed.LocalEndpoint).Port;
        closed.Stop();
        AiException network = Catch(delegate
        {
            new AiClient().CompleteAsync(new AiEndpoint { BaseUrl = "http://127.0.0.1:" + port, Model = "m", ApiKey = "k" }, "s", "u", 16, CancellationToken.None).GetAwaiter().GetResult();
        });
        Equal(AiFailureKind.Network, network.Kind);
        Equal(AiFailureKind.NotFound, Catch(delegate
        {
            new AiClient().CompleteAsync(new AiEndpoint { BaseUrl = "not a url", Model = "m", ApiKey = "k" }, "s", "u", 16, CancellationToken.None).GetAwaiter().GetResult();
        }).Kind);
    }

    private static void TestAiClientTemperatureFallback()
    {
        const string rejected = "{\"error\":{\"message\":\"invalid temperature: only 1 is allowed for this model\",\"type\":\"invalid_request_error\"}}";
        const string ok = "{\"choices\":[{\"message\":{\"content\":\"好的\"}}]}";

        FakeServer fallback = FakeServer.Start(new[] { 400, 200 }, new[] { rejected, ok });
        AiEndpoint endpoint = new AiEndpoint { BaseUrl = fallback.BaseUrl, Model = "kimi-k2.6", ApiKey = "k" };
        Equal("好的", new AiClient().CompleteAsync(endpoint, "s", "u", 16, CancellationToken.None).GetAwaiter().GetResult());
        List<string> requests = fallback.WaitRequests();
        Equal(2, requests.Count);
        True(requests[0].Contains("\"temperature\":0"));
        False(requests[1].Contains("temperature"));
        True(requests[1].Contains("\"model\":\"kimi-k2.6\""));

        FakeServer twice = FakeServer.Start(new[] { 400, 400, 200 }, new[] { rejected, rejected, ok });
        endpoint.BaseUrl = twice.BaseUrl;
        Equal(AiFailureKind.Rejected, Catch(delegate
        {
            new AiClient().CompleteAsync(endpoint, "s", "u", 16, CancellationToken.None).GetAwaiter().GetResult();
        }).Kind);
        Equal(2, twice.WaitRequests().Count);

        FakeServer unrelated = FakeServer.Start(new[] { 400, 200 }, new[] { "{\"error\":{\"message\":\"Model Not Exist\"}}", ok });
        endpoint.BaseUrl = unrelated.BaseUrl;
        Equal(AiFailureKind.Rejected, Catch(delegate
        {
            new AiClient().CompleteAsync(endpoint, "s", "u", 16, CancellationToken.None).GetAwaiter().GetResult();
        }).Kind);
        Equal(1, unrelated.WaitRequests().Count);
    }

    private static void TestCredentialStore()
    {
        string target = "Roost/Test/" + Guid.NewGuid().ToString("N");
        const string secret = "sk-test-FAKE-凭据-1234";
        try
        {
            True(CredentialStore.Read(target) == null);
            CredentialStore.Write(target, secret);
            Equal(secret, CredentialStore.Read(target));
            CredentialStore.Write(target, secret + "-v2");
            Equal(secret + "-v2", CredentialStore.Read(target));
        }
        finally
        {
            CredentialStore.Delete(target);
        }
        True(CredentialStore.Read(target) == null);
        CredentialStore.Delete(target);

        string root = NewTestDirectory();
        TodoService service = new TodoService(new TodoRepository(Path.Combine(root, "data.json")));
        service.Data.Settings.AiPresetId = "deepseek";
        service.Data.Settings.AiBaseUrl = "https://api.deepseek.com/v1";
        service.Data.Settings.AiModel = "deepseek-flash";
        service.SaveSettings();
        foreach (string file in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
        {
            string text = File.ReadAllText(file);
            False(text.Contains("sk-"));
            False(text.Contains("AiConfigured"));
        }
        True(new TodoRepository(Path.Combine(root, "data.json")).Load().Settings.AiConfigured);
    }

    private static void ExpectFailure(int status, string body, AiFailureKind kind, string key)
    {
        FakeServer server = FakeServer.Start(status, body, 0);
        AiException failure = Catch(delegate
        {
            new AiClient().CompleteAsync(new AiEndpoint { BaseUrl = server.BaseUrl, Model = "m", ApiKey = key }, "s", "u", 16, CancellationToken.None).GetAwaiter().GetResult();
        });
        if (failure.Kind != kind) throw new Exception(string.Format("status {0}: expected {1}, actual {2}", status, kind, failure.Kind));
        False(failure.Message.Contains(key));
    }

    private static AiException Catch(Action action)
    {
        try { action(); }
        catch (AiException exception) { return exception; }
        throw new Exception("expected AiException");
    }

    private sealed class FakeServer
    {
        private readonly TcpListener listener;
        private readonly Task<List<string>> handler;

        internal string BaseUrl { get; private set; }

        private FakeServer(int[] statuses, string[] bodies, int delayMilliseconds)
        {
            listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            BaseUrl = "http://127.0.0.1:" + ((IPEndPoint)listener.LocalEndpoint).Port;
            handler = Task.Run(delegate { return ServeAll(statuses, bodies, delayMilliseconds); });
        }

        internal static FakeServer Start(int status, string body, int delayMilliseconds)
        {
            return new FakeServer(new[] { status }, new[] { body }, delayMilliseconds);
        }

        internal static FakeServer Start(int[] statuses, string[] bodies)
        {
            return new FakeServer(statuses, bodies, 0);
        }

        internal string WaitRequest()
        {
            return handler.GetAwaiter().GetResult()[0];
        }

        // 按顺序应答；客户端不再发请求时（等待 2 秒无连接）提前结束。
        internal List<string> WaitRequests()
        {
            return handler.GetAwaiter().GetResult();
        }

        private List<string> ServeAll(int[] statuses, string[] bodies, int delayMilliseconds)
        {
            List<string> requests = new List<string>();
            try
            {
                for (int index = 0; index < statuses.Length; index++)
                {
                    if (index > 0 && !listener.Server.Poll(2000000, SelectMode.SelectRead)) break;
                    requests.Add(Serve(statuses[index], bodies[index], delayMilliseconds));
                }
            }
            finally
            {
                listener.Stop();
            }
            return requests;
        }

        private string Serve(int status, string body, int delayMilliseconds)
        {
            using (TcpClient client = listener.AcceptTcpClient())
            using (NetworkStream stream = client.GetStream())
            {
                MemoryStream received = new MemoryStream();
                byte[] buffer = new byte[8192];
                int headerEnd = -1, contentLength = 0;
                while (true)
                {
                    int read = stream.Read(buffer, 0, buffer.Length);
                    if (read <= 0) break;
                    received.Write(buffer, 0, read);
                    string sofar = Encoding.UTF8.GetString(received.ToArray());
                    if (headerEnd < 0)
                    {
                        headerEnd = sofar.IndexOf("\r\n\r\n", StringComparison.Ordinal);
                        if (headerEnd >= 0)
                        {
                            foreach (string line in sofar.Substring(0, headerEnd).Split(new[] { "\r\n" }, StringSplitOptions.None))
                                if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                                    contentLength = int.Parse(line.Substring(15).Trim());
                        }
                    }
                    if (headerEnd >= 0 && received.Length >= Encoding.UTF8.GetByteCount(sofar.Substring(0, headerEnd + 4)) + contentLength) break;
                }
                string request = Encoding.UTF8.GetString(received.ToArray());
                if (delayMilliseconds > 0) Thread.Sleep(delayMilliseconds);
                byte[] payload = Encoding.UTF8.GetBytes(body);
                string head = string.Format("HTTP/1.1 {0} X\r\nContent-Type: application/json; charset=utf-8\r\nContent-Length: {1}\r\nConnection: close\r\n\r\n", status, payload.Length);
                try
                {
                    byte[] headBytes = Encoding.ASCII.GetBytes(head);
                    stream.Write(headBytes, 0, headBytes.Length);
                    stream.Write(payload, 0, payload.Length);
                }
                catch (IOException) { }
                return request;
            }
        }
    }

    private static string AliasOf(AiRequestContext context, TodoItem item)
    {
        foreach (KeyValuePair<string, TodoItem> entry in context.Items)
            if (entry.Value.Id == item.Id) return entry.Key;
        throw new Exception("alias not found");
    }

    private static int RunStorageWorker(string[] args)
    {
        string dataPath = args[1];
        string readyPath = args[2];
        byte[] replacement = Encoding.UTF8.GetBytes("{\"FormatVersion\":1,\"Todos\":[],\"Settings\":{}}");
        AtomicFile.WriteAllBytes(dataPath, replacement, delegate
        {
            File.WriteAllText(readyPath, "ready");
            Thread.Sleep(30000);
        });
        return 0;
    }

    private static TodoItem Item(string title, string dueDate, string dueTime, bool star, string created)
    {
        return new TodoItem
        {
            Id = Guid.NewGuid().ToString("N"),
            Title = title,
            DueDate = dueDate,
            DueTime = dueTime,
            IsStarred = star,
            CreatedAtUtc = created,
            UpdatedAtUtc = created
        };
    }

    private static string NewTestDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "roost-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void Run(string name, Action test)
    {
        try
        {
            test();
            passed++;
            Console.WriteLine("PASS " + name);
        }
        catch (Exception exception)
        {
            failed++;
            Console.WriteLine("FAIL {0}: {1}", name, exception.Message);
        }
    }

    private static void True(bool value) { if (!value) throw new Exception("expected true"); }
    private static void False(bool value) { if (value) throw new Exception("expected false"); }
    private static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new Exception(string.Format("expected {0}, actual {1}", expected, actual));
    }
}
