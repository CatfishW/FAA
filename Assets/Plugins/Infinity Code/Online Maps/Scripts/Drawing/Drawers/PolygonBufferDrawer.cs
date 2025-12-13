/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using UnityEngine;

namespace OnlineMaps
{
        /// <summary>
        /// Drawer for rendering polygons into a buffer.
        /// </summary>
        public class PolygonBufferDrawer : BufferDrawerBase<Polygon>
    {
        protected override void DrawElement(IMapDrawer mapDrawer, Polygon element, Color32[] buffer, Vector2 bufferPosition, int bufferWidth, int bufferHeight, float zoom, bool invertY = false)
        {
            FillPoly(element, buffer, bufferPosition, bufferWidth, bufferHeight, zoom, element.backgroundColor, invertY);
            DrawLineToBuffer(element, buffer, bufferPosition, bufferWidth, bufferHeight, zoom, element.borderColor, element.borderWidth, true, invertY);
        }
    }
}