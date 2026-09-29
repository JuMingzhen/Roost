using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace Roost.App
{
    // 界面的颜色、字号、圆角和图标集中在这里（效果图：「暖纸 + 陶土橘」）。
    // 尺寸一律按 100% 缩放写，由 DpiScale 统一放大；字号用磅值，随系统缩放自动放大。
    internal static class Theme
    {
        internal static readonly Color Paper = Color.FromArgb(0xFA, 0xF6, 0xEF);
        internal static readonly Color Card = Color.White;
        internal static readonly Color Sidebar = Color.FromArgb(0xF3, 0xEE, 0xE6);
        internal static readonly Color SegmentTrack = Color.FromArgb(0xEF, 0xE8, 0xDC);
        internal static readonly Color Line = Color.FromArgb(0xEA, 0xE2, 0xD5);
        internal static readonly Color InputBorder = Color.FromArgb(0xDD, 0xD2, 0xC2);
        internal static readonly Color Text = Color.FromArgb(0x2A, 0x24, 0x1D);
        internal static readonly Color TextMuted = Color.FromArgb(0x6F, 0x65, 0x58);
        internal static readonly Color TextFaint = Color.FromArgb(0x9A, 0x8F, 0x80);
        internal static readonly Color Ring = Color.FromArgb(0xB3, 0xA9, 0x99);
        internal static readonly Color Accent = Color.FromArgb(0xB5, 0x53, 0x2A);
        internal static readonly Color AccentPressed = Color.FromArgb(0x8F, 0x3F, 0x1E);
        internal static readonly Color AccentSoft = Color.FromArgb(0xFB, 0xEF, 0xE7);
        internal static readonly Color AccentTint = Color.FromArgb(0xF4, 0xE4, 0xD8);
        internal static readonly Color Star = Color.FromArgb(0xC9, 0x8A, 0x0B);
        internal static readonly Color Danger = Color.FromArgb(0xB3, 0x26, 0x1E);
        internal static readonly Color DangerText = Color.FromArgb(0x8E, 0x1D, 0x17);
        internal static readonly Color DangerSoft = Color.FromArgb(0xFD, 0xF1, 0xEF);
        internal static readonly Color DangerLine = Color.FromArgb(0xF0, 0xC9, 0xC4);
        internal static readonly Color Success = Color.FromArgb(0x1F, 0x5E, 0x3A);
        internal static readonly Color SuccessSoft = Color.FromArgb(0xE8, 0xF3, 0xEC);
        internal static readonly Color Warning = Color.FromArgb(0x7A, 0x4B, 0x00);
        internal static readonly Color WarningSoft = Color.FromArgb(0xFF, 0xF1, 0xD6);
        internal static readonly Color Toast = Color.FromArgb(0x2F, 0x2A, 0x24);
        internal static readonly Color ToastButton = Color.FromArgb(0x4A, 0x42, 0x39);
        internal static readonly Color ToastText = Color.FromArgb(0xFF, 0xD9, 0xC2);
        internal static readonly Color Disabled = Color.FromArgb(0xEA, 0xE2, 0xD5);
        internal static readonly Color DisabledText = Color.FromArgb(0x8A, 0x7F, 0x70);

        internal const string FontName = "Microsoft YaHei UI";

        // 字号（像素 → 磅）：11px 8.25 / 12px 9 / 13px 9.75 / 16px 12 / 18px 13.5 / 20px 15。
        internal static readonly Font Caption = new Font(FontName, 9F);
        internal static readonly Font Body = new Font(FontName, 9.75F);
        internal static readonly Font BodyBold = new Font(FontName, 9.75F, FontStyle.Bold);
        internal static readonly Font Small = new Font(FontName, 8.25F);
        internal static readonly Font CaptionBold = new Font(FontName, 9F, FontStyle.Bold);
        internal static readonly Font ListTitle = new Font(FontName, 12F, FontStyle.Bold);
        internal static readonly Font DialogTitle = new Font(FontName, 13.5F, FontStyle.Bold);
        internal static readonly Font PageTitle = new Font(FontName, 15F, FontStyle.Bold);

        internal const int RadiusCard = 8;
        internal const int RadiusControl = 6;

        // Windows 11 自带 Segoe Fluent Icons；Windows 10 用同码位的 Segoe MDL2 Assets。
        private static string iconFontName;

        internal static Font IconFont(float points)
        {
            if (iconFontName == null)
            {
                iconFontName = "Segoe MDL2 Assets";
                using (InstalledFontCollection installed = new InstalledFontCollection())
                    foreach (FontFamily family in installed.Families)
                        if (family.Name == "Segoe Fluent Icons") iconFontName = family.Name;
            }
            return new Font(iconFontName, points);
        }

        internal static class Icons
        {
            internal const string Settings = "";
            internal const string Add = "";
            internal const string Send = "";
            internal const string Check = "";
            internal const string Star = "";
            internal const string StarFilled = "";
            internal const string Delete = "";
            internal const string Close = "";
            internal const string ChevronDown = "";
            internal const string ChevronUp = "";
            internal const string Edit = "";
            internal const string Calendar = "";
            internal const string Clock = "";
            internal const string Warning = "";
            internal const string Completed = "";
            internal const string Info = "";
            internal const string Keyboard = "";
            internal const string Appearance = "";
            internal const string Bell = "";
            internal const string Sparkle = "";
            internal const string General = "";
            internal const string Open = "";
        }

        internal static GraphicsPath RoundedRectangle(RectangleF bounds, float radius)
        {
            GraphicsPath path = new GraphicsPath();
            float diameter = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
            if (diameter <= 0)
            {
                path.AddRectangle(bounds);
                return path;
            }
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        // 在控件上画一个抗锯齿的圆角块；控件的四个角先用父容器底色铺满。
        // radius 是物理像素。
        internal static void PaintRoundedBox(Graphics graphics, Control control, Color fill, Color border, float radius)
        {
            Color outside = control.Parent != null ? control.Parent.BackColor : Paper;
            graphics.Clear(outside);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            float stroke = Math.Max(1F, (float)Math.Round(DpiScale.Factor));
            RectangleF bounds = new RectangleF(stroke / 2F, stroke / 2F, control.Width - stroke, control.Height - stroke);
            using (GraphicsPath path = RoundedRectangle(bounds, radius))
            {
                using (SolidBrush brush = new SolidBrush(fill)) graphics.FillPath(brush, path);
                if (border != Color.Empty && border != fill)
                    using (Pen pen = new Pen(border, stroke)) graphics.DrawPath(pen, path);
            }
            graphics.SmoothingMode = SmoothingMode.None;
        }
    }
}
