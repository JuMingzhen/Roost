using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

internal static class NativeMethods
{
    internal const int WM_APP = 0x8000;
    internal const int WM_QUERY_COUNT = WM_APP + 1;
    internal const int WM_RESET_COUNT = WM_APP + 2;
    internal const int WM_CLOSE = 0x0010;
    internal const uint MONITOR_DEFAULTTONEAREST = 2;
    internal const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    internal const uint MOUSEEVENTF_LEFTUP = 0x0004;

    [StructLayout(LayoutKind.Sequential)]
    internal struct POINT
    {
        internal int X;
        internal int Y;

        internal POINT(int x, int y)
        {
            X = x;
            Y = y;
        }
    }

    [DllImport("user32.dll")]
    internal static extern bool SetProcessDPIAware();

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool SetProcessDpiAwarenessContext(IntPtr value);

    [DllImport("user32.dll")]
    internal static extern IntPtr MonitorFromPoint(POINT point, uint flags);

    [DllImport("shcore.dll")]
    internal static extern int GetDpiForMonitor(IntPtr monitor, int dpiType, out uint dpiX, out uint dpiY);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern IntPtr FindWindow(string className, string windowName);

    [DllImport("user32.dll")]
    internal static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    internal static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    internal static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extraInfo);

    [DllImport("gdi32.dll")]
    internal static extern IntPtr CreateRectRgn(int left, int top, int right, int bottom);

    [DllImport("user32.dll")]
    internal static extern int SetWindowRgn(IntPtr window, IntPtr region, bool redraw);
}

internal sealed class SinkForm : Form
{
    private int clickCount;

    internal SinkForm(string title, Rectangle bounds)
    {
        AutoScaleMode = AutoScaleMode.None;
        Text = title;
        StartPosition = FormStartPosition.Manual;
        Location = bounds.Location;
        ClientSize = bounds.Size;
        FormBorderStyle = FormBorderStyle.None;
        BackColor = Color.FromArgb(34, 74, 108);
        TopMost = false;
        ShowInTaskbar = false;
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            Interlocked.Increment(ref clickCount);
        }
        base.OnMouseDown(e);
    }

    protected override void WndProc(ref Message message)
    {
        if (message.Msg == NativeMethods.WM_QUERY_COUNT)
        {
            message.Result = new IntPtr(Volatile.Read(ref clickCount));
            return;
        }
        if (message.Msg == NativeMethods.WM_RESET_COUNT)
        {
            Interlocked.Exchange(ref clickCount, 0);
            message.Result = IntPtr.Zero;
            return;
        }
        base.WndProc(ref message);
    }
}

internal sealed class RegionOverlayForm : Form
{
    private Rectangle spriteRectangle;
    private int clickCount;
    private int frameIndex;

    internal RegionOverlayForm(Rectangle bounds, Rectangle sprite)
    {
        AutoScaleMode = AutoScaleMode.None;
        StartPosition = FormStartPosition.Manual;
        Location = bounds.Location;
        ClientSize = bounds.Size;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = Color.FromArgb(255, 244, 113, 116);
        spriteRectangle = sprite;
    }

    internal int ClickCount
    {
        get { return clickCount; }
    }

    internal int FrameIndex
    {
        get { return frameIndex; }
        set
        {
            frameIndex = value;
            Invalidate(spriteRectangle);
        }
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        IntPtr region = NativeMethods.CreateRectRgn(
            spriteRectangle.Left,
            spriteRectangle.Top,
            spriteRectangle.Right,
            spriteRectangle.Bottom);
        if (NativeMethods.SetWindowRgn(Handle, region, true) == 0)
        {
            throw new InvalidOperationException("SetWindowRgn failed.");
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            clickCount++;
        }
        base.OnMouseDown(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Color[] frames = new Color[]
        {
            Color.FromArgb(255, 244, 113, 116),
            Color.FromArgb(255, 247, 165, 87),
            Color.FromArgb(255, 111, 196, 169),
            Color.FromArgb(255, 91, 141, 239)
        };
        using (Brush brush = new SolidBrush(frames[Math.Abs(frameIndex) % frames.Length]))
        {
            e.Graphics.FillRectangle(brush, spriteRectangle);
        }
        base.OnPaint(e);
    }
}

internal sealed class PixelForm : Form
{
    private readonly Bitmap sprite;
    private readonly Rectangle spriteRectangle;
    private readonly Color canvasColor;

