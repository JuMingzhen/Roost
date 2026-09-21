using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using Roost.Core;

namespace Roost.App
{
    internal static class Program
    {
        private const string MutexName = "Local\\Roost.Desktop.SingleInstance";
        private const string ShowMessageName = "Roost.Desktop.ShowExisting";

        [STAThread]
        private static int Main()
        {
            EnablePerMonitorDpi();
            bool created;
            using (Mutex mutex = new Mutex(true, MutexName, out created))
            {
                uint showMessage = NativeMethods.RegisterWindowMessage(ShowMessageName);
                if (!created)
                {
                    NativeMethods.PostMessage(NativeMethods.HWND_BROADCAST, showMessage, IntPtr.Zero, IntPtr.Zero);
                    return 0;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.ThreadException += delegate
                {
                    MessageBox.Show("Roost 遇到了错误，但没有记录任何待办内容。请重新启动应用。", "Roost", MessageBoxButtons.OK, MessageBoxIcon.Error);
                };

                string dataRoot = Environment.GetEnvironmentVariable("ROOST_DATA_DIR");
                if (string.IsNullOrEmpty(dataRoot))
                    dataRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Roost");
                string dataPath = Path.Combine(dataRoot, "data.json");
                string assetRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "cat");

                try
                {
                    TodoService todos = new TodoService(new TodoRepository(dataPath));
                    Application.Run(new PetForm(todos, showMessage, assetRoot));
                    return 0;
                }
                catch (Exception)
                {
                    MessageBox.Show("Roost 无法启动。请检查本地数据目录和应用文件是否完整。", "Roost", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return 1;
                }
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
    }
}

