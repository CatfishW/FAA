using UnityEngine;
using System.Collections.Generic;

namespace WeatherRadar.Weather3D
{
    /// <summary>
    /// Visualizes turbulence zones with color-coded severity indicators
    /// and animated boundary effects to show air disturbance areas.
    /// </summary>
    public class TurbulenceIndicator : MonoBehaviour
    {
        [Header("Materials")]
        [SerializeField] private Material turbulenceZoneMaterial;
        [SerializeField] private Material turbulenceBoundaryMaterial;
        
        [Header("Particle System")]
        [SerializeField] private ParticleSystem turbulenceParticles;
        
        // Configuration
        private Weather3DConfig config;
        private Weather3DManager manager;
        
        // Active zones
        private List<TurbulenceZoneVisual> activeZones = new List<TurbulenceZoneVisual>();
        
        // State
        private bool isVisible = true;
        private Weather3DViewMode currentViewMode = Weather3DViewMode.Perspective3D;
        
        // Object pools
        private Queue<MeshRenderer> zonePool = new Queue<MeshRenderer>();
        private Queue<LineRenderer> boundaryPool = new Queue<LineRenderer>();

        #region Initialization

        public void Initialize(Weather3DConfig config, Weather3DManager manager)
        {
            this.config = config;
            this.manager = manager;
            
            CreateMaterials();
            CreateParticleSystem();
            
            Debug.Log("[TurbulenceIndicator] Initialized");
        }

        private void CreateMaterials()
        {
            if (turbulenceZoneMaterial == null)
            {
                // Use Standard shader for better visual quality with emission
                turbulenceZoneMaterial = new Material(Shader.Find("Standard"));
                Weather3DShaderHelpers.SetMaterialToFade(turbulenceZoneMaterial);
                turbulenceZoneMaterial.color = new Color(1f, 0.7f, 0.2f, 0.35f);
                
                // Enable emission for glow effect
                turbulenceZoneMaterial.EnableKeyword("_EMISSION");
                turbulenceZoneMaterial.SetColor("_EmissionColor", new Color(1f, 0.6f, 0.1f) * 0.3f);
            }
            
            if (turbulenceBoundaryMaterial == null)
            {
                turbulenceBoundaryMaterial = new Material(Shader.Find("Standard"));
                Weather3DShaderHelpers.SetMaterialToFade(turbulenceBoundaryMaterial);
                turbulenceBoundaryMaterial.color = new Color(1f, 0.9f, 0.3f, 0.9f);
                
                // Bright emission for boundary visibility
                turbulenceBoundaryMaterial.EnableKeyword("_EMISSION");
                turbulenceBoundaryMaterial.SetColor("_EmissionColor", new Color(1f, 0.85f, 0.2f) * 0.6f);
            }
        }

        private void CreateParticleSystem()
        {
            if (turbulenceParticles == null)
            {
                GameObject psObj = new GameObject("TurbulenceParticles");
                psObj.transform.SetParent(transform);
                turbulenceParticles = psObj.AddComponent<ParticleSystem>();
                
                var main = turbulenceParticles.main;
                main.loop = true;
                main.startLifetime = 2f;
                main.startSpeed = 0f;
                main.maxParticles = 500;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.startSize = 0.5f;
                main.startColor = new Color(1f, 0.7f, 0.3f, 0.5f);
                
                var emission = turbulenceParticles.emission;
                emission.rateOverTime = 0;
                
                var noise = turbulenceParticles.noise;
                noise.enabled = true;
                noise.strength = 2f;
                noise.frequency = 2f;
                noise.scrollSpeed = 1f;
                noise.damping = true;
                
                var renderer = turbulenceParticles.GetComponent<ParticleSystemRenderer>();
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                renderer.material = new Material(Shader.Find("Particles/Standard Unlit"));
            }
        }

        #endregion

        #region Update Methods

        private void Update()
        {
            if (!isVisible || config == null) return;
            
            if (config.animateTurbulence)
            {
                AnimateTurbulenceZones();
            }
        }

        /// <summary>
        /// Update turbulence visualization with new weather data
        /// </summary>
        public void UpdateTurbulence(Weather3DData data)
        {
            if (data == null) return;
            
            // Return unused visuals to pool
            ClearActiveZones();
            
            // Find cells with significant turbulence
            foreach (var cell in data.weatherCells)
            {
                if (cell.turbulenceLevel > 0.1f)
                {
                    CreateTurbulenceZone(cell);
                }
            }
            
            // Update particle emission based on active zones
            UpdateParticleEmission();
        }

