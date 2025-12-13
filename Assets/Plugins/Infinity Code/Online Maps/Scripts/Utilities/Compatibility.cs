/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System.Threading;
using UnityEngine;
using UnityEngine.Rendering;

namespace OnlineMaps
{
    /// <summary>
    /// Provides compatibility methods for different platforms and Unity versions.
    /// </summary>
    public static class Compatibility
    {
        /// <summary>
        /// Returns the first active loaded object of Type.
        /// </summary>
        /// <typeparam name="T">The type of object to find.</typeparam>
        /// <returns>The first active loaded object that matches the specified type. It returns null if no Object matches the type.</returns>
        public static T FindObjectOfType<T>() where T : Object
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindFirstObjectByType<T>();
#else
            return Object.FindObjectOfType<T>();
#endif
        }
        
        /// <summary>
        /// Gets the current render pipeline asset.
        /// </summary>
        /// <returns>The render pipeline asset.</returns>
        public static RenderPipelineAsset GetRenderPipelineAsset()
        {
    #if UNITY_6000_0_OR_NEWER
            return GraphicsSettings.defaultRenderPipeline;
    #else
            return GraphicsSettings.renderPipelineAsset;
    #endif
        }
        
        /// <summary>
        /// The current thread sleeps for the specified number of milliseconds
        /// </summary>
        /// <param name="millisecondsTimeout">number of milliseconds</param>
        public static void ThreadSleep(int millisecondsTimeout)
        {
#if !NETFX_CORE
            Thread.Sleep(millisecondsTimeout);
#else
            ThreadWINRT.Sleep(millisecondsTimeout);
#endif
        }
    }
}