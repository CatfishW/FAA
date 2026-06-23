using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using WeatherRadar.Weather3D.UI;

namespace WeatherRadar.Weather3D
{
    /// <summary>
    /// UI Control panel for the 3D Weather Display System.
    /// Provides controls for view modes, layer visibility, and display options.
    /// </summary>
    public class Weather3DControlPanel : MonoBehaviour
    {
        [Header("Manager Reference")]
        [SerializeField] private Weather3DManager weather3DManager;
        
        [Header("View Mode Controls")]
        [SerializeField] private Button planViewButton;
        [SerializeField] private Button profileViewButton;
        [SerializeField] private Button perspective3DButton;
        
        [Header("Layer Toggles")]
        [SerializeField] private Toggle cloudsToggle;
        [SerializeField] private Toggle precipitationToggle;
        [SerializeField] private Toggle lightningToggle;
        [SerializeField] private Toggle turbulenceToggle;
        [SerializeField] private Toggle hazardPillarsToggle;
        
        [Header("Sliders")]
        [SerializeField] private Slider intensityThresholdSlider;
        [SerializeField] private Slider opacitySlider;
        
        [Header("Info Display")]
        [SerializeField] private TMP_Text statusLabel;
        [SerializeField] private TMP_Text cellCountLabel;
        [SerializeField] private TMP_Text dataAgeLabel;
        
        [Header("View Mode Indicator")]
        [SerializeField] private Image viewModeIndicator;
        [SerializeField] private Sprite planViewIcon;
        [SerializeField] private Sprite profileViewIcon;
        [SerializeField] private Sprite perspective3DIcon;
        
        [Header("Colors")]
        [SerializeField] private Color activeButtonColor = new Color(0.3f, 0.7f, 1f, 1f);
        [SerializeField] private Color inactiveButtonColor = new Color(0.5f, 0.5f, 0.5f, 1f);
        
        [Header("Panel Animation")]
        [SerializeField] private RectTransform panelRect;
        [SerializeField] private CanvasGroup panelCanvasGroup;
        [SerializeField] private Button toggleButton;
        [SerializeField] private float slideDistance = 300f;
        [SerializeField] private float animationDuration = 0.3f;

        // State
        private Weather3DViewMode currentViewMode = Weather3DViewMode.Perspective3D;
        private float intensityThreshold = 0f;
        private bool _isPanelVisible = true;
        private bool _isAnimating = false;
        private Vector2 _visiblePosition; // Saved position when visible
        private bool _hasInitializedPosition = false;

        // Events
        public event Action<Weather3DViewMode> OnViewModeChanged;
        public event Action<float> OnIntensityThresholdChanged;
        public event Action<bool> OnPanelVisibilityChanged;

        #region Unity Lifecycle

        private void Awake()
        {
            if (weather3DManager == null)
            {
                weather3DManager = FindObjectOfType<Weather3DManager>();
            }
        }

        private void Start()
        {
            SetupButtonListeners();
            SetupToggleListeners();
            SetupSliderListeners();
            
            // Subscribe to manager events
            if (weather3DManager != null)
            {
                weather3DManager.OnViewModeChanged += OnManagerViewModeChanged;
                weather3DManager.OnDataUpdated += OnDataUpdated;
            }
            
            // Initialize UI state
            UpdateViewModeUI(currentViewMode);
            SyncTogglesWithManager();
        }

        private void OnDestroy()
        {
            if (weather3DManager != null)
            {
                weather3DManager.OnViewModeChanged -= OnManagerViewModeChanged;
                weather3DManager.OnDataUpdated -= OnDataUpdated;
            }
        }

        private void Update()
        {
            UpdateDataAgeDisplay();
        }

        #endregion

        #region Setup

