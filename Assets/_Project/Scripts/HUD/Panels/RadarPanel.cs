using UnityEngine;
using UnityEngine.UI;

namespace AviationUI.Panels
{
    /// <summary>
    /// Weather radar/map display panel in the bottom left corner.
    /// Shows radar overlay with range rings and heading indicator.
    /// </summary>
    public class RadarPanel : AviationUIPanel
    {
        [Header("Radar References")]
        [SerializeField] private RawImage radarDisplay;
        [SerializeField] private RectTransform radarSweep;
        [SerializeField] private RectTransform aircraftIcon;
        [SerializeField] private RectTransform rangeRings;
        [SerializeField] private Text rangeText;

        [Header("Radar Settings")]
        [SerializeField] private float radarRange = 10f; // Nautical miles
        [SerializeField] private float sweepSpeed = 30f; // Degrees per second
        [SerializeField] private bool enableSweep = true;
        [SerializeField] private RenderTexture radarTexture;

        [Header("Map Options")]
        [SerializeField] private bool showTerrain = true;
        [SerializeField] private bool showWeather = true;
        [SerializeField] private bool showTraffic = true;

        private float sweepAngle = 0f;

        public override string PanelId => "Radar";
        public override Vector2 DefaultAnchorPosition => new Vector2(0.12f, 0.25f);

        /// <summary>
        /// Set the radar range in nautical miles
        /// </summary>
        public void SetRange(float nm)
        {
            radarRange = nm;
            if (rangeText != null)
            {
                rangeText.text = radarRange.ToString("F0") + "nm";
            }
        }

        protected override void InitializePanel()
        {
            if (rangeText != null)
            {
                rangeText.text = radarRange.ToString("F0") + "nm";
            }

            ApplyColors();
        }

        protected override void Update()
        {
            base.Update();

            // Animate radar sweep
            if (enableSweep && radarSweep != null)
            {
                sweepAngle += sweepSpeed * Time.deltaTime;
                if (sweepAngle >= 360f) sweepAngle -= 360f;
                radarSweep.localRotation = Quaternion.Euler(0, 0, -sweepAngle);
            }
        }

        protected override void UpdateDisplay()
        {
            if (displayData == null) return;

            // Rotate aircraft icon with heading
            if (aircraftIcon != null)
            {
                // Aircraft icon points up, rotate with heading
                aircraftIcon.localRotation = Quaternion.Euler(0, 0, 0);
            }

            // Range rings rotate opposite to heading (north-up display)
            if (rangeRings != null)
            {
                rangeRings.localRotation = Quaternion.Euler(0, 0, displayData.heading);
            }
        }

        protected override void ApplyColors()
        {
            if (config == null) return;

            if (rangeText != null)
                rangeText.color = config.textColor;
        }

        /// <summary>
        /// Set the radar render texture
        /// </summary>
        public void SetRadarTexture(RenderTexture texture)
        {
            radarTexture = texture;
            if (radarDisplay != null)
            {
                radarDisplay.texture = radarTexture;
            }
        }

        /// <summary>
        /// Create the radar panel structure
        /// </summary>
        public static RadarPanel CreatePanel(Transform parent, AviationUIConfig config)
        {
            // Create panel root
            GameObject panelObj = new GameObject("RadarPanel");
            panelObj.transform.SetParent(parent, false);
            
            RectTransform panelRect = panelObj.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0, 0);
            panelRect.anchorMax = new Vector2(0, 0);
            panelRect.pivot = new Vector2(0, 0);
            panelRect.anchoredPosition = new Vector2(30, 30);
            panelRect.sizeDelta = new Vector2(200, 200);

            panelObj.AddComponent<CanvasGroup>();

            // Create radar background
            GameObject bgObj = new GameObject("RadarBG");
            bgObj.transform.SetParent(panelObj.transform, false);
            
            RectTransform bgRect = bgObj.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;
            
            Image bgImage = bgObj.AddComponent<Image>();
            bgImage.color = new Color(0, 0.1f, 0, 0.7f);

            // Create range rings
            GameObject ringsObj = new GameObject("RangeRings");
            ringsObj.transform.SetParent(panelObj.transform, false);
            
            RectTransform ringsRect = ringsObj.AddComponent<RectTransform>();
            ringsRect.anchorMin = new Vector2(0.5f, 0.5f);
            ringsRect.anchorMax = new Vector2(0.5f, 0.5f);
            ringsRect.sizeDelta = new Vector2(180, 180);

            // Create concentric range rings
            CreateRangeRings(ringsObj.transform, config);

            // Create radar display area
            GameObject displayObj = new GameObject("RadarDisplay");
            displayObj.transform.SetParent(panelObj.transform, false);
            
