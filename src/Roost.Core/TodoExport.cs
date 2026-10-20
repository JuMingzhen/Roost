using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace Roost.Core
{
    // 导出文件的内容（PRD 第 11 节）。待办的字段与数据文件里的完全一致，将来可以按同一格式导入。
    public sealed class TodoExportFile
    {
        public string Format { get; set; }
        public int FormatVersion { get; set; }
        public string ExportedAtUtc { get; set; }
        public List<TodoItem> Todos { get; set; }

        public TodoExportFile()
        {
            Todos = new List<TodoItem>();
        }
    }

    // 导出全部未删除的待办（含已完成），附格式名、格式版本和导出时间。只导出待办，不导出设置。
    public static class TodoExport
    {
        public const string FormatName = "roost-todos";
        public const int CurrentVersion = 1;

        public static TodoExportFile Build(IEnumerable<TodoItem> todos, DateTime nowUtc)
        {
            TodoExportFile file = new TodoExportFile
            {
                Format = FormatName,
                FormatVersion = CurrentVersion,
                ExportedAtUtc = nowUtc.ToString("o")
            };
            foreach (TodoItem item in todos)
                if (!item.IsDeleted) file.Todos.Add(item.Clone());
            return file;
        }

        // 原子写入：失败时不会留下半个文件，也不碰数据文件和备份。
        public static void Write(string path, IEnumerable<TodoItem> todos, DateTime nowUtc)
        {
            byte[] bytes = new UTF8Encoding(false).GetBytes(Serializer().Serialize(Build(todos, nowUtc)));
            AtomicFile.WriteAllBytes(path, bytes, null);
        }

        // 读回导出文件。v1 没有导入界面，这里用来证明导出的文件可以重新导入。
        public static TodoExportFile Parse(string json)
        {
            TodoExportFile file;
            try { file = Serializer().Deserialize<TodoExportFile>(json); }
            catch (ArgumentException) { file = null; }
            catch (InvalidOperationException) { file = null; }
            if (file == null || file.Format != FormatName || file.FormatVersion != CurrentVersion || file.Todos == null)
                throw new InvalidDataException("不是可以识别的 Roost 导出文件。");
            return file;
        }

        private static JavaScriptSerializer Serializer()
        {
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            serializer.MaxJsonLength = 4 * 1024 * 1024;
            return serializer;
        }
    }
}
