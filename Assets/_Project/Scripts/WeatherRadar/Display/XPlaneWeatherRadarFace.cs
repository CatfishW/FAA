using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WeatherRadar
{
    /// <summary>Crisp vector references. Weather returns remain on their own unmodified layer.</summary>
    [ExecuteAlways, DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class XPlaneWeatherRadarFace : MaskableGraphic
    {
        [SerializeField] private WeatherRadarDataProvider dataProvider;
        private readonly TMP_Text[] _rangeLabels = new TMP_Text[3];
        private Vector2 _lastSize;
        private float _lastRange = -1f;
        private static readonly Color Ink = new Color(.64f, .79f, .83f, .54f);

        public void Configure(WeatherRadarDataProvider provider)
        {
            if (GetComponent<CanvasRenderer>() == null) gameObject.AddComponent<CanvasRenderer>();
            dataProvider = provider;
            raycastTarget = false;
            RefreshFace();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            raycastTarget = false;
        }

        private void Update()
        {
            float range = dataProvider != null ? dataProvider.RadarData.currentRange : 160f;
            if (_rangeLabels[0] == null || _lastSize != rectTransform.rect.size || !Mathf.Approximately(range, _lastRange))
                RefreshFace();
        }

        private void RefreshFace()
        {
            _lastSize = rectTransform.rect.size;
            _lastRange = dataProvider != null ? dataProvider.RadarData.currentRange : 160f;
            Rect bounds = rectTransform.rect;
            Vector2 origin = new Vector2(bounds.center.x, bounds.yMin + bounds.height * XPlaneWeatherRadarGeometry.OriginHeight);
            float radius = bounds.height * XPlaneWeatherRadarGeometry.Radius;
            for (int i = 0; i < _rangeLabels.Length; i++)
            {
                if (_rangeLabels[i] == null)
                {
                    Transform child = transform.Find("Range " + (i + 1));
                    var go = child != null ? child.gameObject : new GameObject("Range " + (i + 1), typeof(RectTransform));
                    go.transform.SetParent(transform, false);
                    _rangeLabels[i] = go.GetComponent<TextMeshProUGUI>() ?? go.AddComponent<TextMeshProUGUI>();
                }
                TMP_Text label = _rangeLabels[i];
                label.text = XPlaneWeatherRadarGeometry.RangeAtRing(_lastRange, i + 1).ToString("0.##");
                label.fontSize = Mathf.Clamp(bounds.width / 31f, 9f, 13f);
                label.enableAutoSizing = false;
                label.alignment = TextAlignmentOptions.Center;
                label.color = new Color(.72f, .84f, .87f, .86f);
                label.faceColor = Color.white;
                label.canvasRenderer.SetColor(Color.white);
                label.raycastTarget = false;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                var rect = label.rectTransform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
                rect.sizeDelta = new Vector2(42f, 18f);
                rect.anchoredPosition = XPlaneWeatherRadarGeometry.Point(origin, radius * (i + 1) / 4f, 28f) + new Vector2(0f, 10f);
            }
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = rectTransform.rect;
            Vector2 origin = new Vector2(r.center.x, r.yMin + r.height * XPlaneWeatherRadarGeometry.OriginHeight);
            float radius = r.height * XPlaneWeatherRadarGeometry.Radius;
            float halfAngle = XPlaneWeatherRadarGeometry.HalfAngle;
            for (int ring = 1; ring <= 4; ring++)
            {
                Color ink = Ink;
                ink.a = ring == 4 ? .70f : ring == 2 ? .46f : .28f;
                float rr = radius * ring / 4f;
                Vector2 previous = XPlaneWeatherRadarGeometry.Point(origin, rr, -halfAngle);
                for (int i = 1; i <= 100; i++)
                {
                    Vector2 point = XPlaneWeatherRadarGeometry.Point(origin, rr, Mathf.Lerp(-halfAngle, halfAngle, i / 100f));
                    Line(vh, previous, point, ring == 4 ? 1.25f : .85f, ink);
                    previous = point;
                }
            }
            Color dim = new Color(Ink.r, Ink.g, Ink.b, .25f);
            foreach (float angle in new[] { -halfAngle, halfAngle })
                Line(vh, XPlaneWeatherRadarGeometry.Point(origin, radius * .08f, angle), XPlaneWeatherRadarGeometry.Point(origin, radius, angle), .8f, dim);
            // The forward reference is deliberately neutral: not an invented navigation course.
            Line(vh, origin + Vector2.up * 14f, origin + Vector2.up * radius, .8f, dim);
            for (int angle = -50; angle <= 50; angle += 10)
                Line(vh, XPlaneWeatherRadarGeometry.Point(origin, radius - (angle % 30 == 0 ? 7f : 4f), angle), XPlaneWeatherRadarGeometry.Point(origin, radius, angle), 1f, Ink);
            Color aircraft = new Color(.82f, .96f, .94f, 1f);
            Line(vh, origin + Vector2.up * 8f, origin - Vector2.up * 7f, 1.6f, aircraft);
            Line(vh, origin + new Vector2(-8f, -1f), origin + Vector2.up * 2f, 1.6f, aircraft);
            Line(vh, origin + Vector2.up * 2f, origin + new Vector2(8f, -1f), 1.6f, aircraft);
            Line(vh, origin + new Vector2(-4f, -6f), origin + new Vector2(4f, -6f), 1.4f, aircraft);
        }

        private static void Line(VertexHelper vh, Vector2 a, Vector2 b, float width, Color tint)
        {
            Vector2 normal = new Vector2(-(b - a).y, (b - a).x).normalized * width * .5f;
            int start = vh.currentVertCount;
            vh.AddVert(a - normal, tint, Vector2.zero);
            vh.AddVert(a + normal, tint, Vector2.zero);
            vh.AddVert(b + normal, tint, Vector2.zero);
            vh.AddVert(b - normal, tint, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
