using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TrafficRadar
{
    /// <summary>Vector traffic, relative altitude tags, and non-pulsing acquisition/motion effects.</summary>
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(CanvasRenderer))]
    public sealed class RadarTrafficOverlay : MaskableGraphic
    {
        public const float TrendThresholdFpm = 500;
        public const float StaleAfterSeconds = 15;
        public const float PositionSettleSeconds = .1f;
        [SerializeField] private TrafficRadarDisplay display;
        private sealed class Track
        {
            public RadarTrafficTarget data;
            public Vector2 position, labelOffset;
            public float born, alpha;
            public bool seen, labelVisible;
            public Rect tag;
            public TMP_Text label;
        }
        private readonly Dictionary<string, Track> tracks = new();
        private readonly List<Track> visible = new();
        private readonly List<Rect> occupied = new();
        private readonly List<string> retired = new();
        private TMP_Text legend;
        private Rect legendRect;
        private float radius, iconRadius = 5;
        private bool draw;
        private static readonly Color Knockout = new(.005f, .026f, .037f, .90f);

        public void Configure(TrafficRadarDisplay owner)
        {
            if (GetComponent<CanvasRenderer>() == null) gameObject.AddComponent<CanvasRenderer>();
            display = owner; raycastTarget = false;
        }

        public static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        public static string FormatRelativeAltitude(float feet)
        {
            if (!Finite(feet)) return "—";
            // Preserve a negative sign even for traffic within 50 ft below us.
            float hundreds = Mathf.Round(Mathf.Abs(feet) / 100);
            return (feet < 0 ? "−" : "+") + hundreds.ToString("00", CultureInfo.InvariantCulture);
        }
        public static int VerticalTrend(float feetPerMinute) => !Finite(feetPerMinute) || Mathf.Abs(feetPerMinute) < TrendThresholdFpm
            ? 0 : feetPerMinute > 0 ? 1 : -1;
        public static bool IsFresh(float sampleAge, float sinceUpdate) => Finite(sampleAge) && Finite(sinceUpdate) &&
            Mathf.Max(0, sampleAge) + Mathf.Max(0, sinceUpdate) <= StaleAfterSeconds;
        public static bool InsideScope(Rect bounds, float scopeRadius)
        {
            float x = Mathf.Max(Mathf.Abs(bounds.xMin), Mathf.Abs(bounds.xMax));
            float y = Mathf.Max(Mathf.Abs(bounds.yMin), Mathf.Abs(bounds.yMax));
            return x * x + y * y <= scopeRadius * scopeRadius;
        }
        public static Vector2 SmoothPosition(Vector2 current, Vector2 next, float deltaTime) =>
            Vector2.Lerp(current, next, 1 - Mathf.Exp(-Mathf.Max(0, deltaTime) / PositionSettleSeconds));

        private void LateUpdate()
        {
            draw = display != null && display.isActiveAndEnabled && !display.UsesXPlaneTrafficTexture;
            radius = Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) * .5f;
            draw &= radius > 5;
            bool full = display != null && display.IsFullscreen;
            iconRadius = full ? 6.2f : 4.7f;
            visible.Clear(); retired.Clear();
            foreach (var track in tracks.Values) track.seen = false;
            if (draw && display.DisplayTargets != null)
            {
                for (int i = 0; i < display.DisplayTargets.Count; i++)
                {
                    var data = display.DisplayTargets[i];
                    if (data == null || !Finite(data.radarPosition.x) || !Finite(data.radarPosition.y) ||
                        !IsFresh(data.sampleAgeSeconds, display.SecondsSinceTrafficUpdate)) continue;
                    string id = !string.IsNullOrWhiteSpace(data.icao24) ? data.icao24 :
                        !string.IsNullOrWhiteSpace(data.callsign) ? data.callsign : "slot-" + i;
                    // Derive from the same true bearing/range as screen cues,
                    // using the chart's currently displayed heading and range.
                    Vector2 point = display.GetTargetDisplayPosition(data) * radius * .9f;
                    if (point.sqrMagnitude > (radius - 10) * (radius - 10)) continue;
                    if (!tracks.TryGetValue(id, out var track))
                    {
                        track = new Track { born = Time.unscaledTime, position = point,
                            label = CreateLabel("Altitude " + id), labelOffset = new(0, data.relativeAltitudeFt < 0 ? -22 : 22) };
                        tracks[id] = track;
                    }
                    track.seen = true; track.data = data;
                    // Range/format changes snap to the correct reference. No extrapolated positions.
                    track.position = point;
                    track.alpha = Application.isPlaying ? Mathf.Clamp01((Time.unscaledTime - track.born) / .25f) : 1;
                    if (data.threatLevel >= ThreatLevel.TrafficAdvisory) track.alpha = 1;
                    visible.Add(track);
                }
            }
            foreach (var pair in tracks)
            {
                if (pair.Value.seen) continue;
                // Removed/stale tracks disappear immediately; never animate a stale target as live.
                if (pair.Value.label != null)
                {
                    pair.Value.label.gameObject.SetActive(false);
                    if (Application.isPlaying) Destroy(pair.Value.label.gameObject); else DestroyImmediate(pair.Value.label.gameObject);
                }
                retired.Add(pair.Key);
            }
            foreach (var id in retired) tracks.Remove(id);
            visible.Sort((a, b) => a.data.threatLevel != b.data.threatLevel
                ? b.data.threatLevel.CompareTo(a.data.threatLevel) : a.data.distanceNM.CompareTo(b.data.distanceNM));
            PlaceLabels(full);
            SetVerticesDirty();
        }

        private void PlaceLabels(bool full)
        {
            if (legend == null) legend = CreateLabel("Altitude Legend");
            bool tags = draw && display.ShowAltitudeLabels && visible.Count > 0;
            occupied.Clear();
            occupied.Add(new Rect(-18, -18, 36, 36)); // Ownship must remain unobscured.
            float legendWidth = Mathf.Min(208, radius * 1.3f);
            legendRect = new Rect(-legendWidth * .5f, -radius + 48, legendWidth, 40);
            occupied.Add(legendRect);
            foreach (var track in visible) occupied.Add(new Rect(track.position - Vector2.one * 8, Vector2.one * 16));
            int hidden = 0;
            foreach (var track in visible)
            {
                bool validAltitude = Finite(track.data.relativeAltitudeFt) && Finite(track.data.altitudeFt);
                string text = validAltitude ? FormatRelativeAltitude(track.data.relativeAltitudeFt) : "—";
                float width = Mathf.Max(full ? 43 : 36, text.Length * (full ? 8 : 6.6f) + 12);
                Vector2 size = new(width, full ? 20 : 17);
                track.labelVisible = tags && TryPlace(track, size, out track.tag);
                track.label.gameObject.SetActive(track.labelVisible);
                if (!track.labelVisible) { if (tags) hidden++; continue; }
                occupied.Add(track.tag);
                track.labelOffset = track.tag.center - track.position;
                track.label.rectTransform.anchoredPosition = track.tag.center;
                track.label.rectTransform.sizeDelta = size;
                track.label.fontSize = full ? 13 : 11;
                track.label.text = text;
                track.label.color = TrafficInk(track);
            }
            legend.gameObject.SetActive(tags);
            legend.rectTransform.anchoredPosition = legendRect.center;
            legend.rectTransform.sizeDelta = legendRect.size;
            legend.fontSize = full ? 10.5f : 9;
            legend.color = new(.65f, .88f, .88f, .9f);
            legend.text = "REL ALT ×100 FT\n↑↓ ≥500 FPM" + (hidden > 0 ? "\n" + hidden + " TAGS CROWDED · FULL MAP" : "");
        }

        private bool TryPlace(Track track, Vector2 size, out Rect tag)
        {
            float sign = track.data.relativeAltitudeFt < 0 ? -1 : 1;
            // Prefer above/below the symbol. Retain a displaced tag's offset to
            // avoid jitter when nearby aircraft move by fractions of a pixel.
            float maximumOffset = display.IsFullscreen ? 62 : 42;
            float minimumSeparation = iconRadius + size.y * .5f + 6;
            if (track.labelOffset.magnitude <= maximumOffset && Candidate(track.position + track.labelOffset, size, out tag)) return true;
            for (int row = 0; row < 4; row++)
                for (int col = 0; col < 7; col++)
                {
                    int step = (col + 1) / 2 * (col % 2 == 0 ? -1 : 1);
                    Vector2 offset = new(step * (size.x + 4) * .65f, sign * (minimumSeparation + row * (size.y + 5)));
                    if (offset.magnitude > maximumOffset) continue;
                    if (Candidate(track.position + offset, size, out tag)) return true;
                }
            tag = default;
            return false;
        }

        private bool Candidate(Vector2 center, Vector2 size, out Rect r)
        {
            r = new Rect(center - size * .5f, size);
            if (!InsideScope(r, radius - 12)) return false;
            Rect padded = new(r.xMin - 2, r.yMin - 2, r.width + 4, r.height + 4);
            foreach (var other in occupied) if (padded.Overlaps(other)) return false;
            return true;
        }

        private TMP_Text CreateLabel(string labelName)
        {
            var existing = transform.Find(labelName);
            var go = existing != null ? existing.gameObject : new GameObject(labelName, typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var label = go.GetComponent<TextMeshProUGUI>() ?? go.AddComponent<TextMeshProUGUI>();
            label.font = TMP_Settings.defaultFontAsset; label.raycastTarget = false;
            label.alignment = TextAlignmentOptions.Center; label.textWrappingMode = TextWrappingModes.NoWrap;
            label.fontStyle = FontStyles.Bold; label.overflowMode = TextOverflowModes.Overflow;
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = label.rectTransform.pivot = Vector2.one * .5f;
            return label;
        }

        private static Color TrafficInk(Track track)
        {
            var c = ThreatLevelConfig.GetColor(track.data.threatLevel);
            if (track.data.threatLevel <= ThreatLevel.Proximate) c = new(.34f, .96f, .94f, 1);
            c.a = track.alpha;
            return c;
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (!draw) return;
            if (legend != null && legend.gameObject.activeSelf) Box(vh, legendRect, Knockout * new Color(1, 1, 1, .82f));
            foreach (var track in visible)
            {
                Color c = TrafficInk(track), dark = Knockout; dark.a *= track.alpha;
                if (track.labelVisible)
                {
                    Vector2 end = track.tag.center;
                    if (Vector2.Distance(track.position, end) > 26)
                        Stroke(vh, track.position, end, .65f, c * new Color(1, 1, 1, .45f));
                    Box(vh, track.tag, dark);
                }
                SymbolType shape = ThreatLevelConfig.GetSymbolType(track.data.threatLevel);
                DrawSymbol(vh, track.position, shape, iconRadius, c, dark);
                int trend = Finite(track.data.relativeAltitudeFt) ? VerticalTrend(track.data.verticalRateFpm) : 0;
                if (display.ShowAltitudeLabels && trend != 0)
                {
                    Vector2 a = track.position + new Vector2(iconRadius + 4, -5 * trend), b = a + new Vector2(0, 10 * trend);
                    Stroke(vh, a, b, 3, dark); Stroke(vh, a, b, 1.3f, c);
                    Stroke(vh, b, b + new Vector2(-2.5f, -3 * trend), 1.3f, c);
                    Stroke(vh, b, b + new Vector2(2.5f, -3 * trend), 1.3f, c);
                }
                float age = Time.unscaledTime - track.born;
                if (Application.isPlaying && age < .9f && track.data.threatLevel < ThreatLevel.TrafficAdvisory)
                {
                    // Fixed-size acquisition brackets fade once; no flashing or growing halo.
                    c.a *= .4f * (1 - Mathf.Clamp01(age / .9f));
                    float d = iconRadius + 4;
                    Stroke(vh, track.position + new Vector2(-d, d - 3), track.position + new Vector2(-d, d), .8f, c);
                    Stroke(vh, track.position + new Vector2(-d, d), track.position + new Vector2(-d + 3, d), .8f, c);
                    Stroke(vh, track.position + new Vector2(d, -d + 3), track.position + new Vector2(d, -d), .8f, c);
                    Stroke(vh, track.position + new Vector2(d, -d), track.position + new Vector2(d - 3, -d), .8f, c);
                }
            }
        }

        private static void DrawSymbol(VertexHelper vh, Vector2 p, SymbolType shape, float size, Color ink, Color dark)
        {
            int corners = shape == SymbolType.FilledCircle ? 24 : 4;
            float rotation = shape == SymbolType.FilledSquare ? 45 : 0;
            for (int i = 0; i < corners; i++)
            {
                float a = (rotation + i * 360f / corners) * Mathf.Deg2Rad;
                float b = (rotation + (i + 1) * 360f / corners) * Mathf.Deg2Rad;
                Vector2 v = p + new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * size;
                Vector2 w = p + new Vector2(Mathf.Sin(b), Mathf.Cos(b)) * size;
                if (shape != SymbolType.UnfilledDiamond)
                {
                    int n = vh.currentVertCount;
                    vh.AddVert(p, ink, Vector2.zero); vh.AddVert(v, ink, Vector2.zero); vh.AddVert(w, ink, Vector2.zero);
                    vh.AddTriangle(n, n + 1, n + 2);
                }
                Stroke(vh, v, w, 3.2f, dark); Stroke(vh, v, w, 1.25f, ink);
            }
        }
        private static void Box(VertexHelper vh, Rect r, Color c)
        {
            int n = vh.currentVertCount;
            vh.AddVert(new Vector2(r.xMin, r.yMin), c, Vector2.zero); vh.AddVert(new Vector2(r.xMin, r.yMax), c, Vector2.zero);
            vh.AddVert(new Vector2(r.xMax, r.yMax), c, Vector2.zero); vh.AddVert(new Vector2(r.xMax, r.yMin), c, Vector2.zero);
            vh.AddTriangle(n, n + 1, n + 2); vh.AddTriangle(n, n + 2, n + 3);
        }
        private static void Stroke(VertexHelper vh, Vector2 a, Vector2 b, float width, Color c)
        {
            if ((b - a).sqrMagnitude < .0001f) return;
            Vector2 d = (b - a).normalized, n = new(-d.y, d.x);
            // Coverage fringe around a crisp solid core, independent of texture resolution.
            float[] offsets = { -width * .5f - .35f, -width * .5f, width * .5f, width * .5f + .35f };
            int start = vh.currentVertCount;
            for (int j = 0; j < 4; j++)
            {
                Color tint = c; if (j == 0 || j == 3) tint.a = 0;
                vh.AddVert(a + n * offsets[j], tint, Vector2.zero); vh.AddVert(b + n * offsets[j], tint, Vector2.zero);
            }
            for (int j = 0; j < 3; j++)
            { int k = start + j * 2; vh.AddTriangle(k, k + 2, k + 3); vh.AddTriangle(k, k + 3, k + 1); }
        }
    }
}
