using System.Collections.Generic;
using System.Globalization;
using FAA.XPlaneIntegration.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FAA.Customization
{
    public enum FaaEngineInstrument { Torque, EngineSpeed, VerticalSpeed }

    /// <summary>Sharp, unit-labelled instruments. No unverified engine operating bands.</summary>
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(CanvasRenderer))]
    public sealed class FaaEngineInstrumentGraphic : MaskableGraphic
    {
        [SerializeField] private FaaEngineInstrument instrument;
        [SerializeField] private XPlane12ApiHudBridge source;
        [SerializeField] private Graphic[] replacedGraphics;
        private readonly Dictionary<string, TMP_Text> labels = new();
        private float left, right, rotor, displayedLeft, displayedRight;
        private bool leftValid, rightValid, rotorValid, initialized;
        private int engineCount = 2;
        private static readonly Color Ink = new(.26f, 1f, .27f, .94f);
        private static readonly Color Quiet = new(.26f, 1f, .27f, .40f);
        private static readonly Color Back = new(.015f, .055f, .065f, .78f);

        public void Configure(FaaEngineInstrument kind, XPlane12ApiHudBridge bridge, Graphic[] legacy)
        {
            if (GetComponent<CanvasRenderer>() == null) gameObject.AddComponent<CanvasRenderer>();
            instrument = kind; source = bridge; replacedGraphics = legacy; raycastTarget = false;
            RefreshPresentation();
        }

        private void LateUpdate() => RefreshPresentation();

        public static bool IsUsable(bool feedHealthy, bool fieldValid, float value) =>
            feedHealthy && fieldValid && !float.IsNaN(value) && !float.IsInfinity(value);

        public static string FormatPercent(float value, bool valid) => valid
            ? value.ToString("0.0", CultureInfo.InvariantCulture) : "—";

        public static string FormatVerticalSpeed(float value, bool valid) => !valid ? "—" :
            (Mathf.Round(value / 10f) * 10f).ToString("+0;-0;0", CultureInfo.InvariantCulture);

        public static float VerticalSpeedPosition(float value) => Mathf.Clamp(value / 2000f, -1, 1) * 84f;

        public void RefreshPresentation()
        {
            if (replacedGraphics != null)
                foreach (var old in replacedGraphics) if (old != null) old.enabled = false;
            if (Application.isPlaying && source == null) source = FindAnyObjectByType<XPlane12ApiHudBridge>();
            var data = source != null ? source.LatestFlightData : null;
            bool healthy = Application.isPlaying && source != null && source.IsFeedHealthy && data != null;
            engineCount = data != null ? Mathf.Clamp(data.engineCount, 1, 2) : 2;
            left = data == null ? 0 : instrument == FaaEngineInstrument.Torque ? data.engine1Torque :
                instrument == FaaEngineInstrument.EngineSpeed ? data.engine1NR : data.verticalSpeed;
            right = data == null ? 0 : instrument == FaaEngineInstrument.Torque ? data.engine2Torque : data.engine2NR;
            rotor = data != null ? data.rotorNR : 0;
            leftValid = IsUsable(healthy, data != null && (instrument == FaaEngineInstrument.VerticalSpeed ||
                (instrument == FaaEngineInstrument.Torque ? data.engine1TorqueValid : data.engine1NRValid)), left);
            rightValid = engineCount > 1 && IsUsable(healthy, data != null &&
                (instrument == FaaEngineInstrument.Torque ? data.engine2TorqueValid : data.engine2NRValid), right);
            rotorValid = IsUsable(healthy, data != null && data.rotorNRValid, rotor);
            // A short pointer settle is purely presentation; numbers always show the current sample.
            float blend = !initialized || !Application.isPlaying ? 1 : 1 - Mathf.Exp(-Time.unscaledDeltaTime / .09f);
            displayedLeft = Mathf.Lerp(displayedLeft, leftValid ? left : 0, blend);
            displayedRight = Mathf.Lerp(displayedRight, rightValid ? right : 0, blend);
            initialized = true;
            if (instrument == FaaEngineInstrument.VerticalSpeed) RefreshVerticalLabels();
            else RefreshEngineLabels();
            SetVerticesDirty();
        }

        private void RefreshEngineLabels()
        {
            Label("Title", instrument == FaaEngineInstrument.Torque ? "TORQUE" : "ENGINE N2", new(0, 99), new(172, 22), 13, Ink);
            Label("Units", "%", new(0, 78), new(40, 18), 10.5f, Quiet);
            for (int i = 0; i <= 2; i++)
                Label("Scale " + i, (i * 50).ToString(), new(0, -32 + i * 42), new(32, 16), 10, Quiet);
            Label("Left", engineCount > 1 ? "L" : "ENG", new(-43, -56), new(42, 17), 10.5f, Quiet);
            Label("Right", engineCount > 1 ? "R" : "", new(43, -56), new(42, 17), 10.5f, Quiet);
            Label("Left Value", FormatPercent(left, leftValid), new(-43, -78), new(73, 28), 22, leftValid ? Ink : Quiet);
            Label("Right Value", engineCount > 1 ? FormatPercent(right, rightValid) : "", new(43, -78), new(73, 28), 22, rightValid ? Ink : Quiet);
            string footer = instrument == FaaEngineInstrument.EngineSpeed ? "ROTOR NR  " + FormatPercent(rotor, rotorValid) + " %" :
                !Application.isPlaying ? "EDITOR PREVIEW" : !leftValid && !rightValid ? "DATA UNAVAILABLE" : "";
            Label("Footer", footer, new(0, -106), new(178, 18), 10, Quiet);
        }

        private void RefreshVerticalLabels()
        {
            Label("Title", "VERTICAL SPEED", new(0, 121), new(154, 20), 11.5f, Ink);
            Label("Units", "FT/MIN", new(0, 103), new(154, 17), 10, Quiet);
            Label("Upper", "+2,000", new(-39, 84), new(49, 16), 10, Quiet);
            Label("Upper Mid", "+1,000", new(-39, 42), new(49, 16), 10, Quiet);
            Label("Zero", "0", new(-40, 0), new(44, 16), 10, Quiet);
            Label("Lower Mid", "−1,000", new(-39, -42), new(49, 16), 10, Quiet);
            Label("Lower", "−2,000", new(-39, -84), new(49, 16), 10, Quiet);
            Label("Value", FormatVerticalSpeed(left, leftValid), new(36, 0), new(83, 30), 22, leftValid ? Ink : Quiet);
            Label("Direction", !leftValid ? "NO DATA" : Mathf.Abs(left) < 50 ? "LEVEL" : left > 0 ? "CLIMB" : "DESCENT",
                new(36, -24), new(83, 17), 9.5f, Quiet);
            Label("Footer", leftValid && Mathf.Abs(left) > 2000 ? "OFF SCALE" : !Application.isPlaying ? "EDITOR PREVIEW" : "",
                new(0, -110), new(154, 18), 10, Quiet);
        }

        private void Label(string key, string value, Vector2 position, Vector2 size, float fontSize, Color tint)
        {
            if (!labels.TryGetValue(key, out var label) || label == null)
            {
                var found = transform.Find(key);
                var go = found != null ? found.gameObject : new GameObject(key, typeof(RectTransform));
                go.transform.SetParent(transform, false);
                label = go.GetComponent<TextMeshProUGUI>() ?? go.AddComponent<TextMeshProUGUI>();
                label.font = TMP_Settings.defaultFontAsset; label.raycastTarget = false;
                label.alignment = TextAlignmentOptions.Center; label.textWrappingMode = TextWrappingModes.NoWrap;
                label.overflowMode = TextOverflowModes.Overflow; labels[key] = label;
            }
            var rt = label.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.one * .5f;
            rt.anchoredPosition = position; rt.sizeDelta = size; rt.localScale = Vector3.one;
            label.fontSize = fontSize; label.color = tint; label.text = value;
            // The legacy color manager caches the edit-preview alpha (dash
            // state) in a per-text material and CanvasRenderer. Do not multiply
            // that old alpha into a now-valid live reading.
            label.fontSharedMaterial = label.font.material;
            label.canvasRenderer.SetAlpha(1);
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (instrument == FaaEngineInstrument.VerticalSpeed)
            {
                Line(vh, new(-11, -84), new(-11, 84), 1, Quiet);
                for (int n = -4; n <= 4; n++) Line(vh, new(-11, n * 21), new(n % 2 == 0 ? -3 : -7, n * 21), 1, n == 0 ? Ink : Quiet);
                Quad(vh, new Rect(0, -15, 77, 31), Back);
                Line(vh, new(0, -15), new(77, -15), 1, Quiet);
                if (leftValid)
                {
                    float y = VerticalSpeedPosition(displayedLeft);
                    Line(vh, new(-11, 0), new(-11, y), 2.4f, Ink);
                    Line(vh, new(-9, y), new(-2, y + 4), 1.5f, Ink);
                    Line(vh, new(-9, y), new(-2, y - 4), 1.5f, Ink);
                }
                return;
            }
            EngineRail(vh, -43, displayedLeft, leftValid);
            if (engineCount > 1) EngineRail(vh, 43, displayedRight, rightValid);
            Line(vh, new(-76, 87), new(-16, 87), .7f, Quiet);
            Line(vh, new(16, 87), new(76, 87), .7f, Quiet);
        }

        private static void EngineRail(VertexHelper vh, float x, float value, bool valid)
        {
            const float bottom = -32, top = 69;
            Quad(vh, new Rect(x - 6, bottom, 12, top - bottom), Back);
            Line(vh, new(x - 6, bottom), new(x - 6, top), .8f, Quiet);
            Line(vh, new(x + 6, bottom), new(x + 6, top), .8f, Quiet);
            for (int n = 0; n <= 4; n++) Line(vh, new(x - 9, bottom + n * 21), new(x + 9, bottom + n * 21), .8f, Quiet);
            // 100% is a numeric reference, not an aircraft-specific limit.
            if (valid)
            {
                float y = bottom + Mathf.Clamp(value, 0, 120) / 100 * 84;
                Quad(vh, new Rect(x - 2, bottom, 4, Mathf.Max(1, y - bottom)), Ink * new Color(1, 1, 1, .55f));
                Line(vh, new(x - 9, y), new(x + 9, y), 2, Ink);
            }
        }

        private static void Quad(VertexHelper vh, Rect r, Color c)
        {
            int i = vh.currentVertCount;
            vh.AddVert(new Vector2(r.xMin, r.yMin), c, Vector2.zero); vh.AddVert(new Vector2(r.xMin, r.yMax), c, Vector2.zero);
            vh.AddVert(new Vector2(r.xMax, r.yMax), c, Vector2.zero); vh.AddVert(new Vector2(r.xMax, r.yMin), c, Vector2.zero);
            vh.AddTriangle(i, i + 1, i + 2); vh.AddTriangle(i, i + 2, i + 3);
        }

        private static void Line(VertexHelper vh, Vector2 a, Vector2 b, float width, Color c)
        {
            if ((a - b).sqrMagnitude < .0001f) return;
            Vector2 d = (b - a).normalized, n = new Vector2(-d.y, d.x) * width * .5f;
            int i = vh.currentVertCount;
            vh.AddVert(a - n, c, Vector2.zero); vh.AddVert(a + n, c, Vector2.zero);
            vh.AddVert(b + n, c, Vector2.zero); vh.AddVert(b - n, c, Vector2.zero);
            vh.AddTriangle(i, i + 1, i + 2); vh.AddTriangle(i, i + 2, i + 3);
        }
    }
}
