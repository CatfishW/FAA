/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using UnityEngine;

namespace OnlineMaps
{
    /// <summary>
    /// Contains shader property IDs for the Online Maps.
    /// </summary>
    public static class ShaderProperties
    {
        /// <summary>
        /// Shader property ID for the main texture.
        /// </summary>
        public static readonly int MainTex = Shader.PropertyToID("_MainTex");

        /// <summary>
        /// Shader property ID for the overlay back alpha.
        /// </summary>
        public static readonly int OverlayBackAlpha = Shader.PropertyToID("_OverlayBackAlpha");

        /// <summary>
        /// Shader property ID for the overlay back texture.
        /// </summary>
        public static readonly int OverlayBackTex = Shader.PropertyToID("_OverlayBackTex");

        /// <summary>
        /// Shader property ID for the overlay front alpha.
        /// </summary>
        public static readonly int OverlayFrontAlpha = Shader.PropertyToID("_OverlayFrontAlpha");

        /// <summary>
        /// Shader property ID for the overlay front texture.
        /// </summary>
        public static readonly int OverlayFrontTex = Shader.PropertyToID("_OverlayFrontTex");

        /// <summary>
        /// Shader property ID for the traffic texture.
        /// </summary>
        public static readonly int TrafficTex = Shader.PropertyToID("_TrafficTex");
    }
}