using UnityEngine;
using UnityEngine.UI;

namespace AviationUI.Panels
{
    /// <summary>
    /// Vertical airspeed tape panel on the left side of the display.
    /// Shows moving speed indicators with digital readout box.
    /// </summary>
    public class AirspeedTapePanel : AviationUIPanel
    {
        [Header("Airspeed Tape References")]
        [SerializeField] private RectTransform tapeContent;
        [SerializeField] private Text airspeedValueText;
        [SerializeField] private Image airspeedBox;
        [SerializeField] private Image trendArrow;

        [Header("Airspeed Settings")]
        [SerializeField] private float tapeHeight = 400f;
        [SerializeField] private float knotsVisible = 60f;
        [SerializeField] private int tickInterval = 10; // Knots between major ticks
        [SerializeField] private int minorTickInterval = 5;
        [SerializeField] private float maxDisplaySpeed = 200f;

        [Header("Prefabs")]
        [SerializeField] private GameObject tickPrefab;
        [SerializeField] private GameObject labelPrefab;

        private float pixelsPerKnot;

        public override string PanelId => "AirspeedTape";
        public override Vector2 DefaultAnchorPosition => new Vector2(0.15f, 0.5f);

        protected override void InitializePanel()
        {
            pixelsPerKnot = tapeHeight / knotsVisible;

            if (tapeContent != null && tickPrefab != null)
            {
                GenerateTapeMarks();
            }

            ApplyColors();
        }

        private void GenerateTapeMarks()
        {
            // Clear existing
            foreach (Transform child in tapeContent)
            {
                if (Application.isPlaying)
                    Destroy(child.gameObject);
                else
                    DestroyImmediate(child.gameObject);
            }

            // Generate ticks from 0 to max speed
            for (int speed = 0; speed <= (int)maxDisplaySpeed; speed += minorTickInterval)
            {
                float yPos = speed * pixelsPerKnot;
                bool isMajor = speed % tickInterval == 0;

                // Create tick
                if (tickPrefab != null)
                {
                    GameObject tick = Instantiate(tickPrefab, tapeContent);
                    RectTransform tickRect = tick.GetComponent<RectTransform>();
                    tickRect.anchoredPosition = new Vector2(0, yPos);
                    tickRect.sizeDelta = new Vector2(isMajor ? 30 : 15, 2);
                    
                    Image tickImage = tick.GetComponent<Image>();
                    if (tickImage != null && config != null)
                    {
                        tickImage.color = config.primaryColor;
                    }
                }

                // Create labels for major ticks
                if (isMajor && labelPrefab != null)
                {
                    GameObject label = Instantiate(labelPrefab, tapeContent);
                    RectTransform labelRect = label.GetComponent<RectTransform>();
                    labelRect.anchoredPosition = new Vector2(-40, yPos);
                    
                    Text labelText = label.GetComponent<Text>();
                    if (labelText != null)
                    {
                        labelText.text = speed.ToString();
                        labelText.alignment = TextAnchor.MiddleRight;
                        if (config != null)
                        {
                            labelText.color = config.textColor;
                            labelText.fontSize = config.secondaryFontSize / 2;
                        }
                    }
                }
            }
        }

        protected override void UpdateDisplay()
        {
            if (displayData == null) return;

            float airspeed = displayData.indicatedAirspeed;

            // Update tape position
            if (tapeContent != null)
            {
                float yOffset = -airspeed * pixelsPerKnot;
                tapeContent.anchoredPosition = new Vector2(tapeContent.anchoredPosition.x, yOffset);
            }

            // Update numeric display
            if (airspeedValueText != null)
            {
                int displaySpeed = Mathf.RoundToInt(Mathf.Max(0, airspeed));
                airspeedValueText.text = displaySpeed.ToString("000");
            }

            // Update colors based on thresholds
            if (config != null)
            {
                Color thresholdColor = GetThresholdColor(
                    airspeed,
                    config.airspeedWarningThreshold,
                    config.airspeedDangerThreshold
                );

                if (airspeedValueText != null)
                    airspeedValueText.color = thresholdColor;

                if (airspeedBox != null)
                    airspeedBox.color = thresholdColor;
            }

            // Update trend arrow (optional)
            UpdateTrendArrow();
        }

