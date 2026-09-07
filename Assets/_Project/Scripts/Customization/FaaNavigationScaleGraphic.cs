using HUDControl.Elements;
using TMPro;
using TrafficRadar;
using UnityEngine;
using UnityEngine.UI;

namespace FAA.Customization
{
    /// <summary>
    /// Vector navigation scale. Map targets have no vertical-flight-path data:
    /// their vertical scale is explicitly along-track position, never glideslope.
    /// An actual LOC/GS deviation source takes precedence when supplied.
    /// </summary>
    [ExecuteAlways, RequireComponent(typeof(CanvasRenderer))]
    public sealed class FaaNavigationScaleGraphic : MaskableGraphic
    {
        [SerializeField] private bool vertical;
        [SerializeField] private LocalizerElement localizer;
        [SerializeField] private GlidescopeElement glideslope;
        [SerializeField] private TrafficRadarDisplay navigationDisplay;
        private TMP_Text title;
        private TMP_Text detail;
        private TMP_Text positiveLabel;
        private TMP_Text negativeLabel;
        private bool hasGuidance;
        private bool mapTarget;
        private bool clamped;
        private float position;

        public void Configure(bool isVertical, LocalizerElement lateral, GlidescopeElement glide, TrafficRadarDisplay radar)
        {
            vertical = isVertical;
            localizer = lateral;
            glideslope = glide;
            navigationDisplay = radar;
            raycastTarget = false;
            Refresh();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            raycastTarget = false;
        }

        private void Update() => Refresh();

        private void Refresh()
        {
            if (navigationDisplay == null && Application.isPlaying)
                navigationDisplay = FindAnyObjectByType<TrafficRadarDisplay>();
            bool deviationAvailable = vertical ? glideslope != null && glideslope.HasDeviationData :
                localizer != null && localizer.HasDeviationData;
            mapTarget = !deviationAvailable && navigationDisplay != null &&
                navigationDisplay.HasNavigationTarget && navigationDisplay.ShowNavigationTarget;
            hasGuidance = deviationAvailable || mapTarget;
            string heading = vertical ? "ALONG TRACK" : "TARGET BEARING";
            string value = "NO TARGET";
            position = 0f;
            if (deviationAvailable)
            {
                heading = vertical ? "G/S" : "LOC";
                float dots = vertical ? -glideslope.GetDisplayedDeviation() : localizer.GetDisplayedDeviation();
                position = dots / 2.5f;
                value = Mathf.Abs(dots).ToString("0.0") + " DOT";
            }
            else if (mapTarget)
            {
                RadarNavigationTarget target = navigationDisplay.CurrentNavigationTarget;
                float relative = Mathf.DeltaAngle(0f, target.RelativeBearingDegrees);
                float alongTrack = target.DistanceNM * Mathf.Cos(relative * Mathf.Deg2Rad);
                position = vertical ? alongTrack / Mathf.Max(1f, navigationDisplay.RangeNM) : relative / 60f;
                value = vertical ? $"{Mathf.Abs(alongTrack):0.0} NM" :
                    $"{target.BearingDegrees:000}°  /  {target.DistanceNM:0.0} NM";
            }
            clamped = Mathf.Abs(position) > 1f;
            position = Mathf.Clamp(position, -1f, 1f);
            float end = vertical ? rectTransform.rect.height * .5f - 36f : rectTransform.rect.width * .5f - 16f;
            title = Label("Mode", title, new Vector2(0f, vertical ? end + 43f : 24f), vertical ? 11f : 12f);
            detail = Label("Detail", detail, new Vector2(0f, vertical ? -end - 44f : -25f), 12f);
            title.text = heading;
            detail.text = value;
            Color tint = hasGuidance ? new Color(.2f, 1f, .2f, .95f) : new Color(.2f, 1f, .2f, .68f);
            title.color = tint;
            detail.color = mapTarget ? new Color(1f, .79f, .32f, 1f) : tint;
            positiveLabel = Label("Positive", positiveLabel, vertical ? new Vector2(0, end + 17f) : new Vector2(end, 16f), 10f);
            negativeLabel = Label("Negative", negativeLabel, vertical ? new Vector2(0, -end - 17f) : new Vector2(-end, 16f), 10f);
            positiveLabel.text = vertical ? deviationAvailable ? "UP" : "AHEAD" : "R";
            negativeLabel.text = vertical ? deviationAvailable ? "DN" : "BEHIND" : "L";
            positiveLabel.color = negativeLabel.color = tint;
            SetVerticesDirty();
        }

