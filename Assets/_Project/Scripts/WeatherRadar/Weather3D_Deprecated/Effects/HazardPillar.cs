using UnityEngine;

namespace WeatherRadar.Weather3D
{
    /// <summary>
    /// Renders hazard pillars (3D columns) for severe weather cells.
    /// Provides clear vertical extent indication with distance labels.
    /// </summary>
    public class HazardPillar : MonoBehaviour
    {
        [Header("Visual Components")]
        [SerializeField] private MeshRenderer pillarRenderer;
        [SerializeField] private LineRenderer[] ringRenderers;
        [SerializeField] private TextMesh altitudeLabel;
        [SerializeField] private TextMesh distanceLabel;
        
        [Header("Settings")]
        [SerializeField] private int ringCount = 4;
        [SerializeField] private int ringSegments = 24;
        [SerializeField] private float pulseSpeed = 1.5f;
        [SerializeField] private float ringAnimationSpeed = 0.5f;

        // State
        private WeatherCell3D sourceCell;
        private Color baseColor;
        private float baseAltitude;
        private float topAltitude;
        private float animationOffset;
        private bool isVisible = true;

        #region Initialization

        private void Awake()
        {
            animationOffset = Random.value * Mathf.PI * 2f;
            
            if (pillarRenderer == null)
            {
                CreatePillarMesh();
            }
            
            if (ringRenderers == null || ringRenderers.Length == 0)
            {
                CreateRings();
            }
        }

        /// <summary>
        /// Initialize the hazard pillar with cell data
        /// </summary>
        public void Initialize(WeatherCell3D cell, Weather3DConfig config)
        {
            sourceCell = cell;
            baseAltitude = cell.altitude;
            topAltitude = cell.topAltitude;
            baseColor = config.GetIntensityColor(cell.intensity);
            
            // Position at cell center
            Vector3 center = cell.position;
            center.y = (baseAltitude + topAltitude) * 0.5f * 0.3048f;
            transform.position = center;
            
            // Scale to match altitude range
            float height = (topAltitude - baseAltitude) * 0.3048f;
            float radius = Mathf.Max(cell.size.x, cell.size.z) * 0.25f;
            transform.localScale = new Vector3(radius, height * 0.5f, radius);
            
            // Apply color
            if (pillarRenderer != null)
            {
                Color pillarColor = baseColor;
                pillarColor.a = 0.3f;
                pillarRenderer.material.color = pillarColor;
            }
            
            // Update labels
            UpdateLabels(cell);
            
            // Configure rings
            ConfigureRings(config);
        }

        private void CreatePillarMesh()
        {
            GameObject meshObj = new GameObject("PillarMesh");
            meshObj.transform.SetParent(transform);
            meshObj.transform.localPosition = Vector3.zero;
            meshObj.transform.localScale = Vector3.one;
            
            MeshFilter filter = meshObj.AddComponent<MeshFilter>();
            filter.mesh = CreateCylinderMesh(16);
            
            pillarRenderer = meshObj.AddComponent<MeshRenderer>();
            pillarRenderer.material = new Material(Shader.Find("Sprites/Default"));
            pillarRenderer.material.color = new Color(1f, 0f, 0f, 0.3f);
        }

        private void CreateRings()
        {
            ringRenderers = new LineRenderer[ringCount];
            
            for (int i = 0; i < ringCount; i++)
            {
                GameObject ringObj = new GameObject($"Ring_{i}");
                ringObj.transform.SetParent(transform);
                
                LineRenderer ring = ringObj.AddComponent<LineRenderer>();
                ring.loop = true;
                ring.positionCount = ringSegments;
                ring.startWidth = 0.1f;
                ring.endWidth = 0.1f;
                ring.material = new Material(Shader.Find("Sprites/Default"));
                
                ringRenderers[i] = ring;
            }
        }

