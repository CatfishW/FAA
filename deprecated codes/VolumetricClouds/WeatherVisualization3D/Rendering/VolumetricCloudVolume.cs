using UnityEngine;
using System;

namespace WeatherVisualization3D
{
    /// <summary>
    /// Renders volumetric weather clouds using raymarching shaders.
    /// This is the main visual component that displays weather data as 3D volumetric clouds.
    /// Uses a cube mesh with a raymarching shader to render the volume.
    /// </summary>
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    [ExecuteInEditMode]
    public class VolumetricCloudVolume : MonoBehaviour, IVolumetricRenderer, ILayeredRenderer
    {
        #region Serialized Fields

        [Header("Rendering")]
        [SerializeField] private Material _cloudMaterial;
        [SerializeField] private Shader _cloudShader;
        [SerializeField] private bool _useEnhancedShader = true;
        [SerializeField] private bool _useURPCloudShader = true; // New: Use URP shader with real 3D textures

        [Header("3D Noise Textures (for URP Shader)")]
        [SerializeField] private Texture3D _worleyNoise128;
        [SerializeField] private Texture3D _erosionNoise32;
        [SerializeField] private Texture3D _perlinNoise32;
        [SerializeField] private bool _autoGenerateNoiseTextures = true;
        [SerializeField] private bool _autoAssignNoiseTextures = true;

        [Header("Volume Bounds")]
        [SerializeField] private Vector3 _volumeSize = new Vector3(150000f, 15000f, 150000f);
        [SerializeField] private Vector3 _volumeOffset = Vector3.zero;

        [Header("Quality")]
        [SerializeField] [Range(0f, 1f)] private float _qualityLevel = 1f;

        #endregion

        #region Private Fields

        private WeatherVolumeConfig _config;
        private WeatherVolumeData _currentData;
        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private bool _isInitialized = false;
        private bool _isVisible = true;
        private WeatherViewMode _currentViewMode = WeatherViewMode.Perspective3D;
        private RenderLayer _visibleLayers = RenderLayer.All;

        // Shader property IDs (cached for performance)
        private static readonly int _DensityVolumeID = Shader.PropertyToID("_DensityVolume");
        private static readonly int _VolumeMinID = Shader.PropertyToID("_VolumeMin");
        private static readonly int _VolumeMaxID = Shader.PropertyToID("_VolumeMax");
        private static readonly int _VolumeSizeID = Shader.PropertyToID("_VolumeSize");
        private static readonly int _VolumeCenterID = Shader.PropertyToID("_VolumeCenter");
        private static readonly int _RaymarchStepsID = Shader.PropertyToID("_RaymarchSteps");
        private static readonly int _StepSizeID = Shader.PropertyToID("_StepSize");
        private static readonly int _JitterAmountID = Shader.PropertyToID("_JitterAmount");
        private static readonly int _CloudDensityID = Shader.PropertyToID("_CloudDensity");
        private static readonly int _DetailScaleID = Shader.PropertyToID("_DetailScale");
        private static readonly int _DetailStrengthID = Shader.PropertyToID("_DetailStrength");
        private static readonly int _EdgeSoftnessID = Shader.PropertyToID("_EdgeSoftness");
        private static readonly int _AnimationSpeedID = Shader.PropertyToID("_AnimationSpeed");
        private static readonly int _LightDirID = Shader.PropertyToID("_LightDir");
        private static readonly int _LightColorID = Shader.PropertyToID("_LightColor");
        private static readonly int _AmbientColorID = Shader.PropertyToID("_AmbientColor");
        private static readonly int _LightAbsorptionID = Shader.PropertyToID("_LightAbsorption");
        private static readonly int _ForwardScatteringID = Shader.PropertyToID("_ForwardScattering");
        private static readonly int _MultiScatterStrengthID = Shader.PropertyToID("_MultiScatterStrength");
        private static readonly int _ShadowStepsID = Shader.PropertyToID("_ShadowSteps");
        private static readonly int _SelfShadowingID = Shader.PropertyToID("_SelfShadowing");
        private static readonly int _LightColorWeatherID = Shader.PropertyToID("_LightColor_Weather");
        private static readonly int _ModerateColorID = Shader.PropertyToID("_ModerateColor");
        private static readonly int _HeavyColorID = Shader.PropertyToID("_HeavyColor");
        private static readonly int _IntenseColorID = Shader.PropertyToID("_IntenseColor");
        private static readonly int _ExtremeColorID = Shader.PropertyToID("_ExtremeColor");
        private static readonly int _StormCoreColorID = Shader.PropertyToID("_StormCoreColor");
        private static readonly int _EarlyTerminationID = Shader.PropertyToID("_EarlyTerminationThreshold");

