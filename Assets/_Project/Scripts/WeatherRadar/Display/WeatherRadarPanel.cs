using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace WeatherRadar
{
    /// <summary>
    /// Main weather radar display panel.
    /// Manages all child renderers and handles data flow from provider to display.
    /// 
    /// TIMING ARCHITECTURE:
    /// This panel is the SINGLE source of truth for update timing.
    /// - sweepCycleDuration: How long for one 360° sweep (e.g., 4 seconds)
    /// - updateEveryNSweeps: Fetch new data every N sweeps (e.g., 1 = every sweep)
    /// 
    /// The weather provider's autoUpdate is disabled - all data refreshes are
    /// triggered by OnSweepComplete(), which calls weatherProvider.RefreshData().
    /// 
    /// Update Calculation:
    /// - Actual update interval = sweepCycleDuration × updateEveryNSweeps
    /// - Example: 4s sweep × 1 sweep = 4 second updates
    /// - Example: 4s sweep × 2 sweeps = 8 second updates
    /// </summary>
    public class WeatherRadarPanel : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private WeatherRadarConfig config;
        [SerializeField] private WeatherRadarDataProvider dataProvider;

        [Header("Display Components")]
        [SerializeField] private RawImage radarDisplay;
        [SerializeField] private RadarSweepRenderer sweepRenderer;
        [SerializeField] private RadarReturnRenderer returnRenderer;
        [SerializeField] private RangeRingsRenderer rangeRingsRenderer;
        [SerializeField] private WaypointOverlayRenderer waypointRenderer;

        [Header("Data Provider")]
        [SerializeField] private WeatherRadarProviderBase weatherProvider;

        [Header("Panel Settings")]
        [SerializeField] private bool isVisible = true;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private RectTransform panelRect;

        [Header("Update Settings")]
        [Tooltip("Time for one full 360° sweep in seconds")]
        [SerializeField] private float sweepCycleDuration = 4f;
        [Tooltip("Update data every N sweeps")]
        [SerializeField] private int updateEveryNSweeps = 1;

        [Header("Info Display")]
        [SerializeField] private TMP_Text rangeLabel;
        [SerializeField] private TMP_Text tiltLabel;
        [SerializeField] private TMP_Text modeLabel;
        [SerializeField] private TMP_Text gainLabel;

        private int sweepCount = 0;
        private float sweepSpeed;
        private bool initialized = false;

        /// <summary>
        /// Current configuration
        /// </summary>
        public WeatherRadarConfig Config => config;

        /// <summary>
        /// Current data provider
        /// </summary>
        public WeatherRadarDataProvider DataProvider => dataProvider;

        /// <summary>
        /// Panel visibility state
        /// </summary>
        public bool IsVisible
        {
            get => isVisible;
            set => SetVisibility(value);
        }

        private void Awake()
        {
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                {
                    canvasGroup = gameObject.AddComponent<CanvasGroup>();
                }
            }

            if (panelRect == null)
            {
                panelRect = GetComponent<RectTransform>();
            }

            // Calculate sweep speed to complete 360° in sweepCycleDuration
            sweepSpeed = 360f / sweepCycleDuration;
        }

        private void Start()
        {
            // Auto-find components if not assigned
            AutoFindComponents();

            // Subscribe to data provider events
            if (dataProvider != null)
            {
                dataProvider.OnRadarDataUpdated += OnRadarDataUpdated;
                dataProvider.OnRangeChanged += OnRangeChanged;
                dataProvider.OnModeChanged += OnModeChanged;
            }

            // Subscribe to weather provider events
            if (weatherProvider != null)
            {
                weatherProvider.OnRadarDataUpdated += OnWeatherDataReceived;
                
                // Disable auto-update on provider - panel controls timing via sweep
                weatherProvider.SetAutoUpdate(false);
            }

            // Initialize child renderers
            InitializeRenderers();

            // Subscribe to sweep complete event
            if (sweepRenderer != null)
            {
                sweepRenderer.OnSweepComplete += OnSweepComplete;
                sweepRenderer.SweepSpeed = sweepSpeed;
            }

            // Apply initial visibility
            SetVisibility(isVisible);

            // Update labels
            UpdateInfoLabels();

            // Request initial data immediately
            StartCoroutine(RequestInitialData());

            initialized = true;
            
            Debug.Log($"[WeatherRadarPanel] Initialized. Sweep cycle: {sweepCycleDuration}s, Update every {updateEveryNSweeps} sweeps");
        }

        private System.Collections.IEnumerator RequestInitialData()
        {
            yield return new WaitForSeconds(0.5f);
            
            if (weatherProvider != null)
            {
                Debug.Log("[WeatherRadarPanel] Requesting initial weather data...");
                weatherProvider.RefreshData();
            }
        }

        private void AutoFindComponents()
        {
            // Find data provider on parent if not assigned
            if (dataProvider == null)
            {
                dataProvider = GetComponentInParent<WeatherRadarDataProvider>();
            }

            // Find weather provider on parent if not assigned
            if (weatherProvider == null)
            {
                weatherProvider = GetComponentInParent<WeatherRadarProviderBase>();
            }

            // Find renderers in children if not assigned
            if (sweepRenderer == null)
            {
                sweepRenderer = GetComponentInChildren<RadarSweepRenderer>();
            }
            if (returnRenderer == null)
            {
                returnRenderer = GetComponentInChildren<RadarReturnRenderer>();
            }
            if (rangeRingsRenderer == null)
            {
                rangeRingsRenderer = GetComponentInChildren<RangeRingsRenderer>();
            }
            if (waypointRenderer == null)
            {
                waypointRenderer = GetComponentInChildren<WaypointOverlayRenderer>();
            }

            // Find labels by name
            var labels = GetComponentsInChildren<TMP_Text>(true);
            foreach (var label in labels)
            {
                string name = label.name.ToLower();
                if (name.Contains("mode") && modeLabel == null)
                    modeLabel = label;
                else if (name.Contains("range") && rangeLabel == null)
                    rangeLabel = label;
                else if (name.Contains("tilt") && tiltLabel == null)
                    tiltLabel = label;
                else if (name.Contains("gain") && gainLabel == null)
                    gainLabel = label;
            }
        }

        private void OnDestroy()
        {
            // Unsubscribe from events
            if (dataProvider != null)
            {
                dataProvider.OnRadarDataUpdated -= OnRadarDataUpdated;
                dataProvider.OnRangeChanged -= OnRangeChanged;
                dataProvider.OnModeChanged -= OnModeChanged;
            }

            if (weatherProvider != null)
            {
                weatherProvider.OnRadarDataUpdated -= OnWeatherDataReceived;
            }

            if (sweepRenderer != null)
            {
                sweepRenderer.OnSweepComplete -= OnSweepComplete;
            }
        }

        private void Update()
        {
            if (!isVisible || dataProvider == null) return;

            if (dataProvider.RadarData.currentMode == RadarMode.STBY)
            {
                return; // Don't animate in standby
            }

            // Advance sweep animation
            if (sweepRenderer != null)
            {
                sweepRenderer.AdvanceSweep(Time.deltaTime);
                dataProvider.SetSweepAngle(sweepRenderer.CurrentAngle);
            }
        }

        private void OnSweepComplete()
        {
            sweepCount++;

            Debug.Log($"[WeatherRadar] Sweep complete! Count: {sweepCount}/{updateEveryNSweeps}");

            // Request new data every N sweeps
            if (sweepCount >= updateEveryNSweeps)
            {
                sweepCount = 0;

                if (weatherProvider != null)
                {
                    weatherProvider.RefreshData();
                }
            }
        }

        private void InitializeRenderers()
        {
            // Initialize sweep renderer
            if (sweepRenderer != null)
            {
                sweepRenderer.Initialize(config, panelRect);
            }

            // Initialize return renderer
            if (returnRenderer != null)
            {
                returnRenderer.Initialize(config, dataProvider);
            }

            // Initialize range rings renderer
            if (rangeRingsRenderer != null)
            {
                rangeRingsRenderer.Initialize(config, dataProvider);
            }

            // Initialize waypoint renderer
            if (waypointRenderer != null)
            {
                waypointRenderer.Initialize(config, dataProvider);
            }
        }

        private void OnRadarDataUpdated(WeatherRadarData data)
        {
            UpdateInfoLabels();

            // Sync settings to weather provider
            if (weatherProvider != null)
            {
                weatherProvider.RangeNM = data.currentRange;
                weatherProvider.TiltDegrees = data.tiltAngle;
                weatherProvider.GainDB = data.gainOffset;
            }
        }

        private void OnRangeChanged(float newRange)
        {
            UpdateInfoLabels();

            // Notify child renderers
            if (rangeRingsRenderer != null)
            {
                rangeRingsRenderer.OnRangeChanged(newRange);
            }
        }

        private void OnModeChanged(RadarMode newMode)
        {
            UpdateInfoLabels();

            // Handle standby mode
            if (sweepRenderer != null)
            {
                sweepRenderer.SetVisible(newMode != RadarMode.STBY);
            }
        }

        private void OnWeatherDataReceived(Texture2D weatherTexture)
        {
            Debug.Log($"[WeatherRadarPanel] Received weather texture: {(weatherTexture != null ? $"{weatherTexture.width}x{weatherTexture.height}" : "null")}");
            
            if (returnRenderer != null && weatherTexture != null)
            {
                returnRenderer.UpdateWeatherData(weatherTexture);
            }
        }

        private void UpdateInfoLabels()
        {
            if (dataProvider == null) return;

            var data = dataProvider.RadarData;

            if (rangeLabel != null)
            {
                rangeLabel.text = $"{data.currentRange:0}nm";
            }

            if (tiltLabel != null)
            {
                string sign = data.tiltAngle >= 0 ? "+" : "";
                tiltLabel.text = $"TLT {sign}{data.tiltAngle:0.0}°";
            }

            if (modeLabel != null)
            {
                modeLabel.text = GetModeDisplayName(data.currentMode);
            }

            if (gainLabel != null)
            {
                string sign = data.gainOffset >= 0 ? "+" : "";
                gainLabel.text = $"GN {sign}{data.gainOffset:0}";
            }
        }

        private string GetModeDisplayName(RadarMode mode)
        {
            switch (mode)
            {
                case RadarMode.WX: return "WX";
                case RadarMode.WX_T: return "WX+T";
                case RadarMode.TURB: return "TURB";
                case RadarMode.MAP: return "MAP";
                case RadarMode.STBY: return "STBY";
                default: return "---";
            }
        }

        /// <summary>
        /// Set panel visibility
        /// </summary>
        public void SetVisibility(bool visible, bool animate = false)
        {
            isVisible = visible;

            if (canvasGroup != null)
            {
                if (animate && config != null && config.enableAnimations)
                {
                    StopAllCoroutines();
                    StartCoroutine(AnimateVisibility(visible));
                }
                else
                {
                    canvasGroup.alpha = visible ? 1f : 0f;
                    canvasGroup.interactable = visible;
                    canvasGroup.blocksRaycasts = visible;
                }
            }
        }

        private System.Collections.IEnumerator AnimateVisibility(bool visible)
        {
            float targetAlpha = visible ? 1f : 0f;
            float duration = config != null ? config.fadeAnimationDuration : 0.3f;
            float elapsed = 0f;
            float startAlpha = canvasGroup.alpha;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / duration);
                yield return null;
            }

            canvasGroup.alpha = targetAlpha;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
        }

        /// <summary>
        /// Set sweep cycle duration (affects sweep speed)
        /// </summary>
        public void SetSweepCycleDuration(float duration)
        {
            sweepCycleDuration = Mathf.Max(1f, duration);
            sweepSpeed = 360f / sweepCycleDuration;

            if (sweepRenderer != null)
            {
                sweepRenderer.SweepSpeed = sweepSpeed;
            }
        }

        /// <summary>
        /// Set configuration
        /// </summary>
        public void SetConfig(WeatherRadarConfig newConfig)
        {
            config = newConfig;
            InitializeRenderers();
        }

        /// <summary>
        /// Set data provider
        /// </summary>
        public void SetDataProvider(WeatherRadarDataProvider provider)
        {
            // Unsubscribe from old provider
            if (dataProvider != null)
            {
                dataProvider.OnRadarDataUpdated -= OnRadarDataUpdated;
                dataProvider.OnRangeChanged -= OnRangeChanged;
                dataProvider.OnModeChanged -= OnModeChanged;
            }

            dataProvider = provider;

            // Subscribe to new provider
            if (dataProvider != null)
            {
                dataProvider.OnRadarDataUpdated += OnRadarDataUpdated;
                dataProvider.OnRangeChanged += OnRangeChanged;
                dataProvider.OnModeChanged += OnModeChanged;
            }

            InitializeRenderers();
        }

        /// <summary>
        /// Set weather provider
        /// </summary>
        public void SetWeatherProvider(WeatherRadarProviderBase provider)
        {
            // Unsubscribe from old provider
            if (weatherProvider != null)
            {
                weatherProvider.OnRadarDataUpdated -= OnWeatherDataReceived;
            }

            weatherProvider = provider;

            // Subscribe to new provider
            if (weatherProvider != null)
            {
                weatherProvider.OnRadarDataUpdated += OnWeatherDataReceived;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Application.isPlaying && initialized)
            {
                SetVisibility(isVisible);
                SetSweepCycleDuration(sweepCycleDuration);
            }
        }
#endif
    }
}
