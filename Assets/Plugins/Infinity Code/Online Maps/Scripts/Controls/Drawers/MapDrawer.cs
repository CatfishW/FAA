/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using UnityEngine;

namespace OnlineMaps
{
    /// <summary>
    /// Abstract class for map drawer functionality.
    /// </summary>
    public abstract class MapDrawer: IMapDrawer
    {
        /// <summary>
        /// The reference to the map.
        /// </summary>
        public Map map { get; set; }
        
        /// <summary>
        /// Disposes the map drawer.
        /// </summary>
        public abstract void Dispose();
        
        /// <summary>
        /// Draws the map.
        /// </summary>
        public abstract void Draw();
        
        /// <summary>
        /// Draws the drawer elements on the map.
        /// </summary>
        public abstract void DrawElements();
        
        /// <summary>
        /// Initializes the map drawer.
        /// </summary>
        public abstract void Initialize();
        
        /// <summary>
        /// Performs a Raycast on the map.
        /// </summary>
        /// <param name="ray">Ray for Raycast.</param>
        /// <param name="hit">Hit information.</param>
        /// <param name="maxRaycastDistance">Maximum Raycast distance.</param>
        /// <returns>True if a hit occurred; otherwise, false.</returns>
        public abstract bool Raycast(Ray ray, out RaycastHit hit, int maxRaycastDistance);
    }
}