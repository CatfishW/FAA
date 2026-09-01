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
    /// Pilot-selected point on the sectional/radar map.  The geographic
    /// position is retained so the cue can move with the aircraft while the
    /// radar range, heading, or map source changes.
    /// </summary>
    [Serializable]
    public struct RadarNavigationTarget
    {
        public bool IsValid;
        public string Identifier;
        public bool HasGeoPosition;
        public double Latitude;
        public double Longitude;
        public float BearingDegrees;
        public float DistanceNM;
        public float RelativeBearingDegrees;
        public Vector2 RadarPosition;
        /// <summary>
        /// Target vector in the aircraft's frame (x = right/left, y =
        /// ahead/behind).  Unlike <see cref="RadarPosition"/>, this remains
        /// useful to HUD course bars when the radar is in north-up mode.
        /// </summary>
        public Vector2 AircraftRelativePosition;
        public bool IsWithinRange;

        public bool IsOffscreen => IsValid && !IsWithinRange;

        public static RadarNavigationTarget Empty => new RadarNavigationTarget
        {
            IsValid = false,
            Identifier = string.Empty,
            HasGeoPosition = false,
            Latitude = 0d,
            Longitude = 0d,
            BearingDegrees = 0f,
            DistanceNM = 0f,
            RelativeBearingDegrees = 0f,
            RadarPosition = Vector2.zero,
            AircraftRelativePosition = Vector2.zero,
            IsWithinRange = false
        };
    }

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

        [Tooltip("Show the range rings, compass ticks, and bearing labels.")]
        [SerializeField] private bool showReferenceLinework = true;

        [Tooltip("Duration of the reference-line fade used by the contextual pilot menu.")]
        [Min(0f)]
        [SerializeField] private float referenceLineworkFadeDuration = 0.18f;
        
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

        [Header("Navigation Target")]
        [Tooltip("Accent used for the pilot-selected map target and HUD guidance cue.")]
        [SerializeField] private Color navigationTargetColor = new Color(1f, 0.78f, 0.28f, 1f);

        [Tooltip("Show the selected target cue on the radar and HUD.")]
        [SerializeField] private bool showNavigationTarget = true;

        [Tooltip("Pulse speed for the selected target cue.")]
        [Min(0f)]
        [SerializeField] private float navigationTargetPulseSpeed = 0.75f;

        [Tooltip("Maximum radial position used when a target is beyond the selected range.")]
        [Range(0.72f, 0.96f)]
        [SerializeField] private float navigationTargetEdgeFraction = 0.90f;

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

        // Pilot-selected navigation cue.  The marker is a separate crisp UI
        // graphic rather than texture pixels, so it remains sharp in the XR-3
        // simulator and in the maximized map view.
        private RadarNavigationTargetGraphic _navigationTargetGraphic;
        private RadarNavigationTargetGraphic _navigationPreviewGraphic;
        private RadarNavigationTarget _navigationTarget = RadarNavigationTarget.Empty;
        private RadarNavigationTarget _navigationPreview = RadarNavigationTarget.Empty;
        private RadarNavigationTarget _lastPublishedNavigationTarget = RadarNavigationTarget.Empty;
        private Vector2 _navigationTargetLocalNormalized;
        private Vector2 _navigationPreviewLocalNormalized;
        private bool _navigationTargetHasGeoPosition;
        private bool _navigationPreviewHasGeoPosition;
        private double _navigationTargetLatitude;
        private double _navigationTargetLongitude;
        private double _navigationPreviewLatitude;
        private double _navigationPreviewLongitude;
        private float _navigationTargetPulse;
        private float _navigationPreviewPulse;
        private bool _hasPublishedNavigationTarget;
        private const float NavigationTargetPulseRateCap = 0.85f;

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
        private float _lineworkVisualAlpha = 1f;
        private float _lineworkFadeFromAlpha = 1f;
        private float _lineworkFadeToAlpha = 1f;
        private float _lineworkFadeStartTime;
        private bool _lineworkFadeAnimating;

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
        public bool ReferenceLineworkVisible => showReferenceLinework;
        public float ReferenceLineworkVisualAlpha => _lineworkVisualAlpha;
        public bool AutoRangeEnabled => radarController != null && radarController.AutoRangeEnabled;

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

        /// <summary>
        /// Current pilot-selected map target.  The value is empty when no
        /// target has been placed.
        /// </summary>
        public RadarNavigationTarget CurrentNavigationTarget => _navigationTarget;

        /// <summary>
        /// True after a pilot has placed a navigation target on the map.
        /// </summary>
        public bool HasNavigationTarget => _navigationTarget.IsValid;

        /// <summary>
        /// User-facing target placement is available only in pilot-focus mode.
        /// External FMS integrations should still use the explicit geographic
        /// setter, but the map interaction path must never commit a point from
        /// the compact HUD.
        /// </summary>
        public bool CanSetNavigationTarget => isActiveAndEnabled && _isFullscreen;

        /// <summary>
        /// Candidate point shown while the target confirmation dialog is open.
        /// It is deliberately separate from <see cref="CurrentNavigationTarget"/>
        /// so HUD guidance never lights up before the pilot confirms.
        /// </summary>
        public RadarNavigationTarget CurrentNavigationPreview => _navigationPreview;

        public bool HasNavigationPreview => _navigationPreview.IsValid;

        /// <summary>
        /// Whether the selected target cue is painted over the radar.
        /// </summary>
        public bool ShowNavigationTarget
        {
            get => showNavigationTarget;
            set
            {
                if (showNavigationTarget == value)
                {
                    return;
                }

                showNavigationTarget = value;
                ApplyNavigationTargetVisual(true);
            }
        }

        public Color NavigationTargetColor
        {
            get => navigationTargetColor;
            set
            {
                navigationTargetColor = value;
                ApplyNavigationTargetVisual(true);
            }
        }

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
        /// Raised when the pilot changes range-ring/compass line visibility.
        /// </summary>
        public event Action<bool> ReferenceLineworkChanged;

        /// <summary>
        /// Raised when a drag/pan settles at a new map offset.
        /// </summary>
        public event Action<Vector2> MapPanChanged;

        /// <summary>
        /// Raised when a pilot places, moves, or clears the map target.
        /// </summary>
        public event Action<RadarNavigationTarget> NavigationTargetChanged;

        /// <summary>
        /// Raised as the uncommitted point in the target confirmation dialog
        /// changes.  Consumers can update coordinate readouts without
        /// exposing an active HUD navigation cue.
        /// </summary>
        public event Action<RadarNavigationTarget> NavigationPreviewChanged;

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
        public RectTransform DisplayRectTransform => rectTransform;

        /// <summary>
        /// Resolve the live/reference own-ship position for coordinate-entry
        /// UI and external XR adapters.
        /// </summary>
        public bool TryGetOwnshipCoordinates(
            out double latitude,
            out double longitude,
            out float headingDegrees)
        {
            return TryResolveNavigationOwnShip(out latitude, out longitude, out headingDegrees);
        }

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
            _lineworkVisualAlpha = showReferenceLinework ? 1f : 0f;
            _lineworkFadeFromAlpha = _lineworkVisualAlpha;
            _lineworkFadeToAlpha = _lineworkVisualAlpha;
            NormalizePanelReadability();
            CreateRadarTexture();
            EnsureRadarImageReference();
            EnsureChartImageReference();
            EnsureNavigationTargetGraphic();
            EnsureNavigationPreviewGraphic();
            ApplyMapPanVisual(true);
            ApplyNavigationTargetVisual(true);
            ApplyNavigationPreviewVisual(true);
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
            EnsureNavigationTargetGraphic();
            EnsureNavigationPreviewGraphic();
            ApplyNavigationTargetVisual(true);
            ApplyNavigationPreviewVisual(true);
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
            if (chartBackgroundImage != null)
            {
                string currentName = chartBackgroundImage.gameObject.name;
                if (currentName.IndexOf("chart", StringComparison.OrdinalIgnoreCase) < 0 &&
                    currentName.IndexOf("sectional", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    // A few legacy prefabs serialize the generic Map Image
                    // into this slot. Reject it so the provider texture is
                    // always presented by the dedicated chart layer.
                    chartBackgroundImage = null;
                }
            }

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

        private void BeginReferenceLineworkFade(float targetAlpha, bool animate)
        {
            targetAlpha = Mathf.Clamp01(targetAlpha);
            _lineworkFadeFromAlpha = _lineworkVisualAlpha;
            _lineworkFadeToAlpha = targetAlpha;

            if (!animate || !Application.isPlaying || referenceLineworkFadeDuration <= 0.001f)
            {
                _lineworkVisualAlpha = targetAlpha;
                _lineworkFadeAnimating = false;
                ApplyPilotLabelStyle();
                MarkRadarDirty();
                return;
            }

            _lineworkFadeStartTime = Time.unscaledTime;
            _lineworkFadeAnimating = true;
            MarkRadarDirty();
        }

        private void UpdateReferenceLineworkFade()
        {
            if (!_lineworkFadeAnimating)
            {
                return;
            }

            float duration = Mathf.Max(0.001f, referenceLineworkFadeDuration);
            float progress = Mathf.Clamp01((Time.unscaledTime - _lineworkFadeStartTime) / duration);
            float eased = progress * progress * (3f - 2f * progress);
            _lineworkVisualAlpha = Mathf.Lerp(_lineworkFadeFromAlpha, _lineworkFadeToAlpha, eased);
            ApplyPilotLabelStyle();
            MarkRadarDirty();

            if (progress >= 1f)
            {
                _lineworkVisualAlpha = _lineworkFadeToAlpha;
                _lineworkFadeAnimating = false;
            }
        }

        private IEnumerator AnimateMapSourceChange()
        {
            float targetOpacity = Mathf.Clamp01(chartOpacity);
            float dippedOpacity = Mathf.Min(targetOpacity, Mathf.Max(0.035f, targetOpacity * 0.18f));
            float duration = Mathf.Max(0.08f, chartFadeDuration);

            BeginChartFade(dippedOpacity, true);
            yield return new WaitForSecondsRealtime(duration * 0.58f);

            chartProvider?.CycleMapSource();

            // Bring the retained composite back while new tiles load. The
            // provider swaps in the replacement texture atomically, so pilots
            // never see an untextured circular scope.
            yield return new WaitForSecondsRealtime(duration * 0.28f);
            if (showChartBackground && !preferXPlaneTrafficTexture)
            {
                BeginChartFade(targetOpacity, true);
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
            UpdateReferenceLineworkFade();
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

            UpdateNavigationTarget();
            UpdateNavigationPreview();
            
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
        /// Take manual range control and advance to the next range gate. This
        /// is used by the one-tap menu so its visible result is not immediately
        /// overwritten by the controller's automatic range selection.
        /// </summary>
        public void CycleRangeManual()
        {
            if (radarController != null)
            {
                radarController.SetAutoRangeEnabled(false);
            }

            // Use the display's normal cycle path after taking manual control
            // so the existing smooth-zoom animation remains intact.
            CycleRange();
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
        /// Show or hide the pilot reference rings, compass ticks, and their
        /// labels. The traffic and own-ship symbols remain visible throughout
        /// the transition so decluttering never removes safety-critical data.
        /// </summary>
        public void SetReferenceLineworkVisible(bool visible, bool animate = true)
        {
            if (showReferenceLinework == visible &&
                Mathf.Approximately(_lineworkFadeToAlpha, visible ? 1f : 0f))
            {
                return;
            }

            showReferenceLinework = visible;
            BeginReferenceLineworkFade(visible ? 1f : 0f, animate);
            ReferenceLineworkChanged?.Invoke(visible);
        }

        public void ToggleReferenceLinework()
        {
            SetReferenceLineworkVisible(!showReferenceLinework, true);
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

            // A candidate point is meaningful only while the pilot-focus map
            // is visible. Never carry an unconfirmed selection back into the
            // compact HUD.
            if (_navigationPreview.IsValid || _navigationPreviewHasGeoPosition)
            {
                ClearNavigationPreviewInternal(true);
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

        private bool TryNormalizeNavigationPoint(
            Vector2 localPoint,
            out Vector2 normalized,
            out float magnitude)
        {
            normalized = Vector2.zero;
            magnitude = 0f;
            if (rectTransform == null)
            {
                rectTransform = GetComponent<RectTransform>();
            }

            if (rectTransform == null)
            {
                return false;
            }

            Rect displayRect = rectTransform.rect;
            float radius = Mathf.Min(displayRect.width, displayRect.height) * 0.5f;
            if (radius <= 1f)
            {
                return false;
            }

            // The chart is the layer that moves during a focused drag while
            // the radar/own-ship frame stays fixed. Convert the tapped screen
            // point back into chart coordinates before projecting it to a
            // geographic waypoint.
            Vector2 chartRelativePoint = localPoint - _mapPan;
            normalized = (chartRelativePoint - displayRect.center) / radius;
            magnitude = normalized.magnitude;
            if (magnitude <= 0.0001f)
            {
                normalized = Vector2.up * 0.035f;
                magnitude = normalized.magnitude;
            }

            float maximum = Mathf.Clamp(navigationTargetEdgeFraction + 0.035f, 0.76f, 0.97f);
            if (magnitude > maximum)
            {
                normalized = normalized.normalized * maximum;
                magnitude = maximum;
            }

            return true;
        }

        /// <summary>
        /// Place (or replace) the pilot's navigation target using a point in
        /// this display's local coordinate space. User-facing placement is
        /// intentionally gated to the maximized map view.
        /// </summary>
        public bool SetNavigationTargetFromLocalPoint(Vector2 localPoint, string identifier = "MAP")
        {
            if (!CanSetNavigationTarget ||
                !TryNormalizeNavigationPoint(localPoint, out Vector2 normalized, out float magnitude))
            {
                return false;
            }

            _navigationTargetLocalNormalized = normalized;
            _navigationTargetHasGeoPosition = false;
            _navigationTargetLatitude = 0d;
            _navigationTargetLongitude = 0d;

            _navigationTarget = RadarNavigationTarget.Empty;
            _navigationTarget.IsValid = true;
            _navigationTarget.Identifier = SanitizeNavigationTargetIdentifier(identifier);

            // Capture the map point immediately when possible. If the bridge
            // is still warming up, UpdateNavigationTarget will promote the
            // local cue to a geographic target as soon as coordinates arrive.
            if (TryResolveNavigationOwnShip(out double ownLatitude, out double ownLongitude, out float ownHeading))
            {
                float relativeBearing = Mathf.Atan2(normalized.x, normalized.y) * Mathf.Rad2Deg;
                float absoluteBearing = enableTrackUpMode
                    ? NormalizeDegrees(ownHeading + relativeBearing)
                    : NormalizeDegrees(relativeBearing);
                float distance = Mathf.Max(0.05f, magnitude * Mathf.Max(0.1f, rangeNM));
                CalculateDestination(
                    ownLatitude,
                    ownLongitude,
                    absoluteBearing,
                    distance,
                    out _navigationTargetLatitude,
                    out _navigationTargetLongitude);
                _navigationTargetHasGeoPosition = true;
            }

            RefreshNavigationTarget(true);
            ApplyNavigationTargetVisual(true);
            return true;
        }

        /// <summary>
        /// Place a target directly from a screen-space pointer event. This is
        /// useful for mouse, touch, and XR ray adapters that already have an
        /// event camera.
        /// </summary>
        public bool SetNavigationTargetFromScreenPoint(
            Vector2 screenPoint,
            Camera eventCamera,
            string identifier = "MAP")
        {
            if (!CanSetNavigationTarget)
            {
                return false;
            }

            if (rectTransform == null)
            {
                rectTransform = GetComponent<RectTransform>();
            }

            if (rectTransform == null ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    rectTransform,
                    screenPoint,
                    eventCamera,
                    out Vector2 localPoint))
            {
                return false;
            }

            return SetNavigationTargetFromLocalPoint(localPoint, identifier);
        }

        /// <summary>
        /// Place a target from a world-space point. Context-menu leaders use
        /// this overload so a menu attached to a sibling interaction surface
        /// remains aligned with the actual radar display.
        /// </summary>
        public bool SetNavigationTargetFromWorldPoint(Vector3 worldPoint, string identifier = "MAP")
        {
            if (!CanSetNavigationTarget)
            {
                return false;
            }

            if (rectTransform == null)
            {
                rectTransform = GetComponent<RectTransform>();
            }

            return rectTransform != null &&
                   SetNavigationTargetFromLocalPoint(
                       rectTransform.InverseTransformPoint(worldPoint),
                       identifier);
        }

        /// <summary>
        /// Place a geographic navigation target. This is also useful for
        /// future waypoint/FMS adapters that already own latitude/longitude.
        /// </summary>
        public bool SetNavigationTarget(double latitude, double longitude, string identifier = "MAP")
        {
            if (!CanSetNavigationTarget || !IsValidGeoPosition(latitude, longitude))
            {
                return false;
            }

            _navigationTarget = RadarNavigationTarget.Empty;
            _navigationTarget.IsValid = true;
            _navigationTarget.Identifier = SanitizeNavigationTargetIdentifier(identifier);
            _navigationTargetHasGeoPosition = true;
            _navigationTargetLatitude = latitude;
            _navigationTargetLongitude = longitude;
            RefreshNavigationTarget(true);
            ApplyNavigationTargetVisual(true);
            return true;
        }

        /// <summary>
        /// Show an uncommitted candidate on the maximized map. The candidate
        /// never reaches the HUD guidance elements until
        /// <see cref="CommitNavigationPreview"/> is called.
        /// </summary>
        public bool SetNavigationPreviewFromLocalPoint(Vector2 localPoint, string identifier = "MAP")
        {
            if (!CanSetNavigationTarget ||
                !TryNormalizeNavigationPoint(localPoint, out Vector2 normalized, out float magnitude))
            {
                return false;
            }

            _navigationPreviewLocalNormalized = normalized;
            _navigationPreviewHasGeoPosition = false;
            _navigationPreviewLatitude = 0d;
            _navigationPreviewLongitude = 0d;
            _navigationPreview = RadarNavigationTarget.Empty;
            _navigationPreview.IsValid = true;
            _navigationPreview.Identifier = SanitizeNavigationTargetIdentifier(identifier);

            if (TryResolveNavigationOwnShip(out double ownLatitude, out double ownLongitude, out float ownHeading))
            {
                float relativeBearing = Mathf.Atan2(normalized.x, normalized.y) * Mathf.Rad2Deg;
                float absoluteBearing = enableTrackUpMode
                    ? NormalizeDegrees(ownHeading + relativeBearing)
                    : NormalizeDegrees(relativeBearing);
                float distance = Mathf.Max(0.05f, magnitude * Mathf.Max(0.1f, rangeNM));
                CalculateDestination(
                    ownLatitude,
                    ownLongitude,
                    absoluteBearing,
                    distance,
                    out _navigationPreviewLatitude,
                    out _navigationPreviewLongitude);
                _navigationPreviewHasGeoPosition = true;
            }

            RefreshNavigationPreview(true);
            ApplyNavigationPreviewVisual(true);
            return true;
        }

        public bool SetNavigationPreviewFromScreenPoint(
            Vector2 screenPoint,
            Camera eventCamera,
            string identifier = "MAP")
        {
            if (!CanSetNavigationTarget)
            {
                return false;
            }

            if (rectTransform == null)
            {
                rectTransform = GetComponent<RectTransform>();
            }

            return rectTransform != null &&
                   RectTransformUtility.ScreenPointToLocalPointInRectangle(
                       rectTransform,
                       screenPoint,
                       eventCamera,
                       out Vector2 localPoint) &&
                   SetNavigationPreviewFromLocalPoint(localPoint, identifier);
        }

        /// <summary>
        /// Update the candidate directly from signed decimal degrees. This is
        /// used by the coordinate-entry dialog and supports precise ±0.001°
        /// adjustments without changing the active target.
        /// </summary>
        public bool SetNavigationPreview(double latitude, double longitude, string identifier = "LAT/LON")
        {
            if (!CanSetNavigationTarget || !IsValidGeoPosition(latitude, longitude))
            {
                return false;
            }

            _navigationPreview = RadarNavigationTarget.Empty;
            _navigationPreview.IsValid = true;
            _navigationPreview.Identifier = SanitizeNavigationTargetIdentifier(identifier);
            _navigationPreviewHasGeoPosition = true;
            _navigationPreviewLatitude = latitude;
            _navigationPreviewLongitude = longitude;
            if (_navigationPreviewLocalNormalized.sqrMagnitude <= 0.0001f)
            {
                _navigationPreviewLocalNormalized = Vector2.up * 0.035f;
            }

            RefreshNavigationPreview(true);
            ApplyNavigationPreviewVisual(true);
            return true;
        }

        public bool CommitNavigationPreview()
        {
            if (!CanSetNavigationTarget || !_navigationPreview.IsValid)
            {
                return false;
            }

            _navigationTarget = _navigationPreview;
            _navigationTargetLocalNormalized = _navigationPreviewLocalNormalized;
            _navigationTargetHasGeoPosition = _navigationPreviewHasGeoPosition;
            _navigationTargetLatitude = _navigationPreviewLatitude;
            _navigationTargetLongitude = _navigationPreviewLongitude;
            RefreshNavigationTarget(true);
            ApplyNavigationTargetVisual(true);
            ClearNavigationPreviewInternal(false);
            return true;
        }

        public void ClearNavigationPreview()
        {
            ClearNavigationPreviewInternal(true);
        }

        private void ClearNavigationPreviewInternal(bool notify)
        {
            bool hadPreview = _navigationPreview.IsValid || _navigationPreviewHasGeoPosition;
            _navigationPreview = RadarNavigationTarget.Empty;
            _navigationPreviewLocalNormalized = Vector2.zero;
            _navigationPreviewHasGeoPosition = false;
            _navigationPreviewLatitude = 0d;
            _navigationPreviewLongitude = 0d;
            _navigationPreviewPulse = 0f;
            ApplyNavigationPreviewVisual(true);
            if (notify && hadPreview)
            {
                NavigationPreviewChanged?.Invoke(_navigationPreview);
            }
        }

        /// <summary>
        /// Remove the active map target and hide its radar/HUD cues.
        /// </summary>
        public void ClearNavigationTarget()
        {
            if (!_navigationTarget.IsValid && !_navigationTargetHasGeoPosition)
            {
                return;
            }

            _navigationTarget = RadarNavigationTarget.Empty;
            _navigationTargetHasGeoPosition = false;
            _navigationTargetLatitude = 0d;
            _navigationTargetLongitude = 0d;
            _navigationTargetLocalNormalized = Vector2.zero;
            _navigationTargetPulse = 0f;
            _lastPublishedNavigationTarget = RadarNavigationTarget.Empty;
            _hasPublishedNavigationTarget = true;
            ApplyNavigationTargetVisual(true);
            NavigationTargetChanged?.Invoke(_navigationTarget);
        }

        public void ToggleNavigationTargetVisibility()
        {
            ShowNavigationTarget = !showNavigationTarget;
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
        /// Cycle the chart source with a restrained dip-and-return transition.
        /// The previous composite remains available while the provider loads
        /// replacement tiles, avoiding a blank radar during network latency.
        /// </summary>
        public void CycleMapSourceAnimated()
        {
            if (chartProvider == null)
            {
                return;
            }

            if (!showChartBackground)
            {
                SetChartBackgroundVisible(true, true);
            }

            if (!Application.isPlaying || !enableChartFadeAnimation || chartFadeDuration <= 0.001f)
            {
                chartProvider.CycleMapSource();
                return;
            }

            StartCoroutine(AnimateMapSourceChange());
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
            if (radarImage != null)
            {
                string currentName = radarImage.gameObject.name;
                if (currentName.IndexOf("radar", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    // Do not feed MapCanvas/Map Image render textures into
                    // the generated traffic scope. That legacy fallback was
                    // the source of stretched/blank chart presentations.
                    radarImage = null;
                }
            }

            if (radarImage == null)
            {
                foreach (RawImage image in GetComponentsInChildren<RawImage>(true))
                {
                    if (image == null || image == chartBackgroundImage)
                    {
                        continue;
                    }

                    string imageName = image.gameObject.name;
                    if (imageName.IndexOf("radar", StringComparison.OrdinalIgnoreCase) >= 0)
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

            RectTransform parentRectForValidation = chartRect.parent as RectTransform;
            Vector2 parentSizeForValidation = parentRectForValidation != null
                ? parentRectForValidation.rect.size
                : Vector2.zero;
            float validationWidth = parentSizeForValidation.x > 1f ? parentSizeForValidation.x : displaySize;
            float validationHeight = parentSizeForValidation.y > 1f ? parentSizeForValidation.y : displaySize;
            bool staleBaseAfterFocus = !_isFullscreen && _chartBaseLayoutStored &&
                                       (_chartBaseSizeDelta.x > validationWidth * 1.6f ||
                                        _chartBaseSizeDelta.y > validationHeight * 1.6f ||
                                        chartRect.rect.width > validationWidth * 1.6f ||
                                        chartRect.rect.height > validationHeight * 1.6f);

            if (!_chartBaseLayoutStored || _chartBaseRect != chartRect || staleBaseAfterFocus)
            {
                RectTransform parentRect = chartRect.parent as RectTransform;
                Vector2 parentSize = parentRect != null ? parentRect.rect.size : Vector2.zero;
                float parentWidth = parentSize.x > 1f ? parentSize.x : displaySize;
                float parentHeight = parentSize.y > 1f ? parentSize.y : displaySize;
                Vector2 currentSize = chartRect.rect.size;

                // Legacy scene snapshots sometimes retain the fullscreen
                // coverage margin (over 1,300 px) in the compact chart's
                // serialized sizeDelta. Normalize that stale layout once so
                // REST always returns to a square map that fits the scope.
                bool staleFullscreenLayout = currentSize.x > parentWidth * 1.6f ||
                                             currentSize.y > parentHeight * 1.6f;
                bool stretched = chartRect.anchorMin != chartRect.anchorMax;
                if (staleFullscreenLayout || stretched)
                {
                    float diameter = Mathf.Min(parentWidth, parentHeight);
                    chartRect.anchorMin = new Vector2(0.5f, 0.5f);
                    chartRect.anchorMax = new Vector2(0.5f, 0.5f);
                    chartRect.pivot = new Vector2(0.5f, 0.5f);
                    chartRect.anchoredPosition = Vector2.zero;
                    chartRect.sizeDelta = new Vector2(diameter, diameter);
                    currentSize = chartRect.rect.size;
                }

                _chartBaseAnchoredPosition = chartRect.anchoredPosition;
                _chartBaseSizeDelta = currentSize;
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
                // Cover the entire maximized scope plus the full radial pan
                // budget. Basing this on the current parent (instead of a
                // stale authored sizeDelta) prevents transparent crescents
                // and keeps the chart centred while the root animates.
                chartRect.anchorMin = new Vector2(0.5f, 0.5f);
                chartRect.anchorMax = new Vector2(0.5f, 0.5f);
                chartRect.pivot = new Vector2(0.5f, 0.5f);
                chartRect.sizeDelta = new Vector2(parentWidth, parentHeight) + coverageMargin;
            }
            else
            {
                chartRect.anchorMin = new Vector2(0.5f, 0.5f);
                chartRect.anchorMax = new Vector2(0.5f, 0.5f);
                chartRect.pivot = new Vector2(0.5f, 0.5f);
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
                chartTexture.filterMode = FilterMode.Bilinear;
                chartTexture.wrapMode = TextureWrapMode.Clamp;
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

        private void EnsureNavigationTargetGraphic()
        {
            if (_navigationTargetGraphic != null)
            {
                if (!_navigationTargetGraphic.gameObject.activeSelf)
                {
                    _navigationTargetGraphic.gameObject.SetActive(true);
                }
                _navigationTargetGraphic.raycastTarget = false;
                _navigationTargetGraphic.transform.SetAsLastSibling();
                return;
            }

            Transform existing = transform.Find("Navigation Target Overlay");
            GameObject targetObject = existing != null
                ? existing.gameObject
                : new GameObject(
                    "Navigation Target Overlay",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(RadarNavigationTargetGraphic));
            targetObject.transform.SetParent(transform, false);
            targetObject.SetActive(true);
            RectTransform targetRect = targetObject.GetComponent<RectTransform>() ??
                                       targetObject.AddComponent<RectTransform>();
            targetRect.anchorMin = Vector2.zero;
            targetRect.anchorMax = Vector2.one;
            targetRect.offsetMin = Vector2.zero;
            targetRect.offsetMax = Vector2.zero;
            targetRect.pivot = new Vector2(0.5f, 0.5f);
            targetRect.localScale = Vector3.one;
            targetRect.localRotation = Quaternion.identity;

            _navigationTargetGraphic = targetObject.GetComponent<RadarNavigationTargetGraphic>() ??
                                        targetObject.AddComponent<RadarNavigationTargetGraphic>();
            _navigationTargetGraphic.raycastTarget = false;
            targetObject.transform.SetAsLastSibling();
        }

        private void EnsureNavigationPreviewGraphic()
        {
            if (_navigationPreviewGraphic != null)
            {
                _navigationPreviewGraphic.raycastTarget = false;
                _navigationPreviewGraphic.transform.SetAsLastSibling();
                return;
            }

            Transform existing = transform.Find("Navigation Preview Overlay");
            GameObject previewObject = existing != null
                ? existing.gameObject
                : new GameObject(
                    "Navigation Preview Overlay",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(RadarNavigationTargetGraphic));
            previewObject.transform.SetParent(transform, false);
            RectTransform previewRect = previewObject.GetComponent<RectTransform>() ??
                                        previewObject.AddComponent<RectTransform>();
            previewRect.anchorMin = Vector2.zero;
            previewRect.anchorMax = Vector2.one;
            previewRect.offsetMin = Vector2.zero;
            previewRect.offsetMax = Vector2.zero;
            previewRect.pivot = new Vector2(0.5f, 0.5f);
            previewRect.localScale = Vector3.one;
            previewRect.localRotation = Quaternion.identity;

            _navigationPreviewGraphic = previewObject.GetComponent<RadarNavigationTargetGraphic>() ??
                                         previewObject.AddComponent<RadarNavigationTargetGraphic>();
            _navigationPreviewGraphic.raycastTarget = false;
            previewObject.transform.SetAsLastSibling();
        }

        private void UpdateNavigationTarget()
        {
            if (!_navigationTarget.IsValid)
            {
                ApplyNavigationTargetVisual(false);
                return;
            }

            _navigationTargetPulse += Time.unscaledDeltaTime *
                                      Mathf.Clamp(navigationTargetPulseSpeed, 0f, NavigationTargetPulseRateCap);
            RefreshNavigationTarget(false);
            ApplyNavigationTargetVisual(false);
        }

        private void UpdateNavigationPreview()
        {
            if (!_navigationPreview.IsValid)
            {
                ApplyNavigationPreviewVisual(false);
                return;
            }

            _navigationPreviewPulse += Time.unscaledDeltaTime *
                                       Mathf.Clamp(navigationTargetPulseSpeed, 0f, NavigationTargetPulseRateCap);
            RefreshNavigationPreview(false);
            ApplyNavigationPreviewVisual(false);
        }

        private void RefreshNavigationPreview(bool notify)
        {
            if (!_navigationPreview.IsValid)
            {
                return;
            }

            // Reuse the production projection math without allowing the
            // temporary candidate to publish NavigationTargetChanged or
            // replace the committed target. All fields are restored before
            // returning to the caller, so HUD consumers remain untouched.
            RadarNavigationTarget savedTarget = _navigationTarget;
            RadarNavigationTarget savedPublished = _lastPublishedNavigationTarget;
            Vector2 savedLocal = _navigationTargetLocalNormalized;
            bool savedHasGeo = _navigationTargetHasGeoPosition;
            double savedLatitude = _navigationTargetLatitude;
            double savedLongitude = _navigationTargetLongitude;
            bool savedHasPublished = _hasPublishedNavigationTarget;

            _navigationTarget = _navigationPreview;
            _navigationTargetLocalNormalized = _navigationPreviewLocalNormalized;
            _navigationTargetHasGeoPosition = _navigationPreviewHasGeoPosition;
            _navigationTargetLatitude = _navigationPreviewLatitude;
            _navigationTargetLongitude = _navigationPreviewLongitude;
            _hasPublishedNavigationTarget = true;
            _lastPublishedNavigationTarget = _navigationPreview;
            RefreshNavigationTarget(notify, true);

            _navigationPreview = _navigationTarget;
            _navigationPreviewLocalNormalized = _navigationTargetLocalNormalized;
            _navigationPreviewHasGeoPosition = _navigationTargetHasGeoPosition;
            _navigationPreviewLatitude = _navigationTargetLatitude;
            _navigationPreviewLongitude = _navigationTargetLongitude;

            _navigationTarget = savedTarget;
            _navigationTargetLocalNormalized = savedLocal;
            _navigationTargetHasGeoPosition = savedHasGeo;
            _navigationTargetLatitude = savedLatitude;
            _navigationTargetLongitude = savedLongitude;
            _lastPublishedNavigationTarget = savedPublished;
            _hasPublishedNavigationTarget = savedHasPublished;

            if (notify)
            {
                NavigationPreviewChanged?.Invoke(_navigationPreview);
            }
        }

        private void RefreshNavigationTarget(bool notify, bool suppressEvent = false)
        {
            if (!_navigationTarget.IsValid)
            {
                return;
            }

            bool ownPositionValid = TryResolveNavigationOwnShip(
                out double ownLatitude,
                out double ownLongitude,
                out float ownHeading);

            if (!_navigationTargetHasGeoPosition && ownPositionValid)
            {
                float localRelativeBearing = Mathf.Atan2(
                    _navigationTargetLocalNormalized.x,
                    _navigationTargetLocalNormalized.y) * Mathf.Rad2Deg;
                float absoluteBearing = enableTrackUpMode
                    ? NormalizeDegrees(ownHeading + localRelativeBearing)
                    : NormalizeDegrees(localRelativeBearing);
                float localDistance = _navigationTargetLocalNormalized.magnitude *
                                      Mathf.Max(0.1f, rangeNM);
                CalculateDestination(
                    ownLatitude,
                    ownLongitude,
                    absoluteBearing,
                    Mathf.Max(0.05f, localDistance),
                    out _navigationTargetLatitude,
                    out _navigationTargetLongitude);
                _navigationTargetHasGeoPosition = true;
            }

            RadarNavigationTarget next = _navigationTarget;
            next.IsValid = true;
            next.HasGeoPosition = _navigationTargetHasGeoPosition;
            next.Latitude = _navigationTargetHasGeoPosition ? _navigationTargetLatitude : 0d;
            next.Longitude = _navigationTargetHasGeoPosition ? _navigationTargetLongitude : 0d;

            float bearing = 0f;
            float distance = _navigationTargetLocalNormalized.magnitude * Mathf.Max(0.1f, rangeNM);
            float displayRelativeBearing = Mathf.Atan2(
                _navigationTargetLocalNormalized.x,
                _navigationTargetLocalNormalized.y) * Mathf.Rad2Deg;
            float pilotRelativeBearing = displayRelativeBearing;

            if (_navigationTargetHasGeoPosition && ownPositionValid)
            {
                distance = (float)CalculateDistanceNM(
                    ownLatitude,
                    ownLongitude,
                    _navigationTargetLatitude,
                    _navigationTargetLongitude);
                bearing = CalculateBearingDegrees(
                    ownLatitude,
                    ownLongitude,
                    _navigationTargetLatitude,
                    _navigationTargetLongitude);
                // The radar vector follows the selected presentation (track
                // up or north up), while HUD course bars always need the
                // bearing relative to the aircraft nose.
                pilotRelativeBearing = Mathf.DeltaAngle(ownHeading, bearing);
                displayRelativeBearing = enableTrackUpMode
                    ? Mathf.DeltaAngle(ownHeading, bearing)
                    : bearing;
            }
            else
            {
                bearing = enableTrackUpMode && ownPositionValid
                    ? NormalizeDegrees(ownHeading + displayRelativeBearing)
                    : NormalizeDegrees(displayRelativeBearing);
                pilotRelativeBearing = ownPositionValid
                    ? Mathf.DeltaAngle(0f, enableTrackUpMode
                        ? displayRelativeBearing
                        : displayRelativeBearing - ownHeading)
                    : displayRelativeBearing;
            }

            float safeRange = Mathf.Max(0.1f, rangeNM);
            bool withinRange = distance <= safeRange + 0.01f;
            float radial = Mathf.Clamp(distance / safeRange, 0f, 1f);
            float edgeFraction = Mathf.Clamp(navigationTargetEdgeFraction, 0.72f, 0.96f);
            radial = Mathf.Clamp(radial, 0.035f, edgeFraction);
            float displayRadians = displayRelativeBearing * Mathf.Deg2Rad;
            Vector2 radarPosition = new Vector2(
                Mathf.Sin(displayRadians) * radial,
                Mathf.Cos(displayRadians) * radial);

            // During a focused chart inspection the texture moves under the
            // fixed radar frame. Keep the selected marker attached to the
            // tapped chart feature for that transient view; the aircraft
            // relative vector below intentionally stays geographic so HUD
            // guidance bars do not jump with a visual pan.
            Rect navigationDisplayRect = rectTransform != null ? rectTransform.rect : new Rect();
            if (_mapPan.sqrMagnitude > 0.001f &&
                navigationDisplayRect.width > 1f && navigationDisplayRect.height > 1f)
            {
                float displayRadiusPixels = Mathf.Min(
                    navigationDisplayRect.width,
                    navigationDisplayRect.height) * 0.5f;
                radarPosition += _mapPan / Mathf.Max(1f, displayRadiusPixels);
                if (radarPosition.sqrMagnitude > edgeFraction * edgeFraction)
                {
                    radarPosition = radarPosition.normalized * edgeFraction;
                }
            }
            float pilotRadians = pilotRelativeBearing * Mathf.Deg2Rad;
            Vector2 aircraftRelativePosition = new Vector2(
                Mathf.Sin(pilotRadians) * radial,
                Mathf.Cos(pilotRadians) * radial);

            next.BearingDegrees = NormalizeDegrees(bearing);
            next.DistanceNM = Mathf.Max(0f, distance);
            next.RelativeBearingDegrees = Mathf.DeltaAngle(0f, pilotRelativeBearing);
            next.RadarPosition = radarPosition;
            next.AircraftRelativePosition = aircraftRelativePosition;
            next.IsWithinRange = withinRange;
            _navigationTarget = next;

            if (!suppressEvent &&
                (notify || !_hasPublishedNavigationTarget || NavigationTargetChangedSignificantly(
                    _lastPublishedNavigationTarget,
                    _navigationTarget)))
            {
                _lastPublishedNavigationTarget = _navigationTarget;
                _hasPublishedNavigationTarget = true;
                NavigationTargetChanged?.Invoke(_navigationTarget);
            }
        }

        private void ApplyNavigationTargetVisual(bool immediate)
        {
            EnsureNavigationTargetGraphic();
            if (_navigationTargetGraphic == null)
            {
                return;
            }

            bool visible = isActiveAndEnabled && showNavigationTarget && _navigationTarget.IsValid;
            _navigationTargetGraphic.SetTarget(
                _navigationTarget.RadarPosition,
                visible,
                _navigationTarget.IsWithinRange,
                navigationTargetColor,
                _navigationTargetPulse,
                immediate);
        }

        private void ApplyNavigationPreviewVisual(bool immediate)
        {
            EnsureNavigationPreviewGraphic();
            if (_navigationPreviewGraphic == null)
            {
                return;
            }

            bool visible = isActiveAndEnabled && _navigationPreview.IsValid;
            _navigationPreviewGraphic.SetTarget(
                _navigationPreview.RadarPosition,
                visible,
                _navigationPreview.IsWithinRange,
                new Color(0.28f, 0.88f, 1f, 0.92f),
                _navigationPreviewPulse,
                immediate,
                true);
        }

        private bool TryResolveNavigationOwnShip(
            out double latitude,
            out double longitude,
            out float heading)
        {
            latitude = 0d;
            longitude = 0d;
            heading = 0f;

            if (!TryResolveChartPosition(out float chartLatitude, out float chartLongitude))
            {
                return false;
            }

            latitude = chartLatitude;
            longitude = chartLongitude;
            if (radarController != null)
            {
                heading = radarController.OwnPosition.HeadingDegrees;
            }

            if (float.IsNaN(heading) || float.IsInfinity(heading))
            {
                heading = 0f;
            }

            heading = NormalizeDegrees(heading);
            return true;
        }

        private static bool NavigationTargetChangedSignificantly(
            RadarNavigationTarget previous,
            RadarNavigationTarget next)
        {
            if (previous.IsValid != next.IsValid ||
                previous.HasGeoPosition != next.HasGeoPosition ||
                previous.IsWithinRange != next.IsWithinRange ||
                !string.Equals(previous.Identifier, next.Identifier, StringComparison.Ordinal))
            {
                return true;
            }

            if (!next.IsValid)
            {
                return false;
            }

            return Math.Abs(previous.Latitude - next.Latitude) > 0.00001d ||
                   Math.Abs(previous.Longitude - next.Longitude) > 0.00001d ||
                   Mathf.Abs(Mathf.DeltaAngle(previous.BearingDegrees, next.BearingDegrees)) > 0.05f ||
                   Mathf.Abs(previous.DistanceNM - next.DistanceNM) > 0.05f ||
                   (previous.RadarPosition - next.RadarPosition).sqrMagnitude > 0.000004f;
        }

        private static string SanitizeNavigationTargetIdentifier(string identifier)
        {
            string value = string.IsNullOrWhiteSpace(identifier)
                ? "MAP"
                : identifier.Trim().ToUpperInvariant();
            if (value.Length > 12)
            {
                value = value.Substring(0, 12);
            }

            return value;
        }

        private static float NormalizeDegrees(float degrees)
        {
            if (float.IsNaN(degrees) || float.IsInfinity(degrees))
            {
                return 0f;
            }

            degrees %= 360f;
            if (degrees < 0f)
            {
                degrees += 360f;
            }

            return degrees;
        }

        private const double EarthRadiusNM = 3440.065;

        private static double CalculateDistanceNM(
            double latitudeA,
            double longitudeA,
            double latitudeB,
            double longitudeB)
        {
            double latA = latitudeA * Math.PI / 180d;
            double latB = latitudeB * Math.PI / 180d;
            double deltaLat = (latitudeB - latitudeA) * Math.PI / 180d;
            double deltaLon = (longitudeB - longitudeA) * Math.PI / 180d;
            double sinLat = Math.Sin(deltaLat * 0.5d);
            double sinLon = Math.Sin(deltaLon * 0.5d);
            double a = sinLat * sinLat + Math.Cos(latA) * Math.Cos(latB) * sinLon * sinLon;
            double arc = 2d * Math.Atan2(Math.Sqrt(Math.Max(0d, a)), Math.Sqrt(Math.Max(0d, 1d - a)));
            return arc * EarthRadiusNM;
        }

        private static float CalculateBearingDegrees(
            double latitudeA,
            double longitudeA,
            double latitudeB,
            double longitudeB)
        {
            double latA = latitudeA * Math.PI / 180d;
            double latB = latitudeB * Math.PI / 180d;
            double deltaLon = (longitudeB - longitudeA) * Math.PI / 180d;
            double y = Math.Sin(deltaLon) * Math.Cos(latB);
            double x = Math.Cos(latA) * Math.Sin(latB) -
                       Math.Sin(latA) * Math.Cos(latB) * Math.Cos(deltaLon);
            return NormalizeDegrees((float)(Math.Atan2(y, x) * 180d / Math.PI));
        }

        private static void CalculateDestination(
            double latitude,
            double longitude,
            float bearingDegrees,
            float distanceNM,
            out double destinationLatitude,
            out double destinationLongitude)
        {
            double angularDistance = Math.Max(0d, distanceNM) / EarthRadiusNM;
            double bearing = bearingDegrees * Math.PI / 180d;
            double lat1 = latitude * Math.PI / 180d;
            double lon1 = longitude * Math.PI / 180d;
            double sinLat = Math.Sin(lat1) * Math.Cos(angularDistance) +
                            Math.Cos(lat1) * Math.Sin(angularDistance) * Math.Cos(bearing);
            double lat2 = Math.Asin(Math.Max(-1d, Math.Min(1d, sinLat)));
            double lon2 = lon1 + Math.Atan2(
                Math.Sin(bearing) * Math.Sin(angularDistance) * Math.Cos(lat1),
                Math.Cos(angularDistance) - Math.Sin(lat1) * Math.Sin(lat2));

            destinationLatitude = lat2 * 180d / Math.PI;
            destinationLongitude = NormalizeLongitude(lon2 * 180d / Math.PI);
        }

        private static double NormalizeLongitude(double longitude)
        {
            longitude %= 360d;
            if (longitude > 180d)
            {
                longitude -= 360d;
            }
            else if (longitude < -180d)
            {
                longitude += 360d;
            }

            return longitude;
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

            // Compass ticks sit beneath the range gates. Drawing the gates
            // last keeps every circle continuous where the two references
            // intersect instead of producing dark, broken-looking notches.
            DrawCompassMarkings(centerX, centerY, radius);

            // Draw range rings
            DrawRangeRings(centerX, centerY, radius);

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
            if (_lineworkVisualAlpha <= 0.001f)
            {
                return;
            }

            int ringCount = Mathf.Clamp(rangeRingCount, 1, 8);
            float lineScale = ResolvePilotLineworkScale();
            Color visibleRingColor = MultiplyAlpha(rangeRingColor, _lineworkVisualAlpha);

            if (!usePilotLinework)
            {
                for (int i = 1; i <= ringCount; i++)
                {
                    float ringRadius = radius * i / ringCount;
                    DrawCircle(centerX, centerY, (int)ringRadius, visibleRingColor, 1);
                }

                return;
            }

            // The outside and half-range gates are the two lines pilots use
            // most often. Give them a deliberate visual weight while keeping
            // the intermediate gates quiet over a dense sectional chart.
            // The small lift in value/alpha is intentional: the chart is
            // rendered beneath this texture and can contain very bright ink.
            Color majorColor = LiftLineColor(
                visibleRingColor,
                0.20f,
                Mathf.Clamp01(Mathf.Max(0.96f, rangeRingColor.a + 0.30f)) * _lineworkVisualAlpha);
            Color minorColor = LiftLineColor(
                visibleRingColor,
                0.10f,
                Mathf.Clamp01(Mathf.Max(0.76f, rangeRingColor.a * 1.25f)) * _lineworkVisualAlpha);
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
                    WithAlpha(haloColor, (isMajor ? 0.42f : 0.24f) * _lineworkVisualAlpha),
                    thickness + 2.8f * lineScale);
                DrawCircleAntiAliased(centerX, centerY, ringRadius, color, thickness);
            }
        }

        private void DrawCompassMarkings(int centerX, int centerY, float radius)
        {
            if (_lineworkVisualAlpha <= 0.001f)
            {
                return;
            }

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
                Mathf.Clamp01(Mathf.Max(0.94f, compassMarkingsColor.a + 0.10f)) * _lineworkVisualAlpha);
            Color majorColor = LiftLineColor(
                compassMarkingsColor,
                0.06f,
                Mathf.Clamp01(Mathf.Max(0.84f, compassMarkingsColor.a * 0.90f)) * _lineworkVisualAlpha);
            Color minorColor = LiftLineColor(
                compassMarkingsColor,
                0.02f,
                Mathf.Clamp01(Mathf.Max(0.62f, compassMarkingsColor.a * 0.70f)) * _lineworkVisualAlpha);
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
                    WithAlpha(haloColor, (isCardinal ? 0.42f : 0.22f) * _lineworkVisualAlpha),
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
                        WithAlpha(haloColor, 0.12f * _lineworkVisualAlpha),
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

            Color labelColor = LiftLineColor(
                compassMarkingsColor,
                0.04f,
                Mathf.Max(0.86f, compassMarkingsColor.a) * _lineworkVisualAlpha);
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
            Color visibleCompassColor = MultiplyAlpha(compassMarkingsColor, _lineworkVisualAlpha);
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

                DrawLine(x1, y1, x2, y2, visibleCompassColor);
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

                DrawLine(
                    x1,
                    y1,
                    x2,
                    y2,
                    new Color(
                        compassMarkingsColor.r,
                        compassMarkingsColor.g,
                        compassMarkingsColor.b,
                        0.4f * _lineworkVisualAlpha));
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

        private static Color MultiplyAlpha(Color color, float multiplier)
        {
            color.a *= Mathf.Clamp01(multiplier);
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
            float outerRadius = safeRadius + halfThickness + 0.75f;
            float innerRadius = Mathf.Max(0f, safeRadius - halfThickness - 0.75f);
            float outerSquared = outerRadius * outerRadius;
            float innerSquared = innerRadius * innerRadius;
            int extent = Mathf.CeilToInt(outerRadius);

            // Walk the exact narrow annulus, including the caps above and
            // below the mathematical centre line. The old centre-line-only
            // scan skipped those cap pixels and produced visible gaps at the
            // cardinal points once the texture was scaled in fullscreen/XR.
            for (int y = -extent; y <= extent; y++)
            {
                float ySquared = y * y;
                float outerInside = outerSquared - ySquared;
                if (outerInside < 0f)
                {
                    continue;
                }

                int outerX = Mathf.CeilToInt(Mathf.Sqrt(outerInside));
                int innerX = 0;
                float innerInside = innerSquared - ySquared;
                if (innerInside > 0f)
                {
                    innerX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Sqrt(innerInside)) - 1);
                }

                for (int x = innerX; x <= outerX; x++)
                {
                    BlendCircleSample(cx + x, cy + y, cx, cy, safeRadius, halfThickness, color);
                    if (x > 0)
                    {
                        BlendCircleSample(cx - x, cy + y, cx, cy, safeRadius, halfThickness, color);
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
            // A compact top-down aircraft reads much faster than the previous
            // solid triangle and cannot be mistaken for a traffic-threat
            // symbol. A dark knockout stroke preserves the silhouette over
            // dense sectional-chart ink without adding an opaque placard.
            float scale = Mathf.Max(0.85f, symbolSize / 12f);
            int noseY = centerY + Mathf.RoundToInt(11f * scale);
            int tailY = centerY - Mathf.RoundToInt(10f * scale);
            int wingRootY = centerY + Mathf.RoundToInt(2f * scale);
            int wingTipY = centerY - Mathf.RoundToInt(2f * scale);
            int wingHalfSpan = Mathf.RoundToInt(12f * scale);
            int tailRootY = centerY - Mathf.RoundToInt(6f * scale);
            int tailTipY = centerY - Mathf.RoundToInt(8f * scale);
            int tailHalfSpan = Mathf.RoundToInt(6f * scale);

            Color knockout = new Color(0.002f, 0.020f, 0.026f, 0.92f);
            Color body = LiftLineColor(ownAircraftColor, 0.12f, 1f);
            float knockoutWidth = Mathf.Max(4.6f, 5.2f * scale);
            float bodyWidth = Mathf.Max(2.0f, 2.35f * scale);

            // Knockout silhouette.
            DrawLineAntiAliased(centerX, noseY, centerX, tailY, knockout, knockoutWidth);
            DrawLineAntiAliased(centerX - wingHalfSpan, wingTipY, centerX, wingRootY, knockout, knockoutWidth);
            DrawLineAntiAliased(centerX, wingRootY, centerX + wingHalfSpan, wingTipY, knockout, knockoutWidth);
            DrawLineAntiAliased(centerX - tailHalfSpan, tailTipY, centerX, tailRootY, knockout, knockoutWidth);
            DrawLineAntiAliased(centerX, tailRootY, centerX + tailHalfSpan, tailTipY, knockout, knockoutWidth);

            // Bright aircraft stroke with swept wings and a distinct tailplane.
            DrawLineAntiAliased(centerX, noseY, centerX, tailY, body, bodyWidth);
            DrawLineAntiAliased(centerX - wingHalfSpan, wingTipY, centerX, wingRootY, body, bodyWidth);
            DrawLineAntiAliased(centerX, wingRootY, centerX + wingHalfSpan, wingTipY, body, bodyWidth);
            DrawLineAntiAliased(centerX - tailHalfSpan, tailTipY, centerX, tailRootY, body, bodyWidth);
            DrawLineAntiAliased(centerX, tailRootY, centerX + tailHalfSpan, tailTipY, body, bodyWidth);

            // The small hub marks the exact map/aircraft reference point.
            DrawCircleAntiAliased(
                centerX,
                centerY,
                Mathf.Max(2.2f, 2.5f * scale),
                LiftLineColor(ownAircraftColor, 0.38f, 1f),
                Mathf.Max(1.2f, 1.35f * scale));
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

    /// <summary>
    /// Crisp, resolution-independent target cue layered over the generated
    /// radar texture.  Keeping this as a MaskableGraphic avoids pixelation
    /// when the XR-3 simulator expands the scope to fullscreen.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    internal sealed class RadarNavigationTargetGraphic : MaskableGraphic
    {
        private Vector2 _normalizedPosition;
        private Color _accent = new Color(1f, 0.78f, 0.28f, 1f);
        private bool _visible;
        private bool _withinRange;
        private float _pulse;
        private bool _preview;

        public void SetTarget(
            Vector2 normalizedPosition,
            bool visible,
            bool withinRange,
            Color accent,
            float pulse,
            bool immediate,
            bool preview = false)
        {
            _normalizedPosition = normalizedPosition;
            _visible = visible;
            _withinRange = withinRange;
            _accent = accent;
            _pulse = pulse;
            _preview = preview;
            raycastTarget = false;
            // The immediate flag is intentionally accepted by the display API
            // so callers can use the same path for placement and animation.
            // Mesh regeneration itself is frame-safe in both cases.
            SetVerticesDirty();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            raycastTarget = false;
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            if (!_visible)
            {
                return;
            }

            Rect rect = rectTransform.rect;
            float scopeRadius = Mathf.Max(24f, Mathf.Min(rect.width, rect.height) * 0.5f - 10f);
            Vector2 center = rect.center;
            Vector2 position = _normalizedPosition;
            if (position.sqrMagnitude <= 0.0001f)
            {
                position = Vector2.up * 0.035f;
            }

            Vector2 direction = position.normalized;
            Vector2 target = center + position * scopeRadius;
            Color tint = _withinRange
                ? _accent
                : new Color(1f, 0.58f, 0.18f, Mathf.Max(0.9f, _accent.a));

            float sizeScale = _preview ? 0.72f : 0.62f;
            float stemDarkWidth = _preview ? 3.2f : 3.6f;
            float stemWidth = _preview ? 1.15f : 1.05f;

            // A dark under-stroke protects the cue over dense sectional ink.
            AddLine(
                vertexHelper,
                center,
                target,
                stemDarkWidth,
                new Color(0.002f, 0.018f, 0.022f, 0.82f));
            AddLine(
                vertexHelper,
                center,
                target,
                stemWidth,
                new Color(tint.r, tint.g, tint.b, _preview ? 0.72f : 0.78f));

            // The diamond is deliberately asymmetrical (a small lead point)
            // so it reads as a selected navigation target rather than traffic.
            AddDiamond(
                vertexHelper,
                target,
                8.5f * sizeScale,
                new Color(tint.r, tint.g, tint.b, _preview ? 0.18f : 0.20f));
            AddDiamondOutline(
                vertexHelper,
                target,
                9.5f * sizeScale,
                _preview ? 1.15f : 1.25f,
                new Color(tint.r, tint.g, tint.b, _preview ? 0.92f : 1f));
            AddDisc(
                vertexHelper,
                target,
                _preview ? 1.55f : 1.7f,
                new Color(0.98f, 1f, 0.92f, 1f),
                16);

            float pulsePhase = Mathf.Repeat(_pulse, 1f);
            float pulseRadius = (_preview ? 8f : 9f) + pulsePhase * (_preview ? 7f : 8f);
            float pulseAlpha = (_preview ? 0.34f : 0.38f) * (1f - pulsePhase);
            AddRing(
                vertexHelper,
                target,
                pulseRadius,
                Mathf.Lerp(_preview ? 1.45f : 1.7f, _preview ? 0.55f : 0.65f, pulsePhase),
                new Color(tint.r, tint.g, tint.b, pulseAlpha),
                28);

            if (!_withinRange)
            {
                // Off-range targets stay represented at the perimeter with a
                // directional chevron, so a pilot never loses the bearing.
                Vector2 tip = center + direction * scopeRadius * 0.93f;
                Vector2 basePoint = tip - direction * (_preview ? 9f : 10f);
                Vector2 normal = new Vector2(-direction.y, direction.x);
                AddLine(vertexHelper, tip, basePoint + normal * (_preview ? 5f : 6f), _preview ? 1.35f : 1.6f, tint);
                AddLine(vertexHelper, tip, basePoint - normal * (_preview ? 5f : 6f), _preview ? 1.35f : 1.6f, tint);
            }
        }

        private static void AddDiamond(
            VertexHelper vertexHelper,
            Vector2 center,
            float radius,
            Color color)
        {
            int start = vertexHelper.currentVertCount;
            AddVertex(vertexHelper, center + Vector2.up * radius, color);
            AddVertex(vertexHelper, center + Vector2.right * radius, color);
            AddVertex(vertexHelper, center + Vector2.down * radius, color);
            AddVertex(vertexHelper, center + Vector2.left * radius, color);
            vertexHelper.AddTriangle(start, start + 1, start + 2);
            vertexHelper.AddTriangle(start, start + 2, start + 3);
        }

        private static void AddDiamondOutline(
            VertexHelper vertexHelper,
            Vector2 center,
            float radius,
            float width,
            Color color)
        {
            Vector2 top = center + Vector2.up * radius;
            Vector2 right = center + Vector2.right * radius;
            Vector2 bottom = center + Vector2.down * radius;
            Vector2 left = center + Vector2.left * radius;
            AddLine(vertexHelper, top, right, width, color);
            AddLine(vertexHelper, right, bottom, width, color);
            AddLine(vertexHelper, bottom, left, width, color);
            AddLine(vertexHelper, left, top, width, color);
        }

        private static void AddDisc(
            VertexHelper vertexHelper,
            Vector2 center,
            float radius,
            Color color,
            int segments)
        {
            int centerIndex = vertexHelper.currentVertCount;
            AddVertex(vertexHelper, center, color);
            for (int i = 0; i <= segments; i++)
            {
                float angle = Mathf.PI * 2f * i / segments;
                AddVertex(
                    vertexHelper,
                    center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius,
                    color);
                if (i > 0)
                {
                    vertexHelper.AddTriangle(centerIndex, centerIndex + i, centerIndex + i + 1);
                }
            }
        }

        private static void AddRing(
            VertexHelper vertexHelper,
            Vector2 center,
            float radius,
            float width,
            Color color,
            int segments)
        {
            float inner = Mathf.Max(0f, radius - width * 0.5f);
            float outer = radius + width * 0.5f;
            for (int i = 0; i < segments; i++)
            {
                float angleA = Mathf.PI * 2f * i / segments;
                float angleB = Mathf.PI * 2f * (i + 1) / segments;
                Vector2 directionA = new Vector2(Mathf.Cos(angleA), Mathf.Sin(angleA));
                Vector2 directionB = new Vector2(Mathf.Cos(angleB), Mathf.Sin(angleB));
                AddQuad(
                    vertexHelper,
                    center + directionA * inner,
                    center + directionA * outer,
                    center + directionB * outer,
                    center + directionB * inner,
                    color);
            }
        }

        private static void AddLine(
            VertexHelper vertexHelper,
            Vector2 from,
            Vector2 to,
            float width,
            Color color)
        {
            Vector2 delta = to - from;
            if (delta.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            Vector2 normal = new Vector2(-delta.y, delta.x).normalized * (width * 0.5f);
            AddQuad(
                vertexHelper,
                from - normal,
                from + normal,
                to + normal,
                to - normal,
                color);
        }

        private static void AddQuad(
            VertexHelper vertexHelper,
            Vector2 a,
            Vector2 b,
            Vector2 c,
            Vector2 d,
            Color color)
        {
            int start = vertexHelper.currentVertCount;
            AddVertex(vertexHelper, a, color);
            AddVertex(vertexHelper, b, color);
            AddVertex(vertexHelper, c, color);
            AddVertex(vertexHelper, d, color);
            vertexHelper.AddTriangle(start, start + 1, start + 2);
            vertexHelper.AddTriangle(start, start + 2, start + 3);
        }

        private static void AddVertex(VertexHelper vertexHelper, Vector2 position, Color color)
        {
            UIVertex vertex = UIVertex.simpleVert;
            vertex.position = position;
            vertex.color = color;
            vertexHelper.AddVert(vertex);
        }
    }
}