    internal PixelForm(Rectangle bounds, Bitmap source, Rectangle target, Color background)
    {
        AutoScaleMode = AutoScaleMode.None;
        StartPosition = FormStartPosition.Manual;
        Location = bounds.Location;
        ClientSize = bounds.Size;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        sprite = source;
        spriteRectangle = target;
        canvasColor = background;
        BackColor = background;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.PageUnit = GraphicsUnit.Pixel;
        e.Graphics.PageScale = 1.0f;
        e.Graphics.Clear(canvasColor);
        e.Graphics.CompositingMode = CompositingMode.SourceOver;
        e.Graphics.CompositingQuality = CompositingQuality.HighSpeed;
        e.Graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
        e.Graphics.PixelOffsetMode = PixelOffsetMode.Half;
        e.Graphics.SmoothingMode = SmoothingMode.None;
        e.Graphics.DrawImage(
            sprite,
            spriteRectangle,
            0,
            0,
            sprite.Width,
            sprite.Height,
            GraphicsUnit.Pixel);
        base.OnPaint(e);
    }
}

internal static class Program
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
            Console.Error.WriteLine("Usage: RoostSpike.exe <click-through|pixel-clarity|cpu> ...");
            return 64;
        }

        try
        {
            if (args[0] == "--click-sink")
            {
                RunClickSink(args);
                return 0;
            }
            if (args[0] == "click-through")
            {
                return RunClickThrough(args);
            }
            if (args[0] == "pixel-clarity")
            {
                return RunPixelClarity(args);
            }
            if (args[0] == "cpu")
            {
                return RunCpu(args);
            }
            Console.Error.WriteLine("Unknown mode: " + args[0]);
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
            if (!NativeMethods.SetProcessDpiAwarenessContext(new IntPtr(-4)))
            {
                NativeMethods.SetProcessDPIAware();
            }
        }
        catch (EntryPointNotFoundException)
        {
            NativeMethods.SetProcessDPIAware();
        }
    }

    private static List<Dictionary<string, object>> GetDisplays()
    {
        List<Dictionary<string, object>> displays = new List<Dictionary<string, object>>();
        foreach (Screen screen in Screen.AllScreens)
        {
            Rectangle bounds = screen.Bounds;
            NativeMethods.POINT center = new NativeMethods.POINT(
                bounds.Left + bounds.Width / 2,
                bounds.Top + bounds.Height / 2);
            IntPtr monitor = NativeMethods.MonitorFromPoint(center, NativeMethods.MONITOR_DEFAULTTONEAREST);
            uint dpiX = 96;
            uint dpiY = 96;
            try
            {
                int result = NativeMethods.GetDpiForMonitor(monitor, 0, out dpiX, out dpiY);
                if (result != 0)
                {
                    dpiX = 96;
                    dpiY = 96;
                }
            }
            catch (DllNotFoundException)
            {
                dpiX = 96;
                dpiY = 96;
            }

            Dictionary<string, object> item = new Dictionary<string, object>();
            item["device"] = screen.DeviceName;
            item["primary"] = screen.Primary;
            item["x"] = bounds.X;
            item["y"] = bounds.Y;
            item["width"] = bounds.Width;
            item["height"] = bounds.Height;
            item["dpiX"] = dpiX;
            item["dpiY"] = dpiY;
            item["scalePercent"] = (int)Math.Round(dpiX / 96.0 * 100.0);
            displays.Add(item);
        }
        return displays;
    }

    private static void RunClickSink(string[] args)
    {
        if (args.Length != 7)
        {
            throw new ArgumentException("click sink requires title, x, y, width, height");
        }
        Rectangle bounds = new Rectangle(
            int.Parse(args[2]),
            int.Parse(args[3]),
            int.Parse(args[4]),
            int.Parse(args[5]));
        Application.Run(new SinkForm(args[1], bounds));
    }

    private static int RunClickThrough(string[] args)
    {
        if (args.Length != 2)
        {
            throw new ArgumentException("click-through requires an output JSON path");
        }

        List<Dictionary<string, object>> displays = GetDisplays();
        List<Dictionary<string, object>> scenarios = new List<Dictionary<string, object>>();
        bool functionalPass = true;
        double[] requestedScales = new double[] { 1.0, 1.5 };

        foreach (Dictionary<string, object> display in displays)
        {
            Rectangle screenBounds = new Rectangle(
                (int)display["x"],
                (int)display["y"],
                (int)display["width"],
                (int)display["height"]);

            foreach (double requestedScale in requestedScales)
            {
                int width = Math.Min(520, screenBounds.Width - 40);
                int height = Math.Min(340, screenBounds.Height - 40);
                Rectangle testBounds = new Rectangle(
                    screenBounds.Left + (screenBounds.Width - width) / 2,
                    screenBounds.Top + (screenBounds.Height - height) / 2,
                    width,
                    height);
                int spriteSide = (int)Math.Round(64 * requestedScale);
                Rectangle sprite = new Rectangle(
                    (width - spriteSide) / 2,
                    (height - spriteSide) / 2,
                    spriteSide,
                    spriteSide);

                string title = "RoostClickSink-" + Guid.NewGuid().ToString("N");
                ProcessStartInfo start = new ProcessStartInfo();
                start.FileName = Application.ExecutablePath;
                start.Arguments = string.Format(
                    "--click-sink \"{0}\" {1} {2} {3} {4} 0",
                    title,
                    testBounds.X,
                    testBounds.Y,
                    testBounds.Width,
                    testBounds.Height);
                start.UseShellExecute = false;
                Process sinkProcess = Process.Start(start);
                IntPtr sinkHandle = WaitForWindow(title, 5000);
                if (sinkHandle == IntPtr.Zero)
                {
                    throw new InvalidOperationException("Click sink window did not appear.");
                }

                RegionOverlayForm overlay = new RegionOverlayForm(testBounds, sprite);
                overlay.Show();
                overlay.BringToFront();
                Pump(250);
                NativeMethods.SendMessage(sinkHandle, NativeMethods.WM_RESET_COUNT, IntPtr.Zero, IntPtr.Zero);

                List<Point> blankPoints = BuildBlankPoints(width, height, sprite);
                foreach (Point point in blankPoints)
                {
                    SendLeftClick(testBounds.Left + point.X, testBounds.Top + point.Y);
                    Pump(45);
                }

                List<Point> spritePoints = new List<Point>();
                spritePoints.Add(new Point(sprite.Left + sprite.Width / 3, sprite.Top + sprite.Height / 2));
                spritePoints.Add(new Point(sprite.Left + sprite.Width * 2 / 3, sprite.Top + sprite.Height / 2));
                foreach (Point point in spritePoints)
                {
                    SendLeftClick(testBounds.Left + point.X, testBounds.Top + point.Y);
                    Pump(45);
                }

                int sinkClicks = NativeMethods.SendMessage(
                    sinkHandle,
                    NativeMethods.WM_QUERY_COUNT,
                    IntPtr.Zero,
                    IntPtr.Zero).ToInt32();
                int overlayClicks = overlay.ClickCount;
                bool pass = sinkClicks == blankPoints.Count && overlayClicks == spritePoints.Count;
                functionalPass = functionalPass && pass;

                Dictionary<string, object> scenario = new Dictionary<string, object>();
                scenario["device"] = display["device"];
                scenario["actualScalePercent"] = display["scalePercent"];
                scenario["scenarioScalePercent"] = (int)Math.Round(requestedScale * 100);
                scenario["scenarioScaleMode"] = ((int)display["scalePercent"] == (int)Math.Round(requestedScale * 100)) ? "actual" : "simulated-geometry";
                scenario["blankClicksSent"] = blankPoints.Count;
                scenario["blankClicksReceivedByUnderlyingWindow"] = sinkClicks;
                scenario["spriteClicksSent"] = spritePoints.Count;
                scenario["spriteClicksReceivedByOverlay"] = overlayClicks;
                scenario["pass"] = pass;
                scenarios.Add(scenario);

                overlay.Close();
                overlay.Dispose();
                NativeMethods.SendMessage(sinkHandle, NativeMethods.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                if (!sinkProcess.WaitForExit(2000))
                {
                    sinkProcess.Kill();
                    sinkProcess.WaitForExit();
                }
                sinkProcess.Dispose();
            }
        }

        bool hasActual100 = HasDisplayScale(displays, 100);
        bool hasActual150 = HasDisplayScale(displays, 150);
        bool hasMultipleDisplays = displays.Count > 1;
        bool hardwareCoverage = hasActual100 && hasActual150;

        Dictionary<string, object> result = BaseResult("click-through", displays);
        result["method"] = "Cross-process sink window + SetWindowRgn hit region + injected physical mouse clicks";
        result["scenarios"] = scenarios;
        result["functionalPass"] = functionalPass;
        result["hardwareCoverage"] = new Dictionary<string, object>
        {
            { "actual100Percent", hasActual100 },
            { "actual150Percent", hasActual150 },
            { "multipleDisplaysAvailable", hasMultipleDisplays },
            { "allDetectedDisplaysTested", true },
            { "complete", hardwareCoverage }
        };
        result["overallPass"] = functionalPass && hardwareCoverage;
        result["status"] = !functionalPass ? "FAIL" : (hardwareCoverage ? "PASS" : "PASS_WITH_COVERAGE_GAP");
        result["note"] = !functionalPass
            ? "At least one injected-click scenario failed."
            : (hardwareCoverage
                ? "Required real display topology was present."
                : "Functional scenarios passed, but unavailable real DPI/monitor combinations were exercised only as simulated geometry.");
        WriteJson(args[1], result);
        return (functionalPass && hardwareCoverage) ? 0 : 2;
    }

    private static List<Point> BuildBlankPoints(int width, int height, Rectangle sprite)
    {
        List<Point> points = new List<Point>();
        points.Add(new Point(12, 12));
        points.Add(new Point(width / 2, 12));
        points.Add(new Point(width - 13, 12));
        points.Add(new Point(12, height / 2));
        points.Add(new Point(width - 13, height / 2));
        points.Add(new Point(12, height - 13));
        points.Add(new Point(width / 2, height - 13));
        points.Add(new Point(width - 13, height - 13));
        points.Add(new Point(sprite.Left - 12, sprite.Top + sprite.Height / 2));
        points.Add(new Point(sprite.Right + 12, sprite.Top + sprite.Height / 2));
        points.Add(new Point(sprite.Left + sprite.Width / 2, sprite.Top - 12));
        points.Add(new Point(sprite.Left + sprite.Width / 2, sprite.Bottom + 12));
        return points;
    }

    private static int RunPixelClarity(string[] args)
    {
        if (args.Length != 3)
        {
            throw new ArgumentException("pixel-clarity requires output JSON and screenshot directory");
        }
        Directory.CreateDirectory(args[2]);
        Bitmap sprite = CreateTestSprite();
        Color background = Color.FromArgb(255, 17, 34, 51);
        HashSet<int> allowedColors = GetAllowedColors(sprite, background);
        int[] dpiScales = new int[] { 100, 125, 150 };
        int[] sizeTiers = new int[] { 2, 3, 4 };
        List<Dictionary<string, object>> scenarios = new List<Dictionary<string, object>>();
        bool passAll = true;
        Screen screen = Screen.PrimaryScreen;

        foreach (int dpiScale in dpiScales)
        {
            double scale = dpiScale / 100.0;
            foreach (int sizeTier in sizeTiers)
            {
                int canvasWidth = (int)Math.Round(360 * scale);
                int canvasHeight = (int)Math.Round(240 * scale);
                int spriteWidth = sprite.Width * sizeTier;
                int spriteHeight = sprite.Height * sizeTier;
                int spriteX = (canvasWidth - spriteWidth) / 2;
                int spriteY = (canvasHeight - spriteHeight) / 2;
                Rectangle target = new Rectangle(spriteX, spriteY, spriteWidth, spriteHeight);
                Rectangle windowBounds = new Rectangle(
                    screen.Bounds.Left + 40,
                    screen.Bounds.Top + 40,
                    canvasWidth,
                    canvasHeight);

                PixelForm form = new PixelForm(windowBounds, sprite, target, background);
                form.Show();
                Pump(150);
                Size captureSize = form.ClientSize;
                Bitmap capture = new Bitmap(captureSize.Width, captureSize.Height, PixelFormat.Format32bppArgb);
                form.DrawToBitmap(capture, new Rectangle(Point.Empty, captureSize));

                Dictionary<int, int> unexpected = new Dictionary<int, int>();
                for (int y = target.Top; y < target.Bottom; y++)
                {
                    for (int x = target.Left; x < target.Right; x++)
                    {
                        int color = capture.GetPixel(x, y).ToArgb();
                        if (!allowedColors.Contains(color))
                        {
                            int current;
                            unexpected.TryGetValue(color, out current);
                            unexpected[color] = current + 1;
                        }
                    }
                }

                string fileName = string.Format("pixel-{0}pct-{1}x.png", dpiScale, sizeTier);
                string screenshotPath = Path.Combine(args[2], fileName);
                capture.Save(screenshotPath, ImageFormat.Png);
                bool pass = unexpected.Count == 0;
                passAll = passAll && pass;

                Dictionary<string, object> scenario = new Dictionary<string, object>();
                scenario["dpiScalePercent"] = dpiScale;
                scenario["dpiScaleMode"] = GetPrimaryScalePercent() == dpiScale ? "actual" : "simulated-layout";
                scenario["sizeTier"] = sizeTier;
                scenario["spritePhysicalWidth"] = spriteWidth;
                scenario["spritePhysicalHeight"] = spriteHeight;
                scenario["captureWidth"] = captureSize.Width;
                scenario["captureHeight"] = captureSize.Height;
                scenario["unexpectedColorCount"] = unexpected.Count;
                scenario["unexpectedPixelCount"] = SumCounts(unexpected);
                scenario["screenshot"] = Path.GetFullPath(screenshotPath);
                scenario["pass"] = pass;
                scenarios.Add(scenario);

                capture.Dispose();
                form.Close();
                form.Dispose();
            }
        }
        sprite.Dispose();

        List<Dictionary<string, object>> displays = GetDisplays();
        bool realCoverage = HasDisplayScale(displays, 100) && HasDisplayScale(displays, 125) && HasDisplayScale(displays, 150);
        Dictionary<string, object> result = BaseResult("pixel-clarity", displays);
        result["method"] = "Full-resolution HWND DrawToBitmap capture; sprite region exact palette membership check";
        result["sizes"] = sizeTiers;
        result["requestedDpiScales"] = dpiScales;
        result["scenarios"] = scenarios;
        result["functionalPass"] = passAll;
        result["hardwareCoverage"] = new Dictionary<string, object>
        {
            { "actual100Percent", HasDisplayScale(displays, 100) },
            { "actual125Percent", HasDisplayScale(displays, 125) },
            { "actual150Percent", HasDisplayScale(displays, 150) },
            { "complete", realCoverage }
        };
        result["overallPass"] = passAll && realCoverage;
        result["status"] = !passAll ? "FAIL" : (realCoverage ? "PASS" : "PASS_WITH_COVERAGE_GAP");
        result["note"] = realCoverage
            ? "All requested DPI scales were captured on real displays."
            : "Nearest-neighbor layout was exercised for every requested scale; unavailable scales are simulated layouts on the current real display.";
        WriteJson(args[1], result);
        return (passAll && realCoverage) ? 0 : 2;
    }

    private static int RunCpu(string[] args)
    {
        if (args.Length != 4)
        {
            throw new ArgumentException("cpu requires output JSON, visible seconds, hidden seconds");
        }

        int visibleSeconds = int.Parse(args[2]);
        int hiddenSeconds = int.Parse(args[3]);
        Screen screen = Screen.PrimaryScreen;
        Rectangle bounds = new Rectangle(screen.WorkingArea.Right - 140, screen.WorkingArea.Bottom - 140, 112, 112);
        Rectangle sprite = new Rectangle(8, 8, 96, 96);
        RegionOverlayForm form = new RegionOverlayForm(bounds, sprite);
        form.Show();
        Pump(200);

        int frameCount = 0;
        System.Windows.Forms.Timer animationTimer = new System.Windows.Forms.Timer();
        animationTimer.Interval = 125;
        animationTimer.Tick += delegate
        {
            frameCount++;
            form.FrameIndex = frameCount;
        };

        Process process = Process.GetCurrentProcess();
        List<long> workingSetSamples = new List<long>();
        TimeSpan visibleCpuStart = process.TotalProcessorTime;
        Stopwatch visibleClock = Stopwatch.StartNew();
        long nextSample = 0;
        animationTimer.Start();

        while (visibleClock.Elapsed.TotalSeconds < visibleSeconds)
        {
            Application.DoEvents();
            if (visibleClock.ElapsedMilliseconds >= nextSample)
            {
                process.Refresh();
                workingSetSamples.Add(process.WorkingSet64);
                nextSample += 1000;
            }
            Thread.Sleep(10);
        }

        animationTimer.Stop();
        TimeSpan visibleCpu = process.TotalProcessorTime - visibleCpuStart;
        double visibleElapsed = visibleClock.Elapsed.TotalSeconds;
        double visibleCpuPercent = visibleCpu.TotalSeconds / visibleElapsed / Environment.ProcessorCount * 100.0;
        int framesBeforeHide = frameCount;

        form.Hide();
        Pump(100);
        TimeSpan hiddenCpuStart = process.TotalProcessorTime;
        Stopwatch hiddenClock = Stopwatch.StartNew();
        while (hiddenClock.Elapsed.TotalSeconds < hiddenSeconds)
        {
            Application.DoEvents();
            Thread.Sleep(50);
        }
        TimeSpan hiddenCpu = process.TotalProcessorTime - hiddenCpuStart;
        double hiddenElapsed = hiddenClock.Elapsed.TotalSeconds;
        double hiddenCpuPercent = hiddenCpu.TotalSeconds / hiddenElapsed / Environment.ProcessorCount * 100.0;
        int framesWhileHidden = frameCount - framesBeforeHide;

        form.Close();
        form.Dispose();
        animationTimer.Dispose();

        double averageWorkingSetMb = Average(workingSetSamples) / 1024.0 / 1024.0;
        double maximumWorkingSetMb = Max(workingSetSamples) / 1024.0 / 1024.0;
        bool pass = visibleCpuPercent <= 0.5 && maximumWorkingSetMb <= 100.0 && framesWhileHidden == 0;

        Dictionary<string, object> result = BaseResult("cpu", GetDisplays());
        result["method"] = "8 FPS WinForms timer animation; Process.TotalProcessorTime normalized by logical processors; 1 s working-set samples";
        result["visibleDurationSeconds"] = visibleElapsed;
        result["hiddenDurationSeconds"] = hiddenElapsed;
        result["logicalProcessors"] = Environment.ProcessorCount;
        result["frameIntervalMilliseconds"] = 125;
        result["framesWhileVisible"] = framesBeforeHide;
        result["framesWhileHidden"] = framesWhileHidden;
        result["visibleAverageCpuPercent"] = Math.Round(visibleCpuPercent, 4);
        result["hiddenAverageCpuPercent"] = Math.Round(hiddenCpuPercent, 4);
        result["averageWorkingSetMb"] = Math.Round(averageWorkingSetMb, 2);
        result["maximumWorkingSetMb"] = Math.Round(maximumWorkingSetMb, 2);
        result["cpuTargetPercent"] = 0.5;
        result["memoryUpperTargetMb"] = 100.0;
        result["animationPausedWhileHidden"] = framesWhileHidden == 0;
        result["durationRequirementMet"] = visibleSeconds >= 600;
        result["overallPass"] = pass && visibleSeconds >= 600;
        result["functionalPass"] = pass;
        result["status"] = (pass && visibleSeconds >= 600) ? "PASS" : "FAIL";
        WriteJson(args[1], result);
        return (pass && visibleSeconds >= 600) ? 0 : 1;
    }

    private static Bitmap CreateTestSprite()
    {
        Bitmap bitmap = new Bitmap(16, 16, PixelFormat.Format32bppArgb);
        Color outline = Color.FromArgb(255, 31, 37, 50);
        Color body = Color.FromArgb(255, 244, 113, 116);
        Color highlight = Color.FromArgb(255, 255, 226, 138);
        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                bitmap.SetPixel(x, y, Color.Transparent);
            }
        }
        for (int y = 3; y <= 13; y++)
        {
            for (int x = 3; x <= 12; x++)
            {
                bitmap.SetPixel(x, y, (x == 3 || x == 12 || y == 3 || y == 13) ? outline : body);
            }
        }
        bitmap.SetPixel(5, 2, outline);
        bitmap.SetPixel(10, 2, outline);
        bitmap.SetPixel(6, 6, highlight);
        bitmap.SetPixel(9, 6, highlight);
        bitmap.SetPixel(6, 10, outline);
        bitmap.SetPixel(9, 10, outline);
        return bitmap;
    }

    private static HashSet<int> GetAllowedColors(Bitmap sprite, Color background)
    {
        HashSet<int> colors = new HashSet<int>();
        colors.Add(background.ToArgb());
        for (int y = 0; y < sprite.Height; y++)
        {
            for (int x = 0; x < sprite.Width; x++)
            {
                Color color = sprite.GetPixel(x, y);
                if (color.A == 255)
                {
                    colors.Add(color.ToArgb());
                }
            }
        }
        return colors;
    }

    private static int GetPrimaryScalePercent()
    {
        List<Dictionary<string, object>> displays = GetDisplays();
        foreach (Dictionary<string, object> display in displays)
        {
            if ((bool)display["primary"])
            {
                return (int)display["scalePercent"];
            }
        }
        return 100;
    }

    private static bool HasDisplayScale(List<Dictionary<string, object>> displays, int scale)
    {
        foreach (Dictionary<string, object> display in displays)
        {
            if ((int)display["scalePercent"] == scale)
            {
                return true;
            }
        }
        return false;
    }

    private static IntPtr WaitForWindow(string title, int timeoutMilliseconds)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        while (stopwatch.ElapsedMilliseconds < timeoutMilliseconds)
        {
            IntPtr handle = NativeMethods.FindWindow(null, title);
            if (handle != IntPtr.Zero)
            {
                return handle;
            }
            Thread.Sleep(25);
        }
        return IntPtr.Zero;
    }

    private static void SendLeftClick(int x, int y)
    {
        if (!NativeMethods.SetCursorPos(x, y))
        {
            throw new InvalidOperationException("SetCursorPos failed.");
        }
        NativeMethods.mouse_event(NativeMethods.MOUSEEVENTF_LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
        NativeMethods.mouse_event(NativeMethods.MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);
    }

    private static void Pump(int milliseconds)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        while (stopwatch.ElapsedMilliseconds < milliseconds)
        {
            Application.DoEvents();
            Thread.Sleep(5);
        }
    }

    private static Dictionary<string, object> BaseResult(string testName, List<Dictionary<string, object>> displays)
    {
        Dictionary<string, object> result = new Dictionary<string, object>();
        result["test"] = testName;
        result["timestampUtc"] = DateTime.UtcNow.ToString("o");
        result["osVersion"] = Environment.OSVersion.VersionString;
        result["is64BitOperatingSystem"] = Environment.Is64BitOperatingSystem;
        result["is64BitProcess"] = Environment.Is64BitProcess;
        result["runtimeVersion"] = Environment.Version.ToString();
        result["displays"] = displays;
        return result;
    }

    private static void WriteJson(string path, Dictionary<string, object> value)
    {
        string fullPath = Path.GetFullPath(path);
        string directory = Path.GetDirectoryName(fullPath);
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        File.WriteAllText(fullPath, Json.Serialize(value));
    }

    private static int SumCounts(Dictionary<int, int> counts)
    {
        int sum = 0;
        foreach (int count in counts.Values)
        {
            sum += count;
        }
        return sum;
    }

    private static double Average(List<long> values)
    {
        if (values.Count == 0)
        {
            return 0;
        }
        double sum = 0;
        foreach (long value in values)
        {
            sum += value;
        }
        return sum / values.Count;
    }

    private static long Max(List<long> values)
    {
        long maximum = 0;
        foreach (long value in values)
        {
            if (value > maximum)
            {
                maximum = value;
            }
        }
        return maximum;
    }
}
