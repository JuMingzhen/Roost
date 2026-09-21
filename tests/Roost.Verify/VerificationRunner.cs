using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
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
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Usage: Roost.Verify.exe <system|pixel|cpu> ...");
            return 64;
        }
        try
        {
            if (args[0] == "system") return RunSystem(args[1]);
            if (args[0] == "pixel") return RunPixel(args[1], args[2]);
            if (args[0] == "cpu") return RunCpu(args[1], int.Parse(args[2]), int.Parse(args[3]));
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
