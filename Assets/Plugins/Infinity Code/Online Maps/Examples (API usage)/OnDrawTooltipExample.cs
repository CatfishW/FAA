/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using OnlineMaps;
using UnityEngine;

namespace OnlineMapsExamples
{
    /// <summary>
    /// Example of how to intercept drawing tooltips and draw them yourself. 
    /// In this case, the square drawn around the marker.
    /// </summary>
    [AddComponentMenu(Utils.ExampleMenuPath + "OnDrawTooltipExample")]
    public class OnDrawTooltipExample : MonoBehaviour
    {
        /// <summary>
        /// Reference to the map. If not specified, the current instance will be used.
        /// </summary>
        public Map map;
        
        private void Start()
        {
            // If map is not specified, use the current instance.
            if (!map && !(map = Map.instance))
            {
                Debug.LogError("Map not found");
                return;
            }
            
            // Create new event OnDrawTooltip for all markers.
            Marker.OnMarkerDrawTooltip += OnMarkerDrawTooltip;

            // Create new event OnDrawTooltip for custom marker.
            map.marker2DManager.Create(new GeoPoint(), null, "New marker").OnDrawTooltip += OnDrawTooltip;
        }

        private void DrawBoxAroundMarker(Marker2D marker)
        {
            // Get screen rect of marker
            Rect rect = marker.realScreenRect;

            // Convert Input coordinates to GUI coordinates
            rect.y = Screen.height - rect.y;
            rect.height *= -1;

            // Draw box
            GUI.Box(rect, new GUIContent());
        }

        private void OnDrawTooltip(Marker marker)
        {
            Debug.Log(marker.label);
            // Here you draw the tooltip for the marker.

            DrawBoxAroundMarker(marker as Marker2D);
        }

        private void OnMarkerDrawTooltip(Marker marker)
        {
            Debug.Log(marker.label);
            // Here you draw the tooltip for the marker.

            DrawBoxAroundMarker(marker as Marker2D);
        }
    }
}