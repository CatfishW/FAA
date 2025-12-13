using UnityEngine;
using System.Collections.Generic;

namespace WeatherRadar.Weather3D
{
    /// <summary>
    /// Unified precipitation effects system for rain, snow, and mixed precipitation.
    /// Uses optimized particle systems for realistic precipitation visualization.
    /// </summary>
    public class PrecipitationSystem : MonoBehaviour
    {
        [Header("Particle Systems")]
        [SerializeField] private ParticleSystem rainParticleSystem;
        [SerializeField] private ParticleSystem snowParticleSystem;
        [SerializeField] private ParticleSystem mixedParticleSystem;
        
        [Header("Materials")]
        [SerializeField] private Material rainMaterial;
        [SerializeField] private Material snowMaterial;
        
        [Header("Settings")]
        [SerializeField] private float precipitationHeight = 50f;
        [SerializeField] private float precipitationRadius = 100f;

        // Configuration
        private Weather3DConfig config;
        private Weather3DManager manager;
        
        // State
        private bool isVisible = true;
        private Weather3DViewMode currentViewMode = Weather3DViewMode.Perspective3D;
        private List<PrecipitationZone> activeZones = new List<PrecipitationZone>();

        #region Initialization

        public void Initialize(Weather3DConfig config, Weather3DManager manager)
        {
            this.config = config;
            this.manager = manager;
            
            CreateRainSystem();
            CreateSnowSystem();
            
            Debug.Log("[PrecipitationSystem] Initialized");
        }

        private void CreateRainSystem()
        {
            if (rainParticleSystem == null)
            {
                GameObject rainObj = new GameObject("RainParticles");
                rainObj.transform.SetParent(transform);
                rainObj.transform.localPosition = Vector3.zero;
                rainParticleSystem = rainObj.AddComponent<ParticleSystem>();
                
                ConfigureRainParticles();
            }
        }

        private void ConfigureRainParticles()
        {
            var main = rainParticleSystem.main;
            main.loop = true;
            main.startLifetime = 2f;
            main.startSpeed = config?.rainFallSpeed ?? 20f;
            main.maxParticles = config?.maxPrecipitationParticles ?? 1000;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startSize = config?.rainDropSize ?? 0.1f;
            main.gravityModifier = 1f;
            main.startColor = new Color(0.7f, 0.8f, 0.9f, 0.6f);
            
            var emission = rainParticleSystem.emission;
            emission.enabled = true;
            emission.rateOverTime = 0; // Controlled by zones
            
            var shape = rainParticleSystem.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(precipitationRadius, 1f, precipitationRadius);
            shape.position = new Vector3(0, precipitationHeight, 0);
            
            // Stretched billboard for rain streaks
            var renderer = rainParticleSystem.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.1f;
            renderer.lengthScale = 2f;
            
            if (rainMaterial == null)
            {
                rainMaterial = new Material(Shader.Find("Particles/Standard Unlit"));
                rainMaterial.SetFloat("_Mode", 2); // Fade
            }
            renderer.material = rainMaterial;
            
            // Add noise for wind effect
            var noise = rainParticleSystem.noise;
            noise.enabled = true;
            noise.strength = 0.3f;
            noise.frequency = 1f;
            noise.scrollSpeed = 0.5f;
        }

        private void CreateSnowSystem()
        {
            if (snowParticleSystem == null)
            {
                GameObject snowObj = new GameObject("SnowParticles");
                snowObj.transform.SetParent(transform);
                snowObj.transform.localPosition = Vector3.zero;
                snowParticleSystem = snowObj.AddComponent<ParticleSystem>();
                
                ConfigureSnowParticles();
            }
        }

        private void ConfigureSnowParticles()
        {
            var main = snowParticleSystem.main;
            main.loop = true;
            main.startLifetime = 8f;
            main.startSpeed = config?.snowFallSpeed ?? 3f;
            main.maxParticles = config?.maxPrecipitationParticles ?? 1000;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startSize = config?.snowFlakeSize ?? 0.08f;
            main.gravityModifier = 0.1f;
            main.startColor = new Color(1f, 1f, 1f, 0.8f);
            main.startRotation3D = true;
            
            var emission = snowParticleSystem.emission;
            emission.enabled = true;
            emission.rateOverTime = 0;
            
            var shape = snowParticleSystem.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(precipitationRadius, 1f, precipitationRadius);
            shape.position = new Vector3(0, precipitationHeight, 0);
            
            // Billboard for snowflakes
            var renderer = snowParticleSystem.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            
            if (snowMaterial == null)
            {
                snowMaterial = new Material(Shader.Find("Particles/Standard Unlit"));
                snowMaterial.SetFloat("_Mode", 2);
            }
            renderer.material = snowMaterial;
            
            // Rotation over lifetime
            var rot = snowParticleSystem.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-1f, 1f);
            
            // More noise for drifting snow
            var noise = snowParticleSystem.noise;
            noise.enabled = true;
            noise.strength = 0.5f;
            noise.frequency = 0.5f;
            noise.scrollSpeed = 0.3f;
        }

        #endregion

        #region Update Methods

