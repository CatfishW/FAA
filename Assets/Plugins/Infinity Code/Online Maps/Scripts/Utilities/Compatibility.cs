/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System.Threading;
#if UNITY_EDITOR
using System.Reflection;
using UnityEditor;
#endif
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
        /// Returns the legacy 32-bit object ID used by Online Maps data files.
        /// Unity 6.5 makes Object.GetInstanceID an error-level obsolete API.
        /// </summary>
        public static int GetInstanceIDCompat(Object obj)
        {
            if (obj == null) return 0;

#if UNITY_6000_5_OR_NEWER
            return (int)EntityId.ToULong(obj.GetEntityId());
#else
            return obj.GetInstanceID();
#endif
        }

#if UNITY_EDITOR
#if UNITY_6000_6_OR_NEWER
        private static MethodInfo _instanceIdToObject;
        private static bool _instanceIdToObjectInitialized;
#endif

        /// <summary>
        /// Resolves a legacy 32-bit object ID in the editor.
        /// </summary>
        public static Object InstanceIDToObjectCompat(int instanceId)
        {
#if UNITY_6000_6_OR_NEWER
            if (!_instanceIdToObjectInitialized)
            {
                _instanceIdToObject = typeof(EditorUtility).GetMethod(
                    "InstanceIDToObject",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[] { typeof(int) },
                    null);
                _instanceIdToObjectInitialized = true;
            }

            return _instanceIdToObject?.Invoke(null, new object[] { instanceId }) as Object;
#elif UNITY_6000_3_OR_NEWER
            return EditorUtility.EntityIdToObject(instanceId);
#else
            return EditorUtility.InstanceIDToObject(instanceId);
#endif
        }

        /// <summary>
        /// Gets an asset path without passing a legacy integer ID to AssetDatabase.
        /// </summary>
        public static string GetAssetPathCompat(Object obj)
        {
            if (obj == null) return string.Empty;

#if UNITY_6000_3_OR_NEWER
            return AssetDatabase.GetAssetPath(obj.GetEntityId());
#else
            return AssetDatabase.GetAssetPath(GetInstanceIDCompat(obj));
#endif
        }
#endif

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
