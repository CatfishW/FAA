/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using OnlineMaps;
using UnityEngine;

namespace OnlineMapsExamples
{
    /// <summary>
    /// Example of how to dynamically create a 3D marker in the GPS locations of user.
    /// </summary>
    [AddComponentMenu(Utils.ExampleMenuPath + "Marker3D_GPS_Example")]
    public class Marker3D_GPS_Example : MonoBehaviour
    {
        /// <summary>
        /// Reference to the 3D control (Texture or Tileset). If not specified, the current instance will be used.
        /// </summary>
        public ControlBase3D control;
        
        /// <summary>
        /// Prefab of 3D marker
        /// </summary>
        public GameObject prefab;

        private Marker3D locationMarker;

        private void Start()
        {
            // If the control is not specified, get the current instance.
            if (!control && !(control = ControlBase3D.instance))
            {
                Debug.LogError("Control not found");
                return;
            }

            //Create a marker to show the current GPS coordinates.
            locationMarker = control.marker3DManager.Create(GeoPoint.zero, prefab);

            //Hide handle until the coordinates are not received.
            locationMarker.enabled = false;

            // Gets User Location Component.
            UserLocation userLocation = UserLocation.instance;

            if (!userLocation)
            {
                Debug.LogError(
                    "User Location component not found.\nAdd Location Service component (Component / Infinity Code / Online Maps / Plugins / User Location).");
                return;
            }

            //Subscribe to the GPS coordinates change
            userLocation.OnLocationChanged += OnLocationChanged;
            userLocation.OnCompassChanged += OnCompassChanged;
        }

        private void OnCompassChanged(float heading)
        {
            //Set marker rotation
            Transform markerTransform = locationMarker.transform;
            if (markerTransform) markerTransform.rotation = Quaternion.Euler(0, heading, 0);
        }

        //This event occurs at each change of GPS coordinates
        private void OnLocationChanged(GeoPoint location)
        {
            //Change the position of the marker to GPS coordinates
            locationMarker.location = location;

            //If the marker is hidden, show it
            if (!locationMarker.enabled) locationMarker.enabled = true;
        }
    }
}