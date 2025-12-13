using UnityEngine;
using System.Collections;

namespace WeatherRadar.Weather3D
{
    /// <summary>
    /// Rain visualization effect with animated rain drops and splash effects.
    /// Supports different intensity levels from light drizzle to heavy downpour.
    /// </summary>
    public class RainEffect : MonoBehaviour
    {
        [Header("Particle Systems")]
        [SerializeField] private ParticleSystem rainDrops;
        [SerializeField] private ParticleSystem splashEffects;
        
        [Header("Rain Settings")]
        [SerializeField] private RainIntensity intensity = RainIntensity.Moderate;
        [SerializeField] private float dropSize = 0.05f;
        [SerializeField] private float fallSpeed = 20f;
        [SerializeField] private float coverageRadius = 50f;
        [SerializeField] private float emissionHeight = 50f;
        
        [Header("Visual Settings")]
        [SerializeField] private Color lightRainColor = new Color(0.7f, 0.8f, 0.9f, 0.4f);
        [SerializeField] private Color moderateRainColor = new Color(0.6f, 0.7f, 0.85f, 0.5f);
        [SerializeField] private Color heavyRainColor = new Color(0.5f, 0.6f, 0.8f, 0.6f);
        
        [Header("Wind")]
        [SerializeField] private Vector3 windDirection = Vector3.right;
        [SerializeField] private float windStrength = 0f;

        // State
        private bool isActive = false;
        private float currentEmissionRate;

        #region Initialization

        private void Awake()
        {
            if (rainDrops == null)
            {
                CreateRainDropSystem();
            }
            
            if (splashEffects == null)
            {
                CreateSplashSystem();
            }
        }

        private void CreateRainDropSystem()
        {
            GameObject obj = new GameObject("RainDrops");
            obj.transform.SetParent(transform);
            obj.transform.localPosition = new Vector3(0, emissionHeight, 0);
            
            rainDrops = obj.AddComponent<ParticleSystem>();
            
            var main = rainDrops.main;
            main.loop = true;
            main.startLifetime = emissionHeight / fallSpeed;
            main.startSpeed = fallSpeed;
            main.maxParticles = 5000;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startSize = dropSize;
            main.gravityModifier = 0f; // We control speed directly
            main.startColor = moderateRainColor;
            
            var emission = rainDrops.emission;
            emission.enabled = true;
            emission.rateOverTime = 0;
            
            var shape = rainDrops.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(coverageRadius * 2, 1, coverageRadius * 2);
            
            // Stretched billboard for rain streaks
            var renderer = rainDrops.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.05f;
            renderer.lengthScale = 3f;
            renderer.material = new Material(Shader.Find("Particles/Standard Unlit"));
        }

        private void CreateSplashSystem()
        {
            GameObject obj = new GameObject("SplashEffects");
            obj.transform.SetParent(transform);
            obj.transform.localPosition = Vector3.zero;
            
            splashEffects = obj.AddComponent<ParticleSystem>();
            
            var main = splashEffects.main;
            main.loop = true;
            main.startLifetime = 0.3f;
            main.startSpeed = 2f;
            main.maxParticles = 500;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startSize = dropSize * 2f;
            main.gravityModifier = 0.5f;
            main.startColor = new Color(0.8f, 0.85f, 0.9f, 0.3f);
            
            var emission = splashEffects.emission;
            emission.enabled = true;
            emission.rateOverTime = 0;
            
            var shape = splashEffects.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(coverageRadius * 2, 0.1f, coverageRadius * 2);
            
            var renderer = splashEffects.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.material = new Material(Shader.Find("Particles/Standard Unlit"));
            
            // Burst upward for splashes
            var velocityOverLifetime = splashEffects.velocityOverLifetime;
            velocityOverLifetime.enabled = true;
            velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(1f, 3f);
        }

        #endregion

        #region Update

        private void Update()
        {
            if (!isActive) return;
            
            // Update wind effect
            UpdateWindEffect();
        }