        private void UpdateTrendArrow()
        {
            if (trendArrow == null) return;

            // Calculate trend based on vertical speed affecting airspeed
            // This is a simplified representation
            float trend = displayData.verticalSpeed * 0.01f; // Simple approximation
            
            if (Mathf.Abs(trend) > 0.5f)
            {
                trendArrow.gameObject.SetActive(true);
                float arrowLength = Mathf.Clamp(Mathf.Abs(trend), 10, 50);
                trendArrow.rectTransform.sizeDelta = new Vector2(10, arrowLength);
                trendArrow.rectTransform.localScale = new Vector3(1, trend > 0 ? 1 : -1, 1);
            }
            else
            {
                trendArrow.gameObject.SetActive(false);
            }
        }

        protected override void ApplyColors()
        {
            if (config == null) return;

            if (airspeedValueText != null)
                airspeedValueText.color = config.textColor;

            if (airspeedBox != null)
                airspeedBox.color = config.primaryColor;
        }

        /// <summary>
        /// Create the airspeed tape panel structure
        /// </summary>
        public static AirspeedTapePanel CreatePanel(Transform parent, AviationUIConfig config)
        {
            // Create panel root
            GameObject panelObj = new GameObject("AirspeedTapePanel");
            panelObj.transform.SetParent(parent, false);
            
            RectTransform panelRect = panelObj.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0, 0.5f);
            panelRect.anchorMax = new Vector2(0, 0.5f);
            panelRect.pivot = new Vector2(0, 0.5f);
            panelRect.anchoredPosition = new Vector2(50, 0);
            panelRect.sizeDelta = new Vector2(100, 300);

            panelObj.AddComponent<CanvasGroup>();

            // Create mask
            GameObject maskObj = new GameObject("TapeMask");
            maskObj.transform.SetParent(panelObj.transform, false);
            
            RectTransform maskRect = maskObj.AddComponent<RectTransform>();
            maskRect.anchorMin = Vector2.zero;
            maskRect.anchorMax = Vector2.one;
            maskRect.sizeDelta = Vector2.zero;
            
            Image maskImage = maskObj.AddComponent<Image>();
            maskImage.color = new Color(0, 0, 0, 0.3f);
            
            Mask mask = maskObj.AddComponent<Mask>();
            mask.showMaskGraphic = true;

            // Create tape content
            GameObject tapeObj = new GameObject("TapeContent");
            tapeObj.transform.SetParent(maskObj.transform, false);
            
            RectTransform tapeRect = tapeObj.AddComponent<RectTransform>();
            tapeRect.anchorMin = new Vector2(1, 0.5f);
            tapeRect.anchorMax = new Vector2(1, 0.5f);
            tapeRect.pivot = new Vector2(1, 0.5f);
            tapeRect.sizeDelta = new Vector2(80, 1000);

            // Create airspeed box
            GameObject boxObj = new GameObject("AirspeedBox");
            boxObj.transform.SetParent(panelObj.transform, false);
            
            RectTransform boxRect = boxObj.AddComponent<RectTransform>();
            boxRect.anchorMin = new Vector2(1, 0.5f);
            boxRect.anchorMax = new Vector2(1, 0.5f);
            boxRect.pivot = new Vector2(0, 0.5f);
            boxRect.anchoredPosition = new Vector2(5, 0);
            boxRect.sizeDelta = new Vector2(70, 35);
            
            Image boxImage = boxObj.AddComponent<Image>();
            boxImage.color = config != null ? config.primaryColor : Color.green;

            // Create airspeed text
            GameObject textObj = new GameObject("AirspeedValue");
            textObj.transform.SetParent(boxObj.transform, false);
            
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;
            
            Text speedText = textObj.AddComponent<Text>();
            speedText.text = "000";
            speedText.alignment = TextAnchor.MiddleCenter;
            speedText.fontSize = config != null ? config.secondaryFontSize : 32;
            speedText.color = config != null ? config.textColor : Color.white;
            speedText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // Add panel component
            AirspeedTapePanel panel = panelObj.AddComponent<AirspeedTapePanel>();
            panel.tapeContent = tapeRect;
            panel.airspeedValueText = speedText;
            panel.airspeedBox = boxImage;
            panel.config = config;

            return panel;
        }
    }
}
