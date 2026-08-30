using System.Globalization;
using TMPro;
using UnityEngine;

namespace HUDControl.Elements
{
    /// <summary>
    /// Shared presentation for compact torque/NR/N2 readouts and their fixed
    /// percentage scale labels. The text stays lightweight so the HUD does
    /// not obscure the pilot's outside view.
    /// </summary>
    internal static class EngineHudNumericReadout
    {
        private const float DefaultFrameSize = 0.2918475f;
        private static readonly Color HudGreen = new Color(0.2f, 1f, 0.2f, 1f);
        private static readonly Color MissingDataGreen = new Color(0.2f, 1f, 0.2f, 0.46f);

        /// <summary>
        /// Configure a TextMeshPro object that was authored in the scene or
        /// prefab. Numeric HUD objects are intentionally never instantiated at
        /// runtime; this method only applies presentation settings to an
        /// existing reference.
        /// </summary>
        public static void ConfigureExisting(TMP_Text readout, float fontSize, int layer)
        {
            if (readout == null)
            {
                return;
            }

            readout.gameObject.layer = layer;
            if (readout.font == null)
            {
                readout.font = TMP_Settings.defaultFontAsset;
            }
            readout.enableAutoSizing = false;
            readout.fontSize = fontSize;
            readout.fontStyle = FontStyles.Normal;
            readout.alignment = TextAlignmentOptions.Center;
            readout.textWrappingMode = TextWrappingModes.NoWrap;
            readout.overflowMode = TextOverflowModes.Overflow;
            readout.raycastTarget = false;
        }

        /// <summary>
        /// Return evenly-spaced percentage values for the fixed scale labels.
        /// The configured maximum is always included so non-step redline values
        /// such as 110% and 120% remain readable at the top of the bar.
        /// </summary>
        public static int[] BuildScaleValues(float maximumPercent, int stepPercent)
        {
            int maximum = Mathf.Max(0, Mathf.RoundToInt(maximumPercent));
            int step = Mathf.Max(1, stepPercent);
            var values = new System.Collections.Generic.List<int>();

            for (int value = 0; value <= maximum; value += step)
            {
                values.Add(value);
            }

            if (values.Count == 0 || values[values.Count - 1] != maximum)
            {
                values.Add(maximum);
            }

            return values.ToArray();
        }

        /// <summary>
        /// Position a scale label relative to the authored frame bounds.
        /// The engine HUD is a world-space canvas, so the existing frame's
        /// local units are used directly instead of screen-pixel offsets.
        /// </summary>
        public static Vector2 GetScaleLabelPosition(
            RectTransform frame,
            float value,
            float maximumPercent,
            float horizontalOffset)
        {
            if (frame == null)
            {
                return new Vector2(horizontalOffset, 0f);
            }

            float height = Mathf.Abs(frame.rect.height);
            if (height < 0.0001f)
            {
                height = Mathf.Abs(frame.sizeDelta.y);
            }

            if (height < 0.0001f)
            {
                height = DefaultFrameSize;
            }

            float normalized = Mathf.Clamp01(value / Mathf.Max(1f, maximumPercent));
            float bottom = frame.anchoredPosition.y - height * 0.5f;
            return new Vector2(
                frame.anchoredPosition.x + horizontalOffset,
                bottom + normalized * height);
        }

        /// <summary>
        /// Position a scale label on the same calibrated travel used by its
        /// live pointer. This keeps the labels aligned even when the source
        /// artwork has transparent top or bottom margins inside its frame.
        /// </summary>
        public static Vector2 GetScaleLabelPosition(
            RectTransform frame,
            float value,
            float maximumPercent,
            float horizontalOffset,
            float pointerMinimumY,
            float pointerTravelY)
        {
            Vector2 position = GetScaleLabelPosition(frame, value, maximumPercent, horizontalOffset);
            float normalized = Mathf.Clamp01(value / Mathf.Max(1f, maximumPercent));
            position.y = pointerMinimumY + normalized * pointerTravelY;
            return position;
        }

        /// <summary>
        /// Set the text and visibility for a fixed scale label.
        /// </summary>
        public static void SetScaleLabel(TMP_Text label, int value, bool visible)
        {
            if (label == null)
            {
                return;
            }

            if (label.gameObject.activeSelf != visible)
            {
                label.gameObject.SetActive(visible);
            }

            if (!visible)
            {
                return;
            }

            string text = value.ToString(CultureInfo.InvariantCulture);
            if (label.text != text)
            {
                label.text = text;
            }

            label.color = HudGreen;
        }

        /// <summary>
        /// Return the authored width, falling back to the source image size
        /// before the first canvas layout pass.
        /// </summary>
        public static float GetFrameWidth(RectTransform frame)
        {
            if (frame == null)
            {
                return DefaultFrameSize;
            }

            float width = Mathf.Abs(frame.rect.width);
            if (width < 0.0001f)
            {
                width = Mathf.Abs(frame.sizeDelta.x);
            }

            return width < 0.0001f ? DefaultFrameSize : width;
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
