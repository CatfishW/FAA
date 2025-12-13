using UnityEngine;
using System.Collections.Generic;

namespace WeatherRadar.Weather3D
{
    /// <summary>
    /// Renders volumetric 3D clouds using particle systems.
    /// Creates realistic cloud formations based on weather cell data.
    /// </summary>
    public class VolumetricCloudRenderer : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private ParticleSystem cloudParticleSystem;
        [SerializeField] private Material cloudMaterial;
        
        [Header("Cloud Settings")]
        [SerializeField] private Gradient cloudColorGradient;
        [SerializeField] private AnimationCurve densityCurve;
        
        // Configuration reference
        private Weather3DConfig config;
        private Weather3DManager manager;
        
        // Cloud particle data
        private List<CloudParticleData> activeCloudParticles = new List<CloudParticleData>();
        private ParticleSystem.Particle[] particles;
        private int maxParticles;
        
        // Animation
        private float animationTime;
        private bool isVisible = true;
        private Weather3DViewMode currentViewMode = Weather3DViewMode.Perspective3D;

        // Noise for cloud movement
        private float[] noiseOffsets;
        private const int NOISE_SAMPLES = 64;

        #region Initialization

        public void Initialize(Weather3DConfig config, Weather3DManager manager)
        {
            this.config = config;
            this.manager = manager;
            
            maxParticles = config.maxCloudParticles;
            particles = new ParticleSystem.Particle[maxParticles];
            
            InitializeParticleSystem();
            InitializeNoiseTable();
            InitializeDefaultGradient();
            
            Debug.Log($"[VolumetricCloudRenderer] Initialized with {maxParticles} max particles");
        }

        private void InitializeParticleSystem()
        {
            if (cloudParticleSystem == null)
            {
                // Create particle system
                GameObject psObj = new GameObject("CloudParticles");
                psObj.transform.SetParent(transform);
                psObj.transform.localPosition = Vector3.zero;
                cloudParticleSystem = psObj.AddComponent<ParticleSystem>();
                
                // Configure main module
                var main = cloudParticleSystem.main;
                main.loop = true;
                main.startLifetime = float.MaxValue;
                main.startSpeed = 0f;
                main.maxParticles = maxParticles;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.startSize3D = true;
                main.playOnAwake = true;
                
                // Disable emission (we control particles manually)
                var emission = cloudParticleSystem.emission;
                emission.enabled = false;
                
                // Configure renderer
                var renderer = cloudParticleSystem.GetComponent<ParticleSystemRenderer>();
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                renderer.sortMode = ParticleSystemSortMode.Distance;
                
                // Create or assign material
                if (cloudMaterial == null)
                {
                    cloudMaterial = new Material(Shader.Find("Particles/Standard Unlit"));
                    cloudMaterial.SetFloat("_Mode", 2); // Fade mode
                }
                renderer.material = cloudMaterial;
            }
        }

        private void InitializeNoiseTable()
        {
            noiseOffsets = new float[NOISE_SAMPLES];
            for (int i = 0; i < NOISE_SAMPLES; i++)
            {
                noiseOffsets[i] = Random.value * 1000f;
            }
        }

        private void InitializeDefaultGradient()
        {
            if (cloudColorGradient == null || cloudColorGradient.colorKeys.Length == 0)
            {
                cloudColorGradient = new Gradient();
                
                GradientColorKey[] colorKeys = new GradientColorKey[4];
                colorKeys[0] = new GradientColorKey(new Color(0.95f, 0.95f, 0.95f), 0f);   // Light clouds
                colorKeys[1] = new GradientColorKey(new Color(0.7f, 0.7f, 0.75f), 0.4f);   // Medium
                colorKeys[2] = new GradientColorKey(new Color(0.4f, 0.4f, 0.45f), 0.7f);   // Dark
                colorKeys[3] = new GradientColorKey(new Color(0.2f, 0.15f, 0.25f), 1f);    // Storm
                
                GradientAlphaKey[] alphaKeys = new GradientAlphaKey[3];
                alphaKeys[0] = new GradientAlphaKey(0.5f, 0f);
                alphaKeys[1] = new GradientAlphaKey(0.8f, 0.5f);
                alphaKeys[2] = new GradientAlphaKey(0.9f, 1f);
                
                cloudColorGradient.SetKeys(colorKeys, alphaKeys);
            }
            
            if (densityCurve == null || densityCurve.keys.Length == 0)
            {
                densityCurve = AnimationCurve.EaseInOut(0f, 0.3f, 1f, 1f);
            }
        }

