using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace WeatherRadar.Weather3D
{
    /// <summary>
    /// Renders thunderstorm cells with dramatic visual effects including
    /// towering cumulonimbus clouds, lightning bolts, and hazard pillars.
    /// </summary>
    public class ThunderstormCellRenderer : MonoBehaviour
    {
        [Header("Lightning Settings")]
        [SerializeField] private Material lightningMaterial;
        [SerializeField] private Color lightningColor = new Color(0.9f, 0.95f, 1f, 1f);
        
        [Header("Pillar Settings")]
        [SerializeField] private Material pillarMaterial;
        [SerializeField] private int pillarSegments = 16;
        
        [Header("Flash Settings")]
        [SerializeField] private Light flashLight;
        [SerializeField] private float flashIntensity = 3f;

        // Configuration
        private Weather3DConfig config;
        private Weather3DManager manager;
        
        // Active cells
        private List<ThunderstormCellData> activeCells = new List<ThunderstormCellData>();
        private List<LightningBolt> activeBolts = new List<LightningBolt>();
        private List<HazardPillar> activePillars = new List<HazardPillar>();
        
        // State
        private bool lightningVisible = true;
        private bool pillarsVisible = true;
        private Weather3DViewMode currentViewMode = Weather3DViewMode.Perspective3D;
        private float lastLightningTime;

        // Object pools
        private Queue<LineRenderer> boltPool = new Queue<LineRenderer>();
        private Queue<MeshRenderer> pillarPool = new Queue<MeshRenderer>();

        #region Initialization

        public void Initialize(Weather3DConfig config, Weather3DManager manager)
        {
            this.config = config;
            this.manager = manager;
            
            CreateFlashLight();
            CreateMaterials();
            
            Debug.Log("[ThunderstormCellRenderer] Initialized");
        }

        private void CreateFlashLight()
        {
            if (flashLight == null)
            {
                GameObject lightObj = new GameObject("LightningFlash");
                lightObj.transform.SetParent(transform);
                flashLight = lightObj.AddComponent<Light>();
                flashLight.type = LightType.Point;
                flashLight.color = lightningColor;
                flashLight.intensity = 0f;
                flashLight.range = 500f;
                flashLight.shadows = LightShadows.None;
            }
        }

        private void CreateMaterials()
        {
            if (lightningMaterial == null)
            {
                lightningMaterial = new Material(Shader.Find("Sprites/Default"));
                lightningMaterial.color = lightningColor;
            }
            
            if (pillarMaterial == null)
            {
                pillarMaterial = new Material(Shader.Find("Sprites/Default"));
                pillarMaterial.color = new Color(1f, 0f, 0f, 0.3f);
            }
        }

        #endregion

        #region Update Methods

        private void Update()
        {
            if (!lightningVisible || config == null) return;
            
            // Process lightning for active cells
            foreach (var cell in activeCells)
            {
                if (cell.hasLightning && Time.time >= cell.nextLightningTime)
                {
                    TriggerLightning(cell);
                    cell.nextLightningTime = Time.time + Random.Range(
                        config.lightningMinInterval,
                        config.lightningMaxInterval
                    ) / cell.intensity;
                }
            }
            
            // Update active bolts
            UpdateActiveBolts();
            
            // Update pillars
            UpdatePillars();
        }

        /// <summary>
        /// Update thunderstorm visualization with new weather data
        /// </summary>
        public void UpdateThunderstorms(Weather3DData data)
        {
            if (data == null) return;
            
            // Find thunderstorm cells
            activeCells.Clear();
            
            foreach (var cell in data.weatherCells)
            {
                if (cell.cellType == WeatherCellType.Thunderstorm || cell.intensity > 0.6f)
                {
                    activeCells.Add(new ThunderstormCellData
                    {
                        cell = cell,
                        hasLightning = cell.hasLightning || cell.intensity > 0.7f,
                        nextLightningTime = Time.time + Random.Range(0f, config.lightningMinInterval),
                        intensity = cell.intensity
                    });
                }
            }
            
            // Update hazard pillars
            UpdateHazardPillars();
        }

        #endregion

        #region Lightning Effects

        private void TriggerLightning(ThunderstormCellData cellData)
        {
            if (!config.enableLightning) return;
            
            // Create lightning bolt
            LightningBolt bolt = CreateLightningBolt(cellData.cell);
            activeBolts.Add(bolt);
            
            // Flash effect
            StartCoroutine(FlashCoroutine(cellData.cell.position));
            
            lastLightningTime = Time.time;
        }

        private LightningBolt CreateLightningBolt(WeatherCell3D cell)
        {
            // Get or create line renderer
            LineRenderer lineRenderer;
            if (boltPool.Count > 0)
            {
                lineRenderer = boltPool.Dequeue();
                lineRenderer.gameObject.SetActive(true);
            }
            else
            {
                GameObject boltObj = new GameObject("LightningBolt");
                boltObj.transform.SetParent(transform);
                lineRenderer = boltObj.AddComponent<LineRenderer>();
                lineRenderer.material = lightningMaterial;
                lineRenderer.startWidth = config.lightningWidth;
                lineRenderer.endWidth = config.lightningWidth * 0.5f;
                lineRenderer.startColor = config.lightningColor;
                lineRenderer.endColor = config.lightningColor;
            }
            
            // Generate bolt path
            int segments = config.lightningSegments;
            lineRenderer.positionCount = segments;
            
            Vector3 start = cell.position + Vector3.up * cell.size.y * 0.4f;
            Vector3 end = Random.value > 0.5f 
                ? cell.position + Vector3.up * cell.size.y * 0.1f  // Cloud to ground
                : cell.position + new Vector3(                     // Cloud to cloud
                    Random.Range(-cell.size.x, cell.size.x) * 0.3f,
                    Random.Range(-cell.size.y, cell.size.y) * 0.2f,
                    Random.Range(-cell.size.z, cell.size.z) * 0.3f
                );
            
            // Generate jagged path
            Vector3[] points = GenerateLightningPath(start, end, segments);
            lineRenderer.SetPositions(points);
            
            return new LightningBolt
            {
                renderer = lineRenderer,
                spawnTime = Time.time,
                duration = config.lightningFlashDuration
            };
        }

        private Vector3[] GenerateLightningPath(Vector3 start, Vector3 end, int segments)
        {
            Vector3[] points = new Vector3[segments];
            points[0] = start;
            points[segments - 1] = end;
            
            Vector3 direction = (end - start) / (segments - 1);
            float displacement = direction.magnitude * 0.5f;
            
            for (int i = 1; i < segments - 1; i++)
            {
                float t = (float)i / (segments - 1);
                Vector3 basePoint = Vector3.Lerp(start, end, t);
                
                // Add randomness that decreases toward the end
                float jitter = displacement * (1f - t * 0.5f);
                Vector3 offset = new Vector3(
                    Random.Range(-jitter, jitter),
                    Random.Range(-jitter * 0.5f, jitter * 0.5f),
                    Random.Range(-jitter, jitter)
                );
                
                points[i] = basePoint + offset;
            }
            
            return points;
        }

        private void UpdateActiveBolts()
        {
            for (int i = activeBolts.Count - 1; i >= 0; i--)
            {
                var bolt = activeBolts[i];
                float age = Time.time - bolt.spawnTime;
                
                if (age >= bolt.duration)
                {
                    // Return to pool
                    bolt.renderer.gameObject.SetActive(false);
                    boltPool.Enqueue(bolt.renderer);
                    activeBolts.RemoveAt(i);
                }
                else
                {
                    // Fade out
                    float alpha = 1f - (age / bolt.duration);
                    Color color = config.lightningColor;
                    color.a = alpha;
                    bolt.renderer.startColor = color;
                    bolt.renderer.endColor = color;
                }
            }
        }

        private IEnumerator FlashCoroutine(Vector3 position)
        {
            if (flashLight == null) yield break;
            
            flashLight.transform.position = position + Vector3.up * 10f;
            
            // Quick flash on
            flashLight.intensity = flashIntensity;
            yield return new WaitForSeconds(0.05f);
            
            // Quick fade
            float fadeTime = config.lightningFlashDuration - 0.05f;
            float elapsed = 0f;
            
            while (elapsed < fadeTime)
            {
                elapsed += Time.deltaTime;
                flashLight.intensity = Mathf.Lerp(flashIntensity, 0f, elapsed / fadeTime);
                yield return null;
            }
            
            flashLight.intensity = 0f;
        }

        #endregion

        #region Hazard Pillars

        private void UpdateHazardPillars()
        {
            // Return unused pillars to pool
            foreach (var pillar in activePillars)
            {
                if (pillar.renderer != null)
                {
                    pillar.renderer.gameObject.SetActive(false);
                    pillarPool.Enqueue(pillar.renderer);
                }
            }
            activePillars.Clear();
            
            // Create pillars for active cells
            foreach (var cellData in activeCells)
            {
                CreateHazardPillar(cellData.cell);
            }
        }

        private void CreateHazardPillar(WeatherCell3D cell)
        {
            // Get color based on intensity
            Color pillarColor = config.GetIntensityColor(cell.intensity);
            pillarColor.a = 0.4f;
            
            // Create or get pillar mesh
            MeshRenderer pillarRenderer;
            if (pillarPool.Count > 0)
            {
                pillarRenderer = pillarPool.Dequeue();
                pillarRenderer.gameObject.SetActive(true);
            }
            else
            {
                GameObject pillarObj = new GameObject("HazardPillar");
                pillarObj.transform.SetParent(transform);
                
                MeshFilter meshFilter = pillarObj.AddComponent<MeshFilter>();
                pillarRenderer = pillarObj.AddComponent<MeshRenderer>();
                
                meshFilter.mesh = CreateCylinderMesh(pillarSegments);
                pillarRenderer.material = new Material(pillarMaterial);
            }
            
            // Position and scale
            Vector3 center = cell.position;
            center.y = (cell.altitude + cell.topAltitude) * 0.5f * 0.3048f;
            pillarRenderer.transform.position = center;
            
            float height = (cell.topAltitude - cell.altitude) * 0.3048f;
            float radius = Mathf.Max(cell.size.x, cell.size.z) * 0.5f * 0.3f;
            pillarRenderer.transform.localScale = new Vector3(radius, height * 0.5f, radius);
            
            // Set color
            pillarRenderer.material.color = pillarColor;
            pillarRenderer.enabled = pillarsVisible;
            
            activePillars.Add(new HazardPillar
            {
                renderer = pillarRenderer,
                cell = cell,
                baseColor = pillarColor
            });
        }

        private Mesh CreateCylinderMesh(int segments)
        {
            Mesh mesh = new Mesh();
            
            Vector3[] vertices = new Vector3[(segments + 1) * 2];
            int[] triangles = new int[segments * 6];
            
            for (int i = 0; i <= segments; i++)
            {
                float angle = (float)i / segments * Mathf.PI * 2f;
                float x = Mathf.Cos(angle);
                float z = Mathf.Sin(angle);
                
                vertices[i] = new Vector3(x, -1f, z);
                vertices[i + segments + 1] = new Vector3(x, 1f, z);
            }
            
            for (int i = 0; i < segments; i++)
            {
                int ti = i * 6;
                triangles[ti] = i;
                triangles[ti + 1] = i + segments + 1;
                triangles[ti + 2] = i + 1;
                triangles[ti + 3] = i + 1;
                triangles[ti + 4] = i + segments + 1;
                triangles[ti + 5] = i + segments + 2;
            }
            
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            
            return mesh;
        }

        private void UpdatePillars()
        {
            if (!pillarsVisible) return;
            
            float time = Time.time * config.hazardPulseSpeed;
            
            foreach (var pillar in activePillars)
            {
                if (pillar.renderer == null) continue;
                
                // Pulsing effect
                float pulse = 0.8f + Mathf.Sin(time + pillar.cell.position.x * 0.01f) * 0.2f;
                Color color = pillar.baseColor;
                color.a *= pulse;
                pillar.renderer.material.color = color;
            }
        }

        #endregion

        #region Public Methods

        public void SetLightningVisible(bool visible)
        {
            lightningVisible = visible;
            
            if (!visible)
            {
                // Clear active bolts
                foreach (var bolt in activeBolts)
                {
                    bolt.renderer.gameObject.SetActive(false);
                    boltPool.Enqueue(bolt.renderer);
                }
                activeBolts.Clear();
                
                if (flashLight != null)
                {
                    flashLight.intensity = 0f;
                }
            }
        }

        public void SetPillarsVisible(bool visible)
        {
            pillarsVisible = visible;
            
            foreach (var pillar in activePillars)
            {
                if (pillar.renderer != null)
                {
                    pillar.renderer.enabled = visible;
                }
            }
        }

        public void SetViewMode(Weather3DViewMode mode)
        {
            currentViewMode = mode;
        }

        public void Clear()
        {
            // Clear bolts
            foreach (var bolt in activeBolts)
            {
                if (bolt.renderer != null)
                {
                    bolt.renderer.gameObject.SetActive(false);
                    boltPool.Enqueue(bolt.renderer);
                }
            }
            activeBolts.Clear();
            
            // Clear pillars
            foreach (var pillar in activePillars)
            {
                if (pillar.renderer != null)
                {
                    pillar.renderer.gameObject.SetActive(false);
                    pillarPool.Enqueue(pillar.renderer);
                }
            }
            activePillars.Clear();
            activeCells.Clear();
            
            if (flashLight != null)
            {
                flashLight.intensity = 0f;
            }
        }

        #endregion

        #region Data Structures

        private class ThunderstormCellData
        {
            public WeatherCell3D cell;
            public bool hasLightning;
            public float nextLightningTime;
            public float intensity;
        }

        private struct LightningBolt
        {
            public LineRenderer renderer;
            public float spawnTime;
            public float duration;
        }

        private struct HazardPillar
        {
            public MeshRenderer renderer;
            public WeatherCell3D cell;
            public Color baseColor;
        }

        #endregion
    }
}