        private TMP_Text Label(string name, TMP_Text text, Vector2 at, float fontSize)
        {
            if (text == null)
            {
                Transform child = transform.Find(name);
                GameObject go = child != null ? child.gameObject : new GameObject(name, typeof(RectTransform));
                go.transform.SetParent(transform, false);
                text = go.GetComponent<TextMeshProUGUI>() ?? go.AddComponent<TextMeshProUGUI>();
                text.font = TMP_Settings.defaultFontAsset;
                text.fontStyle = FontStyles.Normal;
                text.alignment = TextAlignmentOptions.Center;
                text.enableAutoSizing = false;
                text.textWrappingMode = TextWrappingModes.NoWrap;
                text.raycastTarget = false;
                text.faceColor = Color.white;
            }
            text.fontSize = fontSize;
            text.rectTransform.anchorMin = text.rectTransform.anchorMax = text.rectTransform.pivot = new Vector2(.5f,.5f);
            text.rectTransform.anchoredPosition = at;
            text.rectTransform.sizeDelta = new Vector2(vertical ? 112f : 220f, 18f);
            text.rectTransform.localScale = Vector3.one;
            // TMP already bakes its tint into the mesh. A retained renderer
            // fade would multiply that alpha and make the scale unreadable.
            text.canvasRenderer.SetColor(Color.white);
            return text;
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Vector2 axis = vertical ? Vector2.up : Vector2.right;
            Vector2 cross = vertical ? Vector2.right : Vector2.up;
            float end = vertical ? rectTransform.rect.height * .5f - 36f : rectTransform.rect.width * .5f - 16f;
            Color dim = new Color(.2f, 1f, .2f, hasGuidance ? .46f : .25f);
            Line(mesh, -axis * end, axis * end, .85f, dim);
            for (int i = -2; i <= 2; i++)
            {
                Vector2 center = axis * (end * i / 2f);
                if (i == 0 || Mathf.Abs(i) == 2)
                    Line(mesh, center - cross * 4f, center + cross * 4f, 1.1f, dim);
                else
                {
                    const int segments = 24;
                    for (int j = 0; j < segments; j++)
                    {
                        float a = 2 * Mathf.PI * j / segments;
                        float b = 2 * Mathf.PI * (j + 1) / segments;
                        Line(mesh, center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 2.8f,
                            center + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * 2.8f, 1f, dim);
                    }
                }
            }
            if (!hasGuidance) return;
            Vector2 target = axis * (position * end);
            Color accent = mapTarget ? new Color(1f, .79f, .32f, 1f) : new Color(.2f, 1f, .2f, 1f);
            Vector2[] diamond = { Vector2.up * 6f, Vector2.right * 6f, Vector2.down * 6f, Vector2.left * 6f };
            for (int i = 0; i < 4; i++)
            {
                Line(mesh, target + diamond[i], target + diamond[(i+1)%4], 3.6f, new Color(.01f,.025f,.03f,.9f));
                Line(mesh, target + diamond[i], target + diamond[(i+1)%4], 1.4f, accent);
            }
            if (clamped)
            {
                Vector2 tip = target + axis * Mathf.Sign(position) * 10f;
                Line(mesh, tip, target + cross * 4f, 1.4f, accent);
                Line(mesh, tip, target - cross * 4f, 1.4f, accent);
            }
        }

        private static void Line(VertexHelper mesh, Vector2 a, Vector2 b, float width, Color tint)
        {
            Vector2 direction = b-a;
            if (direction.sqrMagnitude < .0001f) return;
            Vector2 n = new Vector2(-direction.y, direction.x).normalized * width * .5f;
            int start = mesh.currentVertCount;
            Vertex(mesh, a-n, tint);
            Vertex(mesh, a+n, tint);
            Vertex(mesh, b+n, tint);
            Vertex(mesh, b-n, tint);
            mesh.AddTriangle(start,start+1,start+2);
            mesh.AddTriangle(start,start+2,start+3);
        }

        private static void Vertex(VertexHelper mesh, Vector2 point, Color tint)
        {
            UIVertex vertex = UIVertex.simpleVert;
            vertex.position = point;
            vertex.color = tint;
            mesh.AddVert(vertex);
        }
    }
}
