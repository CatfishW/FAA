/*         INFINITY CODE         */
/*   https://infinity-code.com   */

namespace OnlineMaps
{
    /// <summary>
    /// Abstract base class for drawing elements on the map.
    /// </summary>
    public abstract class DrawingElementDrawerBase
    {
        /// <summary>
        /// Validates whether the specified element should be drawn by the given mapDrawer.
        /// </summary>
        /// <param name="mapDrawer">The map drawer instance.</param>
        /// <param name="element">The drawing element to validate.</param>
        /// <returns>True if the element is valid for drawing; otherwise, false.</returns>
        protected virtual bool Validate(IMapDrawer mapDrawer, DrawingElement element)
        {
            if (element == null || !element.visible) return false;
            if (element.range != null && !element.range.Contains(mapDrawer.map.view.zoom)) return false;
            return true;
        }
    }
}