        private void CreateTurbulenceZone(WeatherCell3D cell)
        {
            // Get enhanced severity color using shader helpers
            Color zoneColor = Weather3DShaderHelpers.GetTurbulenceColor(cell.turbulenceLevel);
            
            // Create zone visual
            MeshRenderer zoneRenderer = GetOrCreateZoneRenderer();
            
            // Position and scale
            Vector3 center = cell.position;
            zoneRenderer.transform.position = center;
            zoneRenderer.transform.localScale = cell.size;
            
            // Set color with transparency and emission
            zoneColor.a = config.turbulenceOpacity * Mathf.Lerp(0.5f, 1f, cell.turbulenceLevel);
            zoneRenderer.material.color = zoneColor;
            
            // Set emission based on severity
            Color emissionColor = zoneColor;
            emissionColor.a = 1f;
            float emissionStrength = Mathf.Lerp(0.2f, 0.8f, cell.turbulenceLevel);
            zoneRenderer.material.SetColor("_EmissionColor", emissionColor * emissionStrength);
            
            // Create boundary with glow
            LineRenderer boundary = GetOrCreateBoundaryRenderer();
            CreateTurbulenceBoundary(boundary, cell, zoneColor);
            
            activeZones.Add(new TurbulenceZoneVisual
            {
                zoneRenderer = zoneRenderer,
                boundaryRenderer = boundary,
                cell = cell,
                baseColor = zoneColor,
                animationOffset = Random.value * 100f
            });
        }

        private MeshRenderer GetOrCreateZoneRenderer()
        {
            MeshRenderer renderer;
            
            if (zonePool.Count > 0)
            {
                renderer = zonePool.Dequeue();
                renderer.gameObject.SetActive(true);
            }
            else
            {
                GameObject obj = new GameObject("TurbulenceZone");
                obj.transform.SetParent(transform);
                
                MeshFilter filter = obj.AddComponent<MeshFilter>();
                filter.mesh = CreateSphereMesh(12);
                
                renderer = obj.AddComponent<MeshRenderer>();
                renderer.material = new Material(turbulenceZoneMaterial);
            }
            
            return renderer;
        }

        private LineRenderer GetOrCreateBoundaryRenderer()
        {
            LineRenderer renderer;
            
            if (boundaryPool.Count > 0)
            {
                renderer = boundaryPool.Dequeue();
                renderer.gameObject.SetActive(true);
            }
            else
            {
                GameObject obj = new GameObject("TurbulenceBoundary");
                obj.transform.SetParent(transform);
                
                renderer = obj.AddComponent<LineRenderer>();
                renderer.material = new Material(turbulenceBoundaryMaterial);
                renderer.loop = true;
                renderer.startWidth = 0.2f;
                renderer.endWidth = 0.2f;
            }
            
            return renderer;
        }

        private void CreateTurbulenceBoundary(LineRenderer boundary, WeatherCell3D cell, Color color)
        {
            int segments = 24;
            boundary.positionCount = segments;
            
            Vector3 center = cell.position;
            float radiusX = cell.size.x * 0.5f;
            float radiusZ = cell.size.z * 0.5f;
            
            for (int i = 0; i < segments; i++)
            {
                float angle = (float)i / segments * Mathf.PI * 2f;
                Vector3 point = center + new Vector3(
                    Mathf.Cos(angle) * radiusX,
                    0f,
                    Mathf.Sin(angle) * radiusZ
                );
                boundary.SetPosition(i, point);
            }
            
            boundary.startColor = color;
            boundary.endColor = color;
        }

        private Mesh CreateSphereMesh(int subdivisions)
        {
            // Simple icosphere for turbulence zones
            Mesh mesh = new Mesh();
            
            // Use Unity's built-in sphere mesh as base
            GameObject tempSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            mesh = Instantiate(tempSphere.GetComponent<MeshFilter>().sharedMesh);
            DestroyImmediate(tempSphere);
            
            return mesh;
        }

        private Color GetTurbulenceColor(float level)
        {
            // Color coding based on turbulence severity
            if (level < 0.25f)
                return new Color(0.2f, 1f, 0.2f, 1f);    // Light - Green
            else if (level < 0.5f)
                return new Color(1f, 1f, 0.2f, 1f);      // Moderate - Yellow
            else if (level < 0.75f)
                return new Color(1f, 0.5f, 0.1f, 1f);    // Severe - Orange
            else
                return new Color(1f, 0.1f, 0.1f, 1f);    // Extreme - Red
        }

