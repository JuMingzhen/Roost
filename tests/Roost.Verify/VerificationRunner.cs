using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Roost.App;
using Roost.Core;

internal sealed class ClickSinkForm : Form
{
    internal int Clicks { get; private set; }

    internal ClickSinkForm(Rectangle bounds)
    {
        StartPosition = FormStartPosition.Manual;
        Bounds = bounds;
        FormBorderStyle = FormBorderStyle.None;
        BackColor = Color.Navy;
        ShowInTaskbar = false;
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left) Clicks++;
        base.OnMouseDown(e);
    }
}

internal static class VerificationRunner
{
    private static readonly JavaScriptSerializer Json = new JavaScriptSerializer();

    [STAThread]
    private static int Main(string[] args)
    {
        EnablePerMonitorDpi();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Usage: Roost.Verify.exe <system|pixel|cpu|ai> ...");
            return 64;
        }
        try
        {
            if (args[0] == "system") return RunSystem(args[1]);
            if (args[0] == "pixel") return RunPixel(args[1], args[2]);
            if (args[0] == "cpu") return RunCpu(args[1], int.Parse(args[2]), int.Parse(args[3]));
            if (args[0] == "ai") return RunAi(args[1]);
            return 64;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.ToString());
            return 70;
        }
    }

    private static void EnablePerMonitorDpi()
    {
        try
        {
            if (!NativeMethods.SetProcessDpiAwarenessContext(new IntPtr(-4))) NativeMethods.SetProcessDPIAware();
        }
        catch (EntryPointNotFoundException)
        {
            NativeMethods.SetProcessDPIAware();
        }
    }

    private static int RunSystem(string outputPath)
    {
        string root = NewTestDirectory();
        TodoService service = new TodoService(new TodoRepository(Path.Combine(root, "data.json")));
        service.Data.Settings.FirstRunCompleted = true;
        service.Data.Settings.ListVisible = true;
        service.SaveSettings();
        string assets = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "cat");
        uint message = NativeMethods.RegisterWindowMessage("Roost.Verify." + Guid.NewGuid().ToString("N"));
        PetForm pet = new PetForm(service, message, assets);
        ClickSinkForm sink = new ClickSinkForm(pet.Bounds);
        sink.TopMost = true;
        sink.Show();
        pet.Show();
        pet.BringToFront();
        Pump(500);

        List<Point> blank = FindPoints(pet, false, 12);
        foreach (Point point in blank)
        {
            pet.BringToFront();
            SendClick(pet.Left + point.X, pet.Top + point.Y);
            Pump(35);
        }
        int blankClicks = sink.Clicks;

        bool listBefore = service.Data.Settings.ListVisible;
        Point opaque = FindPetPoint(pet);
        pet.BringToFront();
        SendClick(pet.Left + opaque.X, pet.Top + opaque.Y);
        Pump(150);
        bool petClickHandled = service.Data.Settings.ListVisible != listBefore;
        bool trayLabels = pet.TrayLabelsForTest.Length == 3 && pet.TrayLabelsForTest[1] == "设置" && pet.TrayLabelsForTest[2] == "退出";
        bool visibleBeforeHotkey = pet.Visible;
        NativeMethods.SendMessageForTest(pet.Handle, NativeMethods.WM_HOTKEY, new IntPtr(NativeMethods.HOTKEY_ID), IntPtr.Zero);
        Pump(100);
        bool hiddenByHotkey = visibleBeforeHotkey && !pet.Visible;
        int frameBefore = pet.AnimationFrameCountForTest;
        Pump(400);
        bool pausedWhileHidden = pet.AnimationFrameCountForTest == frameBefore;
        NativeMethods.SendMessageForTest(pet.Handle, NativeMethods.WM_HOTKEY, new IntPtr(NativeMethods.HOTKEY_ID), IntPtr.Zero);
        Pump(100);
        pet.EvaluateFullscreenForTest(true);
        bool hiddenByFullscreen = !pet.Visible;
        pet.EvaluateFullscreenForTest(false);
        bool restoredAfterFullscreen = pet.Visible;

        bool pass = blank.Count == 12 && blankClicks == 12 && petClickHandled && trayLabels &&
                    pet.HotKeyRegisteredForTest && hiddenByHotkey && pausedWhileHidden && hiddenByFullscreen && restoredAfterFullscreen;
        Dictionary<string, object> result = Base("system");
        result["actualScalePercent"] = GetScalePercent(pet);
        result["blankPointsSent"] = blank.Count;
        result["blankClicksReceivedByUnderlyingWindow"] = blankClicks;
        result["petClickHandled"] = petClickHandled;
        result["trayMenuLabelsPresent"] = trayLabels;
        result["globalHotkeyRegistered"] = pet.HotKeyRegisteredForTest;
        result["hotkeyHandlerHides"] = hiddenByHotkey;
        result["animationPausedWhileHidden"] = pausedWhileHidden;
        result["fullscreenHides"] = hiddenByFullscreen;
        result["fullscreenExitRestores"] = restoredAfterFullscreen;
        result["overallPass"] = pass;
        result["status"] = pass ? "PASS" : "FAIL";
        WriteJson(outputPath, result);

        sink.Close();
        pet.CloseForTest();
        return pass ? 0 : 1;
    }

    private static int RunPixel(string outputPath, string screenshotDirectory)
    {
        Directory.CreateDirectory(screenshotDirectory);
        string assets = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "cat");
        SpriteView view = new SpriteView(assets);
        PetState[] states = new PetState[] { PetState.Idle, PetState.Hover, PetState.Drag, PetState.Celebrate, PetState.Reminder, PetState.Thinking };
        int[] sizes = new int[] { 96, 128, 160 };
        int[] scales = new int[] { 100, 125, 150 };
        List<Dictionary<string, object>> cases = new List<Dictionary<string, object>>();
        List<Dictionary<string, object>> actualCases = new List<Dictionary<string, object>>();
        bool allPass = true;
        foreach (PetState state in states)
        {
            HashSet<int> colors = view.SourceColorsForTest(state);
            foreach (int size in sizes)
            {
                foreach (int scale in scales)
                {
                    int physical = (int)Math.Round(size * scale / 100.0);
                    using (Bitmap rendered = view.RenderForTest(state, new Size(physical, physical)))
                    {
                        int unexpected = 0;
                        for (int y = 0; y < rendered.Height; y++)
                            for (int x = 0; x < rendered.Width; x++)
                                if (!colors.Contains(rendered.GetPixel(x, y).ToArgb())) unexpected++;
                        bool pass = unexpected == 0;
                        allPass = allPass && pass;
                        string fileName = string.Format("{0}-{1}px-{2}pct.png", state.ToString().ToLowerInvariant(), size, scale);
                        rendered.Save(Path.Combine(screenshotDirectory, fileName), ImageFormat.Png);
                        cases.Add(new Dictionary<string, object>
                        {
                            { "state", state.ToString() }, { "sizeTierPixels", size }, { "simulatedScalePercent", scale },
                            { "physicalPixels", physical }, { "unexpectedPixels", unexpected }, { "pass", pass }
                        });
                    }
                }
            }
        }

        Form host = new Form
        {
            StartPosition = FormStartPosition.Manual,
            Location = new Point(Screen.PrimaryScreen.WorkingArea.Left + 20, Screen.PrimaryScreen.WorkingArea.Top + 20),
            ClientSize = new Size(180, 180),
            FormBorderStyle = FormBorderStyle.None,
            ShowInTaskbar = false,
            TopMost = true,
            BackColor = Color.Magenta
        };
        host.Controls.Add(view);
        view.Location = Point.Empty;
        host.Show();
        Pump(200);
        int actualScale = GetScalePercent(view);
        bool actualPass = true;
        foreach (PetState state in states)
        {
            view.State = state;
            HashSet<int> colors = view.SourceColorsForTest(state);
            foreach (int size in sizes)
            {
                view.Size = new Size(size, size);
                host.ClientSize = view.Size;
                Pump(30);
                using (Bitmap capture = new Bitmap(size, size, PixelFormat.Format32bppArgb))
                {
                    view.DrawToBitmap(capture, new Rectangle(Point.Empty, capture.Size));
                    int unexpected = 0;
                    int spritePixels = 0;
                    for (int y = 0; y < capture.Height; y++)
                        for (int x = 0; x < capture.Width; x++)
                        {
                            int color = capture.GetPixel(x, y).ToArgb();
                            if (color == host.BackColor.ToArgb()) continue;
                            spritePixels++;
                            if (!colors.Contains(color)) unexpected++;
                        }
                    bool pass = spritePixels > 0 && unexpected == 0;
                    actualPass = actualPass && pass;
                    string fileName = string.Format("actual-{0}pct-{1}-{2}px.png", actualScale, state.ToString().ToLowerInvariant(), size);
                    capture.Save(Path.Combine(screenshotDirectory, fileName), ImageFormat.Png);
                    actualCases.Add(new Dictionary<string, object>
                    {
                        { "actualScalePercent", actualScale }, { "state", state.ToString() },
                        { "sizeTierPixels", size }, { "spritePixels", spritePixels },
                        { "unexpectedPixels", unexpected }, { "pass", pass }
                    });
                }
            }
        }
        host.Close();
        view.Dispose();
        Dictionary<string, object> result = Base("pixel");
        result["method"] = "Actual Roost assets rendered with the production nearest-neighbor path; exact ARGB palette membership";
        result["cases"] = cases;
        result["caseCount"] = cases.Count;
        result["actualScalePercent"] = actualScale;
        result["actualCases"] = actualCases;
        result["actualCaseCount"] = actualCases.Count;
        result["actualHardwarePass"] = actualPass;
        result["functionalPass"] = allPass && actualPass;
        result["hardwareCoverage"] = "current real Windows scale captured; repeat at 100/125/150 for complete coverage";
        result["overallPass"] = allPass && actualPass;
        result["status"] = allPass && actualPass ? "PASS_WITH_MANUAL_DPI_GATE" : "FAIL";
        WriteJson(outputPath, result);
        return allPass && actualPass ? 0 : 1;
    }

    private static int RunCpu(string outputPath, int visibleSeconds, int hiddenSeconds)
    {
        string root = NewTestDirectory();
        TodoService service = new TodoService(new TodoRepository(Path.Combine(root, "data.json")));
        service.Data.Settings.FirstRunCompleted = true;
        service.SaveSettings();
        string assets = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "cat");
        PetForm pet = new PetForm(service, NativeMethods.RegisterWindowMessage("Roost.Verify.Cpu." + Guid.NewGuid().ToString("N")), assets);
        pet.Show();
        pet.DisableFullscreenDetectionForTest();
        Pump(300);
        Process process = Process.GetCurrentProcess();
        List<long> memory = new List<long>();
        TimeSpan cpuStart = process.TotalProcessorTime;
        Stopwatch clock = Stopwatch.StartNew();
        long nextSample = 0;
        int startFrames = pet.AnimationFrameCountForTest;
        while (clock.Elapsed.TotalSeconds < visibleSeconds)
        {
            Application.DoEvents();
            if (clock.ElapsedMilliseconds >= nextSample)
            {
                process.Refresh();
                memory.Add(process.WorkingSet64);
                nextSample += 1000;
            }
            Thread.Sleep(10);
        }
        double visibleElapsed = clock.Elapsed.TotalSeconds;
        double cpu = (process.TotalProcessorTime - cpuStart).TotalSeconds / visibleElapsed / Environment.ProcessorCount * 100.0;
        int visibleFrames = pet.AnimationFrameCountForTest - startFrames;
        pet.ToggleVisibilityForTest();
        int hiddenStartFrames = pet.AnimationFrameCountForTest;
        Stopwatch hiddenClock = Stopwatch.StartNew();
        while (hiddenClock.Elapsed.TotalSeconds < hiddenSeconds) { Application.DoEvents(); Thread.Sleep(25); }
        int hiddenFrames = pet.AnimationFrameCountForTest - hiddenStartFrames;
        double average = 0;
        long maximum = 0;
        foreach (long value in memory) { average += value; if (value > maximum) maximum = value; }
        if (memory.Count > 0) average /= memory.Count;
        double averageMb = average / 1024.0 / 1024.0;
        double maximumMb = maximum / 1024.0 / 1024.0;
        bool pass = visibleSeconds >= 600 && cpu <= 0.5 && maximumMb <= 100.0 && visibleFrames > 0 && hiddenFrames == 0;
        Dictionary<string, object> result = Base("cpu");
        result["visibleDurationSeconds"] = visibleElapsed;
        result["hiddenDurationSeconds"] = hiddenClock.Elapsed.TotalSeconds;
        result["visibleAverageCpuPercent"] = Math.Round(cpu, 4);
        result["averageWorkingSetMb"] = Math.Round(averageMb, 2);
        result["maximumWorkingSetMb"] = Math.Round(maximumMb, 2);
        result["framesWhileVisible"] = visibleFrames;
        result["framesWhileHidden"] = hiddenFrames;
        result["overallPass"] = pass;
        result["status"] = pass ? "PASS" : "FAIL";
        WriteJson(outputPath, result);
        pet.CloseForTest();
        return pass ? 0 : 1;
    }

    private static int RunAi(string outputPath)
    {
        string credentialTarget = "Roost/Verify/" + Guid.NewGuid().ToString("N");
        Environment.SetEnvironmentVariable("ROOST_CREDENTIAL_TARGET", credentialTarget);
        Environment.SetEnvironmentVariable("ROOST_SKIP_HOTKEY_WARNING", "1");
        CredentialStore.Write(CredentialStore.ApiKeyTarget, "sk-verify-FAKE");
        ScriptedModelServer server = new ScriptedModelServer();
        Dictionary<string, object> result = Base("ai");
        PetForm pet = null;
        try
        {
            string root = NewTestDirectory();
            string dataPath = Path.Combine(root, "data.json");
            TodoService service = new TodoService(new TodoRepository(dataPath));
            DateTime today = TodoRules.LogicalDate(DateTime.Now, service.Data.Settings.DayStartMinutes);
            service.Create("周报", null, today.AddDays(2).ToString("yyyy-MM-dd"), null, false);
            service.Create("健身", null, null, null, false);
            service.Data.Settings.FirstRunCompleted = true;
            service.Data.Settings.ListVisible = true;
            service.Data.Settings.AiBaseUrl = server.BaseUrl + "/v1";
            service.Data.Settings.AiModel = "fake-model";
            service.Data.Settings.AiPrivacyAcknowledged = true;
            service.SaveSettings();
            string assets = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "cat");
            pet = new PetForm(service, NativeMethods.RegisterWindowMessage("Roost.Verify.Ai." + Guid.NewGuid().ToString("N")), assets);
            pet.Show();
            pet.DisableFullscreenDetectionForTest();
            Pump(300);

            string plan = "{\"status\":\"ok\",\"operations\":[" +
                "{\"op\":\"add\",\"title\":\"复盘会\",\"date\":\"" + today.AddDays(1).ToString("yyyy-MM-dd") + "\",\"time\":\"15:00\",\"starred\":true}," +
                "{\"op\":\"delete\",\"id\":\"t2\"}]}";
            const string sentence = "明天下午三点复盘会很重要，健身不要了";

            // 1. 取消预览：思考动画出现，预览期间和取消后数据都不变，原文保留。
            byte[] original = File.ReadAllBytes(dataPath);
            server.Enqueue(200, ChatBody(plan), 400);
            PreviewProbe cancelProbe = new PreviewProbe(dataPath, original, DialogResult.Cancel);
            Talk(pet, sentence);
            Pump(150);
            bool thinkingShown = pet.ThinkingForTest && pet.PetStateForTest == PetState.Thinking;
            WaitUntil(delegate { return cancelProbe.Handled && !pet.ThinkingForTest; }, 5000);
            cancelProbe.Stop();
            bool deleteListedFirst = cancelProbe.RowTexts.Count == 2 && cancelProbe.RowTexts[0].StartsWith("删除：「健身」");
            bool cancelKeepsData = cancelProbe.UnchangedWhileOpen && File.ReadAllBytes(dataPath).SequenceEqualTo(original) &&
                                   service.Data.Todos.Count == 2 && pet.TalkTextForTest == sentence && pet.PetStateForTest != PetState.Thinking;
            string firstRequest = server.LastRequest;
            bool requestCarriesContext = firstRequest != null && firstRequest.Contains("Bearer sk-verify-FAKE") &&
                                         firstRequest.Contains("周报") && firstRequest.Contains("\"model\":\"fake-model\"");

            // 2. 确认：整批生效，出现「撤销这次改动」；撤销后整批还原。
            server.Enqueue(200, ChatBody(plan), 0);
            PreviewProbe confirmProbe = new PreviewProbe(dataPath, original, DialogResult.OK);
            Talk(pet, sentence);
            WaitUntil(delegate { return confirmProbe.Handled && pet.UndoVisibleForTest; }, 5000);
            confirmProbe.Stop();
            TodoItem gym = service.Data.Todos.Find(delegate(TodoItem item) { return item.Title == "健身"; });
            TodoItem review = service.Data.Todos.Find(delegate(TodoItem item) { return item.Title == "复盘会"; });
            bool confirmApplies = confirmProbe.UnchangedWhileOpen && gym != null && gym.IsDeleted && review != null &&
                                  review.IsStarred && review.DueTime == "15:00" && pet.UndoLabelForTest == "已应用 AI 的改动" &&
                                  pet.TalkTextForTest.Length == 0;
            pet.RunUndoForTest();
            RoostData undone = new TodoRepository(dataPath).Load();
            bool undoRestores = undone.Todos.Count == 2 && !undone.Todos.Exists(delegate(TodoItem item) { return item.IsDeleted || item.Title == "复盘会"; });

            // 3. 失败路径：数据不变，原文保留，宠物冒泡说明原因。
            byte[] beforeFailures = File.ReadAllBytes(dataPath);
            bool invalidKey = Failure(pet, server, 401, "{\"error\":{\"message\":\"Incorrect API key provided: sk-verify-FAKE\"}}", "API Key 无效", "sk-verify-FAKE");
            bool unclear = Failure(pet, server, 200, ChatBody("抱歉，我不明白。"), "我没听懂：抱歉，我不明白。", null);
            bool ambiguous = Failure(pet, server, 200, ChatBody("{\"status\":\"ambiguous\",\"message\":\"两条都可能\",\"candidates\":[\"t1\",\"t2\"]}"), "「周报」「健身」", null);
            bool serverDown = Failure(pet, server, 503, "down", "按回车就能重试", null);

            server.Enqueue(200, ChatBody(plan), 3000);
            int requestsBeforeCancel = server.RequestCount;
            Talk(pet, sentence);
            Pump(200);
            pet.CancelTalkForTest();
            WaitUntil(delegate { return !pet.ThinkingForTest; }, 3000);
            Pump(100);
            bool cancelRequest = !pet.ThinkingForTest && pet.BubbleMessageForTest == null && pet.TalkTextForTest == sentence;
            WaitUntil(delegate { return server.RequestCount > requestsBeforeCancel; }, 4000);
            bool failuresKeepData = File.ReadAllBytes(dataPath).SequenceEqualTo(beforeFailures);

            service.Data.Settings.AiBaseUrl = null;
            int requestsBefore = server.RequestCount;
            Talk(pet, sentence);
            Pump(200);
            string notConfiguredMessage = pet.BubbleMessageForTest;
            bool notConfigured = notConfiguredMessage != null && notConfiguredMessage.Contains("还没有配置模型") &&
                                 notConfiguredMessage.Contains("不配置也能正常使用本地待办") && server.RequestCount == requestsBefore;

            // 4. 模型测试题（PRD 9.6）：只发虚构待办，跑完给出得分。
            service.Create("私人待办-甲乙丙", null, null, null, false);
            int requestsBeforeTest = server.RequestCount;
            AiSelfTestForm test = new AiSelfTestForm(
                new AiEndpoint { BaseUrl = server.BaseUrl + "/v1", Model = "fake-model", ApiKey = "sk-verify-FAKE" },
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "ai-eval", "cases.json"));
            for (int i = 0; i < test.QuestionCountForTest; i++) server.Enqueue(200, ChatBody("{\"status\":\"unclear\",\"message\":\"不明白\"}"), 0);
            test.Show();
            EnsureSyncContext();
            test.StartForTest();
            WaitUntil(delegate { return !test.RunningForTest; }, 30000);
            string testSummary = test.SummaryForTest;
            List<string> testRequests = server.RequestsSince(requestsBeforeTest);
            bool modelTest = test.QuestionCountForTest >= 12 && testRequests.Count == test.QuestionCountForTest &&
                             testSummary.StartsWith("答对 3 / " + test.QuestionCountForTest + " 题") && !testSummary.Contains("⚠") &&
                             test.DetailsForTest.Contains("你说：周报写完了") &&
                             !testRequests.Exists(delegate(string request) { return request.Contains("私人待办-甲乙丙"); });
            if (!modelTest)
                Console.Error.WriteLine("Model test: questions={0} requests={1} summary={2}", test.QuestionCountForTest, testRequests.Count, testSummary);
            test.Close();

            bool pass = thinkingShown && deleteListedFirst && cancelKeepsData && requestCarriesContext && confirmApplies && undoRestores &&
                        invalidKey && unclear && ambiguous && serverDown && cancelRequest && failuresKeepData && notConfigured && modelTest;
            result["thinkingAnimationWhileWaiting"] = thinkingShown;
            result["deleteListedFirstInPreview"] = deleteListedFirst;
            result["previewAndCancelLeaveDataUnchanged"] = cancelKeepsData;
            result["requestCarriesKeyModelAndTodos"] = requestCarriesContext;
            result["confirmAppliesBatch"] = confirmApplies;
            result["undoRestoresBatch"] = undoRestores;
            result["invalidKeyBubbleWithoutKey"] = invalidKey;
            result["unclearShowsModelReply"] = unclear;
            result["ambiguousListsCandidates"] = ambiguous;
            result["serverErrorOffersRetry"] = serverDown;
            result["userCancelKeepsInput"] = cancelRequest;
            result["failuresLeaveDataUnchanged"] = failuresKeepData;
            result["notConfiguredGuidesToSettings"] = notConfigured;
            result["modelTestRunsOnFictionalTodosOnly"] = modelTest;
            result["overallPass"] = pass;
            result["status"] = pass ? "PASS" : "FAIL";
            WriteJson(outputPath, result);
            return pass ? 0 : 1;
        }
        finally
        {
            if (pet != null) pet.CloseForTest();
            server.Stop();
            CredentialStore.Delete(credentialTarget);
        }
    }

    private static bool Failure(PetForm pet, ScriptedModelServer server, int status, string body, string expected, string forbidden)
    {
        const string sentence = "把会改到四点";
        server.Enqueue(status, body, 0);
        Talk(pet, sentence);
        WaitUntil(delegate { return !pet.ThinkingForTest && pet.BubbleMessageForTest != null; }, 5000);
        string message = pet.BubbleMessageForTest;
        bool ok = message != null && message.Contains(expected) && (forbidden == null || !message.Contains(forbidden)) &&
                  pet.TalkTextForTest == sentence;
        if (!ok) Console.Error.WriteLine("AI failure case {0} unexpected bubble: {1}", status, message == null ? "(none)" : "(present)");
        return ok;
    }

    private static void Talk(PetForm pet, string sentence)
    {
        EnsureSyncContext();
        pet.SendTalkForTest(sentence);
    }

    private static void EnsureSyncContext()
    {
        // 验证器没有 Application.Run 主循环：模态预览关闭后 WinForms 会卸载同步上下文，
        // 下一次 await 的后续代码就会跑到线程池上。真实应用有主循环，不受影响。
        if (!(SynchronizationContext.Current is WindowsFormsSynchronizationContext))
            SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
    }

    private static string ChatBody(string content)
    {
        Dictionary<string, object> message = new Dictionary<string, object> { { "role", "assistant" }, { "content", content } };
        Dictionary<string, object> choice = new Dictionary<string, object> { { "index", 0 }, { "message", message } };
        return Json.Serialize(new Dictionary<string, object> { { "choices", new object[] { choice } } });
    }

    private static void WaitUntil(Func<bool> condition, int milliseconds)
    {
        Stopwatch clock = Stopwatch.StartNew();
        while (!condition() && clock.ElapsedMilliseconds < milliseconds) { Application.DoEvents(); Thread.Sleep(5); }
    }

    private static bool SequenceEqualTo(this byte[] left, byte[] right)
    {
        if (left.Length != right.Length) return false;
        for (int i = 0; i < left.Length; i++) if (left[i] != right[i]) return false;
        return true;
    }

    private static List<Point> FindPoints(PetForm form, bool visible, int count)
    {
        List<Point> points = new List<Point>();
        for (int y = 3; y < form.ClientSize.Height - 3 && points.Count < count; y += 7)
            for (int x = 3; x < form.ClientSize.Width - 3 && points.Count < count; x += 7)
                if (form.HitTestForTest(new Point(x, y)) == visible) points.Add(new Point(x, y));
        if (points.Count < count) throw new InvalidOperationException("无法找到足够的命中测试点。");
        return points;
    }

    private static Point FindPetPoint(PetForm form)
    {
        Rectangle sprite = form.SpriteBoundsForTest;
        Point center = new Point(sprite.Left + sprite.Width / 2, sprite.Top + sprite.Height / 2);
        if (form.HitTestForTest(center)) return center;
        for (int y = sprite.Top; y < sprite.Bottom; y += 2)
            for (int x = sprite.Left; x < sprite.Right; x += 2)
                if (form.HitTestForTest(new Point(x, y))) return new Point(x, y);
        throw new InvalidOperationException("宠物区域没有可点击像素。");
    }

    private static void Pump(int milliseconds)
    {
        Stopwatch clock = Stopwatch.StartNew();
        while (clock.ElapsedMilliseconds < milliseconds) { Application.DoEvents(); Thread.Sleep(5); }
    }

    private static string NewTestDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "roost-verify-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static Dictionary<string, object> Base(string name)
    {
        return new Dictionary<string, object>
        {
            { "test", name }, { "timestampUtc", DateTime.UtcNow.ToString("o") },
            { "osVersion", Environment.OSVersion.VersionString }, { "logicalProcessors", Environment.ProcessorCount }
        };
    }

    private static int GetScalePercent(Control control)
    {
        using (Graphics graphics = control.CreateGraphics())
        {
            return (int)Math.Round(graphics.DpiX / 96.0 * 100.0);
        }
    }

    private static void WriteJson(string path, Dictionary<string, object> result)
    {
        string full = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(full));
        File.WriteAllText(full, Json.Serialize(result));
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool SetCursorPos(int x, int y);
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extra);

    private static void SendClick(int x, int y)
    {
        SetCursorPos(x, y);
        mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero);
        mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);
    }
}

