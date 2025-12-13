using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using AviationUI.Panels;

namespace AviationUI
{
    /// <summary>
    /// Main manager component that coordinates all Aviation UI panels.
    /// Handles data updates, visibility, and provides API for external systems.
    /// </summary>
    public class AviationUIManager : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private AviationUIConfig config;
        [SerializeField] private AviationFlightDataProvider dataProvider;

        [Header("Panel References")]
        [SerializeField] private HeadingTapePanel headingTape;
        [SerializeField] private AttitudePanel attitude;
        [SerializeField] private AirspeedTapePanel airspeedTape;
        [SerializeField] private AltitudeTapePanel altitudeTape;
        [SerializeField] private VerticalSpeedPanel verticalSpeed;
        [SerializeField] private EngineGaugesPanel engineGauges;
        [SerializeField] private CompassRosePanel compassRose;
        [SerializeField] private RadarPanel radar;
        [SerializeField] private ModeButtonsPanel modeButtons;

        [Header("Canvas Reference")]
        [SerializeField] private Canvas uiCanvas;

        private Dictionary<string, AviationUIPanel> panels;
        private bool isInitialized = false;

        /// <summary>
        /// Get the current configuration
        /// </summary>
        public AviationUIConfig Config => config;

        /// <summary>
        /// Get the flight data provider
        /// </summary>
        public AviationFlightDataProvider DataProvider => dataProvider;

        private void Awake()
        {
            InitializePanelDictionary();
        }

        private void Start()
        {
            Initialize();
        }

        private void InitializePanelDictionary()
        {
            panels = new Dictionary<string, AviationUIPanel>();

            if (headingTape != null) panels[headingTape.PanelId] = headingTape;
            if (attitude != null) panels[attitude.PanelId] = attitude;
            if (airspeedTape != null) panels[airspeedTape.PanelId] = airspeedTape;
            if (altitudeTape != null) panels[altitudeTape.PanelId] = altitudeTape;
            if (verticalSpeed != null) panels[verticalSpeed.PanelId] = verticalSpeed;
            if (engineGauges != null) panels[engineGauges.PanelId] = engineGauges;
            if (compassRose != null) panels[compassRose.PanelId] = compassRose;
            if (radar != null) panels[radar.PanelId] = radar;
            if (modeButtons != null) panels[modeButtons.PanelId] = modeButtons;
        }

        /// <summary>
        /// Initialize the UI manager and all panels
        /// </summary>
        public void Initialize()
        {
            if (isInitialized) return;

            // Ensure we have a data provider
            if (dataProvider == null)
            {
                dataProvider = GetComponent<AviationFlightDataProvider>();
                if (dataProvider == null)
                {
                    dataProvider = gameObject.AddComponent<AviationFlightDataProvider>();
                }
            }

            // Configure all panels
            foreach (var panel in panels.Values)
            {
                if (panel != null)
                {
                    panel.SetConfig(config);
                    panel.SetDataProvider(dataProvider);
                }
            }

            // Apply visibility from config
            ApplyVisibilityFromConfig();

            // Setup mode button events
            SetupModeButtonEvents();

            isInitialized = true;
        }

        private void ApplyVisibilityFromConfig()
        {
            if (config == null) return;

            SetPanelVisibility("HeadingTape", config.showHeadingTape);
            SetPanelVisibility("Attitude", config.showAttitudeIndicator);
            SetPanelVisibility("AirspeedTape", config.showAirspeedTape);
            SetPanelVisibility("AltitudeTape", config.showAltitudeTape);
            SetPanelVisibility("VerticalSpeed", config.showVerticalSpeed);
            SetPanelVisibility("EngineGauges", config.showEngineGauges);
            SetPanelVisibility("CompassRose", config.showCompassRose);
            SetPanelVisibility("Radar", config.showRadar);
            SetPanelVisibility("ModeButtons", config.showModeButtons);
        }

        private void SetupModeButtonEvents()
        {
            if (modeButtons == null) return;

            modeButtons.OnNightModeChanged += OnNightModeChanged;
            modeButtons.OnWeatherModeChanged += OnWeatherModeChanged;
            modeButtons.OnTrafficFilterChanged += OnTrafficFilterChanged;
        }

        private void OnNightModeChanged(bool enabled)
        {
            // Could adjust colors for night mode here
            Debug.Log($"Night mode: {enabled}");
        }

        private void OnWeatherModeChanged(bool enabled)
        {
            // Toggle weather display on radar
            if (radar != null)
            {
                // radar.SetWeatherDisplay(enabled);
            }
            Debug.Log($"Weather mode: {enabled}");
        }

        private void OnTrafficFilterChanged(bool enabled)
        {
            Debug.Log($"Traffic filter: {enabled}");
        }

        /// <summary>
        /// Set visibility of a specific panel
        /// </summary>
        public void SetPanelVisibility(string panelId, bool visible)
        {
            if (panels.TryGetValue(panelId, out AviationUIPanel panel))
            {
                panel.SetVisibility(visible);
            }
        }

