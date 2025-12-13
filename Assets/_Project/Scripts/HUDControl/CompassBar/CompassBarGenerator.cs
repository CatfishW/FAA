using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

namespace HUDControl.CompassBar
{
    /// <summary>
    /// Procedural generator for compass bar tape.
    /// Creates tick marks and labels (N, E, S, W, and degree numbers) dynamically.
    /// Generates a seamlessly-wrapping 360° tape.
    /// </summary>
    [ExecuteInEditMode]
    [RequireComponent(typeof(RectTransform))]
    public class CompassBarGenerator : MonoBehaviour
    {
        #region Inspector - Generation Settings
        
        [Header("Tape Dimensions")]
        [Tooltip("Pixels per degree (determines tape width: 360 * this value)")]
        [SerializeField] private float pixelsPerDegree = 4f;
        
        [Tooltip("Height of the tape in pixels")]
        [SerializeField] private float tapeHeight = 40f;
        
        [Tooltip("Number of times to repeat the 360° tape (3 = seamless infinite scroll)")]
        [Range(1, 5)]
        [SerializeField] private int repeatCopies = 3;
        
        #endregion
        
        #region Inspector - Tick Settings
        
        [Header("Major Ticks (Every 10°)")]
        [Tooltip("Height of major tick marks")]
        [SerializeField] private float majorTickHeight = 20f;
        
        [Tooltip("Width of major tick marks")]
        [SerializeField] private float majorTickWidth = 2f;
        
        [Tooltip("Color of major tick marks")]
        [SerializeField] private Color majorTickColor = Color.white;
        
        [Header("Minor Ticks (Every 5°)")]
        [Tooltip("Enable minor tick marks between major ticks")]
        [SerializeField] private bool showMinorTicks = true;
        
        [Tooltip("Height of minor tick marks")]
        [SerializeField] private float minorTickHeight = 10f;
        
        [Tooltip("Width of minor tick marks")]
        [SerializeField] private float minorTickWidth = 1f;
        
        [Tooltip("Color of minor tick marks")]
        [SerializeField] private Color minorTickColor = new Color(1f, 1f, 1f, 0.6f);
        
        #endregion
        
        #region Inspector - Label Settings
        
        [Header("Labels")]
        [Tooltip("Font for degree labels")]
        [SerializeField] private TMP_FontAsset labelFont;
        
        [Tooltip("Font size for degree labels")]
        [SerializeField] private float labelFontSize = 16f;
        
        [Tooltip("Font size for cardinal direction labels (N, E, S, W)")]
        [SerializeField] private float cardinalFontSize = 20f;
        
        [Tooltip("Color for degree labels")]
        [SerializeField] private Color labelColor = Color.white;
        
        [Tooltip("Color for cardinal direction labels")]
        [SerializeField] private Color cardinalColor = Color.white;
        
        [Tooltip("Vertical offset for labels from center")]
        [SerializeField] private float labelVerticalOffset = -5f;
        
        [Header("Label Display")]
        [Tooltip("Show labels every N degrees (typically 30 for aviation style)")]
        [SerializeField] private int labelIntervalDegrees = 30;
        
        [Tooltip("Use short format (3 instead of 030, N instead of 360)")]
        [SerializeField] private bool useShortFormat = true;
        
        #endregion
        
        #region Inspector - Appearance
        
        [Header("Appearance")]
        [Tooltip("Background color of the tape (set alpha to 0 for transparent)")]
        [SerializeField] private Color backgroundColor = new Color(0f, 0f, 0f, 0.5f);
        
        [Tooltip("Show a background image")]
        [SerializeField] private bool showBackground = true;
        
        #endregion
        
        #region Public Properties
        
        /// <summary>
        /// Pixels per degree value for tape scrolling calculations
        /// </summary>
        public float PixelsPerDegree => pixelsPerDegree;
        
        /// <summary>
        /// Total width of the 360° tape
        /// </summary>
        public float TapeWidth => pixelsPerDegree * 360f;
        
        #endregion
        
        #region Private Fields
        
        private RectTransform rectTransform;
        private List<GameObject> generatedObjects = new List<GameObject>();
        
        // Cardinal directions
        private static readonly Dictionary<int, string> CardinalLabels = new Dictionary<int, string>
        {
            { 0, "N" },
            { 90, "E" },
            { 180, "S" },
            { 270, "W" }
        };
        
        #endregion
        
        #region Unity Lifecycle
        
        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
        }
        
        #endregion
        
        #region Generation Methods
        
        /// <summary>
        /// Generate the compass bar tape with all tick marks and labels.
        /// North (0°) is centered in the tape by default.
        /// </summary>
        [ContextMenu("Generate Tape")]
        
