/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using OnlineMaps;
using UnityEngine;

namespace OnlineMapsExamples
{
    /// <summary>
    /// Example of how to create a marker on click.
    /// </summary>
    [AddComponentMenu(Utils.ExampleMenuPath + "CreateMarkerOnClick")]
    public class CreateMarkerOnClick:MonoBehaviour
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
            
            // Subscribe to the click event.
            map.control.OnClick += OnMapClick;
        }

        private void OnMapClick()
        {
            // Get the coordinates under the cursor.
            GeoPoint point = map.control.ScreenToLocation();

            // Create a label for the marker.
            string label = "Marker " + (map.marker2DManager.count + 1);

            // Create a new marker.
            map.marker2DManager.Create(point, label);
        }
    }
}
