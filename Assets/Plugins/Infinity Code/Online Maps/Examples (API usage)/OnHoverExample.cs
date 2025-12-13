/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using OnlineMaps;
using UnityEngine;

namespace OnlineMapsExamples
{
    /// <summary>
    /// Example of how to make the event hover on the marker.
    /// </summary>
    [AddComponentMenu(Utils.ExampleMenuPath + "OnHoverExample")]
    public class OnHoverExample : MonoBehaviour
    {
        /// <summary>
        /// Reference to the map. If not specified, the current instance will be used.
        /// </summary>
        public Map map;
        
        private Marker hoverMarker;

        private void Start()
        {
            // If the map is not specified, get the current instance.
            if (!map && !(map = Map.instance))
            {
                Debug.LogError("Can't find Map");
                return;
            }

            // Create a new marker
            Marker2D marker = map.marker2DManager.Create(GeoPoint.zero, "Marker");

            // Subscribe to marker events
            marker.OnRollOver += OnRollOver;
            marker.OnRollOut += OnRollOut;

            // Reset map position
            map.view.center = GeoPoint.zero;
        }

        private void OnRollOut(Marker marker)
        {
            // Remove a reference to marker
            hoverMarker = null;
        }

        private void OnRollOver(Marker marker)
        {
            // Make a reference to marker
            hoverMarker = marker;
        }

        private void Update()
        {
            // If a marker is present log marker label.
            if (hoverMarker != null) Debug.Log(hoverMarker.label);
        }
    }
}