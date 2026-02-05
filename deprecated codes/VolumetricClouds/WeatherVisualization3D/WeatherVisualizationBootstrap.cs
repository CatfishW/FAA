using UnityEngine;

namespace WeatherVisualization3D
{
    /// <summary>
    /// Bootstrap component that initializes and links all weather visualization components.
    /// Ensures proper references between simulator and renderers.
    /// </summary>
    public class WeatherVisualizationBootstrap : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private WeatherSimulator simulator;
        [SerializeField] private IntensityPillarRenderer pillarRenderer;
        [SerializeField] private VolumetricLightning lightningEffect;
        [SerializeField] private PrecipitationVFX precipitationEffect;
        [SerializeField] private VolumetricCloudVolume cloudVolume;
        
        [Header("Camera")]
        [SerializeField] private Camera followCamera;
        
        private void Awake()
        {
            FindReferences();
            SetupComponents();
        }
        
        private void FindReferences()
        {
            // Find simulator
            if (simulator == null)
            {
                simulator = GetComponentInChildren<WeatherSimulator>();
                if (simulator == null)
                {
                    Debug.LogError("[WeatherVisualizationBootstrap] WeatherSimulator not found!");
                    return;
                }
            }
            
            // Find renderers
            if (pillarRenderer == null)
                pillarRenderer = GetComponentInChildren<IntensityPillarRenderer>();
            if (lightningEffect == null)
                lightningEffect = GetComponentInChildren<VolumetricLightning>();
            if (precipitationEffect == null)
                precipitationEffect = GetComponentInChildren<PrecipitationVFX>();
            if (cloudVolume == null)
                cloudVolume = GetComponentInChildren<VolumetricCloudVolume>();
            
            // Find camera if not set
            if (followCamera == null)
                followCamera = Camera.main;
        }
        
        private void SetupComponents()
        {
            if (simulator == null) return;
            
            // Link simulator to pillar renderer
            if (pillarRenderer != null)
            {
                pillarRenderer.SetWeatherSimulator(simulator);
                Debug.Log("[WeatherVisualizationBootstrap] Linked WeatherSimulator to IntensityPillarRenderer");
            }
            
            // Link simulator to lightning effect
            if (lightningEffect != null)
            {
                var simField = lightningEffect.GetType().GetField("weatherSimulator", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (simField != null)
                {
                    simField.SetValue(lightningEffect, simulator);
                    Debug.Log("[WeatherVisualizationBootstrap] Linked WeatherSimulator to VolumetricLightning");
                }
            }
            
            // Link simulator to precipitation effect
            if (precipitationEffect != null)
            {
                precipitationEffect.SetWeatherSimulator(simulator);
                
                // Set camera follow target
                if (followCamera != null)
                {
                    precipitationEffect.SetFollowTarget(followCamera.transform);
                }
                
                Debug.Log("[WeatherVisualizationBootstrap] Linked WeatherSimulator to PrecipitationVFX");
            }
            
            // Link simulator to cloud volume
            if (cloudVolume != null)
            {
                // Cloud volume gets data through events
                simulator.OnDataUpdated += (data) => {
                    cloudVolume.UpdateData(data);
                };
                Debug.Log("[WeatherVisualizationBootstrap] Linked WeatherSimulator to VolumetricCloudVolume");
            }
        }
        
        private void OnValidate()
        {
            // Auto-find references in editor
            if (simulator == null)
                simulator = GetComponentInChildren<WeatherSimulator>();
            if (pillarRenderer == null)
                pillarRenderer = GetComponentInChildren<IntensityPillarRenderer>();
            if (lightningEffect == null)
                lightningEffect = GetComponentInChildren<VolumetricLightning>();
            if (precipitationEffect == null)
                precipitationEffect = GetComponentInChildren<PrecipitationVFX>();
            if (cloudVolume == null)
                cloudVolume = GetComponentInChildren<VolumetricCloudVolume>();
        }
    }
}