        /// <summary>
        /// Update precipitation based on weather data
        /// </summary>
        public void UpdatePrecipitation(Weather3DData data)
        {
            if (data == null) return;
            
            activeZones.Clear();
            
            // Create precipitation zones from weather cells
            foreach (var cell in data.weatherCells)
            {
                if (cell.intensity > 0.1f)
                {
                    activeZones.Add(new PrecipitationZone
                    {
                        center = cell.position,
                        radius = Mathf.Max(cell.size.x, cell.size.z) * 0.5f,
                        intensity = cell.intensity,
                        type = GetPrecipitationType(cell),
                        baseAltitude = cell.altitude * 0.3048f,
                        topAltitude = cell.topAltitude * 0.3048f
                    });
                }
            }
            
            // Update particle emission rates
            UpdateEmissionRates();
            
            // Position systems at aircraft location
            if (data.aircraftPosition != Vector3.zero)
            {
                transform.position = data.aircraftPosition;
            }
        }

        private PrecipitationType GetPrecipitationType(WeatherCell3D cell)
        {
            // Determine precipitation type based on altitude and cell characteristics
            if (cell.altitude > 15000f) // High altitude = likely snow
            {
                return PrecipitationType.Snow;
            }
            else if (cell.cellType == WeatherCellType.Snow)
            {
                return PrecipitationType.Snow;
            }
            else if (cell.cellType == WeatherCellType.MixedPrecipitation)
            {
                return PrecipitationType.Mixed;
            }
            else
            {
                return PrecipitationType.Rain;
            }
        }

        private void UpdateEmissionRates()
        {
            float totalRainIntensity = 0f;
            float totalSnowIntensity = 0f;
            
            foreach (var zone in activeZones)
            {
                float contribution = zone.intensity * (zone.radius / precipitationRadius);
                
                switch (zone.type)
                {
                    case PrecipitationType.Rain:
                        totalRainIntensity += contribution;
                        break;
                    case PrecipitationType.Snow:
                        totalSnowIntensity += contribution;
                        break;
                    case PrecipitationType.Mixed:
                        totalRainIntensity += contribution * 0.5f;
                        totalSnowIntensity += contribution * 0.5f;
                        break;
                }
            }
            
            // Apply emission rates
            if (rainParticleSystem != null)
            {
                var emission = rainParticleSystem.emission;
                emission.rateOverTime = Mathf.Min(totalRainIntensity * config.maxPrecipitationParticles, config.maxPrecipitationParticles);
            }
            
            if (snowParticleSystem != null)
            {
                var emission = snowParticleSystem.emission;
                emission.rateOverTime = Mathf.Min(totalSnowIntensity * config.maxPrecipitationParticles * 0.5f, config.maxPrecipitationParticles * 0.5f);
            }
        }

        #endregion

        #region Public Methods

        public void SetVisible(bool visible)
        {
            isVisible = visible;
            
            if (rainParticleSystem != null)
            {
                var renderer = rainParticleSystem.GetComponent<ParticleSystemRenderer>();
                if (renderer != null) renderer.enabled = visible;
            }
            
            if (snowParticleSystem != null)
            {
                var renderer = snowParticleSystem.GetComponent<ParticleSystemRenderer>();
                if (renderer != null) renderer.enabled = visible;
            }
        }

        public void SetViewMode(Weather3DViewMode mode)
        {
            currentViewMode = mode;
        }

        public void Clear()
        {
            activeZones.Clear();
            
            if (rainParticleSystem != null)
            {
                rainParticleSystem.Clear();
                var emission = rainParticleSystem.emission;
                emission.rateOverTime = 0;
            }
            
            if (snowParticleSystem != null)
            {
                snowParticleSystem.Clear();
                var emission = snowParticleSystem.emission;
                emission.rateOverTime = 0;
            }
        }

        /// <summary>
        /// Set wind direction for precipitation drift
        /// </summary>
        public void SetWindDirection(Vector3 windDirection, float windSpeed)
        {
            if (rainParticleSystem != null)
            {
                var velocityOverLifetime = rainParticleSystem.velocityOverLifetime;
                velocityOverLifetime.enabled = true;
                velocityOverLifetime.x = windDirection.x * windSpeed * 0.1f;
                velocityOverLifetime.z = windDirection.z * windSpeed * 0.1f;
            }
            
            if (snowParticleSystem != null)
            {
                var velocityOverLifetime = snowParticleSystem.velocityOverLifetime;
                velocityOverLifetime.enabled = true;
                velocityOverLifetime.x = windDirection.x * windSpeed * 0.3f; // Snow drifts more
                velocityOverLifetime.z = windDirection.z * windSpeed * 0.3f;
            }
        }

        #endregion

        /// <summary>
        /// Precipitation zone data
        /// </summary>
        private struct PrecipitationZone
        {
            public Vector3 center;
            public float radius;
            public float intensity;
            public PrecipitationType type;
            public float baseAltitude;
            public float topAltitude;
        }

        /// <summary>
        /// Types of precipitation
        /// </summary>
        private enum PrecipitationType
        {
            Rain,
            Snow,
            Mixed
        }
    }
}
