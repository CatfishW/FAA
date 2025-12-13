/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using UnityEngine;

namespace OnlineMaps
{
    /// <summary>
    /// Implements the display of 2D markers
    /// </summary>
    public abstract class Marker2DDrawer : MarkerDrawerBase
    {
        /// <summary>
        /// Gets 2D marker in screen position
        /// </summary>
        /// <param name="screenPosition">Screen position</param>
        /// <returns>2D marker</returns>
        public virtual Marker2D GetMarkerFromScreen(Vector2 screenPosition)
        {
            GeoPoint location = map.control.ScreenToLocation(screenPosition);
            if (location == GeoPoint.zero) return null;

            Marker2D marker = null;
            double lng = double.MinValue, lat = double.MaxValue;
            int zoom = map.view.intZoom;

            foreach (Marker2D m in map.marker2DManager)
            {
                if (!m.enabled || !m.range.Contains(zoom)) continue;
                if (!m.HitTest(location, zoom)) continue;
                
                GeoPoint p = m.location;
                if (p.y < lat || (Math.Abs(p.y - lat) < double.Epsilon && p.x > lng))
                {
                    marker = m;
                    lat = p.y;
                    lng = p.x;
                }
            }

            return marker;
        }
    }
}