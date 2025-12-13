using UnityEngine;
using UnityEngine.UI;

namespace AviationUI.Panels
{
    /// <summary>
    /// Large compass rose display on the right side of the screen.
    /// Shows heading with rotating compass dial.
    /// </summary>
    public class CompassRosePanel : AviationUIPanel
    {
        [Header("Compass References")]
        [SerializeField] private RectTransform compassDial;
        [SerializeField] private Text headingText;
        [SerializeField] private Image compassImage;
        [SerializeField] private Image headingBug;
        [SerializeField] private RectTransform aircraftIcon;

        [Header("Compass Settings")]
        [SerializeField] private float headingBugTarget = 0f;
        [SerializeField] private bool showHeadingBug = true;

        public override string PanelId => "CompassRose";
        public override Vector2 DefaultAnchorPosition => new Vector2(0.9f, 0.35f);

        /// <summary>
        /// Set the heading bug target
        /// </summary>
        public void SetHeadingBug(float heading)
        {
            headingBugTarget = heading;
        }

        protected override void InitializePanel()
        {
            ApplyColors();
        }

        protected override void UpdateDisplay()
        {
            if (displayData == null) return;

            float heading = displayData.heading;

            // Rotate compass dial (opposite of heading so it appears fixed)
            if (compassDial != null)
            {
                compassDial.localRotation = Quaternion.Euler(0, 0, heading);
            }

            // Update heading text
            if (headingText != null)
            {
                int displayHeading = Mathf.RoundToInt(heading);
                displayHeading = ((displayHeading % 360) + 360) % 360;
                headingText.text = displayHeading.ToString("000") + "°";
            }

            // Update heading bug position
            if (headingBug != null && showHeadingBug)
            {
                float bugAngle = headingBugTarget - heading;
                headingBug.rectTransform.localRotation = Quaternion.Euler(0, 0, -bugAngle);
            }
        }

        protected override void ApplyColors()
        {
            if (config == null) return;

            if (compassImage != null)
                compassImage.color = config.primaryColor;

            if (headingText != null)
                headingText.color = config.textColor;

            if (headingBug != null)
                headingBug.color = config.warningColor;
        }

        /// <summary>
        /// Create the compass rose panel structure
        /// </summary>
        public static CompassRosePanel CreatePanel(Transform parent, AviationUIConfig config)
        {
            // Create panel root
            GameObject panelObj = new GameObject("CompassRosePanel");
            panelObj.transform.SetParent(parent, false);
            
            RectTransform panelRect = panelObj.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(1, 0.3f);
            panelRect.anchorMax = new Vector2(1, 0.3f);
            panelRect.pivot = new Vector2(1, 0.5f);
            panelRect.anchoredPosition = new Vector2(-30, 0);
            panelRect.sizeDelta = new Vector2(200, 200);

            panelObj.AddComponent<CanvasGroup>();

            // Create compass background
            GameObject bgObj = new GameObject("CompassBG");
            bgObj.transform.SetParent(panelObj.transform, false);
            
            RectTransform bgRect = bgObj.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;
            
            Image bgImage = bgObj.AddComponent<Image>();
            bgImage.color = new Color(0, 0, 0, 0.3f);

            // Create rotating dial
            GameObject dialObj = new GameObject("CompassDial");
            dialObj.transform.SetParent(panelObj.transform, false);
            
            RectTransform dialRect = dialObj.AddComponent<RectTransform>();
            dialRect.anchorMin = new Vector2(0.5f, 0.5f);
            dialRect.anchorMax = new Vector2(0.5f, 0.5f);
            dialRect.sizeDelta = new Vector2(180, 180);

            // Create dial marks and labels
            CreateCompassMarks(dialObj.transform, config);

            // Create center aircraft icon (static)
            GameObject aircraftObj = new GameObject("AircraftIcon");
            aircraftObj.transform.SetParent(panelObj.transform, false);
            
            RectTransform aircraftRect = aircraftObj.AddComponent<RectTransform>();
            aircraftRect.anchorMin = new Vector2(0.5f, 0.5f);
            aircraftRect.anchorMax = new Vector2(0.5f, 0.5f);
            aircraftRect.sizeDelta = new Vector2(30, 30);
            
            Image aircraftImage = aircraftObj.AddComponent<Image>();
            aircraftImage.color = config != null ? config.primaryColor : Color.green;

            // Create heading text at top
            GameObject textObj = new GameObject("HeadingText");
            textObj.transform.SetParent(panelObj.transform, false);
            
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0.5f, 1);
            textRect.anchorMax = new Vector2(0.5f, 1);
            textRect.pivot = new Vector2(0.5f, 0);
            textRect.anchoredPosition = new Vector2(0, 5);
            textRect.sizeDelta = new Vector2(60, 25);
            
