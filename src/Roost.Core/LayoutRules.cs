using System;
using System.Drawing;

namespace Roost.Core
{
    public sealed class PetLayout
    {
        public Rectangle WindowBounds { get; set; }
        public Rectangle PetBounds { get; set; }
        public Rectangle ListBounds { get; set; }
        public bool ListOnLeft { get; set; }
        public bool ListAlignedAbove { get; set; }
    }

    public static class LayoutRules
    {
        public const double MinimumOpacity = 0.35;

        public static double ClampOpacity(double value)
        {
            if (double.IsNaN(value) || value < MinimumOpacity) return MinimumOpacity;
            return value > 1.0 ? 1.0 : value;
        }

        public static bool IsDrag(Point start, Point current, int threshold)
        {
            long dx = current.X - start.X;
            long dy = current.Y - start.Y;
            return dx * dx + dy * dy > threshold * threshold;
        }

        public static Point RecoverPetPosition(Point position, Size petSize, Rectangle workArea)
        {
            int x = Math.Max(workArea.Left, Math.Min(position.X, workArea.Right - petSize.Width));
            int y = Math.Max(workArea.Top, Math.Min(position.Y, workArea.Bottom - petSize.Height));
            return new Point(x, y);
        }

        public static Point SnapPetPosition(Point position, Size petSize, Rectangle workArea, int threshold)
        {
            Point recovered = RecoverPetPosition(position, petSize, workArea);
            if (Math.Abs(recovered.X - workArea.Left) <= threshold) recovered.X = workArea.Left;
            if (Math.Abs((recovered.X + petSize.Width) - workArea.Right) <= threshold)
                recovered.X = workArea.Right - petSize.Width;
            if (Math.Abs(recovered.Y - workArea.Top) <= threshold) recovered.Y = workArea.Top;
            if (Math.Abs((recovered.Y + petSize.Height) - workArea.Bottom) <= threshold)
                recovered.Y = workArea.Bottom - petSize.Height;
            return recovered;
        }

        public static PetLayout Compute(
            Point requestedPetPosition,
            Size petSize,
            Size listSize,
            Rectangle workArea,
            bool listVisible,
            int gap)
        {
            Point petPosition = RecoverPetPosition(requestedPetPosition, petSize, workArea);
            Rectangle petAbsolute = new Rectangle(petPosition, petSize);
            if (!listVisible)
            {
                return new PetLayout
                {
                    WindowBounds = petAbsolute,
                    PetBounds = new Rectangle(Point.Empty, petSize),
                    ListBounds = Rectangle.Empty
                };
            }

            bool left = petAbsolute.Right + gap + listSize.Width > workArea.Right;
            int listX = left ? petAbsolute.Left - gap - listSize.Width : petAbsolute.Right + gap;
            if (listX < workArea.Left)
            {
                left = false;
                listX = Math.Min(petAbsolute.Right + gap, workArea.Right - listSize.Width);
            }

            bool above = petAbsolute.Top + listSize.Height > workArea.Bottom;
            int listY = above ? petAbsolute.Bottom - listSize.Height : petAbsolute.Top;
            listY = Math.Max(workArea.Top, Math.Min(listY, workArea.Bottom - listSize.Height));
            Rectangle listAbsolute = new Rectangle(new Point(listX, listY), listSize);
            Rectangle window = Rectangle.Union(petAbsolute, listAbsolute);
            return new PetLayout
            {
                WindowBounds = window,
                PetBounds = new Rectangle(petAbsolute.X - window.X, petAbsolute.Y - window.Y, petSize.Width, petSize.Height),
                ListBounds = new Rectangle(listAbsolute.X - window.X, listAbsolute.Y - window.Y, listSize.Width, listSize.Height),
                ListOnLeft = left,
                ListAlignedAbove = above
            };
        }
    }
}

