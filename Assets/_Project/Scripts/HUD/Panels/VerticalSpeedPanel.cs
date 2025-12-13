using UnityEngine;
using UnityEngine.UI;

namespace AviationUI.Panels
{
    /// <summary>
    /// Vertical speed indicator panel with bar and arrow pointer.
    /// Displays rate of climb/descent in feet per minute.
    /// </summary>
    public class VerticalSpeedPanel : AviationUIPanel
    {
        [Header("VS Indicator References")]
        [SerializeField] private RectTransform vsBar;
        [SerializeField] private RectTransform vsPointer;
        [SerializeField] private Text vsValueText;
        [SerializeField] private Image vsBox;
        [SerializeField] private Image barImage;
        [SerializeField] private Image pointerImage;

        [Header("VS Settings")]
        [SerializeField] private float maxDisplayVS = 6000f; // Max FPM to display
        [SerializeField] private float barMaxHeight = 100f;
        [SerializeField] private float pointerMaxOffset = 80f;

        public override string PanelId => "VerticalSpeed";
        public override Vector2 DefaultAnchorPosition => new Vector2(0.75f, 0.5f);

        protected override void InitializePanel()
        {
            ApplyColors();
        }

        protected override void UpdateDisplay()
        {
            if (displayData == null) return;

            float vs = displayData.verticalSpeed;

            // Clamp and normalize vertical speed
            float normalizedVS = Mathf.Clamp(vs, -maxDisplayVS, maxDisplayVS) / maxDisplayVS;
            float absNormalized = Mathf.Abs(normalizedVS);

            // Update bar scale (grows from center)
            if (vsBar != null)
            {
                float barHeight = absNormalized * barMaxHeight;
                vsBar.sizeDelta = new Vector2(vsBar.sizeDelta.x, barHeight);
                
                // Flip bar direction based on climb/descent
                if (vs >= 0)
                {
                    vsBar.pivot = new Vector2(0.5f, 0);
                    vsBar.anchoredPosition = new Vector2(vsBar.anchoredPosition.x, 0);
                }
                else
                {
                    vsBar.pivot = new Vector2(0.5f, 1);
                    vsBar.anchoredPosition = new Vector2(vsBar.anchoredPosition.x, 0);
                }
            }

            // Update pointer position
            if (vsPointer != null)
            {
                float pointerY = normalizedVS * pointerMaxOffset;
                vsPointer.anchoredPosition = new Vector2(vsPointer.anchoredPosition.x, pointerY);
                
                // Flip pointer direction
                vsPointer.localScale = new Vector3(1, vs >= 0 ? 1 : -1, 1);
            }

            // Update numeric display
            if (vsValueText != null)
            {
                int displayVS = Mathf.RoundToInt(vs);
                string prefix = displayVS > 0 ? "+" : "";
                vsValueText.text = prefix + displayVS.ToString();
            }

            // Update colors based on thresholds
            if (config != null)
            {
                Color thresholdColor = GetThresholdColor(
                    Mathf.Abs(vs),
                    config.verticalSpeedWarningThreshold,
                    config.verticalSpeedDangerThreshold
                );

                if (vsValueText != null)
                    vsValueText.color = thresholdColor;

                if (vsBox != null)
                    vsBox.color = thresholdColor;

                if (barImage != null)
                    barImage.color = thresholdColor;

                if (pointerImage != null)
                    pointerImage.color = thresholdColor;
            }
        }

        protected override void ApplyColors()
        {
            if (config == null) return;

            if (vsValueText != null)
                vsValueText.color = config.primaryColor;

            if (vsBox != null)
                vsBox.color = config.primaryColor;

            if (barImage != null)
                barImage.color = config.primaryColor;

            if (pointerImage != null)
                pointerImage.color = config.primaryColor;
        }

        /// <summary>
        /// Create the vertical speed panel structure
        /// </summary>
        public static VerticalSpeedPanel CreatePanel(Transform parent, AviationUIConfig config)
        {
            // Create panel root
            GameObject panelObj = new GameObject("VerticalSpeedPanel");
            panelObj.transform.SetParent(parent, false);
            
            RectTransform panelRect = panelObj.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.72f, 0.5f);
            panelRect.anchorMax = new Vector2(0.72f, 0.5f);
            panelRect.sizeDelta = new Vector2(60, 200);

            panelObj.AddComponent<CanvasGroup>();

            // Create scale background
            GameObject scaleObj = new GameObject("Scale");
            scaleObj.transform.SetParent(panelObj.transform, false);
            
            RectTransform scaleRect = scaleObj.AddComponent<RectTransform>();
            scaleRect.anchorMin = Vector2.zero;
            scaleRect.anchorMax = Vector2.one;
            scaleRect.sizeDelta = Vector2.zero;

            // Create scale marks
            CreateScaleMarks(scaleObj.transform, config);

