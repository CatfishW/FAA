/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System.Linq;
using OnlineMaps;
using UnityEngine;

namespace OnlineMapsExamples
{
    /// <summary>
    /// Calculates the center and the best  zoom for all markers on the map, and show it.
    /// </summary>
    [AddComponentMenu(Utils.ExampleMenuPath + "GetCenterPointOfMarkersExample")]
    public class GetCenterPointOfMarkersExample : MonoBehaviour
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
            }
        }

        private void OnGUI()
        {
            if (GUI.Button(new Rect(5, 5, 100, 20), "Center"))
            {
                // Get the center point and zoom the best for all markers.
                (GeoPoint center, int zoom) = GeoMath.CenterPointAndZoom(map.marker2DManager.ToArray());

                // Change the position and zoom of the map.
                map.view.center = center;
                map.view.intZoom = zoom;
            }
        }
    }
}