        private Mesh CreateCylinderMesh(int segments)
        {
            Mesh mesh = new Mesh();
            
            int vertexCount = (segments + 1) * 2;
            Vector3[] vertices = new Vector3[vertexCount];
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

        private void ConfigureRings(Weather3DConfig config)
        {
            if (ringRenderers == null) return;
            
            Color ringColor = baseColor;
            ringColor.a = 0.8f;
            
            for (int i = 0; i < ringRenderers.Length; i++)
            {
                LineRenderer ring = ringRenderers[i];
                if (ring == null) continue;
                
                ring.startColor = ringColor;
                ring.endColor = ringColor;
                ring.startWidth = config.hazardPillarWidth * 0.1f;
                ring.endWidth = config.hazardPillarWidth * 0.1f;
            }
        }

        private void UpdateLabels(WeatherCell3D cell)
        {
            if (altitudeLabel != null)
            {
                altitudeLabel.text = $"FL{Mathf.RoundToInt(topAltitude / 100f)}";
            }
        }

        #endregion

        #region Update

        private void Update()
        {
            if (!isVisible) return;
            
            AnimatePulse();
            AnimateRings();
        }

        private void AnimatePulse()
        {
            float time = Time.time * pulseSpeed + animationOffset;
            float pulse = 0.8f + Mathf.Sin(time) * 0.2f;
            
            if (pillarRenderer != null)
            {
                Color color = baseColor;
                color.a = 0.3f * pulse;
                pillarRenderer.material.color = color;
            }
        }

        private void AnimateRings()
        {
            if (ringRenderers == null) return;
            
            float time = Time.time * ringAnimationSpeed;
            
            for (int i = 0; i < ringRenderers.Length; i++)
            {
                LineRenderer ring = ringRenderers[i];
                if (ring == null) continue;
                
                // Move rings upward, looping
                float normalizedHeight = (float)i / ringRenderers.Length;
                float animatedHeight = (normalizedHeight + time) % 1f;
                float worldY = Mathf.Lerp(-1f, 1f, animatedHeight);
                
                ring.transform.localPosition = new Vector3(0, worldY, 0);
                
                // Generate ring points
                for (int j = 0; j < ringSegments; j++)
                {
                    float angle = (float)j / ringSegments * Mathf.PI * 2f;
                    Vector3 point = new Vector3(
                        Mathf.Cos(angle),
                        0,
                        Mathf.Sin(angle)
                    );
                    ring.SetPosition(j, point);
                }
                
                // Fade alpha based on position
                Color color = baseColor;
                color.a = 0.8f * (1f - Mathf.Abs(animatedHeight - 0.5f) * 2f);
                ring.startColor = color;
                ring.endColor = color;
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Set pillar visibility
        /// </summary>
        public void SetVisible(bool visible)
        {
            isVisible = visible;
            
            if (pillarRenderer != null)
            {
                pillarRenderer.enabled = visible;
            }
            
            if (ringRenderers != null)
            {
                foreach (var ring in ringRenderers)
                {
                    if (ring != null)
                    {
                        ring.enabled = visible;
                    }
                }
            }
            
            if (altitudeLabel != null)
            {
                altitudeLabel.gameObject.SetActive(visible);
            }
            
            if (distanceLabel != null)
            {
                distanceLabel.gameObject.SetActive(visible);
            }
        }

        /// <summary>
        /// Update distance label based on aircraft position
        /// </summary>
        public void UpdateDistanceLabel(Vector3 aircraftPosition)
        {
            if (distanceLabel == null || sourceCell == null) return;
            
            float distance = Vector3.Distance(sourceCell.position, aircraftPosition);
            float distanceNM = distance / 1852f; // Meters to nautical miles
            
            distanceLabel.text = $"{distanceNM:F1}nm";
        }

        /// <summary>
        /// Get the threat level (0-1) of this pillar
        /// </summary>
        public float GetThreatLevel()
        {
            return sourceCell?.intensity ?? 0f;
        }

        #endregion
    }
}
