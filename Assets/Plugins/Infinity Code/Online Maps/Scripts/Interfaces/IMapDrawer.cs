/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using UnityEngine;

namespace OnlineMaps
{
    /// <summary>
    /// Interface for map drawer functionality.
    /// </summary>
    public interface IMapDrawer
    {
        /// <summary>
        /// The reference to the map.
        /// </summary>
        public Map map { get; set; }
        
        /// <summary>
        /// Disposes the map drawer.
        /// </summary>
        void Dispose();

        /// <summary>
        /// Draws the map.
        /// </summary>
        void Draw();

        /// <summary>
        /// Draws the map elements.
        /// </summary>
        void DrawElements();

        /// <summary>
        /// Initializes the map drawer.
        /// </summary>
        void Initialize();

        /// <summary>
        /// Performs a Raycast on the map.
        /// </summary>
        /// <param name="ray">Ray for Raycast.</param>
        /// <param name="hit">Hit information.</param>
        /// <param name="maxRaycastDistance">Maximum Raycast distance.</param>
        /// <returns>True if a hit occurred; otherwise, false.</returns>
        bool Raycast(Ray ray, out RaycastHit hit, int maxRaycastDistance);
    }
}