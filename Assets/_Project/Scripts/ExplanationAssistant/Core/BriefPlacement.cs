using System;
using System.Collections.Generic;

namespace FAA.Explanations
{
    /// <summary>Pure layout policy. Never chooses a panel rectangle covering protected instruments.</summary>
    public static class BriefPlacement
    {
        public readonly struct Box
        {
            public readonly float X, Y, W, H;
            public Box(float x, float y, float w, float h) { X = x; Y = y; W = w; H = h; }
            public float Right => X + W;
            public float Top => Y + H;
            public bool Overlaps(Box b, float gap = 12) =>
                X < b.Right + gap && Right > b.X - gap && Y < b.Top + gap && Top > b.Y - gap;
        }

        public static bool TryPlace(float width, float height, float panelWidth, float panelHeight,
            bool mapOpen, IReadOnlyList<Box> obstacles, out Box placement)
        {
            // In map view use a side rail, not the bottom-center chart area.
            float[] xs = mapOpen ? new[] { width - panelWidth - 18, 18f, (width - panelWidth) / 2 } :
                new[] { (width - panelWidth) / 2, width - panelWidth - 18, 18f };
            foreach (float x in xs)
            {
                // Prefer the bottom; move vertically only if a real obstacle requires it.
                var ys = new List<float> { 18 };
                foreach (Box obstacle in obstacles) ys.Add(obstacle.Top + 12);
                ys.Sort();
                foreach (float y in ys)
                {
                    var candidate = new Box(x, y, panelWidth, panelHeight);
                    if (x < 12 || y < 12 || candidate.Right > width - 12 || candidate.Top > height - 12) continue;
                    bool blocked = false;
                    foreach (Box obstacle in obstacles) if (candidate.Overlaps(obstacle)) { blocked = true; break; }
                    if (!blocked) { placement = candidate; return true; }
                }
            }
            placement = default;
            return false; // Caller collapses to a launcher instead of covering the map.
        }
    }
}
