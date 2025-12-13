using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TrafficRadar.Core;

namespace TrafficRadar
{
    /// <summary>
    /// Main traffic radar display panel.
    /// Renders a circular radar display with aircraft symbols, range rings, and compass markings.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class TrafficRadarDisplay : MonoBehaviour
    {
        [Header("Controller")]
        [Tooltip("Traffic Radar Controller - manages data and events")]
        [SerializeField] private TrafficRadarController radarController;
        
        [Header("Chart Provider")]
        [SerializeField] private FAASectionalChartProvider chartProvider;

        [Header("Display Settings")]
        [Tooltip("Size of the radar display in pixels")]
        [SerializeField] private int displaySize = 400;
        
        [Tooltip("Show FAA sectional chart as background")]
        [SerializeField] private bool showChartBackground = true;
        
        [Tooltip("Chart background opacity")]
        [Range(0f, 1f)]
        [SerializeField] private float chartOpacity = 0.5f;
        
        [Tooltip("Edge softness for circular chart mask (0 = hard edge, 0.1 = soft edge)")]
        [Range(0f, 0.1f)]
        [SerializeField] private float chartEdgeSoftness = 0.02f;

        [Header("Range Settings")]
        [Tooltip("Current radar range in nautical miles")]
        [SerializeField] private float rangeNM = 20f;
        
        [Tooltip("Minimum range in nautical miles")]
        [SerializeField] private float minRangeNM = 2f;
        
        [Tooltip("Maximum range in nautical miles")]
        [SerializeField] private float maxRangeNM = 150f;
        
        [Tooltip("Zoom speed multiplier per scroll step")]
        [SerializeField] private float zoomSpeed = 1.5f;
        
        [Tooltip("Available range options (for CycleRange, optional)")]
        [SerializeField] private float[] rangeOptionsNM = { 5f, 10f, 20f, 40f, 80f };
        
        [Tooltip("Number of range rings to display")]
        [SerializeField] private int rangeRingCount = 4;
        
        [Header("Zoom Animation")]
        [Tooltip("Enable smooth zoom animation")]
        [SerializeField] private bool enableSmoothZoom = true;
        
        [Tooltip("Animation duration in seconds")]
        [SerializeField] private float zoomAnimationDuration = 0.3f;

        [Header("Visual Settings")]
        [Tooltip("Show radar background circle (disable to show only chart)")]
        [SerializeField] private bool showRadarBackground = false;
        
        [SerializeField] private Color backgroundColor = new Color(0.05f, 0.1f, 0.15f, 0.9f);
        [SerializeField] private Color rangeRingColor = new Color(0.3f, 0.4f, 0.5f, 0.6f);
        [SerializeField] private Color compassMarkingsColor = new Color(0.6f, 0.7f, 0.8f, 0.8f);
        [SerializeField] private Color ownAircraftColor = new Color(1f, 0f, 0f, 1f);

        [Header("Symbol Settings")]
        [Tooltip("Size of aircraft symbols in pixels")]
        [SerializeField] private float symbolSize = 12f;
        
        [Tooltip("Show altitude labels on symbols")]
        [SerializeField] private bool showAltitudeLabels = true;

        [Header("UI References")]
        [SerializeField] private RawImage radarImage;
        [SerializeField] private RawImage chartBackgroundImage;
        [SerializeField] private TextMeshProUGUI rangeLabel;
        [SerializeField] private TextMeshProUGUI[] compassLabels;
        
        [Header("Circular Mask Settings")]
        [Tooltip("Material for circular chart mask (auto-created if null)")]
        [SerializeField] private Material circularMaskMaterial;
        
        [Header("Heading Rotation (Track-Up Mode)")]
        [Tooltip("Enable track-up mode - display rotates with aircraft heading")]
        [SerializeField] private bool enableTrackUpMode = true;
        
        [Tooltip("Smoothing speed for heading rotation (higher = faster)")]
        [Range(1f, 20f)]
        [SerializeField] private float headingRotationSpeed = 8f;
        
        [Tooltip("Container for compass tick marks (rotates as a whole)")]
        [SerializeField] private RectTransform compassTicksContainer;
        
        [Header("Events")]
        [Tooltip("Fired when zoom/range changes")]
        public UnityEngine.Events.UnityEvent<float> OnZoomChanged;
        
        // Runtime-created material for radar overlay (full opacity)
        private Material radarOverlayMaterial;

        // Internal textures
        private Texture2D radarTexture;
        private RectTransform rectTransform;
        private List<RadarTrafficTarget> currentTargets = new List<RadarTrafficTarget>();

        // Symbol drawing
        private Color32[] clearPixels;
        
        // Zoom animation state
        private float zoomFromRange;
        private float zoomToRange;
        private float zoomProgress;
        private bool isAnimatingZoom;
        private float zoomAnimStartTime;
        
        // Heading rotation state
        private float _currentHeadingRotation;
        private float _targetHeadingRotation;
        private RectTransform[] _compassLabelRects;

        #region Properties

        public float RangeNM
        {
            get => rangeNM;
            set
            {
                float newRange = Mathf.Clamp(value, minRangeNM, maxRangeNM);
                if (!Mathf.Approximately(rangeNM, newRange))
                {
                    rangeNM = newRange;
                    UpdateRangeLabel();
                    OnZoomChanged?.Invoke(rangeNM);
                }
            }
        }
        
        /// <summary>
        /// Minimum zoom range in nautical miles.
        /// </summary>
        public float MinRangeNM => minRangeNM;
        
        /// <summary>
        /// Maximum zoom range in nautical miles.
        /// </summary>
        public float MaxRangeNM => maxRangeNM;
        
        /// <summary>
        /// Whether a zoom animation is currently in progress.
        /// </summary>
        public bool IsAnimatingZoom => isAnimatingZoom;
        
        /// <summary>
        /// Gets or sets the chart background opacity (0 = fully transparent, 1 = fully opaque).
        /// </summary>
        public float ChartOpacity
        {
            get => chartOpacity;
            set
            {
                chartOpacity = Mathf.Clamp01(value);
                UpdateChartOpacity();
            }
        }
        
        /// <summary>
        /// Gets or sets the circular mask edge softness (0 = hard edge, 0.1 = soft edge).
        /// </summary>
        public float ChartEdgeSoftness
        {
            get => chartEdgeSoftness;
            set
            {
                chartEdgeSoftness = Mathf.Clamp(value, 0f, 0.1f);
                UpdateChartEdgeSoftness();
            }
        }
        
        /// <summary>
        /// Gets or sets whether the radar background is shown.
        /// </summary>
        public bool ShowRadarBackground
        {
            get => showRadarBackground;
            set => showRadarBackground = value;
        }
        
        /// <summary>
        /// Gets or sets the radar background color.
        /// </summary>
        public Color BackgroundColor
        {
            get => backgroundColor;
            set => backgroundColor = value;
        }
        
        /// <summary>
        /// Gets or sets the range ring color.
        /// </summary>
        public Color RangeRingColor
        {
            get => rangeRingColor;
            set => rangeRingColor = value;
        }
        
        /// <summary>
        /// Gets or sets the compass markings color.
        /// </summary>
        public Color CompassMarkingsColor
        {
            get => compassMarkingsColor;
            set => compassMarkingsColor = value;
        }
        
        /// <summary>
        /// Gets or sets the own aircraft symbol color.
        /// </summary>
        public Color OwnAircraftColor
        {
            get => ownAircraftColor;
            set => ownAircraftColor = value;
        }

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            CreateRadarTexture();
        }

