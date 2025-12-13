using UnityEngine;
using UnityEngine.UI;
using System;

namespace AviationUI.Panels
{
    /// <summary>
    /// Side panel with mode selection buttons for different display modes.
    /// Includes night mode, weather mode, aircraft mode toggles.
    /// </summary>
    public class ModeButtonsPanel : AviationUIPanel
    {
        [Header("Mode Button References")]
        [SerializeField] private Button nightModeButton;
        [SerializeField] private Button weatherModeButton;
        [SerializeField] private Button aircraftModeButton;
        [SerializeField] private Button trafficFilterButton;

        [Header("Button Icons")]
        [SerializeField] private Image nightModeIcon;
        [SerializeField] private Image weatherModeIcon;
        [SerializeField] private Image aircraftModeIcon;
        [SerializeField] private Image trafficFilterIcon;

        [Header("Mode States")]
        [SerializeField] private bool nightModeEnabled = false;
        [SerializeField] private bool weatherModeEnabled = true;
        [SerializeField] private bool aircraftModeEnabled = true;
        [SerializeField] private bool trafficFilterEnabled = true;

        // Events
        public event Action<bool> OnNightModeChanged;
        public event Action<bool> OnWeatherModeChanged;
        public event Action<bool> OnAircraftModeChanged;
        public event Action<bool> OnTrafficFilterChanged;

        public override string PanelId => "ModeButtons";
        public override Vector2 DefaultAnchorPosition => new Vector2(0.03f, 0.6f);

        protected override void InitializePanel()
        {
            // Setup button listeners
            if (nightModeButton != null)
                nightModeButton.onClick.AddListener(ToggleNightMode);

            if (weatherModeButton != null)
                weatherModeButton.onClick.AddListener(ToggleWeatherMode);

            if (aircraftModeButton != null)
                aircraftModeButton.onClick.AddListener(ToggleAircraftMode);

            if (trafficFilterButton != null)
                trafficFilterButton.onClick.AddListener(ToggleTrafficFilter);

            UpdateButtonVisuals();
            ApplyColors();
        }

        protected override void UpdateDisplay()
        {
            // Mode buttons don't need flight data updates
        }

        private void ToggleNightMode()
        {
            nightModeEnabled = !nightModeEnabled;
            UpdateButtonVisuals();
            OnNightModeChanged?.Invoke(nightModeEnabled);
        }

        private void ToggleWeatherMode()
        {
            weatherModeEnabled = !weatherModeEnabled;
            UpdateButtonVisuals();
            OnWeatherModeChanged?.Invoke(weatherModeEnabled);
        }

        private void ToggleAircraftMode()
        {
            aircraftModeEnabled = !aircraftModeEnabled;
            UpdateButtonVisuals();
            OnAircraftModeChanged?.Invoke(aircraftModeEnabled);
        }

        private void ToggleTrafficFilter()
        {
            trafficFilterEnabled = !trafficFilterEnabled;
            UpdateButtonVisuals();
            OnTrafficFilterChanged?.Invoke(trafficFilterEnabled);
        }

        private void UpdateButtonVisuals()
        {
            Color activeColor = config != null ? config.primaryColor : Color.green;
            Color inactiveColor = config != null ? new Color(config.primaryColor.r * 0.3f, config.primaryColor.g * 0.3f, config.primaryColor.b * 0.3f, 0.5f) : Color.gray;

            if (nightModeIcon != null)
                nightModeIcon.color = nightModeEnabled ? activeColor : inactiveColor;

            if (weatherModeIcon != null)
                weatherModeIcon.color = weatherModeEnabled ? activeColor : inactiveColor;

            if (aircraftModeIcon != null)
                aircraftModeIcon.color = aircraftModeEnabled ? activeColor : inactiveColor;

            if (trafficFilterIcon != null)
                trafficFilterIcon.color = trafficFilterEnabled ? activeColor : inactiveColor;
        }

        protected override void ApplyColors()
        {
            UpdateButtonVisuals();
        }

        /// <summary>
        /// Get current mode states
        /// </summary>
        public bool IsNightMode => nightModeEnabled;
        public bool IsWeatherMode => weatherModeEnabled;
        public bool IsAircraftMode => aircraftModeEnabled;
        public bool IsTrafficFilterEnabled => trafficFilterEnabled;

