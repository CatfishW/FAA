using System.Collections.Generic;
using FAA.XPlaneIntegration.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FAA.Customization
{
    /// <summary>
    /// Head-fixed attitude instrument with perspective-scaled angular marks.
    /// This is not a calibrated, world-conformal or certified rotorcraft HUD.
    /// No texture dimensions, fixed pixels/degree or clamped attitude drive it.
    /// </summary>
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(CanvasRenderer))]
    public sealed class FaaPitchLadderGraphic : MaskableGraphic
    {
        public const float MajorIntervalDegrees = 5f;
        public const float MinorIntervalDegrees = 2.5f;
        public const float LegacyUnitsPerReferencePixel = 1f / 540f;

        [SerializeField] private XPlane12ApiHudBridge flightData;
        [SerializeField] private Camera projectionCamera;
        [SerializeField] private Graphic[] replacedGraphics;
        [Header("Rotorcraft small-attitude cues")]
        [Tooltip("Short, unnumbered 2.5° marks within ±10°. Numbered marks remain 5° apart.")]
        [SerializeField] private bool showHalfSteps = true;
        [Tooltip("Suppress forward-flight vector below this ground speed; it is not a hover-velocity cue.")]
        [SerializeField, Min(1f)] private float minimumFpvGroundSpeedKnots = 5f;
        [Header("Editor preview only — never overrides live flight data")]
        [SerializeField, Range(-30, 30)] private float previewPitch;
        [SerializeField, Range(-60, 60)] private float previewRoll;

        private readonly List<TMP_Text> labels = new();
        private TMP_Text status;
        private float pitch, roll;
        private Vector2 focal, fpv;
        private bool hasAttitude, hasFpv, lowSpeed, fpvOffScale, fpvUnavailable;
        private static readonly Color Ink = new(.2f, 1f, .2f, .94f);

        public void Configure(XPlane12ApiHudBridge source, Camera view, Graphic[] legacy)
        {
            flightData = source;
            projectionCamera = view;
            replacedGraphics = legacy;
            raycastTarget = false;
            RefreshPresentation();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            raycastTarget = false;
        }

        private void LateUpdate() => RefreshPresentation();

        public void RefreshPresentation()
        {
            // Legacy attitude controllers may continue animating their own
            // transforms. Only their bitmap renderers are retired, so they
            // cannot rescale or move this independent vector instrument.
            if (replacedGraphics != null)
                foreach (var legacy in replacedGraphics)
                    if (legacy != null) legacy.enabled = false;

            Canvas ui = canvas;
            Camera view = ui != null && ui.renderMode != RenderMode.ScreenSpaceOverlay && ui.worldCamera != null
                ? ui.worldCamera : projectionCamera != null ? projectionCamera : Camera.main;
            Camera uiCamera = ui != null && ui.renderMode != RenderMode.ScreenSpaceOverlay ? ui.worldCamera : null;
            Vector2 origin = RectTransformUtility.WorldToScreenPoint(uiCamera, transform.position);
            float scaleX = Vector2.Distance(origin, RectTransformUtility.WorldToScreenPoint(uiCamera, transform.TransformPoint(Vector3.right)));
            float scaleY = Vector2.Distance(origin, RectTransformUtility.WorldToScreenPoint(uiCamera, transform.TransformPoint(Vector3.up)));
            bool validProjection = view != null && !view.orthographic && scaleX > .0001f && scaleY > .0001f;
            focal = validProjection ? ProjectionFocalLengths(view.projectionMatrix, view.pixelRect.size, new Vector2(scaleX, scaleY)) : Vector2.zero;
            validProjection &= Finite(focal.x) && Finite(focal.y) && focal.x > 0 && focal.y > 0;

            if (Application.isPlaying && flightData == null) flightData = FindAnyObjectByType<XPlane12ApiHudBridge>();
            var data = flightData != null ? flightData.LatestFlightData : null;
            hasAttitude = validProjection && (!Application.isPlaying ||
                flightData != null && flightData.IsFeedHealthy && data != null && Finite(data.pitch) && Finite(data.roll));
            pitch = Application.isPlaying && data != null ? data.pitch : previewPitch;
            roll = Application.isPlaying && data != null ? data.roll : previewRoll;
            lowSpeed = Application.isPlaying && hasAttitude && data != null &&
                (!Finite(data.groundSpeed) || data.groundSpeed < minimumFpvGroundSpeedKnots);
            bool forwardFlight = Application.isPlaying && hasAttitude && !lowSpeed;
            bool validFlightPathInputs = data != null && Finite(data.flightPathAngle) && Finite(data.track) && Finite(data.heading);
            bool validFlightPath = forwardFlight && validFlightPathInputs &&
                ProjectDirection(data.flightPathAngle, Mathf.DeltaAngle(data.heading, data.track), pitch, roll, focal, out fpv);
            hasFpv = validFlightPath && Inside(fpv, 25f, 18f);
            fpvOffScale = forwardFlight && validFlightPathInputs && !hasFpv;
            fpvUnavailable = forwardFlight && !validFlightPathInputs;

            RefreshLabels();
            status = GetLabel("Attitude Status", status, 10.5f, new Vector2(rectTransform.rect.width, 18));
            status.rectTransform.anchoredPosition = new Vector2(0, hasAttitude ? rectTransform.rect.yMin + 12 : -26);
            status.text = !validProjection ? "ATT · PERSPECTIVE VIEW REQUIRED" :
                !hasAttitude ? "ATT · NO LIVE DATA" : lowSpeed ? "FPV · LOW SPEED" :
                fpvOffScale ? "FPV · OFF SCALE" : fpvUnavailable ? "FPV · UNAVAILABLE" : "";
            status.color = hasAttitude ? new Color(.2f, 1f, .2f, .60f) : new Color(1f, .77f, .32f, .95f);
            SetVerticesDirty();
        }

        public static Vector2 ProjectionFocalLengths(Matrix4x4 projection, Vector2 viewportPixels, Vector2 pixelsPerUnit) =>
            new(viewportPixels.x * .5f * Mathf.Abs(projection.m00) / pixelsPerUnit.x,
                viewportPixels.y * .5f * Mathf.Abs(projection.m11) / pixelsPerUnit.y);

        /// <summary>
        /// Positive pitch is nose up; positive roll is right bank. A ray at
        /// zero earth elevation therefore falls below the boresight when the
        /// aircraft pitches up. Right bank rotates the horizon counterclockwise.
        /// </summary>
        public static bool ProjectDirection(float elevationDegrees, float bearingDegrees, float aircraftPitch,
            float aircraftRoll, Vector2 focalLengths, out Vector2 point)
        {
            point = Vector2.zero;
            if (!Finite(elevationDegrees) || !Finite(bearingDegrees) || !Finite(aircraftPitch) ||
                !Finite(aircraftRoll) || !Finite(focalLengths.x) || !Finite(focalLengths.y) ||
                focalLengths.x <= 0 || focalLengths.y <= 0) return false;
            float e = elevationDegrees * Mathf.Deg2Rad, b = bearingDegrees * Mathf.Deg2Rad;
            Vector3 earthRay = new(Mathf.Sin(b) * Mathf.Cos(e), Mathf.Sin(e), Mathf.Cos(b) * Mathf.Cos(e));
            // Explicit rotations avoid Unity Euler-order ambiguity: first
            // remove nose-up pitch, then express the ray in the banked axes.
            Vector3 bodyRay = Quaternion.AngleAxis(aircraftRoll, Vector3.forward) *
                              (Quaternion.AngleAxis(aircraftPitch, Vector3.right) * earthRay);
            if (bodyRay.z <= .001f) return false; // Never pin off-scale attitude to a plausible edge value.
            point = new Vector2(focalLengths.x * bodyRay.x / bodyRay.z, focalLengths.y * bodyRay.y / bodyRay.z);
            return Finite(point.x) && Finite(point.y);
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private bool Inside(Vector2 point, float marginX, float marginY)
        {
            Rect area = rectTransform.rect;
            return point.x >= area.xMin + marginX && point.x <= area.xMax - marginX &&
                   point.y >= area.yMin + marginY && point.y <= area.yMax - marginY;
        }

        private bool Project(float angle, float bearing, out Vector2 point) =>
            ProjectDirection(angle, bearing, pitch, roll, focal, out point);

        private void RefreshLabels()
        {
            int used = 0;
            if (hasAttitude)
                for (int mark = -85; mark <= 85; mark += (int)MajorIntervalDegrees)
                {
                    if (mark == 0) continue;
                    for (int side = -1; side <= 1; side += 2)
                    {
                        if (!Project(mark, side * 7.7f, out Vector2 at) || !Inside(at, 15, 13)) continue;
                        if (used == labels.Count) labels.Add(null);
                        TMP_Text label = GetLabel("Pitch Label " + used, labels[used], 15f, new Vector2(30, 22));
                        labels[used++] = label;
                        label.gameObject.SetActive(true);
                        label.text = mark.ToString();
                        label.color = Ink;
                        label.rectTransform.anchoredPosition = at;
                        if (Project(mark, side * 7.7f - .1f, out var a) && Project(mark, side * 7.7f + .1f, out var b))
                            label.rectTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg);
                    }
                }
            for (int i = used; i < labels.Count; i++)
                if (labels[i] != null) labels[i].gameObject.SetActive(false);
            // Find serialized labels after a domain reload, without leaving
            // obsolete numbers at their last positions on the editor canvas.
            for (int i = used; i < transform.childCount; i++)
            {
                Transform child = transform.Find("Pitch Label " + i);
                if (child != null) child.gameObject.SetActive(false);
            }
        }

        private TMP_Text GetLabel(string name, TMP_Text text, float size, Vector2 bounds)
        {
            if (text == null)
            {
                Transform existing = transform.Find(name);
                GameObject go = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform));
                go.transform.SetParent(transform, false);
                if (!go.TryGetComponent(out TextMeshProUGUI tmp)) tmp = go.AddComponent<TextMeshProUGUI>();
                text = tmp;
                text.font = TMP_Settings.defaultFontAsset;
                text.fontStyle = FontStyles.Normal;
                text.alignment = TextAlignmentOptions.Center;
                text.enableAutoSizing = false;
                text.textWrappingMode = TextWrappingModes.NoWrap;
                text.raycastTarget = false;
                text.faceColor = Color.white;
            }
            text.fontSize = size;
            RectTransform rect = text.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = bounds;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            text.canvasRenderer.SetColor(Color.white);
            return text;
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Color reference = hasAttitude ? Ink : new Color(.2f, 1f, .2f, .35f);
            // Fixed, compact aircraft waterline; no scale animation or glow.
            Stroke(mesh, new Vector2(-11, -4), Vector2.zero, 1.5f, reference);
            Stroke(mesh, Vector2.zero, new Vector2(11, -4), 1.5f, reference);
            if (!hasAttitude) return;
            for (int mark = -85; mark <= 85; mark += (int)MajorIntervalDegrees)
            {
                Color tint = mark == 0 ? Ink : new Color(Ink.r, Ink.g, Ink.b, .83f);
                float outer = mark == 0 ? 7f : 6.2f;
                for (int side = -1; side <= 1; side += 2)
                {
                    if (mark < 0)
                        for (int dash = 0; dash < 3; dash++)
                            Arc(mesh, mark, side * Mathf.Lerp(2.7f, outer, dash / 3f),
                                side * Mathf.Lerp(2.7f, outer, (dash + .65f) / 3f), 1.35f, tint);
                    else Arc(mesh, mark, side * 2.7f, side * outer, mark == 0 ? 1.65f : 1.35f, tint);
                    if (mark != 0 && Project(mark, side * outer, out var end) &&
                        Project(mark - Mathf.Sign(mark) * .5f, side * outer, out var hook))
                        Stroke(mesh, end, hook, 1.35f, tint);
                }
            }
            if (showHalfSteps)
                for (float mark = -3 * MinorIntervalDegrees; mark <= 3 * MinorIntervalDegrees; mark += MajorIntervalDegrees)
                    for (int side = -1; side <= 1; side += 2)
                        Arc(mesh, mark, side * 5.25f, side * 6.2f, .85f, new Color(Ink.r, Ink.g, Ink.b, .48f));
            if (!hasFpv) return;
            const float radius = 7.5f;
            for (int i = 0; i < 64; i++)
            {
                float a = Mathf.PI * 2 * i / 64, b = Mathf.PI * 2 * (i + 1) / 64;
                Stroke(mesh, fpv + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius,
                    fpv + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * radius, 1.45f, Ink);
            }
            Stroke(mesh, fpv + Vector2.left * radius, fpv + Vector2.left * 22, 1.45f, Ink);
            Stroke(mesh, fpv + Vector2.right * radius, fpv + Vector2.right * 22, 1.45f, Ink);
            Stroke(mesh, fpv + Vector2.up * radius, fpv + Vector2.up * 16, 1.45f, Ink);
        }

        private void Arc(VertexHelper mesh, float elevation, float startBearing, float endBearing, float width, Color tint)
        {
            const int segments = 12;
            for (int i = 0; i < segments; i++)
                if (Project(elevation, Mathf.Lerp(startBearing, endBearing, i / (float)segments), out var a) &&
                    Project(elevation, Mathf.Lerp(startBearing, endBearing, (i + 1) / (float)segments), out var b))
                    Stroke(mesh, a, b, width, tint);
        }

        private static void Stroke(VertexHelper mesh, Vector2 a, Vector2 b, float width, Color tint)
        {
            Vector2 delta = b - a;
            if (delta.sqrMagnitude < .000001f) return;
            Vector2 normal = new Vector2(-delta.y, delta.x).normalized;
            // A sub-pixel coverage fringe, not a bloom/glow layer, keeps the
            // vector crisp on downscaled Game views and high-DPI headsets.
            Vector2 inner = normal * width * .5f, outer = normal * (width * .5f + .35f);
            int start = mesh.currentVertCount;
            Color clear = new(tint.r, tint.g, tint.b, 0);
            Vertex(mesh, a - outer, clear); Vertex(mesh, b - outer, clear);
            Vertex(mesh, a - inner, tint); Vertex(mesh, b - inner, tint);
            Vertex(mesh, a + inner, tint); Vertex(mesh, b + inner, tint);
            Vertex(mesh, a + outer, clear); Vertex(mesh, b + outer, clear);
            for (int i = 0; i < 3; i++)
            {
                int n = start + i * 2;
                mesh.AddTriangle(n, n + 2, n + 1);
                mesh.AddTriangle(n + 1, n + 2, n + 3);
            }
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
