using System.Drawing;

namespace Roost.Core
{
    public static class FullscreenRules
    {
        public static bool IsFullscreen(Rectangle foreground, Rectangle monitor, bool visible, bool desktopWindow)
        {
            if (!visible || desktopWindow || foreground.Width <= 0 || foreground.Height <= 0) return false;
            const int tolerance = 2;
            return foreground.Left <= monitor.Left + tolerance &&
                   foreground.Top <= monitor.Top + tolerance &&
                   foreground.Right >= monitor.Right - tolerance &&
                   foreground.Bottom >= monitor.Bottom - tolerance;
        }
    }
}

