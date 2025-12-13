using UnityEngine;
using UnityEngine.UI;

namespace AviationUI.Panels
{
    /// <summary>
    /// Engine gauges panel showing torque and rotor RPM (NR) for dual engines.
    /// Display as bar indicators with percentage values.
    /// </summary>
    public class EngineGaugesPanel : AviationUIPanel
    {
        [Header("Engine 1 References")]
        [SerializeField] private RectTransform engine1TorqueBar;
        [SerializeField] private RectTransform engine1NRBar;
        [SerializeField] private Text engine1TorqueText;
        [SerializeField] private Text engine1NRText;

        [Header("Engine 2 References")]
        [SerializeField] private RectTransform engine2TorqueBar;
        [SerializeField] private RectTransform engine2NRBar;
        [SerializeField] private Text engine2TorqueText;
        [SerializeField] private Text engine2NRText;

        [Header("Gauge Settings")]
        [SerializeField] private float maxBarHeight = 100f;
        [SerializeField] private float maxTorque = 120f;
        [SerializeField] private float maxNR = 110f;

        [Header("Labels")]
        [SerializeField] private Text torqueLabel;
        [SerializeField] private Text nrLabel;

        private Image[] allBarImages;

        public override string PanelId => "EngineGauges";
        public override Vector2 DefaultAnchorPosition => new Vector2(0.5f, 0.15f);

        protected override void InitializePanel()
        {
            // Cache bar images for color updates
            allBarImages = new Image[]
            {
                engine1TorqueBar?.GetComponent<Image>(),
                engine2TorqueBar?.GetComponent<Image>(),
                engine1NRBar?.GetComponent<Image>(),
                engine2NRBar?.GetComponent<Image>()
            };

            ApplyColors();
        }

        protected override void UpdateDisplay()
        {
            if (displayData == null) return;

            // Update Engine 1
            UpdateGaugeBar(engine1TorqueBar, displayData.engine1Torque, maxTorque);
            UpdateGaugeBar(engine1NRBar, displayData.engine1NR, maxNR);
            UpdateGaugeText(engine1TorqueText, displayData.engine1Torque, "%");
            UpdateGaugeText(engine1NRText, displayData.engine1NR, "%");

            // Update Engine 2
            UpdateGaugeBar(engine2TorqueBar, displayData.engine2Torque, maxTorque);
            UpdateGaugeBar(engine2NRBar, displayData.engine2NR, maxNR);
            UpdateGaugeText(engine2TorqueText, displayData.engine2Torque, "%");
            UpdateGaugeText(engine2NRText, displayData.engine2NR, "%");

            // Update colors based on thresholds
            UpdateGaugeColors();
        }

        private void UpdateGaugeBar(RectTransform bar, float value, float maxValue)
        {
            if (bar == null) return;

            float normalized = Mathf.Clamp01(value / maxValue);
            float height = normalized * maxBarHeight;
            bar.sizeDelta = new Vector2(bar.sizeDelta.x, height);
        }

        private void UpdateGaugeText(Text text, float value, string suffix = "")
        {
            if (text == null) return;

            text.text = value.ToString("F1") + suffix;
        }

        private void UpdateGaugeColors()
        {
            if (config == null) return;

            // Get highest torque value for color warning
            float maxTorqueValue = Mathf.Max(displayData.engine1Torque, displayData.engine2Torque);
            Color torqueColor = GetThresholdColor(
                maxTorqueValue,
                config.torqueWarningThreshold,
                config.torqueDangerThreshold
            );

            // Apply to torque elements
            if (engine1TorqueBar != null)
            {
                var img = engine1TorqueBar.GetComponent<Image>();
                if (img != null) img.color = GetThresholdColor(displayData.engine1Torque, config.torqueWarningThreshold, config.torqueDangerThreshold);
            }
            if (engine2TorqueBar != null)
            {
                var img = engine2TorqueBar.GetComponent<Image>();
                if (img != null) img.color = GetThresholdColor(displayData.engine2Torque, config.torqueWarningThreshold, config.torqueDangerThreshold);
            }

            if (engine1TorqueText != null)
                engine1TorqueText.color = GetThresholdColor(displayData.engine1Torque, config.torqueWarningThreshold, config.torqueDangerThreshold);
            if (engine2TorqueText != null)
                engine2TorqueText.color = GetThresholdColor(displayData.engine2Torque, config.torqueWarningThreshold, config.torqueDangerThreshold);

            // NR gauges - generally stay green unless extreme
            Color nrColor = config.primaryColor;
            if (engine1NRBar != null)
            {
                var img = engine1NRBar.GetComponent<Image>();
                if (img != null) img.color = nrColor;
            }
            if (engine2NRBar != null)
            {
                var img = engine2NRBar.GetComponent<Image>();
                if (img != null) img.color = nrColor;
            }
        }

        protected override void ApplyColors()
        {
            if (config == null) return;

            foreach (var img in allBarImages)
            {
                if (img != null)
                    img.color = config.primaryColor;
            }

            if (torqueLabel != null)
                torqueLabel.color = config.textColor;

            if (nrLabel != null)
                nrLabel.color = config.textColor;
        }

