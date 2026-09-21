using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

namespace Roost.App
{
    internal enum PetState
    {
        Idle,
        Hover,
        Drag,
        Celebrate,
        Reminder,
        Thinking
    }

    internal sealed class SpriteView : Control
    {
        private readonly Dictionary<PetState, Bitmap> images;
        private readonly Timer timer;
        private PetState state;
        private int frame;
        private bool paused;

        internal SpriteView(string assetRoot)
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            images = new Dictionary<PetState, Bitmap>();
            Bitmap idle = Load(assetRoot, "idle.png");
            images[PetState.Idle] = idle;
            images[PetState.Hover] = idle;
            images[PetState.Drag] = idle;
            images[PetState.Celebrate] = Load(assetRoot, "happy.png");
            images[PetState.Reminder] = Load(assetRoot, "notification.png");
            images[PetState.Thinking] = Load(assetRoot, "thinking.png");
            state = PetState.Idle;
            timer = new Timer();
            timer.Interval = 125;
            timer.Tick += delegate { frame++; Invalidate(); };
            timer.Start();
        }

        internal PetState State
        {
            get { return state; }
            set
            {
                if (state == value) return;
                state = value;
                frame = 0;
                Invalidate();
            }
        }

        internal bool Paused
        {
            get { return paused; }
            set
            {
                paused = value;
                if (paused) timer.Stop(); else timer.Start();
            }
        }

        internal int FrameCount { get { return frame; } }

        internal Bitmap RenderForTest(PetState value, Size target)
        {
            Bitmap source = images[value];
            Bitmap output = new Bitmap(target.Width, target.Height, PixelFormat.Format32bppArgb);
            using (Graphics graphics = Graphics.FromImage(output))
            {
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.CompositingQuality = CompositingQuality.HighSpeed;
                graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
                graphics.PixelOffsetMode = PixelOffsetMode.Half;
                graphics.SmoothingMode = SmoothingMode.None;
                graphics.Clear(Color.Transparent);
                graphics.DrawImage(source, new Rectangle(Point.Empty, target), 0, 0, source.Width, source.Height, GraphicsUnit.Pixel);
            }
            return output;
        }

        internal HashSet<int> SourceColorsForTest(PetState value)
        {
            HashSet<int> colors = new HashSet<int>();
            Bitmap source = images[value];
            for (int y = 0; y < source.Height; y++)
                for (int x = 0; x < source.Width; x++)
                    colors.Add(source.GetPixel(x, y).ToArgb());
            return colors;
        }

        internal Region CreateHitRegion(Rectangle bounds)
        {
            Bitmap source = images[state];
            Bitmap scaled = new Bitmap(Math.Max(1, bounds.Width), Math.Max(1, bounds.Height), PixelFormat.Format32bppArgb);
            using (Graphics graphics = Graphics.FromImage(scaled))
            {
                ConfigurePixelGraphics(graphics);
                graphics.Clear(Color.Transparent);
                graphics.DrawImage(source, new Rectangle(0, 0, scaled.Width, scaled.Height), 0, 0, source.Width, source.Height, GraphicsUnit.Pixel);
            }
            Region region = new Region();
            region.MakeEmpty();
            for (int y = 0; y < scaled.Height; y++)
            {
                int runStart = -1;
                for (int x = 0; x <= scaled.Width; x++)
                {
                    bool opaque = x < scaled.Width && scaled.GetPixel(x, y).A >= 16;
                    if (opaque && runStart < 0) runStart = x;
                    if (!opaque && runStart >= 0)
                    {
                        region.Union(new Rectangle(bounds.X + runStart, bounds.Y + y, x - runStart, 1));
                        runStart = -1;
                    }
                }
            }
            scaled.Dispose();
            return region;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            ConfigurePixelGraphics(e.Graphics);
            Bitmap image = images[state];
            Point offset = FrameOffset(state, frame);
            Rectangle destination = new Rectangle(offset.X, offset.Y, Width, Height);
            e.Graphics.DrawImage(image, destination, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                timer.Dispose();
                HashSet<Bitmap> disposed = new HashSet<Bitmap>();
                foreach (Bitmap image in images.Values)
                {
                    if (disposed.Add(image)) image.Dispose();
                }
            }
            base.Dispose(disposing);
        }

        internal static void ConfigurePixelGraphics(Graphics graphics)
        {
            graphics.CompositingMode = CompositingMode.SourceOver;
            graphics.CompositingQuality = CompositingQuality.HighSpeed;
            graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
            graphics.PixelOffsetMode = PixelOffsetMode.Half;
            graphics.SmoothingMode = SmoothingMode.None;
        }

        private static Bitmap Load(string root, string name)
        {
            string path = Path.Combine(root, name);
            if (!File.Exists(path)) throw new FileNotFoundException("缺少宠物素材。", path);
            using (Bitmap original = new Bitmap(path))
            {
                return new Bitmap(original);
            }
        }

        private static Point FrameOffset(PetState value, int index)
        {
            int step = index % 8;
            if (value == PetState.Celebrate) return new Point(0, step < 4 ? -2 : 0);
            if (value == PetState.Reminder) return new Point(step % 2 == 0 ? -1 : 1, 0);
            if (value == PetState.Thinking) return new Point(0, step < 4 ? -1 : 0);
            if (value == PetState.Hover) return new Point(step < 4 ? 0 : 1, 0);
            if (value == PetState.Idle) return new Point(0, step == 3 || step == 4 ? -1 : 0);
            return Point.Empty;
        }
    }
}
