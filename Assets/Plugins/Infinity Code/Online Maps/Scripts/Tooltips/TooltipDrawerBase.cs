/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using UnityEngine;

namespace OnlineMaps
{
    /// <summary>
    /// The base class for implementation of tooltip drawers
    /// </summary>
    public abstract class TooltipDrawerBase: GenericBase<TooltipDrawerBase>
    {
        /// <summary>
        /// Tooltip
        /// </summary>
        public static string tooltip;

        /// <summary>
        /// The drawing element for which the tooltip is drawn
        /// </summary>
        public static DrawingElement tooltipDrawingElement;

        /// <summary>
        /// The marker for which the tooltip is drawn
        /// </summary>
        public static Marker tooltipMarker;

        private static Marker rolledMarker;

        protected Map map;
        protected ControlBase control;

        /// <summary>
        /// Checks if the marker in the specified screen coordinates, and shows him a tooltip.
        /// </summary>
        /// <param name="screenPosition">Screen coordinates</param>
        public void ShowMarkersTooltip(Vector2 screenPosition)
        {
            if (map.showMarkerTooltip != ShowMarkerTooltip.onPress)
            {
                tooltip = string.Empty;
                tooltipDrawingElement = null;
                tooltipMarker = null;
            }

            IInteractiveElement el = control.GetInteractiveElement(screenPosition);
            Marker marker = el as Marker;

            if (map.showMarkerTooltip == ShowMarkerTooltip.onHover)
            {
                if (marker != null)
                {
                    tooltip = marker.label;
                    tooltipMarker = marker;
                }
                else
                {
                    DrawingElement drawingElement = map.GetDrawingElement(screenPosition);
                    if (drawingElement != null)
                    {
                        tooltip = drawingElement.tooltip;
                        tooltipDrawingElement = drawingElement;
                    }
                }
            }

            if (rolledMarker != marker)
            {
                if (rolledMarker != null && rolledMarker.OnRollOut != null) rolledMarker.OnRollOut(rolledMarker);
                rolledMarker = marker;
                if (rolledMarker != null && rolledMarker.OnRollOver != null) rolledMarker.OnRollOver(rolledMarker);
            }
        }
    }
}