        private void SetupButtonListeners()
        {
            if (planViewButton != null)
            {
                planViewButton.onClick.AddListener(() => SetViewMode(Weather3DViewMode.PlanView));
            }
            
            if (profileViewButton != null)
            {
                profileViewButton.onClick.AddListener(() => SetViewMode(Weather3DViewMode.ProfileView));
            }
            
            if (perspective3DButton != null)
            {
                perspective3DButton.onClick.AddListener(() => SetViewMode(Weather3DViewMode.Perspective3D));
            }
        }

        private void SetupToggleListeners()
        {
            if (cloudsToggle != null)
            {
                cloudsToggle.onValueChanged.AddListener(OnCloudsToggleChanged);
            }
            
            if (precipitationToggle != null)
            {
                precipitationToggle.onValueChanged.AddListener(OnPrecipitationToggleChanged);
            }
            
            if (lightningToggle != null)
            {
                lightningToggle.onValueChanged.AddListener(OnLightningToggleChanged);
            }
            
            if (turbulenceToggle != null)
            {
                turbulenceToggle.onValueChanged.AddListener(OnTurbulenceToggleChanged);
            }
            
            if (hazardPillarsToggle != null)
            {
                hazardPillarsToggle.onValueChanged.AddListener(OnHazardPillarsToggleChanged);
            }
        }

        private void SetupSliderListeners()
        {
            if (intensityThresholdSlider != null)
            {
                intensityThresholdSlider.onValueChanged.AddListener(OnIntensitySliderChanged);
            }
            
            if (opacitySlider != null)
            {
                opacitySlider.onValueChanged.AddListener(OnOpacitySliderChanged);
            }
        }

        private void SyncTogglesWithManager()
        {
            if (weather3DManager == null) return;
            
            if (cloudsToggle != null)
                cloudsToggle.isOn = weather3DManager.ShowClouds;
            
            if (precipitationToggle != null)
                precipitationToggle.isOn = weather3DManager.ShowPrecipitation;
            
            if (lightningToggle != null)
                lightningToggle.isOn = weather3DManager.ShowLightning;
            
            if (turbulenceToggle != null)
                turbulenceToggle.isOn = weather3DManager.ShowTurbulence;
            
            if (hazardPillarsToggle != null)
                hazardPillarsToggle.isOn = weather3DManager.ShowHazardPillars;
        }

        #endregion

        #region View Mode

        public void SetViewMode(Weather3DViewMode mode)
        {
            if (currentViewMode == mode) return;
            
            currentViewMode = mode;
            
            if (weather3DManager != null)
            {
                weather3DManager.SetViewMode(mode);
            }
            
            UpdateViewModeUI(mode);
            OnViewModeChanged?.Invoke(mode);
        }

        private void OnManagerViewModeChanged(Weather3DViewMode mode)
        {
            currentViewMode = mode;
            UpdateViewModeUI(mode);
        }

        private void UpdateViewModeUI(Weather3DViewMode mode)
        {
            // Update button colors
            UpdateButtonColor(planViewButton, mode == Weather3DViewMode.PlanView);
            UpdateButtonColor(profileViewButton, mode == Weather3DViewMode.ProfileView);
            UpdateButtonColor(perspective3DButton, mode == Weather3DViewMode.Perspective3D);
            
            // Update icon
            if (viewModeIndicator != null)
            {
                switch (mode)
                {
                    case Weather3DViewMode.PlanView:
                        if (planViewIcon != null) viewModeIndicator.sprite = planViewIcon;
                        break;
                    case Weather3DViewMode.ProfileView:
                        if (profileViewIcon != null) viewModeIndicator.sprite = profileViewIcon;
                        break;
                    case Weather3DViewMode.Perspective3D:
                        if (perspective3DIcon != null) viewModeIndicator.sprite = perspective3DIcon;
                        break;
                }
            }
        }

        private void UpdateButtonColor(Button button, bool isActive)
        {
            if (button == null) return;
            
            var colors = button.colors;
            colors.normalColor = isActive ? activeButtonColor : inactiveButtonColor;
            button.colors = colors;
        }

        #endregion

        #region Toggle Handlers

