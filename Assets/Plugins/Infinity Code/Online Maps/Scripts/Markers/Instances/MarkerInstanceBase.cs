/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using UnityEngine;

namespace OnlineMaps
{
    /// <summary>
    /// Base class for instance of marker. 
    /// This class is used when for each marker create a separate GameObject.
    /// </summary>
    public abstract class MarkerInstanceBase:MonoBehaviour 
    {
        /// <summary>
        /// Reference to marker.
        /// </summary>
        public abstract Marker marker { get; set; }
    }
}