using UnityEngine;
using UnityEngine.UI;

namespace WeatherRadar
{
    /// <summary>
    /// Thin reference symbology drawn over the preserved X-Plane weather radar PNG.
    /// The overlay is transparent and never modifies or recolors weather returns.
    /// </summary>
    [AddComponentMenu("Weather Radar/Display/X-Plane Weather Radar Overlay")]
    public class XPlaneWeatherRadarOverlay : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private RawImage overlayImage;
        [SerializeField] private WeatherRadarDataProvider dataProvider;

        [Header("Texture")]
        [SerializeField] private int textureWidth = 724;
        [SerializeField] private int textureHeight = 512;

        [Header("Symbology")]
        [SerializeField] private int rangeRingCount = 4;
        [SerializeField] private float sectorHalfAngleDegrees = 64f;
        [SerializeField] private float originHeightRatio = 0.078f;
        [SerializeField] private float lineWidthPixels = 0.62f;
        [SerializeField] private float majorLineWidthPixels = 0.92f;
        [SerializeField] private Color rangeLineColor = new Color(0.2f, 1f, 0.25f, 0.26f);
        [SerializeField] private Color majorLineColor = new Color(0.58f, 1f, 0.6f, 0.58f);
        [SerializeField] private Color tickColor = new Color(0.55f, 1f, 0.58f, 0.42f);
        [SerializeField] private Color textColor = new Color(0.66f, 1f, 0.68f, 0.72f);
        [SerializeField] private bool drawRangeLabels = true;
        [SerializeField] private bool drawCardinalLabels = true;

        private Texture2D _overlayTexture;
        private Color32[] _pixels;
        private float _lastRange = -1f;

        private void Awake()
        {
            AutoFindReferences();
            Redraw();
        }

        private void OnEnable()
        {
            AutoFindReferences();
            Subscribe();
            Redraw();
        }