        // Enhanced shader property IDs
        private static readonly int _ShapeScaleID = Shader.PropertyToID("_ShapeScale");
        private static readonly int _ErosionScaleID = Shader.PropertyToID("_ErosionScale");
        private static readonly int _ShapeStrengthID = Shader.PropertyToID("_ShapeStrength");
        private static readonly int _ErosionStrengthID = Shader.PropertyToID("_ErosionStrength");
        private static readonly int _CloudBaseHeightID = Shader.PropertyToID("_CloudBaseHeight");
        private static readonly int _CloudTopHeightID = Shader.PropertyToID("_CloudTopHeight");
        private static readonly int _BaseSoftnessID = Shader.PropertyToID("_BaseSoftness");
        private static readonly int _TopSoftnessID = Shader.PropertyToID("_TopSoftness");
        private static readonly int _AnvilAmountID = Shader.PropertyToID("_AnvilAmount");
        private static readonly int _WindSpeedID = Shader.PropertyToID("_WindSpeed");
        private static readonly int _WindDirectionID = Shader.PropertyToID("_WindDirection");
        private static readonly int _ShapeEvolutionID = Shader.PropertyToID("_ShapeEvolution");
        private static readonly int _ErosionEvolutionID = Shader.PropertyToID("_ErosionEvolution");
        private static readonly int _SilverLiningID = Shader.PropertyToID("_SilverLining");
        private static readonly int _ColorBlendID = Shader.PropertyToID("_ColorBlend");
        private static readonly int _SunIntensityID = Shader.PropertyToID("_SunIntensity");
        private static readonly int _AmbientIntensityID = Shader.PropertyToID("_AmbientIntensity");

        // New URP shader property IDs for 3D noise textures
        private static readonly int _WorleyNoiseID = Shader.PropertyToID("_WorleyNoise");
        private static readonly int _ErosionNoiseID = Shader.PropertyToID("_ErosionNoise");
        private static readonly int _PerlinNoiseID = Shader.PropertyToID("_PerlinNoise");
        private static readonly int _ShapeFactorID = Shader.PropertyToID("_ShapeFactor");
        private static readonly int _ErosionFactorID = Shader.PropertyToID("_ErosionFactor");

        #endregion

        #region IVolumetricRenderer Implementation

        public string RendererName => "Volumetric Cloud Volume";
        
        public bool IsVisible
        {
            get => _isVisible;
            set
            {
                _isVisible = value;
                if (_meshRenderer != null)
                    _meshRenderer.enabled = value && (_visibleLayers & RenderLayer.VolumetricClouds) != 0;
            }
        }

        public bool IsInitialized => _isInitialized;

        public float QualityLevel
        {
            get => _qualityLevel;
            set
            {
                _qualityLevel = Mathf.Clamp01(value);
                UpdateQualitySettings();
            }
        }

        public void Initialize(WeatherVolumeConfig config)
        {
            _config = config;

            // Get components
            _meshFilter = GetComponent<MeshFilter>();
            _meshRenderer = GetComponent<MeshRenderer>();

            // Create cube mesh if not present
            if (_meshFilter.sharedMesh == null)
            {
                _meshFilter.sharedMesh = CreateCubeMesh();
            }

            // Create or get material
            InitializeMaterial();

            // Apply initial settings
            ApplyConfigToMaterial();

            _isInitialized = true;
            Debug.Log("[VolumetricCloudVolume] Initialized");
        }