        private void OnEnable()
        {
            // Subscribe to controller
            if (radarController != null)
            {
                radarController.OnTargetsUpdated.AddListener(OnControllerTargetsUpdated);
            }

            if (chartProvider != null)
            {
                chartProvider.OnChartTileLoaded += OnChartLoaded;
            }
        }

        private void OnDisable()
        {
            // Unsubscribe from controller
            if (radarController != null)
            {
                radarController.OnTargetsUpdated.RemoveListener(OnControllerTargetsUpdated);
            }

            if (chartProvider != null)
            {
                chartProvider.OnChartTileLoaded -= OnChartLoaded;
            }
        }

        private void Start()
        {
            // Auto-find controller
            if (radarController == null)
                radarController = FindObjectOfType<TrafficRadarController>();

            if (chartProvider == null)
                chartProvider = FindObjectOfType<FAASectionalChartProvider>();

            // Re-subscribe to controller if found in Start
            if (radarController != null)
            {
                radarController.OnTargetsUpdated.RemoveListener(OnControllerTargetsUpdated);
                radarController.OnTargetsUpdated.AddListener(OnControllerTargetsUpdated);
                Debug.Log("[TrafficRadarDisplay] Connected to TrafficRadarController");
            }

            // Setup UI
            SetupDisplay();
            UpdateRangeLabel();

            // Initial chart fetch
            if (showChartBackground && chartProvider != null && radarController != null)
            {
                float lat = (float)radarController.OwnPosition.Latitude;
                float lon = (float)radarController.OwnPosition.Longitude;
                if (lat != 0 && lon != 0)
                    chartProvider.FetchChartTiles(lat, lon, rangeNM);
            }
            
            // Auto-discover compass labels if not assigned
            if (compassLabels == null || compassLabels.Length == 0)
            {
                AutoDiscoverCompassLabels();
            }
            
            // Cache compass label RectTransforms for heading rotation
            if (compassLabels != null && compassLabels.Length > 0)
            {
                _compassLabelRects = new RectTransform[compassLabels.Length];
                for (int i = 0; i < compassLabels.Length; i++)
                {
                    if (compassLabels[i] != null)
                    {
                        _compassLabelRects[i] = compassLabels[i].GetComponent<RectTransform>();
                        Debug.Log($"[TrafficRadarDisplay] Compass label {i} '{compassLabels[i].text}' found at position {_compassLabelRects[i].anchoredPosition}");
                    }
                }
            }
            else
            {
                Debug.LogWarning("[TrafficRadarDisplay] No compass labels found! Cardinal directions will not rotate with heading.");
            }
        }
        
