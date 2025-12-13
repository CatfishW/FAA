/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using OnlineMaps;
using UnityEngine;

namespace OnlineMapsExamples
{
    /// <summary>
    /// Example of calculation of the distance and angle between the locations.
    /// </summary>
    [AddComponentMenu(Utils.ExampleMenuPath + "DistanceAndDirectionExample")]
    public class DistanceAndDirectionExample : MonoBehaviour
    {
        /// <summary>
        /// Reference to the map. If not specified, the current instance will be used.
        /// </summary>
        public Map map;
        
        /// <summary>
        /// The location of user.
        /// </summary>
        public GeoPoint userLocation;

        /// <summary>
        /// The location of the destination.
        /// </summary>
        public GeoPoint markerLocation;

        /// <summary>
        /// The direction of the compass.
        /// </summary>
        public float compassTrueHeading = 0;
        
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
            if (GUI.Button(new Rect(5, 5, 100, 30), "Calc"))
            {
                // Calculate the distance in km between locations.
                double distance = userLocation.Distance(markerLocation);
                Debug.Log("Distance: " + distance);

                // Calculate the angle between locations in Mercator projection.
                double angle = userLocation.AngleInMercator(map, markerLocation);
                Debug.Log("Angle: " + angle);

                // Calculate relative angle between locations.
                double relativeAngle = angle - compassTrueHeading;
                Debug.Log("Relative angle: " + relativeAngle);
            }
        }
    }
}