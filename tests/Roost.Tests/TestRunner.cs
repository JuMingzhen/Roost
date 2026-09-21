using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
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