        /// <summary>
        /// Set mode states programmatically
        /// </summary>
        public void SetNightMode(bool enabled)
        {
            nightModeEnabled = enabled;
            UpdateButtonVisuals();
        }

        public void SetWeatherMode(bool enabled)
        {
            weatherModeEnabled = enabled;
            UpdateButtonVisuals();
        }

        public void SetAircraftMode(bool enabled)
        {
            aircraftModeEnabled = enabled;
            UpdateButtonVisuals();
        }

        public void SetTrafficFilter(bool enabled)
        {
            trafficFilterEnabled = enabled;
            UpdateButtonVisuals();
        }

        /// <summary>
        /// Create the mode buttons panel structure
        /// </summary>
        public static ModeButtonsPanel CreatePanel(Transform parent, AviationUIConfig config)
        {
            // Create panel root
            GameObject panelObj = new GameObject("ModeButtonsPanel");
            panelObj.transform.SetParent(parent, false);
            
            RectTransform panelRect = panelObj.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0, 0.5f);
            panelRect.anchorMax = new Vector2(0, 0.5f);
            panelRect.pivot = new Vector2(0, 0.5f);
            panelRect.anchoredPosition = new Vector2(10, 50);
            panelRect.sizeDelta = new Vector2(50, 200);

            panelObj.AddComponent<CanvasGroup>();

            // Create buttons
            float buttonSize = 40f;
            float spacing = 10f;
            float startY = 70f;

            var nightBtn = CreateModeButton(panelObj.transform, "NightMode", 0, startY, buttonSize, "☾", config);
            var weatherBtn = CreateModeButton(panelObj.transform, "WeatherMode", 0, startY - (buttonSize + spacing), buttonSize, "☁", config);
            var aircraftBtn = CreateModeButton(panelObj.transform, "AircraftMode", 0, startY - (buttonSize + spacing) * 2, buttonSize, "✈", config);
            var trafficBtn = CreateModeButton(panelObj.transform, "TrafficFilter", 0, startY - (buttonSize + spacing) * 3, buttonSize, "◎", config);

            // Add panel component
            ModeButtonsPanel panel = panelObj.AddComponent<ModeButtonsPanel>();
            panel.nightModeButton = nightBtn.button;
            panel.nightModeIcon = nightBtn.icon;
            panel.weatherModeButton = weatherBtn.button;
            panel.weatherModeIcon = weatherBtn.icon;
            panel.aircraftModeButton = aircraftBtn.button;
            panel.aircraftModeIcon = aircraftBtn.icon;
            panel.trafficFilterButton = trafficBtn.button;
            panel.trafficFilterIcon = trafficBtn.icon;
            panel.config = config;

            return panel;
        }

        private static (Button button, Image icon) CreateModeButton(Transform parent, string name, float x, float y, float size, string iconText, AviationUIConfig config)
        {
            // Create button object
            GameObject btnObj = new GameObject(name + "Button");
            btnObj.transform.SetParent(parent, false);
            
            RectTransform btnRect = btnObj.AddComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0.5f, 0.5f);
            btnRect.anchorMax = new Vector2(0.5f, 0.5f);
            btnRect.anchoredPosition = new Vector2(x, y);
            btnRect.sizeDelta = new Vector2(size, size);
            
            Image btnBg = btnObj.AddComponent<Image>();
            btnBg.color = new Color(0, 0, 0, 0.5f);
            
            Button button = btnObj.AddComponent<Button>();
            button.targetGraphic = btnBg;

            // Create icon
            GameObject iconObj = new GameObject("Icon");
            iconObj.transform.SetParent(btnObj.transform, false);
            
            RectTransform iconRect = iconObj.AddComponent<RectTransform>();
            iconRect.anchorMin = Vector2.zero;
            iconRect.anchorMax = Vector2.one;
            iconRect.sizeDelta = new Vector2(-8, -8);
            
            // Use text as icon (could be replaced with sprites)
            Text iconTextComp = iconObj.AddComponent<Text>();
            iconTextComp.text = iconText;
            iconTextComp.alignment = TextAnchor.MiddleCenter;
            iconTextComp.fontSize = 24;
            iconTextComp.color = config != null ? config.primaryColor : Color.green;
            iconTextComp.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // For the image reference, we need to add an Image component for color control
            Image iconImage = iconObj.AddComponent<Image>();
            iconImage.color = new Color(0, 0, 0, 0); // Invisible, just for color reference
            
            return (button, iconImage);
        }
    }
}
