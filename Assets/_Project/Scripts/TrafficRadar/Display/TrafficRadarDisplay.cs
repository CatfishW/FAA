using System.Collections;
using System.Collections.Generic;
using System;
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
        [SerializeField] private int displaySize = 512;
        
        [Tooltip("Show FAA sectional chart as background")]
        [SerializeField] private bool showChartBackground = true;
        
        [Tooltip("Chart background opacity")]
        [Range(0f, 1f)]
        [SerializeField] private float chartOpacity = 0.28f;
        
        [Tooltip("Edge softness for circular chart mask (0 = hard edge, 0.1 = soft edge)")]
        [Range(0f, 0.1f)]
        [SerializeField] private float chartEdgeSoftness = 0.035f;

        [Header("Chart Presentation")]
        [Tooltip("Fade the sectional chart in and out so a pilot never loses the traffic picture abruptly.")]
        [SerializeField] private bool enableChartFadeAnimation = true;

        [Tooltip("Duration of the sectional chart fade in seconds.")]
        [Min(0f)]
        [SerializeField] private float chartFadeDuration = 0.24f;

        [Tooltip("How often to retry chart positioning while the simulator is still publishing own-ship coordinates.")]
        [Min(0.1f)]
        [SerializeField] private float chartPositionRetrySeconds = 0.75f;

        [Header("Map Interaction")]
        [Tooltip("Allow pilots to drag the sectional/chart layer while the map is focused.")]
        [SerializeField] private bool enableMapPanning = true;

        [Tooltip("Maximum map drag distance as a fraction of the radar footprint.")]
        [Range(0.05f, 0.9f)]
        [SerializeField] private float maxMapPanFraction = 0.36f;

        [Tooltip("Smooth map drag settling speed. Set to zero for immediate movement.")]
        [Min(0f)]
        [SerializeField] private float mapPanSmoothing = 22f;

        [Header("Pilot Focus Mode")]
        [Tooltip("Allow the traffic radar to expand into a centered, immersive map view.")]
        [SerializeField] private bool enableFullscreenMode = true;

        [Tooltip("Inset from the edge of the XR-3 view while the radar is maximized.")]
        [Min(0f)]
        [SerializeField] private float fullscreenMargin = 42f;

        [Tooltip("Animate the radar into and out of the maximized view.")]
        [SerializeField] private bool animateFullscreenTransition = true;

        [Tooltip("Duration of the maximized-view transition in seconds.")]
        [Min(0f)]
        [SerializeField] private float fullscreenTransitionDuration = 0.24f;

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

        [Header("Pilot Linework")]
        [Tooltip("Use a clear hierarchy of major/minor range rings and bearing ticks for quick pilot scanability.")]
        [SerializeField] private bool usePilotLinework = true;

        [Tooltip("Scale the line strokes with the generated radar texture so fullscreen and headset views stay crisp.")]
        [Range(0.5f, 3f)]
        [SerializeField] private float pilotLineworkScale = 1f;

        [Tooltip("Show a small, low-contrast cardinal bearing cue inside the perimeter ticks.")]
        [SerializeField] private bool showCardinalBearingCues = true;

        [Tooltip("Show the 15-degree secondary bearing ticks. Disable for a cleaner scan at wide ranges.")]
        [SerializeField] private bool showMinorBearingTicks;

        [Tooltip("Inset for the primary perimeter stroke so it remains readable inside the softened circular mask.")]
        [Min(2f)]
        [SerializeField] private float perimeterInsetPixels = 9f;

        [Tooltip("Cardinal label size in the compact HUD footprint.")]
        [Min(8f)]
        [SerializeField] private float compactCompassFontSize = 16f;

        [Tooltip("Cardinal label size in the maximized pilot-focus view.")]
        [Min(10f)]
        [SerializeField] private float fullscreenCompassFontSize = 24f;

        [Tooltip("Fraction of the radar diameter used for cardinal labels in the maximized view.")]
        [Range(0.34f, 0.46f)]
        [SerializeField] private float fullscreenCompassLabelRadius = 0.42f;
        
        [Header("Zoom Animation")]
        [Tooltip("Enable smooth zoom animation")]
        [SerializeField] private bool enableSmoothZoom = true;
        
        [Tooltip("Animation duration in seconds")]
        [SerializeField] private float zoomAnimationDuration = 0.3f;

        [Header("Visual Settings")]
        [Tooltip("Show radar background circle (disable to show only chart)")]
        [SerializeField] private bool showRadarBackground = true;

        [Tooltip("Keep the traffic radar readable over bright terrain by enforcing a minimum circular backdrop opacity.")]
        [SerializeField] private bool enforceReadablePanelBackground;

        [Tooltip("Minimum opacity for the traffic radar panel background.")]
        [Range(0f, 1f)]
        [SerializeField] private float minimumPanelBackgroundOpacity;

        [Tooltip("Minimum opacity for FAA chart texture backgrounds.")]
        [Range(0f, 1f)]
        [SerializeField] private float minimumChartBackgroundOpacity;

        [Header("X-Plane Traffic Texture")]
        [Tooltip("Show the live X-Plane traffic radar PNG directly instead of reconstructing traffic symbols in Unity.")]
        [SerializeField] private bool preferXPlaneTrafficTexture = false;

        [Tooltip("Hide Unity-generated rings, chart, range labels, and compass labels while the X-Plane texture is the selected source.")]
        [SerializeField] private bool hideGeneratedOverlaysWithXPlaneTexture = true;

        [Tooltip("Fallback aspect used before the first X-Plane traffic PNG arrives.")]
        [SerializeField] private Vector2 xPlaneTextureFallbackSize = new Vector2(420f, 480f);
        
        [SerializeField] private Color backgroundColor = new Color(0.004f, 0.055f, 0.06f, 0.34f);
        [SerializeField] private Color rangeRingColor = new Color(0.18f, 0.9f, 0.84f, 0.58f);
        [SerializeField] private Color compassMarkingsColor = new Color(0.74f, 1f, 0.95f, 0.88f);
        [SerializeField] private Color ownAircraftColor = new Color(0.35f, 1f, 0.55f, 1f);

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

        [Header("Debug")]
        [SerializeField] private bool verboseLogging = false;
        
        // Runtime-created material for radar overlay (full opacity)
        private Material radarOverlayMaterial;

        // Internal textures
        private Texture2D radarTexture;
        private Texture _xPlaneTrafficTexture;
        private Texture2D _blackPlaceholder;
        private RectTransform rectTransform;
        private List<RadarTrafficTarget> currentTargets = new List<RadarTrafficTarget>();

        // Symbol drawing
        private Color32[] clearPixels;
        private Color32[] drawPixels;
        
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
        private bool _radarTextureDirty = true;
        private float _lastDrawnHeadingRotation = float.NaN;

        // Sectional chart presentation/fetch state.  The chart child remains
        // active in the hierarchy so CHT can be used at runtime even when the
        // authored RawImage was disabled in the scene.  Its graphic is faded
        // independently of the traffic symbols to preserve legibility.
        private float _chartVisualOpacity;
        private float _chartFadeFromOpacity;
        private float _chartFadeToOpacity;
        private float _chartFadeStartTime;
        private bool _chartFadeAnimating;
        private float _nextChartPositionRetryTime;
        private bool _chartFetchRequested;
        private float _lastChartRequestLat = float.NaN;
        private float _lastChartRequestLon = float.NaN;
        private float _lastChartRequestRange = float.NaN;

        // Pilot-focus map interaction state.  The chart is moved relative to
        // the fixed traffic scope so dragging never displaces the REST/FULL
        // affordance or clips the circular mask.  Keeping target/current
        // values separate gives pointer and XR drags a polished, low-jitter
        // settle without blocking input.
        private Vector2 _mapPan;
        private Vector2 _mapPanTarget;
        // While a pilot is inspecting the chart, the own-ship glyph is a
        // moving reference that obscures the map beneath the scope. Keep the
        // suppression transient so the normal centered radar presentation is
        // restored as soon as the drag is released.
        private bool _mapDragActive;
        private Vector2 _chartBaseAnchoredPosition;
        private Vector2 _chartBaseSizeDelta;
        private RectTransform _chartBaseRect;
        private bool _chartBaseLayoutStored;
        private const float MapCoverageSafetyPixels = 8f;
        private const string CircularMaskCenterProperty = "_MaskCenter";
        private const string CircularMaskRadiusProperty = "_MaskRadius";
        private const string CircularMaskFixedProperty = "_UseFixedMask";

        // Pilot focus/fullscreen layout state.  The radar root stays in the
        // scene hierarchy (rather than cloning or reparenting the display), so
        // controller, chart provider, and XR interaction references remain
        // valid while the map is maximized.
        private bool _isFullscreen;
        private RectTransform _fullscreenRoot;
        private Transform _fullscreenOriginalParent;
        private int _fullscreenOriginalSiblingIndex;
        private Vector2 _fullscreenOriginalAnchorMin;
        private Vector2 _fullscreenOriginalAnchorMax;
        private Vector2 _fullscreenOriginalPivot;
        private Vector2 _fullscreenOriginalAnchoredPosition;
        private Vector2 _fullscreenOriginalSizeDelta;
        private Vector3 _fullscreenOriginalLocalPosition;
        private Quaternion _fullscreenOriginalLocalRotation;
        private Vector3 _fullscreenOriginalScale;
        private bool _fullscreenLayoutStored;
        private bool _fullscreenRestorePending;
        private Coroutine _fullscreenTransition;
        private bool _fullscreenTransitionIsExit;
        private bool _updatingFullscreenLayout;

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
                    MarkRadarDirty();
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
                float previous = chartOpacity;
                chartOpacity = Mathf.Clamp01(value);
                BeginChartFade(showChartBackground && !preferXPlaneTrafficTexture ? chartOpacity : 0f, true);
                if (!Mathf.Approximately(previous, chartOpacity))
                {
                    ChartOpacityChanged?.Invoke(chartOpacity);
                }
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
            set
            {
                if (showRadarBackground == value)
                {
                    return;
                }

                showRadarBackground = value;
                MarkRadarDirty();
            }
        }

        public bool ChartBackgroundVisible => showChartBackground;
        public FAASectionalChartProvider ChartProvider => chartProvider;
        public int RangeRingCount => rangeRingCount;

        /// <summary>
        /// Current chart drag offset in display-local pixels.
        /// </summary>
        public Vector2 MapPan => _mapPanTarget;

        /// <summary>
        /// Whether a pilot is currently dragging the chart in the focused
        /// traffic radar.  This is intentionally runtime-only state; it must
        /// never persist into the compact HUD or a scene reload.
        /// </summary>
        public bool IsMapDragging => _mapDragActive;

        /// <summary>
        /// Whether own-ship symbology is currently painted into the generated
        /// radar texture. It is false only for the transient chart-drag
        /// interval (or while the component is disabled).
        /// </summary>
        public bool OwnAircraftOverlayVisible => !_mapDragActive;

        public bool MapPanningEnabled => enableMapPanning;

        public FAAChartMapSource MapSource => chartProvider != null
            ? chartProvider.MapSource
            : FAAChartMapSource.Sectional;

        public string MapSourceName => chartProvider != null
            ? chartProvider.MapSourceName
            : "SECTIONAL";

        /// <summary>
        /// Whether the traffic radar is currently in the centered pilot-focus view.
        /// The map remains the same scene object, so chart, traffic, and XR pointer
        /// references continue to work while it is maximized.
        /// </summary>
        public bool IsFullscreen => _isFullscreen;

        /// <summary>
        /// Raised after the radar enters or leaves pilot-focus view.
        /// </summary>
        public event Action<bool> FullscreenChanged;

        /// <summary>
        /// Raised when the chart opacity target changes.
        /// </summary>
        public event Action<float> ChartOpacityChanged;

        /// <summary>
        /// Raised when the map source changes.
        /// </summary>
        public event Action<FAAChartMapSource> MapSourceChanged;

        /// <summary>
        /// Raised when a drag/pan settles at a new map offset.
        /// </summary>
        public event Action<Vector2> MapPanChanged;

        /// <summary>
        /// Gets or sets the radar background color.
        /// </summary>
        public Color BackgroundColor
        {
            get => backgroundColor;
            set
            {
                if (backgroundColor == value)
                {
                    return;
                }

                backgroundColor = value;
                MarkRadarDirty();
            }
        }
        
        /// <summary>
        /// Gets or sets the range ring color.
        /// </summary>
        public Color RangeRingColor
        {
            get => rangeRingColor;
            set
            {
                if (rangeRingColor == value)
                {
                    return;
                }

                rangeRingColor = value;
                MarkRadarDirty();
            }
        }
        
        /// <summary>
        /// Gets or sets the compass markings color.
        /// </summary>
        public Color CompassMarkingsColor
        {
            get => compassMarkingsColor;
            set
            {
                if (compassMarkingsColor == value)
                {
                    return;
                }

                compassMarkingsColor = value;
                MarkRadarDirty();
            }
        }
        
        /// <summary>
        /// Gets or sets the own aircraft symbol color.
        /// </summary>
        public Color OwnAircraftColor
        {
            get => ownAircraftColor;
            set
            {
                if (ownAircraftColor == value)
                {
                    return;
                }

                ownAircraftColor = value;
                MarkRadarDirty();
            }
        }

        public bool TrackUpModeEnabled
        {
            get => enableTrackUpMode;
            set
            {
                enableTrackUpMode = value;
                if (!enableTrackUpMode)
                {
                    ResetHeadingPresentation();
                }
                else
                {
                    MarkRadarDirty();
                }
            }
        }

        public bool PreferXPlaneTrafficTexture
        {
            get => preferXPlaneTrafficTexture;
            set
            {
                if (preferXPlaneTrafficTexture == value)
                {
                    return;
                }

                preferXPlaneTrafficTexture = value;
                SetupDisplay();
                MarkRadarDirty();
            }
        }

        public bool UsesXPlaneTrafficTexture => preferXPlaneTrafficTexture;
        public Texture XPlaneTrafficTexture => _xPlaneTrafficTexture;
        public RawImage RadarImage => radarImage;

        public void ConfigureHudPresentation(float panelOpacity, float requestedChartOpacity)
        {
            // The circular render itself provides contrast. A separate opaque
            // square is unnecessary and blocks the pilot's outside view.
            enforceReadablePanelBackground = false;
            minimumPanelBackgroundOpacity = 0f;
            minimumChartBackgroundOpacity = 0f;
            showRadarBackground = true;
            backgroundColor = new Color(0.004f, 0.055f, 0.06f, Mathf.Clamp(panelOpacity, 0.12f, 0.55f));
            chartOpacity = Mathf.Clamp(requestedChartOpacity, 0.1f, 0.48f);
            rangeRingColor = new Color(0.18f, 0.9f, 0.84f, 0.58f);
            compassMarkingsColor = new Color(0.74f, 1f, 0.95f, 0.88f);
            ownAircraftColor = new Color(0.35f, 1f, 0.55f, 1f);
            ClearRectangularMaskPlate();
            BeginChartFade(showChartBackground && !preferXPlaneTrafficTexture ? chartOpacity : 0f, false);
            MarkRadarDirty();
        }

        private void ClearRectangularMaskPlate()
        {
            foreach (Mask mask in GetComponents<Mask>())
            {
                if (mask != null)
                {
                    mask.showMaskGraphic = false;
                }
            }

            Image panelImage = GetComponent<Image>();
            if (panelImage != null)
            {
                Color color = panelImage.color;
                color.a = 0f;
                panelImage.color = color;
                panelImage.raycastTarget = false;
            }
        }

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            _chartVisualOpacity = showChartBackground ? Mathf.Clamp01(chartOpacity) : 0f;
            NormalizePanelReadability();
            CreateRadarTexture();
            EnsureRadarImageReference();
            EnsureChartImageReference();
            ApplyMapPanVisual(true);
        }

        private void OnEnable()
        {
            // If the radar was disabled while its parent Canvas was also being
            // deactivated, the original layout is restored on the first safe
            // enable pass instead of touching an inactive hierarchy.
            if (_fullscreenRestorePending)
            {
                RestoreFullscreenLayout(false, true, false);
            }

            NormalizePanelReadability();
            EnsureRuntimeDisplayReady();

            // Subscribe to controller
            if (radarController != null)
            {
                radarController.OnTargetsUpdated.AddListener(OnControllerTargetsUpdated);
            }

            SubscribeToChartProvider();
            ApplyMapPanVisual(true);
        }

        private void OnDisable()
        {
            // A scene reload, prefab disable, or XR mode switch must never leave
            // the shared radar root in its transient focus layout.
            // Do not force canvases or rebuild textures while Unity is tearing
            // down the hierarchy; defer the restore if the parent is inactive.
            RestoreFullscreenLayout(false, false, true);

            // Pointer cancellation and XR mode switches can disable the
            // display without delivering IEndDrag to the sibling surface.
            // Clear the transient suppression so a subsequent enable always
            // redraws the own-ship glyph.
            _mapDragActive = false;

            // Unsubscribe from controller
            if (radarController != null)
            {
                radarController.OnTargetsUpdated.RemoveListener(OnControllerTargetsUpdated);
            }

            if (chartProvider != null)
            {
                chartProvider.OnChartTileLoaded -= OnChartLoaded;
                chartProvider.OnMapSourceChanged -= OnChartMapSourceChanged;
            }
        }

        private void Start()
        {
            // Auto-find controller
            if (radarController == null)
                radarController = FindAnyObjectByType<TrafficRadarController>();

            if (chartProvider == null)
                chartProvider = FindAnyObjectByType<FAASectionalChartProvider>();

            SubscribeToChartProvider();

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
            // Initial chart fetch.  The X-Plane bridge may publish own-ship
            // coordinates one or two frames after Start, so this method also
            // retries from Update until a valid position is available.
            TryFetchChartForCurrentPosition(true);
            
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

            ApplyPilotLabelStyle();
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

        private void SubscribeToChartProvider()
        {
            if (chartProvider != null)
            {
                // Remove first so a provider discovered in Start cannot be
                // subscribed twice when the component was already enabled.
                chartProvider.OnChartTileLoaded -= OnChartLoaded;
                chartProvider.OnChartTileLoaded += OnChartLoaded;
                chartProvider.OnMapSourceChanged -= OnChartMapSourceChanged;
                chartProvider.OnMapSourceChanged += OnChartMapSourceChanged;
            }
        }

        private void EnsureChartImageReference()
        {
            if (chartBackgroundImage == null)
            {
                foreach (RawImage image in GetComponentsInChildren<RawImage>(true))
                {
                    if (image == null || image == radarImage)
                    {
                        continue;
                    }

                    string imageName = image.gameObject.name;
                    if (imageName.IndexOf("chart", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        imageName.IndexOf("sectional", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        chartBackgroundImage = image;
                        break;
                    }
                }
            }

            if (chartBackgroundImage == null)
            {
                foreach (RawImage image in GetComponentsInChildren<RawImage>(true))
                {
                    if (image != null && image != radarImage)
                    {
                        chartBackgroundImage = image;
                        break;
                    }
                }
            }

            if (chartBackgroundImage != null)
            {
                // Keep the GameObject alive even when the graphic is hidden.
                // A disabled parent was the reason the CHT control appeared to
                // do nothing in the XR-3 simulator scene.
                chartBackgroundImage.gameObject.SetActive(true);
                chartBackgroundImage.raycastTarget = false;
                StoreChartBaseLayout(chartBackgroundImage.rectTransform);
            }
        }

        private bool TryResolveChartPosition(out float latitude, out float longitude)
        {
            latitude = 0f;
            longitude = 0f;

            if (radarController != null && IsValidGeoPosition(
                    radarController.OwnPosition.Latitude,
                    radarController.OwnPosition.Longitude))
            {
                latitude = (float)radarController.OwnPosition.Latitude;
                longitude = (float)radarController.OwnPosition.Longitude;
                return true;
            }

            // During simulator startup the controller can still contain its
            // zero-value position while the data manager already has the
            // authored/reference airport location.  Use that location for
            // chart tiles and let the bridge replace it when live data arrives.
            TrafficRadarDataManager manager = radarController != null
                ? radarController.GetComponentInChildren<TrafficRadarDataManager>(true)
                : null;
            if (manager == null)
            {
                manager = FindAnyObjectByType<TrafficRadarDataManager>();
            }

            if (manager != null && IsValidGeoPosition(manager.referenceLatitude, manager.referenceLongitude))
            {
                latitude = manager.referenceLatitude;
                longitude = manager.referenceLongitude;
                return true;
            }

            return false;
        }

        private static bool IsValidGeoPosition(double latitude, double longitude)
        {
            return !double.IsNaN(latitude) && !double.IsInfinity(latitude) &&
                   !double.IsNaN(longitude) && !double.IsInfinity(longitude) &&
                   latitude >= -90d && latitude <= 90d &&
                   longitude >= -180d && longitude <= 180d &&
                   (Math.Abs(latitude) > 0.00001d || Math.Abs(longitude) > 0.00001d);
        }

        private bool TryFetchChartForCurrentPosition(bool force)
        {
            if (!showChartBackground || preferXPlaneTrafficTexture || chartProvider == null)
            {
                return false;
            }

            if (!TryResolveChartPosition(out float latitude, out float longitude))
            {
                if (Time.unscaledTime >= _nextChartPositionRetryTime)
                {
                    _nextChartPositionRetryTime = Time.unscaledTime + Mathf.Max(0.1f, chartPositionRetrySeconds);
                }
                return false;
            }

            bool positionChanged = !_chartFetchRequested ||
                Mathf.Abs(latitude - _lastChartRequestLat) > 0.02f ||
                Mathf.Abs(longitude - _lastChartRequestLon) > 0.02f ||
                !Mathf.Approximately(rangeNM, _lastChartRequestRange);

            if (!force && !positionChanged)
            {
                return true;
            }

            if (!force && chartProvider.IsLoading)
            {
                return true;
            }

            chartProvider.FetchChartTiles(latitude, longitude, rangeNM);
            _lastChartRequestLat = latitude;
            _lastChartRequestLon = longitude;
            _lastChartRequestRange = rangeNM;
            _chartFetchRequested = true;
            _nextChartPositionRetryTime = Time.unscaledTime + Mathf.Max(0.1f, chartPositionRetrySeconds);
            return true;
        }

        private void BeginChartFade(float targetOpacity, bool animate)
        {
            _chartFadeToOpacity = Mathf.Clamp01(targetOpacity);
            if (!animate || !enableChartFadeAnimation || chartFadeDuration <= 0.001f || !Application.isPlaying)
            {
                _chartVisualOpacity = _chartFadeToOpacity;
                _chartFadeAnimating = false;
                ApplyChartVisualOpacity();
                return;
            }

            _chartFadeFromOpacity = _chartVisualOpacity;
            _chartFadeStartTime = Time.unscaledTime;
            _chartFadeAnimating = true;
            ApplyChartVisualOpacity();
        }

        private void UpdateChartFade()
        {
            if (!_chartFadeAnimating)
            {
                return;
            }

            float duration = Mathf.Max(0.001f, chartFadeDuration);
            float progress = Mathf.Clamp01((Time.unscaledTime - _chartFadeStartTime) / duration);
            // Smooth-step keeps the transition polished without adding a
            // distracting pulse over the pilot's traffic symbols.
            float eased = progress * progress * (3f - 2f * progress);
            _chartVisualOpacity = Mathf.Lerp(_chartFadeFromOpacity, _chartFadeToOpacity, eased);
            ApplyChartVisualOpacity();

            if (progress >= 1f)
            {
                _chartFadeAnimating = false;
            }
        }

        private void OnDestroy()
        {
            // OnDisable runs before destruction and handles any safe layout
            // restore.  Do not touch parent/sibling transforms from OnDestroy:
            // Unity may already be tearing the hierarchy down at this point.
            ClearFullscreenState(false);

            if (radarTexture != null)
                Destroy(radarTexture);

            if (_blackPlaceholder != null)
                Destroy(_blackPlaceholder);
            
            // Clean up runtime-created materials
            if (circularMaskMaterial != null && circularMaskMaterial.name.Contains("_Runtime"))
                Destroy(circularMaskMaterial);
            
            if (radarOverlayMaterial != null)
                Destroy(radarOverlayMaterial);
        }

        private void Update()
        {
            EnsureRuntimeDisplayReady();

            UpdateChartFade();
            TryFetchChartForCurrentPosition(false);

            // Handle zoom animation
            if (isAnimatingZoom)
            {
                UpdateZoomAnimation();
            }
            
            // Handle heading rotation (track-up mode)
            if (enableTrackUpMode)
            {
                if (!preferXPlaneTrafficTexture)
                {
                    UpdateHeadingRotation();
                }
            }
            
            DrawRadarIfNeeded();
            UpdateMapPan();
        }

        private void NormalizePanelReadability()
        {
            if (!enforceReadablePanelBackground)
            {
                return;
            }

            // Respect the pilot's BKG/CLR control. Readability enforcement only
            // applies while the circular backdrop is intentionally enabled.
            if (!showRadarBackground)
            {
                return;
            }

            bool changed = false;

            float minimumPanelAlpha = Mathf.Clamp01(minimumPanelBackgroundOpacity);
            if (backgroundColor.a < minimumPanelAlpha)
            {
                backgroundColor.a = minimumPanelAlpha;
                changed = true;
            }

            float minimumChartAlpha = Mathf.Clamp01(minimumChartBackgroundOpacity);
            if (chartOpacity < minimumChartAlpha)
            {
                chartOpacity = minimumChartAlpha;
                UpdateChartOpacity();
            }

            if (changed)
            {
                MarkRadarDirty();
            }
        }

        private void EnsureRuntimeDisplayReady()
        {
            NormalizePanelReadability();

            if (rectTransform == null)
            {
                rectTransform = GetComponent<RectTransform>();
            }

            if (radarTexture == null || clearPixels == null || clearPixels.Length != displaySize * displaySize)
            {
                CreateRadarTexture();
                MarkRadarDirty();
            }

            EnsureRadarImageReference();
            EnsureChartImageReference();
            if (radarImage == null)
            {
                return;
            }

            if (preferXPlaneTrafficTexture)
            {
                ApplyXPlaneTrafficTexture();
                return;
            }

            if (radarImage.texture == radarTexture && radarImage.enabled && radarImage.color.a > 0.99f)
            {
                return;
            }

            SetupDisplay();
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
            // Track-up can be switched off while the last heading update is
            // still visible. Reset every rotated presentation element here so
            // north-up ticks, labels, and the chart never drift out of sync.
            if (!enableTrackUpMode)
            {
                ResetHeadingPresentation();
                return;
            }

            if (radarController == null) return;
            
            float heading = radarController.OwnPosition.HeadingDegrees;
            
            // Target rotation: negative heading so heading points up
            _targetHeadingRotation = -heading;
            
            // Smooth rotation using lerp
            _currentHeadingRotation = Mathf.LerpAngle(_currentHeadingRotation, _targetHeadingRotation, 
                Time.deltaTime * headingRotationSpeed);
            if (float.IsNaN(_lastDrawnHeadingRotation) ||
                Mathf.Abs(Mathf.DeltaAngle(_lastDrawnHeadingRotation, _currentHeadingRotation)) > 0.2f)
            {
                MarkRadarDirty();
            }
            
            // Rotate compass ticks container as a whole
            if (compassTicksContainer != null)
            {
                compassTicksContainer.localRotation = Quaternion.Euler(0, 0, _currentHeadingRotation);
            }
            
            // Rotate compass labels around center, keeping text upright.
            PositionCompassLabels(_currentHeadingRotation);
            
            // Also rotate the chart background image
            if (chartBackgroundImage != null)
            {
                RectTransform chartRect = chartBackgroundImage.GetComponent<RectTransform>();
                if (chartRect != null)
                {
                    chartRect.localRotation = Quaternion.Euler(0, 0, _currentHeadingRotation);
                    UpdateChartMaskParameters();
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
                // The focused toolbar docks just above the scope. Pull the
                // cardinal labels slightly inside the perimeter in that mode
                // so the north cue cannot be hidden behind the toolbar while
                // keeping the compact HUD labels on their authored track.
                float labelRadiusFactor = IsFullscreen
                    ? Mathf.Clamp(fullscreenCompassLabelRadius, 0.34f, 0.46f)
                    : 0.45f;
                float diameter = Mathf.Min(size.x, size.y);
                float halfExtent = diameter * 0.5f;
                float labelInset = IsFullscreen
                    ? Mathf.Max(28f, fullscreenCompassFontSize * 0.9f)
                    : Mathf.Max(16f, compactCompassFontSize * 0.9f);
                // The serialized factor is expressed against the diameter so
                // the compact and fullscreen layouts use the same visual
                // language.  Applying it to the radius would pull labels
                // into the inner traffic gates and make the scope read like
                // four unrelated annotations.
                return Mathf.Min(diameter * labelRadiusFactor, Mathf.Max(0f, halfExtent - labelInset));
            }
            return displaySize * (IsFullscreen ? fullscreenCompassLabelRadius : 0.45f);
        }

        private void ResetHeadingPresentation()
        {
            bool changed = !Mathf.Approximately(_currentHeadingRotation, 0f) ||
                           !Mathf.Approximately(_targetHeadingRotation, 0f);
            _currentHeadingRotation = 0f;
            _targetHeadingRotation = 0f;
            if (compassTicksContainer != null)
            {
                compassTicksContainer.localRotation = Quaternion.identity;
            }

            if (chartBackgroundImage != null)
            {
                RectTransform chartRect = chartBackgroundImage.GetComponent<RectTransform>();
                if (chartRect != null)
                {
                    chartRect.localRotation = Quaternion.identity;
                }
            }

            PositionCompassLabels(0f);
            if (changed)
            {
                MarkRadarDirty();
            }
            UpdateChartMaskParameters();
        }

        private void PositionCompassLabels(float headingRotation)
        {
            if (_compassLabelRects == null || _compassLabelRects.Length == 0)
            {
                return;
            }

            float[] baseAngles = { 0f, 90f, 180f, 270f };
            float radius = GetCompassLabelRadius();
            for (int i = 0; i < _compassLabelRects.Length && i < 4; i++)
            {
                RectTransform labelRect = _compassLabelRects[i];
                if (labelRect == null)
                {
                    continue;
                }

                labelRect.anchorMin = new Vector2(0.5f, 0.5f);
                labelRect.anchorMax = new Vector2(0.5f, 0.5f);
                labelRect.pivot = new Vector2(0.5f, 0.5f);
                float radians = (baseAngles[i] + headingRotation) * Mathf.Deg2Rad;
                labelRect.anchoredPosition = new Vector2(
                    Mathf.Sin(radians) * radius,
                    Mathf.Cos(radians) * radius);
                labelRect.localRotation = Quaternion.identity;
            }
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
            TryFetchChartForCurrentPosition(false);
        }

        /// <summary>
        /// Toggle chart background visibility.
        /// </summary>
        public void ToggleChartBackground()
        {
            SetChartBackgroundVisible(!showChartBackground, true);
        }

        /// <summary>
        /// Toggle the traffic radar between its normal HUD footprint and a
        /// centered, maximized pilot-focus view.
        /// </summary>
        public void ToggleFullscreen()
        {
            SetFullscreen(!_isFullscreen, true);
        }

        /// <summary>
        /// Expand or restore the complete radar root.  The root is laid out
        /// inside the existing Canvas instead of creating a second camera or
        /// display, which keeps chart tiles, traffic symbols, and XR pointer
        /// interaction live throughout the transition.
        /// </summary>
        public void SetFullscreen(bool fullscreen, bool animate = true)
        {
            if (fullscreen)
            {
                // A pilot can press FULL again while REST is still animating.
                // Reverse the in-flight transition instead of leaving the map
                // in a half-sized state until the next button refresh.
                if (_isFullscreen && _fullscreenTransition != null && _fullscreenTransitionIsExit)
                {
                    ReverseFullscreenEnter(animate);
                    return;
                }

                EnterFullscreen(animate);
            }
            else
            {
                ExitFullscreen(animate);
            }
        }

        private void EnterFullscreen(bool animate)
        {
            if (_isFullscreen)
            {
                return;
            }

            if (!enableFullscreenMode)
            {
                Debug.LogWarning("[TrafficRadarDisplay] Pilot-focus mode is disabled on this display.");
                return;
            }

            RectTransform root = ResolveFullscreenRoot();
            RectTransform canvasRect = ResolveCanvasRect(root);
            if (root == null || canvasRect == null)
            {
                Debug.LogWarning("[TrafficRadarDisplay] Could not resolve a Canvas for pilot-focus mode.");
                return;
            }

            CaptureFullscreenLayout(root);
            if (_fullscreenTransition != null)
            {
                StopCoroutine(_fullscreenTransition);
                _fullscreenTransition = null;
            }

            // A nested canvas is valid too; move only when needed and restore
            // the original parent on exit.  In the FAA scene this is a no-op,
            // because the traffic system is already a direct Canvas child.
            if (root.parent != canvasRect)
            {
                root.SetParent(canvasRect, false);
            }

            ApplyFullscreenLayout(root, canvasRect);

            _fullscreenRoot = root;
            _isFullscreen = true;
            Canvas.ForceUpdateCanvases();
            SetupDisplay();
            MarkRadarDirty();
            FullscreenChanged?.Invoke(true);
            ApplyPilotLabelStyle();
            PositionCompassLabels(_currentHeadingRotation);

            if (ShouldAnimateFullscreen(animate))
            {
                Vector3 targetScale = _fullscreenOriginalScale;
                Vector3 startScale = targetScale * 0.92f;
                root.localScale = startScale;
                _fullscreenTransitionIsExit = false;
                _fullscreenTransition = StartCoroutine(AnimateFullscreenScale(root, startScale, targetScale));
            }
        }

        private void OnRectTransformDimensionsChange()
        {
            if (!_isFullscreen || _fullscreenRoot == null || _updatingFullscreenLayout ||
                _fullscreenRestorePending || !isActiveAndEnabled)
            {
                return;
            }

            RectTransform canvasRect = ResolveCanvasRect(_fullscreenRoot);
            if (canvasRect != null)
            {
                ApplyFullscreenLayout(_fullscreenRoot, canvasRect);
                ApplyPilotLabelStyle();
                PositionCompassLabels(_currentHeadingRotation);
            }
        }

        private void ApplyFullscreenLayout(RectTransform root, RectTransform canvasRect)
        {
            if (root == null || canvasRect == null || _updatingFullscreenLayout)
            {
                return;
            }

            _updatingFullscreenLayout = true;
            try
            {
                // Layout groups and XR CanvasScaler values may have settled
                // only one frame before the button press or a display change.
                Canvas.ForceUpdateCanvases();
                Vector2 canvasSize = canvasRect.rect.size;
                if (canvasSize.x <= 1f || canvasSize.y <= 1f)
                {
                    canvasSize = canvasRect.sizeDelta;
                }

                if (canvasSize.x <= 1f || canvasSize.y <= 1f)
                {
                    canvasSize = new Vector2(Screen.width, Screen.height);
                }

                float margin = Mathf.Max(0f, fullscreenMargin);
                float availableWidth = Mathf.Max(1f, canvasSize.x - margin * 2f);
                // Reserve a small band for the sibling traffic strip. Without
                // this, a square that reaches the top of a 16:9 Canvas pushes
                // REST/CHT controls into the clipped XR view.
                float controlsReserve = 0f;
                Transform controls = canvasRect.Find("TrafficControlStrip");
                if (controls != null)
                {
                    RectTransform controlsRect = controls as RectTransform ?? controls.GetComponent<RectTransform>();
                    float controlsHeight = controlsRect != null
                        ? (controlsRect.rect.height > 1f ? controlsRect.rect.height : controlsRect.sizeDelta.y)
                        : 0f;
                    controlsReserve = Mathf.Max(0f, controlsHeight + 12f);
                }

                float availableHeight = Mathf.Max(1f, canvasSize.y - margin * 2f - controlsReserve);
                float focusSize = Mathf.Min(availableWidth, availableHeight);

                root.anchorMin = new Vector2(0.5f, 0.5f);
                root.anchorMax = new Vector2(0.5f, 0.5f);
                root.pivot = new Vector2(0.5f, 0.5f);
                root.anchoredPosition = Vector2.zero;
                root.sizeDelta = new Vector2(focusSize, focusSize);
                root.localPosition = new Vector3(root.localPosition.x, root.localPosition.y, _fullscreenOriginalLocalPosition.z);
                root.localRotation = Quaternion.identity;
                root.localScale = _fullscreenOriginalScale;
                root.SetAsLastSibling();

                // The strip is a sibling of the radar root. Keep it above the
                // maximized map so the pilot can immediately press REST/CHT/TRK.
                BringTrafficControlsForward(canvasRect);
            }
            finally
            {
                _updatingFullscreenLayout = false;
            }
        }

        private void ReverseFullscreenEnter(bool animate)
        {
            RectTransform root = _fullscreenRoot;
            if (_fullscreenTransition != null)
            {
                StopCoroutine(_fullscreenTransition);
                _fullscreenTransition = null;
            }

            if (root == null)
            {
                _fullscreenTransitionIsExit = false;
                return;
            }

            if (ShouldAnimateFullscreen(animate))
            {
                Vector3 startScale = root.localScale;
                _fullscreenTransitionIsExit = false;
                _fullscreenTransition = StartCoroutine(AnimateFullscreenScale(root, startScale, _fullscreenOriginalScale));
            }
            else
            {
                root.localScale = _fullscreenOriginalScale;
                _fullscreenTransitionIsExit = false;
            }
        }

        private void ExitFullscreen(bool animate)
        {
            if (!_isFullscreen)
            {
                return;
            }

            // REST can be invoked by the compact toolbar while a pointer/XR
            // drag is still captured. Treat that transition as a cancelled
            // drag so the chart is centered and the own-ship glyph is restored
            // before the root starts shrinking.
            if (_mapDragActive)
            {
                EndMapDrag();
            }

            if (_fullscreenTransition != null)
            {
                StopCoroutine(_fullscreenTransition);
                _fullscreenTransition = null;
            }

            RectTransform root = _fullscreenRoot;
            if (ShouldAnimateFullscreen(animate) && root != null)
            {
                Vector3 startScale = root.localScale;
                Vector3 targetScale = _fullscreenOriginalScale * 0.92f;
                _fullscreenTransitionIsExit = true;
                _fullscreenTransition = StartCoroutine(AnimateFullscreenExit(root, startScale, targetScale));
                return;
            }

            RestoreFullscreenLayout(true);
        }

        private IEnumerator AnimateFullscreenScale(RectTransform root, Vector3 from, Vector3 to)
        {
            float duration = Mathf.Max(0.001f, fullscreenTransitionDuration);
            float elapsed = 0f;
            while (root != null && elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                float eased = progress * progress * (3f - 2f * progress);
                root.localScale = Vector3.LerpUnclamped(from, to, eased);
                yield return null;
            }

            if (root != null)
            {
                root.localScale = to;
            }

            _fullscreenTransition = null;
            _fullscreenTransitionIsExit = false;
        }

        private IEnumerator AnimateFullscreenExit(RectTransform root, Vector3 from, Vector3 to)
        {
            float duration = Mathf.Max(0.001f, fullscreenTransitionDuration);
            float elapsed = 0f;
            while (root != null && elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                float eased = progress * progress * (3f - 2f * progress);
                root.localScale = Vector3.LerpUnclamped(from, to, eased);
                yield return null;
            }

            _fullscreenTransition = null;
            _fullscreenTransitionIsExit = false;
            RestoreFullscreenLayout(true);
        }

        private bool ShouldAnimateFullscreen(bool requested)
        {
            return requested && animateFullscreenTransition && fullscreenTransitionDuration > 0.001f && Application.isPlaying;
        }

        private RectTransform ResolveFullscreenRoot()
        {
            // TrafficRadarDisplay is intentionally a child of the system root;
            // resizing that root also resizes the interaction surface and chart.
            return transform.parent as RectTransform ?? rectTransform;
        }

        private static RectTransform ResolveCanvasRect(RectTransform root)
        {
            if (root == null)
            {
                return null;
            }

            Canvas canvas = root.GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                return root.parent as RectTransform;
            }

            RectTransform canvasRect = canvas.transform as RectTransform;
            // GetComponentInParent includes the root itself.  Never attempt
            // root.SetParent(root, false) when a traffic system happens to own
            // a Canvas component; use its parent Canvas/RectTransform instead.
            if (canvasRect == root)
            {
                Canvas parentCanvas = canvas.transform.parent != null
                    ? canvas.transform.parent.GetComponentInParent<Canvas>()
                    : null;
                canvasRect = parentCanvas != null
                    ? parentCanvas.transform as RectTransform
                    : canvas.transform.parent as RectTransform;
            }

            return canvasRect != root ? canvasRect : null;
        }

        private void CaptureFullscreenLayout(RectTransform root)
        {
            _fullscreenRoot = root;
            _fullscreenOriginalParent = root.parent;
            _fullscreenOriginalSiblingIndex = root.GetSiblingIndex();
            _fullscreenOriginalAnchorMin = root.anchorMin;
            _fullscreenOriginalAnchorMax = root.anchorMax;
            _fullscreenOriginalPivot = root.pivot;
            _fullscreenOriginalAnchoredPosition = root.anchoredPosition;
            _fullscreenOriginalSizeDelta = root.sizeDelta;
            _fullscreenOriginalLocalPosition = root.localPosition;
            _fullscreenOriginalLocalRotation = root.localRotation;
            _fullscreenOriginalScale = root.localScale;
            _fullscreenLayoutStored = true;
            _fullscreenRestorePending = false;
        }

        private void RestoreFullscreenLayout(bool notify, bool refreshDisplay = true, bool allowDefer = true)
        {
            if (_fullscreenTransition != null)
            {
                StopCoroutine(_fullscreenTransition);
                _fullscreenTransition = null;
            }
            _fullscreenTransitionIsExit = false;

            if (!_fullscreenLayoutStored)
            {
                _isFullscreen = false;
                _fullscreenRoot = null;
                _fullscreenRestorePending = false;
                return;
            }

            RectTransform root = _fullscreenRoot;
            if (root == null)
            {
                ClearFullscreenState(false);
                return;
            }

            bool rootActive = root.gameObject.activeInHierarchy;
            bool parentActive = _fullscreenOriginalParent == null || _fullscreenOriginalParent.gameObject.activeInHierarchy;
            if (!rootActive || !parentActive)
            {
                if (allowDefer)
                {
                    _fullscreenRestorePending = true;
                    return;
                }

                // During destruction an inactive hierarchy cannot be safely
                // reparented. The object is going away, so clear state without
                // issuing the sibling-position warning Unity otherwise emits.
                ClearFullscreenState(false);
                return;
            }

            // RectTransform changes synchronously raise OnRectTransformDimensionsChange.
            // Keep the display out of fullscreen while restoring so that callback
            // cannot immediately re-apply the focus layout between property writes.
            // This is especially important after a drag, where the enlarged chart
            // and root used to survive a REST press as a 928px stale layout.
            _isFullscreen = false;
            _updatingFullscreenLayout = true;
            try
            {
                if (_fullscreenOriginalParent != null && root.parent != _fullscreenOriginalParent)
                {
                    root.SetParent(_fullscreenOriginalParent, false);
                }

                root.anchorMin = _fullscreenOriginalAnchorMin;
                root.anchorMax = _fullscreenOriginalAnchorMax;
                root.pivot = _fullscreenOriginalPivot;
                root.anchoredPosition = _fullscreenOriginalAnchoredPosition;
                root.sizeDelta = _fullscreenOriginalSizeDelta;
                root.localPosition = _fullscreenOriginalLocalPosition;
                root.localRotation = _fullscreenOriginalLocalRotation;
                root.localScale = _fullscreenOriginalScale;
            }
            finally
            {
                _updatingFullscreenLayout = false;
            }

            if (root.parent != null)
            {
                int maxSiblingIndex = Mathf.Max(0, root.parent.childCount - 1);
                int siblingIndex = Mathf.Clamp(_fullscreenOriginalSiblingIndex, 0, maxSiblingIndex);
                if (root.GetSiblingIndex() != siblingIndex)
                {
                    root.SetSiblingIndex(siblingIndex);
                }
            }

            if (refreshDisplay && isActiveAndEnabled)
            {
                Canvas.ForceUpdateCanvases();
                SetupDisplay();
                // A compact HUD chart is intentionally own-ship centred. Do not
                // carry a focus-mode drag offset into the small circular scope.
                ResetMapPan(true);
                MarkRadarDirty();
            }

            ApplyPilotLabelStyle();
            PositionCompassLabels(_currentHeadingRotation);

            ClearFullscreenState(notify);
        }

        private void ClearFullscreenState(bool notify)
        {
            _fullscreenTransition = null;
            _fullscreenTransitionIsExit = false;
            _isFullscreen = false;
            _fullscreenRoot = null;
            _fullscreenLayoutStored = false;
            _fullscreenRestorePending = false;
            if (notify)
            {
                FullscreenChanged?.Invoke(false);
            }
        }

        private static void BringTrafficControlsForward(RectTransform canvasRect)
        {
            if (canvasRect == null)
            {
                return;
            }

            Transform strip = canvasRect.Find("TrafficControlStrip");
            if (strip != null)
            {
                strip.SetAsLastSibling();
            }
        }

        /// <summary>
        /// Set sectional chart visibility while keeping the chart child active
        /// for XR simulator input and animating the visual transition.
        /// </summary>
        public void SetChartBackgroundVisible(bool visible, bool animate = true)
        {
            showChartBackground = visible;
            EnsureChartImageReference();
            SetupDisplay();
            BeginChartFade(showChartBackground && !preferXPlaneTrafficTexture ? chartOpacity : 0f, animate);

            if (showChartBackground && !preferXPlaneTrafficTexture)
            {
                TryFetchChartForCurrentPosition(true);
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
        /// Move the chart by a display-local pixel delta.  Positive X moves
        /// east/right and positive Y moves north/up in the UI.  The offset is
        /// clamped to keep the composite covering the circular radar mask.
        /// </summary>
        public void PanMap(Vector2 deltaPixels)
        {
            if (!enableMapPanning || deltaPixels.sqrMagnitude < 0.0001f)
            {
                return;
            }

            _mapPanTarget = ClampMapPan(_mapPanTarget + deltaPixels);
            if (mapPanSmoothing <= 0.001f)
            {
                _mapPan = _mapPanTarget;
                ApplyMapPanVisual(true);
            }

            MapPanChanged?.Invoke(_mapPanTarget);
        }

        /// <summary>
        /// Mark the beginning of a focused chart drag.  The chart remains the
        /// only moving layer; own-ship symbology is omitted until
        /// <see cref="EndMapDrag"/> recenters the map.
        /// </summary>
        public void BeginMapDrag()
        {
            if (!enableMapPanning || _mapDragActive)
            {
                return;
            }

            _mapDragActive = true;
            MarkRadarDirty();
        }

        /// <summary>
        /// Finish a focused chart drag and immediately return the chart to the
        /// own-ship-centered position.  Immediate reset avoids a frame where
        /// the restored own-ship glyph is offset from the aircraft position.
        /// </summary>
        public void EndMapDrag()
        {
            bool wasDragging = _mapDragActive;
            _mapDragActive = false;

            // Always reset, even if the pointer was cancelled after the last
            // drag event.  ResetMapPan is idempotent when already centered.
            ResetMapPan(true);
            if (wasDragging)
            {
                MarkRadarDirty();
            }
        }

        /// <summary>
        /// Convenience setter for XR/input adapters that expose a single drag
        /// state callback.
        /// </summary>
        public void SetMapDragState(bool dragging)
        {
            if (dragging)
            {
                BeginMapDrag();
            }
            else
            {
                EndMapDrag();
            }
        }

        /// <summary>
        /// Set an absolute chart offset in display-local pixels.
        /// </summary>
        public void SetMapPan(Vector2 panPixels, bool immediate = false)
        {
            Vector2 clamped = ClampMapPan(panPixels);
            bool changed = (_mapPanTarget - clamped).sqrMagnitude > 0.0001f;
            _mapPanTarget = clamped;
            if (immediate || mapPanSmoothing <= 0.001f)
            {
                _mapPan = clamped;
                ApplyMapPanVisual(true);
            }

            if (changed)
            {
                MapPanChanged?.Invoke(_mapPanTarget);
            }
        }

        /// <summary>
        /// Return the chart to the own-ship-centered position.
        /// </summary>
        public void ResetMapPan(bool immediate = false)
        {
            SetMapPan(Vector2.zero, immediate);
        }

        /// <summary>
        /// Select a chart/basemap source and refresh its tiles.
        /// </summary>
        public void SetMapSource(FAAChartMapSource source)
        {
            if (chartProvider == null)
            {
                return;
            }

            chartProvider.SetMapSource(source);
        }

        /// <summary>
        /// Cycle through the provider's available basemap sources.
        /// </summary>
        public void CycleMapSource()
        {
            chartProvider?.CycleMapSource();
        }

        /// <summary>
        /// Alias for compact UnityEvent/voice bindings.
        /// </summary>
        public void ToggleMapSource()
        {
            CycleMapSource();
        }

        /// <summary>
        /// UnityEvent-friendly source setter for generated UI buttons.
        /// </summary>
        public void SetMapSource(int sourceIndex)
        {
            chartProvider?.SetMapSource(sourceIndex);
        }

        public void SetCustomMapSource(string tileUrlTemplate)
        {
            chartProvider?.SetCustomTileUrlTemplate(tileUrlTemplate, true);
        }

        /// <summary>
        /// Refresh the chart background.
        /// </summary>
        public void RefreshChart()
        {
            TryFetchChartForCurrentPosition(true);
        }

        public void SetTrackUpMode(bool enabled)
        {
            TrackUpModeEnabled = enabled;
        }

        public void ToggleTrackUpMode()
        {
            TrackUpModeEnabled = !TrackUpModeEnabled;
        }

        public void SetRadarBackgroundVisible(bool visible)
        {
            ShowRadarBackground = visible;
        }

        public void ToggleRadarBackground()
        {
            ShowRadarBackground = !showRadarBackground;
        }

        public void SetRangeRingCount(int count)
        {
            int nextCount = Mathf.Clamp(count, 1, 8);
            if (rangeRingCount == nextCount)
            {
                return;
            }

            rangeRingCount = nextCount;
            MarkRadarDirty();
        }

        public void IncreaseRangeRingCount()
        {
            SetRangeRingCount(rangeRingCount + 1);
        }

        public void DecreaseRangeRingCount()
        {
            SetRangeRingCount(rangeRingCount - 1);
        }

        public void ShowXPlaneTrafficTexture(Texture texture)
        {
            if (texture == null)
            {
                return;
            }

            _xPlaneTrafficTexture = texture;
            ApplyXPlaneTrafficTexture();
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
            drawPixels = new Color32[displaySize * displaySize];
            Color32 clearColor = new Color32(0, 0, 0, 0);
            for (int i = 0; i < clearPixels.Length; i++)
            {
                clearPixels[i] = clearColor;
                drawPixels[i] = clearColor;
            }
            MarkRadarDirty();
        }

        private void SetupDisplay()
        {
            EnsureRadarImageReference();
            if (!preferXPlaneTrafficTexture &&
                (radarTexture == null || clearPixels == null || clearPixels.Length != displaySize * displaySize))
            {
                CreateRadarTexture();
            }

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
                radarImage.enabled = true;
                bool hasPresentedTexture = !preferXPlaneTrafficTexture || _xPlaneTrafficTexture != null;
                radarImage.color = hasPresentedTexture
                    ? Color.white
                    : new Color(0.004f, 0.055f, 0.06f, 0.06f);
                radarImage.texture = preferXPlaneTrafficTexture
                    ? (_xPlaneTrafficTexture != null ? _xPlaneTrafficTexture : GetBlackPlaceholder())
                    : radarTexture;
                
                // Create radar overlay material with full opacity
                if (!preferXPlaneTrafficTexture && radarOverlayMaterial == null && circularShader != null)
                {
                    radarOverlayMaterial = new Material(circularShader);
                    radarOverlayMaterial.name = "RadarOverlayMask_Runtime";
                    radarOverlayMaterial.SetFloat("_Opacity", 1.0f);
                    radarOverlayMaterial.SetFloat("_SoftEdge", chartEdgeSoftness);
                }
                
                if (preferXPlaneTrafficTexture)
                {
                    radarImage.material = null;
                    FitRadarImageToXPlaneAspect(radarImage.rectTransform, radarImage.texture);
                }
                else if (radarOverlayMaterial != null)
                {
                    radarImage.material = radarOverlayMaterial;
                    StretchRadarImage(radarImage.rectTransform);
                }

                if (!preferXPlaneTrafficTexture)
                {
                    MarkRadarDirty();
                    DrawRadarIfNeeded();
                }
            }

            // Setup chart background with circular mask
            if (chartBackgroundImage != null)
            {
                // Keep the child active even while hidden.  Unity does not
                // receive the CHT button event when an authored inactive
                // parent is left disabled, which made the chart appear to be
                // missing in the XR-3 simulator.
                chartBackgroundImage.gameObject.SetActive(true);
                chartBackgroundImage.enabled = !preferXPlaneTrafficTexture &&
                    (showChartBackground || _chartVisualOpacity > 0.001f || _chartFadeAnimating);
                chartBackgroundImage.raycastTarget = false;
                
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
                    UpdateChartEdgeSoftness();
                    ApplyChartVisualOpacity();
                    UpdateChartMaskParameters();
                }
                else
                {
                    // Fallback: just set color alpha
                    Color c = chartBackgroundImage.color;
                    c.a = _chartVisualOpacity;
                    chartBackgroundImage.color = c;
                }
            }

            SetGeneratedOverlayVisibility(!preferXPlaneTrafficTexture || !hideGeneratedOverlaysWithXPlaneTexture);
            SetLocalMaskEnabled(!preferXPlaneTrafficTexture);
            DisableVisualRaycasts();
            ApplyMapPanVisual(true);
        }

        private void EnsureRadarImageReference()
        {
            if (radarImage == null)
            {
                foreach (RawImage image in GetComponentsInChildren<RawImage>(true))
                {
                    if (image == null || image == chartBackgroundImage)
                    {
                        continue;
                    }

                    string imageName = image.gameObject.name;
                    if (imageName.IndexOf("radar", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        imageName.IndexOf("map", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        radarImage = image;
                        break;
                    }
                }
            }

            if (radarImage == null)
            {
                foreach (RawImage image in GetComponentsInChildren<RawImage>(true))
                {
                    if (image != null && image != chartBackgroundImage)
                    {
                        radarImage = image;
                        break;
                    }
                }
            }

            if (radarImage == null)
            {
                GameObject imageObject = new GameObject("Radar Image", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
                imageObject.transform.SetParent(transform, false);
                radarImage = imageObject.GetComponent<RawImage>();
            }

            radarImage.gameObject.SetActive(true);
            radarImage.enabled = true;
            radarImage.raycastTarget = false;
            if (radarImage.texture == null)
            {
                radarImage.color = Color.clear;
            }
        }

        private void ApplyXPlaneTrafficTexture()
        {
            if (radarImage == null)
            {
                return;
            }

            bool hasLiveTexture = _xPlaneTrafficTexture != null;
            Texture texture = hasLiveTexture ? _xPlaneTrafficTexture : GetBlackPlaceholder();
            radarImage.enabled = true;
            radarImage.color = hasLiveTexture
                ? Color.white
                : new Color(0.004f, 0.055f, 0.06f, 0.06f);
            radarImage.texture = texture;
            radarImage.material = null;
            radarImage.raycastTarget = false;
            FitRadarImageToXPlaneAspect(radarImage.rectTransform, texture);

            if (chartBackgroundImage != null)
            {
                chartBackgroundImage.gameObject.SetActive(true);
                chartBackgroundImage.enabled = false;
            }

            SetGeneratedOverlayVisibility(!hideGeneratedOverlaysWithXPlaneTexture);
            SetLocalMaskEnabled(false);
        }

        private Texture2D GetBlackPlaceholder()
        {
            if (_blackPlaceholder == null)
            {
                _blackPlaceholder = new Texture2D(2, 2, TextureFormat.RGBA32, false)
                {
                    name = "XPlaneTrafficRadarBlackPlaceholder",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Point
                };
                _blackPlaceholder.SetPixels(new[] { Color.black, Color.black, Color.black, Color.black });
                _blackPlaceholder.Apply(false);
            }

            return _blackPlaceholder;
        }

        private void FitRadarImageToXPlaneAspect(RectTransform imageRect, Texture texture)
        {
            if (imageRect == null)
            {
                return;
            }

            float aspect = ResolveXPlaneTextureAspect(texture);
            float parentWidth = rectTransform != null && rectTransform.rect.width > 1f ? rectTransform.rect.width : displaySize;
            float parentHeight = rectTransform != null && rectTransform.rect.height > 1f ? rectTransform.rect.height : displaySize;
            float width = parentWidth;
            float height = width / aspect;
            if (height > parentHeight)
            {
                height = parentHeight;
                width = height * aspect;
            }

            imageRect.anchorMin = new Vector2(0.5f, 0.5f);
            imageRect.anchorMax = new Vector2(0.5f, 0.5f);
            imageRect.pivot = new Vector2(0.5f, 0.5f);
            imageRect.anchoredPosition = Vector2.zero;
            imageRect.sizeDelta = new Vector2(Mathf.Max(1f, width), Mathf.Max(1f, height));
            imageRect.localScale = Vector3.one;
            imageRect.localRotation = Quaternion.identity;
        }

        private float ResolveXPlaneTextureAspect(Texture texture)
        {
            if (texture != null && texture.height > 0)
            {
                return texture.width / (float)texture.height;
            }

            return xPlaneTextureFallbackSize.y > 0f
                ? Mathf.Max(0.1f, xPlaneTextureFallbackSize.x / xPlaneTextureFallbackSize.y)
                : 420f / 480f;
        }

        private static void StretchRadarImage(RectTransform imageRect)
        {
            if (imageRect == null)
            {
                return;
            }

            imageRect.anchorMin = Vector2.zero;
            imageRect.anchorMax = Vector2.one;
            imageRect.pivot = new Vector2(0.5f, 0.5f);
            imageRect.anchoredPosition = Vector2.zero;
            imageRect.sizeDelta = Vector2.zero;
            imageRect.localScale = Vector3.one;
            imageRect.localRotation = Quaternion.identity;
        }

        private void SetGeneratedOverlayVisibility(bool visible)
        {
            if (rangeLabel != null)
            {
                rangeLabel.gameObject.SetActive(visible);
            }

            if (compassLabels != null)
            {
                foreach (TextMeshProUGUI label in compassLabels)
                {
                    if (label != null)
                    {
                        label.gameObject.SetActive(visible);
                    }
                }
            }

            if (compassTicksContainer != null)
            {
                compassTicksContainer.gameObject.SetActive(visible);
            }
        }

        /// <summary>
        /// Keep the radar artwork passive so the sibling interaction surface
        /// receives both mouse/XR pointer and drag events.  The display has no
        /// interactive child graphics; all pilot actions are exposed by the
        /// generated control strip and glass surface.
        /// </summary>
        private void DisableVisualRaycasts()
        {
            foreach (Graphic graphic in GetComponentsInChildren<Graphic>(true))
            {
                if (graphic != null &&
                    graphic.gameObject.name.IndexOf("InteractionSurface", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    graphic.raycastTarget = false;
                }
            }
        }

        private void SetLocalMaskEnabled(bool enabled)
        {
            foreach (Mask mask in GetComponents<Mask>())
            {
                if (mask != null)
                {
                    mask.enabled = enabled;
                    // The mask still clips child graphics, but the rectangular
                    // mask source must never be painted over the outside view.
                    mask.showMaskGraphic = false;
                }
            }
        }
        
        private void UpdateChartOpacity()
        {
            // ChartOpacity is the pilot-selected target.  Keep the currently
            // animated visual value separate so +/- controls and CHT fades do
            // not pop the map or hide traffic for a frame.
            if (!_chartFadeAnimating)
            {
                _chartVisualOpacity = showChartBackground && !preferXPlaneTrafficTexture
                    ? Mathf.Clamp01(chartOpacity)
                    : 0f;
            }

            ApplyChartVisualOpacity();
        }

        private void UpdateMapPan()
        {
            if (!enableMapPanning)
            {
                if (_mapPan.sqrMagnitude > 0.001f || _mapPanTarget.sqrMagnitude > 0.001f)
                {
                    _mapPan = Vector2.zero;
                    _mapPanTarget = Vector2.zero;
                    ApplyMapPanVisual(true);
                }

                return;
            }

            if (mapPanSmoothing > 0.001f && (_mapPan - _mapPanTarget).sqrMagnitude > 0.001f)
            {
                float blend = 1f - Mathf.Exp(-mapPanSmoothing * Time.unscaledDeltaTime);
                _mapPan = Vector2.Lerp(_mapPan, _mapPanTarget, blend);
            }

            ApplyMapPanVisual(false);
        }

        private Vector2 ClampMapPan(Vector2 value)
        {
            float width = rectTransform != null && rectTransform.rect.width > 1f
                ? rectTransform.rect.width
                : displaySize;
            float height = rectTransform != null && rectTransform.rect.height > 1f
                ? rectTransform.rect.height
                : displaySize;
            float fraction = Mathf.Clamp(maxMapPanFraction, 0.05f, 0.9f);
            // The chart is circularly masked. A pair of independent axis
            // clamps permits a diagonal offset larger than the chart's
            // coverage margin, which can expose a transparent wedge at the
            // opposite corner. Treat the limit as a radial travel budget so
            // every reachable offset remains covered while still allowing the
            // full configured distance in any cardinal direction.
            float maxDistance = Mathf.Min(width, height) * fraction;
            if (maxDistance <= 0.001f || value.sqrMagnitude <= maxDistance * maxDistance)
            {
                return value;
            }

            return value.normalized * maxDistance;
        }

        private void StoreChartBaseLayout(RectTransform chartRect)
        {
            if (chartRect == null)
            {
                return;
            }

            if (!_chartBaseLayoutStored || _chartBaseRect != chartRect)
            {
                _chartBaseAnchoredPosition = chartRect.anchoredPosition;
                _chartBaseSizeDelta = chartRect.sizeDelta;
                _chartBaseRect = chartRect;
                _chartBaseLayoutStored = true;
            }
        }

        private void ApplyMapPanVisual(bool immediate)
        {
            if (chartBackgroundImage == null)
            {
                return;
            }

            RectTransform chartRect = chartBackgroundImage.rectTransform;
            if (chartRect == null)
            {
                return;
            }

            StoreChartBaseLayout(chartRect);
            Vector2 offset = immediate ? _mapPanTarget : _mapPan;

            // The interaction surface allows a pilot to drag the chart while
            // the circular radar mask stays fixed.  A stretched chart that is
            // exactly the size of the mask would reveal transparent corners as
            // soon as it moved.  Give the chart a deterministic safety margin
            // on all four sides so the full masked footprint remains covered
            // at the configured pan limit.  The authored sizeDelta is restored
            // when panning is disabled, preserving existing scene layouts.
            // Panning is only enabled by the pilot-focus interaction surface.
            // Keep the compact HUD chart at its authored size; enlarging it in
            // the normal 296px footprint makes the chart bleed outside the
            // circular scope and can cover adjacent cockpit readouts. During
            // focus we enlarge it enough to cover the mask at the pan limit.
            if (enableMapPanning && _isFullscreen)
            {
                RectTransform parentRect = chartRect.parent as RectTransform;
                float parentWidth = parentRect != null && parentRect.rect.width > 1f
                    ? parentRect.rect.width
                    : (rectTransform != null && rectTransform.rect.width > 1f ? rectTransform.rect.width : displaySize);
                float parentHeight = parentRect != null && parentRect.rect.height > 1f
                    ? parentRect.rect.height
                    : (rectTransform != null && rectTransform.rect.height > 1f ? rectTransform.rect.height : displaySize);
                float fraction = Mathf.Clamp(maxMapPanFraction, 0.05f, 0.9f);
                Vector2 coverageMargin = new Vector2(
                    parentWidth * fraction * 2f + MapCoverageSafetyPixels,
                    parentHeight * fraction * 2f + MapCoverageSafetyPixels);
                chartRect.sizeDelta = _chartBaseSizeDelta + coverageMargin;
            }
            else
            {
                chartRect.sizeDelta = _chartBaseSizeDelta;
            }

            chartRect.anchoredPosition = _chartBaseAnchoredPosition + offset;
            UpdateChartMaskParameters();
        }

        /// <summary>
        /// Updates the chart material's mask in chart-local UV space. The
        /// chart image is intentionally larger than the radar while panning,
        /// so a UV-centred circle would move with the image and expose a
        /// transparent crescent at the edge. These parameters pin the mask to
        /// the radar root instead.
        /// </summary>
        private void UpdateChartMaskParameters()
        {
            if (circularMaskMaterial == null || chartBackgroundImage == null ||
                rectTransform == null || !circularMaskMaterial.HasProperty(CircularMaskFixedProperty))
            {
                return;
            }

            RectTransform chartRect = chartBackgroundImage.rectTransform;
            if (chartRect == null)
            {
                return;
            }

            Rect chartBounds = chartRect.rect;
            float chartWidth = Mathf.Abs(chartBounds.width);
            float chartHeight = Mathf.Abs(chartBounds.height);
            if (chartWidth <= 0.001f || chartHeight <= 0.001f)
            {
                return;
            }

            // Work in world space so CanvasScaler, fullscreen animation,
            // track-up rotation, and XR world-space canvases are all handled
            // by the same transform path as the rendered vertices.
            Vector3 rootCenterWorld = rectTransform.TransformPoint(rectTransform.rect.center);
            float rootRadius = Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) * 0.5f;
            Vector3 rootRightWorld = rectTransform.TransformPoint(rectTransform.rect.center + Vector2.right * rootRadius);
            Vector3 rootUpWorld = rectTransform.TransformPoint(rectTransform.rect.center + Vector2.up * rootRadius);

            Vector3 chartCenterLocal = chartRect.InverseTransformPoint(rootCenterWorld);
            Vector3 chartRightLocal = chartRect.InverseTransformVector(rootRightWorld - rootCenterWorld);
            Vector3 chartUpLocal = chartRect.InverseTransformVector(rootUpWorld - rootCenterWorld);

            Vector2 center01 = new Vector2(
                (chartCenterLocal.x - chartBounds.xMin) / chartWidth,
                (chartCenterLocal.y - chartBounds.yMin) / chartHeight);
            Vector2 radius01 = new Vector2(
                Mathf.Sqrt(chartRightLocal.x * chartRightLocal.x + chartUpLocal.x * chartUpLocal.x) / chartWidth,
                Mathf.Sqrt(chartRightLocal.y * chartRightLocal.y + chartUpLocal.y * chartUpLocal.y) / chartHeight);
            radius01.x = Mathf.Max(0.0001f, radius01.x);
            radius01.y = Mathf.Max(0.0001f, radius01.y);

            // RawImage's shader varying includes uvRect (_MainTex_ST), so
            // express the fixed centre/radius in that same transformed space.
            Rect uvRect = chartBackgroundImage.uvRect;
            Vector2 uvCenter = new Vector2(
                uvRect.x + center01.x * uvRect.width,
                uvRect.y + center01.y * uvRect.height);
            Vector2 uvRadius = new Vector2(
                radius01.x * Mathf.Abs(uvRect.width),
                radius01.y * Mathf.Abs(uvRect.height));

            circularMaskMaterial.SetVector(CircularMaskCenterProperty, uvCenter);
            circularMaskMaterial.SetVector(CircularMaskRadiusProperty, uvRadius);
            circularMaskMaterial.SetFloat(CircularMaskFixedProperty, 1f);
        }

        private void ApplyChartVisualOpacity()
        {
            float visualOpacity = Mathf.Clamp01(_chartVisualOpacity);
            if (circularMaskMaterial != null)
            {
                circularMaskMaterial.SetFloat("_Opacity", visualOpacity);
            }
            else if (chartBackgroundImage != null)
            {
                Color c = chartBackgroundImage.color;
                c.a = visualOpacity;
                chartBackgroundImage.color = c;
            }

            if (chartBackgroundImage != null)
            {
                chartBackgroundImage.enabled = !preferXPlaneTrafficTexture &&
                    (showChartBackground || visualOpacity > 0.001f || _chartFadeAnimating);
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
            MarkRadarDirty();
        }
        
        /// <summary>
        /// Called by TrafficRadarController when targets are updated
        /// </summary>
        private void OnControllerTargetsUpdated(IReadOnlyList<RadarTarget> targets)
        {
            currentTargets.Clear();
            
            if (targets == null)
            {
                MarkRadarDirty();
                return;
            }
            
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
            
            if (verboseLogging)
            {
                Debug.Log($"[TrafficRadarDisplay] Received {currentTargets.Count} targets from controller");
            }
            MarkRadarDirty();
        }

        private void OnChartLoaded(Texture2D chartTexture)
        {
            if (chartBackgroundImage != null && chartTexture != null)
            {
                EnsureChartImageReference();
                chartBackgroundImage.texture = chartTexture;
                if (showChartBackground && !preferXPlaneTrafficTexture && _chartVisualOpacity <= 0.001f)
                {
                    BeginChartFade(chartOpacity, true);
                }
            }
        }

        private void OnChartMapSourceChanged(FAAChartMapSource source)
        {
            // A source switch changes the projection/style beneath the scope;
            // clear the old drag offset so the new composite starts centered.
            ResetMapPan(true);
            MapSourceChanged?.Invoke(source);
        }

        private void DrawRadarIfNeeded()
        {
            if (preferXPlaneTrafficTexture)
            {
                return;
            }

            if (!_radarTextureDirty)
            {
                return;
            }

            DrawRadar();
        }

        private void DrawRadar()
        {
            // Clear texture
            Array.Copy(clearPixels, drawPixels, clearPixels.Length);

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

            // Draw own aircraft at center unless the pilot is temporarily
            // inspecting a panned chart.  Keeping this in the generated
            // texture means the traffic targets/range rings remain useful
            // during a drag while the own-ship marker cannot obscure chart
            // details underneath it.
            if (!_mapDragActive)
            {
                DrawOwnAircraft(centerX, centerY);
            }

            // Apply texture changes
            radarTexture.SetPixels32(drawPixels);
            radarTexture.Apply(false);
            _lastDrawnHeadingRotation = _currentHeadingRotation;
            _radarTextureDirty = false;
        }

        private void MarkRadarDirty()
        {
            _radarTextureDirty = true;
        }

        private void DrawRangeRings(int centerX, int centerY, float radius)
        {
            int ringCount = Mathf.Clamp(rangeRingCount, 1, 8);
            float lineScale = ResolvePilotLineworkScale();

            if (!usePilotLinework)
            {
                for (int i = 1; i <= ringCount; i++)
                {
                    float ringRadius = radius * i / ringCount;
                    DrawCircle(centerX, centerY, (int)ringRadius, rangeRingColor, 1);
                }

                return;
            }

            // The outside and half-range gates are the two lines pilots use
            // most often. Give them a deliberate visual weight while keeping
            // the intermediate gates quiet over a dense sectional chart.
            // The small lift in value/alpha is intentional: the chart is
            // rendered beneath this texture and can contain very bright ink.
            Color majorColor = LiftLineColor(
                rangeRingColor,
                0.20f,
                Mathf.Clamp01(Mathf.Max(0.96f, rangeRingColor.a + 0.30f)));
            Color minorColor = LiftLineColor(
                rangeRingColor,
                0.10f,
                Mathf.Clamp01(Mathf.Max(0.76f, rangeRingColor.a * 1.25f)));
            Color haloColor = new Color(0.005f, 0.045f, 0.05f, 0.40f);
            int halfRangeRing = Mathf.Max(1, Mathf.CeilToInt(ringCount * 0.5f));
            for (int i = 1; i <= ringCount; i++)
            {
                // Pull the outside stroke in slightly so the circular mask's
                // feathered edge cannot erase the pilot's primary reference.
                float ringRadius = radius * i / ringCount;
                if (i == ringCount)
                {
                    ringRadius = Mathf.Max(
                        1f,
                        ringRadius - Mathf.Max(perimeterInsetPixels, 7f * lineScale));
                }

                bool isMajor = i == ringCount || i == halfRangeRing;
                float thickness = (isMajor ? 2.35f : 1.35f) * lineScale;
                Color color = isMajor ? majorColor : minorColor;
                // A restrained dark halo keeps the line legible over bright
                // chart ink without turning the scope into a glowing cage.
                DrawCircleAntiAliased(
                    centerX,
                    centerY,
                    ringRadius,
                    WithAlpha(haloColor, isMajor ? 0.42f : 0.24f),
                    thickness + 2.8f * lineScale);
                DrawCircleAntiAliased(centerX, centerY, ringRadius, color, thickness);
            }
        }

        private void DrawCompassMarkings(int centerX, int centerY, float radius)
        {
            // Apply heading rotation offset for track-up mode.
            float headingOffset = enableTrackUpMode ? _currentHeadingRotation : 0f;

            if (!usePilotLinework)
            {
                DrawLegacyCompassMarkings(centerX, centerY, radius, headingOffset);
                return;
            }

            float lineScale = ResolvePilotLineworkScale();
            Color cardinalColor = LiftLineColor(
                compassMarkingsColor,
                0.14f,
                Mathf.Clamp01(Mathf.Max(0.94f, compassMarkingsColor.a + 0.10f)));
            Color majorColor = LiftLineColor(
                compassMarkingsColor,
                0.06f,
                Mathf.Clamp01(Mathf.Max(0.84f, compassMarkingsColor.a * 0.90f)));
            Color minorColor = LiftLineColor(
                compassMarkingsColor,
                0.02f,
                Mathf.Clamp01(Mathf.Max(0.62f, compassMarkingsColor.a * 0.70f)));
            Color haloColor = new Color(0.005f, 0.045f, 0.05f, 0.34f);
            float outerRadius = Mathf.Max(1f, radius - 3f * lineScale);

            // A 15-degree tick cadence reads cleanly in peripheral vision;
            // 30-degree ticks establish the larger bearing rhythm and the
            // four cardinal ticks remain unmistakable without long spokes.
            const int tickSpacingDegrees = 15;
            for (int angle = 0; angle < 360; angle += tickSpacingDegrees)
            {
                bool isCardinal = angle % 90 == 0;
                bool isMajor = angle % 30 == 0;
                if (!isCardinal && !isMajor && !showMinorBearingTicks)
                {
                    continue;
                }
                float length = isCardinal ? 24f : isMajor ? 15f : 9f;
                float innerRadius = outerRadius - length * lineScale;
                float adjustedAngle = angle + headingOffset;
                float rad = adjustedAngle * Mathf.Deg2Rad;

                int x1 = centerX + Mathf.RoundToInt(innerRadius * Mathf.Sin(rad));
                int y1 = centerY + Mathf.RoundToInt(innerRadius * Mathf.Cos(rad));
                int x2 = centerX + Mathf.RoundToInt(outerRadius * Mathf.Sin(rad));
                int y2 = centerY + Mathf.RoundToInt(outerRadius * Mathf.Cos(rad));

                Color tickColor = isCardinal ? cardinalColor : isMajor ? majorColor : minorColor;
                float thickness = (isCardinal ? 2.7f : isMajor ? 1.8f : 1.15f) * lineScale;
                DrawLineAntiAliased(
                    x1,
                    y1,
                    x2,
                    y2,
                    WithAlpha(haloColor, isCardinal ? 0.42f : 0.22f),
                    thickness + 2.6f * lineScale);
                DrawLineAntiAliased(x1, y1, x2, y2, tickColor, thickness);

                if (showCardinalBearingCues && isCardinal)
                {
                    // Keep a small interior cue for orientation, rather than
                    // drawing a full spoke that competes with traffic targets.
                    float cueInner = radius * 0.78f;
                    float cueOuter = radius * 0.84f;
                    int cx1 = centerX + Mathf.RoundToInt(cueInner * Mathf.Sin(rad));
                    int cy1 = centerY + Mathf.RoundToInt(cueInner * Mathf.Cos(rad));
                    int cx2 = centerX + Mathf.RoundToInt(cueOuter * Mathf.Sin(rad));
                    int cy2 = centerY + Mathf.RoundToInt(cueOuter * Mathf.Cos(rad));
                    DrawLineAntiAliased(
                        cx1,
                        cy1,
                        cx2,
                        cy2,
                        WithAlpha(haloColor, 0.12f),
                        2f * lineScale);
                    DrawLineAntiAliased(cx1, cy1, cx2, cy2, WithAlpha(cardinalColor, 0.34f), 1.2f * lineScale);
                }
            }
        }

        private void ApplyPilotLabelStyle()
        {
            if (!usePilotLinework || compassLabels == null)
            {
                return;
            }

            Color labelColor = LiftLineColor(compassMarkingsColor, 0.04f, Mathf.Max(0.86f, compassMarkingsColor.a));
            Color outlineColor = new Color(0.005f, 0.035f, 0.035f, 0.84f);
            float labelFontSize = IsFullscreen
                ? Mathf.Max(10f, fullscreenCompassFontSize)
                : Mathf.Max(8f, compactCompassFontSize);
            Vector2 labelSize = IsFullscreen
                ? new Vector2(48f, 34f)
                : new Vector2(32f, 24f);
            foreach (TextMeshProUGUI label in compassLabels)
            {
                if (label == null)
                {
                    continue;
                }

                label.fontStyle = FontStyles.Bold;
                label.fontSize = labelFontSize;
                label.enableAutoSizing = false;
                label.extraPadding = true;
                label.color = labelColor;
                label.outlineWidth = IsFullscreen ? 0.22f : 0.18f;
                label.outlineColor = outlineColor;
                label.raycastTarget = false;
                RectTransform labelRect = label.rectTransform;
                if (labelRect != null)
                {
                    labelRect.sizeDelta = labelSize;
                }
            }

            if (rangeLabel != null)
            {
                rangeLabel.fontStyle = FontStyles.Bold;
                rangeLabel.fontSize = IsFullscreen ? 16f : 14f;
                rangeLabel.extraPadding = true;
                rangeLabel.color = labelColor;
                rangeLabel.outlineWidth = IsFullscreen ? 0.20f : 0.16f;
                rangeLabel.outlineColor = outlineColor;
                rangeLabel.raycastTarget = false;
            }
        }

        private void DrawLegacyCompassMarkings(int centerX, int centerY, float radius, float headingOffset)
        {
            // Preserve the original spoke treatment for projects that opt out
            // of the pilot linework pass.
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

            for (int angle = 0; angle < 360; angle += 30)
            {
                if (angle % 90 == 0)
                {
                    continue;
                }

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

        private float ResolvePilotLineworkScale()
        {
            float textureScale = displaySize > 0 ? displaySize / 512f : 1f;
            return Mathf.Clamp(textureScale * pilotLineworkScale, 0.5f, 3f);
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = Mathf.Clamp01(alpha);
            return color;
        }

        private static Color LiftLineColor(Color color, float lift, float alpha)
        {
            Color lifted = Color.Lerp(color, Color.white, Mathf.Clamp01(lift));
            lifted.a = Mathf.Clamp01(alpha);
            return lifted;
        }

        private void DrawCircleAntiAliased(int cx, int cy, float radius, Color color, float thickness)
        {
            float safeRadius = Mathf.Max(0f, radius);
            float halfThickness = Mathf.Max(0.5f, thickness * 0.5f);
            int extent = Mathf.CeilToInt(safeRadius + halfThickness + 1f);
            int sampleSpan = Mathf.CeilToInt(halfThickness + 1.5f);
            float radiusSquared = safeRadius * safeRadius;

            // Rasterize only the narrow annulus around the circumference. The
            // previous full-square walk was expensive enough to make heading
            // updates visible on XR hardware; this scan visits O(circumference)
            // pixels while preserving soft anti-aliased edges.
            for (int y = -extent; y <= extent; y++)
            {
                float inside = radiusSquared - y * y;
                if (inside < -1f)
                {
                    continue;
                }

                int xRadius = Mathf.RoundToInt(Mathf.Sqrt(Mathf.Max(0f, inside)));
                for (int offset = -sampleSpan; offset <= sampleSpan; offset++)
                {
                    BlendCircleSample(cx + xRadius + offset, cy + y, cx, cy, safeRadius, halfThickness, color);
                    if (xRadius > 0)
                    {
                        BlendCircleSample(cx - xRadius + offset, cy + y, cx, cy, safeRadius, halfThickness, color);
                    }
                }
            }
        }

        private void BlendCircleSample(
            int x,
            int y,
            int centerX,
            int centerY,
            float radius,
            float halfThickness,
            Color color)
        {
            float distance = Mathf.Sqrt((x - centerX) * (x - centerX) + (y - centerY) * (y - centerY));
            float edgeDistance = Mathf.Abs(distance - radius);
            float coverage = Mathf.Clamp01(halfThickness + 0.5f - edgeDistance);
            if (coverage > 0f)
            {
                BlendPixelSafe(x, y, WithAlpha(color, color.a * coverage));
            }
        }

        private void DrawLineAntiAliased(int x1, int y1, int x2, int y2, Color color, float thickness)
        {
            float halfThickness = Mathf.Max(0.5f, thickness * 0.5f);
            int minX = Mathf.FloorToInt(Mathf.Min(x1, x2) - halfThickness - 1f);
            int maxX = Mathf.CeilToInt(Mathf.Max(x1, x2) + halfThickness + 1f);
            int minY = Mathf.FloorToInt(Mathf.Min(y1, y2) - halfThickness - 1f);
            int maxY = Mathf.CeilToInt(Mathf.Max(y1, y2) + halfThickness + 1f);

            Vector2 start = new Vector2(x1, y1);
            Vector2 end = new Vector2(x2, y2);
            Vector2 segment = end - start;
            float segmentLengthSquared = segment.sqrMagnitude;

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    Vector2 point = new Vector2(x, y);
                    float t = segmentLengthSquared > 0.0001f
                        ? Mathf.Clamp01(Vector2.Dot(point - start, segment) / segmentLengthSquared)
                        : 0f;
                    float distance = Vector2.Distance(point, start + segment * t);
                    float coverage = Mathf.Clamp01(halfThickness + 0.5f - distance);
                    if (coverage > 0f)
                    {
                        BlendPixelSafe(x, y, WithAlpha(color, color.a * coverage));
                    }
                }
            }
        }

        private void BlendPixelSafe(int x, int y, Color color)
        {
            if (x < 0 || x >= displaySize || y < 0 || y >= displaySize)
            {
                return;
            }

            int index = y * displaySize + x;
            Color32 destination = drawPixels[index];
            float sourceAlpha = Mathf.Clamp01(color.a);
            float destinationAlpha = destination.a / 255f;
            float outputAlpha = sourceAlpha + destinationAlpha * (1f - sourceAlpha);
            if (outputAlpha <= 0.0001f)
            {
                return;
            }

            Color destinationColor = destination;
            Color output = (color * sourceAlpha + destinationColor * (destinationAlpha * (1f - sourceAlpha))) / outputAlpha;
            output.a = outputAlpha;
            drawPixels[index] = output;
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
            // Draw own aircraft in the shared HUD green, pointing up.
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
                drawPixels[(y * displaySize) + x] = color;
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
