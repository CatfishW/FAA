using UnityEngine;
using System.Collections.Generic;

namespace WeatherRadar.Weather3D
{
    /// <summary>
    /// Procedural lightning bolt generator.
    /// Creates realistic branching lightning bolts with animation.
    /// </summary>
    public class LightningRenderer : MonoBehaviour
    {
        [Header("Lightning Settings")]
        [SerializeField] private int mainBoltSegments = 10;
        [SerializeField] private int branchCount = 3;
        [SerializeField] private int branchSegments = 5;
        [SerializeField] private float boltWidth = 0.3f;
        [SerializeField] private float branchWidth = 0.15f;
        [SerializeField] private float jaggedness = 0.3f;
        
        [Header("Visual Settings")]
        [SerializeField] private Color boltColor = new Color(0.9f, 0.95f, 1f, 1f);
        [SerializeField] private Color glowColor = new Color(0.7f, 0.8f, 1f, 0.5f);
        [SerializeField] private float flashDuration = 0.2f;
        
        [Header("Materials")]
        [SerializeField] private Material lightningMaterial;

        // State
        private LineRenderer mainBolt;
        private List<LineRenderer> branches = new List<LineRenderer>();
        private Light flashLight;
        private bool isActive = false;
        private float activeStartTime;

        #region Initialization

        private void Awake()
        {
            CreateMainBolt();
            CreateBranches();
            CreateFlashLight();
            
            // Start inactive
            SetBoltActive(false);
        }

        private void CreateMainBolt()
        {
            GameObject obj = new GameObject("MainBolt");
            obj.transform.SetParent(transform);
            obj.transform.localPosition = Vector3.zero;
            
            mainBolt = obj.AddComponent<LineRenderer>();
            ConfigureLineRenderer(mainBolt, boltWidth);
        }

        private void CreateBranches()
        {
            for (int i = 0; i < branchCount; i++)
            {
                GameObject obj = new GameObject($"Branch_{i}");
                obj.transform.SetParent(transform);
                obj.transform.localPosition = Vector3.zero;
                
                LineRenderer branch = obj.AddComponent<LineRenderer>();
                ConfigureLineRenderer(branch, branchWidth);
                branches.Add(branch);
            }
        }

        private void ConfigureLineRenderer(LineRenderer lr, float width)
        {
            if (lightningMaterial == null)
            {
                lightningMaterial = new Material(Shader.Find("Sprites/Default"));
            }
            
            lr.material = new Material(lightningMaterial);
            lr.startWidth = width;
            lr.endWidth = width * 0.3f;
            lr.startColor = boltColor;
            lr.endColor = boltColor;
            lr.textureMode = LineTextureMode.Stretch;
            lr.numCornerVertices = 0;
            lr.numCapVertices = 0;
        }

        private void CreateFlashLight()
        {
            GameObject obj = new GameObject("FlashLight");
            obj.transform.SetParent(transform);
            obj.transform.localPosition = Vector3.zero;
            
            flashLight = obj.AddComponent<Light>();
            flashLight.type = LightType.Point;
            flashLight.color = boltColor;
            flashLight.intensity = 0f;
            flashLight.range = 100f;
            flashLight.shadows = LightShadows.None;
        }

        #endregion

        #region Update

        private void Update()
        {
            if (!isActive) return;
            
            float elapsed = Time.time - activeStartTime;
            
            if (elapsed >= flashDuration)
            {
                SetBoltActive(false);
            }
            else
            {
                // Fade out
                float alpha = 1f - (elapsed / flashDuration);
                UpdateBoltAlpha(alpha);
                
                // Flash light decay
                if (flashLight != null)
                {
                    flashLight.intensity = alpha * 5f;
                }
            }
        }

