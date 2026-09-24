using System;
using System.Drawing;
using System.Windows.Forms;

namespace Roost.App
{
    // 进程是 PMv2 DPI 感知：字体按点数自动放大，但代码里写死的像素坐标不会。
    // 界面坐标一律按 96 DPI（100% 缩放）编写，建好后用这里按当前缩放整体放大一次。
    internal static class DpiScale
    {
        private static float factor;

        internal static float Factor
        {
            get
            {
                if (factor <= 0)
                {
                    using (Graphics screen = Graphics.FromHwnd(IntPtr.Zero)) factor = screen.DpiX / 96F;
                }
                return factor;
            }
        }

        internal static int Px(int logical)
        {
            return (int)Math.Round(logical * Factor);
        }

        internal static Size Px(Size logical)
        {
            return new Size(Px(logical.Width), Px(logical.Height));
        }

        internal static void Apply(Control control)
        {
            if (Math.Abs(Factor - 1F) > 0.001F) control.Scale(new SizeF(Factor, Factor));
        }
    }
}
