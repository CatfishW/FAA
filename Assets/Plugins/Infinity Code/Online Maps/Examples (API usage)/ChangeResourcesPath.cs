/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using OnlineMaps;
using UnityEngine;

namespace OnlineMapsExamples
{
    /// <summary>
    /// Example of how to use a tiles from multiple resources folders.
    /// </summary>
    [AddComponentMenu(Utils.ExampleMenuPath + "Change Resources Path")]
    public class ChangeResourcesPath : MonoBehaviour
    {
        /// <summary>
        /// Reference to the map. If not specified, the current instance will be used.
        /// </summary>
        public Map map;
        
        /// <summary>
        /// Path to the tiles in the first resources folder.
        /// </summary>
        public string path1 = "OnlineMapsTiles/type1/{zoom}/{x}/{y}";
        
        /// <summary>
        /// Path to the tiles in the second resources folder.
        /// </summary>
        public string path2 = "OnlineMapsTiles/type2/{zoom}/{x}/{y}";

        private void OnGUI()
        {
            // If the button is pressed, change the path to the tiles.
            if (GUILayout.Button("Change"))
            {
                // Change the path to the tiles.
                map.resourcesPath = map.resourcesPath == path1 ? path2 : path1;
                
                // Reset the tile manager.
                map.tileManager.Reset();
                
                // Redraw map.
                map.Redraw();
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
            
            // Set the first path.
            map.resourcesPath = path1;
        }
    }
}