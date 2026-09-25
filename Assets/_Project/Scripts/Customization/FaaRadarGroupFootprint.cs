using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace FAA.Customization
{
    /// <summary>Reserve the entire radar UI, including faded/closed drawers, in radar-centred canvas units.
    /// Clipped map tiles/markers do not enlarge the envelope. No camera/world projection feeds the layout.</summary>
    public sealed class FaaRadarGroupFootprint
    {
        private readonly List<Graphic> graphics = new();
        private float nextScan;
        private Vector2 reservedRadius;
        private int knownChildren = -1;
        public Vector2 RadiusInRadarWidths => reservedRadius;

        public Vector2 Measure(Canvas canvas, RectTransform radar)
        {
            if (!FaaCanvasLocalGeometry.TryMatrix(radar, canvas.transform, out var rm)) return Vector2.one;
            Rect root = FaaCanvasLocalGeometry.TransformRect(radar.rect, rm);
            if (root.width < 1f) return Vector2.one;
            if (Time.unscaledTime >= nextScan || knownChildren != canvas.transform.childCount || graphics.Count == 0)
            {
                nextScan = Time.unscaledTime + .5f; knownChildren = canvas.transform.childCount;
                graphics.Clear(); canvas.GetComponentsInChildren(true, graphics);
            }
            Vector2 radius = new Vector2(root.width, root.height) * .5f;
            foreach (var graphic in graphics)
            {
                if (graphic == null || graphic.color.a <= .001f) continue;
                RectTransform rect = graphic.rectTransform;
                if (!FaaCanvasLocalGeometry.TryMatrix(rect, canvas.transform, out var matrix)) continue;
                Rect box = FaaCanvasLocalGeometry.TransformRect(rect.rect, matrix);
                // Mask clipping applies to renderable content even when its drawer is hidden.
                for (Transform ancestor = rect.parent; ancestor != null && ancestor != canvas.transform; ancestor = ancestor.parent)
                {
                    var mask = ancestor.GetComponent<RectMask2D>(); var stencil = ancestor.GetComponent<Mask>();
                    if ((mask == null || !mask.enabled) && (stencil == null || !stencil.enabled)) continue;
                    if (!(ancestor is RectTransform clip) || !FaaCanvasLocalGeometry.TryMatrix(clip, canvas.transform, out var cm)) continue;
                    Rect area = FaaCanvasLocalGeometry.TransformRect(clip.rect, cm);
                    float x0 = Mathf.Max(box.xMin, area.xMin), y0 = Mathf.Max(box.yMin, area.yMin);
                    float x1 = Mathf.Min(box.xMax, area.xMax), y1 = Mathf.Min(box.yMax, area.yMax);
                    box = x1 > x0 && y1 > y0 ? Rect.MinMaxRect(x0, y0, x1, y1) : Rect.zero;
                    if (box.width <= 0) break;
                }
                if (box.width <= 0 || box.height <= 0) continue;
                Vector2 extent = new Vector2(Mathf.Max(Mathf.Abs(box.xMin-root.center.x),Mathf.Abs(box.xMax-root.center.x)),
                    Mathf.Max(Mathf.Abs(box.yMin-root.center.y),Mathf.Abs(box.yMax-root.center.y)));
                radius = Vector2.Max(radius, extent);
            }
            // Include animation overshoot/margins. Retain the largest normalized envelope in this
            // session so closing a drawer never moves the group toward the flight view again.
            reservedRadius = Vector2.Max(reservedRadius, radius / root.width * 1.12f);
            return reservedRadius;
        }
    }
}
