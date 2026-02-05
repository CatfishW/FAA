using UnityEngine;
using System.Collections.Generic;

namespace WeatherVisualization3D
{
    /// <summary>
    /// Renders vertical intensity pillars at storm cell locations.
    /// Pillars show the vertical extent and intensity of weather cells.
    /// </summary>
    public class IntensityPillarRenderer : MonoBehaviour, IVolumetricRenderer
    {
        #region Inspector Fields
        
        [Header("Pillar Settings")]
        [Tooltip("Material for rendering pillars (uses IntensityPillar shader)")]
        [SerializeField] private Material pillarMaterial;
        
        [Tooltip("Direct reference to IntensityPillar shader")]
        [SerializeField] private Shader pillarShader;
        
        [Tooltip("Number of segments around pillar circumference")]
        [Range(6, 32)]
        [SerializeField] private int cylinderSegments = 16;
        
        [Tooltip("Minimum pillar radius in world units")]
        [Range(100f, 2000f)]
        [SerializeField] private float minRadius = 500f;
        
        [Tooltip("Maximum pillar radius in world units")]
        [Range(500f, 5000f)]
        [SerializeField] private float maxRadius = 2000f;
        
        [Tooltip("Pillar opacity")]
        [Range(0f, 1f)]
        [SerializeField] private float pillarOpacity = 0.6f;
        
        [Header("Visual Effects")]
        [Tooltip("Animate pillar glow")]
        [SerializeField] private bool animateGlow = true;
        
        [Tooltip("Glow pulse speed")]
        [Range(0.1f, 5f)]
        [SerializeField] private float glowPulseSpeed = 1f;
        
        [Tooltip("Edge glow intensity")]
        [Range(0f, 2f)]
        [SerializeField] private float edgeGlowIntensity = 1f;
        
        [Header("Intensity Colors")]
        [SerializeField] private Color lightIntensityColor = new Color(0f, 1f, 0f, 0.5f);
        [SerializeField] private Color moderateIntensityColor = new Color(1f, 1f, 0f, 0.6f);
        [SerializeField] private Color heavyIntensityColor = new Color(1f, 0.5f, 0f, 0.7f);
        [SerializeField] private Color extremeIntensityColor = new Color(1f, 0f, 0.5f, 0.8f);
        
        [Header("References")]
        [Tooltip("Weather simulator to get cell data from")]
        [SerializeField] private WeatherSimulator weatherSimulator;
        
        [Header("Performance")]
        [Tooltip("Maximum number of pillars to render")]
        [Range(1, 50)]
        [SerializeField] private int maxPillars = 20;
        
        [Tooltip("Update frequency in Hz")]
        [Range(1f, 30f)]
        [SerializeField] private float updateFrequency = 10f;
        
        #endregion

        #region Private Fields
        
        private List<PillarInstance> pillarInstances = new List<PillarInstance>();
        private Mesh pillarMesh;
        private MaterialPropertyBlock propertyBlock;
        private float lastUpdateTime;
        private bool _isInitialized;
        private bool _isVisible = true;
        private float _qualityLevel = 0.5f;
        private WeatherViewMode _viewMode = WeatherViewMode.Perspective3D;
        
        // Shader property IDs
        private static readonly int ColorPropId = Shader.PropertyToID("_Color");
        private static readonly int IntensityPropId = Shader.PropertyToID("_Intensity");
        private static readonly int OpacityPropId = Shader.PropertyToID("_Opacity");
        private static readonly int GlowIntensityPropId = Shader.PropertyToID("_GlowIntensity");
        private static readonly int PulseSpeedPropId = Shader.PropertyToID("_PulseSpeed");
        
        #endregion

        #region IVolumetricRenderer Implementation
        
        public string RendererName => "Intensity Pillar Renderer";
        
        public bool IsVisible
        {
            get => _isVisible;
            set => _isVisible = value;
        }
        
        public bool IsInitialized => _isInitialized;
        
