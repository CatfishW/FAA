using UnityEngine;
#if UNITY_EDITOR
using System.Reflection;
using UnityEditor;
#endif

namespace MCPForUnity.Runtime.Helpers
{
    /// <summary>
    /// Provides a stable compatibility layer for Unity's instance-ID API,
    /// which became an error-level obsolete API in Unity 6.5.
    /// </summary>
    public static class UnityObjectIdCompat
    {
        /// <summary>
        /// Returns the legacy 32-bit object ID used by MCP's existing wire format.
        /// </summary>
        public static int GetInstanceIDCompat(this Object obj)
        {
            if (obj == null)
            {
                return 0;
            }

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
        /// Resolves a legacy 32-bit object ID without referencing APIs that are
        /// removed or error-level obsolete in newer Unity versions.
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
#endif
    }
}
