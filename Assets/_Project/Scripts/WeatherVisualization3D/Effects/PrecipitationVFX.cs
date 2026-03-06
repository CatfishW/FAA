using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace WeatherVisualization3D
{
    /// <summary>
    /// Precipitation visual effects using particle systems.
    /// Creates rain and snow effects based on storm cell positions and intensity.
    /// </summary>
    [ExecuteInEditMode]
    public class PrecipitationVFX : MonoBehaviour, IWeatherEffectRenderer
    {
        #region Inspector Fields

        [Header("Precipitation Settings")]
        [Tooltip("Rain particle prefab (optional, will create default if null)")]
        [SerializeField] private GameObject rainPrefab;

        [Tooltip("Snow particle prefab (optional, will create default if null)")]
        [SerializeField] private GameObject snowPrefab;

        [Tooltip("Rain material (optional, will create default if null)")]
        [SerializeField] private Material rainMaterial;

        [Tooltip("Snow material (optional, will create default if null)")]
        [SerializeField] private Material snowMaterial;

        [Tooltip("Follow camera for precipitation")]
        [SerializeField] private Transform followTarget;
        
        [Header("Rain Settings")]
        [Tooltip("Maximum rain particles")]
        [Range(100, 10000)]
        [SerializeField] private int maxRainParticles = 5000;
        
        [Tooltip("Rain particle size")]
        [Range(0.1f, 5f)]
        [SerializeField] private float rainParticleSize = 0.5f;
        
        [Tooltip("Rain fall speed")]
        [Range(5f, 50f)]
        [SerializeField] private float rainFallSpeed = 20f;
        
        [Tooltip("Rain color")]
        [SerializeField] private Color rainColor = new Color(0.7f, 0.8f, 0.9f, 0.4f);
        
        [Header("Snow Settings")]
        [Tooltip("Maximum snow particles")]
        [Range(100, 5000)]
        [SerializeField] private int maxSnowParticles = 2000;
        
        [Tooltip("Snow particle size")]
        [Range(0.1f, 3f)]
        [SerializeField] private float snowParticleSize = 0.3f;
        
        [Tooltip("Snow fall speed")]
        [Range(0.5f, 10f)]
        [SerializeField] private float snowFallSpeed = 2f;
        
        [Tooltip("Snow color")]
        [SerializeField] private Color snowColor = new Color(1f, 1f, 1f, 0.6f);
        
        [Header("Coverage")]
        [Tooltip("Precipitation spawn area size")]
        [SerializeField] private Vector3 spawnAreaSize = new Vector3(5000f, 2000f, 5000f);
        
        [Tooltip("Precipitation spawn height above ground")]
        [Range(500f, 10000f)]
        [SerializeField] private float spawnHeight = 3000f;
        
        [Header("Temperature")]
        [Tooltip("Temperature threshold for snow (below this = snow, above = rain)")]
        [Range(-20f, 10f)]
        [SerializeField] private float snowThresholdCelsius = 2f;
        
        [Tooltip("Current temperature (for testing)")]
        [Range(-30f, 40f)]
        [SerializeField] private float currentTemperature = 15f;
        
        [Header("References")]
        [Tooltip("Weather simulator reference")]
        [SerializeField] private WeatherSimulator weatherSimulator;
        
        [Header("Test Mode")]
        [Tooltip("Enable test precipitation (ignores weather simulator)")]
        [SerializeField] private bool testMode = false;

        [Tooltip("Test precipitation rate (0-1)")]
        [Range(0f, 1f)]
        [SerializeField] private float testPrecipitationRate = 0.5f;

        [Header("Performance")]
        [Tooltip("Update frequency in Hz")]
        [Range(1f, 30f)]
        [SerializeField] private float updateFrequency = 10f;
        
        #endregion

        #region Private Fields

        private ParticleSystem rainParticleSystem;
        private ParticleSystem snowParticleSystem;
        // Don't cache module references - access via particleSystem.main/emission etc.

        private bool isInitialized;
        private float effectIntensity = 1f;
        private bool effectEnabled = true;
        private float lastUpdateTime;
        private float currentPrecipitationRate;
        
        #endregion

        #region IWeatherEffectRenderer Implementation
        
        public string EffectName => "Precipitation Effects";
        
        public bool IsActive
        {
            get => enabled && effectEnabled && gameObject.activeInHierarchy;
            set
            {
                effectEnabled = value;
                if (value)
                    Enable();
                else
                    Disable();
            }
        }
        
        public float IntensityMultiplier
        {
            get => effectIntensity;
            set
            {
                effectIntensity = Mathf.Clamp01(value);
                UpdateEmissionRates();
            }
        }
        
        public void Initialize(WeatherVolumeConfig config)
        {
            CreateParticleSystems();
            
            if (followTarget == null)
            {
                // Try to find main camera
                if (Camera.main != null)
                {
                    followTarget = Camera.main.transform;
                }
            }
            
            isInitialized = true;
            Debug.Log("[PrecipitationVFX] Initialized");
        }
        
        public void UpdateEffect(WeatherVolumeData data)
        {
            // Effect updates are handled in Update() via the weather simulator
            // This method is called by the manager when volume data changes
        }
        
        public void Cleanup()
        {
            if (rainParticleSystem != null)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    DestroyImmediate(rainParticleSystem.gameObject);
                else
#endif
                    Destroy(rainParticleSystem.gameObject);
                rainParticleSystem = null;
            }

            if (snowParticleSystem != null)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    DestroyImmediate(snowParticleSystem.gameObject);
                else
#endif
                    Destroy(snowParticleSystem.gameObject);
                snowParticleSystem = null;
            }

            isInitialized = false;
        }
        
        #endregion
        
        #region Private Helpers
        
        private void Enable()
        {
            if (rainParticleSystem != null)
                rainParticleSystem.Play();
            if (snowParticleSystem != null)
                snowParticleSystem.Play();
        }
        
        private void Disable()
        {
            if (rainParticleSystem != null)
                rainParticleSystem.Stop();
            if (snowParticleSystem != null)
                snowParticleSystem.Stop();
        }
        
        #endregion

        #region Unity Lifecycle
        
        private void Awake()
        {
            if (!isInitialized)
            {
                Initialize(null);
            }
        }

        private void OnEnable()
        {
            // Check if we need to recreate (either not initialized or systems are missing)
            if (!isInitialized || rainParticleSystem == null || snowParticleSystem == null)
            {
                Cleanup(); // Clean up any partial state
                Initialize(null);
            }
            else
            {
                // Re-enable particle systems if already initialized
                if (rainParticleSystem != null)
                    rainParticleSystem.Play();
                if (snowParticleSystem != null)
                    snowParticleSystem.Play();
            }
        }

        private void OnDisable()
        {
            Disable();
        }

        private void OnDestroy()
        {
            Cleanup();
        }
        
        private void Update()
        {
            if (!isInitialized || !effectEnabled)
                return;
            
            // Update position to follow target
            if (followTarget != null)
            {
                Vector3 targetPos = followTarget.position;
                targetPos.y = spawnHeight;
                transform.position = targetPos;
            }
            
            // Update precipitation based on weather
            float updateInterval = 1f / updateFrequency;
            if (Time.time - lastUpdateTime >= updateInterval)
            {
                UpdatePrecipitation();
                lastUpdateTime = Time.time;
            }
        }
        
        #endregion

        #region Particle System Setup
        
        private void CreateParticleSystems()
        {
            // Create rain particle system
            if (rainPrefab != null)
            {
                GameObject rainObj = Instantiate(rainPrefab, transform);
                rainParticleSystem = rainObj.GetComponent<ParticleSystem>();
            }
            else
            {
                rainParticleSystem = CreateDefaultRainSystem();
            }
            
            // Create snow particle system
            if (snowPrefab != null)
            {
                GameObject snowObj = Instantiate(snowPrefab, transform);
                snowParticleSystem = snowObj.GetComponent<ParticleSystem>();
            }
            else
            {
                snowParticleSystem = CreateDefaultSnowSystem();
            }

            // Start with zero emission
            var rainEmission = rainParticleSystem.emission;
            rainEmission.rateOverTime = 0;
            var snowEmission = snowParticleSystem.emission;
            snowEmission.rateOverTime = 0;
        }
        
        private ParticleSystem CreateDefaultRainSystem()
        {
            GameObject rainObj = new GameObject("RainParticles");
            rainObj.transform.SetParent(transform, false);

            ParticleSystem ps = rainObj.AddComponent<ParticleSystem>();

            // Main module
            var main = ps.main;
            main.maxParticles = maxRainParticles;
            main.startLifetime = spawnAreaSize.y / rainFallSpeed;
            main.startSpeed = rainFallSpeed;
            main.startSize = rainParticleSize;
            main.startColor = rainColor;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0f; // We control speed directly
            main.loop = true;
            main.playOnAwake = true;

            // Shape module - box emitter
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = spawnAreaSize;
            shape.position = Vector3.zero;
            shape.rotation = new Vector3(0f, 0f, 0f);

            // Emission module
            var emission = ps.emission;
            emission.rateOverTime = 0; // Will be set dynamically

            // Velocity over lifetime - straight down
            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.y = new ParticleSystem.MinMaxCurve(-rainFallSpeed, -rainFallSpeed);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            // Renderer - use existing material or create new one
            var renderer = rainObj.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 2f;
            renderer.velocityScale = 0.1f;

            // Check for existing material first, then create new one
            if (rainMaterial == null)
            {
                rainMaterial = CreateRainMaterial();
            }
            renderer.material = rainMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            Debug.Log($"[PrecipitationVFX] Rain system created with material: {(renderer.material != null ? renderer.material.name : "NULL")}");

            return ps;
        }
        
        private ParticleSystem CreateDefaultSnowSystem()
        {
            GameObject snowObj = new GameObject("SnowParticles");
            snowObj.transform.SetParent(transform, false);
            
            ParticleSystem ps = snowObj.AddComponent<ParticleSystem>();
            
            // Main module
            var main = ps.main;
            main.maxParticles = maxSnowParticles;
            main.startLifetime = spawnAreaSize.y / snowFallSpeed * 2f; // Longer lifetime
            main.startSpeed = snowFallSpeed;
            main.startSize = snowParticleSize;
            main.startColor = snowColor;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0f;
            main.loop = true;
            main.playOnAwake = true;
            
            // Shape module
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = spawnAreaSize;
            
            // Emission module
            var emission = ps.emission;
            emission.rateOverTime = 0;
            
            // Velocity over lifetime - gentle drift
            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.5f, 0.5f);
            velocity.y = new ParticleSystem.MinMaxCurve(-snowFallSpeed, -snowFallSpeed);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.5f, 0.5f);
            
            // Noise module for realistic drifting
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.5f;
            noise.frequency = 0.3f;
            noise.scrollSpeed = 0.2f;
            noise.damping = true;
            
            // Rotation over lifetime
            var rotation = ps.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-90f, 90f);
            
            // Renderer - use existing material or create new one
            var renderer = snowObj.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;

            // Check for existing material first, then create new one
            if (snowMaterial == null)
            {
                snowMaterial = CreateSnowMaterial();
            }
            renderer.material = snowMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            Debug.Log($"[PrecipitationVFX] Snow system created with material: {(renderer.material != null ? renderer.material.name : "NULL")}");

            return ps;
        }
        
        private Material CreateRainMaterial()
        {
            Shader shader = Shader.Find("Particles/Standard Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
                Debug.Log("[PrecipitationVFX] Using fallback Sprites/Default shader for rain");
            }

            Material mat = new Material(shader);
            mat.name = "RainMaterial";
            mat.SetColor("_Color", rainColor);

            Texture2D rainTexture = WeatherParticleTextureGenerator.GetRainTexture();
            if (rainTexture != null)
            {
                mat.SetTexture("_MainTex", rainTexture);
            }
            else
            {
                Debug.LogWarning("[PrecipitationVFX] Rain texture is null!");
            }

            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = 3000;

            // Store reference so it shows in inspector
            rainMaterial = mat;

#if UNITY_EDITOR
            // Create persistent material asset in editor
            string path = "Assets/_Project/Materials/WeatherVisualization/RainMaterial.mat";
            string dir = System.IO.Path.GetDirectoryName(path);
            if (!System.IO.Directory.Exists(dir))
            {
                System.IO.Directory.CreateDirectory(dir);
            }
            UnityEditor.AssetDatabase.CreateAsset(mat, path);
            UnityEditor.AssetDatabase.SaveAssets();
            Debug.Log($"[PrecipitationVFX] Created rain material asset at: {path}");
#endif

            return mat;
        }
        
        private Material CreateSnowMaterial()
        {
            Shader shader = Shader.Find("Particles/Standard Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
                Debug.Log("[PrecipitationVFX] Using fallback Sprites/Default shader for snow");
            }

            Material mat = new Material(shader);
            mat.name = "SnowMaterial";
            mat.SetColor("_Color", snowColor);

            Texture2D snowTexture = WeatherParticleTextureGenerator.GetSnowTexture();
            if (snowTexture != null)
            {
                mat.SetTexture("_MainTex", snowTexture);
            }
            else
            {
                Debug.LogWarning("[PrecipitationVFX] Snow texture is null!");
            }

            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = 3000;

            // Store reference so it shows in inspector
            snowMaterial = mat;

#if UNITY_EDITOR
            // Create persistent material asset in editor
            string path = "Assets/_Project/Materials/WeatherVisualization/SnowMaterial.mat";
            string dir = System.IO.Path.GetDirectoryName(path);
            if (!System.IO.Directory.Exists(dir))
            {
                System.IO.Directory.CreateDirectory(dir);
            }
            UnityEditor.AssetDatabase.CreateAsset(mat, path);
            UnityEditor.AssetDatabase.SaveAssets();
            Debug.Log($"[PrecipitationVFX] Created snow material asset at: {path}");
#endif

            return mat;
        }
        
        #endregion

        #region Precipitation Logic
        
        private void UpdatePrecipitation()
        {
            if (testMode)
            {
                // Use test precipitation rate
                currentPrecipitationRate = testPrecipitationRate;
                UpdateEmissionRates();
                return;
            }

            if (weatherSimulator == null)
            {
                // No simulator - no precipitation
                currentPrecipitationRate = 0f;
                UpdateEmissionRates();
                return;
            }

            // Calculate precipitation rate based on nearby storm cells
            currentPrecipitationRate = CalculatePrecipitationRate();
            UpdateEmissionRates();
        }
        
        private float CalculatePrecipitationRate()
        {
            if (weatherSimulator == null)
                return 0f;
            
            var cells = weatherSimulator.GetActiveCells();
            float maxRate = 0f;
            
            Vector2 viewPos = followTarget != null 
                ? new Vector2(followTarget.position.x, followTarget.position.z)
                : Vector2.zero;
            
            foreach (var cell in cells)
            {
                if (!cell.IsActive)
                    continue;
                
                float distance = Vector2.Distance(viewPos, cell.Position);
                
                // Check if within cell radius (with some margin)
                if (distance <= cell.Radius * 1.5f)
                {
                    // Rate based on intensity and distance
                    float distanceFactor = 1f - Mathf.Clamp01((distance - cell.Radius * 0.5f) / (cell.Radius));
                    float rate = cell.PrecipitationRate * distanceFactor;
                    maxRate = Mathf.Max(maxRate, rate);
                }
            }
            
            return maxRate;
        }
        
        private void UpdateEmissionRates()
        {
            float rate = currentPrecipitationRate * effectIntensity;

#if UNITY_EDITOR
            if (rate > 0 && rainParticleSystem != null)
            {
                var emission = rainParticleSystem.emission;
                Debug.Log($"[PrecipitationVFX] Setting emission rate: {rate * maxRainParticles:F0} (rate={rate:F2}, temp={currentTemperature:F1}°C)");
            }
#endif

            bool isSnow = currentTemperature <= snowThresholdCelsius;

            if (rainParticleSystem != null && snowParticleSystem != null)
            {
                if (isSnow)
                {
                    // Snow mode
                    var rainEm = rainParticleSystem.emission;
                    rainEm.rateOverTime = 0;
                    var snowEm = snowParticleSystem.emission;
                    snowEm.rateOverTime = rate * maxSnowParticles;
                }
                else
                {
                    // Rain mode
                    var snowEm = snowParticleSystem.emission;
                    snowEm.rateOverTime = 0;
                    var rainEm = rainParticleSystem.emission;
                    rainEm.rateOverTime = rate * maxRainParticles;
                }
            }
        }
        
        #endregion

        #region Public API
        
        /// <summary>
        /// Set the weather simulator reference
        /// </summary>
        public void SetWeatherSimulator(WeatherSimulator simulator)
        {
            weatherSimulator = simulator;
        }
        
        /// <summary>
        /// Set the follow target
        /// </summary>
        public void SetFollowTarget(Transform target)
        {
            followTarget = target;
        }
        
        /// <summary>
        /// Set the current temperature
        /// </summary>
        public void SetTemperature(float celsius)
        {
            currentTemperature = celsius;
            UpdateEmissionRates();
        }
        
        /// <summary>
        /// Force a specific precipitation rate (0-1)
        /// </summary>
        public void SetPrecipitationRate(float rate)
        {
            currentPrecipitationRate = Mathf.Clamp01(rate);
            UpdateEmissionRates();
        }
        
        /// <summary>
        /// Get current precipitation type
        /// </summary>
        public PrecipitationType GetCurrentPrecipitationType()
        {
            if (currentPrecipitationRate <= 0.01f)
                return PrecipitationType.None;
            
            return currentTemperature <= snowThresholdCelsius 
                ? PrecipitationType.Snow 
                : PrecipitationType.Rain;
        }
        
        #endregion
    }

    /// <summary>
    /// Types of precipitation
    /// </summary>
    public enum PrecipitationType
    {
        None,
        Rain,
        Snow,
        Sleet,
        Hail
    }
}