        private void UpdateWindEffect()
        {
            if (rainDrops == null || windStrength == 0) return;
            
            var velocityOverLifetime = rainDrops.velocityOverLifetime;
            velocityOverLifetime.enabled = true;
            velocityOverLifetime.x = windDirection.x * windStrength;
            velocityOverLifetime.z = windDirection.z * windStrength;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Set rain intensity
        /// </summary>
        public void SetIntensity(RainIntensity newIntensity)
        {
            intensity = newIntensity;
            ApplyIntensity();
        }

        /// <summary>
        /// Set intensity from a 0-1 value
        /// </summary>
        public void SetIntensityNormalized(float normalizedIntensity)
        {
            if (normalizedIntensity < 0.25f)
                SetIntensity(RainIntensity.Light);
            else if (normalizedIntensity < 0.5f)
                SetIntensity(RainIntensity.Moderate);
            else if (normalizedIntensity < 0.75f)
                SetIntensity(RainIntensity.Heavy);
            else
                SetIntensity(RainIntensity.Extreme);
        }

        /// <summary>
        /// Start rain effect
        /// </summary>
        public void StartRain()
        {
            isActive = true;
            ApplyIntensity();
            
            if (rainDrops != null && !rainDrops.isPlaying)
            {
                rainDrops.Play();
            }
            
            if (splashEffects != null && !splashEffects.isPlaying)
            {
                splashEffects.Play();
            }
        }

        /// <summary>
        /// Stop rain effect
        /// </summary>
        public void StopRain()
        {
            isActive = false;
            
            if (rainDrops != null)
            {
                var emission = rainDrops.emission;
                emission.rateOverTime = 0;
            }
            
            if (splashEffects != null)
            {
                var emission = splashEffects.emission;
                emission.rateOverTime = 0;
            }
        }

        /// <summary>
        /// Set wind parameters
        /// </summary>
        public void SetWind(Vector3 direction, float strength)
        {
            windDirection = direction.normalized;
            windStrength = strength;
        }

        /// <summary>
        /// Set coverage area
        /// </summary>
        public void SetCoverage(float radius, float height)
        {
            coverageRadius = radius;
            emissionHeight = height;
            
            if (rainDrops != null)
            {
                rainDrops.transform.localPosition = new Vector3(0, emissionHeight, 0);
                
                var shape = rainDrops.shape;
                shape.scale = new Vector3(coverageRadius * 2, 1, coverageRadius * 2);
                
                var main = rainDrops.main;
                main.startLifetime = emissionHeight / fallSpeed;
            }
            
            if (splashEffects != null)
            {
                var shape = splashEffects.shape;
                shape.scale = new Vector3(coverageRadius * 2, 0.1f, coverageRadius * 2);
            }
        }

        #endregion

        #region Private Methods

        private void ApplyIntensity()
        {
            if (rainDrops == null) return;
            
            var main = rainDrops.main;
            var emission = rainDrops.emission;
            
            switch (intensity)
            {
                case RainIntensity.Light:
                    currentEmissionRate = 100f;
                    main.startSize = dropSize * 0.7f;
                    main.startColor = lightRainColor;
                    main.startSpeed = fallSpeed * 0.8f;
                    break;
                    
                case RainIntensity.Moderate:
                    currentEmissionRate = 500f;
                    main.startSize = dropSize;
                    main.startColor = moderateRainColor;
                    main.startSpeed = fallSpeed;
                    break;
                    
                case RainIntensity.Heavy:
                    currentEmissionRate = 1500f;
                    main.startSize = dropSize * 1.3f;
                    main.startColor = heavyRainColor;
                    main.startSpeed = fallSpeed * 1.2f;
                    break;
                    
                case RainIntensity.Extreme:
                    currentEmissionRate = 3000f;
                    main.startSize = dropSize * 1.5f;
                    main.startColor = new Color(0.4f, 0.5f, 0.7f, 0.7f);
                    main.startSpeed = fallSpeed * 1.4f;
                    break;
            }
            
            emission.rateOverTime = isActive ? currentEmissionRate : 0;
            
            // Scale splashes with rain
            if (splashEffects != null)
            {
                var splashEmission = splashEffects.emission;
                splashEmission.rateOverTime = isActive ? currentEmissionRate * 0.1f : 0;
            }
        }

        #endregion
    }

    /// <summary>
    /// Rain intensity levels
    /// </summary>
    public enum RainIntensity
    {
        Light,      // Drizzle
        Moderate,   // Normal rain
        Heavy,      // Heavy rain
        Extreme     // Downpour / Thunderstorm
    }
}
