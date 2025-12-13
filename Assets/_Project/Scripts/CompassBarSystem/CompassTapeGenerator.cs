using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CompassBarSystem
{
    /// <summary>
    /// Builds a procedural compass tape with tick marks and labels.
    /// Generates repeated 360° copies so scrolling stays seamless when anchors shift.
    /// </summary>
    [ExecuteInEditMode]
    [RequireComponent(typeof(RectTransform))]
    [AddComponentMenu("UI/Compass Bar System/Compass Tape Generator")]
    public class CompassTapeGenerator : MonoBehaviour
    {
        [Header("Tape")]
        [SerializeField] private float pixelsPerDegree = 4f;
        [SerializeField] private float tapeHeight = 60f;
        [SerializeField] private int repeatCopies = 3;
        [SerializeField] private bool showBackground = true;
        [SerializeField] private Color backgroundColor = new Color(0f, 0f, 0f, 0.6f);
        [SerializeField] private bool autoGenerateInEditMode = true;

        [Header("Ticks")]
        [SerializeField] private int majorTickInterval = 10;
        [SerializeField] private float majorTickHeight = 22f;
        [SerializeField] private float majorTickWidth = 2.5f;
        [SerializeField] private Color majorTickColor = Color.white;
        [SerializeField] private bool showMinorTicks = true;
        [SerializeField] private int minorTickInterval = 5;
        [SerializeField] private float minorTickHeight = 12f;
        [SerializeField] private float minorTickWidth = 1.25f;
        [SerializeField] private Color minorTickColor = new Color(1f, 1f, 1f, 0.75f);

        [Header("Labels")]
        [SerializeField] private TMP_FontAsset labelFont;
        [SerializeField] private float labelFontSize = 18f;
        [SerializeField] private float cardinalFontSize = 22f;
        [SerializeField] private Color labelColor = Color.white;
        [SerializeField] private Color cardinalColor = Color.white;
        [SerializeField] private float labelVerticalOffset = -8f;
        [SerializeField] private int labelIntervalDegrees = 30;
        [SerializeField] private bool useShortFormat = true;

        private RectTransform rectTransform;
        private readonly List<GameObject> generated = new List<GameObject>();

        private static readonly Dictionary<int, string> CardinalLabels = new Dictionary<int, string>
        {
            { 0, "N" },
            { 90, "E" },
            { 180, "S" },
            { 270, "W" }
        };

        public float PixelsPerDegree => pixelsPerDegree;
        public float CycleWidth => pixelsPerDegree * 360f;
        public float TotalWidth => CycleWidth * repeatCopies;

        void Awake()
        {
            if (rectTransform == null)
                rectTransform = GetComponent<RectTransform>();
        }

        void OnEnable()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying && autoGenerateInEditMode)
            {
                GenerateTape();
            }
#endif
        }

        [ContextMenu("Generate Tape")]
        public void GenerateTape()
        {
            if (rectTransform == null)
                rectTransform = GetComponent<RectTransform>();

            ClearGenerated();

            float singleWidth = CycleWidth;
            float totalWidth = TotalWidth;

            rectTransform.sizeDelta = new Vector2(totalWidth, tapeHeight);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);

            if (showBackground)
            {
                CreateBackground();
            }

            float anchorPerCopy = 1f / repeatCopies;
            for (int copy = 0; copy < repeatCopies; copy++)
            {
                float copyAnchorBase = copy * anchorPerCopy;
                for (int deg = 0; deg < 360; deg += Mathf.Min(minorTickInterval, majorTickInterval))
                {
                    float degNormalized = deg / 360f;
                    float anchorX = copyAnchorBase + degNormalized * anchorPerCopy;

                    bool isMajor = deg % majorTickInterval == 0;
                    bool isMinor = showMinorTicks && deg % minorTickInterval == 0 && !isMajor;
                    bool isLabeled = deg % labelIntervalDegrees == 0;
                    bool isCardinal = CardinalLabels.ContainsKey(deg);

                    if (isMajor)
                    {
                        CreateTick(anchorX, majorTickHeight, majorTickWidth, majorTickColor, $"Tick_{copy}_{deg}");
                    }
                    else if (isMinor)
                    {
                        CreateTick(anchorX, minorTickHeight, minorTickWidth, minorTickColor, $"Minor_{copy}_{deg}");
                    }

                    if (isLabeled)
                    {
                        string text = GetLabelText(deg);
                        float size = isCardinal ? cardinalFontSize : labelFontSize;
                        Color color = isCardinal ? cardinalColor : labelColor;
                        CreateLabel(anchorX, text, size, color, $"Label_{copy}_{deg}");
                    }
                }
            }
        }

        [ContextMenu("Clear Generated")]
        public void ClearGenerated()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                if (Application.isPlaying)
                    Destroy(child.gameObject);
                else
                    DestroyImmediate(child.gameObject);
            }
            generated.Clear();
        }

        void CreateBackground()
        {
            var bg = new GameObject("Background");
            bg.transform.SetParent(transform, false);
            var rt = bg.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;
            var img = bg.AddComponent<Image>();
            img.color = backgroundColor;
            bg.transform.SetAsFirstSibling();
            generated.Add(bg);
        }

        void CreateTick(float anchorX, float height, float width, Color color, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(anchorX, 0.5f);
            rt.anchorMax = new Vector2(anchorX, 0.5f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(width, height);
            rt.anchoredPosition = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.color = color;
            generated.Add(go);
        }

        void CreateLabel(float anchorX, string text, float fontSize, Color color, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(anchorX, 0.5f);
            rt.anchorMax = new Vector2(anchorX, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(40f, 30f);
            rt.anchoredPosition = new Vector2(0f, labelVerticalOffset);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            if (labelFont != null)
                tmp.font = labelFont;
            generated.Add(go);
        }

        string GetLabelText(int degrees)
        {
            if (CardinalLabels.TryGetValue(degrees, out string cardinal))
                return cardinal;

            if (useShortFormat)
                return (degrees / 10).ToString();

            return degrees.ToString("000");
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            pixelsPerDegree = Mathf.Max(0.1f, pixelsPerDegree);
            tapeHeight = Mathf.Max(10f, tapeHeight);
            repeatCopies = Mathf.Clamp(repeatCopies, 1, 5);
            majorTickInterval = Mathf.Max(1, majorTickInterval);
            minorTickInterval = Mathf.Max(1, minorTickInterval);
            labelIntervalDegrees = Mathf.Clamp(labelIntervalDegrees, 5, 90);

            if (!Application.isPlaying && autoGenerateInEditMode)
            {
                GenerateTape();
            }
        }
#endif
    }
}
