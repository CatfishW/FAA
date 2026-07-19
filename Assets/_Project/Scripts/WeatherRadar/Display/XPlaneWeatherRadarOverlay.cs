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
        private const int NativeTextureWidth = 724;
        private const int NativeTextureHeight = 512;
        private const int OverlaySupersample = 2;
        private const int BearingLabelScale = 5;
        private const int InnerRangeLabelScale = 4;
        private const int OuterRangeLabelScale = 5;

        [Header("References")]
        [SerializeField] private RawImage overlayImage;
        [SerializeField] private WeatherRadarDataProvider dataProvider;

        [Header("Texture")]
        [SerializeField] private int textureWidth = NativeTextureWidth * OverlaySupersample;
        [SerializeField] private int textureHeight = NativeTextureHeight * OverlaySupersample;

        [Header("Symbology")]
        [SerializeField] private int rangeRingCount = 4;
        [SerializeField] private float sectorHalfAngleDegrees = 55f;
        [SerializeField] private float originHeightRatio = 0.07f;
        [SerializeField] private float lineWidthPixels = 1.9f;
        [SerializeField] private float majorLineWidthPixels = 2.65f;
        [SerializeField] private Color rangeLineColor = new Color(0.82f, 0.95f, 0.84f, 0.72f);
        [SerializeField] private Color majorLineColor = new Color(0.94f, 1f, 0.95f, 1f);
        [SerializeField] private Color tickColor = new Color(0.7f, 1f, 0.74f, 0.82f);
        [SerializeField] private Color textColor = new Color(0.72f, 1f, 0.75f, 1f);
        [SerializeField] private bool drawRangeLabels = true;
        [SerializeField] private bool drawCardinalLabels = true;

        private Texture2D _overlayTexture;
        private Color32[] _pixels;
        private float _lastRange = -1f;
        private float _lastHeading = float.NaN;
        private float _renderScale = OverlaySupersample;

        public Vector2Int TextureResolution => _overlayTexture != null
            ? new Vector2Int(_overlayTexture.width, _overlayTexture.height)
            : new Vector2Int(textureWidth, textureHeight);
        public float RenderScale => _renderScale;

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
            _renderScale = CalculateRenderScale(width, height);
            EnsureTexture(width, height);
            Clear();

            WeatherRadarData radarData = dataProvider != null ? dataProvider.RadarData : null;
            float range = radarData != null ? radarData.currentRange : 160f;
            float tilt = radarData != null ? radarData.tiltAngle : 0f;
            float heading = radarData != null ? radarData.heading : 0f;
            RadarMode mode = radarData != null ? radarData.currentMode : RadarMode.WX;
            _lastRange = range;
            _lastHeading = heading;

            int originX = width / 2;
            int originY = Mathf.RoundToInt(height * Mathf.Clamp01(originHeightRatio));
            float radius = Mathf.Min(height - originY - ScalePixels(18f), width * 0.608f);
            float halfAngle = Mathf.Clamp(sectorHalfAngleDegrees, 35f, 85f);
            int rings = Mathf.Clamp(rangeRingCount, 2, 6);

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
            DrawBearingSpoke(originX, originY, radius, -30f, rangeLineColor, lineWidthPixels * 0.7f);
            DrawBearingSpoke(originX, originY, radius, 30f, rangeLineColor, lineWidthPixels * 0.7f);
            DrawBearingSpoke(originX, originY, radius, -halfAngle, rangeLineColor, lineWidthPixels);
            DrawBearingSpoke(originX, originY, radius, halfAngle, rangeLineColor, lineWidthPixels);

            DrawOuterBearingTicks(originX, originY, radius, halfAngle, heading);
            DrawAircraftReference(originX, originY);
            // Mode/range/tilt/source are rendered by the panel's high-resolution
            // TextMesh Pro readouts. Avoid drawing a second tiny bitmap legend.

            Apply();
        }

        private void ApplyRequestedOverlayDefaults()
        {
            textureWidth = NativeTextureWidth * OverlaySupersample;
            textureHeight = NativeTextureHeight * OverlaySupersample;
            rangeRingCount = Mathf.Clamp(rangeRingCount, 4, 4);
            sectorHalfAngleDegrees = 55f;
            originHeightRatio = 0.07f;
            lineWidthPixels = 1.9f;
            majorLineWidthPixels = 2.65f;
            rangeLineColor = new Color(0.82f, 0.95f, 0.84f, 0.72f);
            majorLineColor = new Color(0.94f, 1f, 0.95f, 1f);
            tickColor = new Color(0.7f, 1f, 0.74f, 0.82f);
            textColor = new Color(0.72f, 1f, 0.75f, 1f);
            drawRangeLabels = true;
            drawCardinalLabels = true;
        }

        public static float CalculateRenderScale(int width, int height)
        {
            float widthScale = Mathf.Max(1, width) / (float)NativeTextureWidth;
            float heightScale = Mathf.Max(1, height) / (float)NativeTextureHeight;
            return Mathf.Max(0.25f, Mathf.Min(widthScale, heightScale));
        }

        private float ScalePixels(float value)
        {
            return value * _renderScale;
        }

        private void Update()
        {
            if (dataProvider == null)
            {
                return;
            }

            float range = dataProvider.RadarData.currentRange;
            float heading = dataProvider.RadarData.heading;
            if (!Mathf.Approximately(range, _lastRange) ||
                float.IsNaN(_lastHeading) ||
                Mathf.Abs(Mathf.DeltaAngle(_lastHeading, heading)) >= 0.5f)
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
            Redraw();
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
            float edge = ScalePixels(1f);
            float inset = ScalePixels(2f);
            DrawLine(edge, edge, width - inset, edge, rangeLineColor, lineWidthPixels * 0.75f);
            DrawLine(edge, height - inset, width - inset, height - inset, rangeLineColor, lineWidthPixels * 0.75f);
            DrawLine(edge, edge, edge, height - inset, rangeLineColor, lineWidthPixels * 0.75f);
            DrawLine(width - inset, edge, width - inset, height - inset, rangeLineColor, lineWidthPixels * 0.75f);

            float corner = Mathf.Min(width, height) * 0.045f;
            DrawLine(edge, corner, corner, edge, majorLineColor, lineWidthPixels);
            DrawLine(width - corner, edge, width - inset, corner, majorLineColor, lineWidthPixels);
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

        private void DrawOuterBearingTicks(float cx, float cy, float radius, float halfAngle, float heading)
        {
            float firstBearing = Mathf.Ceil(-halfAngle / 10f) * 10f;
            float lastBearing = Mathf.Floor(halfAngle / 10f) * 10f;
            for (float bearing = firstBearing; bearing <= lastBearing; bearing += 10f)
            {
                bool major = Mathf.Approximately(bearing, 0f) ||
                             Mathf.Approximately(Mathf.Repeat(Mathf.Abs(bearing), 30f), 0f);
                float tickLength = ScalePixels(major ? 18f : 11f);
                Vector2 outer = PointOnBearing(cx, cy, radius, bearing);
                Vector2 inner = PointOnBearing(cx, cy, radius - tickLength, bearing);
                DrawLine(inner.x, inner.y, outer.x, outer.y, tickColor, major ? majorLineWidthPixels : lineWidthPixels);

                if (drawCardinalLabels)
                {
                    string label = FormatHeadingLabel(heading, bearing);
                    Vector2 labelPoint = PointOnBearing(cx, cy, radius - tickLength - ScalePixels(18f), bearing);
                    const int labelScale = BearingLabelScale;
                    DrawTinyText(
                        labelPoint.x - MeasureTinyTextWidth(label, labelScale) * 0.5f,
                        labelPoint.y - MeasureTinyTextHeight(labelScale) * 0.5f,
                        label,
                        textColor,
                        labelScale);
                }
            }

            Vector2 leftEdge = PointOnBearing(cx, cy, radius, -halfAngle);
            Vector2 leftInner = PointOnBearing(cx, cy, radius - ScalePixels(26f), -halfAngle);
            DrawLine(leftInner.x, leftInner.y, leftEdge.x, leftEdge.y, majorLineColor, majorLineWidthPixels);

            Vector2 rightEdge = PointOnBearing(cx, cy, radius, halfAngle);
            Vector2 rightInner = PointOnBearing(cx, cy, radius - ScalePixels(26f), halfAngle);
            DrawLine(rightInner.x, rightInner.y, rightEdge.x, rightEdge.y, majorLineColor, majorLineWidthPixels);
        }

        private void DrawRangeScaleTicks(float cx, float cy, float radius, int rings)
        {
            for (int i = 1; i <= rings; i++)
            {
                float y = cy + radius * i / rings;
                DrawLine(cx - ScalePixels(8f), y, cx + ScalePixels(8f), y, majorLineColor, lineWidthPixels);
            }
        }

        private void DrawAircraftReference(float cx, float cy)
        {
            DrawLine(cx, cy + ScalePixels(21f), cx - ScalePixels(10f), cy + ScalePixels(2f), majorLineColor, majorLineWidthPixels);
            DrawLine(cx, cy + ScalePixels(21f), cx + ScalePixels(10f), cy + ScalePixels(2f), majorLineColor, majorLineWidthPixels);
            DrawLine(cx - ScalePixels(17f), cy, cx + ScalePixels(17f), cy, majorLineColor, majorLineWidthPixels);
            DrawLine(cx, cy - ScalePixels(3f), cx, cy - ScalePixels(18f), tickColor, lineWidthPixels);
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
                DrawLine(x, cy - ScalePixels(5f), x, cy + ScalePixels(5f), tickColor, lineWidthPixels * 0.75f);
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
                DrawLine(x, midY - ScalePixels(4f), x, midY + ScalePixels(4f), tickColor, lineWidthPixels * 0.55f);
            }

            DrawTinyText(cx - ScalePixels(15f), innerY - ScalePixels(16f), "1/4", textColor, 1);
            DrawTinyText(cx - ScalePixels(15f), midY - ScalePixels(16f), "1/2", textColor, 1);
            DrawTinyText(cx - ScalePixels(15f), outerY - ScalePixels(16f), "3/4", textColor, 1);
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

            float notch = ScalePixels(14f);
            DrawLine(cx - notch, boxTop - notch, cx, boxTop, centerColor, lineWidthPixels * 0.72f);
            DrawLine(cx + notch, boxTop - notch, cx, boxTop, centerColor, lineWidthPixels * 0.72f);
        }

        private void DrawRangeLabel(float cx, float cy, float ringRadius, float rangeNm, bool outer)
        {
            int labelScale = outer ? OuterRangeLabelScale : InnerRangeLabelScale;
            float labelX = cx + ScalePixels(10f);
            float labelY = cy + ringRadius - MeasureTinyTextHeight(labelScale) - ScalePixels(7f);
            string label = outer ? $"{Mathf.RoundToInt(rangeNm)}NM" : Mathf.RoundToInt(rangeNm).ToString();
            DrawTinyText(labelX, labelY, label, outer ? majorLineColor : textColor, labelScale);
        }

        private void DrawModeLegend(int width, int height, float rangeNm, float tiltDegrees, float heading, RadarMode mode)
        {
            const int legendScale = 2;
            string sourceLabel = "X-PLANE";
            string headingLabel = $"HDG {Mathf.RoundToInt(Mathf.Repeat(heading, 360f)):000}";
            string modeLabel = GetModeDisplay(mode);
            float topY = height - MeasureTinyTextHeight(legendScale) - ScalePixels(7f);
            DrawTinyText(ScalePixels(10f), topY, sourceLabel, majorLineColor, legendScale);
            DrawTinyText(
                width * 0.5f - MeasureTinyTextWidth(headingLabel, legendScale) * 0.5f,
                topY,
                headingLabel,
                majorLineColor,
                legendScale);
            DrawTinyText(
                width - MeasureTinyTextWidth(modeLabel, legendScale) - ScalePixels(10f),
                topY,
                modeLabel,
                majorLineColor,
                legendScale);
            string sign = tiltDegrees >= 0f ? "+" : string.Empty;
            string tiltLabel = $"TLT {sign}{tiltDegrees:0.0}";
            DrawTinyText(
                width - MeasureTinyTextWidth(tiltLabel, legendScale) - ScalePixels(10f),
                ScalePixels(9f),
                tiltLabel,
                textColor,
                legendScale);
            DrawTinyText(ScalePixels(10f), ScalePixels(9f), $"RNG {Mathf.RoundToInt(rangeNm)}NM", textColor, legendScale);
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

        public static string FormatHeadingLabel(float heading, float relativeBearing)
        {
            int degrees = Mathf.RoundToInt(Mathf.Repeat(heading + relativeBearing, 360f) / 10f) * 10;
            degrees %= 360;
            switch (degrees)
            {
                case 0: return "N";
                case 90: return "E";
                case 180: return "S";
                case 270: return "W";
                default: return (degrees / 10).ToString("00");
            }
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
            float scaledWidth = Mathf.Max(0.75f, ScalePixels(width));
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(new Vector2(x0, y0), new Vector2(x1, y1))));
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                float x = Mathf.Lerp(x0, x1, t);
                float y = Mathf.Lerp(y0, y1, t);
                DrawBrush(x, y, color, scaledWidth);
            }
        }

        private void DrawDashedLine(float x0, float y0, float x1, float y1, Color color, float width, float dashLength, float gapLength)
        {
            float distance = Vector2.Distance(new Vector2(x0, y0), new Vector2(x1, y1));
            int steps = Mathf.Max(1, Mathf.CeilToInt(distance));
            float scaledDash = ScalePixels(dashLength);
            float scaledGap = ScalePixels(gapLength);
            float scaledWidth = Mathf.Max(0.75f, ScalePixels(width));
            float cycle = Mathf.Max(1f, scaledDash + scaledGap);
            for (int i = 0; i <= steps; i++)
            {
                float travelled = distance * i / steps;
                if (Mathf.Repeat(travelled, cycle) > scaledDash)
                {
                    continue;
                }

                float t = i / (float)steps;
                float x = Mathf.Lerp(x0, x1, t);
                float y = Mathf.Lerp(y0, y1, t);
                DrawBrush(x, y, color, scaledWidth);
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
            int pixelScale = ResolveTinyTextPixelScale(scale);
            string upper = text.ToUpperInvariant();
            for (int i = 0; i < upper.Length; i++)
            {
                DrawGlyph(cursor, baseline, upper[i], color, pixelScale);
                cursor += 4 * pixelScale;
            }
        }

        private int ResolveTinyTextPixelScale(int scale)
        {
            return Mathf.Clamp(Mathf.RoundToInt((scale + 1) * _renderScale), 1, 16);
        }

        private float MeasureTinyTextWidth(string text, int scale)
        {
            return string.IsNullOrEmpty(text) ? 0f : text.Length * 4f * ResolveTinyTextPixelScale(scale);
        }

        private float MeasureTinyTextHeight(int scale)
        {
            return 5f * ResolveTinyTextPixelScale(scale);
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
                case 'D': return new[] { "110", "101", "101", "101", "110" };
                case 'E': return new[] { "111", "100", "111", "100", "111" };
                case 'G': return new[] { "111", "100", "101", "101", "111" };
                case 'H': return new[] { "101", "101", "111", "101", "101" };
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
