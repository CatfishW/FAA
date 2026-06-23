using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace WeatherVisualization3D
{
    /// <summary>
    /// Lightning bolt visual effects for storm cells.
    /// Generates procedural lightning bolts with branching and glow effects.
    /// </summary>
    [ExecuteInEditMode]
    public class VolumetricLightning : MonoBehaviour, IWeatherEffectRenderer
    {
        #region Inspector Fields
        
        [Header("Lightning Appearance")]
        [Tooltip("Main lightning bolt color")]
        [SerializeField] private Color boltColor = new Color(0.8f, 0.9f, 1f, 1f);
        
        [Tooltip("Core glow color")]
        [SerializeField] private Color glowColor = new Color(0.6f, 0.7f, 1f, 0.5f);
        
        [Tooltip("Lightning bolt width")]
        [Range(10f, 200f)]
        [SerializeField] private float boltWidth = 50f;
        
        [Tooltip("Glow width multiplier")]
        [Range(1f, 5f)]
        [SerializeField] private float glowWidthMultiplier = 3f;
        
        [Header("Bolt Generation")]
        [Tooltip("Number of segments per bolt")]
        [Range(5, 30)]
        [SerializeField] private int segmentsPerBolt = 15;
        
        [Tooltip("Maximum deviation per segment")]
        [Range(100f, 2000f)]
        [SerializeField] private float maxSegmentDeviation = 500f;
        
        [Tooltip("Probability of branching at each segment")]
        [Range(0f, 0.5f)]
        [SerializeField] private float branchProbability = 0.2f;
        
        [Tooltip("Maximum branch depth")]
        [Range(0, 3)]
        [SerializeField] private int maxBranchDepth = 2;
        
        [Tooltip("Branch length multiplier (relative to remaining main bolt)")]
        [Range(0.2f, 0.8f)]
        [SerializeField] private float branchLengthMultiplier = 0.4f;
        
        [Header("Timing")]
        [Tooltip("Lightning flash duration in seconds")]
        [Range(0.05f, 0.5f)]
        [SerializeField] private float flashDuration = 0.15f;
        
        [Tooltip("Afterglow duration in seconds")]
        [Range(0.1f, 1f)]
        [SerializeField] private float afterglowDuration = 0.3f;
        
        [Tooltip("Minimum time between strikes in same cell")]
        [Range(0.5f, 5f)]
        [SerializeField] private float minStrikeInterval = 1f;
        
        [Header("References")]
        [Tooltip("Weather simulator to get cell data")]
        [SerializeField] private WeatherSimulator weatherSimulator;
        
        [Tooltip("Line renderer material")]
        [SerializeField] private Material lightningMaterial;
        
        [Header("Performance")]
        [Tooltip("Maximum simultaneous lightning bolts")]
        [Range(1, 20)]
        [SerializeField] private int maxActiveBolts = 10;
        
        [Tooltip("Object pool size")]
        [Range(5, 30)]
        [SerializeField] private int poolSize = 15;
        
        #endregion

        #region Private Fields
        
        private List<LightningBolt> activeBolts = new List<LightningBolt>();
        private Queue<LightningBolt> boltPool = new Queue<LightningBolt>();
        private Dictionary<string, float> lastStrikeTime = new Dictionary<string, float>();
        private bool isInitialized;
        private float effectIntensity = 1f;
        private bool effectEnabled = true;
        
        #endregion

        #region IWeatherEffectRenderer Implementation
        
        public string EffectName => "Volumetric Lightning";
        
        public bool IsActive
        {
            get => enabled && effectEnabled && gameObject.activeInHierarchy;
            set => effectEnabled = value;
        }
        
        public float IntensityMultiplier
        {
            get => effectIntensity;
            set => effectIntensity = Mathf.Clamp01(value);
        }
        
        public void Initialize(WeatherVolumeConfig config)
        {
            CreateBoltPool();
            
            if (lightningMaterial == null)
            {
                CreateDefaultMaterial();
            }
            
            isInitialized = true;
            Debug.Log("[VolumetricLightning] Initialized");
        }
        
        public void UpdateEffect(WeatherVolumeData data)
        {
            // Effect updates are handled in Update() via the weather simulator
            // This method is called by the manager when volume data changes
        }
        
        public void Cleanup()
        {
            Disable();
            
            // Destroy pool objects
            while (boltPool.Count > 0)
            {
                var bolt = boltPool.Dequeue();
                if (bolt.LineRenderer != null)
                {
#if UNITY_EDITOR
                    if (!Application.isPlaying)
                        DestroyImmediate(bolt.LineRenderer.gameObject);
                    else
#endif
                        Destroy(bolt.LineRenderer.gameObject);
                }
                if (bolt.GlowRenderer != null)
                {
#if UNITY_EDITOR
                    if (!Application.isPlaying)
                        DestroyImmediate(bolt.GlowRenderer.gameObject);
                    else
#endif
                        Destroy(bolt.GlowRenderer.gameObject);
                }
            }
            
            isInitialized = false;
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
            if (!isInitialized)
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
            if (!isInitialized || !effectEnabled || weatherSimulator == null)
                return;
            
            // Check for new lightning strikes
            CheckForNewStrikes();
            
            // Update active bolts
            UpdateActiveBolts();
        }
        
        #endregion

        #region Lightning Generation
        
        private void CreateBoltPool()
        {
            for (int i = 0; i < poolSize; i++)
            {
                var bolt = CreateNewBolt();
                bolt.LineRenderer.gameObject.SetActive(false);
                boltPool.Enqueue(bolt);
            }
        }
        
        private LightningBolt CreateNewBolt()
        {
            // Create main bolt renderer
            GameObject mainObj = new GameObject("LightningBolt");
            mainObj.transform.SetParent(transform);
            
            LineRenderer mainLine = mainObj.AddComponent<LineRenderer>();
            mainLine.material = lightningMaterial;
            mainLine.startWidth = boltWidth;
            mainLine.endWidth = boltWidth * 0.3f;
            mainLine.startColor = boltColor;
            mainLine.endColor = boltColor;
            mainLine.numCapVertices = 3;
            mainLine.numCornerVertices = 3;
            mainLine.useWorldSpace = true;
            mainLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mainLine.receiveShadows = false;
            
            // Create glow renderer
            GameObject glowObj = new GameObject("LightningGlow");
            glowObj.transform.SetParent(mainObj.transform);
            
            LineRenderer glowLine = glowObj.AddComponent<LineRenderer>();
            glowLine.material = lightningMaterial;
            glowLine.startWidth = boltWidth * glowWidthMultiplier;
            glowLine.endWidth = boltWidth * 0.3f * glowWidthMultiplier;
            glowLine.startColor = glowColor;
            glowLine.endColor = glowColor;
            glowLine.numCapVertices = 3;
            glowLine.numCornerVertices = 3;
            glowLine.useWorldSpace = true;
            glowLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            glowLine.receiveShadows = false;
            
            return new LightningBolt
            {
                LineRenderer = mainLine,
                GlowRenderer = glowLine,
                BranchRenderers = new List<LineRenderer>()
            };
        }
        
        private void CreateDefaultMaterial()
        {
            // Create additive material for lightning
            Shader shader = Shader.Find("Particles/Standard Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
                Debug.Log("[VolumetricLightning] Using fallback Sprites/Default shader");
            }

            lightningMaterial = new Material(shader);
            lightningMaterial.name = "LightningMaterial";
            lightningMaterial.SetColor("_Color", Color.white);

            // Set to additive blending
            lightningMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            lightningMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
            lightningMaterial.SetInt("_ZWrite", 0);
            lightningMaterial.renderQueue = 3100;

#if UNITY_EDITOR
            // Create persistent material asset in editor
            string path = "Assets/_Project/Materials/WeatherVisualization/LightningMaterial.mat";
            string dir = System.IO.Path.GetDirectoryName(path);
            if (!System.IO.Directory.Exists(dir))
            {
                System.IO.Directory.CreateDirectory(dir);
            }
            UnityEditor.AssetDatabase.CreateAsset(lightningMaterial, path);
            UnityEditor.AssetDatabase.SaveAssets();
            Debug.Log($"[VolumetricLightning] Created lightning material asset at: {path}");
#endif
        }
        
        private LightningBolt GetBoltFromPool()
        {
            if (boltPool.Count > 0)
            {
                var bolt = boltPool.Dequeue();
                bolt.LineRenderer.gameObject.SetActive(true);
                return bolt;
            }
            
            // Create new if pool empty
            return CreateNewBolt();
        }
        
        private void ReturnBoltToPool(LightningBolt bolt)
        {
            bolt.LineRenderer.gameObject.SetActive(false);
            bolt.LineRenderer.positionCount = 0;
            bolt.GlowRenderer.positionCount = 0;
            
            // Clear branches
            foreach (var branch in bolt.BranchRenderers)
            {
                if (branch != null)
                {
#if UNITY_EDITOR
                    if (!Application.isPlaying)
                        DestroyImmediate(branch.gameObject);
                    else
#endif
                        Destroy(branch.gameObject);
                }
            }
            bolt.BranchRenderers.Clear();
            
            boltPool.Enqueue(bolt);
        }
        
        private void CheckForNewStrikes()
        {
            if (activeBolts.Count >= maxActiveBolts)
                return;
            
            var cells = weatherSimulator.GetActiveCells();
            
            foreach (var cell in cells)
            {
                // Only lightning for cells with activity
                if (cell.LightningActivity <= 0 || !cell.IsActive)
                    continue;
                
                // Check strike interval
                if (lastStrikeTime.TryGetValue(cell.CellId, out float lastTime))
                {
                    if (Time.time - lastTime < minStrikeInterval)
                        continue;
                }
                
                // Random chance based on activity level
                float strikeChance = cell.LightningActivity * effectIntensity * Time.deltaTime * 2f;
                
                if (Random.value < strikeChance)
                {
                    SpawnLightningStrike(cell);
                    lastStrikeTime[cell.CellId] = Time.time;
                }
            }
        }
        
        private void SpawnLightningStrike(SimulatedStormCell cell)
        {
            if (activeBolts.Count >= maxActiveBolts)
                return;
            
            var bolt = GetBoltFromPool();
            
            // Generate strike position within cell
            float offsetAngle = Random.value * Mathf.PI * 2f;
            float offsetDist = Random.value * cell.Radius * 0.7f;
            Vector2 offset = new Vector2(Mathf.Cos(offsetAngle), Mathf.Sin(offsetAngle)) * offsetDist;
            
            Vector3 startPos = new Vector3(
                cell.Position.x + offset.x,
                cell.TopAltitude * 0.8f, // Start from upper portion of cell
                cell.Position.y + offset.y
            );
            
            // End position - either ground or lower cloud
            bool cloudToCloud = Random.value < 0.3f;
            Vector3 endPos;
            
            if (cloudToCloud)
            {
                // Cloud-to-cloud lightning
                float endAngle = offsetAngle + Random.Range(0.5f, 2.5f);
                float endDist = Random.Range(cell.Radius * 0.3f, cell.Radius * 0.9f);
                endPos = new Vector3(
                    cell.Position.x + Mathf.Cos(endAngle) * endDist,
                    cell.BaseAltitude + Random.Range(0f, (cell.TopAltitude - cell.BaseAltitude) * 0.5f),
                    cell.Position.y + Mathf.Sin(endAngle) * endDist
                );
            }
            else
            {
                // Cloud-to-ground lightning
                endPos = new Vector3(
                    startPos.x + Random.Range(-500f, 500f),
                    0f,
                    startPos.z + Random.Range(-500f, 500f)
                );
            }
            
            // Generate bolt path
            Vector3[] path = GenerateBoltPath(startPos, endPos, segmentsPerBolt, 0);
            
            bolt.LineRenderer.positionCount = path.Length;
            bolt.LineRenderer.SetPositions(path);
            
            bolt.GlowRenderer.positionCount = path.Length;
            bolt.GlowRenderer.SetPositions(path);
            
            // Generate branches
            GenerateBranches(bolt, path, 0);
            
            // Set timing
            bolt.SpawnTime = Time.time;
            bolt.FlashDuration = flashDuration;
            bolt.AfterglowDuration = afterglowDuration;
            bolt.CellId = cell.CellId;
            
            activeBolts.Add(bolt);
        }
        
        private Vector3[] GenerateBoltPath(Vector3 start, Vector3 end, int segments, int depth)
        {
            Vector3[] path = new Vector3[segments + 1];
            path[0] = start;
            path[segments] = end;
            
            Vector3 direction = (end - start).normalized;
            float totalLength = Vector3.Distance(start, end);
            float segmentLength = totalLength / segments;
            
            // Generate perpendicular vectors for deviation
            Vector3 perp1 = Vector3.Cross(direction, Vector3.up).normalized;
            if (perp1.magnitude < 0.1f)
            {
                perp1 = Vector3.Cross(direction, Vector3.right).normalized;
            }
            Vector3 perp2 = Vector3.Cross(direction, perp1).normalized;
            
            // Deviation decreases with depth
            float deviationScale = maxSegmentDeviation / (depth + 1);
            
            for (int i = 1; i < segments; i++)
            {
                // Interpolate base position
                float t = (float)i / segments;
                Vector3 basePos = Vector3.Lerp(start, end, t);
                
                // Add random deviation (more at middle, less at ends)
                float deviationMult = Mathf.Sin(t * Mathf.PI); // Peaks at 0.5
                float deviation1 = (Random.value - 0.5f) * 2f * deviationScale * deviationMult;
                float deviation2 = (Random.value - 0.5f) * 2f * deviationScale * deviationMult;
                
                path[i] = basePos + perp1 * deviation1 + perp2 * deviation2;
            }
            
            return path;
        }
        
        private void GenerateBranches(LightningBolt bolt, Vector3[] mainPath, int depth)
        {
            if (depth >= maxBranchDepth)
                return;
            
            for (int i = 1; i < mainPath.Length - 2; i++)
            {
                if (Random.value < branchProbability / (depth + 1))
                {
                    // Create branch
                    Vector3 branchStart = mainPath[i];
                    
                    // Branch direction is offset from main direction
                    Vector3 mainDir = (mainPath[i + 1] - mainPath[i]).normalized;
                    Vector3 branchDir = Quaternion.Euler(
                        Random.Range(-45f, 45f),
                        Random.Range(-60f, 60f),
                        0f
                    ) * mainDir;
                    
                    // Branch length
                    float remainingLength = 0f;
                    for (int j = i; j < mainPath.Length - 1; j++)
                    {
                        remainingLength += Vector3.Distance(mainPath[j], mainPath[j + 1]);
                    }
                    float branchLength = remainingLength * branchLengthMultiplier * Random.Range(0.5f, 1f);
                    
                    Vector3 branchEnd = branchStart + branchDir * branchLength;
                    
                    // Generate branch path
                    int branchSegments = Mathf.Max(3, segmentsPerBolt / 2);
                    Vector3[] branchPath = GenerateBoltPath(branchStart, branchEnd, branchSegments, depth + 1);
                    
                    // Create branch renderer
                    GameObject branchObj = new GameObject("Branch");
                    branchObj.transform.SetParent(bolt.LineRenderer.transform);
                    
                    LineRenderer branchLine = branchObj.AddComponent<LineRenderer>();
                    branchLine.material = lightningMaterial;
                    branchLine.startWidth = boltWidth * 0.5f / (depth + 1);
                    branchLine.endWidth = boltWidth * 0.1f / (depth + 1);
                    branchLine.startColor = boltColor;
                    branchLine.endColor = boltColor * 0.5f;
                    branchLine.numCapVertices = 2;
                    branchLine.numCornerVertices = 2;
                    branchLine.useWorldSpace = true;
                    branchLine.positionCount = branchPath.Length;
                    branchLine.SetPositions(branchPath);
                    branchLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    
                    bolt.BranchRenderers.Add(branchLine);
                    
                    // Recursively add sub-branches
                    // (would need separate bolt struct for proper recursion, simplified here)
                }
            }
        }
        
        private void UpdateActiveBolts()
        {
            for (int i = activeBolts.Count - 1; i >= 0; i--)
            {
                var bolt = activeBolts[i];
                float age = Time.time - bolt.SpawnTime;
                float totalDuration = bolt.FlashDuration + bolt.AfterglowDuration;
                
                if (age > totalDuration)
                {
                    // Remove expired bolt
                    ReturnBoltToPool(bolt);
                    activeBolts.RemoveAt(i);
                    continue;
                }
                
                // Calculate alpha based on age
                float alpha;
                if (age < bolt.FlashDuration)
                {
                    // Flash phase - full brightness with flicker
                    alpha = 1f - (age / bolt.FlashDuration) * 0.2f;
                    // Add flicker
                    alpha *= Random.Range(0.8f, 1f);
                }
                else
                {
                    // Afterglow phase - fade out
                    float afterglowProgress = (age - bolt.FlashDuration) / bolt.AfterglowDuration;
                    alpha = 1f - afterglowProgress;
                    alpha *= alpha; // Quadratic falloff
                }
                
                // Apply alpha
                Color mainColor = boltColor;
                mainColor.a = alpha;
                bolt.LineRenderer.startColor = mainColor;
                bolt.LineRenderer.endColor = mainColor;
                
                Color glow = glowColor;
                glow.a = alpha * 0.5f;
                bolt.GlowRenderer.startColor = glow;
                bolt.GlowRenderer.endColor = glow;
                
                // Update branch colors
                foreach (var branch in bolt.BranchRenderers)
                {
                    if (branch != null)
                    {
                        Color branchColor = boltColor;
                        branchColor.a = alpha * 0.7f;
                        branch.startColor = branchColor;
                        branch.endColor = branchColor * 0.5f;
                    }
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
        /// Manually trigger a lightning strike at a position
        /// </summary>
        public void TriggerStrike(Vector3 startPosition, Vector3 endPosition)
        {
            if (!isInitialized || activeBolts.Count >= maxActiveBolts)
                return;
            
            var bolt = GetBoltFromPool();
            
            Vector3[] path = GenerateBoltPath(startPosition, endPosition, segmentsPerBolt, 0);
            bolt.LineRenderer.positionCount = path.Length;
            bolt.LineRenderer.SetPositions(path);
            bolt.GlowRenderer.positionCount = path.Length;
            bolt.GlowRenderer.SetPositions(path);
            
            GenerateBranches(bolt, path, 0);
            
            bolt.SpawnTime = Time.time;
            bolt.FlashDuration = flashDuration;
            bolt.AfterglowDuration = afterglowDuration;
            bolt.CellId = "";
            
            activeBolts.Add(bolt);
        }
        
        /// <summary>
        /// Set lightning colors
        /// </summary>
        public void SetColors(Color bolt, Color glow)
        {
            boltColor = bolt;
            glowColor = glow;
        }
        
        #endregion

        #region Nested Types
        
        private class LightningBolt
        {
            public LineRenderer LineRenderer;
            public LineRenderer GlowRenderer;
            public List<LineRenderer> BranchRenderers;
            public float SpawnTime;
            public float FlashDuration;
            public float AfterglowDuration;
            public string CellId;
        }
        
        #endregion
        
        #region Private Helpers
        
        private void Disable()
        {
            effectEnabled = false;
            
            // Return all active bolts to pool
            foreach (var bolt in activeBolts)
            {
                ReturnBoltToPool(bolt);
            }
            activeBolts.Clear();
        }
        
        #endregion
    }
}
