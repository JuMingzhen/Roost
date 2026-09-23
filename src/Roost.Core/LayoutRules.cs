using System;
using System.Drawing;

namespace Roost.Core
{
    public sealed class PetLayout
    {
        public Rectangle WindowBounds { get; set; }
        public Rectangle PetBounds { get; set; }
        public Rectangle ListBounds { get; set; }
        public Rectangle BubbleBounds { get; set; }
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
            return Compute(requestedPetPosition, petSize, listSize, Size.Empty, workArea, listVisible, gap);
        }

        public static PetLayout Compute(
            Point requestedPetPosition,
            Size petSize,
            Size listSize,
            Size bubbleSize,
            Rectangle workArea,
            bool listVisible,
            int gap)
        {
            Point petPosition = RecoverPetPosition(requestedPetPosition, petSize, workArea);
            Rectangle petAbsolute = new Rectangle(petPosition, petSize);
            if (!listVisible)
            {
                Rectangle alone = PlaceBubble(petAbsolute, bubbleSize, workArea, false, false, false, gap);
                Rectangle aloneWindow = alone.IsEmpty ? petAbsolute : Rectangle.Union(petAbsolute, alone);
                return new PetLayout
                {
                    WindowBounds = aloneWindow,
                    PetBounds = Relative(petAbsolute, aloneWindow),
                    ListBounds = Rectangle.Empty,
                    BubbleBounds = alone.IsEmpty ? Rectangle.Empty : Relative(alone, aloneWindow)
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
            Rectangle bubble = PlaceBubble(petAbsolute, bubbleSize, workArea, true, left, above, gap);
            Rectangle window = Rectangle.Union(petAbsolute, listAbsolute);
            if (!bubble.IsEmpty) window = Rectangle.Union(window, bubble);
            return new PetLayout
            {
                WindowBounds = window,
                PetBounds = Relative(petAbsolute, window),
                ListBounds = Relative(listAbsolute, window),
                BubbleBounds = bubble.IsEmpty ? Rectangle.Empty : Relative(bubble, window),
                ListOnLeft = left,
                ListAlignedAbove = above
            };
        }

        private static Rectangle PlaceBubble(Rectangle pet, Size bubble, Rectangle workArea, bool listVisible, bool listOnLeft, bool listAbove, int gap)
        {
            if (bubble.Width <= 0 || bubble.Height <= 0) return Rectangle.Empty;
            // 气泡放在清单没有延伸的那一侧，并向远离清单的方向展开，避免压住清单。
            bool placeAbove = !(listVisible && listAbove);
            int y = placeAbove ? pet.Top - gap - bubble.Height : pet.Bottom + gap;
            if (placeAbove && y < workArea.Top) y = pet.Bottom + gap;
            else if (!placeAbove && y + bubble.Height > workArea.Bottom) y = pet.Top - gap - bubble.Height;
            y = Math.Max(workArea.Top, Math.Min(y, workArea.Bottom - bubble.Height));

            int x;
            if (!listVisible) x = pet.Left + (pet.Width - bubble.Width) / 2;
            else if (listOnLeft) x = pet.Left;
            else x = pet.Right - bubble.Width;
            x = Math.Max(workArea.Left, Math.Min(x, workArea.Right - bubble.Width));
            return new Rectangle(new Point(x, y), bubble);
        }

        private static Rectangle Relative(Rectangle absolute, Rectangle window)
        {
            return new Rectangle(absolute.X - window.X, absolute.Y - window.Y, absolute.Width, absolute.Height);
        }
    }
}