        public void UpdateData(WeatherVolumeData data)
        {
            if (!_isInitialized || data == null) return;

            _currentData = data;

            // Update volume bounds
            _volumeSize = data.WorldBounds.size;
            transform.position = data.WorldBounds.center;
            transform.localScale = _volumeSize;

            // Update shader with new data
            if (_cloudMaterial != null && data.DensityVolume != null)
            {
                _cloudMaterial.SetTexture(_DensityVolumeID, data.DensityVolume);
                
                // Update bounds
                Vector3 min = data.WorldBounds.min;
                Vector3 max = data.WorldBounds.max;
                _cloudMaterial.SetVector(_VolumeMinID, min);
                _cloudMaterial.SetVector(_VolumeMaxID, max);
                _cloudMaterial.SetVector(_VolumeSizeID, data.WorldBounds.size);
                _cloudMaterial.SetVector(_VolumeCenterID, data.WorldBounds.center);
            }
        }

        public void SetViewMode(WeatherViewMode mode)
        {
            _currentViewMode = mode;

            // Adjust rendering based on view mode
            switch (mode)
            {
                case WeatherViewMode.PlanView:
                    // Flatten the rendering for top-down view
                    // Could adjust camera or shader parameters
                    break;
                case WeatherViewMode.ProfileView:
                    // Side view
                    break;
                case WeatherViewMode.Perspective3D:
                case WeatherViewMode.CockpitView:
                    // Full 3D rendering
                    break;
            }
        }

        public void Cleanup()
        {
            if (_cloudMaterial != null && Application.isPlaying)
            {
                Destroy(_cloudMaterial);
                _cloudMaterial = null;
            }
            _isInitialized = false;
        }

        public void Refresh()
        {
            if (_currentData != null)
            {
                UpdateData(_currentData);
            }
        }

        #endregion

        #region ILayeredRenderer Implementation

        public void SetLayerVisible(RenderLayer layer, bool visible)
        {
            if (visible)
                _visibleLayers |= layer;
            else
                _visibleLayers &= ~layer;

            // Update mesh renderer visibility
            if (_meshRenderer != null)
            {
                _meshRenderer.enabled = _isVisible && (_visibleLayers & RenderLayer.VolumetricClouds) != 0;
            }
        }

        public bool IsLayerVisible(RenderLayer layer)
        {
            return (_visibleLayers & layer) != 0;
        }

        #endregion

        #region Material Setup

        private void InitializeMaterial()
        {
            // Get the mesh renderer
            if (_meshRenderer == null)
                _meshRenderer = GetComponent<MeshRenderer>();

            // Find or create shader
            if (_cloudShader == null)
            {
                // Auto-detect render pipeline
                bool isSRP = UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline != null;

                string shaderName;

                // Priority: SRP shader > URP shader > Enhanced shader > Original shader
                if (_useURPCloudShader)
                {
                    if (isSRP)
                    {
                        shaderName = "WeatherVisualization3D/WeatherCloudVolumeSRP";
                        _cloudShader = Shader.Find(shaderName);
                    }
                    else
                    {
                        shaderName = "WeatherVisualization3D/WeatherCloudVolume";
                        _cloudShader = Shader.Find(shaderName);
                    }

                    if (_cloudShader == null)
                    {
                        Debug.LogWarning("[VolumetricCloudVolume] URP/SRP WeatherCloudVolume shader not found, trying enhanced shader.");
                        _useURPCloudShader = false;
                    }
                }

                if (_cloudShader == null)
                {
                    shaderName = _useEnhancedShader
                        ? "WeatherVisualization3D/VolumetricCloudEnhanced"
                        : "WeatherVisualization3D/VolumetricCloud";

                    _cloudShader = Shader.Find(shaderName);

                    // Fallback to original if enhanced not found
                    if (_cloudShader == null && _useEnhancedShader)
                    {
                        Debug.LogWarning("[VolumetricCloudVolume] Enhanced shader not found, falling back to original.");
                        _cloudShader = Shader.Find("WeatherVisualization3D/VolumetricCloud");
                    }
                }

                if (_cloudShader == null)
                {
                    Debug.LogError($"[VolumetricCloudVolume] Could not find any cloud shader!");
                    return;
                }
            }

            // Initialize noise textures if using URP shader
            if (_useURPCloudShader && _autoAssignNoiseTextures)
            {
                InitializeNoiseTextures();
            }

            // Always create a new material instance to ensure it gets the shader
            if (_cloudMaterial != null)
            {
                if (Application.isPlaying)
                    Destroy(_cloudMaterial);
                else
                    DestroyImmediate(_cloudMaterial);
            }

            _cloudMaterial = new Material(_cloudShader);
            _cloudMaterial.name = "VolumetricCloudMaterial_Instance";
            _meshRenderer.material = _cloudMaterial;
            _meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _meshRenderer.receiveShadows = false;

            Debug.Log($"[VolumetricCloudVolume] Initialized material with shader: {_cloudShader.name}");

            // Create material instance
            if (_cloudMaterial == null)
            {
                _cloudMaterial = new Material(_cloudShader);
                _cloudMaterial.name = "VolumetricCloudMaterial (Instance)";
            }

            _meshRenderer.material = _cloudMaterial;
            _meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _meshRenderer.receiveShadows = false;
        }