        public float QualityLevel
        {
            get => _qualityLevel;
            set
            {
                _qualityLevel = Mathf.Clamp01(value);
                AdjustQuality();
            }
        }
        
        public void Initialize(WeatherVolumeConfig config)
        {
            CreatePillarMesh();
            
            if (pillarMaterial == null)
            {
                CreateDefaultMaterial();
            }
            
            propertyBlock = new MaterialPropertyBlock();
            
            if (config != null)
            {
                lightIntensityColor = config.lightColor;
                moderateIntensityColor = config.moderateColor;
                heavyIntensityColor = config.heavyColor;
                extremeIntensityColor = config.extremeColor;
            }
            
            _isInitialized = true;
            Debug.Log("[IntensityPillarRenderer] Initialized");
        }
        
        public void UpdateData(WeatherVolumeData data)
        {
            // Pillars are updated from storm cells, not volume data directly
        }
        
        public void SetViewMode(WeatherViewMode mode)
        {
            _viewMode = mode;
        }
        
        public void Cleanup()
        {
            if (pillarMesh != null)
            {
                DestroyImmediate(pillarMesh);
                pillarMesh = null;
            }
            
            pillarInstances.Clear();
            _isInitialized = false;
        }
        
        public void Refresh()
        {
            if (weatherSimulator != null)
            {
                UpdatePillarsFromCells();
            }
        }
        
        #endregion

        #region Unity Lifecycle
        
        private void Awake()
        {
            if (!_isInitialized)
            {
                Initialize(null);
            }
        }
        
        private void OnEnable()
        {
            if (!_isInitialized)
            {
                Initialize(null);
            }
        }
        
        private void OnDestroy()
        {
            Cleanup();
        }
        
        private void Update()
        {
            if (!_isInitialized || !_isVisible || weatherSimulator == null)
                return;
            
            float updateInterval = 1f / updateFrequency;
            if (Time.time - lastUpdateTime >= updateInterval)
            {
                UpdatePillarsFromCells();
                lastUpdateTime = Time.time;
            }
        }
        
        private void LateUpdate()
        {
            if (!_isInitialized || !_isVisible)
                return;
            
            RenderPillars();
        }
        
        #endregion

        #region Pillar Management
        
        private void AdjustQuality()
        {
            if (_qualityLevel < 0.25f)
                cylinderSegments = 8;
            else if (_qualityLevel < 0.5f)
                cylinderSegments = 12;
            else if (_qualityLevel < 0.75f)
                cylinderSegments = 16;
            else
                cylinderSegments = 24;
            
            CreatePillarMesh();
        }
        
        private void CreatePillarMesh()
        {
            if (pillarMesh != null)
            {
                DestroyImmediate(pillarMesh);
            }
            
            pillarMesh = CreateCylinderMesh(1f, 1f, cylinderSegments);
            pillarMesh.name = "IntensityPillarMesh";
        }
        
