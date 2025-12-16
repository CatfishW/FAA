using UnityEngine;
using UnityEngine.UI;
using OnlineMaps;
using AircraftControl.Core;

namespace AircraftControl.Integration
{
    /// <summary>
    /// Bridges AircraftController position data to OnlineMaps display.
    /// Updates the map center to follow the aircraft's current latitude/longitude.
    /// Applies circular mask for a radar-style round display.
    /// </summary>
    [AddComponentMenu("Aircraft Control/Integration/OnlineMap Aircraft Bridge")]
    public class OnlineMapAircraftBridge : MonoBehaviour
    {
        #region Inspector Settings

        [Header("Components")]
        [Tooltip("Aircraft controller providing position data")]
        [SerializeField] private AircraftController aircraftController;
        
        [Tooltip("OnlineMaps Map component")]
        [SerializeField] private Map onlineMap;
        
        [Tooltip("RawImage displaying the map (for circular mask)")]
        [SerializeField] private RawImage mapRawImage;
        
        [Tooltip("Optional: UserLocation component for compass sync")]
        [SerializeField] private UserLocation userLocation;
        
        [Tooltip("Optional: TrafficRadarDisplay for range sync")]
        [SerializeField] private TrafficRadar.TrafficRadarDisplay trafficRadarDisplay;

        [Header("Follow Settings")]
        [Tooltip("Enable following aircraft position")]
        [SerializeField] private bool followAircraft = true;
        
        [Tooltip("Update interval in seconds (0 = every frame)")]
        [SerializeField] private float updateInterval = 0.1f;
        
        [Tooltip("Sync aircraft heading to UserLocation compass")]
        [SerializeField] private bool syncCompass = true;
        
        [Tooltip("Sync map zoom with traffic radar display range")]
        [SerializeField] private bool syncZoomWithRadar = true;
        
        [Header("Heading Rotation")]
        [Tooltip("Rotate map based on aircraft heading (track-up mode)")]
        [SerializeField] private bool rotateMapWithHeading = true;
        
        [Tooltip("Smoothing speed for heading rotation (higher = faster)")]
        [Range(1f, 20f)]
        [SerializeField] private float headingRotationSpeed = 5f;
        
        [Tooltip("Compass labels (N, E, S, W) to rotate with heading. Assign in order: N, E, S, W")]
        [SerializeField] private RectTransform[] compassLabelTransforms;
        
        [Tooltip("Container holding compass tick marks - entire container rotates with heading")]
        [SerializeField] private RectTransform compassTicksContainer;

        [Header("Circular Mask Settings")]
        [Tooltip("Apply circular mask to the map display")]
        [SerializeField] private bool applyCircularMask = true;
        
        [Tooltip("Circular mask opacity (0 = transparent, 1 = opaque)")]
        [Range(0f, 1f)]
        [SerializeField] private float maskOpacity = 1.0f;
        
        [Tooltip("Mask edge softness (0 = hard edge, 0.1 = very soft)")]
        [Range(0f, 0.1f)]
        [SerializeField] private float maskEdgeSoftness = 0.02f;

        [Header("Debug")]
        [SerializeField] private bool showDebugInfo = false;

        #endregion

        #region Private Fields

        private float _lastUpdateTime;
        private bool _isInitialized;
        private Material _circularMaskMaterial;
        private RectTransform _mapRectTransform;
        private float _currentRotation;
        private float _targetRotation;
        
        // Shader property IDs
        private static readonly int OpacityProperty = Shader.PropertyToID("_Opacity");
        private static readonly int SoftEdgeProperty = Shader.PropertyToID("_SoftEdge");

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets whether the map follows the aircraft position.
        /// </summary>
        public bool FollowAircraft
        {
            get => followAircraft;
            set => followAircraft = value;
        }

        /// <summary>
        /// Gets or sets the circular mask opacity.
        /// </summary>
        public float MaskOpacity
        {
            get => maskOpacity;
            set
            {
                maskOpacity = Mathf.Clamp01(value);
                UpdateMaskProperties();
            }
        }

        /// <summary>
        /// Gets or sets the mask edge softness.
        /// </summary>
        public float MaskEdgeSoftness
        {
            get => maskEdgeSoftness;
            set
            {
                maskEdgeSoftness = Mathf.Clamp(value, 0f, 0.1f);
                UpdateMaskProperties();
            }
        }
        

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            Initialize();
        }

        private void OnEnable()
        {
            if (aircraftController != null)
            {
                aircraftController.OnPositionChanged += OnAircraftPositionChanged;
            }
            
            if (trafficRadarDisplay != null && syncZoomWithRadar)
            {
                trafficRadarDisplay.OnZoomChanged.AddListener(OnRadarZoomChanged);
            }
        }

        private void OnDisable()
        {
            if (aircraftController != null)
            {
                aircraftController.OnPositionChanged -= OnAircraftPositionChanged;
            }
            
            if (trafficRadarDisplay != null)
            {
                trafficRadarDisplay.OnZoomChanged.RemoveListener(OnRadarZoomChanged);
            }
        }

