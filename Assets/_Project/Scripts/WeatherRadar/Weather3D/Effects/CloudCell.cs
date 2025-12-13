using UnityEngine;

namespace WeatherRadar.Weather3D
{
    /// <summary>
    /// Individual cloud cell component that represents a discrete cloud formation.
    /// Can be instantiated as a prefab or created dynamically.
    /// Features enhanced visuals with gradient colors, emission glow, and soft edges.
    /// </summary>
    public class CloudCell : MonoBehaviour
    {
        [Header("Visual Components")]
        [SerializeField] private ParticleSystem cloudParticles;
        [SerializeField] private MeshRenderer cloudMesh;
        [SerializeField] private MeshRenderer innerGlowMesh;
        
        [Header("Settings")]
        [SerializeField] private float baseSize = 1f;
        [SerializeField] private float morphSpeed = 0.5f;
        [SerializeField] private float internalTurbulence = 0.3f;
        [SerializeField] private bool enableEmission = true;
        [SerializeField] private float emissionIntensity = 0.5f;
        
        // State
        private float intensity;
        private WeatherCellType cellType;
        private Color baseColor;
        private Color emissionColor;
        private Vector3 targetScale;
        private float animationOffset;
        private Material cloudMaterial;
        private Material innerGlowMaterial;
        private bool useEnhancedVisuals = true;

        #region Initialization

        private void Awake()
        {
            animationOffset = Random.value * 100f;
            
            if (cloudParticles == null)
            {
                cloudParticles = GetComponentInChildren<ParticleSystem>();
            }
            
            if (cloudMesh == null)
            {
                cloudMesh = GetComponentInChildren<MeshRenderer>();
            }
            
            // Create inner glow mesh if not assigned
            CreateInnerGlowMesh();
        }

        /// <summary>
        /// Initialize the cloud cell with weather cell data
        /// </summary>
        public void Initialize(WeatherCell3D cellData, Weather3DConfig config)
        {
            intensity = cellData.intensity;
            cellType = cellData.cellType;
            baseColor = Weather3DShaderHelpers.GetIntensityGradientColor(intensity, true);
            emissionColor = Weather3DShaderHelpers.CalculateEmissionColor(baseColor, intensity);
            
            // Position
            transform.position = cellData.position;
            
            // Scale based on cell size
            targetScale = cellData.size * 0.001f; // Scale down for scene units
            transform.localScale = targetScale;
            
            // Configure visuals based on cell type with enhanced materials
            ConfigureEnhancedVisuals(config);
            ConfigureForCellType(config);
        }
        
        /// <summary>
        /// Create inner glow mesh for layered effect
        /// </summary>
        private void CreateInnerGlowMesh()
        {
            if (innerGlowMesh != null) return;
            
            GameObject glowObj = new GameObject("InnerGlow");
            glowObj.transform.SetParent(transform);
            glowObj.transform.localPosition = Vector3.zero;
            glowObj.transform.localScale = Vector3.one * 0.7f; // Slightly smaller
            
            MeshFilter filter = glowObj.AddComponent<MeshFilter>();
            filter.mesh = Weather3DShaderHelpers.CreateSoftSphereMesh(12);
            
            innerGlowMesh = glowObj.AddComponent<MeshRenderer>();
        }
        
        /// <summary>
        /// Configure enhanced visual materials
        /// </summary>
        private void ConfigureEnhancedVisuals(Weather3DConfig config)
        {
            if (!useEnhancedVisuals) return;
            
            // Create main cloud material with gradient and emission
            cloudMaterial = Weather3DShaderHelpers.CreateCloudMaterial(baseColor, intensity, enableEmission);
            if (cloudMesh != null)
            {
                cloudMesh.material = cloudMaterial;
            }
            
            // Create inner glow material (brighter core)
            if (innerGlowMesh != null)
            {
                innerGlowMaterial = Weather3DShaderHelpers.CreateCloudMaterial(baseColor, intensity, true);
                Color glowColor = baseColor;
                glowColor.a = Mathf.Min(1f, baseColor.a * 1.5f);
                innerGlowMaterial.color = glowColor;
                innerGlowMaterial.SetColor("_EmissionColor", emissionColor * (emissionIntensity * 1.5f));
                innerGlowMesh.material = innerGlowMaterial;
            }
        }

        private void ConfigureForCellType(Weather3DConfig config)
        {
            switch (cellType)
            {
                case WeatherCellType.LightRain:
                    SetupLightCloud(config);
                    break;
                    
                case WeatherCellType.ModerateRain:
                    SetupModerateCloud(config);
                    break;
                    
                case WeatherCellType.HeavyRain:
                    SetupHeavyCloud(config);
                    break;
                    
                case WeatherCellType.Thunderstorm:
                    SetupThunderstormCloud(config);
                    break;
                    
                case WeatherCellType.Snow:
                    SetupSnowCloud(config);
                    break;
                    
                default:
                    SetupDefaultCloud(config);
                    break;
            }
        }