        private Mesh CreateCylinderMesh(float radius, float height, int segments)
        {
            Mesh mesh = new Mesh();
            
            int vertexCount = segments * 2 + 2;
            Vector3[] vertices = new Vector3[vertexCount];
            Vector3[] normals = new Vector3[vertexCount];
            Vector2[] uvs = new Vector2[vertexCount];
            
            for (int i = 0; i < segments; i++)
            {
                float angle = (float)i / segments * Mathf.PI * 2f;
                float x = Mathf.Cos(angle) * radius;
                float z = Mathf.Sin(angle) * radius;
                
                vertices[i] = new Vector3(x, 0f, z);
                normals[i] = new Vector3(x, 0f, z).normalized;
                uvs[i] = new Vector2((float)i / segments, 0f);
                
                vertices[segments + i] = new Vector3(x, height, z);
                normals[segments + i] = new Vector3(x, 0f, z).normalized;
                uvs[segments + i] = new Vector2((float)i / segments, 1f);
            }
            
            vertices[segments * 2] = new Vector3(0f, 0f, 0f);
            normals[segments * 2] = Vector3.down;
            uvs[segments * 2] = new Vector2(0.5f, 0.5f);
            
            vertices[segments * 2 + 1] = new Vector3(0f, height, 0f);
            normals[segments * 2 + 1] = Vector3.up;
            uvs[segments * 2 + 1] = new Vector2(0.5f, 0.5f);
            
            int triangleCount = segments * 2 * 3 + segments * 2 * 3;
            int[] triangles = new int[triangleCount];
            int tri = 0;
            
            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;
                
                triangles[tri++] = i;
                triangles[tri++] = segments + i;
                triangles[tri++] = segments + next;
                
                triangles[tri++] = i;
                triangles[tri++] = segments + next;
                triangles[tri++] = next;
            }
            
            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;
                triangles[tri++] = segments * 2;
                triangles[tri++] = next;
                triangles[tri++] = i;
            }
            
            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;
                triangles[tri++] = segments * 2 + 1;
                triangles[tri++] = segments + i;
                triangles[tri++] = segments + next;
            }
            
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            
            return mesh;
        }

        private void OnValidate()
        {
            if (pillarShader == null)
            {
                FindShader();
            }
        }
        
        [ContextMenu("Find Shader")]
        private void FindShader()
        {
            if (pillarShader == null)
            {
                pillarShader = Shader.Find("IntensityPillar");
            }
        }

        private void CreateDefaultMaterial()
        {
            if (pillarShader == null)
            {
                FindShader();
            }

            Shader shader = pillarShader;
            
            if (shader == null)
            {
                Debug.LogWarning("[IntensityPillarRenderer] IntensityPillar shader not found, using fallback");
                shader = Shader.Find("Standard");
            }
            
            pillarMaterial = new Material(shader);
            pillarMaterial.name = "IntensityPillarMaterial";
            
            if (shader.name.Contains("IntensityPillar"))
            {
                // Set default values for weather visualization shader
                pillarMaterial.SetColor("_Color", new Color(1f, 1f, 1f, 0.4f));
                pillarMaterial.SetFloat("_Intensity", 0.5f);
                pillarMaterial.SetFloat("_Opacity", 0.5f);
                pillarMaterial.SetFloat("_TopFade", 0.8f);
                pillarMaterial.SetFloat("_BottomFade", 0.2f);
                pillarMaterial.SetFloat("_EdgeFalloff", 1.5f);
                pillarMaterial.SetFloat("_PulseSpeed", 1.0f);
                pillarMaterial.SetFloat("_PulseAmount", 0.1f);
                pillarMaterial.SetFloat("_VerticalWaveSpeed", 0.5f);
                pillarMaterial.SetFloat("_VerticalWaveScale", 3f);
                pillarMaterial.SetFloat("_FresnelPower", 2.0f);
                pillarMaterial.SetFloat("_FresnelIntensity", 0.3f);
                pillarMaterial.SetFloat("_InnerGlow", 0.2f);
                pillarMaterial.SetFloat("_GlowIntensity", 0.5f);
                pillarMaterial.SetFloat("_NoiseScale", 0.01f);
                pillarMaterial.SetFloat("_NoiseStrength", 0.3f);
                pillarMaterial.SetFloat("_NoiseSpeed", 0.2f);
                
                // Set weather colors (Aviation standard)
                pillarMaterial.SetColor("_LightColor", new Color(0.2f, 0.9f, 0.2f, 0.4f));
                pillarMaterial.SetColor("_ModerateColor", new Color(0.95f, 0.9f, 0.2f, 0.5f));
                pillarMaterial.SetColor("_HeavyColor", new Color(1f, 0.5f, 0.1f, 0.6f));
                pillarMaterial.SetColor("_IntenseColor", new Color(0.95f, 0.15f, 0.1f, 0.7f));
                pillarMaterial.SetColor("_ExtremeColor", new Color(0.95f, 0.2f, 0.8f, 0.8f));
            }
            else
            {
                // Fallback to Standard shader
                pillarMaterial.SetFloat("_Mode", 3);
                pillarMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                pillarMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                pillarMaterial.SetInt("_ZWrite", 0);
                pillarMaterial.DisableKeyword("_ALPHATEST_ON");
                pillarMaterial.EnableKeyword("_ALPHABLEND_ON");
                pillarMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                pillarMaterial.renderQueue = 3000;
            }
        }
        
        private void UpdatePillarsFromCells()
        {
            var cells = weatherSimulator.GetActiveCells();
            
            while (pillarInstances.Count < cells.Count && pillarInstances.Count < maxPillars)
            {
                pillarInstances.Add(new PillarInstance());
            }
            
            int pillarIndex = 0;
            foreach (var cell in cells)
            {
                if (pillarIndex >= maxPillars)
                    break;
                
                if (!cell.IsActive || cell.Radius <= 0)
                    continue;
                
                var pillar = pillarInstances[pillarIndex];
                pillar.IsActive = true;
                pillar.Position = new Vector3(cell.Position.x, cell.BaseAltitude, cell.Position.y);
                pillar.Height = cell.TopAltitude - cell.BaseAltitude;
                pillar.Radius = Mathf.Lerp(minRadius, maxRadius, cell.NormalizedIntensity);
                pillar.Intensity = cell.Intensity;
                pillar.Opacity = cell.Opacity * pillarOpacity;
                pillar.Color = GetColorForIntensity(cell.Intensity);
                pillar.Color.a = pillar.Opacity;
                
                pillarIndex++;
            }
            
            for (int i = pillarIndex; i < pillarInstances.Count; i++)
            {
                pillarInstances[i].IsActive = false;
            }
        }
        
        private Color GetColorForIntensity(IntensityLevel intensity)
        {
            return intensity switch
            {
                IntensityLevel.Light => lightIntensityColor,
                IntensityLevel.Moderate => moderateIntensityColor,
                IntensityLevel.Heavy => heavyIntensityColor,
                IntensityLevel.Extreme => extremeIntensityColor,
                _ => Color.gray
            };
        }
        
        private void RenderPillars()
        {
            if (pillarMesh == null || pillarMaterial == null)
                return;
            
            float time = Time.time;
            
            foreach (var pillar in pillarInstances)
            {
                if (!pillar.IsActive)
                    continue;
                
                Matrix4x4 matrix = Matrix4x4.TRS(
                    pillar.Position,
                    Quaternion.identity,
                    new Vector3(pillar.Radius, pillar.Height, pillar.Radius)
                );
                
                propertyBlock.SetColor(ColorPropId, pillar.Color);
                propertyBlock.SetFloat(IntensityPropId, (float)pillar.Intensity / 4f);
                propertyBlock.SetFloat(OpacityPropId, pillar.Opacity);
                propertyBlock.SetFloat(GlowIntensityPropId, edgeGlowIntensity);
                
                if (animateGlow)
                {
                    propertyBlock.SetFloat(PulseSpeedPropId, glowPulseSpeed);
                }
                
                Graphics.DrawMesh(
                    pillarMesh,
                    matrix,
                    pillarMaterial,
                    gameObject.layer,
                    null,
                    0,
                    propertyBlock
                );
            }
        }
        
        #endregion

        #region Public API
        
        public void SetWeatherSimulator(WeatherSimulator simulator)
        {
            weatherSimulator = simulator;
        }
        
        public void SetOpacity(float opacity)
        {
            pillarOpacity = Mathf.Clamp01(opacity);
        }
        
        public void SetAnimateGlow(bool animate)
        {
            animateGlow = animate;
        }
        
        #endregion

        #region Nested Types
        
        private class PillarInstance
        {
            public bool IsActive;
            public Vector3 Position;
            public float Height;
            public float Radius;
            public IntensityLevel Intensity;
            public float Opacity;
            public Color Color;
        }
        
        #endregion
    }
}