        public void GenerateTape()
        {
            ClearGeneratedObjects();
            
            if (rectTransform == null)
                rectTransform = GetComponent<RectTransform>();
            
            // Set tape dimensions - multiply by repeat copies for seamless wrapping
            float singleTapeWidth = TapeWidth;
            float totalWidth = singleTapeWidth * repeatCopies;
            rectTransform.sizeDelta = new Vector2(totalWidth, tapeHeight);
            
            // Set pivot to center so North (0°) is at the center of the tape
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            
            // Create background if needed
            if (showBackground)
            {
                CreateBackground();
            }
            
            // Generate multiple copies of the tape for seamless scrolling
            // Each copy spans anchor range of 1/repeatCopies
            float anchorPerCopy = 1f / repeatCopies;
            
            for (int copy = 0; copy < repeatCopies; copy++)
            {
                // Calculate anchor base for this copy
                // For 3 copies: copy 0 = left third, copy 1 = center third, copy 2 = right third
                float copyAnchorBase = copy * anchorPerCopy;
                
                // Generate tape for this copy
                // Degrees 0-360 map to anchor range of anchorPerCopy
                for (int deg = 0; deg < 360; deg += 5)
                {
                    // Calculate anchor X: deg 0 should be at center of center copy
                    // For center copy (copy 1 of 3), deg 0 should be at anchor 0.5
                    // Normalize deg to 0-1 within the copy's anchor range
                    float degNormalized = deg / 360f;
                    float anchorX = copyAnchorBase + (degNormalized * anchorPerCopy);
                    
                    bool isMajor = (deg % 10 == 0);
                    bool isLabeled = (deg % labelIntervalDegrees == 0);
                    bool isCardinal = CardinalLabels.ContainsKey(deg);
                    
                    // Create tick mark
                    if (isMajor)
                    {
                        CreateTickMark(anchorX, majorTickHeight, majorTickWidth, majorTickColor, $"Tick_{copy}_{deg}");
                    }
                    else if (showMinorTicks)
                    {
                        CreateTickMark(anchorX, minorTickHeight, minorTickWidth, minorTickColor, $"MinorTick_{copy}_{deg}");
                    }
                    
                    // Create label
                    if (isLabeled)
                    {
                        string labelText = GetLabelText(deg);
                        float fontSize = isCardinal ? cardinalFontSize : labelFontSize;
                        Color color = isCardinal ? cardinalColor : labelColor;
                        CreateLabel(anchorX, labelText, fontSize, color, $"Label_{copy}_{deg}");
                    }
                }
            }
            
            Debug.Log($"[CompassBarGenerator] Generated tape with anchor-based positioning ({repeatCopies} copies)");
            
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(gameObject);
#endif
        }
        
        /// <summary>
        /// Clear all generated child objects
        /// </summary>
        [ContextMenu("Clear Generated Objects")]
        public void ClearGeneratedObjects()
        {
            // Destroy all children
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                if (Application.isPlaying)
                    Destroy(child.gameObject);
                else
                    DestroyImmediate(child.gameObject);
            }
            
            generatedObjects.Clear();
        }
        
        private void CreateBackground()
        {
            var bgObj = new GameObject("Background");
            bgObj.transform.SetParent(transform, false);
            
            var bgRect = bgObj.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;
            bgRect.anchoredPosition = Vector2.zero;
            
            var bgImage = bgObj.AddComponent<Image>();
            bgImage.color = backgroundColor;
            
            // Move to back
            bgObj.transform.SetAsFirstSibling();
            
            generatedObjects.Add(bgObj);
        }
        
        private void CreateTickMark(float anchorX, float height, float width, Color color, string name)
        {
            var tickObj = new GameObject(name);
            tickObj.transform.SetParent(transform, false);
            
            var tickRect = tickObj.AddComponent<RectTransform>();
            // Use anchor-based positioning (anchorX is 0-1 range)
            tickRect.anchorMin = new Vector2(anchorX, 0.5f);
            tickRect.anchorMax = new Vector2(anchorX, 0.5f);
            tickRect.pivot = new Vector2(0.5f, 0f);
            tickRect.sizeDelta = new Vector2(width, height);
            tickRect.anchoredPosition = new Vector2(0f, 0f); // Position at anchor
            
            var tickImage = tickObj.AddComponent<Image>();
            tickImage.color = color;
            
            generatedObjects.Add(tickObj);
        }
        
        private void CreateLabel(float anchorX, string text, float fontSize, Color color, string name)
        {
            var labelObj = new GameObject(name);
            labelObj.transform.SetParent(transform, false);
            
            var labelRect = labelObj.AddComponent<RectTransform>();
            // Use anchor-based positioning (anchorX is 0-1 range)
            labelRect.anchorMin = new Vector2(anchorX, 0.5f);
            labelRect.anchorMax = new Vector2(anchorX, 0.5f);
            labelRect.pivot = new Vector2(0.5f, 0.5f);
            labelRect.sizeDelta = new Vector2(40f, 30f);
            labelRect.anchoredPosition = new Vector2(0f, labelVerticalOffset);
            
            var tmpText = labelObj.AddComponent<TextMeshProUGUI>();
            tmpText.text = text;
            tmpText.fontSize = fontSize;
            tmpText.color = color;
            tmpText.alignment = TextAlignmentOptions.Center;
            tmpText.enableAutoSizing = false;
            
            if (labelFont != null)
            {
                tmpText.font = labelFont;
            }
            
            generatedObjects.Add(labelObj);
        }
        
        private string GetLabelText(int degrees)
        {
            // Check for cardinal direction
            if (CardinalLabels.TryGetValue(degrees, out string cardinal))
            {
                return cardinal;
            }
            
            if (useShortFormat)
            {
                // Aviation style: 030 -> 3, 060 -> 6, 120 -> 12, etc.
                int shortValue = degrees / 10;
                return shortValue.ToString();
            }
            else
            {
                return degrees.ToString("000");
            }
        }
        
        #endregion
        
        #region Editor
        
#if UNITY_EDITOR
        private void OnValidate()
        {
            // Clamp values
            pixelsPerDegree = Mathf.Max(1f, pixelsPerDegree);
            tapeHeight = Mathf.Max(10f, tapeHeight);
            majorTickHeight = Mathf.Max(1f, majorTickHeight);
            minorTickHeight = Mathf.Max(1f, minorTickHeight);
            labelIntervalDegrees = Mathf.Clamp(labelIntervalDegrees, 10, 90);
        }
        
        [ContextMenu("Preview Tape")]
        private void PreviewTape()
        {
            GenerateTape();
        }
#endif
        
        #endregion
    }
}