        private void SetupLightCloud(Weather3DConfig config)
        {
            // Light rain - soft green gradient with subtle glow
            Color lightColor = Weather3DShaderHelpers.GetIntensityGradientColor(0.15f, true);
            
            if (cloudMaterial != null)
            {
                cloudMaterial.color = lightColor;
                if (enableEmission)
                {
                    cloudMaterial.SetColor("_EmissionColor", lightColor * 0.2f);
                }
            }
            
            if (cloudParticles != null)
            {
                var main = cloudParticles.main;
                main.startColor = new Color(lightColor.r, lightColor.g, lightColor.b, 0.4f);
                main.startSize = config.cloudParticleSize * 0.8f;
            }
        }

        private void SetupModerateCloud(Weather3DConfig config)
        {
            // Moderate rain - yellow gradient with medium glow
            Color modColor = Weather3DShaderHelpers.GetIntensityGradientColor(0.35f, true);
            
            if (cloudMaterial != null)
            {
                cloudMaterial.color = modColor;
                if (enableEmission)
                {
                    cloudMaterial.SetColor("_EmissionColor", modColor * 0.35f);
                }
            }
            
            if (cloudParticles != null)
            {
                var main = cloudParticles.main;
                main.startColor = new Color(modColor.r, modColor.g, modColor.b, 0.5f);
                main.startSize = config.cloudParticleSize;
            }
        }

        private void SetupHeavyCloud(Weather3DConfig config)
        {
            // Heavy rain - orange gradient with strong glow
            Color heavyColor = Weather3DShaderHelpers.GetIntensityGradientColor(0.55f, true);
            
            if (cloudMaterial != null)
            {
                cloudMaterial.color = heavyColor;
                if (enableEmission)
                {
                    cloudMaterial.SetColor("_EmissionColor", heavyColor * 0.5f);
                }
            }
            
            if (cloudParticles != null)
            {
                var main = cloudParticles.main;
                main.startColor = new Color(heavyColor.r, heavyColor.g, heavyColor.b, 0.6f);
                main.startSize = config.cloudParticleSize * 1.2f;
            }
        }

        private void SetupThunderstormCloud(Weather3DConfig config)
        {
            // Thunderstorm - red/magenta gradient with intense pulsing glow
            Color stormColor = Weather3DShaderHelpers.GetIntensityGradientColor(0.85f, true);
            
            if (cloudMaterial != null)
            {
                cloudMaterial.color = stormColor;
                if (enableEmission)
                {
                    // Stronger emission for thunderstorms
                    cloudMaterial.SetColor("_EmissionColor", stormColor * 0.8f);
                }
            }
            
            // Inner glow more intense for thunderstorms
            if (innerGlowMaterial != null)
            {
                Color innerColor = new Color(stormColor.r * 1.2f, stormColor.g * 0.9f, stormColor.b * 1.3f, 0.9f);
                innerGlowMaterial.color = innerColor;
                innerGlowMaterial.SetColor("_EmissionColor", innerColor * 1.2f);
            }
            
            if (cloudParticles != null)
            {
                var main = cloudParticles.main;
                main.startColor = new Color(stormColor.r, stormColor.g, stormColor.b, 0.7f);
                main.startSize = config.cloudParticleSize * 1.5f;
                
                // More turbulent particles
                var noise = cloudParticles.noise;
                noise.enabled = true;
                noise.strength = 0.6f;
                noise.frequency = 2.5f;
            }
            
            internalTurbulence = 0.7f;
            morphSpeed = 0.8f; // Faster morphing for dramatic effect
        }

        private void SetupSnowCloud(Weather3DConfig config)
        {
            // Snow - white/blue gradient with soft glow
            Color snowColor = new Color(0.85f, 0.9f, 1f, 0.55f);
            
            if (cloudMaterial != null)
            {
                cloudMaterial.color = snowColor;
                if (enableEmission)
                {
                    // Cool blue-white emission
                    cloudMaterial.SetColor("_EmissionColor", new Color(0.8f, 0.9f, 1f) * 0.25f);
                }
            }
            
            if (innerGlowMaterial != null)
            {
                innerGlowMaterial.color = new Color(0.95f, 0.97f, 1f, 0.7f);
                innerGlowMaterial.SetColor("_EmissionColor", new Color(0.9f, 0.95f, 1f) * 0.3f);
            }
            
            if (cloudParticles != null)
            {
                var main = cloudParticles.main;
                main.startColor = new Color(0.95f, 0.97f, 1f, 0.5f);
                main.startSize = config.cloudParticleSize * 1.1f;
            }
            
            morphSpeed = 0.3f; // Slower, more gentle movement
        }