internal sealed class PreviewProbe
{
    private readonly System.Windows.Forms.Timer timer;

    internal bool Handled { get; private set; }
    internal bool UnchangedWhileOpen { get; private set; }
    internal List<string> RowTexts { get; private set; }

    internal PreviewProbe(string dataPath, byte[] expected, DialogResult answer)
    {
        RowTexts = new List<string>();
        timer = new System.Windows.Forms.Timer { Interval = 50 };
        timer.Tick += delegate
        {
            foreach (Form form in Application.OpenForms)
            {
                AiPreviewForm preview = form as AiPreviewForm;
                if (preview == null || Handled) continue;
                Handled = true;
                timer.Stop();
                RowTexts = preview.RowTextsForTest;
                byte[] current = File.ReadAllBytes(dataPath);
                bool same = current.Length == expected.Length;
                for (int i = 0; same && i < current.Length; i++) same = current[i] == expected[i];
                UnchangedWhileOpen = same;
                preview.DialogResult = answer;
                return;
            }
        };
        timer.Start();
    }

    internal void Stop()
    {
        timer.Stop();
        timer.Dispose();
    }
}

internal sealed class ScriptedModelServer
{
    private readonly TcpListener listener;
    private readonly Queue<object[]> script = new Queue<object[]>();
    private readonly List<string> requests = new List<string>();
    private readonly Thread worker;
    private volatile bool stopping;
    private int requestCount;
    private string lastRequest;