        /// <summary>
        /// Auto-discover compass labels (N, E, S, W) from child TextMeshProUGUI components.
        /// </summary>
        private void AutoDiscoverCompassLabels()
        {
            TextMeshProUGUI[] allLabels = GetComponentsInChildren<TextMeshProUGUI>(true);
            List<TextMeshProUGUI> found = new List<TextMeshProUGUI>();
            
            // Look for labels with text N, E, S, W (in that order for base angles 0, 90, 180, 270)
            string[] cardinals = { "N", "E", "S", "W" };
            foreach (string cardinal in cardinals)
            {
                TextMeshProUGUI label = null;
                foreach (var tmp in allLabels)
                {
                    if (tmp.text.Trim().Equals(cardinal, System.StringComparison.OrdinalIgnoreCase))
                    {
                        label = tmp;
                        break;
                    }
                }
                
                if (label == null)
                {
                    // Also try by name
                    foreach (var tmp in allLabels)
                    {
                        string name = tmp.gameObject.name.ToUpperInvariant();
                        if (name.Contains(cardinal) && (name.Contains("LABEL") || name.Contains("COMPASS") || name.Length <= 3))
                        {
                            label = tmp;
                            break;
                        }
                    }
                }
                
                found.Add(label); // May be null if not found
            }
            
            // Only assign if we found at least some labels
            int foundCount = found.FindAll(l => l != null).Count;
            if (foundCount > 0)
            {
                compassLabels = found.ToArray();
                Debug.Log($"[TrafficRadarDisplay] Auto-discovered {foundCount} compass labels (N/E/S/W)");
            }
        }

        private void OnDestroy()
        {
            if (radarTexture != null)
                Destroy(radarTexture);
            
            // Clean up runtime-created materials
            if (circularMaskMaterial != null && circularMaskMaterial.name.Contains("_Runtime"))
                Destroy(circularMaskMaterial);
            
            if (radarOverlayMaterial != null)
                Destroy(radarOverlayMaterial);
        }

        private void Update()
        {
            // Handle zoom animation
            if (isAnimatingZoom)
            {
                UpdateZoomAnimation();
            }
            
            // Handle heading rotation (track-up mode)
            if (enableTrackUpMode)
            {
                UpdateHeadingRotation();
            }
            
            // Redraw radar each frame
            DrawRadar();
        }
        
        /// <summary>
        /// Updates the smooth zoom animation.
        /// </summary>
        private void UpdateZoomAnimation()
        {
            zoomProgress = (Time.time - zoomAnimStartTime) / zoomAnimationDuration;
            
            if (zoomProgress >= 1f)
            {
                zoomProgress = 1f;
                isAnimatingZoom = false;
            }
            
            // Lerp the range value
            float newRange = Mathf.Lerp(zoomFromRange, zoomToRange, zoomProgress);
            RangeNM = newRange;
            
            // Update controller if available
            if (radarController != null && !Mathf.Approximately(radarController.RangeNM, newRange))
            {
                radarController.RangeNM = newRange;
            }
            
            // Refresh chart when animation completes
            if (!isAnimatingZoom)
            {
                RefreshChartForCurrentRange();
            }
        }
        