        private void SetupDefaultCloud(Weather3DConfig config)
        {
            // Default - use intensity-based gradient color
            if (cloudMaterial != null)
            {
                cloudMaterial.color = baseColor;
                if (enableEmission)
                {
                    cloudMaterial.SetColor("_EmissionColor", emissionColor * emissionIntensity);
                }
            }
            
            if (innerGlowMaterial != null)
            {
                innerGlowMaterial.color = baseColor;
                innerGlowMaterial.SetColor("_EmissionColor", emissionColor * emissionIntensity * 1.2f);
            }
            
            if (cloudParticles != null)
            {
                var main = cloudParticles.main;
                main.startColor = baseColor;
                main.startSize = config.cloudParticleSize;
            }
        }

        #endregion

        #region Update

        private void Update()
        {
            AnimateMorphing();
            AnimateEmission();
        }

        private void AnimateMorphing()
        {
            // Use shader helper for breathing scale effect
            transform.localScale = Weather3DShaderHelpers.GetBreathingScale(
                targetScale, 
                0.08f, 
                morphSpeed, 
                animationOffset
            );
            
            // Internal turbulence rotation
            if (internalTurbulence > 0)
            {
                float rotSpeed = internalTurbulence * 10f;
                transform.Rotate(0, rotSpeed * Time.deltaTime, 0);
            }
        }
        
        /// <summary>
        /// Animate emission intensity for dynamic glow effect
        /// </summary>
        private void AnimateEmission()
        {
            if (!enableEmission) return;
            
            // Pulsing emission for thunderstorms
            if (cellType == WeatherCellType.Thunderstorm)
            {
                float pulse = Weather3DShaderHelpers.GetPulseAlpha(0.6f, 0.4f, 0.8f, animationOffset);
                
                if (cloudMaterial != null)
                {
                    cloudMaterial.SetColor("_EmissionColor", emissionColor * pulse);
                }
                if (innerGlowMaterial != null)
                {
                    innerGlowMaterial.SetColor("_EmissionColor", emissionColor * pulse * 1.3f);
                }
            }
            else
            {
                // Subtle breathing for other types
                float pulse = Weather3DShaderHelpers.GetPulseAlpha(emissionIntensity * 0.8f, emissionIntensity * 0.2f, 0.3f, animationOffset);
                
                if (innerGlowMaterial != null)
                {
                    innerGlowMaterial.SetColor("_EmissionColor", emissionColor * pulse);
                }
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Update cloud intensity and appearance
        /// </summary>
        public void SetIntensity(float newIntensity)
        {
            intensity = Mathf.Clamp01(newIntensity);
            
            // Update color based on new intensity
            Color color = GetIntensityColor(intensity);
            
            if (cloudMesh != null)
            {
                cloudMesh.material.color = color;
            }
        }

        /// <summary>
        /// Set cloud visibility
        /// </summary>
        public void SetVisible(bool visible)
        {
            if (cloudMesh != null)
            {
                cloudMesh.enabled = visible;
            }
            
            if (cloudParticles != null)
            {
                var renderer = cloudParticles.GetComponent<ParticleSystemRenderer>();
                if (renderer != null)
                {
                    renderer.enabled = visible;
                }
            }
        }

        /// <summary>
        /// Play lightning flash effect (for thunderstorm cells)
        /// </summary>
        public void FlashLightning()
        {
            if (cellType != WeatherCellType.Thunderstorm) return;
            
            StartCoroutine(LightningFlashCoroutine());
        }

        private System.Collections.IEnumerator LightningFlashCoroutine()
        {
            if (cloudMesh != null)
            {
                Color originalColor = cloudMesh.material.color;
                cloudMesh.material.color = Color.white;
                yield return new WaitForSeconds(0.05f);
                cloudMesh.material.color = new Color(0.8f, 0.8f, 0.9f, originalColor.a);
                yield return new WaitForSeconds(0.1f);
                cloudMesh.material.color = originalColor;
            }
        }

        private Color GetIntensityColor(float intensity)
        {
            if (intensity < 0.2f)
                return new Color(0.9f, 0.9f, 0.95f, 0.4f + intensity);
            else if (intensity < 0.4f)
                return new Color(0.7f, 0.7f, 0.75f, 0.5f + intensity * 0.5f);
            else if (intensity < 0.6f)
                return new Color(0.5f, 0.5f, 0.55f, 0.6f + intensity * 0.3f);
            else if (intensity < 0.8f)
                return new Color(0.3f, 0.3f, 0.4f, 0.7f + intensity * 0.2f);
            else
                return new Color(0.2f, 0.2f, 0.3f, 0.85f);
        }

        #endregion
    }
}