        private void UpdateBoltAlpha(float alpha)
        {
            Color color = boltColor;
            color.a = alpha;
            
            if (mainBolt != null)
            {
                mainBolt.startColor = color;
                mainBolt.endColor = color;
            }
            
            foreach (var branch in branches)
            {
                if (branch != null)
                {
                    branch.startColor = color;
                    branch.endColor = color;
                }
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Generate and display a lightning bolt between two points
        /// </summary>
        public void Strike(Vector3 start, Vector3 end)
        {
            // Generate main bolt path
            Vector3[] mainPath = GenerateBoltPath(start, end, mainBoltSegments, jaggedness);
            
            if (mainBolt != null)
            {
                mainBolt.positionCount = mainPath.Length;
                mainBolt.SetPositions(mainPath);
            }
            
            // Generate branches
            GenerateBranches(mainPath);
            
            // Position flash light
            if (flashLight != null)
            {
                flashLight.transform.position = (start + end) * 0.5f;
                flashLight.intensity = 5f;
            }
            
            // Activate
            SetBoltActive(true);
            activeStartTime = Time.time;
            isActive = true;
        }

        /// <summary>
        /// Generate a cloud-to-ground strike
        /// </summary>
        public void StrikeToGround(Vector3 cloudPosition, float groundY = 0f)
        {
            Vector3 groundPoint = new Vector3(
                cloudPosition.x + Random.Range(-10f, 10f),
                groundY,
                cloudPosition.z + Random.Range(-10f, 10f)
            );
            Strike(cloudPosition, groundPoint);
        }

        /// <summary>
        /// Generate a cloud-to-cloud strike
        /// </summary>
        public void CloudToCloud(Vector3 start, Vector3 end)
        {
            // Add some randomness to the end point
            Vector3 jitteredEnd = end + new Vector3(
                Random.Range(-5f, 5f),
                Random.Range(-2f, 2f),
                Random.Range(-5f, 5f)
            );
            Strike(start, jitteredEnd);
        }

        /// <summary>
        /// Set lightning color
        /// </summary>
        public void SetColor(Color color)
        {
            boltColor = color;
            
            if (mainBolt != null)
            {
                mainBolt.startColor = color;
                mainBolt.endColor = color;
            }
            
            foreach (var branch in branches)
            {
                if (branch != null)
                {
                    branch.startColor = color;
                    branch.endColor = color;
                }
            }
            
            if (flashLight != null)
            {
                flashLight.color = color;
            }
        }

        #endregion

        #region Private Methods

        private Vector3[] GenerateBoltPath(Vector3 start, Vector3 end, int segments, float jag)
        {
            Vector3[] points = new Vector3[segments];
            points[0] = start;
            points[segments - 1] = end;
            
            Vector3 direction = end - start;
            float length = direction.magnitude;
            float segmentLength = length / (segments - 1);
            
            // Get perpendicular vectors for offset
            Vector3 perpX = Vector3.Cross(direction.normalized, Vector3.up).normalized;
            if (perpX.magnitude < 0.1f)
            {
                perpX = Vector3.Cross(direction.normalized, Vector3.right).normalized;
            }
            Vector3 perpY = Vector3.Cross(direction.normalized, perpX).normalized;
            
            for (int i = 1; i < segments - 1; i++)
            {
                float t = (float)i / (segments - 1);
                Vector3 basePoint = Vector3.Lerp(start, end, t);
                
                // Offset decreases toward ends
                float offsetScale = Mathf.Sin(t * Mathf.PI) * jag * segmentLength;
                
                Vector3 offset = perpX * Random.Range(-offsetScale, offsetScale) +
                                perpY * Random.Range(-offsetScale, offsetScale);
                
                points[i] = basePoint + offset;
            }
            
            return points;
        }

        private void GenerateBranches(Vector3[] mainPath)
        {
            if (mainPath.Length < 3) return;
            
            for (int i = 0; i < branches.Count && i < branchCount; i++)
            {
                LineRenderer branch = branches[i];
                if (branch == null) continue;
                
                // Pick a random point along the main bolt (not start or end)
                int branchPoint = Random.Range(2, mainPath.Length - 2);
                Vector3 branchStart = mainPath[branchPoint];
                
                // Random direction, biased downward
                Vector3 branchDirection = new Vector3(
                    Random.Range(-1f, 1f),
                    Random.Range(-1f, 0.3f),
                    Random.Range(-1f, 1f)
                ).normalized;
                
                float branchLength = (mainPath[0] - mainPath[mainPath.Length - 1]).magnitude * Random.Range(0.2f, 0.5f);
                Vector3 branchEnd = branchStart + branchDirection * branchLength;
                
                Vector3[] branchPath = GenerateBoltPath(branchStart, branchEnd, branchSegments, jaggedness * 1.5f);
                
                branch.positionCount = branchPath.Length;
                branch.SetPositions(branchPath);
            }
        }

        private void SetBoltActive(bool active)
        {
            if (mainBolt != null)
            {
                mainBolt.enabled = active;
            }
            
            foreach (var branch in branches)
            {
                if (branch != null)
                {
                    branch.enabled = active;
                }
            }
            
            if (!active)
            {
                isActive = false;
                if (flashLight != null)
                {
                    flashLight.intensity = 0f;
                }
            }
        }

        #endregion
    }
}
