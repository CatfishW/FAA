/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using UnityEngine;

namespace OnlineMaps
{
    /// <summary>
    /// Drawer for rendering Line elements into a buffer.
    /// </summary>
    public class LineBufferDrawer : BufferDrawerBase<Line>
    {
        protected override void DrawElement(IMapDrawer mapDrawer, Line element, Color32[] buffer, Vector2 bufferPosition, int bufferWidth, int bufferHeight, float zoom, bool invertY = false)
        {
            DrawLineToBuffer(element, buffer, bufferPosition, bufferWidth, bufferHeight, zoom, element.color, element.width, false, invertY);
        }
    }
}