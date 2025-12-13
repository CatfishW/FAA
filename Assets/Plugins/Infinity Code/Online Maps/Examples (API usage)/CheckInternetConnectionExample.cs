/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using OnlineMaps;
using UnityEngine;

namespace OnlineMapsExamples
{
    /// <summary>
    /// Example of how to test the connection to the Internet.
    /// </summary>
    [AddComponentMenu(Utils.ExampleMenuPath + "CheckInternetConnectionExample")]
    public class CheckInternetConnectionExample : MonoBehaviour
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
            
            // Begin to check your Internet connection.
            map.tileManager.CheckServerConnection(OnCheckConnectionComplete);
        }

        /// <summary>
        /// When the connection test is completed, this function will be called.
        /// </summary>
        /// <param name="status">Result of the test.</param>
        private void OnCheckConnectionComplete(bool status)
        {
            // If the test is successful, then allow the user to manipulate the map.
            map.GetComponent<MouseController>().allowUserControl = status;

            // Showing test result in console.
            Debug.Log(status ? "Has connection" : "No connection");
        }
    }
}