        /// <summary>
        /// Create the engine gauges panel structure
        /// </summary>
        public static EngineGaugesPanel CreatePanel(Transform parent, AviationUIConfig config)
        {
            // Create panel root
            GameObject panelObj = new GameObject("EngineGaugesPanel");
            panelObj.transform.SetParent(parent, false);
            
            RectTransform panelRect = panelObj.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0);
            panelRect.anchorMax = new Vector2(0.5f, 0);
            panelRect.pivot = new Vector2(0.5f, 0);
            panelRect.anchoredPosition = new Vector2(0, 50);
            panelRect.sizeDelta = new Vector2(200, 150);

            panelObj.AddComponent<CanvasGroup>();

            // Create background
            Image bgImage = panelObj.AddComponent<Image>();
            bgImage.color = new Color(0, 0, 0, 0.3f);

            // Create gauge containers
            float barWidth = 20f;
            float spacing = 10f;
            float startX = -60f;

            // Engine 1 Torque
            var e1tBar = CreateGaugeBar(panelObj.transform, "E1_Torque", startX, config);
            var e1tText = CreateGaugeLabel(panelObj.transform, "E1_TQ", startX, -60f, "TQ", config);
            var e1tValue = CreateGaugeValue(panelObj.transform, "E1_TQ_Value", startX, -80f, "0.0%", config);

            // Engine 1 NR
            var e1nBar = CreateGaugeBar(panelObj.transform, "E1_NR", startX + barWidth + spacing, config);

            // Engine 2 Torque
            var e2tBar = CreateGaugeBar(panelObj.transform, "E2_Torque", startX + (barWidth + spacing) * 2, config);
            var e2tText = CreateGaugeLabel(panelObj.transform, "E2_TQ", startX + (barWidth + spacing) * 2, -60f, "TQ", config);
            var e2tValue = CreateGaugeValue(panelObj.transform, "E2_TQ_Value", startX + (barWidth + spacing) * 2, -80f, "0.0%", config);

            // Engine 2 NR
            var e2nBar = CreateGaugeBar(panelObj.transform, "E2_NR", startX + (barWidth + spacing) * 3, config);

            // NR label
            var nrLabel = CreateGaugeLabel(panelObj.transform, "NR_Label", 50f, -60f, "NR", config);
            var nrValue = CreateGaugeValue(panelObj.transform, "NR_Value", 50f, -80f, "100%", config);

            // Add panel component
            EngineGaugesPanel panel = panelObj.AddComponent<EngineGaugesPanel>();
            panel.engine1TorqueBar = e1tBar;
            panel.engine1NRBar = e1nBar;
            panel.engine2TorqueBar = e2tBar;
            panel.engine2NRBar = e2nBar;
            panel.engine1TorqueText = e1tValue;
            panel.engine2TorqueText = e2tValue;
            panel.config = config;

            return panel;
        }

        private static RectTransform CreateGaugeBar(Transform parent, string name, float xPos, AviationUIConfig config)
        {
            GameObject barBg = new GameObject(name + "_BG");
            barBg.transform.SetParent(parent, false);
            
            RectTransform bgRect = barBg.AddComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0.5f, 0.5f);
            bgRect.anchorMax = new Vector2(0.5f, 0.5f);
            bgRect.anchoredPosition = new Vector2(xPos, 20);
            bgRect.sizeDelta = new Vector2(20, 100);
            
            Image bgImg = barBg.AddComponent<Image>();
            bgImg.color = new Color(0.2f, 0.2f, 0.2f, 0.5f);

            GameObject bar = new GameObject(name);
            bar.transform.SetParent(barBg.transform, false);
            
            RectTransform barRect = bar.AddComponent<RectTransform>();
            barRect.anchorMin = new Vector2(0, 0);
            barRect.anchorMax = new Vector2(1, 0);
            barRect.pivot = new Vector2(0.5f, 0);
            barRect.sizeDelta = new Vector2(0, 50);
            
            Image barImg = bar.AddComponent<Image>();
            barImg.color = config != null ? config.primaryColor : Color.green;

            return barRect;
        }

        private static Text CreateGaugeLabel(Transform parent, string name, float xPos, float yPos, string text, AviationUIConfig config)
        {
            GameObject labelObj = new GameObject(name);
            labelObj.transform.SetParent(parent, false);
            
            RectTransform labelRect = labelObj.AddComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0.5f, 0.5f);
            labelRect.anchorMax = new Vector2(0.5f, 0.5f);
            labelRect.anchoredPosition = new Vector2(xPos, yPos);
            labelRect.sizeDelta = new Vector2(40, 20);
            
            Text labelText = labelObj.AddComponent<Text>();
            labelText.text = text;
            labelText.alignment = TextAnchor.MiddleCenter;
            labelText.fontSize = 16;
            labelText.color = config != null ? config.textColor : Color.white;
            labelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            return labelText;
        }

        private static Text CreateGaugeValue(Transform parent, string name, float xPos, float yPos, string text, AviationUIConfig config)
        {
            GameObject valueObj = new GameObject(name);
            valueObj.transform.SetParent(parent, false);
            
            RectTransform valueRect = valueObj.AddComponent<RectTransform>();
            valueRect.anchorMin = new Vector2(0.5f, 0.5f);
            valueRect.anchorMax = new Vector2(0.5f, 0.5f);
            valueRect.anchoredPosition = new Vector2(xPos, yPos);
            valueRect.sizeDelta = new Vector2(60, 20);
            
            Text valueText = valueObj.AddComponent<Text>();
            valueText.text = text;
            valueText.alignment = TextAnchor.MiddleCenter;
            valueText.fontSize = 14;
            valueText.color = config != null ? config.primaryColor : Color.green;
            valueText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            return valueText;
        }
    }
}