            // Create bar
            GameObject barObj = new GameObject("VSBar");
            barObj.transform.SetParent(panelObj.transform, false);
            
            RectTransform barRect = barObj.AddComponent<RectTransform>();
            barRect.anchorMin = new Vector2(0.3f, 0.5f);
            barRect.anchorMax = new Vector2(0.3f, 0.5f);
            barRect.pivot = new Vector2(0.5f, 0);
            barRect.sizeDelta = new Vector2(15, 0);
            
            Image barImg = barObj.AddComponent<Image>();
            barImg.color = config != null ? config.primaryColor : Color.green;

            // Create pointer
            GameObject pointerObj = new GameObject("VSPointer");
            pointerObj.transform.SetParent(panelObj.transform, false);
            
            RectTransform pointerRect = pointerObj.AddComponent<RectTransform>();
            pointerRect.anchorMin = new Vector2(0.5f, 0.5f);
            pointerRect.anchorMax = new Vector2(0.5f, 0.5f);
            pointerRect.sizeDelta = new Vector2(20, 15);
            
            Image pointerImg = pointerObj.AddComponent<Image>();
            pointerImg.color = config != null ? config.primaryColor : Color.green;

            // Create VS box
            GameObject boxObj = new GameObject("VSBox");
            boxObj.transform.SetParent(panelObj.transform, false);
            
            RectTransform boxRect = boxObj.AddComponent<RectTransform>();
            boxRect.anchorMin = new Vector2(0.5f, 0);
            boxRect.anchorMax = new Vector2(0.5f, 0);
            boxRect.pivot = new Vector2(0.5f, 1);
            boxRect.anchoredPosition = new Vector2(0, -10);
            boxRect.sizeDelta = new Vector2(60, 30);
            
            Image boxImage = boxObj.AddComponent<Image>();
            boxImage.color = config != null ? config.primaryColor : Color.green;

            // Create VS text
            GameObject textObj = new GameObject("VSValue");
            textObj.transform.SetParent(boxObj.transform, false);
            
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;
            
            Text vsText = textObj.AddComponent<Text>();
            vsText.text = "0";
            vsText.alignment = TextAnchor.MiddleCenter;
            vsText.fontSize = config != null ? config.labelFontSize : 24;
            vsText.color = config != null ? config.textColor : Color.white;
            vsText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // Add panel component
            VerticalSpeedPanel panel = panelObj.AddComponent<VerticalSpeedPanel>();
            panel.vsBar = barRect;
            panel.vsPointer = pointerRect;
            panel.vsValueText = vsText;
            panel.vsBox = boxImage;
            panel.barImage = barImg;
            panel.pointerImage = pointerImg;
            panel.config = config;

            return panel;
        }

        private static void CreateScaleMarks(Transform parent, AviationUIConfig config)
        {
            // Create marks at 0, 1000, 2000, 4000, 6000 positions
            float[] positions = { 0, 0.167f, 0.333f, 0.667f, 1f };
            string[] labels = { "0", "1", "2", "4", "6" };

            for (int i = 0; i < positions.Length; i++)
            {
                // Positive marks
                CreateMark(parent, positions[i], labels[i], true, config);
                
                // Negative marks (skip 0)
                if (i > 0)
                {
                    CreateMark(parent, positions[i], labels[i], false, config);
                }
            }
        }

        private static void CreateMark(Transform parent, float normalizedPos, string label, bool positive, AviationUIConfig config)
        {
            float yPos = positive ? normalizedPos * 80 : -normalizedPos * 80;

            // Create tick
            GameObject tickObj = new GameObject("Tick_" + label + (positive ? "+" : "-"));
            tickObj.transform.SetParent(parent, false);
            
            RectTransform tickRect = tickObj.AddComponent<RectTransform>();
            tickRect.anchorMin = new Vector2(0, 0.5f);
            tickRect.anchorMax = new Vector2(0, 0.5f);
            tickRect.anchoredPosition = new Vector2(10, yPos);
            tickRect.sizeDelta = new Vector2(15, 2);
            
            Image tickImage = tickObj.AddComponent<Image>();
            tickImage.color = config != null ? config.primaryColor : Color.green;

            // Create label
            GameObject labelObj = new GameObject("Label");
            labelObj.transform.SetParent(parent, false);
            
            RectTransform labelRect = labelObj.AddComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(1, 0.5f);
            labelRect.anchorMax = new Vector2(1, 0.5f);
            labelRect.anchoredPosition = new Vector2(-5, yPos);
            labelRect.sizeDelta = new Vector2(20, 20);
            
            Text labelText = labelObj.AddComponent<Text>();
            labelText.text = label;
            labelText.alignment = TextAnchor.MiddleRight;
            labelText.fontSize = 16;
            labelText.color = config != null ? config.textColor : Color.white;
            labelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
    }
}