    internal string BaseUrl { get; private set; }
    internal int RequestCount { get { return Thread.VolatileRead(ref requestCount); } }
    internal string LastRequest { get { lock (script) return lastRequest; } }

    internal List<string> RequestsSince(int index)
    {
        lock (script) return requests.GetRange(index, requests.Count - index);
    }

    internal ScriptedModelServer()
    {
        listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        BaseUrl = "http://127.0.0.1:" + ((IPEndPoint)listener.LocalEndpoint).Port;
        worker = new Thread(Serve) { IsBackground = true };
        worker.Start();
    }

    internal void Enqueue(int status, string body, int delayMilliseconds)
    {
        lock (script) script.Enqueue(new object[] { status, body, delayMilliseconds });
    }

    internal void Stop()
    {
        stopping = true;
        listener.Stop();
    }

    private void Serve()
    {
        while (!stopping)
        {
            TcpClient client;
            try { client = listener.AcceptTcpClient(); }
            catch (SocketException) { return; }
            catch (ObjectDisposedException) { return; }
            using (client)
            using (NetworkStream stream = client.GetStream())
            {
                try
                {
                    string request = ReadRequest(stream);
                    object[] entry;
                    lock (script)
                    {
                        lastRequest = request;
                        requests.Add(request);
                        entry = script.Count > 0 ? script.Dequeue() : new object[] { 500, "no script", 0 };
                    }
                    Interlocked.Increment(ref requestCount);
                    if ((int)entry[2] > 0) Thread.Sleep((int)entry[2]);
                    byte[] payload = Encoding.UTF8.GetBytes((string)entry[1]);
                    byte[] head = Encoding.ASCII.GetBytes(string.Format("HTTP/1.1 {0} X\r\nContent-Type: application/json; charset=utf-8\r\nContent-Length: {1}\r\nConnection: close\r\n\r\n", entry[0], payload.Length));
                    stream.Write(head, 0, head.Length);
                    stream.Write(payload, 0, payload.Length);
                }
                catch (IOException) { }
            }
        }
    }

    private static string ReadRequest(NetworkStream stream)
    {
        MemoryStream received = new MemoryStream();
        byte[] buffer = new byte[8192];
        while (true)
        {
            int read = stream.Read(buffer, 0, buffer.Length);
            if (read <= 0) break;
            received.Write(buffer, 0, read);
            byte[] bytes = received.ToArray();
            string text = Encoding.UTF8.GetString(bytes);
            int headerEnd = text.IndexOf("\r\n\r\n", StringComparison.Ordinal);
            if (headerEnd < 0) continue;
            int contentLength = 0;
            foreach (string line in text.Substring(0, headerEnd).Split(new[] { "\r\n" }, StringSplitOptions.None))
                if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase)) contentLength = int.Parse(line.Substring(15).Trim());
            if (bytes.Length >= Encoding.UTF8.GetByteCount(text.Substring(0, headerEnd + 4)) + contentLength) break;
        }
        return Encoding.UTF8.GetString(received.ToArray());
    }
}
