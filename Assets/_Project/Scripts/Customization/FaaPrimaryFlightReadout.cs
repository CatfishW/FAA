using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FAA.Customization
{
    /// <summary>
    /// Presentation only: flight values and unavailable-data states remain owned
    /// by the existing HUD elements. Authored in edit mode as well as in play.
    /// </summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class FaaPrimaryFlightReadout : MonoBehaviour
    {
        [SerializeField] private TMP_Text valueText;
        [SerializeField] private TMP_Text unitText;
        [SerializeField] private bool altitude;

        public void Configure(TMP_Text value, TMP_Text units, bool isAltitude)
        {
            valueText = value;
            unitText = units;
            altitude = isAltitude;
            ApplyPresentation();
        }

        private void OnEnable() => ApplyPresentation();

        public void ApplyPresentation()
        {
            if (valueText == null || unitText == null) return;
            RectTransform value = valueText.rectTransform;
            value.sizeDelta = new Vector2(altitude ? 154f : 114f, 52f);
            valueText.fontSize = 38f;
            valueText.fontStyle = FontStyles.Normal;
            valueText.alignment = TextAlignmentOptions.Center;
            valueText.enableAutoSizing = false;
            valueText.textWrappingMode = TextWrappingModes.NoWrap;
            valueText.overflowMode = TextOverflowModes.Overflow;
            valueText.richText = true;
            valueText.raycastTarget = false;
            valueText.characterSpacing = 0f;
            // Do not change its color: loss-of-data dimming and the pilot's HUD
            // color selection are controlled elsewhere.
            RectTransform units = unitText.rectTransform;
            units.anchorMin = value.anchorMin;
            units.anchorMax = value.anchorMax;
            units.pivot = value.pivot;
            units.localScale = value.localScale;
            units.anchoredPosition = value.anchoredPosition + new Vector2(0f, -36f * value.localScale.y);
            units.sizeDelta = new Vector2(value.sizeDelta.x, 20f);
            unitText.text = altitude ? "ALT  /  FT" : "IAS  /  KT";
            unitText.fontSize = 12f;
            unitText.fontStyle = FontStyles.Normal;
            unitText.characterSpacing = 2f;
            unitText.alignment = TextAlignmentOptions.Center;
            unitText.enableAutoSizing = false;
            unitText.textWrappingMode = TextWrappingModes.NoWrap;
            unitText.raycastTarget = false;

            RectTransform frame = Child(transform, "FAA Readout Frame");
            frame.anchorMin = value.anchorMin;
            frame.anchorMax = value.anchorMax;
            frame.pivot = value.pivot;
            frame.anchoredPosition = value.anchoredPosition;
            frame.sizeDelta = value.sizeDelta + new Vector2(12f, 4f);
            frame.localScale = value.localScale;
            frame.SetAsFirstSibling();
            float halfWidth = frame.sizeDelta.x * .5f;
            Color line = new Color(.2f, 1f, .2f, .65f);
            Line(frame, "Left", new Vector2(-halfWidth, 0), new Vector2(1.25f, 32f), line);
            Line(frame, "Right", new Vector2(halfWidth, 0), new Vector2(1.25f, 32f), line);
            Line(frame, "Top Left", new Vector2(-halfWidth + 4f, 16f), new Vector2(8f, 1.25f), line);
            Line(frame, "Top Right", new Vector2(halfWidth - 4f, 16f), new Vector2(8f, 1.25f), line);
            Line(frame, "Bottom Left", new Vector2(-halfWidth + 4f, -16f), new Vector2(8f, 1.25f), line);
            Line(frame, "Bottom Right", new Vector2(halfWidth - 4f, -16f), new Vector2(8f, 1.25f), line);
        }

        private static RectTransform Child(Transform parent, string name)
        {
            Transform found = parent.Find(name);
            RectTransform rect = found != null ? found.GetComponent<RectTransform>() :
                new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static void Line(RectTransform parent, string name, Vector2 position, Vector2 size, Color color)
        {
            RectTransform rect = Child(parent, name);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
            Image image = rect.GetComponent<Image>() ?? rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
        }
    }
}
