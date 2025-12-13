using UnityEngine;
using UnityEngine.UI;

namespace AviationUI.Panels
{
    /// <summary>
    /// Heading compass tape panel displayed at the top of the screen.
    /// Shows cardinal directions (N, NE, E, SE, S, SW, W, NW) with numeric heading.
    /// </summary>
    public class HeadingTapePanel : AviationUIPanel
    {
        [Header("Heading Tape References")]
        [SerializeField] private RectTransform tapeContent;
        [SerializeField] private Text headingValueText;
        [SerializeField] private Image headingBox;
        [SerializeField] private Image headingPointer;

        [Header("Heading Tape Settings")]
        [SerializeField] private float tapeWidth = 1000f;
        [SerializeField] private float degreesVisible = 120f;
        [SerializeField] private GameObject tickPrefab;
        [SerializeField] private GameObject labelPrefab;

        private float pixelsPerDegree;
        private Text[] cardinalLabels;
        private RectTransform[] tickMarks;

        public override string PanelId => "HeadingTape";
        public override Vector2 DefaultAnchorPosition => new Vector2(0.5f, 1f);

        protected override void InitializePanel()
        {
            pixelsPerDegree = tapeWidth / degreesVisible;
            
            // Generate tick marks and labels if we have prefabs
            if (tickPrefab != null && tapeContent != null)
            {
                GenerateTapeMarks();
            }

            ApplyColors();
        }

        private void GenerateTapeMarks()
        {
            // Clear existing children
            foreach (Transform child in tapeContent)
            {
                if (Application.isPlaying)
                    Destroy(child.gameObject);
                else
                    DestroyImmediate(child.gameObject);
            }

            // Generate marks for 360 degrees + extra for wrapping
            for (int i = -10; i <= 370; i += 5)
            {
                float xPos = i * pixelsPerDegree;
                
                // Create tick mark
                if (tickPrefab != null)
                {
                    GameObject tick = Instantiate(tickPrefab, tapeContent);
                    RectTransform tickRect = tick.GetComponent<RectTransform>();
                    tickRect.anchoredPosition = new Vector2(xPos, 0);
                    
                    // Major tick every 10 degrees
                    bool isMajor = i % 10 == 0;
                    tickRect.sizeDelta = new Vector2(2, isMajor ? 20 : 10);
                }

                // Create labels for cardinal directions and every 30 degrees
                if (labelPrefab != null && i >= 0 && i <= 360)
                {
                    string labelText = GetCardinalLabel(i);
                    if (!string.IsNullOrEmpty(labelText))
                    {
                        GameObject label = Instantiate(labelPrefab, tapeContent);
                        RectTransform labelRect = label.GetComponent<RectTransform>();
                        labelRect.anchoredPosition = new Vector2(xPos, -25);
                        
                        Text labelTextComp = label.GetComponent<Text>();
                        if (labelTextComp != null)
                        {
                            labelTextComp.text = labelText;
                            if (config != null)
                            {
                                labelTextComp.color = config.textColor;
                            }
                        }
                    }
                }
            }
        }

        private string GetCardinalLabel(int degrees)
        {
            degrees = ((degrees % 360) + 360) % 360;
            
            switch (degrees)
            {
                case 0: return "N";
                case 45: return "NE";
                case 90: return "E";
                case 135: return "SE";
                case 180: return "S";
                case 225: return "SW";
                case 270: return "W";
                case 315: return "NW";
                default:
                    if (degrees % 30 == 0)
                        return degrees.ToString();
                    return null;
            }
        }

        protected override void UpdateDisplay()
        {
            if (displayData == null) return;

            float heading = displayData.heading;
            
            // Update tape position
            if (tapeContent != null)
            {
                float xOffset = -heading * pixelsPerDegree;
                tapeContent.anchoredPosition = new Vector2(xOffset, tapeContent.anchoredPosition.y);
            }

            // Update numeric display
            if (headingValueText != null)
            {
                int displayHeading = Mathf.RoundToInt(heading);
                displayHeading = ((displayHeading % 360) + 360) % 360;
                headingValueText.text = displayHeading.ToString("000");
            }
        }

        protected override void ApplyColors()
        {
            if (config == null) return;

            if (headingValueText != null)
                headingValueText.color = config.textColor;

            if (headingBox != null)
                headingBox.color = config.primaryColor;

            if (headingPointer != null)
                headingPointer.color = config.primaryColor;
        }

        /// <summary>
        /// Create the heading tape panel structure
        /// </summary>
        public static HeadingTapePanel CreatePanel(Transform parent, AviationUIConfig config)
        {
            // Create panel root
            GameObject panelObj = new GameObject("HeadingTapePanel");
            panelObj.transform.SetParent(parent, false);
            
            RectTransform panelRect = panelObj.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0, 1);
            panelRect.anchorMax = new Vector2(1, 1);
            panelRect.pivot = new Vector2(0.5f, 1);
            panelRect.anchoredPosition = new Vector2(0, -20);
            panelRect.sizeDelta = new Vector2(0, 60);

            // Add canvas group
            panelObj.AddComponent<CanvasGroup>();

            // Create mask for tape
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
            tapeRect.anchorMin = new Vector2(0.5f, 0.5f);
            tapeRect.anchorMax = new Vector2(0.5f, 0.5f);
            tapeRect.sizeDelta = new Vector2(2000, 60);

            // Create heading box
            GameObject boxObj = new GameObject("HeadingBox");
            boxObj.transform.SetParent(panelObj.transform, false);
            
            RectTransform boxRect = boxObj.AddComponent<RectTransform>();
            boxRect.anchorMin = new Vector2(0.5f, 0.5f);
            boxRect.anchorMax = new Vector2(0.5f, 0.5f);
            boxRect.sizeDelta = new Vector2(80, 40);
            boxRect.anchoredPosition = new Vector2(0, -40);
            
            Image boxImage = boxObj.AddComponent<Image>();
            boxImage.color = config != null ? config.primaryColor : Color.green;

            // Create heading text
            GameObject textObj = new GameObject("HeadingValue");
            textObj.transform.SetParent(boxObj.transform, false);
            
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;
            
            Text headingText = textObj.AddComponent<Text>();
            headingText.text = "000";
            headingText.alignment = TextAnchor.MiddleCenter;
            headingText.fontSize = config != null ? config.primaryFontSize / 2 : 36;
            headingText.color = config != null ? config.textColor : Color.white;
            headingText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // Create pointer
            GameObject pointerObj = new GameObject("Pointer");
            pointerObj.transform.SetParent(panelObj.transform, false);
            
            RectTransform pointerRect = pointerObj.AddComponent<RectTransform>();
            pointerRect.anchorMin = new Vector2(0.5f, 1);
            pointerRect.anchorMax = new Vector2(0.5f, 1);
            pointerRect.sizeDelta = new Vector2(20, 20);
            pointerRect.anchoredPosition = new Vector2(0, -10);
            
            Image pointerImage = pointerObj.AddComponent<Image>();
            pointerImage.color = config != null ? config.primaryColor : Color.green;

            // Add panel component
            HeadingTapePanel panel = panelObj.AddComponent<HeadingTapePanel>();
            panel.tapeContent = tapeRect;
            panel.headingValueText = headingText;
            panel.headingBox = boxImage;
            panel.headingPointer = pointerImage;
            panel.config = config;

            return panel;
        }
    }
}
