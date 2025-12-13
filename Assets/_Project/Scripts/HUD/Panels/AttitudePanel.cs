using UnityEngine;
using UnityEngine.UI;

namespace AviationUI.Panels
{
    /// <summary>
    /// Attitude indicator panel showing pitch ladder and bank angle.
    /// Center of the display.
    /// </summary>
    public class AttitudePanel : AviationUIPanel
    {
        [Header("Attitude References")]
        [SerializeField] private RectTransform pitchLadder;
        [SerializeField] private RectTransform bankIndicator;
        [SerializeField] private RectTransform horizonLine;
        [SerializeField] private Image skyBackground;
        [SerializeField] private Image groundBackground;
        [SerializeField] private RectTransform flightPathMarker;
        [SerializeField] private RectTransform aircraftSymbol;

        [Header("Attitude Settings")]
        [SerializeField] private float pitchScale = 5f; // Pixels per degree
        [SerializeField] private float maxPitchDisplay = 30f;
        [SerializeField] private bool showFlightPath = true;

        [Header("Pitch Ladder")]
        [SerializeField] private GameObject pitchLinePrefab;
        [SerializeField] private int pitchLineSpacing = 5; // Degrees between lines

        public override string PanelId => "Attitude";
        public override Vector2 DefaultAnchorPosition => new Vector2(0.5f, 0.5f);

        protected override void InitializePanel()
        {
            if (pitchLadder != null && pitchLinePrefab != null)
            {
                GeneratePitchLadder();
            }

            ApplyColors();
        }

        private void GeneratePitchLadder()
        {
            // Clear existing
            foreach (Transform child in pitchLadder)
            {
                if (Application.isPlaying)
                    Destroy(child.gameObject);
                else
                    DestroyImmediate(child.gameObject);
            }

            // Generate pitch lines from -90 to +90
            for (int pitch = -90; pitch <= 90; pitch += pitchLineSpacing)
            {
                if (pitch == 0) continue; // Skip horizon line (handled separately)

                float yPos = pitch * pitchScale;
                
                GameObject line = Instantiate(pitchLinePrefab, pitchLadder);
                RectTransform lineRect = line.GetComponent<RectTransform>();
                lineRect.anchoredPosition = new Vector2(0, yPos);
                
                // Different line lengths based on pitch value
                float lineWidth = Mathf.Abs(pitch) % 10 == 0 ? 100 : 50;
                lineRect.sizeDelta = new Vector2(lineWidth, 2);

                // Add text labels for major pitches
                if (Mathf.Abs(pitch) % 10 == 0)
                {
                    // Left label
                    CreatePitchLabel(line.transform, pitch, -lineWidth / 2 - 30);
                    // Right label
                    CreatePitchLabel(line.transform, pitch, lineWidth / 2 + 30);
                }
            }
        }

