/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using OnlineMaps;
using UnityEngine;

namespace OnlineMapsExamples
{
    /// <summary>
    /// Example of how to dynamically create a 2D marker in the GPS locations of user.
    /// </summary>
    [AddComponentMenu(Utils.ExampleMenuPath + "Marker_GPS_Example")]
    public class Marker_GPS_Example : MonoBehaviour
    {
        /// <summary>
        /// Reference to the map. If not specified, the current instance will be used.
        /// </summary>
        public Map map;
        
        // Marker, which should display the location.
        private Marker2D playerMarker;

        private void Start()
        {
            // If map is not specified, use the current instance.
            if (!map && !(map = Map.instance))
            {
                Debug.LogError("Map not found");
                return;
            }
            
            // Create a new marker.
            playerMarker = map.marker2DManager.Create(0, 0, null, "Player");

            // Get instance of UserLocation.
            UserLocation userLocation = UserLocation.instance;

            if (!userLocation)
            {
                Debug.LogError(
                    "User Location component not found.\nAdd User Location Component (Component / Infinity Code / Online Maps / Plugins / User Location).");
                return;
            }

            // Subscribe to the change location event.
            userLocation.OnLocationChanged += OnLocationChanged;
        }

        // When the location has changed
        private void OnLocationChanged(GeoPoint location)
        {
            // Change the position of the marker.
            playerMarker.location = location;

            // Redraw the map.
            map.Redraw();
        }
    }
}
