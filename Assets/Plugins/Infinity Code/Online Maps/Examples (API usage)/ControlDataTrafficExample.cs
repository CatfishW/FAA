/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using OnlineMaps;
using UnityEngine;

namespace OnlineMapsExamples
{
    /// <summary>
    /// Example of how to calculate the size of downloaded tiles.
    /// </summary>
    [AddComponentMenu(Utils.ExampleMenuPath + "ControlDataTrafficExample")]
    public class ControlDataTrafficExample : MonoBehaviour
    {
        /// <summary>
        /// Counter of downloaded data.
        /// </summary>
        private int totalTileTraffic;

        private void OnGUI()
        {
            // Showing the counter of downloaded data.
            GUI.Label(new Rect(5, 5, Screen.width - 10, 30), "Total downloaded tiles: " + totalTileTraffic.ToString("N0") + " bytes");
        }

        private void Start()
        {
            // Subscribe to the event of success download tile.
            Tile.OnTileDownloaded += OnTileDownloaded;
        }

        /// <summary>
        /// This method is called when tile is success downloaded.
        /// </summary>
        /// <param name="tile">Reference to tile.</param>
        private void OnTileDownloaded(Tile tile)
        {
            // Increases counter of downloaded data.
            totalTileTraffic += tile.www.bytesDownloaded;
        }
    }
}