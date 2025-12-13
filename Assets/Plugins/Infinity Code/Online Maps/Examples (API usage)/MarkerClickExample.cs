/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using OnlineMaps;
using UnityEngine;

namespace OnlineMapsExamples
{
    /// <summary>
    /// Example of how to create a click event for dynamic markers and markers created by the inspector.
    /// </summary>
    [AddComponentMenu(Utils.ExampleMenuPath + "MarkerClickExample")]
    public class MarkerClickExample : MonoBehaviour
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

            // Add OnClick events to static markers
            foreach (Marker2D marker in map.marker2DManager)
            {
                marker.OnClick += OnMarkerClick;
            }

            // Add OnClick events to dynamic markers
            Marker2D dynamicMarker = map.marker2DManager.Create(GeoPoint.zero, null, "Dynamic marker");
            dynamicMarker.OnClick += OnMarkerClick;
        }

        private void OnMarkerClick(Marker marker)
        {
            // Show in console marker label.
            Debug.Log(marker.label);
        }
    }
}