        /// <summary>
        /// Toggle visibility of a specific panel
        /// </summary>
        public void TogglePanelVisibility(string panelId)
        {
            if (panels.TryGetValue(panelId, out AviationUIPanel panel))
            {
                panel.SetVisibility(!panel.IsVisible);
            }
        }

        /// <summary>
        /// Get a panel by ID
        /// </summary>
        public AviationUIPanel GetPanel(string panelId)
        {
            panels.TryGetValue(panelId, out AviationUIPanel panel);
            return panel;
        }

        /// <summary>
        /// Get a typed panel
        /// </summary>
        public T GetPanel<T>() where T : AviationUIPanel
        {
            foreach (var panel in panels.Values)
            {
                if (panel is T typedPanel)
                    return typedPanel;
            }
            return null;
        }

        /// <summary>
        /// Update flight data from external source
        /// </summary>
        public void UpdateFlightData(AviationFlightData data)
        {
            if (dataProvider != null)
            {
                dataProvider.UpdateFlightData(data);
            }
        }

        /// <summary>
        /// Set individual flight values
        /// </summary>
        public void SetHeading(float heading)
        {
            dataProvider?.SetHeading(heading);
        }

        public void SetPitch(float pitch)
        {
            dataProvider?.SetPitch(pitch);
        }

        public void SetRoll(float roll)
        {
            dataProvider?.SetRoll(roll);
        }

        public void SetAirspeed(float airspeed)
        {
            dataProvider?.SetAirspeed(airspeed);
        }

        public void SetAltitude(float altitude)
        {
            dataProvider?.SetAltitude(altitude);
        }

        public void SetVerticalSpeed(float vs)
        {
            dataProvider?.SetVerticalSpeed(vs);
        }

        public void SetAltitudeAGL(float agl)
        {
            dataProvider?.SetAltitudeAGL(agl);
        }

        public void SetEngineTorque(int engine, float torque)
        {
            if (engine == 1)
                dataProvider?.SetEngine1Torque(torque);
            else if (engine == 2)
                dataProvider?.SetEngine2Torque(torque);
        }

        public void SetEngineNR(int engine, float nr)
        {
            if (engine == 1)
                dataProvider?.SetEngine1NR(nr);
            else if (engine == 2)
                dataProvider?.SetEngine2NR(nr);
        }

        /// <summary>
        /// Apply a new configuration to all panels
        /// </summary>
        public void ApplyConfig(AviationUIConfig newConfig)
        {
            config = newConfig;
            
            foreach (var panel in panels.Values)
            {
                if (panel != null)
                {
                    panel.SetConfig(config);
                }
            }

            ApplyVisibilityFromConfig();
        }

        /// <summary>
        /// Show all panels
        /// </summary>
        public void ShowAllPanels()
        {
            foreach (var panel in panels.Values)
            {
                panel?.SetVisibility(true);
            }
        }

        /// <summary>
        /// Hide all panels
        /// </summary>
        public void HideAllPanels()
        {
            foreach (var panel in panels.Values)
            {
                panel?.SetVisibility(false);
            }
        }

        /// <summary>
        /// Register a panel with the manager
        /// </summary>
        public void RegisterPanel(AviationUIPanel panel)
        {
            if (panel != null && !panels.ContainsKey(panel.PanelId))
            {
                panels[panel.PanelId] = panel;
                panel.SetConfig(config);
                panel.SetDataProvider(dataProvider);
            }
        }

        /// <summary>
        /// Unregister a panel from the manager
        /// </summary>
        public void UnregisterPanel(string panelId)
        {
            panels.Remove(panelId);
        }

#if UNITY_EDITOR
        /// <summary>
        /// Editor helper to find all panels in hierarchy
        /// </summary>
        [ContextMenu("Find All Panels")]
        private void FindAllPanels()
        {
            headingTape = GetComponentInChildren<HeadingTapePanel>(true);
            attitude = GetComponentInChildren<AttitudePanel>(true);
            airspeedTape = GetComponentInChildren<AirspeedTapePanel>(true);
            altitudeTape = GetComponentInChildren<AltitudeTapePanel>(true);
            verticalSpeed = GetComponentInChildren<VerticalSpeedPanel>(true);
            engineGauges = GetComponentInChildren<EngineGaugesPanel>(true);
            compassRose = GetComponentInChildren<CompassRosePanel>(true);
            radar = GetComponentInChildren<RadarPanel>(true);
            modeButtons = GetComponentInChildren<ModeButtonsPanel>(true);

            InitializePanelDictionary();
            
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif

        private void OnDestroy()
        {
            if (modeButtons != null)
            {
                modeButtons.OnNightModeChanged -= OnNightModeChanged;
                modeButtons.OnWeatherModeChanged -= OnWeatherModeChanged;
                modeButtons.OnTrafficFilterChanged -= OnTrafficFilterChanged;
            }
        }
    }
}
