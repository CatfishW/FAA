/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using OnlineMaps;
using UnityEngine;

namespace OnlineMapsExamples
{
    /// <summary>
    /// Example how to find out that all tiles are loaded
    /// </summary>
    [AddComponentMenu(Utils.ExampleMenuPath + "CheckAllTilesLoadedExample")]
    public class CheckAllTilesLoadedExample : MonoBehaviour
    {
        private void Start()
        {
            // Subscribe to OnAllTilesLoaded
            Tile.OnAllTilesLoaded += OnAllTilesLoaded;
        }

        /// <summary>
        /// This method will be called when all tiles are loaded
        /// </summary>
        private void OnAllTilesLoaded()
        {
            Debug.Log("All tiles loaded");
        }
    }
}