            Text headingText = textObj.AddComponent<Text>();
            headingText.text = "000°";
            headingText.alignment = TextAnchor.MiddleCenter;
            headingText.fontSize = config != null ? config.labelFontSize : 24;
            headingText.color = config != null ? config.textColor : Color.white;
            headingText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // Add heading bug
            GameObject bugObj = new GameObject("HeadingBug");
            bugObj.transform.SetParent(dialObj.transform, false);
            
            RectTransform bugRect = bugObj.AddComponent<RectTransform>();
            bugRect.anchorMin = new Vector2(0.5f, 0.5f);
            bugRect.anchorMax = new Vector2(0.5f, 0.5f);
            bugRect.anchoredPosition = new Vector2(0, 85);
            bugRect.sizeDelta = new Vector2(15, 10);
            
            Image bugImage = bugObj.AddComponent<Image>();
            bugImage.color = config != null ? config.warningColor : Color.yellow;

            // Add panel component
            CompassRosePanel panel = panelObj.AddComponent<CompassRosePanel>();
            panel.compassDial = dialRect;
            panel.headingText = headingText;
            panel.compassImage = bgImage;
            panel.headingBug = bugImage;
            panel.aircraftIcon = aircraftRect;
            panel.config = config;

            return panel;
        }

        private static void CreateCompassMarks(Transform parent, AviationUIConfig config)
        {
            string[] cardinals = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
            
            for (int i = 0; i < 360; i += 5)
            {
                float angle = -i; // Negative for correct rotation direction
                float radians = angle * Mathf.Deg2Rad;
                
                bool isCardinal = i % 45 == 0;
                bool isMajor = i % 30 == 0;
                
                float radius = 80f;
                float tickLength = isCardinal ? 15f : (isMajor ? 10f : 5f);
                
                // Create tick mark
                GameObject tickObj = new GameObject("Tick_" + i);
                tickObj.transform.SetParent(parent, false);
                
                RectTransform tickRect = tickObj.AddComponent<RectTransform>();
                float x = Mathf.Sin(radians) * (radius - tickLength / 2);
                float y = Mathf.Cos(radians) * (radius - tickLength / 2);
                tickRect.anchoredPosition = new Vector2(x, y);
                tickRect.sizeDelta = new Vector2(2, tickLength);
                tickRect.localRotation = Quaternion.Euler(0, 0, angle);
                
                Image tickImage = tickObj.AddComponent<Image>();
                tickImage.color = config != null ? config.primaryColor : Color.green;

                // Create cardinal labels
                if (isCardinal)
                {
                    int cardinalIndex = i / 45;
                    
                    GameObject labelObj = new GameObject("Label_" + cardinals[cardinalIndex]);
                    labelObj.transform.SetParent(parent, false);
                    
                    RectTransform labelRect = labelObj.AddComponent<RectTransform>();
                    float labelRadius = radius - 25f;
                    float lx = Mathf.Sin(radians) * labelRadius;
                    float ly = Mathf.Cos(radians) * labelRadius;
                    labelRect.anchoredPosition = new Vector2(lx, ly);
                    labelRect.sizeDelta = new Vector2(30, 20);
                    
                    Text labelText = labelObj.AddComponent<Text>();
                    labelText.text = cardinals[cardinalIndex];
                    labelText.alignment = TextAnchor.MiddleCenter;
                    labelText.fontSize = 16;
                    labelText.color = config != null ? config.textColor : Color.white;
                    labelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                }
            }
        }
    }
}