        private void OnCloudsToggleChanged(bool isOn)
        {
            if (weather3DManager != null)
            {
                weather3DManager.ShowClouds = isOn;
            }
        }

        private void OnPrecipitationToggleChanged(bool isOn)
        {
            if (weather3DManager != null)
            {
                weather3DManager.ShowPrecipitation = isOn;
            }
        }

        private void OnLightningToggleChanged(bool isOn)
        {
            if (weather3DManager != null)
            {
                weather3DManager.ShowLightning = isOn;
            }
        }

        private void OnTurbulenceToggleChanged(bool isOn)
        {
            if (weather3DManager != null)
            {
                weather3DManager.ShowTurbulence = isOn;
            }
        }

        private void OnHazardPillarsToggleChanged(bool isOn)
        {
            if (weather3DManager != null)
            {
                weather3DManager.ShowHazardPillars = isOn;
            }
        }

        #endregion

        #region Slider Handlers

        private void OnIntensitySliderChanged(float value)
        {
            intensityThreshold = value;
            OnIntensityThresholdChanged?.Invoke(value);
            
            // Could filter display based on threshold
        }

        private void OnOpacitySliderChanged(float value)
        {
            // Apply opacity to all effects through config or manager
            if (weather3DManager != null && weather3DManager.Config != null)
            {
                // This would need a runtime config modification method
            }
        }

        #endregion

        #region Info Display

        private void OnDataUpdated(Weather3DData data)
        {
            if (data == null) return;
            
            // Update cell count
            if (cellCountLabel != null)
            {
                int thunderstormCount = 0;
                foreach (var cell in data.weatherCells)
                {
                    if (cell.cellType == WeatherCellType.Thunderstorm)
                        thunderstormCount++;
                }
                
                cellCountLabel.text = $"Cells: {data.weatherCells.Count} ({thunderstormCount} CB)";
            }
            
            // Update status
            if (statusLabel != null)
            {
                statusLabel.text = "ACTIVE";
                statusLabel.color = Color.green;
            }
        }