        private void Update()
        {
            if (!_isInitialized || !followAircraft) return;

            if (updateInterval > 0 && Time.time - _lastUpdateTime < updateInterval)
                return;

            _lastUpdateTime = Time.time;
            UpdateMapPosition();
        }

        private void OnDestroy()
        {
            if (_circularMaskMaterial != null)
            {
                Destroy(_circularMaskMaterial);
                _circularMaskMaterial = null;
            }
        }

        private void OnValidate()
        {
            if (_circularMaskMaterial != null)
            {
                UpdateMaskProperties();
            }
        }

        #endregion

        #region Initialization

        private void Initialize()
        {
            // Auto-find components if not assigned
            if (aircraftController == null)
                aircraftController = FindObjectOfType<AircraftController>();

            if (onlineMap == null)
                onlineMap = FindObjectOfType<Map>();

            if (userLocation == null)
                userLocation = FindObjectOfType<UserLocation>();
            
            // Auto-find TrafficRadarDisplay for range sync
            if (trafficRadarDisplay == null)
                trafficRadarDisplay = FindObjectOfType<TrafficRadar.TrafficRadarDisplay>();

            // Try to find map RawImage if not assigned
            if (mapRawImage == null)
            {
                // Look for RawImage with a RenderTexture (map display)
                foreach (var rawImage in FindObjectsOfType<RawImage>())
                {
                    if (rawImage.texture != null && rawImage.texture is RenderTexture)
                    {
                        mapRawImage = rawImage;
                        Debug.Log($"[OnlineMapAircraftBridge] Auto-found map RawImage: {rawImage.name}");
                        break;
                    }
                }
            }

            // Validate required components
            if (aircraftController == null)
            {
                Debug.LogError("[OnlineMapAircraftBridge] AircraftController not found!");
                return;
            }

            if (onlineMap == null)
            {
                Debug.LogError("[OnlineMapAircraftBridge] OnlineMaps Map component not found!");
                return;
            }

            // Setup circular mask on RawImage
            if (applyCircularMask && mapRawImage != null)
            {
                SetupCircularMask();
            }
            else if (applyCircularMask && mapRawImage == null)
            {
                Debug.LogWarning("[OnlineMapAircraftBridge] Map RawImage not found. Assign it in Inspector to apply circular mask.");
            }
            
            // Cache RectTransform for heading rotation
            if (mapRawImage != null)
            {
                _mapRectTransform = mapRawImage.GetComponent<RectTransform>();
            }

            _isInitialized = true;
            Debug.Log("[OnlineMapAircraftBridge] Initialized successfully");

            // Initial position update
            UpdateMapPosition();
            
            // Initial zoom sync with radar
            if (syncZoomWithRadar && trafficRadarDisplay != null)
            {
                OnRadarZoomChanged(trafficRadarDisplay.RangeNM);
            }
        }

        private void SetupCircularMask()
        {
            // Find the circular mask shader
            Shader circularShader = Shader.Find("TrafficRadar/CircularRadarMask");
            if (circularShader == null)
            {
                Debug.LogError("[OnlineMapAircraftBridge] CircularRadarMask shader not found! Make sure TrafficRadar shaders are in project.");
                return;
            }

            // Create runtime material
            _circularMaskMaterial = new Material(circularShader);
            _circularMaskMaterial.name = "OnlineMapCircularMask_Runtime";
            
            UpdateMaskProperties();
            
            // Apply to RawImage
            mapRawImage.material = _circularMaskMaterial;
            
            Debug.Log($"[OnlineMapAircraftBridge] Circular mask applied to {mapRawImage.name}");
        }

        private void UpdateMaskProperties()
        {
            if (_circularMaskMaterial == null) return;
            
            _circularMaskMaterial.SetFloat(OpacityProperty, maskOpacity);
            _circularMaskMaterial.SetFloat(SoftEdgeProperty, maskEdgeSoftness);
        }

        #endregion

        #region Position Updates

        private void OnAircraftPositionChanged(double latitude, double longitude, float altitude)
        {
            if (followAircraft && _isInitialized)
            {
                SetMapCenter(longitude, latitude);
            }
        }

        private void UpdateMapPosition()
        {
            if (aircraftController == null || onlineMap == null) return;

            var state = aircraftController.State;
            if (state == null) return;

            SetMapCenter(state.Longitude, state.Latitude);

            if (syncCompass && userLocation != null)
            {
                userLocation.emulatedCompass = state.Heading;
            }
            
            // Update heading rotation
            if (rotateMapWithHeading && _mapRectTransform != null)
            {
                UpdateHeadingRotation(state.Heading);
            }

            if (showDebugInfo)
            {
                Debug.Log($"[OnlineMapAircraftBridge] Map updated: {state.Latitude:F4}, {state.Longitude:F4}, HDG {state.Heading:F1}°");
            }
        }
        