        private void Start()
        {
            AutoFindReferences();
            Subscribe();
            Redraw();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void OnDestroy()
        {
            Unsubscribe();

            if (_overlayTexture != null)
            {
                Destroy(_overlayTexture);
                _overlayTexture = null;
            }
        }

        public void SetDataProvider(WeatherRadarDataProvider provider)
        {
            if (ReferenceEquals(dataProvider, provider))
            {
                return;
            }

            Unsubscribe();
            dataProvider = provider;
            Subscribe();
            Redraw();
        }

        public void SetOverlayImage(RawImage image)
        {
            overlayImage = image;
            ApplyTexture();
        }

        [ContextMenu("Redraw Weather Radar Overlay")]
        public void Redraw()
        {
            ApplyRequestedOverlayDefaults();

            int width = Mathf.Clamp(textureWidth, 128, 2048);
            int height = Mathf.Clamp(textureHeight, 128, 2048);
            EnsureTexture(width, height);
            Clear();

            WeatherRadarData radarData = dataProvider != null ? dataProvider.RadarData : null;
            float range = radarData != null ? radarData.currentRange : 160f;
            float tilt = radarData != null ? radarData.tiltAngle : 0f;
            RadarMode mode = radarData != null ? radarData.currentMode : RadarMode.WX;
            _lastRange = range;

            int originX = width / 2;
            int originY = Mathf.RoundToInt(height * Mathf.Clamp01(originHeightRatio));
            float radius = Mathf.Min(height - originY - 8f, width * 0.535f);
            float halfAngle = Mathf.Clamp(sectorHalfAngleDegrees, 35f, 85f);
            int rings = Mathf.Clamp(rangeRingCount, 2, 6);

            DrawBorder(width, height);

            for (int i = 1; i <= rings; i++)
            {
                float ringRadius = radius * i / rings;
                DrawSectorArc(originX, originY, ringRadius, -halfAngle, halfAngle, rangeLineColor, lineWidthPixels);
                if (drawRangeLabels)
                {
                    DrawRangeLabel(originX, originY, ringRadius, range * i / rings, i == rings);
                }
            }

            DrawBearingSpoke(originX, originY, radius, 0f, majorLineColor, majorLineWidthPixels);
            DrawBearingSpoke(originX, originY, radius, -15f, rangeLineColor, lineWidthPixels * 0.72f);
            DrawBearingSpoke(originX, originY, radius, 15f, rangeLineColor, lineWidthPixels * 0.72f);
            DrawBearingSpoke(originX, originY, radius, -30f, rangeLineColor, lineWidthPixels);
            DrawBearingSpoke(originX, originY, radius, 30f, rangeLineColor, lineWidthPixels);
            DrawBearingSpoke(originX, originY, radius, -60f, rangeLineColor, lineWidthPixels);
            DrawBearingSpoke(originX, originY, radius, 60f, rangeLineColor, lineWidthPixels);

            DrawOuterBearingTicks(originX, originY, radius, halfAngle);
            DrawCrossRangeScale(originX, originY, radius);
            DrawAircraftReference(originX, originY);
            DrawRangeScaleTicks(originX, originY, radius, rings);
            DrawAzimuthGrid(originX, originY, radius, halfAngle);
            DrawWeatherRadarScanRails(originX, originY, radius, halfAngle);
            DrawWeatherRadarCourseBox(originX, originY, radius);
            DrawModeLegend(width, height, range, tilt, mode);

            Apply();
        }

        private void ApplyRequestedOverlayDefaults()
        {
            textureWidth = Mathf.Clamp(textureWidth, 724, 724);
            textureHeight = Mathf.Clamp(textureHeight, 512, 512);
            rangeRingCount = Mathf.Clamp(rangeRingCount, 4, 4);
            sectorHalfAngleDegrees = 64f;
            originHeightRatio = 0.078f;
            lineWidthPixels = 0.62f;
            majorLineWidthPixels = 0.92f;
            rangeLineColor = new Color(0.2f, 1f, 0.25f, 0.26f);
            majorLineColor = new Color(0.58f, 1f, 0.6f, 0.58f);
            tickColor = new Color(0.55f, 1f, 0.58f, 0.42f);
            textColor = new Color(0.66f, 1f, 0.68f, 0.72f);
            drawRangeLabels = true;
            drawCardinalLabels = true;
        }

        private void Update()
        {
            if (dataProvider == null)
            {
                return;
            }

            float range = dataProvider.RadarData.currentRange;
            if (!Mathf.Approximately(range, _lastRange))
            {
                Redraw();
            }

            if (overlayImage != null)
            {
                overlayImage.enabled = true;
            }
        }

        private void AutoFindReferences()
        {
            if (overlayImage == null)
            {
                overlayImage = GetComponent<RawImage>();
            }

            if (dataProvider == null)
            {
                dataProvider = GetComponentInParent<WeatherRadarDataProvider>();
            }

            if (overlayImage != null)
            {
                overlayImage.raycastTarget = false;
                if (_overlayTexture == null)
                {
                    overlayImage.color = Color.clear;
                }
            }
        }

        private void Subscribe()
        {
            if (dataProvider == null)
            {
                return;
            }

            dataProvider.OnRangeChanged -= OnRangeChanged;
            dataProvider.OnRangeChanged += OnRangeChanged;
            dataProvider.OnModeChanged -= OnModeChanged;
            dataProvider.OnModeChanged += OnModeChanged;
        }

        private void Unsubscribe()
        {
            if (dataProvider == null)
            {
                return;
            }

            dataProvider.OnRangeChanged -= OnRangeChanged;
            dataProvider.OnModeChanged -= OnModeChanged;
        }

        private void OnRangeChanged(float rangeNm)
        {
            Redraw();
        }

        private void OnModeChanged(RadarMode mode)
        {
            if (overlayImage != null)
            {
                overlayImage.enabled = true;
            }
        }

        private void EnsureTexture(int width, int height)
        {
            if (_overlayTexture != null && _overlayTexture.width == width && _overlayTexture.height == height && _pixels != null)
            {
                return;
            }

            if (_overlayTexture != null)
            {
                Destroy(_overlayTexture);
            }

            _overlayTexture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "XPlaneWeatherRadarReferenceOverlay",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            _pixels = new Color32[width * height];
            ApplyTexture();
        }

        private void ApplyTexture()
        {
            if (overlayImage != null && _overlayTexture != null)
            {
                overlayImage.texture = _overlayTexture;
                overlayImage.color = Color.white;
                overlayImage.enabled = true;
                overlayImage.raycastTarget = false;
            }
            else if (overlayImage != null)
            {
                overlayImage.color = Color.clear;
                overlayImage.raycastTarget = false;
            }
        }

        private void Clear()
        {
            if (_pixels == null)
            {
                return;
            }

            Color32 clear = new Color32(0, 0, 0, 0);
            for (int i = 0; i < _pixels.Length; i++)
            {
                _pixels[i] = clear;
            }
        }

        private void Apply()
        {
            if (_overlayTexture == null || _pixels == null)
            {
                return;
            }

            _overlayTexture.SetPixels32(_pixels);
            _overlayTexture.Apply(false);
            ApplyTexture();
        }

        private void DrawBorder(int width, int height)
        {
            DrawLine(1f, 1f, width - 2f, 1f, rangeLineColor, lineWidthPixels * 0.75f);
            DrawLine(1f, height - 2f, width - 2f, height - 2f, rangeLineColor, lineWidthPixels * 0.75f);
            DrawLine(1f, 1f, 1f, height - 2f, rangeLineColor, lineWidthPixels * 0.75f);
            DrawLine(width - 2f, 1f, width - 2f, height - 2f, rangeLineColor, lineWidthPixels * 0.75f);

            float corner = Mathf.Min(width, height) * 0.045f;
            DrawLine(1f, corner, corner, 1f, majorLineColor, lineWidthPixels);
            DrawLine(width - corner, 1f, width - 2f, corner, majorLineColor, lineWidthPixels);
        }

        private void DrawSectorArc(float cx, float cy, float radius, float fromDegrees, float toDegrees, Color color, float width)
        {
            Vector2? previous = null;
            for (float angle = fromDegrees; angle <= toDegrees; angle += 0.7f)
            {
                Vector2 point = PointOnBearing(cx, cy, radius, angle);
                if (previous.HasValue)
                {
                    DrawLine(previous.Value.x, previous.Value.y, point.x, point.y, color, width);
                }
                previous = point;
            }
        }

        private void DrawBearingSpoke(float cx, float cy, float radius, float bearingDegrees, Color color, float width)
        {
            Vector2 end = PointOnBearing(cx, cy, radius, bearingDegrees);
            DrawLine(cx, cy, end.x, end.y, color, width);
        }

        private void DrawAzimuthGrid(float cx, float cy, float radius, float halfAngle)
        {
            Color gridColor = new Color(rangeLineColor.r, rangeLineColor.g, rangeLineColor.b, rangeLineColor.a * 0.58f);
            float clampedHalfAngle = Mathf.Min(halfAngle, 64f);
            for (float bearing = -50f; bearing <= 50f; bearing += 10f)
            {
                if (Mathf.Approximately(Mathf.Repeat(Mathf.Abs(bearing), 30f), 0f))
                {
                    continue;
                }

                Vector2 inner = PointOnBearing(cx, cy, radius * 0.18f, bearing);
                Vector2 outer = PointOnBearing(cx, cy, radius * 0.96f, Mathf.Clamp(bearing, -clampedHalfAngle, clampedHalfAngle));
                DrawDashedLine(inner.x, inner.y, outer.x, outer.y, gridColor, lineWidthPixels * 0.46f, 7f, 15f);
            }
        }

        private void DrawOuterBearingTicks(float cx, float cy, float radius, float halfAngle)
        {
            for (float bearing = -60f; bearing <= 60f; bearing += 10f)
            {
                bool major = Mathf.Approximately(Mathf.Repeat(Mathf.Abs(bearing), 30f), 0f);
                float tickLength = major ? 18f : 9f;
                Vector2 outer = PointOnBearing(cx, cy, radius, bearing);
                Vector2 inner = PointOnBearing(cx, cy, radius - tickLength, bearing);
                DrawLine(inner.x, inner.y, outer.x, outer.y, tickColor, major ? majorLineWidthPixels : lineWidthPixels);

                if (drawCardinalLabels && major)
                {
                    Vector2 labelPoint = PointOnBearing(cx, cy, radius - tickLength - 18f, bearing);
                    DrawTinyText(labelPoint.x - 8f, labelPoint.y - 4f, FormatBearingLabel(bearing), textColor, 1);
                }
            }

            Vector2 leftEdge = PointOnBearing(cx, cy, radius, -halfAngle);
            Vector2 leftInner = PointOnBearing(cx, cy, radius - 26f, -halfAngle);
            DrawLine(leftInner.x, leftInner.y, leftEdge.x, leftEdge.y, majorLineColor, majorLineWidthPixels);

            Vector2 rightEdge = PointOnBearing(cx, cy, radius, halfAngle);
            Vector2 rightInner = PointOnBearing(cx, cy, radius - 26f, halfAngle);
            DrawLine(rightInner.x, rightInner.y, rightEdge.x, rightEdge.y, majorLineColor, majorLineWidthPixels);
        }

        private void DrawRangeScaleTicks(float cx, float cy, float radius, int rings)
        {
            for (int i = 1; i <= rings; i++)
            {
                float y = cy + radius * i / rings;
                DrawLine(cx - 8f, y, cx + 8f, y, majorLineColor, lineWidthPixels);
            }
        }

        private void DrawAircraftReference(float cx, float cy)
        {
            DrawLine(cx, cy + 21f, cx - 10f, cy + 2f, majorLineColor, majorLineWidthPixels);
            DrawLine(cx, cy + 21f, cx + 10f, cy + 2f, majorLineColor, majorLineWidthPixels);
            DrawLine(cx - 17f, cy, cx + 17f, cy, majorLineColor, majorLineWidthPixels);
            DrawLine(cx, cy - 3f, cx, cy - 18f, tickColor, lineWidthPixels);
        }

        private void DrawCrossRangeScale(float cx, float cy, float radius)
        {
            for (int i = -3; i <= 3; i++)
            {
                if (i == 0)
                {
                    continue;
                }

                float x = cx + radius * i / 4f;
                DrawLine(x, cy - 5f, x, cy + 5f, tickColor, lineWidthPixels * 0.75f);
            }
        }

        private void DrawWeatherRadarScanRails(float cx, float cy, float radius, float halfAngle)
        {
            float midY = cy + radius * 0.54f;
            float innerY = cy + radius * 0.18f;
            float outerY = cy + radius * 0.84f;
            float halfChord = Mathf.Sin(halfAngle * Mathf.Deg2Rad) * radius;
            Color railColor = new Color(tickColor.r, tickColor.g, tickColor.b, tickColor.a * 0.82f);

            DrawDashedLine(cx - halfChord * 0.52f, midY, cx + halfChord * 0.52f, midY, railColor, lineWidthPixels * 0.58f, 10f, 8f);
            DrawDashedLine(cx - halfChord * 0.32f, innerY, cx + halfChord * 0.32f, innerY, railColor, lineWidthPixels * 0.48f, 8f, 10f);
            DrawDashedLine(cx - halfChord * 0.68f, outerY, cx + halfChord * 0.68f, outerY, railColor, lineWidthPixels * 0.48f, 8f, 10f);

            for (int i = -2; i <= 2; i++)
            {
                if (i == 0)
                {
                    continue;
                }

                float x = cx + halfChord * i * 0.22f;
                DrawLine(x, midY - 4f, x, midY + 4f, tickColor, lineWidthPixels * 0.55f);
            }

            DrawTinyText(cx - 12f, innerY - 13f, "1/4", textColor, 0);
            DrawTinyText(cx - 12f, midY - 13f, "1/2", textColor, 0);
            DrawTinyText(cx - 12f, outerY - 13f, "3/4", textColor, 0);
        }

        private void DrawWeatherRadarCourseBox(float cx, float cy, float radius)
        {
            float boxTop = cy + radius * 0.93f;
            float boxBottom = cy + radius * 0.08f;
            float boxHalfWidth = radius * 0.17f;
            float centerAlphaScale = 0.74f;
            Color centerColor = new Color(majorLineColor.r, majorLineColor.g, majorLineColor.b, majorLineColor.a * centerAlphaScale);
            Color bracketColor = new Color(tickColor.r, tickColor.g, tickColor.b, tickColor.a * 0.82f);

            DrawDashedLine(cx - boxHalfWidth, boxBottom, cx - boxHalfWidth, boxTop, bracketColor, lineWidthPixels * 0.48f, 9f, 12f);
            DrawDashedLine(cx + boxHalfWidth, boxBottom, cx + boxHalfWidth, boxTop, bracketColor, lineWidthPixels * 0.48f, 9f, 12f);
            DrawDashedLine(cx - boxHalfWidth, boxTop, cx + boxHalfWidth, boxTop, bracketColor, lineWidthPixels * 0.48f, 9f, 12f);
            DrawLine(cx, boxBottom, cx, boxTop, centerColor, lineWidthPixels * 0.7f);

            float notch = 14f;
            DrawLine(cx - notch, boxTop - notch, cx, boxTop, centerColor, lineWidthPixels * 0.72f);
            DrawLine(cx + notch, boxTop - notch, cx, boxTop, centerColor, lineWidthPixels * 0.72f);
        }

        private void DrawRangeLabel(float cx, float cy, float ringRadius, float rangeNm, bool outer)
        {
            float labelX = cx + 10f;
            float labelY = cy + ringRadius - 6f;
            string label = outer ? $"{Mathf.RoundToInt(rangeNm)}NM" : Mathf.RoundToInt(rangeNm).ToString();
            DrawTinyText(labelX, labelY, label, outer ? majorLineColor : textColor, outer ? 1 : 0);
        }

        private void DrawModeLegend(int width, int height, float rangeNm, float tiltDegrees, RadarMode mode)
        {
            DrawTinyText(9f, height - 16f, "X-PLANE WX", majorLineColor, 1);
            DrawTinyText(width * 0.48f, height - 16f, GetModeDisplay(mode), majorLineColor, 1);
            string sign = tiltDegrees >= 0f ? "+" : string.Empty;
            DrawTinyText(width - 74f, 10f, $"TLT {sign}{tiltDegrees:0.0}", textColor, 0);
            DrawTinyText(width - 74f, height - 16f, $"{Mathf.RoundToInt(rangeNm)}NM", textColor, 1);
        }

        private static string GetModeDisplay(RadarMode mode)
        {
            switch (mode)
            {
                case RadarMode.WX_T: return "WX+T";
                case RadarMode.TURB: return "TURB";
                case RadarMode.MAP: return "MAP";
                case RadarMode.STBY: return "STBY";
                default: return "WX";
            }
        }

        private static string FormatBearingLabel(float relativeBearing)
        {
            if (Mathf.Approximately(relativeBearing, 0f))
            {
                return "N";
            }

            return relativeBearing > 0f ? $"+{Mathf.RoundToInt(relativeBearing)}" : Mathf.RoundToInt(relativeBearing).ToString();
        }

        private static Vector2 PointOnBearing(float cx, float cy, float radius, float bearingDegrees)
        {
            float radians = bearingDegrees * Mathf.Deg2Rad;
            return new Vector2(
                cx + Mathf.Sin(radians) * radius,
                cy + Mathf.Cos(radians) * radius);
        }

        private void DrawLine(float x0, float y0, float x1, float y1, Color color, float width)
        {
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(new Vector2(x0, y0), new Vector2(x1, y1))));
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                float x = Mathf.Lerp(x0, x1, t);
                float y = Mathf.Lerp(y0, y1, t);
                DrawBrush(x, y, color, width);
            }
        }

        private void DrawDashedLine(float x0, float y0, float x1, float y1, Color color, float width, float dashLength, float gapLength)
        {
            float distance = Vector2.Distance(new Vector2(x0, y0), new Vector2(x1, y1));
            int steps = Mathf.Max(1, Mathf.CeilToInt(distance));
            float cycle = Mathf.Max(1f, dashLength + gapLength);
            for (int i = 0; i <= steps; i++)
            {
                float travelled = distance * i / steps;
                if (Mathf.Repeat(travelled, cycle) > dashLength)
                {
                    continue;
                }

                float t = i / (float)steps;
                float x = Mathf.Lerp(x0, x1, t);
                float y = Mathf.Lerp(y0, y1, t);
                DrawBrush(x, y, color, width);
            }
        }

        private void DrawBrush(float x, float y, Color color, float width)
        {
            int radius = Mathf.Max(1, Mathf.CeilToInt(width));
            for (int yy = -radius; yy <= radius; yy++)
            {
                for (int xx = -radius; xx <= radius; xx++)
                {
                    float distance = Mathf.Sqrt(xx * xx + yy * yy);
                    if (distance > width)
                    {
                        continue;
                    }

                    float alpha = color.a * Mathf.Clamp01(1f - distance / Mathf.Max(width, 0.001f));
                    SetPixel(Mathf.RoundToInt(x) + xx, Mathf.RoundToInt(y) + yy, color, alpha);
                }
            }
        }

        private void SetPixel(int x, int y, Color color, float alpha)
        {
            if (_overlayTexture == null || _pixels == null || x < 0 || x >= _overlayTexture.width || y < 0 || y >= _overlayTexture.height)
            {
                return;
            }

            int index = y * _overlayTexture.width + x;
            Color32 source = (Color32)color;
            byte nextAlpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha) * 255f);
            if (nextAlpha <= _pixels[index].a)
            {
                return;
            }

            _pixels[index] = new Color32(source.r, source.g, source.b, nextAlpha);
        }

        private void DrawTinyText(float x, float y, string text, Color color, int scale)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            int cursor = Mathf.RoundToInt(x);
            int baseline = Mathf.RoundToInt(y);
            int pixelScale = Mathf.Clamp(scale + 1, 1, 3);
            string upper = text.ToUpperInvariant();
            for (int i = 0; i < upper.Length; i++)
            {
                DrawGlyph(cursor, baseline, upper[i], color, pixelScale);
                cursor += 4 * pixelScale;
            }
        }

        private void DrawGlyph(int x, int y, char glyph, Color color, int scale)
        {
            string[] pattern = GetGlyphPattern(glyph);
            if (pattern == null)
            {
                return;
            }

            for (int row = 0; row < pattern.Length; row++)
            {
                string line = pattern[row];
                for (int col = 0; col < line.Length; col++)
                {
                    if (line[col] != '1')
                    {
                        continue;
                    }

                    for (int yy = 0; yy < scale; yy++)
                    {
                        for (int xx = 0; xx < scale; xx++)
                        {
                            SetPixel(x + col * scale + xx, y + (pattern.Length - row - 1) * scale + yy, color, color.a);
                        }
                    }
                }
            }
        }

        private static string[] GetGlyphPattern(char glyph)
        {
            switch (glyph)
            {
                case '0': return new[] { "111", "101", "101", "101", "111" };
                case '1': return new[] { "010", "110", "010", "010", "111" };
                case '2': return new[] { "111", "001", "111", "100", "111" };
                case '3': return new[] { "111", "001", "111", "001", "111" };
                case '4': return new[] { "101", "101", "111", "001", "001" };
                case '5': return new[] { "111", "100", "111", "001", "111" };
                case '6': return new[] { "111", "100", "111", "101", "111" };
                case '7': return new[] { "111", "001", "010", "010", "010" };
                case '8': return new[] { "111", "101", "111", "101", "111" };
                case '9': return new[] { "111", "101", "111", "001", "111" };
                case 'A': return new[] { "111", "101", "111", "101", "101" };
                case 'B': return new[] { "110", "101", "110", "101", "110" };
                case 'E': return new[] { "111", "100", "111", "100", "111" };
                case 'G': return new[] { "111", "100", "101", "101", "111" };
                case 'I': return new[] { "111", "010", "010", "010", "111" };
                case 'L': return new[] { "100", "100", "100", "100", "111" };
                case 'M': return new[] { "101", "111", "111", "101", "101" };
                case 'N': return new[] { "101", "111", "111", "111", "101" };
                case 'P': return new[] { "111", "101", "111", "100", "100" };
                case 'R': return new[] { "110", "101", "110", "101", "101" };
                case 'S': return new[] { "111", "100", "111", "001", "111" };
                case 'T': return new[] { "111", "010", "010", "010", "010" };
                case 'U': return new[] { "101", "101", "101", "101", "111" };
                case 'W': return new[] { "101", "101", "111", "111", "101" };
                case 'X': return new[] { "101", "101", "010", "101", "101" };
                case 'Y': return new[] { "101", "101", "010", "010", "010" };
                case '+': return new[] { "000", "010", "111", "010", "000" };
                case '-': return new[] { "000", "000", "111", "000", "000" };
                case '.': return new[] { "000", "000", "000", "000", "010" };
                case '/': return new[] { "001", "001", "010", "100", "100" };
                case ' ': return new[] { "000", "000", "000", "000", "000" };
                default: return null;
            }
        }
    }
}
