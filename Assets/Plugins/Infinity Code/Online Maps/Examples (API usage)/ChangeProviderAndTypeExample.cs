/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using OnlineMaps;
using UnityEngine;

namespace OnlineMapsExamples
{
    /// <summary>
    /// Example how to get the available providers and to change the current provider.
    /// </summary>
    [AddComponentMenu(Utils.ExampleMenuPath + "ChangeProviderAndTypeExample")]
    public class ChangeProviderAndTypeExample : MonoBehaviour
    {
        /// <summary>
        /// Reference to the map. If not specified, the current instance will be used.
        /// </summary>
        public Map map;
        
        /// <summary>
        /// Logs providers id and map types
        /// </summary>
        private void LogTypeList()
        {
            // Gets all providers
            TileProvider[] providers = TileProvider.providers;
            foreach (TileProvider provider in providers)
            {
                Debug.Log(provider.id);
                foreach (MapType type in provider.types)
                {
                    Debug.Log(type);
                }
            }
        }

        private void Start()
        {
            // If map is not specified, use the current instance.
            if (!map && !(map = Map.instance))
            {
                Debug.LogError("Map not found");
                return;
            }
            
            // Show full provider list
            LogTypeList();

            // Select Google Satellite
            map.mapType = "google.satellite"; // providerID.typeID

            // Select the first type for ArcGIS.
            map.mapType = "arcgis"; // providerID
        }
    }
}