        private void CreatePitchLabel(Transform parent, int pitch, float xOffset)
        {
            GameObject labelObj = new GameObject("Label");
            labelObj.transform.SetParent(parent, false);
            
            RectTransform labelRect = labelObj.AddComponent<RectTransform>();
            labelRect.anchoredPosition = new Vector2(xOffset, 0);
            labelRect.sizeDelta = new Vector2(40, 20);
            
            Text label = labelObj.AddComponent<Text>();
            label.text = pitch.ToString();
            label.alignment = TextAnchor.MiddleCenter;
            label.fontSize = config != null ? config.labelFontSize : 24;
            label.color = config != null ? config.textColor : Color.white;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        protected override void UpdateDisplay()
        {
            if (displayData == null) return;

            float pitch = displayData.pitch;
            float roll = displayData.roll;

            // Update pitch ladder position
            if (pitchLadder != null)
            {
                float yOffset = -pitch * pitchScale;
                pitchLadder.anchoredPosition = new Vector2(0, yOffset);
            }

            // Update bank/roll indicator
            if (bankIndicator != null)
            {
                bankIndicator.localRotation = Quaternion.Euler(0, 0, roll);
            }

            // Update horizon line
            if (horizonLine != null)
            {
                float yOffset = -pitch * pitchScale;
                horizonLine.anchoredPosition = new Vector2(0, yOffset);
                horizonLine.localRotation = Quaternion.Euler(0, 0, roll);
            }

            // Update flight path marker
            if (showFlightPath && flightPathMarker != null)
            {
                float fpa = displayData.flightPathAngle;
                float yOffset = fpa * pitchScale;
                flightPathMarker.anchoredPosition = new Vector2(0, yOffset);
            }
        }

        protected override void ApplyColors()
        {
            if (config == null) return;

            if (skyBackground != null)
                skyBackground.color = new Color(0.2f, 0.4f, 0.8f, 0.5f);

            if (groundBackground != null)
                groundBackground.color = new Color(0.4f, 0.25f, 0.1f, 0.5f);

            if (horizonLine != null)
            {
                Image lineImage = horizonLine.GetComponent<Image>();
                if (lineImage != null)
                    lineImage.color = config.primaryColor;
            }
        }

        /// <summary>
        /// Create the attitude panel structure
        /// </summary>
        public static AttitudePanel CreatePanel(Transform parent, AviationUIConfig config)
        {
            // Create panel root
            GameObject panelObj = new GameObject("AttitudePanel");
            panelObj.transform.SetParent(parent, false);
            
            RectTransform panelRect = panelObj.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(400, 300);

            panelObj.AddComponent<CanvasGroup>();

            // Create mask
            GameObject maskObj = new GameObject("AttitudeMask");
            maskObj.transform.SetParent(panelObj.transform, false);
            
            RectTransform maskRect = maskObj.AddComponent<RectTransform>();
            maskRect.anchorMin = Vector2.zero;
            maskRect.anchorMax = Vector2.one;
            maskRect.sizeDelta = Vector2.zero;
            
            Image maskImage = maskObj.AddComponent<Image>();
            maskImage.color = new Color(0, 0, 0, 0);
            
            Mask mask = maskObj.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            // Create bank indicator (rotates with roll)
            GameObject bankObj = new GameObject("BankIndicator");
            bankObj.transform.SetParent(maskObj.transform, false);
            
            RectTransform bankRect = bankObj.AddComponent<RectTransform>();
            bankRect.anchorMin = new Vector2(0.5f, 0.5f);
            bankRect.anchorMax = new Vector2(0.5f, 0.5f);
            bankRect.sizeDelta = new Vector2(500, 500);

            // Create pitch ladder
            GameObject ladderObj = new GameObject("PitchLadder");
            ladderObj.transform.SetParent(bankObj.transform, false);
            
            RectTransform ladderRect = ladderObj.AddComponent<RectTransform>();
            ladderRect.anchorMin = new Vector2(0.5f, 0.5f);
            ladderRect.anchorMax = new Vector2(0.5f, 0.5f);
            ladderRect.sizeDelta = new Vector2(300, 1000);

            // Create horizon line
            GameObject horizonObj = new GameObject("HorizonLine");
            horizonObj.transform.SetParent(bankObj.transform, false);
            
            RectTransform horizonRect = horizonObj.AddComponent<RectTransform>();
            horizonRect.anchorMin = new Vector2(0.5f, 0.5f);
            horizonRect.anchorMax = new Vector2(0.5f, 0.5f);
            horizonRect.sizeDelta = new Vector2(200, 3);
            
            Image horizonImage = horizonObj.AddComponent<Image>();
            horizonImage.color = config != null ? config.primaryColor : Color.green;

            // Create aircraft symbol (static)
            GameObject aircraftObj = new GameObject("AircraftSymbol");
            aircraftObj.transform.SetParent(panelObj.transform, false);
            
            RectTransform aircraftRect = aircraftObj.AddComponent<RectTransform>();
            aircraftRect.anchorMin = new Vector2(0.5f, 0.5f);
            aircraftRect.anchorMax = new Vector2(0.5f, 0.5f);
            aircraftRect.sizeDelta = new Vector2(100, 20);
            
            Image aircraftImage = aircraftObj.AddComponent<Image>();
            aircraftImage.color = config != null ? config.primaryColor : Color.green;

            // Add panel component
            AttitudePanel panel = panelObj.AddComponent<AttitudePanel>();
            panel.pitchLadder = ladderRect;
            panel.bankIndicator = bankRect;
            panel.horizonLine = horizonRect;
            panel.aircraftSymbol = aircraftRect;
            panel.config = config;

            return panel;
        }
    }
}