        private void AnimateTurbulenceZones()
        {
            float time = Time.time * config.turbulenceAnimFrequency;
            
            foreach (var zone in activeZones)
            {
                if (zone.zoneRenderer == null) continue;
                
                float zoneTime = time + zone.animationOffset;
                
                // Enhanced pulsing scale effect with breathing
                float pulse = Weather3DShaderHelpers.GetPulseAlpha(1f, 0.15f, config.turbulenceAnimFrequency * 0.5f, zone.animationOffset);
                zone.zoneRenderer.transform.localScale = zone.cell.size * pulse;
                
                // Boundary animation with more organic movement
                if (zone.boundaryRenderer != null)
                {
                    int count = zone.boundaryRenderer.positionCount;
                    for (int i = 0; i < count; i++)
                    {
                        Vector3 pos = zone.boundaryRenderer.GetPosition(i);
                        float noise = Mathf.PerlinNoise(
                            pos.x * 0.1f + zoneTime * 0.5f,
                            pos.z * 0.1f + zoneTime * 0.3f
                        ) - 0.5f;
                        float noise2 = Mathf.PerlinNoise(
                            pos.z * 0.15f + zoneTime * 0.4f,
                            pos.x * 0.12f
                        ) - 0.5f;
                        pos.y = zone.cell.position.y + (noise + noise2 * 0.5f) * zone.cell.size.y * 0.15f;
                        zone.boundaryRenderer.SetPosition(i, pos);
                    }
                    
                    // Animate boundary width based on severity
                    float widthPulse = 0.15f + 0.1f * Mathf.Sin(zoneTime * 2f);
                    widthPulse *= (1f + zone.cell.turbulenceLevel * 0.5f);
                    zone.boundaryRenderer.startWidth = widthPulse;
                    zone.boundaryRenderer.endWidth = widthPulse;
                }
                
                // Enhanced color and emission pulsing
                Color color = zone.baseColor;
                float alphaPulse = Weather3DShaderHelpers.GetPulseAlpha(0.8f, 0.2f, config.turbulenceAnimFrequency, zone.animationOffset);
                color.a = config.turbulenceOpacity * alphaPulse;
                zone.zoneRenderer.material.color = color;
                
                // Pulse emission intensity
                float emissionPulse = Weather3DShaderHelpers.GetPulseAlpha(0.4f, 0.3f, config.turbulenceAnimFrequency * 1.5f, zone.animationOffset);
                Color emissionColor = zone.baseColor;
                emissionColor.a = 1f;
                zone.zoneRenderer.material.SetColor("_EmissionColor", emissionColor * emissionPulse * zone.cell.turbulenceLevel);
            }
        }

        private void UpdateParticleEmission()
        {
            if (turbulenceParticles == null) return;
            
            float totalTurbulence = 0f;
            Vector3 averagePosition = Vector3.zero;
            
            foreach (var zone in activeZones)
            {
                totalTurbulence += zone.cell.turbulenceLevel;
                averagePosition += zone.cell.position;
            }
            
            if (activeZones.Count > 0)
            {
                averagePosition /= activeZones.Count;
                turbulenceParticles.transform.position = averagePosition;
                
                var emission = turbulenceParticles.emission;
                emission.rateOverTime = totalTurbulence * 50f;
                
                var shape = turbulenceParticles.shape;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 20f * Mathf.Sqrt(activeZones.Count);
            }
            else
            {
                var emission = turbulenceParticles.emission;
                emission.rateOverTime = 0;
            }
        }

        private void ClearActiveZones()
        {
            foreach (var zone in activeZones)
            {
                if (zone.zoneRenderer != null)
                {
                    zone.zoneRenderer.gameObject.SetActive(false);
                    zonePool.Enqueue(zone.zoneRenderer);
                }
                if (zone.boundaryRenderer != null)
                {
                    zone.boundaryRenderer.gameObject.SetActive(false);
                    boundaryPool.Enqueue(zone.boundaryRenderer);
                }
            }
            activeZones.Clear();
        }

        #endregion

        #region Public Methods

        public void SetVisible(bool visible)
        {
            isVisible = visible;
            
            foreach (var zone in activeZones)
            {
                if (zone.zoneRenderer != null)
                    zone.zoneRenderer.enabled = visible;
                if (zone.boundaryRenderer != null)
                    zone.boundaryRenderer.enabled = visible;
            }
            
            if (turbulenceParticles != null)
            {
                var renderer = turbulenceParticles.GetComponent<ParticleSystemRenderer>();
                if (renderer != null)
                    renderer.enabled = visible;
            }
        }

        public void SetViewMode(Weather3DViewMode mode)
        {
            currentViewMode = mode;
        }

        public void Clear()
        {
            ClearActiveZones();
            
            if (turbulenceParticles != null)
            {
                turbulenceParticles.Clear();
                var emission = turbulenceParticles.emission;
                emission.rateOverTime = 0;
            }
        }

        /// <summary>
        /// Get turbulence level at a specific world position
        /// </summary>
        public float GetTurbulenceAtPosition(Vector3 worldPos)
        {
            float maxTurbulence = 0f;
            
            foreach (var zone in activeZones)
            {
                Vector3 localPos = worldPos - zone.cell.position;
                Vector3 normalizedPos = new Vector3(
                    localPos.x / (zone.cell.size.x * 0.5f),
                    localPos.y / (zone.cell.size.y * 0.5f),
                    localPos.z / (zone.cell.size.z * 0.5f)
                );
                
                float distance = normalizedPos.magnitude;
                if (distance < 1f)
                {
                    float turbulence = zone.cell.turbulenceLevel * (1f - distance);
                    maxTurbulence = Mathf.Max(maxTurbulence, turbulence);
                }
            }
            
            return maxTurbulence;
        }

        #endregion

        #region Data Structures

        private struct TurbulenceZoneVisual
        {
            public MeshRenderer zoneRenderer;
            public LineRenderer boundaryRenderer;
            public WeatherCell3D cell;
            public Color baseColor;
            public float animationOffset;
        }

        #endregion
    }
}
