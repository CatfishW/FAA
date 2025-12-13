using UnityEngine;
using UnityEngine.UI;

namespace AviationUI.Panels
{
    /// <summary>
    /// Vertical altitude tape panel on the right side of the display.
    /// Shows moving altitude indicators with digital readout box.
    /// </summary>
    public class AltitudeTapePanel : AviationUIPanel
    {
        [Header("Altitude Tape References")]
        [SerializeField] private RectTransform tapeContent;
        [SerializeField] private Text altitudeValueText;
        [SerializeField] private Text altitudeAGLText;
        [SerializeField] private Image altitudeBox;
        [SerializeField] private Image aglBox;

        [Header("Altitude Settings")]
        [SerializeField] private float tapeHeight = 400f;
        [SerializeField] private float feetVisible = 600f;
        [SerializeField] private int tickInterval = 100; // Feet between major ticks
        [SerializeField] private int minorTickInterval = 50;
        [SerializeField] private float maxDisplayAltitude = 25000f;

        [Header("Prefabs")]
        [SerializeField] private GameObject tickPrefab;
        [SerializeField] private GameObject labelPrefab;

        private float pixelsPerFoot;

        public override string PanelId => "AltitudeTape";
        public override Vector2 DefaultAnchorPosition => new Vector2(0.85f, 0.5f);

