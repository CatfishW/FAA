/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using OnlineMaps;
using UnityEngine;

namespace OnlineMapsExamples
{
    /// <summary>
    /// Example of how to intercept the request to the elevation data, and send elevation value to Online Maps.
    /// </summary>
    [AddComponentMenu(Utils.ExampleMenuPath + "InterceptElevationRequestExample")]
    public class InterceptElevationRequestExample : MonoBehaviour
    {
        /// <summary>
        /// Reference to the Bing Maps Elevation Manager. If not specified, the current instance will be used.
        /// </summary>
        public BingMapsElevationManager elevationManager;

        private void Start()
        {
            // If the elevation manager is not specified, get the current instance.
            if (!elevationManager && !(elevationManager = BingMapsElevationManager.instance))
            {
                Debug.LogError("BingMapsElevationManager not found");
                return;
            }

            // Intercept elevation request
            elevationManager.OnGetElevation += OnGetElevation;
        }

        private void OnGetElevation(GeoRect rect)
        {
            // Elevation map must be 32x32
            short[,] elevation = new short[32, 32];

            // Here you get the elevation from own sources.

            // Set elevation map
            elevationManager.SetElevationData(elevation);
        }
    }
}