        /// <summary>
        /// Initialize 3D noise textures for the URP cloud shader
        /// </summary>
        private void InitializeNoiseTextures()
        {
            // Try to find existing noise texture assets
            if (_worleyNoise128 == null)
            {
                _worleyNoise128 = Resources.Load<Texture3D>("CloudNoise/WorleyNoise128RGBA_3D");
#if UNITY_EDITOR
                if (_worleyNoise128 == null)
                {
                    _worleyNoise128 = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture3D>(
                        "Assets/_Project/Textures/CloudNoise/WorleyNoise128RGBA_3D.asset");
                }
#endif
            }

            if (_erosionNoise32 == null)
            {
                _erosionNoise32 = Resources.Load<Texture3D>("CloudNoise/WorleyNoise32RGB_3D");
#if UNITY_EDITOR
                if (_erosionNoise32 == null)
                {
                    _erosionNoise32 = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture3D>(
                        "Assets/_Project/Textures/CloudNoise/WorleyNoise32RGB_3D.asset");
                }
#endif
            }

            if (_perlinNoise32 == null)
            {
                _perlinNoise32 = Resources.Load<Texture3D>("CloudNoise/PerlinNoise32RGB_3D");
#if UNITY_EDITOR
                if (_perlinNoise32 == null)
                {
                    _perlinNoise32 = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture3D>(
                        "Assets/_Project/Textures/CloudNoise/PerlinNoise32RGB_3D.asset");
                }
#endif
            }

            // Auto-generate if not found and enabled
#if UNITY_EDITOR
            if (_autoGenerateNoiseTextures && (_worleyNoise128 == null || _erosionNoise32 == null))
            {
                GenerateNoiseTexturesRuntime();
            }
#endif

            // Apply to material
            if (_cloudMaterial != null)
            {
                if (_worleyNoise128 != null)
                    _cloudMaterial.SetTexture(_WorleyNoiseID, _worleyNoise128);
                if (_erosionNoise32 != null)
                    _cloudMaterial.SetTexture(_ErosionNoiseID, _erosionNoise32);
                if (_perlinNoise32 != null)
                    _cloudMaterial.SetTexture(_PerlinNoiseID, _perlinNoise32);

                Debug.Log("[VolumetricCloudVolume] Applied 3D noise textures to material.");
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// Generate noise textures at runtime using the CloudNoiseTextureGenerator
        /// </summary>
        [ContextMenu("Generate Noise Textures")]
        public void GenerateNoiseTexturesRuntime()
        {
            var generator = gameObject.GetComponent<CloudNoiseTextureGenerator>();
            if (generator == null)
            {
                generator = gameObject.AddComponent<CloudNoiseTextureGenerator>();
            }

            generator.GenerateAllTextures();

            // Get the generated textures
            _worleyNoise128 = generator.worleyNoise128;
            _erosionNoise32 = generator.erosionNoise32;
            _perlinNoise32 = generator.perlinNoise32;

            // Apply to material
            if (_cloudMaterial != null)
            {
                if (_worleyNoise128 != null)
                    _cloudMaterial.SetTexture(_WorleyNoiseID, _worleyNoise128);
                if (_erosionNoise32 != null)
                    _cloudMaterial.SetTexture(_ErosionNoiseID, _erosionNoise32);
                if (_perlinNoise32 != null)
                    _cloudMaterial.SetTexture(_PerlinNoiseID, _perlinNoise32);
            }

            Debug.Log("[VolumetricCloudVolume] Generated and applied runtime noise textures.");
        }
#endif

        private void ApplyConfigToMaterial()
        {
            if (_cloudMaterial == null || _config == null) return;

            // URP shader uses different settings
            if (_useURPCloudShader)
            {
                ApplyURPShaderSettings();
                return;
            }

            // Raymarching settings
            int steps = Mathf.RoundToInt(_config.raymarchSteps * _qualityLevel);
            steps = Mathf.Max(32, steps);
            _cloudMaterial.SetInt(_RaymarchStepsID, steps);
            _cloudMaterial.SetFloat(_StepSizeID, _config.stepSizeMultiplier * 100f);
            _cloudMaterial.SetFloat(_JitterAmountID, _config.jitterAmount);
            _cloudMaterial.SetFloat(_EarlyTerminationID, _config.earlyTerminationThreshold);

            // Cloud appearance
            _cloudMaterial.SetFloat(_CloudDensityID, _config.cloudDensity);
            _cloudMaterial.SetFloat(_DetailScaleID, _config.detailScale);
            _cloudMaterial.SetFloat(_DetailStrengthID, _config.detailStrength);
            _cloudMaterial.SetFloat(_EdgeSoftnessID, _config.edgeSoftness);
            _cloudMaterial.SetFloat(_AnimationSpeedID, _config.animateClouds ? _config.animationSpeed : 0f);

            // Lighting
            Vector3 lightDir = RenderSettings.sun != null
                ? -RenderSettings.sun.transform.forward
                : new Vector3(0.5f, 1f, 0.5f).normalized;
            _cloudMaterial.SetVector(_LightDirID, lightDir);
            _cloudMaterial.SetColor(_LightColorID, _config.sunColor);
            _cloudMaterial.SetColor(_AmbientColorID, _config.ambientColor);
            _cloudMaterial.SetFloat(_LightAbsorptionID, _config.lightAbsorption);
            _cloudMaterial.SetFloat(_ForwardScatteringID, _config.forwardScattering);
            _cloudMaterial.SetFloat(_MultiScatterStrengthID, _config.multiScatterStrength);
            _cloudMaterial.SetInt(_ShadowStepsID, _config.selfShadowing ? _config.shadowSteps : 0);
            _cloudMaterial.SetFloat(_SelfShadowingID, _config.selfShadowing ? 1f : 0f);

            // Weather colors
            _cloudMaterial.SetColor(_LightColorWeatherID, _config.lightColor);
            _cloudMaterial.SetColor(_ModerateColorID, _config.moderateColor);
            _cloudMaterial.SetColor(_HeavyColorID, _config.heavyColor);
            _cloudMaterial.SetColor(_IntenseColorID, _config.intenseColor);
            _cloudMaterial.SetColor(_ExtremeColorID, _config.extremeColor);
            _cloudMaterial.SetColor(_StormCoreColorID, _config.stormCoreColor);

            // Enhanced shader settings
            if (_useEnhancedShader)
            {
                ApplyEnhancedShaderSettings();
            }
        }

        /// <summary>
        /// Apply settings specific to the URP WeatherCloudVolume shader
        /// </summary>
        private void ApplyURPShaderSettings()
        {
            if (_cloudMaterial == null || _config == null) return;

            // Volume bounds
            Vector3 min = transform.position - _volumeSize * 0.5f + _volumeOffset;
            Vector3 max = transform.position + _volumeSize * 0.5f + _volumeOffset;
            _cloudMaterial.SetVector(_VolumeMinID, min);
            _cloudMaterial.SetVector(_VolumeMaxID, max);

            // Raymarching settings
            int steps = Mathf.RoundToInt(_config.raymarchSteps * _qualityLevel);
            steps = Mathf.Max(24, Mathf.Min(steps, 96));
            _cloudMaterial.SetInt(_RaymarchStepsID, steps);
            _cloudMaterial.SetInt(Shader.PropertyToID("_LightSteps"), _config.selfShadowing ? _config.shadowSteps : 0);
            _cloudMaterial.SetFloat(_StepSizeID, _config.stepSizeMultiplier * 500f);
            _cloudMaterial.SetFloat(_JitterAmountID, _config.jitterAmount);

            // Cloud shape
            _cloudMaterial.SetFloat(_ShapeScaleID, _config.shapeScale);
            _cloudMaterial.SetFloat(_ShapeFactorID, _config.shapeStrength);
            _cloudMaterial.SetFloat(_ErosionScaleID, _config.erosionScale);
            _cloudMaterial.SetFloat(_ErosionFactorID, _config.erosionStrength);
            _cloudMaterial.SetFloat(Shader.PropertyToID("_DensityMultiplier"), _config.cloudDensity * 2f);

            // Height gradient
            _cloudMaterial.SetFloat(_CloudBaseHeightID, _config.cloudBaseHeight);
            _cloudMaterial.SetFloat(_CloudTopHeightID, _config.cloudTopHeight);
            _cloudMaterial.SetFloat(_BaseSoftnessID, _config.baseSoftness);
            _cloudMaterial.SetFloat(_TopSoftnessID, _config.topSoftness);

            // Animation
            _cloudMaterial.SetFloat(_WindSpeedID, _config.windSpeed);
            _cloudMaterial.SetVector(_WindDirectionID, _config.windDirection.normalized);
            _cloudMaterial.SetFloat(_ShapeEvolutionID, _config.shapeEvolution);

            // Lighting
            Vector3 lightDir = RenderSettings.sun != null
                ? -RenderSettings.sun.transform.forward
                : new Vector3(0.5f, 1f, 0.5f).normalized;
            _cloudMaterial.SetVector(_LightDirID, lightDir);
            _cloudMaterial.SetColor(_LightColorID, _config.sunColor);
            _cloudMaterial.SetFloat(_LightAbsorptionID, _config.lightAbsorption);
            _cloudMaterial.SetFloat(Shader.PropertyToID("_Scattering"), _config.forwardScattering);
            _cloudMaterial.SetFloat(_SilverLiningID, _config.silverLining);
            _cloudMaterial.SetFloat(_AmbientIntensityID, _config.ambientIntensity);
            _cloudMaterial.SetFloat(_SunIntensityID, 1.5f);

            // Weather colors
            _cloudMaterial.SetColor(_LightColorWeatherID, _config.lightColor);
            _cloudMaterial.SetColor(_ModerateColorID, _config.moderateColor);
            _cloudMaterial.SetColor(_HeavyColorID, _config.heavyColor);
            _cloudMaterial.SetColor(_IntenseColorID, _config.intenseColor);
            _cloudMaterial.SetColor(_ExtremeColorID, _config.extremeColor);
            _cloudMaterial.SetFloat(_ColorBlendID, _config.colorBlend);
        }

        private void ApplyEnhancedShaderSettings()
        {
            if (_cloudMaterial == null || _config == null) return;

            _cloudMaterial.SetFloat(_ShapeScaleID, _config.shapeScale);
            _cloudMaterial.SetFloat(_ErosionScaleID, _config.erosionScale);
            _cloudMaterial.SetFloat(_ShapeStrengthID, _config.shapeStrength);
            _cloudMaterial.SetFloat(_ErosionStrengthID, _config.erosionStrength);
            _cloudMaterial.SetFloat(_CloudBaseHeightID, _config.cloudBaseHeight);
            _cloudMaterial.SetFloat(_CloudTopHeightID, _config.cloudTopHeight);
            _cloudMaterial.SetFloat(_BaseSoftnessID, _config.baseSoftness);
            _cloudMaterial.SetFloat(_TopSoftnessID, _config.topSoftness);
            _cloudMaterial.SetFloat(_AnvilAmountID, _config.anvilAmount);
            _cloudMaterial.SetFloat(_WindSpeedID, _config.windSpeed);
            _cloudMaterial.SetVector(_WindDirectionID, _config.windDirection.normalized);
            _cloudMaterial.SetFloat(_ShapeEvolutionID, _config.shapeEvolution);
            _cloudMaterial.SetFloat(_ErosionEvolutionID, _config.erosionEvolution);
            _cloudMaterial.SetFloat(_SilverLiningID, _config.silverLining);
            _cloudMaterial.SetFloat(_ColorBlendID, _config.colorBlend);
            _cloudMaterial.SetFloat(_SunIntensityID, 1.5f);
            _cloudMaterial.SetFloat(_AmbientIntensityID, _config.ambientIntensity);
            _cloudMaterial.SetColor(_StormCoreColorID, _config.stormCoreColor);
        }

        private void UpdateQualitySettings()
        {
            if (_cloudMaterial == null || _config == null) return;

            int steps = Mathf.RoundToInt(_config.raymarchSteps * _qualityLevel);
            steps = Mathf.Max(32, steps);
            _cloudMaterial.SetInt(_RaymarchStepsID, steps);

            int shadowSteps = _config.selfShadowing ? Mathf.RoundToInt(_config.shadowSteps * _qualityLevel) : 0;
            shadowSteps = Mathf.Max(2, shadowSteps);
            _cloudMaterial.SetInt(_ShadowStepsID, shadowSteps);
        }

        #endregion

        #region Mesh Generation

        /// <summary>
        /// Create a unit cube mesh for the volume
        /// </summary>
        private Mesh CreateCubeMesh()
        {
            Mesh mesh = new Mesh();
            mesh.name = "VolumetricVolumeCube";

            // Vertices
            Vector3[] vertices = new Vector3[]
            {
                // Front face
                new Vector3(-0.5f, -0.5f, 0.5f),
                new Vector3(0.5f, -0.5f, 0.5f),
                new Vector3(0.5f, 0.5f, 0.5f),
                new Vector3(-0.5f, 0.5f, 0.5f),
                // Back face
                new Vector3(0.5f, -0.5f, -0.5f),
                new Vector3(-0.5f, -0.5f, -0.5f),
                new Vector3(-0.5f, 0.5f, -0.5f),
                new Vector3(0.5f, 0.5f, -0.5f),
                // Top face
                new Vector3(-0.5f, 0.5f, 0.5f),
                new Vector3(0.5f, 0.5f, 0.5f),
                new Vector3(0.5f, 0.5f, -0.5f),
                new Vector3(-0.5f, 0.5f, -0.5f),
                // Bottom face
                new Vector3(-0.5f, -0.5f, -0.5f),
                new Vector3(0.5f, -0.5f, -0.5f),
                new Vector3(0.5f, -0.5f, 0.5f),
                new Vector3(-0.5f, -0.5f, 0.5f),
                // Right face
                new Vector3(0.5f, -0.5f, 0.5f),
                new Vector3(0.5f, -0.5f, -0.5f),
                new Vector3(0.5f, 0.5f, -0.5f),
                new Vector3(0.5f, 0.5f, 0.5f),
                // Left face
                new Vector3(-0.5f, -0.5f, -0.5f),
                new Vector3(-0.5f, -0.5f, 0.5f),
                new Vector3(-0.5f, 0.5f, 0.5f),
                new Vector3(-0.5f, 0.5f, -0.5f)
            };

            // Triangles (CCW winding for back-face culling - we cull front faces)
            int[] triangles = new int[]
            {
                // Front
                0, 2, 1, 0, 3, 2,
                // Back
                4, 6, 5, 4, 7, 6,
                // Top
                8, 10, 9, 8, 11, 10,
                // Bottom
                12, 14, 13, 12, 15, 14,
                // Right
                16, 18, 17, 16, 19, 18,
                // Left
                20, 22, 21, 20, 23, 22
            };

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return mesh;
        }

        #endregion

        #region Unity Lifecycle

        private void Update()
        {
            if (!_isInitialized || _cloudMaterial == null) return;

            // Update light direction if sun moves
            if (RenderSettings.sun != null)
            {
                Vector3 lightDir = -RenderSettings.sun.transform.forward;
                _cloudMaterial.SetVector(_LightDirID, lightDir);
            }
        }

        private void OnDestroy()
        {
            Cleanup();
        }

        private void OnValidate()
        {
            // Update material when inspector values change
            if (_isInitialized && _config != null)
            {
                ApplyConfigToMaterial();
            }
        }

        private void OnRenderObject()
        {
            // Render in Scene view when not playing
            if (!Application.isPlaying && _cloudMaterial != null && _meshFilter != null)
            {
                // Only render if visible in scene view
                if (_meshRenderer != null && !_meshRenderer.enabled)
                    return;

                _cloudMaterial.SetPass(0);
                Graphics.DrawMeshNow(_meshFilter.sharedMesh, transform.localToWorldMatrix);
            }
        }

        #endregion

        #region Debug

        private void OnDrawGizmosSelected()
        {
            // Draw volume bounds
            Gizmos.color = new Color(0f, 1f, 0.5f, 0.5f);

            if (_currentData != null)
            {
                Gizmos.DrawWireCube(_currentData.WorldBounds.center, _currentData.WorldBounds.size);
            }
            else
            {
                Gizmos.DrawWireCube(transform.position, _volumeSize);
            }
        }

        private void OnDrawGizmos()
        {
            // Always draw preview in Scene view
            if (!Application.isPlaying)
            {
                DrawPreviewClouds();
            }
        }

        private void DrawPreviewClouds()
        {
            // Simple preview using gizmos
            int samples = 50;
            float voxelSize = Mathf.Min(_volumeSize.x, _volumeSize.y, _volumeSize.z) * 0.015f;

            for (int i = 0; i < samples; i++)
            {
                // Pseudo-random positions
                float nx = Mathf.Sin(i * 1.618f) * 0.5f + 0.5f;
                float ny = Mathf.Sin(i * 2.618f) * 0.5f + 0.5f;
                float nz = Mathf.Sin(i * 4.236f) * 0.5f + 0.5f;

                // Storm cell centers
                Vector3 cell1 = new Vector3(0.3f, 0.4f, 0.3f);
                Vector3 cell2 = new Vector3(0.7f, 0.5f, 0.6f);
                Vector3 cell3 = new Vector3(0.5f, 0.35f, 0.5f);

                float dist1 = Vector3.Distance(new Vector3(nx, ny, nz), cell1);
                float dist2 = Vector3.Distance(new Vector3(nx, ny, nz), cell2);
                float dist3 = Vector3.Distance(new Vector3(nx, ny, nz), cell3);

                float density = Mathf.Max(
                    Mathf.Max(Mathf.Clamp01(1f - dist1 * 2.5f), Mathf.Clamp01(1f - dist2 * 2.5f)),
                    Mathf.Clamp01(1f - dist3 * 2f)
                );

                // Boost density for visibility
                density = Mathf.Pow(density, 0.7f) * 1.2f;

                if (density > 0.15f)
                {
                    Vector3 pos = transform.position + new Vector3(
                        (nx - 0.5f) * _volumeSize.x,
                        (ny - 0.5f) * _volumeSize.y,
                        (nz - 0.5f) * _volumeSize.z
                    );

                    // Color based on density - aviation weather colors
                    Color color;
                    if (density < 0.3f)
                        color = new Color(0.2f, 0.9f, 0.2f, density * 0.6f);      // Green - Light
                    else if (density < 0.5f)
                        color = new Color(1f, 0.95f, 0.1f, density * 0.7f);       // Yellow - Moderate
                    else if (density < 0.7f)
                        color = new Color(1f, 0.5f, 0.1f, density * 0.8f);        // Orange - Heavy
                    else
                        color = new Color(1f, 0.15f, 0.15f, density * 0.9f);      // Red - Intense

                    Gizmos.color = color;
                    Gizmos.DrawSphere(pos, voxelSize * (0.5f + density * 0.5f));
                }
            }
        }

        #endregion
    }
}
