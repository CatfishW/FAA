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
    public class VolumetricCloudVolume : MonoBehaviour, IVolumetricRenderer, ILayeredRenderer
    {
        #region Serialized Fields

        [Header("Rendering")]
        [SerializeField] private Material _cloudMaterial;
        [SerializeField] private Shader _cloudShader;

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
        private static readonly int _TimeID = Shader.PropertyToID("_Time");
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
            // Find or create shader
            if (_cloudShader == null)
            {
                _cloudShader = Shader.Find("WeatherVisualization3D/VolumetricCloud");
                if (_cloudShader == null)
                {
                    Debug.LogError("[VolumetricCloudVolume] Could not find VolumetricCloud shader!");
                    return;
                }
            }

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

        private void ApplyConfigToMaterial()
        {
            if (_cloudMaterial == null || _config == null) return;

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

            // Update time for animation
            if (_config != null && _config.animateClouds)
            {
                _cloudMaterial.SetFloat(_TimeID, Time.time);
            }

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

        #endregion
    }
}