        #endregion

        #region Update Methods

        private void Update()
        {
            if (!isVisible || config == null) return;
            
            if (config.animateClouds)
            {
                animationTime += Time.deltaTime * config.cloudAnimationSpeed;
                UpdateCloudAnimation();
            }
        }

        /// <summary>
        /// Update cloud visualization with new weather data
        /// </summary>
        public void UpdateClouds(Weather3DData data)
        {
            if (data == null || cloudParticleSystem == null) return;
            
            activeCloudParticles.Clear();
            
            // Generate cloud particles from weather cells
            foreach (var cell in data.weatherCells)
            {
                GenerateCloudParticlesForCell(cell, data);
            }
            
            // Generate additional clouds from cloud layers
            foreach (var layer in data.cloudLayers)
            {
                GenerateCloudParticlesForLayer(layer, data);
            }
            
            // Apply particles to system
            ApplyParticlesToSystem();
        }

        private void GenerateCloudParticlesForCell(WeatherCell3D cell, Weather3DData data)
        {
            if (activeCloudParticles.Count >= maxParticles) return;
            
            // Calculate number of particles based on cell size and intensity
            int particleCount = Mathf.CeilToInt(
                cell.intensity * config.maxCloudParticles * 0.1f * 
                (cell.size.magnitude / 5000f)
            );
            particleCount = Mathf.Min(particleCount, 50); // Cap per cell
            
            // Generate particles distributed within the cell
            for (int i = 0; i < particleCount && activeCloudParticles.Count < maxParticles; i++)
            {
                Vector3 randomOffset = new Vector3(
                    Random.Range(-0.5f, 0.5f) * cell.size.x,
                    Random.Range(-0.3f, 0.5f) * cell.size.y, // Bias upward
                    Random.Range(-0.5f, 0.5f) * cell.size.z
                );
                
                Vector3 position = cell.position + randomOffset;
                
                // Size based on intensity and position in cell
                float distFromCenter = randomOffset.magnitude / cell.size.magnitude;
                float sizeFactor = 1f - distFromCenter * 0.5f;
                float size = config.cloudParticleSize * sizeFactor * 
                            (1f + Random.Range(-config.cloudSizeVariation, config.cloudSizeVariation));
                
                // Color based on intensity
                Color color = cloudColorGradient.Evaluate(cell.intensity);
                color.a *= config.cloudOpacity * densityCurve.Evaluate(cell.intensity);
                
                activeCloudParticles.Add(new CloudParticleData
                {
                    position = position,
                    size = size * (cell.cellType == WeatherCellType.Thunderstorm ? 1.5f : 1f),
                    color = color,
                    intensity = cell.intensity,
                    noiseIndex = Random.Range(0, NOISE_SAMPLES),
                    basePosition = position
                });
            }
        }

        private void GenerateCloudParticlesForLayer(CloudLayer layer, Weather3DData data)
        {
            if (activeCloudParticles.Count >= maxParticles) return;
            
            // Fewer particles for thin layers
            int particleCount = Mathf.CeilToInt(layer.coverage * 20f);
            particleCount = Mathf.Min(particleCount, maxParticles - activeCloudParticles.Count);
            
            float layerThickness = (layer.topAltitude - layer.baseAltitude) * 0.3048f; // Feet to meters
            float halfCoverage = data.coverageNM * 1852f * 0.4f; // NM to meters, 80% of area
            
            for (int i = 0; i < particleCount; i++)
            {
                Vector3 position = data.aircraftPosition + new Vector3(
                    Random.Range(-halfCoverage, halfCoverage),
                    layer.baseAltitude * 0.3048f + Random.Range(0, layerThickness),
                    Random.Range(-halfCoverage, halfCoverage)
                );
                
                float size = config.cloudParticleSize * Random.Range(0.8f, 1.5f);
                
                // Layer-specific coloring
                Color color = layer.tintColor;
                color.a *= config.cloudOpacity * layer.coverage;
                
                // Adjust for cloud type
                if (layer.layerType == CloudLayerType.Cumulonimbus)
                {
                    size *= 2f;
                }
                else if (layer.layerType == CloudLayerType.Cirrus)
                {
                    size *= 1.5f;
                    color.a *= 0.5f; // More transparent
                }
                
                activeCloudParticles.Add(new CloudParticleData
                {
                    position = position,
                    size = size,
                    color = color,
                    intensity = layer.coverage,
                    noiseIndex = Random.Range(0, NOISE_SAMPLES),
                    basePosition = position
                });
            }
        }

