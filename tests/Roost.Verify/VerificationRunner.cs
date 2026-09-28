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
            Console.Error.WriteLine("Usage: Roost.Verify.exe <system|pixel|cpu|ai|layout> ...");
            return 64;
        }
        try
        {
            if (args[0] == "system") return RunSystem(args[1]);
            if (args[0] == "pixel") return RunPixel(args[1], args[2]);
            if (args[0] == "cpu") return RunCpu(args[1], int.Parse(args[2]), int.Parse(args[3]));
            if (args[0] == "ai") return RunAi(args[1]);
            if (args[0] == "layout") return RunLayout(args[1], args[2]);
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
        // 低于 100% 的透明度会让窗口变成分层窗口，圆角和点击穿透要在这种情况下也成立。
        service.Data.Settings.Opacity = 0.85;
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
        List<string> cornerColors = new List<string>();
        bool listCornersRounded = ListCornersRounded(pet, sink.BackColor, Path.ChangeExtension(outputPath, ".corner.png"), cornerColors);

        List<Point> blank = FindPoints(pet, false, 12);
        foreach (Point point in blank)
        {
            pet.BringToFront();
            SendClick(pet.Left + point.X, pet.Top + point.Y);
            Pump(35);
        }
        int blankClicks = sink.Clicks;

        pet.BringToFront();
        pet.ClickSettingsButtonForTest();
        Pump(200);
        bool settingsFromList = false;
        foreach (Form open in Application.OpenForms) if (open is SettingsForm) { settingsFromList = true; open.Close(); break; }
        bool settingsEntries = settingsFromList && pet.PetMenuHasSettingsForTest && pet.TrayIconCustomForTest;
        pet.BringToFront();
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
        bool hiddenByHotkey = visibleBeforeHotkey && !pet.Visible && !pet.ListWindowForTest.Visible;
        int frameBefore = pet.AnimationFrameCountForTest;
        Pump(400);
        bool pausedWhileHidden = pet.AnimationFrameCountForTest == frameBefore;
        NativeMethods.SendMessageForTest(pet.Handle, NativeMethods.WM_HOTKEY, new IntPtr(NativeMethods.HOTKEY_ID), IntPtr.Zero);
        Pump(100);
        pet.EvaluateFullscreenForTest(true);
        bool hiddenByFullscreen = !pet.Visible;
        pet.EvaluateFullscreenForTest(false);
        bool restoredAfterFullscreen = pet.Visible;

        bool pass = blank.Count == 12 && blankClicks == 12 && petClickHandled && trayLabels && settingsEntries && listCornersRounded &&
                    pet.HotKeyRegisteredForTest && hiddenByHotkey && pausedWhileHidden && hiddenByFullscreen && restoredAfterFullscreen;
        Dictionary<string, object> result = Base("system");
        result["actualScalePercent"] = GetScalePercent(pet);
        result["blankPointsSent"] = blank.Count;
        result["blankClicksReceivedByUnderlyingWindow"] = blankClicks;
        result["petClickHandled"] = petClickHandled;
        result["trayMenuLabelsPresent"] = trayLabels;
        result["settingsOpensFromListButton"] = settingsFromList;
        result["petRightClickMenuHasSettings"] = pet.PetMenuHasSettingsForTest;
        result["trayIconIsCat"] = pet.TrayIconCustomForTest;
        result["listCornersRounded"] = listCornersRounded;
        result["listCornerColors"] = cornerColors.ToArray();
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
                int shown = DpiScale.Px(size);
                view.Size = new Size(shown, shown);
                host.ClientSize = view.Size;
                Pump(30);
                using (Bitmap capture = new Bitmap(shown, shown, PixelFormat.Format32bppArgb))
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
                        { "sizeTierPixels", size }, { "physicalPixels", shown }, { "spritePixels", spritePixels },
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
        string providerTarget = null;
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
            providerTarget = CredentialStore.ApiKeyTargetFor(service.Data.Settings);
            bool legacyKeyMigrated = CredentialStore.Read(CredentialStore.ApiKeyTarget) == null &&
                                     CredentialStore.Read(providerTarget) == "sk-verify-FAKE" &&
                                     providerTarget.EndsWith("/custom/127.0.0.1");
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
                        invalidKey && unclear && ambiguous && serverDown && cancelRequest && failuresKeepData && notConfigured && modelTest && legacyKeyMigrated;
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
            result["legacyKeyMigratedToProvider"] = legacyKeyMigrated;
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
            if (providerTarget != null) CredentialStore.Delete(providerTarget);
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

    // 在当前真实缩放下打开每个窗口，检查文字放得下、同级控件不重叠、控件不超出父容器。
    private static int RunLayout(string outputPath, string screenshotDirectory)
    {
        Directory.CreateDirectory(screenshotDirectory);
        Environment.SetEnvironmentVariable("ROOST_SKIP_HOTKEY_WARNING", "1");
        List<string> problems = new List<string>();
        List<string> checkedForms = new List<string>();
        int scale = 0;

        RoostSettings settings = new RoostSettings { AiPresetId = "deepseek", AiBaseUrl = "https://api.deepseek.com/v1", AiModel = "deepseek-flash" };
        Environment.SetEnvironmentVariable("ROOST_CREDENTIAL_TARGET", "Roost/Verify/" + Guid.NewGuid().ToString("N"));
        string keyTarget = CredentialStore.ApiKeyTargetFor(settings);
        CredentialStore.Write(keyTarget, "sk-verify-FAKE-ABCD");
        SettingsForm settingsForm = new SettingsForm(settings);
        ShowOffscreen(settingsForm);
        scale = GetScalePercent(settingsForm);
        for (int i = 0; i < settingsForm.PageCountForTest; i++)
        {
            string name = "设置/" + settingsForm.PageNameForTest(i);
            settingsForm.ShowPageForTest(i);
            Pump(50);
            CheckLayout(settingsForm, name, problems);
            Capture(settingsForm, Path.Combine(screenshotDirectory, "settings-" + i + ".png"));
            checkedForms.Add(name);
        }
        settingsForm.ShowAiTab();
        bool maskedKeyShown = settingsForm.KeyStatusForTest == "已保存：sk-…ABCD";
        if (!maskedKeyShown) problems.Add("设置/AI 模型 已保存 key 的掩码显示不对：" + settingsForm.KeyStatusForTest);
        settingsForm.ShowKeyEditorForTest();
        Pump(50);
        CheckLayout(settingsForm, "设置/AI 模型（更换 key）", problems);
        Capture(settingsForm, Path.Combine(screenshotDirectory, "settings-key-editor.png"));
        checkedForms.Add("设置/AI 模型（更换 key）");
        settingsForm.Close();
        CredentialStore.Delete(keyTarget);

        TodoItem sample = new TodoItem { Title = "虚构待办：整理季度材料", Notes = "虚构备注", DueDate = "2026-09-24", DueTime = "15:00", IsStarred = true };
        TodoEditorForm editor = new TodoEditorForm(sample);
        CheckForm(editor, "编辑待办", screenshotDirectory, problems, checkedForms);

        List<TodoItem> todos = new List<TodoItem>();
        foreach (string title in new[] { "健身", "周报", "交房租" }) todos.Add(new TodoItem { Title = title });
        AiRequestContext context = AiRequestContext.Create(todos, new DateTime(2026, 9, 23, 10, 0, 0), 240);
        AiPlan plan = AiPlanParser.Parse("{\"status\":\"ok\",\"operations\":[" +
            "{\"op\":\"add\",\"title\":\"复盘会\",\"date\":\"2026-09-24\",\"time\":\"15:00\",\"starred\":true}," +
            "{\"op\":\"add\",\"title\":\"交电费\",\"date\":\"2026-09-23\",\"time\":\"08:00\"}," +
            "{\"op\":\"complete\",\"id\":\"t2\"},{\"op\":\"delete\",\"id\":\"t1\"},{\"op\":\"delete\",\"id\":null,\"ref\":\"体检\"}]}", context);
        CheckForm(new AiPreviewForm(plan), "AI 预览", screenshotDirectory, problems, checkedForms);
        CheckForm(new AiSelfTestForm(new AiEndpoint { BaseUrl = "http://127.0.0.1:1", Model = "deepseek-flash", ApiKey = "x" },
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "ai-eval", "cases.json")), "模型测试题", screenshotDirectory, problems, checkedForms);

        string root = NewTestDirectory();
        TodoService service = new TodoService(new TodoRepository(Path.Combine(root, "data.json")));
        service.Create("虚构待办：给植物浇水", null, "2026-09-24", "09:00", true);
        service.Create("虚构待办：买牙膏", null, null, null, false);
        foreach (string title in new[] { "虚构待办：一个很长很长的标题用来检查截断是否正常显示", "虚构待办：还书", "虚构待办：订机票", "虚构待办：整理相册", "虚构待办：预约牙医" })
            service.Create(title, null, DateTime.Today.ToString("yyyy-MM-dd"), null, false);
        service.Data.Settings.FirstRunCompleted = true;
        service.Data.Settings.ListVisible = true;
        service.SaveSettings();
        PetForm pet = new PetForm(service, NativeMethods.RegisterWindowMessage("Roost.Verify.Layout." + Guid.NewGuid().ToString("N")), Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "cat"));
        pet.Show();
        pet.DisableFullscreenDetectionForTest();
        Pump(300);
        CheckLayout(pet.ListPanelForTest, "清单", problems);
        Capture(pet.ListPanelForTest, Path.Combine(screenshotDirectory, "list.png"));
        checkedForms.Add("清单");

        // 右键菜单：在屏幕上弹出后截图留证，并检查菜单项排版。
        ContextMenuStrip menu = pet.MenuForTest;
        menu.Show(new Point(40, 40));
        Pump(150);
        CheckLayout(menu, "右键菜单", problems);
        using (Bitmap shot = new Bitmap(Math.Max(1, menu.Width), Math.Max(1, menu.Height)))
        {
            using (Graphics graphics = Graphics.FromImage(shot)) graphics.CopyFromScreen(menu.Location, Point.Empty, shot.Size);
            shot.Save(Path.Combine(screenshotDirectory, "menu.png"), ImageFormat.Png);
        }
        checkedForms.Add("右键菜单");
        menu.Close();
        Pump(50);

        pet.ShowBubbleForTest("还没有配置模型，暂时不能跟我说话。不配置也能正常使用本地待办。", "去设置");
        Pump(100);
        // 气泡窗口要真的显示出来，否则子控件都算不可见，排版检查会漏掉。
        if (!pet.BubbleForTest.Visible) problems.Add("气泡窗口没有显示");
        CheckLayout(pet.BubbleForTest, "气泡", problems);
        Capture(pet.BubbleForTest, Path.Combine(screenshotDirectory, "bubble.png"));
        checkedForms.Add("气泡");

        // 编辑模式：全选后批量删除，出现撤销条；撤销后整批恢复。
        pet.SetEditingForTest(true);
        pet.SelectAllForTest();
        Pump(100);
        CheckLayout(pet.ListPanelForTest, "清单（编辑模式）", problems);
        Capture(pet.ListPanelForTest, Path.Combine(screenshotDirectory, "list-editing.png"));
        checkedForms.Add("清单（编辑模式）");
        pet.DeleteSelectedForTest();
        Pump(100);
        int remaining = service.Data.Todos.FindAll(delegate(TodoItem item) { return !item.IsDeleted; }).Count;
        if (pet.EditingForTest || remaining != 0 || !pet.UndoVisibleForTest || pet.UndoLabelForTest != "已删除 7 条")
            problems.Add("清单编辑模式批量删除结果不对：剩余 " + remaining + " 条，撤销条「" + pet.UndoLabelForTest + "」");
        CheckLayout(pet.ListPanelForTest, "清单（撤销条）", problems);
        Capture(pet.ListPanelForTest, Path.Combine(screenshotDirectory, "list-undo.png"));
        checkedForms.Add("清单（撤销条）");
        pet.RunUndoForTest();
        Pump(100);
        remaining = service.Data.Todos.FindAll(delegate(TodoItem item) { return !item.IsDeleted; }).Count;
        if (remaining != 7 || pet.UndoVisibleForTest) problems.Add("清单批量删除撤销后应恢复 7 条，实际 " + remaining + " 条");
        pet.CloseForTest();

        foreach (string problem in problems) Console.Error.WriteLine(problem);
        bool pass = problems.Count == 0;
        Dictionary<string, object> result = Base("layout");
        result["actualScalePercent"] = scale;
        result["checked"] = checkedForms.ToArray();
        result["problemCount"] = problems.Count;
        result["problems"] = problems.ToArray();
        result["overallPass"] = pass;
        result["status"] = pass ? "PASS" : "FAIL";
        WriteJson(outputPath, result);
        return pass ? 0 : 1;
    }

    private static void CheckForm(Form form, string name, string screenshotDirectory, List<string> problems, List<string> checkedForms)
    {
        ShowOffscreen(form);
        CheckLayout(form, name, problems);
        Capture(form, Path.Combine(screenshotDirectory, "form-" + checkedForms.Count + ".png"));
        checkedForms.Add(name);
        form.Close();
        form.Dispose();
    }

    private static void ShowOffscreen(Form form)
    {
        form.StartPosition = FormStartPosition.CenterScreen;
        form.Show();
        Pump(150);
    }

    private static void Capture(Control control, string path)
    {
        using (Bitmap bitmap = new Bitmap(Math.Max(1, control.Width), Math.Max(1, control.Height)))
        {
            control.DrawToBitmap(bitmap, new Rectangle(Point.Empty, control.Size));
            bitmap.Save(path, ImageFormat.Png);
        }
    }

    private static void CheckLayout(Control parent, string path, List<string> problems)
    {
        bool scrolls = parent is ScrollableControl && ((ScrollableControl)parent).AutoScroll;
        List<Control> visible = new List<Control>();
        foreach (Control child in parent.Controls) if (child.Visible) visible.Add(child);
        foreach (Control child in visible)
        {
            string name = path + "/" + Describe(child);
            if (!scrolls && !(parent is TabControl) && !parent.ClientRectangle.Contains(child.Bounds))
                problems.Add(name + " 超出父容器 " + child.Bounds + " / " + parent.ClientRectangle);
            string textProblem = TextProblem(child);
            if (textProblem != null) problems.Add(name + " " + textProblem);
            if (!(child is TabControl) && child.Controls.Count > 0) CheckLayout(child, name, problems);
            if (child is TabControl)
            {
                TabPage page = ((TabControl)child).SelectedTab;
                if (page != null) CheckLayout(page, name + "/" + page.Text, problems);
            }
        }
        for (int i = 0; i < visible.Count; i++)
            for (int j = i + 1; j < visible.Count; j++)
                if (visible[i].Bounds.IntersectsWith(visible[j].Bounds))
                    problems.Add(path + " 重叠：" + Describe(visible[i]) + " " + visible[i].Bounds + " 与 " + Describe(visible[j]) + " " + visible[j].Bounds);
    }

    private static string TextProblem(Control control)
    {
        if (string.IsNullOrEmpty(control.Text) || control is TextBox || control is ComboBox || control is TabControl || control is Form) return null;
        Label label = control as Label;
        ButtonBase button = control as ButtonBase;
        if (label == null && button == null) return null;
        if ((label != null && label.AutoEllipsis) || (button != null && button.AutoEllipsis)) return null;
        if (control.AutoSize && !(control is Label && control.Text.Contains("\n"))) return null;
        int chrome = control is CheckBox || control is RadioButton ? 20 : (button != null ? 8 : 0);
        int available = Math.Max(1, control.Width - chrome);
        Size needed = TextRenderer.MeasureText(control.Text, control.Font, new Size(available, int.MaxValue), TextFormatFlags.WordBreak);
        Size singleLine = TextRenderer.MeasureText(control.Text, control.Font);
        bool wraps = label != null && !label.AutoSize;
        if (!wraps && singleLine.Width > available) return string.Format("文字放不下：需要宽 {0}，只有 {1}", singleLine.Width, available);
        if (needed.Height > control.Height + 1) return string.Format("文字放不下：需要高 {0}，只有 {1}", needed.Height, control.Height);
        return null;
    }

    private static string Describe(Control control)
    {
        string text = control.Text ?? string.Empty;
        text = text.Replace("\r", " ").Replace("\n", " ");
        if (text.Length > 12) text = text.Substring(0, 12) + "…";
        return control.GetType().Name + (text.Length > 0 ? "「" + text + "」" : string.Empty);
    }

    // 清单窗口的四个角在屏幕上应露出下层的测试窗口（系统圆角生效）。左上角截图留作证据。
    private static bool ListCornersRounded(PetForm pet, Color underlying, string screenshotPath, List<string> cornerColors)
    {
        Form list = pet.ListWindowForTest;
        Point[] corners = new Point[]
        {
            new Point(list.Left, list.Top), new Point(list.Right - 1, list.Top),
            new Point(list.Left, list.Bottom - 1), new Point(list.Right - 1, list.Bottom - 1)
        };
        bool pass = pet.ListRoundedForTest && list.Visible;
        using (Bitmap pixel = new Bitmap(1, 1))
        using (Graphics graphics = Graphics.FromImage(pixel))
        {
            foreach (Point corner in corners)
            {
                graphics.CopyFromScreen(corner, Point.Empty, new Size(1, 1));
                Color color = pixel.GetPixel(0, 0);
                cornerColors.Add(string.Format("#{0:X2}{1:X2}{2:X2}", color.R, color.G, color.B));
                // 系统阴影会把角上露出的下层颜色压暗几级，所以按容差比较。
                if (Math.Abs(color.R - underlying.R) > 24 || Math.Abs(color.G - underlying.G) > 24 || Math.Abs(color.B - underlying.B) > 24) pass = false;
            }
        }
        using (Bitmap shot = new Bitmap(48, 48))
        {
            using (Graphics graphics = Graphics.FromImage(shot)) graphics.CopyFromScreen(new Point(list.Left - 8, list.Top - 8), Point.Empty, shot.Size);
            shot.Save(screenshotPath, ImageFormat.Png);
        }
        return pass;
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