        private void UpdateDataAgeDisplay()
        {
            if (dataAgeLabel == null || weather3DManager == null) return;
            
            var data = weather3DManager.CurrentData;
            if (data != null)
            {
                int seconds = Mathf.FloorToInt(data.dataAge);
                dataAgeLabel.text = $"Age: {seconds}s";
                
                // Color code based on freshness
                if (seconds < 10)
                    dataAgeLabel.color = Color.green;
                else if (seconds < 30)
                    dataAgeLabel.color = Color.yellow;
                else
                    dataAgeLabel.color = Color.red;
            }
            else
            {
                dataAgeLabel.text = "Age: --";
                dataAgeLabel.color = Color.gray;
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Set the Weather3D manager reference
        /// </summary>
        public void SetManager(Weather3DManager manager)
        {
            if (weather3DManager != null)
            {
                weather3DManager.OnViewModeChanged -= OnManagerViewModeChanged;
                weather3DManager.OnDataUpdated -= OnDataUpdated;
            }
            
            weather3DManager = manager;
            
            if (weather3DManager != null)
            {
                weather3DManager.OnViewModeChanged += OnManagerViewModeChanged;
                weather3DManager.OnDataUpdated += OnDataUpdated;
                SyncTogglesWithManager();
            }
        }

        /// <summary>
        /// Toggle all layers on/off
        /// </summary>
        public void ToggleAllLayers(bool enabled)
        {
            if (weather3DManager == null) return;
            
            weather3DManager.ShowClouds = enabled;
            weather3DManager.ShowPrecipitation = enabled;
            weather3DManager.ShowLightning = enabled;
            weather3DManager.ShowTurbulence = enabled;
            weather3DManager.ShowHazardPillars = enabled;
            
            SyncTogglesWithManager();
        }

        /// <summary>
        /// Cycle to the next view mode
        /// </summary>
        public void CycleViewMode()
        {
            switch (currentViewMode)
            {
                case Weather3DViewMode.PlanView:
                    SetViewMode(Weather3DViewMode.ProfileView);
                    break;
                case Weather3DViewMode.ProfileView:
                    SetViewMode(Weather3DViewMode.Perspective3D);
                    break;
                case Weather3DViewMode.Perspective3D:
                    SetViewMode(Weather3DViewMode.PlanView);
                    break;
            }
        }
        
        /// <summary>
        /// Whether the panel is currently visible.
        /// </summary>
        public bool IsPanelVisible => _isPanelVisible;
        
        /// <summary>
        /// Show the control panel with slide animation.
        /// </summary>
        public void Show()
        {
            if (_isPanelVisible || _isAnimating) return;
            
            _isAnimating = true;
            _isPanelVisible = true;
            
            if (panelRect == null) panelRect = GetComponent<RectTransform>();
            if (panelCanvasGroup == null) panelCanvasGroup = GetComponent<CanvasGroup>();
            
            // Initialize saved position if first time
            if (!_hasInitializedPosition)
            {
                _visiblePosition = panelRect.anchoredPosition;
                _hasInitializedPosition = true;
            }
            
            // Enable interaction
            if (panelCanvasGroup != null)
            {
                panelCanvasGroup.interactable = true;
                panelCanvasGroup.blocksRaycasts = true;
            }
            
            // Animate slide in from right (from hidden position to saved visible position)
            Vector2 hiddenPos = _visiblePosition + new Vector2(slideDistance, 0);
            
            UITweenAnimator.MoveTo(panelRect, hiddenPos, _visiblePosition, animationDuration, 
                UITweenAnimator.EaseType.EaseOutQuad, () => _isAnimating = false);
            
            if (panelCanvasGroup != null)
            {
                UITweenAnimator.FadeTo(panelCanvasGroup, 0f, 1f, animationDuration);
            }
            
            OnPanelVisibilityChanged?.Invoke(true);
        }
        
        /// <summary>
        /// Hide the control panel with slide animation.
        /// </summary>
        public void Hide()
        {
            if (!_isPanelVisible || _isAnimating) return;
            
            _isAnimating = true;
            _isPanelVisible = false;
            
            if (panelRect == null) panelRect = GetComponent<RectTransform>();
            if (panelCanvasGroup == null) panelCanvasGroup = GetComponent<CanvasGroup>();
            
            // Save current position as visible position before hiding
            if (!_hasInitializedPosition)
            {
                _visiblePosition = panelRect.anchoredPosition;
                _hasInitializedPosition = true;
            }
            
            // Animate slide out to right
            Vector2 hiddenPos = _visiblePosition + new Vector2(slideDistance, 0);
            
            UITweenAnimator.Move(panelRect, hiddenPos, animationDuration,
                UITweenAnimator.EaseType.EaseOutQuad, () => 
                {
                    _isAnimating = false;
                    // Disable interaction when hidden
                    if (panelCanvasGroup != null)
                    {
                        panelCanvasGroup.interactable = false;
                        panelCanvasGroup.blocksRaycasts = false;
                    }
                });
            
            if (panelCanvasGroup != null)
            {
                UITweenAnimator.Fade(panelCanvasGroup, 0f, animationDuration);
            }
            
            OnPanelVisibilityChanged?.Invoke(false);
        }
        
        /// <summary>
        /// Toggle panel visibility.
        /// </summary>
        public void Toggle()
        {
            if (_isPanelVisible)
                Hide();
            else
                Show();
        }
        
        /// <summary>
        /// Set panel visibility instantly (no animation).
        /// </summary>
        public void SetVisible(bool visible)
        {
            _isPanelVisible = visible;
            
            if (panelCanvasGroup != null)
            {
                panelCanvasGroup.alpha = visible ? 1f : 0f;
                panelCanvasGroup.interactable = visible;
                panelCanvasGroup.blocksRaycasts = visible;
            }
        }

        #endregion
    }
}