            RectTransform displayRect = displayObj.AddComponent<RectTransform>();
            displayRect.anchorMin = new Vector2(0.05f, 0.05f);
            displayRect.anchorMax = new Vector2(0.95f, 0.95f);
            displayRect.sizeDelta = Vector2.zero;
            
            RawImage radarImg = displayObj.AddComponent<RawImage>();
            radarImg.color = Color.white;

            // Create sweep line
            GameObject sweepObj = new GameObject("RadarSweep");
            sweepObj.transform.SetParent(panelObj.transform, false);
            
            RectTransform sweepRect = sweepObj.AddComponent<RectTransform>();
            sweepRect.anchorMin = new Vector2(0.5f, 0.5f);
            sweepRect.anchorMax = new Vector2(0.5f, 0.5f);
            sweepRect.pivot = new Vector2(0.5f, 0);
            sweepRect.anchoredPosition = Vector2.zero;
            sweepRect.sizeDelta = new Vector2(3, 90);
            
            Image sweepImage = sweepObj.AddComponent<Image>();
            sweepImage.color = new Color(0, 1, 0, 0.5f);

            // Create aircraft icon at center
            GameObject aircraftObj = new GameObject("AircraftIcon");
            aircraftObj.transform.SetParent(panelObj.transform, false);
            
            RectTransform aircraftRect = aircraftObj.AddComponent<RectTransform>();
            aircraftRect.anchorMin = new Vector2(0.5f, 0.5f);
            aircraftRect.anchorMax = new Vector2(0.5f, 0.5f);
            aircraftRect.sizeDelta = new Vector2(20, 20);
            
            Image aircraftImage = aircraftObj.AddComponent<Image>();
            aircraftImage.color = config != null ? config.primaryColor : Color.green;

            // Create range label
            GameObject rangeObj = new GameObject("RangeLabel");
            rangeObj.transform.SetParent(panelObj.transform, false);
            
            RectTransform rangeRect = rangeObj.AddComponent<RectTransform>();
            rangeRect.anchorMin = new Vector2(0, 1);
            rangeRect.anchorMax = new Vector2(0, 1);
            rangeRect.pivot = new Vector2(0, 1);
            rangeRect.anchoredPosition = new Vector2(5, -5);
            rangeRect.sizeDelta = new Vector2(50, 20);
            
            Text rangeText = rangeObj.AddComponent<Text>();
            rangeText.text = "10nm";
            rangeText.alignment = TextAnchor.UpperLeft;
            rangeText.fontSize = 14;
            rangeText.color = config != null ? config.textColor : Color.white;
            rangeText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // Add panel component
            RadarPanel panel = panelObj.AddComponent<RadarPanel>();
            panel.radarDisplay = radarImg;
            panel.radarSweep = sweepRect;
            panel.aircraftIcon = aircraftRect;
            panel.rangeRings = ringsRect;
            panel.rangeText = rangeText;
            panel.config = config;

            return panel;
        }

        private static void CreateRangeRings(Transform parent, AviationUIConfig config)
        {
            float[] radii = { 30f, 60f, 90f };
            
            foreach (float radius in radii)
            {
                GameObject ringObj = new GameObject("Ring_" + radius);
                ringObj.transform.SetParent(parent, false);
                
                RectTransform ringRect = ringObj.AddComponent<RectTransform>();
                ringRect.anchorMin = new Vector2(0.5f, 0.5f);
                ringRect.anchorMax = new Vector2(0.5f, 0.5f);
                ringRect.sizeDelta = new Vector2(radius * 2, radius * 2);
                
                Image ringImage = ringObj.AddComponent<Image>();
                ringImage.color = new Color(0, 0.5f, 0, 0.5f);
                ringImage.type = Image.Type.Simple;
                
                // Create compass lines at N, E, S, W
                for (int i = 0; i < 4; i++)
                {
                    float angle = i * 90f;
                    
                    GameObject lineObj = new GameObject("Line_" + angle);
                    lineObj.transform.SetParent(ringObj.transform, false);
                    
                    RectTransform lineRect = lineObj.AddComponent<RectTransform>();
                    lineRect.anchorMin = new Vector2(0.5f, 0.5f);
                    lineRect.anchorMax = new Vector2(0.5f, 0.5f);
                    lineRect.pivot = new Vector2(0.5f, 0);
                    lineRect.sizeDelta = new Vector2(1, radius);
                    lineRect.localRotation = Quaternion.Euler(0, 0, angle);
                    
                    Image lineImage = lineObj.AddComponent<Image>();
                    lineImage.color = new Color(0, 0.5f, 0, 0.3f);
                }
            }
        }
    }
}
