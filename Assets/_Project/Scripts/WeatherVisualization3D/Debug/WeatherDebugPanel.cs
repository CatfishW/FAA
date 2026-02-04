using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace WeatherVisualization3D
{
    /// <summary>
    /// Runtime debug panel for weather visualization testing.
    /// Provides real-time controls and statistics display.
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public class WeatherDebugPanel : MonoBehaviour
    {
        #region References
        
        private WeatherSimulator simulator;
        private VolumetricWeatherManager manager;
        private VolumetricCloudVolume cloudVolume;
        
        #endregion

        #region UI Elements
        
        private GameObject panelRoot;
        private Text statusText;
        private Text statsText;
        private Slider timeScaleSlider;
        private Text timeScaleLabel;
        private Slider qualitySlider;
        private Text qualityLabel;
        private Toggle cloudsToggle;
        private Toggle pillarsToggle;
        private Toggle lightningToggle;
        private Toggle precipToggle;
        
        // Style settings
        private Color panelColor = new Color(0.1f, 0.1f, 0.15f, 0.9f);
        private Color headerColor = new Color(0.2f, 0.6f, 0.9f);
        private Color textColor = Color.white;
        private Font defaultFont;
        
        #endregion

        #region State
        
        private bool panelVisible = true;
        private float updateInterval = 0.1f;
        private float lastUpdateTime;
        
        #endregion

        private void Awake()
        {
            // Try to find default font
            defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (defaultFont == null)
            {
                defaultFont = Font.CreateDynamicFontFromOSFont("Arial", 14);
            }
        }

        private void Start()
        {
            FindComponents();
            CreateUI();
            UpdateUI();
        }

        private void Update()
        {
            // Toggle panel with F1
            if (Input.GetKeyDown(KeyCode.F1))
            {
                panelVisible = !panelVisible;
                if (panelRoot != null)
                    panelRoot.SetActive(panelVisible);
            }
            
            // Update stats periodically
            if (Time.time - lastUpdateTime >= updateInterval)
            {
                UpdateUI();
                lastUpdateTime = Time.time;
            }
            
            // Quick controls
            HandleKeyboardShortcuts();
        }

        private void FindComponents()
        {
            simulator = FindObjectOfType<WeatherSimulator>();
            manager = FindObjectOfType<VolumetricWeatherManager>();
            cloudVolume = FindObjectOfType<VolumetricCloudVolume>();
        }

        private void HandleKeyboardShortcuts()
        {
            if (simulator == null) return;
            
            // Space to pause/resume
            if (Input.GetKeyDown(KeyCode.P))
            {
                simulator.IsPaused = !simulator.IsPaused;
            }
            
            // R to reset
            if (Input.GetKeyDown(KeyCode.R) && Input.GetKey(KeyCode.LeftControl))
            {
                simulator.ResetSimulation();
            }
            
            // Number keys for scenarios
            if (Input.GetKeyDown(KeyCode.Alpha1))
                simulator.SetScenarioByType(ScenarioType.ScatteredShowers);
            if (Input.GetKeyDown(KeyCode.Alpha2))
                simulator.SetScenarioByType(ScenarioType.ThunderstormCells);
            if (Input.GetKeyDown(KeyCode.Alpha3))
                simulator.SetScenarioByType(ScenarioType.SquallLine);
            if (Input.GetKeyDown(KeyCode.Alpha4))
                simulator.SetScenarioByType(ScenarioType.Supercell);
            
            // +/- for time scale
            if (Input.GetKey(KeyCode.Equals) || Input.GetKey(KeyCode.KeypadPlus))
            {
                simulator.TimeScale = Mathf.Min(100f, simulator.TimeScale * 1.02f);
                if (timeScaleSlider != null) timeScaleSlider.value = simulator.TimeScale;
            }
            if (Input.GetKey(KeyCode.Minus) || Input.GetKey(KeyCode.KeypadMinus))
            {
                simulator.TimeScale = Mathf.Max(0.1f, simulator.TimeScale * 0.98f);
                if (timeScaleSlider != null) timeScaleSlider.value = simulator.TimeScale;
            }
        }

        private void CreateUI()
        {
            // Create main panel
            panelRoot = CreatePanel("DebugPanel", transform, new Vector2(280, 420));
            var panelRect = panelRoot.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0, 1);
            panelRect.anchorMax = new Vector2(0, 1);
            panelRect.pivot = new Vector2(0, 1);
            panelRect.anchoredPosition = new Vector2(10, -10);
            
            // Vertical layout
            var layout = panelRoot.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 10, 10);
            layout.spacing = 5;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            
            // Header
            CreateHeader();
            
            // Status section
            CreateStatusSection();
            
            // Controls section
            CreateControlsSection();
            
            // Visibility toggles
            CreateVisibilitySection();
            
            // Scenario buttons
            CreateScenarioSection();
            
            // Help text
            CreateHelpSection();
        }

        private void CreateHeader()
        {
            var header = CreateText(panelRoot.transform, "☁️ Weather Debug Panel", 16, FontStyle.Bold, headerColor);
            var headerLayout = header.gameObject.AddComponent<LayoutElement>();
            headerLayout.preferredHeight = 25;
            
            var separator = CreatePanel("Separator", panelRoot.transform, new Vector2(0, 2));
            separator.GetComponent<Image>().color = headerColor;
            var sepLayout = separator.AddComponent<LayoutElement>();
            sepLayout.preferredHeight = 2;
        }

        private void CreateStatusSection()
        {
            var sectionLabel = CreateText(panelRoot.transform, "Status", 12, FontStyle.Bold, headerColor);
            
            statusText = CreateText(panelRoot.transform, "Initializing...", 11, FontStyle.Normal, textColor);
            var statusLayout = statusText.gameObject.AddComponent<LayoutElement>();
            statusLayout.preferredHeight = 20;
            
            statsText = CreateText(panelRoot.transform, "", 10, FontStyle.Normal, new Color(0.8f, 0.8f, 0.8f));
            var statsLayout = statsText.gameObject.AddComponent<LayoutElement>();
            statsLayout.preferredHeight = 60;
        }

        private void CreateControlsSection()
        {
            var sectionLabel = CreateText(panelRoot.transform, "Controls", 12, FontStyle.Bold, headerColor);
            
            // Time Scale slider
            var timeScaleRow = CreateRow(panelRoot.transform);
            timeScaleLabel = CreateText(timeScaleRow.transform, "Time Scale: 1.0x", 11, FontStyle.Normal, textColor);
            timeScaleSlider = CreateSlider(timeScaleRow.transform, 0.1f, 100f, 1f);
            timeScaleSlider.onValueChanged.AddListener((value) => {
                if (simulator != null) simulator.TimeScale = value;
                timeScaleLabel.text = $"Time Scale: {value:F1}x";
            });
            
            // Quality slider
            var qualityRow = CreateRow(panelRoot.transform);
            qualityLabel = CreateText(qualityRow.transform, "Quality: 100%", 11, FontStyle.Normal, textColor);
            qualitySlider = CreateSlider(qualityRow.transform, 0.1f, 1f, 1f);
            qualitySlider.onValueChanged.AddListener((value) => {
                if (cloudVolume != null) cloudVolume.QualityLevel = value;
                qualityLabel.text = $"Quality: {value * 100:F0}%";
            });
            
            // Playback buttons
            var buttonRow = CreateRow(panelRoot.transform);
            
            var pauseBtn = CreateButton(buttonRow.transform, "⏸ Pause", () => {
                if (simulator != null) simulator.IsPaused = !simulator.IsPaused;
            });
            
            var resetBtn = CreateButton(buttonRow.transform, "⟳ Reset", () => {
                if (simulator != null) simulator.ResetSimulation();
            });
        }

        private void CreateVisibilitySection()
        {
            var sectionLabel = CreateText(panelRoot.transform, "Visibility", 12, FontStyle.Bold, headerColor);
            
            var toggleRow1 = CreateRow(panelRoot.transform);
            cloudsToggle = CreateToggle(toggleRow1.transform, "Clouds", true, (value) => {
                if (manager != null) manager.ShowVolumetricClouds = value;
            });
            pillarsToggle = CreateToggle(toggleRow1.transform, "Pillars", true, (value) => {
                if (manager != null) manager.ShowIntensityPillars = value;
            });
            
            var toggleRow2 = CreateRow(panelRoot.transform);
            lightningToggle = CreateToggle(toggleRow2.transform, "Lightning", true, (value) => {
                if (manager != null) manager.ShowLightning = value;
            });
            precipToggle = CreateToggle(toggleRow2.transform, "Precip", true, (value) => {
                if (manager != null) manager.ShowPrecipitation = value;
            });
        }

        private void CreateScenarioSection()
        {
            var sectionLabel = CreateText(panelRoot.transform, "Scenarios", 12, FontStyle.Bold, headerColor);
            
            var scenarioRow1 = CreateRow(panelRoot.transform);
            CreateButton(scenarioRow1.transform, "🌧️ Scattered", () => {
                simulator?.SetScenarioByType(ScenarioType.ScatteredShowers);
            });
            CreateButton(scenarioRow1.transform, "⛈️ Thunder", () => {
                simulator?.SetScenarioByType(ScenarioType.ThunderstormCells);
            });
            
            var scenarioRow2 = CreateRow(panelRoot.transform);
            CreateButton(scenarioRow2.transform, "🌪️ Squall", () => {
                simulator?.SetScenarioByType(ScenarioType.SquallLine);
            });
            CreateButton(scenarioRow2.transform, "🌀 Supercell", () => {
                simulator?.SetScenarioByType(ScenarioType.Supercell);
            });
        }

        private void CreateHelpSection()
        {
            var separator = CreatePanel("Separator2", panelRoot.transform, new Vector2(0, 1));
            separator.GetComponent<Image>().color = new Color(0.3f, 0.3f, 0.3f);
            var sepLayout = separator.AddComponent<LayoutElement>();
            sepLayout.preferredHeight = 1;
            
            var helpText = CreateText(panelRoot.transform, 
                "F1: Toggle Panel | P: Pause\n" +
                "1-4: Quick Scenarios | +/-: Time\n" +
                "Ctrl+R: Reset | RMB: Look",
                9, FontStyle.Normal, new Color(0.6f, 0.6f, 0.6f));
        }

        private void UpdateUI()
        {
            if (simulator == null)
            {
                FindComponents();
                if (simulator == null)
                {
                    if (statusText != null)
                        statusText.text = "No simulator found";
                    return;
                }
            }
            
            var stats = simulator.GetStats();
            
            // Update status
            string statusStr = stats.IsRunning ? "▶ RUNNING" : "⏸ PAUSED";
            Color statusColor = stats.IsRunning ? new Color(0.3f, 0.9f, 0.3f) : new Color(0.9f, 0.7f, 0.2f);
            
            if (statusText != null)
            {
                statusText.text = statusStr;
                statusText.color = statusColor;
            }
            
            // Update stats
            if (statsText != null)
            {
                var cells = simulator.GetActiveCells();
                string intensityBreakdown = "";
                
                if (cells != null && cells.Count > 0)
                {
                    int light = 0, moderate = 0, heavy = 0, extreme = 0;
                    foreach (var cell in cells)
                    {
                        if (cell == null) continue;
                        switch (cell.Intensity)
                        {
                            case IntensityLevel.Light: light++; break;
                            case IntensityLevel.Moderate: moderate++; break;
                            case IntensityLevel.Heavy: heavy++; break;
                            case IntensityLevel.Extreme: extreme++; break;
                        }
                    }
                    intensityBreakdown = $"\n🟢{light} 🟡{moderate} 🟠{heavy} 🔴{extreme}";
                }
                
                statsText.text = $"Scenario: {stats.ScenarioName}\n" +
                                $"Cells: {stats.ActiveCellCount}{intensityBreakdown}\n" +
                                $"Sim Time: {stats.SimulationTime:F1}s";
            }
            
            // Sync slider values
            if (timeScaleSlider != null && !timeScaleSlider.Equals(null))
            {
                if (Mathf.Abs(timeScaleSlider.value - stats.TimeScale) > 0.1f)
                {
                    timeScaleSlider.SetValueWithoutNotify(stats.TimeScale);
                    timeScaleLabel.text = $"Time Scale: {stats.TimeScale:F1}x";
                }
            }
        }

        #region UI Creation Helpers
        
        private GameObject CreatePanel(string name, Transform parent, Vector2 size)
        {
            var panel = new GameObject(name);
            panel.transform.SetParent(parent, false);
            
            var rect = panel.AddComponent<RectTransform>();
            rect.sizeDelta = size;
            
            var image = panel.AddComponent<Image>();
            image.color = panelColor;
            
            return panel;
        }

        private Text CreateText(Transform parent, string content, int fontSize, FontStyle style, Color color)
        {
            var textObj = new GameObject("Text");
            textObj.transform.SetParent(parent, false);
            
            var text = textObj.AddComponent<Text>();
            text.text = content;
            text.font = defaultFont;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = color;
            text.alignment = TextAnchor.MiddleLeft;
            
            var rect = textObj.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(260, fontSize + 6);
            
            return text;
        }

        private GameObject CreateRow(Transform parent)
        {
            var row = new GameObject("Row");
            row.transform.SetParent(parent, false);
            
            var layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 5;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            
            var layoutElement = row.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = 25;
            
            return row;
        }

        private Slider CreateSlider(Transform parent, float min, float max, float value)
        {
            var sliderObj = new GameObject("Slider");
            sliderObj.transform.SetParent(parent, false);
            
            var sliderRect = sliderObj.AddComponent<RectTransform>();
            sliderRect.sizeDelta = new Vector2(100, 20);
            
            // Background
            var bgObj = new GameObject("Background");
            bgObj.transform.SetParent(sliderObj.transform, false);
            var bgRect = bgObj.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;
            var bgImage = bgObj.AddComponent<Image>();
            bgImage.color = new Color(0.2f, 0.2f, 0.2f);
            
            // Fill area
            var fillArea = new GameObject("Fill Area");
            fillArea.transform.SetParent(sliderObj.transform, false);
            var fillAreaRect = fillArea.AddComponent<RectTransform>();
            fillAreaRect.anchorMin = Vector2.zero;
            fillAreaRect.anchorMax = Vector2.one;
            fillAreaRect.offsetMin = new Vector2(5, 0);
            fillAreaRect.offsetMax = new Vector2(-5, 0);
            
            // Fill
            var fillObj = new GameObject("Fill");
            fillObj.transform.SetParent(fillArea.transform, false);
            var fillRect = fillObj.AddComponent<RectTransform>();
            fillRect.sizeDelta = Vector2.zero;
            var fillImage = fillObj.AddComponent<Image>();
            fillImage.color = headerColor;
            
            // Handle slide area
            var handleArea = new GameObject("Handle Slide Area");
            handleArea.transform.SetParent(sliderObj.transform, false);
            var handleAreaRect = handleArea.AddComponent<RectTransform>();
            handleAreaRect.anchorMin = Vector2.zero;
            handleAreaRect.anchorMax = Vector2.one;
            handleAreaRect.offsetMin = new Vector2(10, 0);
            handleAreaRect.offsetMax = new Vector2(-10, 0);
            
            // Handle
            var handleObj = new GameObject("Handle");
            handleObj.transform.SetParent(handleArea.transform, false);
            var handleRect = handleObj.AddComponent<RectTransform>();
            handleRect.sizeDelta = new Vector2(10, 0);
            var handleImage = handleObj.AddComponent<Image>();
            handleImage.color = Color.white;
            
            var slider = sliderObj.AddComponent<Slider>();
            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = value;
            slider.targetGraphic = handleImage;
            
            var layoutElement = sliderObj.AddComponent<LayoutElement>();
            layoutElement.flexibleWidth = 1;
            
            return slider;
        }

        private Button CreateButton(Transform parent, string label, System.Action onClick)
        {
            var buttonObj = new GameObject("Button");
            buttonObj.transform.SetParent(parent, false);
            
            var image = buttonObj.AddComponent<Image>();
            image.color = new Color(0.25f, 0.25f, 0.3f);
            
            var button = buttonObj.AddComponent<Button>();
            button.targetGraphic = image;
            
            var colors = button.colors;
            colors.highlightedColor = new Color(0.35f, 0.35f, 0.4f);
            colors.pressedColor = new Color(0.2f, 0.5f, 0.8f);
            button.colors = colors;
            
            button.onClick.AddListener(() => onClick?.Invoke());
            
            // Label
            var labelObj = new GameObject("Label");
            labelObj.transform.SetParent(buttonObj.transform, false);
            var labelRect = labelObj.AddComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.sizeDelta = Vector2.zero;
            
            var text = labelObj.AddComponent<Text>();
            text.text = label;
            text.font = defaultFont;
            text.fontSize = 11;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            
            var layoutElement = buttonObj.AddComponent<LayoutElement>();
            layoutElement.flexibleWidth = 1;
            layoutElement.preferredHeight = 25;
            
            return button;
        }

        private Toggle CreateToggle(Transform parent, string label, bool isOn, System.Action<bool> onChanged)
        {
            var toggleObj = new GameObject("Toggle");
            toggleObj.transform.SetParent(parent, false);
            
            var toggle = toggleObj.AddComponent<Toggle>();
            toggle.isOn = isOn;
            toggle.onValueChanged.AddListener((value) => onChanged?.Invoke(value));
            
            // Background
            var bgObj = new GameObject("Background");
            bgObj.transform.SetParent(toggleObj.transform, false);
            var bgRect = bgObj.AddComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0, 0.5f);
            bgRect.anchorMax = new Vector2(0, 0.5f);
            bgRect.pivot = new Vector2(0, 0.5f);
            bgRect.anchoredPosition = new Vector2(0, 0);
            bgRect.sizeDelta = new Vector2(16, 16);
            var bgImage = bgObj.AddComponent<Image>();
            bgImage.color = new Color(0.2f, 0.2f, 0.2f);
            
            // Checkmark
            var checkObj = new GameObject("Checkmark");
            checkObj.transform.SetParent(bgObj.transform, false);
            var checkRect = checkObj.AddComponent<RectTransform>();
            checkRect.anchorMin = Vector2.zero;
            checkRect.anchorMax = Vector2.one;
            checkRect.sizeDelta = new Vector2(-4, -4);
            var checkImage = checkObj.AddComponent<Image>();
            checkImage.color = headerColor;
            
            toggle.graphic = checkImage;
            toggle.targetGraphic = bgImage;
            
            // Label
            var labelObj = new GameObject("Label");
            labelObj.transform.SetParent(toggleObj.transform, false);
            var labelRect = labelObj.AddComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0, 0);
            labelRect.anchorMax = new Vector2(1, 1);
            labelRect.offsetMin = new Vector2(20, 0);
            labelRect.offsetMax = Vector2.zero;
            
            var text = labelObj.AddComponent<Text>();
            text.text = label;
            text.font = defaultFont;
            text.fontSize = 11;
            text.alignment = TextAnchor.MiddleLeft;
            text.color = textColor;
            
            var layoutElement = toggleObj.AddComponent<LayoutElement>();
            layoutElement.flexibleWidth = 1;
            layoutElement.preferredHeight = 20;
            
            return toggle;
        }
        
        #endregion
    }
}
