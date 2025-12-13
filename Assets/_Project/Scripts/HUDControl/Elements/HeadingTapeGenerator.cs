using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

namespace HUDControl.Elements
{
    /// <summary>
    /// Procedurally generates a 360-degree heading tape/compass rose.
    /// Creates tick marks and labels dynamically instead of using a static texture.
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu("HUD Control/Procedural/Heading Tape Generator")]
    public class HeadingTapeGenerator : MonoBehaviour
    {
        #region Inspector - Container
        
        [Header("Container")]
        [Tooltip("Parent RectTransform to hold all generated elements")]
        [SerializeField] private RectTransform container;
        
        [Tooltip("Total width of the tape (should cover 360° + wrapping buffer)")]
        [SerializeField] private float tapeWidth = 3600f;
        
        [Tooltip("Height of the tape")]
        [SerializeField] private float tapeHeight = 30f;
        
        #endregion
        
        #region Inspector - Tick Marks
        
        [Header("Tick Marks")]
        [Tooltip("Degrees between major ticks (labeled)")]
        [SerializeField] private int majorTickInterval = 10;
        
        [Tooltip("Degrees between minor ticks")]
        [SerializeField] private int minorTickInterval = 5;
        
        [Tooltip("Major tick height")]
        [SerializeField] private float majorTickHeight = 20f;
        
        [Tooltip("Minor tick height")]
        [SerializeField] private float minorTickHeight = 10f;
        
        [Tooltip("Tick width")]
        [SerializeField] private float tickWidth = 2f;
        
        [Tooltip("Tick color")]
        [SerializeField] private Color tickColor = Color.white;
        
        #endregion
        
        #region Inspector - Labels
        
        [Header("Labels")]
        [Tooltip("Font for heading labels")]
        [SerializeField] private TMP_FontAsset font;
        
        [Tooltip("Font size")]
        [SerializeField] private float fontSize = 16f;
        
        [Tooltip("Label color")]
        [SerializeField] private Color labelColor = Color.white;
        
        [Tooltip("Vertical offset for labels from top")]
        [SerializeField] private float labelYOffset = 5f;
        
        [Tooltip("Show cardinal directions (N, E, S, W)")]
        [SerializeField] private bool showCardinals = true;
        
        [Tooltip("Show numeric headings without leading zeros")]
        [SerializeField] private bool compactNumbers = true;
        
        #endregion
        
        #region Inspector - Generation
        
        [Header("Generation")]
        [Tooltip("Generate extra copies for seamless wrapping")]
        [SerializeField] private bool enableWrapping = true;
        
        [Tooltip("Degrees of extra tape on each side for wrapping")]
        [SerializeField] private float wrapBuffer = 60f;
        
        #endregion
        
        private List<GameObject> generatedElements = new List<GameObject>();
        private float pixelsPerDegree;
        
        public float PixelsPerDegree => pixelsPerDegree;
        public float TapeWidth => tapeWidth;
        
        private void Start()
        {
            if (Application.isPlaying && generatedElements.Count == 0)
            {
                Generate();
            }
        }
        
        [ContextMenu("Generate Tape")]
        public void Generate()
        {
            ClearGenerated();
            
            if (container == null)
            {
                container = GetComponent<RectTransform>();
                if (container == null)
                {
                    Debug.LogError("[HeadingTapeGenerator] Container RectTransform required");
                    return;
                }
            }
            
            // Calculate pixels per degree
            // Full 360 degrees spans tapeWidth (before wrapping buffer)
            pixelsPerDegree = tapeWidth / 360f;
            
            // Set container size
            float totalWidth = tapeWidth;
            if (enableWrapping)
            {
                totalWidth += wrapBuffer * pixelsPerDegree * 2;
            }
            container.sizeDelta = new Vector2(totalWidth, tapeHeight);
            
            // Starting position (left edge)
            float startDegree = enableWrapping ? -wrapBuffer : 0f;
            float endDegree = enableWrapping ? 360f + wrapBuffer : 360f;
            
            // Generate ticks and labels
            for (float deg = startDegree; deg <= endDegree; deg += minorTickInterval)
            {
                float normalizedDeg = NormalizeDegree(deg);
                bool isMajor = Mathf.RoundToInt(normalizedDeg) % majorTickInterval == 0;
                
                float xPos = (deg - startDegree) * pixelsPerDegree;
                
                // Create tick
                CreateTick(xPos, isMajor ? majorTickHeight : minorTickHeight);
                
                // Create label for major ticks
                if (isMajor)
                {
                    CreateLabel(xPos, normalizedDeg);
                }
            }
            
            Debug.Log($"[HeadingTapeGenerator] Generated tape: {pixelsPerDegree:F2} px/deg, " +
                      $"{generatedElements.Count} elements, width: {totalWidth}");
        }
        
        private void CreateTick(float xPos, float height)
        {
            GameObject tickObj = new GameObject("Tick");
            tickObj.transform.SetParent(container, false);
            
            RectTransform rt = tickObj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(0, 0);
            rt.pivot = new Vector2(0.5f, 0);
            rt.sizeDelta = new Vector2(tickWidth, height);
            rt.anchoredPosition = new Vector2(xPos, 0);
            
            Image img = tickObj.AddComponent<Image>();
            img.color = tickColor;
            img.raycastTarget = false;
            
            generatedElements.Add(tickObj);
        }
        
        private void CreateLabel(float xPos, float heading)
        {
            string labelText = GetHeadingLabel(Mathf.RoundToInt(heading));
            
            GameObject labelObj = new GameObject("Label_" + labelText);
            labelObj.transform.SetParent(container, false);
            
            RectTransform rt = labelObj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.sizeDelta = new Vector2(40, fontSize + 4);
            rt.anchoredPosition = new Vector2(xPos, -labelYOffset);
            
            TMP_Text text = labelObj.AddComponent<TextMeshProUGUI>();
            text.text = labelText;
            text.fontSize = fontSize;
            text.color = labelColor;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            
            if (font != null)
                text.font = font;
            
            generatedElements.Add(labelObj);
        }
        
        private string GetHeadingLabel(int heading)
        {
            heading = (int)NormalizeDegree(heading);
            
            if (showCardinals)
            {
                switch (heading)
                {
                    case 0: case 360: return "N";
                    case 90: return "E";
                    case 180: return "S";
                    case 270: return "W";
                }
            }
            
            if (compactNumbers)
            {
                // Show 3, 6, 12, 15, 21, 24, 30, 33 (divide by 10)
                int shortHeading = heading / 10;
                return shortHeading.ToString();
            }
            else
            {
                return heading.ToString("000");
            }
        }
        
        private float NormalizeDegree(float deg)
        {
            while (deg < 0) deg += 360;
            while (deg >= 360) deg -= 360;
            return deg;
        }
        
        [ContextMenu("Clear Generated")]
        public void ClearGenerated()
        {
            foreach (var obj in generatedElements)
            {
                if (obj != null)
                {
                    if (Application.isPlaying)
                        Destroy(obj);
                    else
                        DestroyImmediate(obj);
                }
            }
            generatedElements.Clear();
            
            // Also clear any orphaned children
            if (container != null)
            {
                for (int i = container.childCount - 1; i >= 0; i--)
                {
                    var child = container.GetChild(i);
                    if (child.name.StartsWith("Tick") || child.name.StartsWith("Label"))
                    {
                        if (Application.isPlaying)
                            Destroy(child.gameObject);
                        else
                            DestroyImmediate(child.gameObject);
                    }
                }
            }
        }
        
        #region Editor
        
#if UNITY_EDITOR
        [ContextMenu("Preview in Editor")]
        private void PreviewInEditor()
        {
            Generate();
        }
        
        private void OnValidate()
        {
            if (tapeWidth < 360) tapeWidth = 360;
            if (majorTickInterval < 1) majorTickInterval = 1;
            if (minorTickInterval < 1) minorTickInterval = 1;
        }
#endif
        
        #endregion
    }
}
