using System.Globalization;
using TMPro;
using UnityEngine;

namespace HUDControl.Elements
{
    /// <summary>
    /// Shared presentation for the compact torque and NR/N2 numeric readouts.
    /// The values intentionally remain text-only so the HUD does not obscure
    /// the pilot's outside view.
    /// </summary>
    internal static class EngineHudNumericReadout
    {
        private const string PreferredFontResource = "CNPro/Fonts/Distance Font SDF";
        private static readonly Color HudGreen = new Color(0.2f, 1f, 0.2f, 1f);
        private static readonly Color MissingDataGreen = new Color(0.2f, 1f, 0.2f, 0.46f);

        public static TMP_Text Ensure(
            Transform parent,
            TMP_Text readout,
            string childName,
            Vector2 anchoredPosition,
            float fontSize,
            int layer)
        {
            if (parent == null)
            {
                return readout;
            }

            bool generated = false;

            if (readout == null)
            {
                Transform existing = parent.Find(childName);
                if (existing != null)
                {
                    readout = existing.GetComponent<TMP_Text>();
                }
            }

            if (readout == null)
            {
                GameObject readoutObject = new GameObject(childName, typeof(RectTransform));
                readoutObject.transform.SetParent(parent, false);
                readout = readoutObject.AddComponent<TextMeshProUGUI>();
                generated = true;
            }

            readout.gameObject.layer = layer;
            RectTransform rect = readout.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(52f, 26f);
            rect.localScale = Vector3.one * 0.0016f;
            rect.localRotation = Quaternion.identity;

            TMP_FontAsset preferredFont = Resources.Load<TMP_FontAsset>(PreferredFontResource);
            if (generated && preferredFont != null)
            {
                readout.font = preferredFont;
            }
            else if (readout.font == null)
            {
                readout.font = preferredFont ?? TMP_Settings.defaultFontAsset;
            }

            readout.enableAutoSizing = false;
            readout.fontSize = fontSize;
            readout.fontStyle = FontStyles.Normal;
            readout.alignment = TextAlignmentOptions.Center;
            readout.textWrappingMode = TextWrappingModes.NoWrap;
            readout.overflowMode = TextOverflowModes.Overflow;
            readout.raycastTarget = false;
            return readout;
        }

        public static void SetValue(TMP_Text readout, float value, bool dataValid, bool channelVisible)
        {
            if (readout == null)
            {
                return;
            }

            if (readout.gameObject.activeSelf != channelVisible)
            {
                readout.gameObject.SetActive(channelVisible);
            }

            if (!channelVisible)
            {
                return;
            }

            string text = dataValid
                ? Mathf.RoundToInt(value).ToString("000", CultureInfo.InvariantCulture)
                : "---";
            if (readout.text != text)
            {
                readout.text = text;
            }

            readout.color = dataValid ? HudGreen : MissingDataGreen;
        }
    }
}