        private void ApplyParticlesToSystem()
        {
            if (cloudParticleSystem == null) return;
            
            int count = Mathf.Min(activeCloudParticles.Count, maxParticles);
            
            for (int i = 0; i < count; i++)
            {
                var data = activeCloudParticles[i];
                
                particles[i].position = data.position;
                particles[i].startSize3D = new Vector3(data.size, data.size * 0.6f, data.size);
                particles[i].startColor = data.color;
                particles[i].remainingLifetime = 1000f;
                particles[i].startLifetime = 1000f;
            }
            
            cloudParticleSystem.SetParticles(particles, count);
        }

        private void UpdateCloudAnimation()
        {
            if (activeCloudParticles.Count == 0) return;
            
            int count = cloudParticleSystem.GetParticles(particles);
            
            for (int i = 0; i < count && i < activeCloudParticles.Count; i++)
            {
                var data = activeCloudParticles[i];
                float noiseValue = Mathf.PerlinNoise(
                    animationTime * 0.1f + noiseOffsets[data.noiseIndex],
                    data.noiseIndex * 0.1f
                );
                
                // Subtle position animation
                Vector3 offset = new Vector3(
                    Mathf.Sin(animationTime * 0.5f + data.noiseIndex) * 0.5f,
                    Mathf.Sin(animationTime * 0.3f + data.noiseIndex * 1.3f) * 0.3f,
                    Mathf.Cos(animationTime * 0.4f + data.noiseIndex * 0.7f) * 0.5f
                );
                
                particles[i].position = data.basePosition + offset * (1f + data.intensity);
                
                // Subtle size pulsing
                float sizePulse = 1f + Mathf.Sin(animationTime * 2f + data.noiseIndex) * 0.1f;
                float size = data.size * sizePulse;
                particles[i].startSize3D = new Vector3(size, size * 0.6f, size);
                
                // Subtle alpha pulsing
                Color color = data.color;
                color.a *= 0.9f + noiseValue * 0.2f;
                particles[i].startColor = color;
            }
            
            cloudParticleSystem.SetParticles(particles, count);
        }

        #endregion

        #region Public Methods

        public void SetVisible(bool visible)
        {
            isVisible = visible;
            
            if (cloudParticleSystem != null)
            {
                var renderer = cloudParticleSystem.GetComponent<ParticleSystemRenderer>();
                if (renderer != null)
                {
                    renderer.enabled = visible;
                }
            }
        }

        public void SetViewMode(Weather3DViewMode mode)
        {
            currentViewMode = mode;
            
            // Adjust rendering based on view mode
            if (cloudParticleSystem != null)
            {
                var renderer = cloudParticleSystem.GetComponent<ParticleSystemRenderer>();
                switch (mode)
                {
                    case Weather3DViewMode.PlanView:
                        renderer.alignment = ParticleSystemRenderSpace.Facing;
                        break;
                    case Weather3DViewMode.ProfileView:
                        renderer.alignment = ParticleSystemRenderSpace.Facing;
                        break;
                    case Weather3DViewMode.Perspective3D:
                        renderer.alignment = ParticleSystemRenderSpace.View;
                        break;
                }
            }
        }

        public void Clear()
        {
            activeCloudParticles.Clear();
            if (cloudParticleSystem != null)
            {
                cloudParticleSystem.Clear();
            }
        }

        #endregion

        /// <summary>
        /// Internal data structure for cloud particles
        /// </summary>
        private struct CloudParticleData
        {
            public Vector3 position;
            public Vector3 basePosition;
            public float size;
            public Color color;
            public float intensity;
            public int noiseIndex;
        }
    }
}
