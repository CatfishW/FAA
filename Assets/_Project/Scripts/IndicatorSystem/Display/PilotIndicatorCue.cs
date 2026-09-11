using System.Globalization;
using FAA.Customization;
using IndicatorSystem.Core;
using UnityEngine;
using UnityEngine.UI;

namespace IndicatorSystem.Display
{
    /// <summary>Quiet, scale-independent screen cues. These are simulator aids, not TCAS/RA guidance.</summary>
    public sealed class PilotIndicatorCue : MonoBehaviour
    {
        private PilotCueSymbol _symbol;
        private RectTransform _plate;
        private Text _title, _detail, _state;
        private const float PlateWidth = 222f;

        public static PilotIndicatorCue Create(Transform parent)
        {
            var root = new GameObject("Pilot cue", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            var cue = root.AddComponent<PilotIndicatorCue>();
            var symbol = new GameObject("Vector symbol", typeof(RectTransform), typeof(PilotCueSymbol));
            symbol.transform.SetParent(root.transform, false);
            cue._symbol = symbol.GetComponent<PilotCueSymbol>();
            cue._symbol.rectTransform.sizeDelta = new Vector2(42, 42);
            cue._symbol.raycastTarget = false;
            var plate = new GameObject("Readout", typeof(RectTransform), typeof(Image));
            plate.transform.SetParent(root.transform, false);
            cue._plate = plate.GetComponent<RectTransform>();
            cue._plate.sizeDelta = new Vector2(PlateWidth, 58);
            var background = plate.GetComponent<Image>();
            background.color = new Color(0.015f, 0.055f, 0.08f, 0.83f);
            background.raycastTarget = false;
            cue._title = MakeText(plate.transform, "Identity", 12, FontStyle.Bold);
            cue._detail = MakeText(plate.transform, "Range and relative altitude", 11, FontStyle.Normal);
            cue._state = MakeText(plate.transform, "View and source", 10, FontStyle.Normal);
            LayoutText(cue._title, new Vector2(8, -5), new Vector2(PlateWidth - 16, 16));
            LayoutText(cue._detail, new Vector2(8, -23), new Vector2(PlateWidth - 16, 15));
            LayoutText(cue._state, new Vector2(8, -40), new Vector2(PlateWidth - 16, 14));
            return cue;
        }

        public void Present(IndicatorData data)
        {
            bool weather = data.Type == IndicatorType.Weather;
            bool offscreen = data.Visibility != IndicatorVisibility.OnScreen;
            bool leftLabel = data.ScreenPosition.x > Screen.width * 0.5f;
            _plate.pivot = new Vector2(leftLabel ? 1 : 0, 0.5f);
            _plate.anchoredPosition = new Vector2(leftLabel ? -30 : 30, 0);
            Color tint = weather ? new Color(0.64f, 0.84f, 1f) : new Color(0.4f, 0.94f, 0.91f);
            if (!weather && data.Priority >= 2) tint = data.Color;
            _symbol.Configure(IconFor(data), offscreen, data.ArrowRotation, tint);
            _title.text = weather ? WeatherLabel(data.WeatherKind) : AircraftLabel(data.AircraftType) + " · " + CleanLabel(data.Label, "NO ID");
            _title.color = tint;
            _detail.color = new Color(0.83f, 0.92f, 0.96f);
            _detail.text = weather
                ? data.DistanceNM.ToString("F0", CultureInfo.InvariantCulture) + " NM · " + (data.IsIllustrativeWeather || data.Label == "SIM WX" ? "SIMULATED RETURN" : "RADAR RETURN")
                : data.DistanceNM.ToString("F1", CultureInfo.InvariantCulture) + " NM  " + FormatRelativeAltitude(data.RelativeAltitudeFeet);
            _state.text = data.Visibility == IndicatorVisibility.Behind ? "BEHIND YOU · FOLLOW ARROW" : offscreen ? "OUTSIDE VIEW · FOLLOW ARROW" : "IN VIEW · TARGET AT ICON";
            _state.color = new Color(0.62f, 0.76f, 0.82f);
        }

        public static string AircraftLabel(TrafficRadar.TrafficRadarDataManager.AircraftType type)
        {
            switch (type)
            {
                case TrafficRadar.TrafficRadarDataManager.AircraftType.Commercial: return "AIRLINER";
                case TrafficRadar.TrafficRadarDataManager.AircraftType.General: return "LIGHT AIRCRAFT";
                case TrafficRadar.TrafficRadarDataManager.AircraftType.Military: return "MILITARY";
                case TrafficRadar.TrafficRadarDataManager.AircraftType.Helicopter: return "HELICOPTER";
                default: return "TYPE UNKNOWN";
            }
        }

        public static string WeatherLabel(WeatherCueKind kind) => kind == WeatherCueKind.RainLight ? "LIGHT RAIN" :
            kind == WeatherCueKind.RainModerate ? "MODERATE RAIN" : kind == WeatherCueKind.RainHeavy ? "HEAVY RAIN" : "WEATHER RETURN";

        public static FaaRadarIcon IconFor(IndicatorData data)
        {
            if (data.Type == IndicatorType.Weather)
                return data.WeatherKind == WeatherCueKind.RainLight ? FaaRadarIcon.WeatherRainLight :
                    data.WeatherKind == WeatherCueKind.RainModerate ? FaaRadarIcon.WeatherRainModerate :
                    data.WeatherKind == WeatherCueKind.RainHeavy ? FaaRadarIcon.WeatherRainHeavy : FaaRadarIcon.WeatherReturn;
            switch (data.AircraftType)
            {
                case TrafficRadar.TrafficRadarDataManager.AircraftType.Commercial: return FaaRadarIcon.AircraftAirliner;
                case TrafficRadar.TrafficRadarDataManager.AircraftType.General: return FaaRadarIcon.AircraftGeneral;
                case TrafficRadar.TrafficRadarDataManager.AircraftType.Military: return FaaRadarIcon.AircraftMilitary;
                case TrafficRadar.TrafficRadarDataManager.AircraftType.Helicopter: return FaaRadarIcon.AircraftHelicopter;
                default: return FaaRadarIcon.AircraftUnknown;
            }
        }

        public static string FormatRelativeAltitude(float feet)
        {
            if (float.IsNaN(feet) || float.IsInfinity(feet)) return "REL — FT";
            // Explicit feet, rounded to the nearest hundred; no ambiguous thousands/hundreds suffix.
            int rounded = Mathf.RoundToInt(feet / 100f) * 100;
            return "REL " + rounded.ToString("+#,0;-#,0;0", CultureInfo.InvariantCulture) + " FT";
        }

        private static string CleanLabel(string value, string fallback)
        {
            if (string.IsNullOrWhiteSpace(value)) return fallback;
            value = value.Replace("\n", " ").Replace("\r", " ").Replace("\t", " ").Trim();
            return value.Length > 8 ? value.Substring(0, 8) : value;
        }

        public static Rect ScreenBounds(IndicatorData data, float scale)
        {
            bool left = data.ScreenPosition.x > Screen.width * 0.5f;
            return new Rect(data.ScreenPosition.x - (left ? 258f : 27f) * scale,
                data.ScreenPosition.y - 34f * scale, 285f * scale, 68f * scale);
        }

        internal static Text MakeText(Transform parent, string name, int size, FontStyle style)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.fontStyle = style;
            text.supportRichText = false;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.alignment = TextAnchor.MiddleLeft;
            return text;
        }

