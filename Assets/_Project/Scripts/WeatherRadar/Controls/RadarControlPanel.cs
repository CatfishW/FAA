using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

namespace WeatherRadar
{
    /// <summary>
    /// Control panel for weather radar.
    /// Provides controls for range, tilt, gain, and mode selection.
    /// Uses standard Unity Button components for reliability.
    /// </summary>
    public class RadarControlPanel : MonoBehaviour
    {
        [Header("Data Provider")]
        [SerializeField] private WeatherRadarDataProvider dataProvider;

        [Header("Range Controls")]
        [SerializeField] private Button rangeUpButton;
        [SerializeField] private Button rangeDownButton;
        [SerializeField] private TMP_Text rangeValueText;

        [Header("Tilt Controls")]
        [SerializeField] private Button tiltUpButton;
        [SerializeField] private Button tiltDownButton;
        [SerializeField] private TMP_Text tiltValueText;

        [Header("Gain Controls")]
        [SerializeField] private Button gainUpButton;
        [SerializeField] private Button gainDownButton;
        [SerializeField] private TMP_Text gainValueText;

        [Header("Mode Buttons")]
        [SerializeField] private Button wxModeButton;
        [SerializeField] private Button wxTModeButton;
        [SerializeField] private Button turbModeButton;
        [SerializeField] private Button mapModeButton;
        [SerializeField] private Button stbyModeButton;

        [Header("Active Mode Indicator")]
        [SerializeField] private Color normalButtonColor = new Color(0.12f, 0.22f, 0.12f, 1f);
        [SerializeField] private Color activeButtonColor = new Color(0.2f, 0.5f, 0.25f, 1f);

        [Header("Visibility")]
        [SerializeField] private bool startVisible = false;

        private float displayedRange;
        private float displayedTilt;
        private float displayedGain;
        private RadarMode currentMode;
        private Dictionary<RadarMode, Button> modeButtons = new Dictionary<RadarMode, Button>();
        private CanvasGroup _canvasGroup;
        private bool _isVisible = false;

        /// <summary>
        /// Get or set panel visibility
        /// </summary>
        public bool IsVisible
        {
            get => _isVisible;
            set => SetVisibility(value);
        }

        /// <summary>
        /// Set panel visibility
        /// </summary>
        public void SetVisibility(bool visible)
        {
            _isVisible = visible;
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = visible ? 1f : 0f;
                _canvasGroup.interactable = visible;
                _canvasGroup.blocksRaycasts = visible;
            }
        }

        /// <summary>
        /// Toggle panel visibility
        /// </summary>
        public void ToggleVisibility()
        {
            SetVisibility(!_isVisible);
        }

