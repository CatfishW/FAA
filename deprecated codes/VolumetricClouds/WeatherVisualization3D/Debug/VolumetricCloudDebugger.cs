using UnityEngine;

namespace WeatherVisualization3D
{
    /// <summary>
    /// Debug helper for volumetric cloud system
    /// </summary>
    public class VolumetricCloudDebugger : MonoBehaviour
    {
        [Header("Debug Settings")]
        [SerializeField] private bool showDebugInfo = true;
        [SerializeField] private bool forceTextureGeneration = true;
        [SerializeField] private bool logMaterialStatus = true;
        
        private VolumetricCloudVolume cloudVolume;
        private MeshRenderer meshRenderer;
        private Material material;
        
        void Start()
        {
            cloudVolume = GetComponent<VolumetricCloudVolume>();
            meshRenderer = GetComponent<MeshRenderer>();
            
            if (meshRenderer != null)
            {
                material = meshRenderer.material;
            }
            
            InvokeRepeating(nameof(DebugStatus), 1f, 2f);
        }
        
        void DebugStatus()
        {
            if (!showDebugInfo) return;
            
            Debug.Log("=== Volumetric Cloud Debug ===");
            
            if (cloudVolume == null)
            {
                Debug.LogError("VolumetricCloudVolume component not found!");
                return;
            }
            
            // Check if initialized
            var initializedField = cloudVolume.GetType().GetField("_isInitialized", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            bool isInitialized = initializedField != null && (bool)initializedField.GetValue(cloudVolume);
            Debug.Log($"Initialized: {isInitialized}");
            
            // Check mesh renderer
            if (meshRenderer == null)
            {
                Debug.LogError("MeshRenderer not found!");
                return;
            }
            Debug.Log($"MeshRenderer enabled: {meshRenderer.enabled}");
            
            // Check material
            if (material == null)
            {
                Debug.LogError("Material is null!");
                return;
            }
            Debug.Log($"Material: {material.name}");
            Debug.Log($"Material shader: {material.shader?.name}");
            
            // Check density volume texture
            var densityVolume = material.GetTexture("_DensityVolume");
            if (densityVolume == null)
            {
                Debug.LogError("Density Volume texture is NULL!");
            }
            else
            {
                Debug.Log($"Density Volume: {densityVolume.name} ({densityVolume.width}x{densityVolume.height}x{(densityVolume as Texture3D)?.depth})");
            }
            
            // Check other shader properties
            if (logMaterialStatus)
            {
                Debug.Log($"Raymarch Steps: {material.GetInt("_RaymarchSteps")}");
                Debug.Log($"Step Size: {material.GetFloat("_StepSize")}");
                Debug.Log($"Cloud Density: {material.GetFloat("_CloudDensity")}");
                Debug.Log($"Volume Min: {material.GetVector("_VolumeMin")}");
                Debug.Log($"Volume Max: {material.GetVector("_VolumeMax")}");
            }
            
            Debug.Log("=============================");
        }
        
        void OnDrawGizmos()
        {
            if (!showDebugInfo) return;
            
            // Draw bounds
            Gizmos.color = new Color(1, 0, 0, 0.3f);
            Gizmos.DrawWireCube(transform.position, transform.localScale);
            
            // Draw label
            #if UNITY_EDITOR
            UnityEditor.Handles.Label(transform.position + Vector3.up * transform.localScale.y * 0.6f, 
                "Volumetric Cloud Volume");
            #endif
        }
    }
}