        /// <summary>
        /// Updates heading rotation for track-up mode.
        /// Rotates compass labels and tick marks based on aircraft heading.
        /// </summary>
        private void UpdateHeadingRotation()
        {
            if (radarController == null) return;
            
            float heading = radarController.OwnPosition.HeadingDegrees;
            
            // Target rotation: negative heading so heading points up
            _targetHeadingRotation = -heading;
            
            // Smooth rotation using lerp
            _currentHeadingRotation = Mathf.LerpAngle(_currentHeadingRotation, _targetHeadingRotation, 
                Time.deltaTime * headingRotationSpeed);
            
            // Rotate compass ticks container as a whole
            if (compassTicksContainer != null)
            {
                compassTicksContainer.localRotation = Quaternion.Euler(0, 0, _currentHeadingRotation);
            }
            
            // Rotate compass labels around center, keeping text upright
            if (_compassLabelRects != null && _compassLabelRects.Length > 0)
            {
                // Base angles for N, E, S, W (in order of array)
                float[] baseAngles = { 0f, 90f, 180f, 270f };
                float radius = GetCompassLabelRadius();
                
                for (int i = 0; i < _compassLabelRects.Length && i < 4; i++)
                {
                    if (_compassLabelRects[i] == null) continue;
                    
                    // Ensure label anchors are centered for proper positioning
                    // This makes anchoredPosition relative to parent's center
                    _compassLabelRects[i].anchorMin = new Vector2(0.5f, 0.5f);
                    _compassLabelRects[i].anchorMax = new Vector2(0.5f, 0.5f);
                    _compassLabelRects[i].pivot = new Vector2(0.5f, 0.5f);
                    
                    // Calculate new position around center
                    float angle = baseAngles[i] + _currentHeadingRotation;
                    float radians = angle * Mathf.Deg2Rad;
                    
                    // Position around center (N at top = angle 0 = up)
                    float x = Mathf.Sin(radians) * radius;
                    float y = Mathf.Cos(radians) * radius;
                    
                    _compassLabelRects[i].anchoredPosition = new Vector2(x, y);
                    
                    // Keep text upright
                    _compassLabelRects[i].localRotation = Quaternion.identity;
                }
            }
            
            // Also rotate the chart background image
            if (chartBackgroundImage != null)
            {
                RectTransform chartRect = chartBackgroundImage.GetComponent<RectTransform>();
                if (chartRect != null)
                {
                    chartRect.localRotation = Quaternion.Euler(0, 0, _currentHeadingRotation);
                }
            }
        }
        
