using UnityEngine;
using CesiumForUnity;

namespace FAA.Physics
{
    /// <summary>
    /// Addresses the Unity physics warning: "Detected one or more triangles where the distance between any 2 vertices is greater than 500 units."
    /// This script optimizes the Cesium3DTileset to ensure triangles are small enough for Unity's physics engine.
    /// </summary>
    [ExecuteInEditMode]
    [RequireComponent(typeof(Cesium3DTileset))]
    public class CesiumPhysicsOptimizer : MonoBehaviour
    {
        [SerializeField]
        [Range(2.0f, 16.0f)]
        [Tooltip("Lower values force smaller triangles. 8.0 is a good balance for physics stability.")]
        private float _targetScreenSpaceError = 8.0f;

        [SerializeField]
        [Tooltip("If true, will automatically fix the tileset configuration.")]
        private bool _autoFix = true;

        void OnEnable()
        {
            if (_autoFix)
            {
                ApplyOptimizations();
            }
        }

        [ContextMenu("Apply Optimizations Now")]
        public void ApplyOptimizations()
        {
            Cesium3DTileset tileset = GetComponent<Cesium3DTileset>();
            if (tileset != null)
            {
                bool changed = false;

                // 1. Lower Maximum Screen Space Error to force smaller triangles
                if (tileset.maximumScreenSpaceError > _targetScreenSpaceError)
                {
                    tileset.maximumScreenSpaceError = _targetScreenSpaceError;
                    changed = true;
                }

                // 2. Ensure physics meshes are enabled if this script is present
                if (!tileset.createPhysicsMeshes)
                {
                    tileset.createPhysicsMeshes = true;
                    changed = true;
                }

                if (changed)
                {
                    Debug.Log($"<color=green>[CesiumPhysicsOptimizer]</color> Optimized {gameObject.name} for physics stability. Target SSE: {_targetScreenSpaceError}");
                }
            }
        }
    }
}