        private void Awake()
        {
            // Setup canvas group for visibility animations
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
            {
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            _isVisible = startVisible;
            AutoFindComponents();
        }

        private void Start()
        {
            // Auto-find data provider if not assigned
            if (dataProvider == null)
            {
                dataProvider = GetComponentInParent<WeatherRadarDataProvider>();
            }

            if (dataProvider == null)
            {
                Debug.LogError("[RadarControlPanel] Data Provider NOT FOUND!");
            }
            else
            {
                Debug.Log($"[RadarControlPanel] Data Provider connected: {dataProvider.name}");
            }

            SetupButtonListeners();
            SetupModeButtons();

            if (dataProvider != null)
            {
                dataProvider.OnRadarDataUpdated += OnRadarDataUpdated;
                dataProvider.OnRangeChanged += OnRangeChanged;
                dataProvider.OnModeChanged += OnModeChanged;

                displayedRange = dataProvider.RadarData.currentRange;
                displayedTilt = dataProvider.RadarData.tiltAngle;
                displayedGain = dataProvider.RadarData.gainOffset;
                currentMode = dataProvider.RadarData.currentMode;

                UpdateAllDisplays();
            }
        }



        private void OnDestroy()
        {
            if (dataProvider != null)
            {
                dataProvider.OnRadarDataUpdated -= OnRadarDataUpdated;
                dataProvider.OnRangeChanged -= OnRangeChanged;
                dataProvider.OnModeChanged -= OnModeChanged;
            }
        }

        private void AutoFindComponents()
        {
            // Find buttons by name
            foreach (var btn in GetComponentsInChildren<Button>(true))
            {
                string name = btn.name.ToLower();

                if (name.Contains("rangeup"))
                    rangeUpButton = btn;
                else if (name.Contains("rangedown"))
                    rangeDownButton = btn;
                else if (name.Contains("tiltup"))
                    tiltUpButton = btn;
                else if (name.Contains("tiltdown"))
                    tiltDownButton = btn;
                else if (name.Contains("gainup"))
                    gainUpButton = btn;
                else if (name.Contains("gaindown"))
                    gainDownButton = btn;
                else if (name.Contains("wxt") || name.Contains("wx+t"))
                    wxTModeButton = btn;
                else if (name.Contains("wx") && !name.Contains("wxt"))
                    wxModeButton = btn;
                else if (name.Contains("turb"))
                    turbModeButton = btn;
                else if (name.Contains("map"))
                    mapModeButton = btn;
                else if (name.Contains("stby"))
                    stbyModeButton = btn;
            }

            // Find text labels by name
            foreach (var txt in GetComponentsInChildren<TMP_Text>(true))
            {
                string name = txt.name.ToLower();

                if (name.Contains("rangevalue"))
                    rangeValueText = txt;
                else if (name.Contains("tiltvalue"))
                    tiltValueText = txt;
                else if (name.Contains("gainvalue"))
                    gainValueText = txt;
            }
        }

        private void SetupButtonListeners()
        {
            // Range buttons
            if (rangeUpButton != null)
            {
                rangeUpButton.onClick.AddListener(IncreaseRange);
            }
            if (rangeDownButton != null)
            {
                rangeDownButton.onClick.AddListener(DecreaseRange);
            }

            // Tilt buttons
            if (tiltUpButton != null)
            {
                tiltUpButton.onClick.AddListener(IncreaseTilt);
            }
            if (tiltDownButton != null)
            {
                tiltDownButton.onClick.AddListener(DecreaseTilt);
            }

            // Gain buttons
            if (gainUpButton != null)
            {
                gainUpButton.onClick.AddListener(IncreaseGain);
            }
            if (gainDownButton != null)
            {
                gainDownButton.onClick.AddListener(DecreaseGain);
            }
        }

        private void SetupModeButtons()
        {
            modeButtons.Clear();

            if (wxModeButton != null)
            {
                modeButtons[RadarMode.WX] = wxModeButton;
                wxModeButton.onClick.AddListener(() => SetMode(RadarMode.WX));
            }
            if (wxTModeButton != null)
            {
                modeButtons[RadarMode.WX_T] = wxTModeButton;
                wxTModeButton.onClick.AddListener(() => SetMode(RadarMode.WX_T));
            }
            if (turbModeButton != null)
            {
                modeButtons[RadarMode.TURB] = turbModeButton;
                turbModeButton.onClick.AddListener(() => SetMode(RadarMode.TURB));
            }
            if (mapModeButton != null)
            {
                modeButtons[RadarMode.MAP] = mapModeButton;
                mapModeButton.onClick.AddListener(() => SetMode(RadarMode.MAP));
            }
            if (stbyModeButton != null)
            {
                modeButtons[RadarMode.STBY] = stbyModeButton;
                stbyModeButton.onClick.AddListener(() => SetMode(RadarMode.STBY));
            }

            UpdateModeButtonVisuals();
        }

        #region Range Controls

        public void IncreaseRange()
        {
            Debug.Log("[RadarControlPanel] IncreaseRange Clicked");
            if (dataProvider != null)
            {
                dataProvider.IncreaseRange();
            }
        }

        public void DecreaseRange()
        {
            Debug.Log("[RadarControlPanel] DecreaseRange Clicked");
            if (dataProvider != null)
            {
                dataProvider.DecreaseRange();
            }
        }

        private void OnRangeChanged(float newRange)
        {
            displayedRange = newRange;
            UpdateRangeDisplay();
        }

        private void UpdateRangeDisplay()
        {
            if (rangeValueText != null)
            {
                rangeValueText.text = $"{displayedRange:0}nm";
            }
        }

        #endregion

        #region Tilt Controls

        public void IncreaseTilt()
        {
            Debug.Log("[RadarControlPanel] IncreaseTilt Clicked");
            if (dataProvider != null)
            {
                float newTilt = Mathf.Min(dataProvider.RadarData.tiltAngle + 0.5f, 15f);
                dataProvider.SetTilt(newTilt);
                displayedTilt = newTilt;
                UpdateTiltDisplay();
            }
        }

        public void DecreaseTilt()
        {
            Debug.Log("[RadarControlPanel] DecreaseTilt Clicked");
            if (dataProvider != null)
            {
                float newTilt = Mathf.Max(dataProvider.RadarData.tiltAngle - 0.5f, -15f);
                dataProvider.SetTilt(newTilt);
                displayedTilt = newTilt;
                UpdateTiltDisplay();
            }
        }

        /// <summary>
        /// Set tilt to a specific angle in degrees
        /// </summary>
        /// <param name="degrees">Tilt angle in degrees (-15 to +15)</param>
        public void SetTilt(float degrees)
        {
            Debug.Log($"[RadarControlPanel] SetTilt to {degrees}°");
            if (dataProvider != null)
            {
                float newTilt = Mathf.Clamp(degrees, -15f, 15f);
                dataProvider.SetTilt(newTilt);
                displayedTilt = newTilt;
                UpdateTiltDisplay();
            }
        }

        private void UpdateTiltDisplay()
        {
            if (tiltValueText != null)
            {
                string sign = displayedTilt >= 0 ? "+" : "";
                tiltValueText.text = $"{sign}{displayedTilt:0.0}°";
            }
        }

        #endregion

        #region Gain Controls

        public void IncreaseGain()
        {
            if (dataProvider != null)
            {
                float newGain = Mathf.Min(dataProvider.RadarData.gainOffset + 1f, 8f);
                dataProvider.SetGain(newGain);
                displayedGain = newGain;
                UpdateGainDisplay();
            }
        }

        public void DecreaseGain()
        {
            if (dataProvider != null)
            {
                float newGain = Mathf.Max(dataProvider.RadarData.gainOffset - 1f, -8f);
                dataProvider.SetGain(newGain);
                displayedGain = newGain;
                UpdateGainDisplay();
            }
        }

        /// <summary>
        /// Set gain to a specific dB value
        /// </summary>
        /// <param name="dB">Gain offset in dB (-8 to +8)</param>
        public void SetGain(float dB)
        {
            Debug.Log($"[RadarControlPanel] SetGain to {dB} dB");
            if (dataProvider != null)
            {
                float newGain = Mathf.Clamp(dB, -8f, 8f);
                dataProvider.SetGain(newGain);
                displayedGain = newGain;
                UpdateGainDisplay();
            }
        }

        private void UpdateGainDisplay()
        {
            if (gainValueText != null)
            {
                string sign = displayedGain >= 0 ? "+" : "";
                gainValueText.text = $"{sign}{displayedGain:0}dB";
            }
        }

        #endregion

        #region Mode Controls

        public void SetMode(RadarMode mode)
        {
            if (dataProvider != null && mode != currentMode)
            {
                Debug.Log($"[RadarControlPanel] Setting mode to: {mode}");
                currentMode = mode;
                dataProvider.SetMode(mode);
                UpdateModeButtonVisuals();
            }
        }

        private void OnModeChanged(RadarMode newMode)
        {
            currentMode = newMode;
            UpdateModeButtonVisuals();
        }

        private void UpdateModeButtonVisuals()
        {
            foreach (var kvp in modeButtons)
            {
                if (kvp.Value != null)
                {
                    Image img = kvp.Value.GetComponent<Image>();
                    if (img != null)
                    {
                        img.color = (kvp.Key == currentMode) ? activeButtonColor : normalButtonColor;
                    }

                    // Also update text brightness
                    TMP_Text txt = kvp.Value.GetComponentInChildren<TMP_Text>();
                    if (txt != null)
                    {
                        txt.color = (kvp.Key == currentMode) 
                            ? new Color(0.9f, 1f, 0.9f, 1f) 
                            : new Color(0.7f, 0.9f, 0.7f, 1f);
                    }
                }
            }
        }

        #endregion

        private void OnRadarDataUpdated(WeatherRadarData data)
        {
            // Sync display if needed
        }

        private void UpdateAllDisplays()
        {
            UpdateRangeDisplay();
            UpdateTiltDisplay();
            UpdateGainDisplay();
            UpdateModeButtonVisuals();
        }

        /// <summary>
        /// Set data provider reference
        /// </summary>
        public void SetDataProvider(WeatherRadarDataProvider provider)
        {
            if (dataProvider != null)
            {
                dataProvider.OnRadarDataUpdated -= OnRadarDataUpdated;
                dataProvider.OnRangeChanged -= OnRangeChanged;
                dataProvider.OnModeChanged -= OnModeChanged;
            }

            dataProvider = provider;

            if (dataProvider != null)
            {
                dataProvider.OnRadarDataUpdated += OnRadarDataUpdated;
                dataProvider.OnRangeChanged += OnRangeChanged;
                dataProvider.OnModeChanged += OnModeChanged;

                displayedRange = dataProvider.RadarData.currentRange;
                displayedTilt = dataProvider.RadarData.tiltAngle;
                displayedGain = dataProvider.RadarData.gainOffset;
                currentMode = dataProvider.RadarData.currentMode;

                UpdateAllDisplays();
            }
        }
    }
}