        /// <summary>
        /// Gets the radius for compass label positioning.
        /// </summary>
        private float GetCompassLabelRadius()
        {
            if (rectTransform != null)
            {
                Vector2 size = rectTransform.rect.size;
                return Mathf.Min(size.x, size.y) * 0.45f;
            }
            return displaySize * 0.45f;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Cycle through available range options.
        /// </summary>
        public void CycleRange()
        {
            int currentIndex = 0;
            for (int i = 0; i < rangeOptionsNM.Length; i++)
            {
                if (Mathf.Approximately(rangeOptionsNM[i], rangeNM))
                {
                    currentIndex = i;
                    break;
                }
            }

            currentIndex = (currentIndex + 1) % rangeOptionsNM.Length;
            SetRange(rangeOptionsNM[currentIndex]);
        }
        
        /// <summary>
        /// Zoom in (decrease range) by the zoom speed amount.
        /// Uses smooth animation if enabled.
        /// </summary>
        public void ZoomIn()
        {
            float targetRange = rangeNM / zoomSpeed;
            if (isAnimatingZoom) targetRange = zoomToRange / zoomSpeed;
            
            targetRange = Mathf.Clamp(targetRange, minRangeNM, maxRangeNM);
            
            if (enableSmoothZoom)
            {
                StartZoomAnimation(targetRange);
            }
            else
            {
                SetRangeImmediate(targetRange);
            }
        }
        
        /// <summary>
        /// Zoom out (increase range) by the zoom speed amount.
        /// Uses smooth animation if enabled.
        /// </summary>
        public void ZoomOut()
        {
            float targetRange = rangeNM * zoomSpeed;
            if (isAnimatingZoom) targetRange = zoomToRange * zoomSpeed;
            
            targetRange = Mathf.Clamp(targetRange, minRangeNM, maxRangeNM);
            
            if (enableSmoothZoom)
            {
                StartZoomAnimation(targetRange);
            }
            else
            {
                SetRangeImmediate(targetRange);
            }
        }
        
        /// <summary>
        /// Zoom by a specific amount (positive = zoom out, negative = zoom in).
        /// </summary>
        /// <param name="delta">Zoom delta (positive increases range, negative decreases)</param>
        public void ZoomBy(float delta)
        {
            float targetRange = rangeNM + delta;
            if (isAnimatingZoom) targetRange = zoomToRange + delta;
            
            targetRange = Mathf.Clamp(targetRange, minRangeNM, maxRangeNM);
            
            if (enableSmoothZoom)
            {
                StartZoomAnimation(targetRange);
            }
            else
            {
                SetRangeImmediate(targetRange);
            }
        }
        
        /// <summary>
        /// Start a smooth zoom animation to the target range.
        /// </summary>
        /// <param name="targetRange">Target range in nautical miles.</param>
        public void StartZoomAnimation(float targetRange)
        {
            targetRange = Mathf.Clamp(targetRange, minRangeNM, maxRangeNM);
            
            zoomFromRange = rangeNM;
            zoomToRange = targetRange;
            zoomProgress = 0f;
            zoomAnimStartTime = Time.time;
            isAnimatingZoom = true;
        }
        
        /// <summary>
        /// Set the radar range immediately (no animation).
        /// Also updates chart tiles.
        /// </summary>
        /// <param name="newRangeNM">New range in nautical miles.</param>
        public void SetRangeImmediate(float newRangeNM)
        {
            RangeNM = newRangeNM;
            
            // Update the controller's range if available
            if (radarController != null && !Mathf.Approximately(radarController.RangeNM, newRangeNM))
            {
                radarController.RangeNM = newRangeNM;
            }
            
            // Refresh chart for new range
            RefreshChartForCurrentRange();
        }
        
        /// <summary>
        /// Set the radar range and update chart tiles accordingly.
        /// Uses animation if smooth zoom is enabled.
        /// </summary>
        /// <param name="newRangeNM">New range in nautical miles.</param>
        public void SetRange(float newRangeNM)
        {
            if (enableSmoothZoom)
            {
                StartZoomAnimation(newRangeNM);
            }
            else
            {
                SetRangeImmediate(newRangeNM);
            }
        }
        
        /// <summary>
        /// Get the current range index in the options array.
        /// </summary>
        private int GetCurrentRangeIndex()
        {
            for (int i = 0; i < rangeOptionsNM.Length; i++)
            {
                if (Mathf.Approximately(rangeOptionsNM[i], rangeNM))
                {
                    return i;
                }
            }
            // Find closest range if exact match not found
            int closestIndex = 0;
            float closestDiff = Mathf.Abs(rangeOptionsNM[0] - rangeNM);
            for (int i = 1; i < rangeOptionsNM.Length; i++)
            {
                float diff = Mathf.Abs(rangeOptionsNM[i] - rangeNM);
                if (diff < closestDiff)
                {
                    closestDiff = diff;
                    closestIndex = i;
                }
            }
            return closestIndex;
        }
        
        /// <summary>
        /// Refresh the chart tiles for the current range.
        /// </summary>
        private void RefreshChartForCurrentRange()
        {
            if (showChartBackground && chartProvider != null && radarController != null)
            {
                chartProvider.FetchChartTiles(
                    (float)radarController.OwnPosition.Latitude, 
                    (float)radarController.OwnPosition.Longitude, 
                    rangeNM);
            }
        }

        /// <summary>
        /// Toggle chart background visibility.
        /// </summary>
        public void ToggleChartBackground()
        {
            showChartBackground = !showChartBackground;
            if (chartBackgroundImage != null)
            {
                chartBackgroundImage.enabled = showChartBackground;
            }
        }
        
        /// <summary>
        /// Set the chart background opacity (0 = fully transparent, 1 = fully opaque).
        /// </summary>
        /// <param name="opacity">Opacity value between 0 and 1.</param>
        public void SetChartOpacity(float opacity)
        {
            ChartOpacity = opacity;
        }
        
        /// <summary>
        /// Increase chart background opacity by the specified amount.
        /// </summary>
        public void IncreaseChartOpacity(float amount = 0.1f)
        {
            ChartOpacity += amount;
        }
        
        /// <summary>
        /// Decrease chart background opacity by the specified amount.
        /// </summary>
        public void DecreaseChartOpacity(float amount = 0.1f)
        {
            ChartOpacity -= amount;
        }

        /// <summary>
        /// Refresh the chart background.
        /// </summary>
        public void RefreshChart()
        {
            if (chartProvider != null && radarController != null)
            {
                chartProvider.FetchChartTiles((float)radarController.OwnPosition.Latitude, (float)radarController.OwnPosition.Longitude, rangeNM);
            }
        }

        #endregion

        #region Private Methods

        private void CreateRadarTexture()
        {
            radarTexture = new Texture2D(displaySize, displaySize, TextureFormat.RGBA32, false);
            radarTexture.wrapMode = TextureWrapMode.Clamp;
            radarTexture.filterMode = FilterMode.Bilinear;

            // Create clear pixels array for fast clearing
            clearPixels = new Color32[displaySize * displaySize];
            Color32 clearColor = new Color32(0, 0, 0, 0);
            for (int i = 0; i < clearPixels.Length; i++)
            {
                clearPixels[i] = clearColor;
            }
        }

        private void SetupDisplay()
        {
            // Find or create the circular mask shader
            Shader circularShader = null;
            if (circularMaskMaterial == null || radarOverlayMaterial == null)
            {
                circularShader = Shader.Find("TrafficRadar/CircularRadarMask");
                if (circularShader == null)
                {
                    Debug.LogWarning("[TrafficRadarDisplay] Circular mask shader not found, display will be square.");
                }
            }
            
            // Setup radar image with circular mask (full opacity)
            if (radarImage != null)
            {
                radarImage.texture = radarTexture;
                
                // Create radar overlay material with full opacity
                if (radarOverlayMaterial == null && circularShader != null)
                {
                    radarOverlayMaterial = new Material(circularShader);
                    radarOverlayMaterial.name = "RadarOverlayMask_Runtime";
                    radarOverlayMaterial.SetFloat("_Opacity", 1.0f);
                    radarOverlayMaterial.SetFloat("_SoftEdge", chartEdgeSoftness);
                }
                
                if (radarOverlayMaterial != null)
                {
                    radarImage.material = radarOverlayMaterial;
                }
            }

            // Setup chart background with circular mask
            if (chartBackgroundImage != null)
            {
                chartBackgroundImage.enabled = showChartBackground;
                
                // Create circular mask material if needed
                if (circularMaskMaterial == null && circularShader != null)
                {
                    circularMaskMaterial = new Material(circularShader);
                    circularMaskMaterial.name = "CircularRadarMask_Runtime";
                }
                
                // Apply circular mask material
                if (circularMaskMaterial != null)
                {
                    chartBackgroundImage.material = circularMaskMaterial;
                    UpdateChartOpacity();
                    UpdateChartEdgeSoftness();
                }
                else
                {
                    // Fallback: just set color alpha
                    Color c = chartBackgroundImage.color;
                    c.a = chartOpacity;
                    chartBackgroundImage.color = c;
                }
            }
        }
        
        private void UpdateChartOpacity()
        {
            if (circularMaskMaterial != null)
            {
                circularMaskMaterial.SetFloat("_Opacity", chartOpacity);
            }
            else if (chartBackgroundImage != null)
            {
                Color c = chartBackgroundImage.color;
                c.a = chartOpacity;
                chartBackgroundImage.color = c;
            }
        }
        
        private void UpdateChartEdgeSoftness()
        {
            if (circularMaskMaterial != null)
            {
                circularMaskMaterial.SetFloat("_SoftEdge", chartEdgeSoftness);
            }
            
            // Also update radar overlay material to match
            if (radarOverlayMaterial != null)
            {
                radarOverlayMaterial.SetFloat("_SoftEdge", chartEdgeSoftness);
            }
        }

        private void OnTrafficUpdated(List<RadarTrafficTarget> targets)
        {
            currentTargets = targets;
        }
        
        /// <summary>
        /// Called by TrafficRadarController when targets are updated
        /// </summary>
        private void OnControllerTargetsUpdated(IReadOnlyList<RadarTarget> targets)
        {
            currentTargets.Clear();
            
            if (targets == null)
                return;
            
            foreach (var target in targets)
            {
                currentTargets.Add(new RadarTrafficTarget
                {
                    icao24 = target.Icao24,
                    callsign = target.Callsign,
                    latitude = (float)target.Latitude,
                    longitude = (float)target.Longitude,
                    altitudeFt = target.AltitudeFeet,
                    heading = target.Heading,
                    groundSpeedKts = target.GroundSpeedKnots,
                    verticalRateFpm = target.VerticalRateFpm,
                    distanceNM = target.DistanceNM,
                    bearingDeg = target.BearingDegrees,
                    relativeAltitudeFt = target.RelativeAltitudeFeet,
                    threatLevel = target.ThreatLevel,
                    radarPosition = target.RadarPosition
                });
            }
            
            Debug.Log($"[TrafficRadarDisplay] Received {currentTargets.Count} targets from controller");
        }

        private void OnChartLoaded(Texture2D chartTexture)
        {
            if (chartBackgroundImage != null && chartTexture != null)
            {
                chartBackgroundImage.texture = chartTexture;
            }
        }

        private void DrawRadar()
        {
            // Clear texture
            radarTexture.SetPixels32(clearPixels);

            int centerX = displaySize / 2;
            int centerY = displaySize / 2;
            float radius = displaySize / 2f;

            // Draw background circle only if enabled
            if (showRadarBackground)
            {
                DrawFilledCircle(centerX, centerY, (int)radius, backgroundColor);
            }

            // Draw range rings
            DrawRangeRings(centerX, centerY, radius);

            // Draw compass markings
            DrawCompassMarkings(centerX, centerY, radius);

            // Draw traffic symbols
            DrawTrafficSymbols(centerX, centerY, radius);

            // Draw own aircraft at center
            DrawOwnAircraft(centerX, centerY);

            // Apply texture changes
            radarTexture.Apply();
        }

        private void DrawRangeRings(int centerX, int centerY, float radius)
        {
            for (int i = 1; i <= rangeRingCount; i++)
            {
                float ringRadius = radius * i / rangeRingCount;
                DrawCircle(centerX, centerY, (int)ringRadius, rangeRingColor, 1);
            }
        }

        private void DrawCompassMarkings(int centerX, int centerY, float radius)
        {
            // Apply heading rotation offset for track-up mode
            float headingOffset = enableTrackUpMode ? _currentHeadingRotation : 0f;
            
            // Draw cardinal direction lines
            int[] cardinalAngles = { 0, 90, 180, 270 };
            foreach (int angle in cardinalAngles)
            {
                float adjustedAngle = angle + headingOffset;
                float rad = adjustedAngle * Mathf.Deg2Rad;
                float innerRadius = radius * 0.85f;
                
                int x1 = centerX + (int)(innerRadius * Mathf.Sin(rad));
                int y1 = centerY + (int)(innerRadius * Mathf.Cos(rad));
                int x2 = centerX + (int)(radius * Mathf.Sin(rad));
                int y2 = centerY + (int)(radius * Mathf.Cos(rad));
                
                DrawLine(x1, y1, x2, y2, compassMarkingsColor);
            }

            // Draw minor tick marks every 30 degrees
            for (int angle = 0; angle < 360; angle += 30)
            {
                if (angle % 90 == 0) continue; // Skip cardinals
                
                float adjustedAngle = angle + headingOffset;
                float rad = adjustedAngle * Mathf.Deg2Rad;
                float innerRadius = radius * 0.92f;
                
                int x1 = centerX + (int)(innerRadius * Mathf.Sin(rad));
                int y1 = centerY + (int)(innerRadius * Mathf.Cos(rad));
                int x2 = centerX + (int)(radius * Mathf.Sin(rad));
                int y2 = centerY + (int)(radius * Mathf.Cos(rad));
                
                DrawLine(x1, y1, x2, y2, new Color(compassMarkingsColor.r, compassMarkingsColor.g, compassMarkingsColor.b, 0.4f));
            }
        }

        private void DrawTrafficSymbols(int centerX, int centerY, float radius)
        {
            foreach (var target in currentTargets)
            {
                // Convert radar position (-1 to 1) to pixel position
                int x = centerX + (int)(target.radarPosition.x * radius * 0.9f);
                int y = centerY + (int)(target.radarPosition.y * radius * 0.9f);

                // Get symbol properties based on threat level
                Color symbolColor = ThreatLevelConfig.GetColor(target.threatLevel);
                SymbolType symbolType = ThreatLevelConfig.GetSymbolType(target.threatLevel);

                // Draw the appropriate symbol
                DrawSymbol(x, y, symbolType, symbolColor, (int)symbolSize);
            }
        }

        private void DrawOwnAircraft(int centerX, int centerY)
        {
            // Draw own aircraft as a red aircraft symbol pointing up
            int size = (int)(symbolSize * 1.2f);
            
            // Simple aircraft shape (triangle pointing up)
            DrawFilledTriangle(centerX, centerY + size/2, 
                               centerX - size/3, centerY - size/2,
                               centerX + size/3, centerY - size/2,
                               ownAircraftColor);
        }

        private void DrawSymbol(int x, int y, SymbolType type, Color color, int size)
        {
            switch (type)
            {
                case SymbolType.FilledSquare:
                    DrawFilledRect(x - size/2, y - size/2, size, size, color);
                    break;
                    
                case SymbolType.FilledCircle:
                    DrawFilledCircle(x, y, size/2, color);
                    break;
                    
                case SymbolType.FilledDiamond:
                    DrawFilledDiamond(x, y, size, color);
                    break;
                    
                case SymbolType.UnfilledDiamond:
                    DrawDiamond(x, y, size, color);
                    break;
            }
        }

        #region Drawing Primitives

        private void DrawFilledCircle(int cx, int cy, int radius, Color color)
        {
            Color32 c = color;
            for (int y = -radius; y <= radius; y++)
            {
                for (int x = -radius; x <= radius; x++)
                {
                    if (x*x + y*y <= radius*radius)
                    {
                        SetPixelSafe(cx + x, cy + y, c);
                    }
                }
            }
        }

        private void DrawCircle(int cx, int cy, int radius, Color color, int thickness)
        {
            Color32 c = color;
            for (int y = -radius - thickness; y <= radius + thickness; y++)
            {
                for (int x = -radius - thickness; x <= radius + thickness; x++)
                {
                    float dist = Mathf.Sqrt(x*x + y*y);
                    if (dist >= radius - thickness/2f && dist <= radius + thickness/2f)
                    {
                        SetPixelSafe(cx + x, cy + y, c);
                    }
                }
            }
        }

        private void DrawFilledRect(int x, int y, int width, int height, Color color)
        {
            Color32 c = color;
            for (int py = y; py < y + height; py++)
            {
                for (int px = x; px < x + width; px++)
                {
                    SetPixelSafe(px, py, c);
                }
            }
        }

        private void DrawFilledDiamond(int cx, int cy, int size, Color color)
        {
            Color32 c = color;
            int halfSize = size / 2;
            for (int y = -halfSize; y <= halfSize; y++)
            {
                int xWidth = halfSize - Mathf.Abs(y);
                for (int x = -xWidth; x <= xWidth; x++)
                {
                    SetPixelSafe(cx + x, cy + y, c);
                }
            }
        }

        private void DrawDiamond(int cx, int cy, int size, Color color)
        {
            Color32 c = color;
            int halfSize = size / 2;
            
            // Draw outline only
            DrawLine(cx, cy + halfSize, cx + halfSize, cy, c);
            DrawLine(cx + halfSize, cy, cx, cy - halfSize, c);
            DrawLine(cx, cy - halfSize, cx - halfSize, cy, c);
            DrawLine(cx - halfSize, cy, cx, cy + halfSize, c);
        }

        private void DrawFilledTriangle(int x1, int y1, int x2, int y2, int x3, int y3, Color color)
        {
            // Simple triangle fill using scanline
            Color32 c = color;
            
            int minY = Mathf.Min(y1, Mathf.Min(y2, y3));
            int maxY = Mathf.Max(y1, Mathf.Max(y2, y3));
            int minX = Mathf.Min(x1, Mathf.Min(x2, x3));
            int maxX = Mathf.Max(x1, Mathf.Max(x2, x3));

            for (int py = minY; py <= maxY; py++)
            {
                for (int px = minX; px <= maxX; px++)
                {
                    if (PointInTriangle(px, py, x1, y1, x2, y2, x3, y3))
                    {
                        SetPixelSafe(px, py, c);
                    }
                }
            }
        }

        private bool PointInTriangle(int px, int py, int x1, int y1, int x2, int y2, int x3, int y3)
        {
            float d1 = Sign(px, py, x1, y1, x2, y2);
            float d2 = Sign(px, py, x2, y2, x3, y3);
            float d3 = Sign(px, py, x3, y3, x1, y1);

            bool hasNeg = (d1 < 0) || (d2 < 0) || (d3 < 0);
            bool hasPos = (d1 > 0) || (d2 > 0) || (d3 > 0);

            return !(hasNeg && hasPos);
        }

        private float Sign(int px, int py, int x1, int y1, int x2, int y2)
        {
            return (px - x2) * (y1 - y2) - (x1 - x2) * (py - y2);
        }

        private void DrawLine(int x1, int y1, int x2, int y2, Color color)
        {
            Color32 c = color;
            
            int dx = Mathf.Abs(x2 - x1);
            int dy = Mathf.Abs(y2 - y1);
            int sx = x1 < x2 ? 1 : -1;
            int sy = y1 < y2 ? 1 : -1;
            int err = dx - dy;

            while (true)
            {
                SetPixelSafe(x1, y1, c);

                if (x1 == x2 && y1 == y2) break;

                int e2 = 2 * err;
                if (e2 > -dy)
                {
                    err -= dy;
                    x1 += sx;
                }
                if (e2 < dx)
                {
                    err += dx;
                    y1 += sy;
                }
            }
        }

        private void SetPixelSafe(int x, int y, Color32 color)
        {
            if (x >= 0 && x < displaySize && y >= 0 && y < displaySize)
            {
                radarTexture.SetPixel(x, y, color);
            }
        }

        #endregion

        private void UpdateRangeLabel()
        {
            if (rangeLabel != null)
            {
                rangeLabel.text = $"{rangeNM:F0} NM";
            }
        }

        #endregion
    }
}
