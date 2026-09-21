using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace Roost.Core
{
    public static class AtomicFile
    {
        public static void WriteAllBytes(string path, byte[] bytes, Action afterTempFlushed)
        {
            string fullPath = Path.GetFullPath(path);
            string directory = Path.GetDirectoryName(fullPath);
            Directory.CreateDirectory(directory);
            string temporary = fullPath + ".tmp." + Guid.NewGuid().ToString("N");
            try
            {
                using (FileStream stream = new FileStream(
                    temporary,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    4096,
                    FileOptions.WriteThrough))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }

                if (afterTempFlushed != null)
                {
                    afterTempFlushed();
                }

                if (File.Exists(fullPath))
                {
                    File.Replace(temporary, fullPath, null, true);
                }
                else
                {
                    File.Move(temporary, fullPath);
                }
            }
            finally
            {
                if (File.Exists(temporary))
                {
                    try { File.Delete(temporary); }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                }
            }
        }
    }

    public sealed class TodoRepository
    {
        private readonly string dataPath;
        private readonly string backupDirectory;
        private readonly JavaScriptSerializer serializer;

        public TodoRepository(string dataPath)
        {
            this.dataPath = Path.GetFullPath(dataPath);
            backupDirectory = Path.Combine(Path.GetDirectoryName(this.dataPath), "backups");
            serializer = new JavaScriptSerializer();
            serializer.MaxJsonLength = 4 * 1024 * 1024;
        }

        public string DataPath { get { return dataPath; } }
        public string BackupDirectory { get { return backupDirectory; } }

        public RoostData Load()
        {
            CleanupTemporaryFiles();
            if (!File.Exists(dataPath)) return new RoostData();

            RoostData loaded;
            if (TryLoad(dataPath, out loaded)) return Normalize(loaded);

            if (Directory.Exists(backupDirectory))
            {
                string[] backups = Directory.GetFiles(backupDirectory, "data-*.json");
                Array.Sort(backups, StringComparer.OrdinalIgnoreCase);
                Array.Reverse(backups);
                foreach (string backup in backups)
                {
                    if (TryLoad(backup, out loaded))
                    {
                        byte[] recovered = File.ReadAllBytes(backup);
                        AtomicFile.WriteAllBytes(dataPath, recovered, null);
                        return Normalize(loaded);
                    }
                }
            }

            throw new InvalidDataException("本地数据和备份都无法读取。");
        }

        public void Save(RoostData data)
        {
            RoostData normalized = Normalize(data);
            byte[] bytes = new UTF8Encoding(false).GetBytes(serializer.Serialize(normalized));
            AtomicFile.WriteAllBytes(dataPath, bytes, null);
            Directory.CreateDirectory(backupDirectory);
            string stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff");
            string backup = Path.Combine(backupDirectory, "data-" + stamp + "-" + Guid.NewGuid().ToString("N") + ".json");
            AtomicFile.WriteAllBytes(backup, bytes, null);
            PruneBackups(7);
        }

        public int BackupCount()
        {
            return Directory.Exists(backupDirectory)
                ? Directory.GetFiles(backupDirectory, "data-*.json").Length
                : 0;
        }

        private bool TryLoad(string path, out RoostData data)
        {
            try
            {
                string json = File.ReadAllText(path, Encoding.UTF8);
                data = serializer.Deserialize<RoostData>(json);
                return data != null && data.FormatVersion == 1;
            }
            catch (Exception exception)
            {
                if (exception is IOException || exception is UnauthorizedAccessException ||
                    exception is ArgumentException || exception is InvalidOperationException)
                {
                    data = null;
                    return false;
                }
                throw;
            }
        }

        private static RoostData Normalize(RoostData data)
        {
            if (data == null) data = new RoostData();
            data.FormatVersion = 1;
            if (data.Todos == null) data.Todos = new List<TodoItem>();
            if (data.Settings == null) data.Settings = new RoostSettings();
            if (data.Settings.SizeTier < 1 || data.Settings.SizeTier > 3) data.Settings.SizeTier = 2;
            data.Settings.Opacity = LayoutRules.ClampOpacity(data.Settings.Opacity);
            if (data.Settings.DayStartMinutes < 0 || data.Settings.DayStartMinutes >= 24 * 60)
                data.Settings.DayStartMinutes = 4 * 60;
            return data;
        }

        private void PruneBackups(int keep)
        {
            string[] backups = Directory.GetFiles(backupDirectory, "data-*.json");
            Array.Sort(backups, StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < backups.Length - keep; index++)
            {
                File.Delete(backups[index]);
            }
        }

        private void CleanupTemporaryFiles()
        {
            string directory = Path.GetDirectoryName(dataPath);
            if (!Directory.Exists(directory)) return;
            foreach (string temporary in Directory.GetFiles(directory, Path.GetFileName(dataPath) + ".tmp.*"))
            {
                try { File.Delete(temporary); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
    }
}
