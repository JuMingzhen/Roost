using System;
using System.IO;
using Microsoft.Win32;

namespace Roost.Core
{
    // 开机自启（PRD 第 4 节）：在当前用户注册表的 Run 键下写一项，指向 Roost.exe（决策 0004）。
    // 不需要管理员权限，任务管理器的「启动应用」里也能看到。测试用单独的注册表键，不碰真实的自启项。
    public sealed class AutoStart
    {
        public const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        public const string DefaultValueName = "Roost";

        private readonly string keyPath;
        private readonly string valueName;
        private readonly string exePath;

        public AutoStart(string keyPath, string valueName, string exePath)
        {
            this.keyPath = keyPath;
            this.valueName = valueName;
            this.exePath = Path.GetFullPath(exePath);
        }

        public static AutoStart ForCurrentUser(string exePath)
        {
            return new AutoStart(RunKeyPath, DefaultValueName, exePath);
        }

        public bool IsEnabled { get { return RegisteredPath() != null; } }

        public void Enable()
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(keyPath))
                key.SetValue(valueName, "\"" + exePath + "\"", RegistryValueKind.String);
        }

        public void Disable()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(keyPath, true))
                if (key != null) key.DeleteValue(valueName, false);
        }

        // 便携版的文件夹被挪走后，自启项指向的 Roost.exe 已经不存在：开着自启时改成现在的位置。
        // 指向的文件还在（例如另一份 Roost）就不动。返回是否改了。
        public bool Repair()
        {
            string registered = RegisteredPath();
            if (registered == null || File.Exists(registered) || string.Equals(registered, exePath, StringComparison.OrdinalIgnoreCase)) return false;
            Enable();
            return true;
        }

        // 自启项里登记的程序路径（去掉引号），没有自启项时返回 null。
        public string RegisteredPath()
        {
            string value;
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(keyPath))
                value = key == null ? null : key.GetValue(valueName) as string;
            if (string.IsNullOrWhiteSpace(value)) return null;
            value = value.Trim();
            if (!value.StartsWith("\"", StringComparison.Ordinal)) return value;
            int end = value.IndexOf('"', 1);
            return end > 1 ? value.Substring(1, end - 1) : null;
        }
    }
}
