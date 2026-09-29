using System;
using System.Drawing;
using System.Windows.Forms;

namespace Roost.App
{
    // 清单和气泡各自放在一个无边框的小窗口里，跟着宠物窗口移动。
    // 宠物窗口靠颜色键抠透明，画不出平滑圆角；独立窗口可以用 Windows 11 的系统圆角和阴影。
    internal sealed class FloatingWindow : Form
    {
        private readonly int cornerPreference;
        private int cornerResult = -1;

        internal FloatingWindow(Control content, int cornerPreference)
        {
            this.cornerPreference = cornerPreference;
            AutoScaleMode = AutoScaleMode.None;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            BackColor = content.BackColor;
            Font = content.Font;
            content.Dock = DockStyle.Fill;
            Controls.Add(content);
        }

        // 系统接受了圆角设置（Windows 11）时为 true。
        internal bool RoundedForTest { get { return cornerResult == 0; } }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                const int WS_EX_TOOLWINDOW = 0x00000080;
                CreateParams parameters = base.CreateParams;
                parameters.ExStyle |= WS_EX_TOOLWINDOW;
                return parameters;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            int preference = cornerPreference;
            // 系统细边框改成主题的暖灰色（COLORREF 为 0x00BBGGRR）。
            int border = Theme.Line.R | (Theme.Line.G << 8) | (Theme.Line.B << 16);
            try
            {
                cornerResult = NativeMethods.DwmSetWindowAttribute(Handle, NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE, ref preference, sizeof(int));
                NativeMethods.DwmSetWindowAttribute(Handle, NativeMethods.DWMWA_BORDER_COLOR, ref border, sizeof(int));
            }
            catch (DllNotFoundException) { cornerResult = -1; }
            catch (EntryPointNotFoundException) { cornerResult = -1; }
        }
    }
}