        internal static void LayoutText(Text text, Vector2 position, Vector2 size)
        {
            var rt = text.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
        }
    }

    /// <summary>SVG aircraft/weather pictogram with a separate edge-direction arrow. No blinking or spinning.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class PilotCueSymbol : MaskableGraphic
    {
        private FaaRadarIcon _icon;
        private bool _offscreen;
        private float _angle;
        private static FaaSvgIconLibrary _library;
        public void Configure(bool weather, bool offscreen, float angle, Color tint)
            => Configure(weather ? FaaRadarIcon.WeatherRainModerate : FaaRadarIcon.AircraftAirliner, offscreen, angle, tint);

        public void Configure(FaaRadarIcon icon, bool offscreen, float angle, Color tint)
        {
            if (_icon == icon && _offscreen == offscreen && Mathf.Abs(Mathf.DeltaAngle(_angle, angle)) < 0.1f && color == tint) return;
            _icon = icon; _offscreen = offscreen; _angle = angle; color = tint;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (_library == null) _library = Resources.Load<FaaSvgIconLibrary>(FaaSvgIconLibrary.ResourcePath);
            var entry = _library != null ? _library.Find(_icon) : null;
            float scale = Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) / 42f;
            if (entry != null)
            {
                foreach (var point in entry.vertices) vh.AddVert(rectTransform.rect.center + point * (1.18f * scale), color, Vector2.zero);
                for (int i = 0; i < entry.triangles.Length; i += 3) vh.AddTriangle(entry.triangles[i], entry.triangles[i+1], entry.triangles[i+2]);
            }
            if (_offscreen)
            {
                Quaternion rotation = Quaternion.Euler(0, 0, -_angle);
                Stroke(vh, rotation * (new Vector2(-5, 16) * scale), rotation * (new Vector2(0, 21) * scale), color);
                Stroke(vh, rotation * (new Vector2(0, 21) * scale), rotation * (new Vector2(5, 16) * scale), color);
            }
        }

        private static void Stroke(VertexHelper vh, Vector2 a, Vector2 b, Color tint)
        {
            Vector2 n = new Vector2(-(b-a).y, (b-a).x).normalized * 0.9f;
            int start = vh.currentVertCount;
            vh.AddVert(a - n, tint, Vector2.zero); vh.AddVert(a + n, tint, Vector2.zero);
            vh.AddVert(b + n, tint, Vector2.zero); vh.AddVert(b - n, tint, Vector2.zero);
            vh.AddTriangle(start, start+1, start+2); vh.AddTriangle(start, start+2, start+3);
        }
    }
}