        private void UpdateHeadingRotation(float heading)
        {
            // Target rotation: rotate map counter-clockwise so aircraft heading points up (track-up)
            _targetRotation = heading;
            
            // Smooth rotation
            _currentRotation = Mathf.LerpAngle(_currentRotation, _targetRotation, Time.deltaTime * headingRotationSpeed);
            
            // Apply rotation to RawImage (map)
            _mapRectTransform.localRotation = Quaternion.Euler(0, 0, _currentRotation);
            
            // Rotate compass labels around center
            // Labels are expected in order: N (0°), E (90°), S (180°), W (270°)
            if (compassLabelTransforms != null && compassLabelTransforms.Length > 0)
            {
                float[] baseAngles = { 0f, 90f, 180f, 270f }; // N, E, S, W
                float radius = GetCompassLabelRadius();
                
                for (int i = 0; i < compassLabelTransforms.Length && i < 4; i++)
                {
                    if (compassLabelTransforms[i] == null) continue;
                    
                    // Calculate new position around center
                    float angle = baseAngles[i] - _currentRotation;
                    float radians = angle * Mathf.Deg2Rad;
                    
                    // Position around center (N at top = angle 0 = up)
                    float x = Mathf.Sin(radians) * radius;
                    float y = Mathf.Cos(radians) * radius;
                    
                    compassLabelTransforms[i].anchoredPosition = new Vector2(x, y);
                    
                    // Counter-rotate text so it stays upright
                    compassLabelTransforms[i].localRotation = Quaternion.Euler(0, 0, 0);
                }
            }
            
            // Rotate compass ticks container as a whole
            if (compassTicksContainer != null)
            {
                compassTicksContainer.localRotation = Quaternion.Euler(0, 0, _currentRotation);
            }
        }
        
        private float GetCompassLabelRadius()
        {
            // Calculate radius based on map size
            if (_mapRectTransform != null)
            {
                Vector2 size = _mapRectTransform.rect.size;
                return Mathf.Min(size.x, size.y) * 0.45f; // 45% of size for labels
            }
            return 200f; // Default fallback
        }

        private void SetMapCenter(double longitude, double latitude)
        {
            if (onlineMap == null) return;
            onlineMap.view.center = new GeoPoint(longitude, latitude);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Toggle aircraft following on/off.
        /// </summary>
        public void ToggleFollow()
        {
            followAircraft = !followAircraft;
            Debug.Log($"[OnlineMapAircraftBridge] Follow mode: {(followAircraft ? "ON" : "OFF")}");
        }

        /// <summary>
        /// Set the map zoom level.
        /// </summary>
        public void SetZoom(float zoom)
        {
            if (onlineMap != null)
            {
                onlineMap.view.zoom = zoom;
            }
        }

        /// <summary>
        /// Get the current map zoom level.
        /// </summary>
        public float GetZoom()
        {
            return onlineMap != null ? onlineMap.view.zoom : 0f;
        }

        /// <summary>
        /// Manually force a position update.
        /// </summary>
        public void ForceUpdate()
        {
            UpdateMapPosition();
        }

        /// <summary>
        /// Manually apply or reapply the circular mask.
        /// </summary>
        public void ApplyCircularMask()
        {
            if (mapRawImage != null)
            {
                SetupCircularMask();
            }
        }

        #endregion

        #region Zoom Sync

        /// <summary>
        /// Called when TrafficRadarDisplay range changes.
        /// Converts NM range to OnlineMaps zoom level.
        /// </summary>
        private void OnRadarZoomChanged(float rangeNM)
        {
            if (!syncZoomWithRadar || onlineMap == null) return;
            
            float zoom = RangeNMToZoom(rangeNM);
            onlineMap.view.zoom = zoom;
            
            if (showDebugInfo)
            {
                Debug.Log($"[OnlineMapAircraftBridge] Synced zoom: {rangeNM:F1}NM → Zoom {zoom:F1}");
            }
        }

        /// <summary>
        /// Converts nautical miles range to OnlineMaps zoom level.
        /// Based on: at zoom 10, map shows roughly 40NM radius.
        /// Formula: zoom ≈ 12 - log2(rangeNM / 10)
        /// </summary>
        private float RangeNMToZoom(float rangeNM)
        {
            // Clamp range to reasonable values
            rangeNM = Mathf.Clamp(rangeNM, 1f, 500f);
            
            // At zoom level 10, approximately 40NM visible across map
            // At zoom level 12, approximately 10NM visible
            // At zoom level 14, approximately 2.5NM visible
            // Formula: zoom = 14 - log2(rangeNM / 2.5)
            float zoom = 14f - Mathf.Log(rangeNM / 2.5f, 2f);
            
            // Clamp to valid OnlineMaps zoom range
            return Mathf.Clamp(zoom, 3f, 20f);
        }

        /// <summary>
        /// Manually sync map zoom with current radar range.
        /// </summary>
        public void SyncZoomWithRadar()
        {
            if (trafficRadarDisplay != null && onlineMap != null)
            {
                float range = trafficRadarDisplay.RangeNM;
                float zoom = RangeNMToZoom(range);
                onlineMap.view.zoom = zoom;
                Debug.Log($"[OnlineMapAircraftBridge] Manual sync: {range:F1}NM → Zoom {zoom:F1}");
            }
        }

        #endregion
    }
}