        protected override void InitializePanel()
        {
            pixelsPerFoot = tapeHeight / feetVisible;

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

            // Generate ticks from 0 to max altitude
            for (int alt = 0; alt <= (int)maxDisplayAltitude; alt += minorTickInterval)
            {
                float yPos = alt * pixelsPerFoot;
                bool isMajor = alt % tickInterval == 0;

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

                // Create labels for major ticks (every 200 feet to reduce clutter)
                if (isMajor && alt % 200 == 0 && labelPrefab != null)
                {
                    GameObject label = Instantiate(labelPrefab, tapeContent);
                    RectTransform labelRect = label.GetComponent<RectTransform>();
                    labelRect.anchoredPosition = new Vector2(40, yPos);
                    
                    Text labelText = label.GetComponent<Text>();
                    if (labelText != null)
                    {
                        // Format altitude display (e.g., 10000 -> 100, displayed as hundreds)
                        labelText.text = (alt / 100).ToString();
                        labelText.alignment = TextAnchor.MiddleLeft;
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

            float altitude = displayData.altitudeMSL;
            float altitudeAGL = displayData.altitudeAGL;

            // Update tape position
            if (tapeContent != null)
            {
                float yOffset = -altitude * pixelsPerFoot;
                tapeContent.anchoredPosition = new Vector2(tapeContent.anchoredPosition.x, yOffset);
            }

            // Update MSL altitude display
            if (altitudeValueText != null)
            {
                int displayAlt = Mathf.RoundToInt(Mathf.Max(0, altitude));
                altitudeValueText.text = displayAlt.ToString("00000");
            }

            // Update AGL altitude display
            if (altitudeAGLText != null)
            {
                int displayAGL = Mathf.RoundToInt(Mathf.Max(0, altitudeAGL));
                altitudeAGLText.text = displayAGL.ToString();
            }

            // Update colors based on thresholds
            if (config != null)
            {
                Color thresholdColor = GetThresholdColor(
                    altitude,
                    config.altitudeWarningThreshold,
                    config.altitudeDangerThreshold
                );

                if (altitudeValueText != null)
                    altitudeValueText.color = thresholdColor;

                if (altitudeBox != null)
                    altitudeBox.color = thresholdColor;

                // AGL gets danger color when low
                if (altitudeAGL < 500)
                {
                    Color aglColor = altitudeAGL < 200 ? config.dangerColor : 
                                    altitudeAGL < 500 ? config.warningColor : config.primaryColor;
                    
                    if (altitudeAGLText != null)
                        altitudeAGLText.color = aglColor;
                    
                    if (aglBox != null)
                        aglBox.color = aglColor;
                }
            }
        }

        protected override void ApplyColors()
        {
            if (config == null) return;

            if (altitudeValueText != null)
                altitudeValueText.color = config.textColor;

            if (altitudeBox != null)
                altitudeBox.color = config.primaryColor;

            if (altitudeAGLText != null)
                altitudeAGLText.color = config.textColor;

            if (aglBox != null)
                aglBox.color = config.primaryColor;
        }

        /// <summary>
        /// Create the altitude tape panel structure
        /// </summary>
        public static AltitudeTapePanel CreatePanel(Transform parent, AviationUIConfig config)
        {
            // Create panel root
            GameObject panelObj = new GameObject("AltitudeTapePanel");
            panelObj.transform.SetParent(parent, false);
            
            RectTransform panelRect = panelObj.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(1, 0.5f);
            panelRect.anchorMax = new Vector2(1, 0.5f);
            panelRect.pivot = new Vector2(1, 0.5f);
            panelRect.anchoredPosition = new Vector2(-50, 0);
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
            tapeRect.anchorMin = new Vector2(0, 0.5f);
            tapeRect.anchorMax = new Vector2(0, 0.5f);
            tapeRect.pivot = new Vector2(0, 0.5f);
            tapeRect.sizeDelta = new Vector2(80, 2000);

            // Create altitude box
            GameObject boxObj = new GameObject("AltitudeBox");
            boxObj.transform.SetParent(panelObj.transform, false);
            
            RectTransform boxRect = boxObj.AddComponent<RectTransform>();
            boxRect.anchorMin = new Vector2(0, 0.5f);
            boxRect.anchorMax = new Vector2(0, 0.5f);
            boxRect.pivot = new Vector2(1, 0.5f);
            boxRect.anchoredPosition = new Vector2(-5, 0);
            boxRect.sizeDelta = new Vector2(80, 35);
            
            Image boxImage = boxObj.AddComponent<Image>();
            boxImage.color = config != null ? config.primaryColor : Color.green;

            // Create altitude text
            GameObject textObj = new GameObject("AltitudeValue");
            textObj.transform.SetParent(boxObj.transform, false);
            
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;
            
            Text altText = textObj.AddComponent<Text>();
            altText.text = "00000";
            altText.alignment = TextAnchor.MiddleCenter;
            altText.fontSize = config != null ? config.secondaryFontSize : 32;
            altText.color = config != null ? config.textColor : Color.white;
            altText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // Create AGL box
            GameObject aglBoxObj = new GameObject("AGLBox");
            aglBoxObj.transform.SetParent(panelObj.transform, false);
            
            RectTransform aglBoxRect = aglBoxObj.AddComponent<RectTransform>();
            aglBoxRect.anchorMin = new Vector2(0.5f, 0);
            aglBoxRect.anchorMax = new Vector2(0.5f, 0);
            aglBoxRect.pivot = new Vector2(0.5f, 1);
            aglBoxRect.anchoredPosition = new Vector2(0, -10);
            aglBoxRect.sizeDelta = new Vector2(70, 30);
            
            Image aglBoxImage = aglBoxObj.AddComponent<Image>();
            aglBoxImage.color = config != null ? config.primaryColor : Color.green;

            // Create AGL text
            GameObject aglTextObj = new GameObject("AGLValue");
            aglTextObj.transform.SetParent(aglBoxObj.transform, false);
            
            RectTransform aglTextRect = aglTextObj.AddComponent<RectTransform>();
            aglTextRect.anchorMin = Vector2.zero;
            aglTextRect.anchorMax = Vector2.one;
            aglTextRect.sizeDelta = Vector2.zero;
            
            Text aglText = aglTextObj.AddComponent<Text>();
            aglText.text = "0";
            aglText.alignment = TextAnchor.MiddleCenter;
            aglText.fontSize = config != null ? config.labelFontSize : 24;
            aglText.color = config != null ? config.textColor : Color.white;
            aglText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // Create label
            GameObject labelObj = new GameObject("AGLLabel");
            labelObj.transform.SetParent(aglBoxObj.transform, false);
            
            RectTransform labelRect = labelObj.AddComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0, 0);
            labelRect.anchorMax = new Vector2(0, 0);
            labelRect.pivot = new Vector2(1, 0.5f);
            labelRect.anchoredPosition = new Vector2(-5, 15);
            labelRect.sizeDelta = new Vector2(40, 20);
            
            Text labelText = labelObj.AddComponent<Text>();
            labelText.text = "AGL";
            labelText.alignment = TextAnchor.MiddleRight;
            labelText.fontSize = 16;
            labelText.color = config != null ? config.textColor : Color.white;
            labelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // Add panel component
            AltitudeTapePanel panel = panelObj.AddComponent<AltitudeTapePanel>();
            panel.tapeContent = tapeRect;
            panel.altitudeValueText = altText;
            panel.altitudeAGLText = aglText;
            panel.altitudeBox = boxImage;
            panel.aglBox = aglBoxImage;
            panel.config = config;

            return panel;
        }
    }
}
