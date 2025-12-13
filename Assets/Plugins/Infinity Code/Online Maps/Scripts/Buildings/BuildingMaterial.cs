/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using UnityEngine;

namespace OnlineMaps
{
    /// <summary>
    /// Building material.
    /// </summary>
    [Serializable]
    public class BuildingMaterial
    {
        /// <summary>
        /// Wall material.
        /// </summary>
        public Material wall;

        /// <summary>
        /// Roof material.
        /// </summary>
        public Material roof;

        /// <summary>
        /// Wall main texture scale factor.
        /// </summary>
        public Vector2 scale = Vector2.one;
    }
}