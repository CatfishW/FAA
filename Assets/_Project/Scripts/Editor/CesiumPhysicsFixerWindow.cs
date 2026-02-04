using UnityEngine;
using UnityEditor;
using CesiumForUnity;

namespace FAA.Editor
{
    /// <summary>
    /// Editor tool to quickly address physics warnings in Cesium tilesets.
    /// </summary>
    public class CesiumPhysicsFixerWindow : EditorWindow
    {
        [MenuItem("FAA/Physics/Fix Cesium Physics Warnings")]
        public static void FixWarnings()
        {
            Cesium3DTileset[] tilesets = Object.FindObjectsOfType<Cesium3DTileset>();
            if (tilesets.Length == 0)
            {
                Debug.LogWarning("[CesiumPhysicsFixer] No Cesium3DTileset found in the scene.");
                return;
            }

            int count = 0;
            foreach (var tileset in tilesets)
            {
                Undo.RecordObject(tileset, "Fix Cesium Physics");
                
                // Lowering SSE forces more subdivisions, preventing the "large triangle" warning (> 500 units)
                tileset.maximumScreenSpaceError = 8.0f;
                tileset.createPhysicsMeshes = true;
                
                EditorUtility.SetDirty(tileset);
                count++;
            }

            Debug.Log($"[CesiumPhysicsFixer] Successfully optimized {count} Cesium tileset(s) for physics stability.");
        }
    }
}
