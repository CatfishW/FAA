/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using OnlineMaps;
using UnityEngine;

namespace OnlineMapsExamples
{
    /// <summary>
    /// Example of how to switch to a provider that requires authorization
    /// </summary>
    [AddComponentMenu(Utils.ExampleMenuPath + "SwitchToProviderWithKey")]
    public class SwitchToProviderWithKey : MonoBehaviour
    {
        /// <summary>
        /// Reference to the map. If not specified, the current instance will be used.
        /// </summary>
        public Map map;
        
        /// <summary>
        /// Draws UI elements using IMGUI
        /// </summary>
        private void OnGUI()
        {
            if (GUILayout.Button("Set DigitalGlobe"))
            {
                // If the map is not specified, get the current instance.
                if (map == null) map = Map.instance;
                
                // Switch to DigitalGlobe / Satellite
                string mapTypeID = "digitalglobe.satellite";

                map.mapType = mapTypeID;

                // Get map type
                MapType mapType = TileProvider.FindMapType(mapTypeID);
                
                // Set DigitalGlobe token
                mapType["accesstoken"] = "My DigitalGlobe Token";
            }
        }
    }
}