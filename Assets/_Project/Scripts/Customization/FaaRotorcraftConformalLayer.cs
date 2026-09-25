using System.Collections.Generic;
using AviationUI;
using FAA.XPlaneIntegration.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace FAA.Customization
{
    /// <summary>
    /// Research/simulator presentation, NOT an FAA-approved flight instrument.
    /// Angular cues are drawn on a distant world-space shell; geographic cues keep their actual
    /// scene depth. Both eyes therefore use Unity's native view/projection, not a mono overlay.
    /// The finite angular shell approximates optical collimation; XR hardware calibration is required.
    /// </summary>
    [DefaultExecutionOrder(12100), DisallowMultipleComponent]
    [AddComponentMenu("FAA/HUD/Rotorcraft Conformal Layer")]
    public sealed class FaaRotorcraftConformalLayer : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Camera view;
        [SerializeField] private Transform aircraft;
        [SerializeField] private XPlane12ApiHudBridge flightData;
        [Tooltip("Optional local tangent frame: X east, Y up, Z true north. Defaults to Unity world axes.")]
        [SerializeField] private Transform earthFrame;
        [Header("Pilot-selected references, not flight-director commands")]
        [SerializeField, Range(-15f, 10f)] private float selectedFpaDegrees = -3f;
        [SerializeField] private bool showFpa = true;
        [SerializeField] private bool showSceneCues = true;
        [SerializeField] private bool showHover = true;
        [SerializeField, Min(1f)] private float hoverFullScaleKnots = 10f;
        [SerializeField, Min(1f)] private float angularDistanceMeters = 4000f;
        [SerializeField, Range(.1f, 2f)] private float maximumPacketAgeSeconds = 1f;
        [SerializeField] private LayerMask landingSurfaceLayers = ~0;
        [SerializeField] private Color cueColor = new Color(.2f, 1f, .2f, .9f);
        [SerializeField, Range(.01f, .1f)] private float strokeDegrees = .06f;

        private readonly List<Vector3> vertices = new(16000);
        private readonly List<Color> colors = new(16000);
        private readonly List<int> triangles = new(24000);
        private readonly List<TextMeshPro> labels = new(40);
        private readonly List<MeshRenderer> labelRenderers = new(40);
        private readonly Dictionary<GameObject, bool> retiredObjects = new();
        private readonly Dictionary<Graphic, bool> retiredGraphics = new();
        private Mesh mesh;
        private MeshRenderer meshRenderer;
        private Material material, labelMaterial;
        private Transform geometryRoot;
        private Transform worldLabelRoot;
        private Canvas sourceCanvas;
        private string dataStatus = "NOT INITIALIZED";
        private Transform primaryHudRoot;
        private int usedLabels;
        private float radius, nextSourceSearch;
        private bool isHover, frameVisible, warnedShader;
        private Vector3 eyePosition;
        private FaaRotorcraftCueAnchor userLandingReference;
        private readonly RaycastHit[] landingHits = new RaycastHit[64];
        private CanvasGroup[] visibilityGroups;
        private int lastRefreshFrame = -1;
        private Vector3 lastViewPosition;
        private Quaternion lastViewRotation;
        private string actionFeedback;
        private float feedbackUntil;
        private float worldTextLineHeight = 1f;

        public float SelectedFpaDegrees => selectedFpaDegrees;
        public bool HoverMode => isHover;
        public bool HasLiveAttitude { get; private set; }
        public bool HasLiveVelocity { get; private set; }
        public int LastVertexCount => vertices.Count;
        public int VisibleSceneCueCount { get; private set; }
        public string DataStatus => dataStatus;

        public void Bind(Camera camera, Transform aircraftTransform, Canvas fixedFlightCanvas)
        {
            view = camera;
            aircraft = aircraftTransform;
            if (sourceCanvas == fixedFlightCanvas) return;
            RestoreLegacy();
            sourceCanvas = fixedFlightCanvas;
            primaryHudRoot = sourceCanvas != null ? sourceCanvas.transform.Find("Second Interation GUI") : null;
            visibilityGroups = primaryHudRoot != null ? primaryHudRoot.GetComponentsInParent<CanvasGroup>(true) : null;
            RetireLegacy();
        }

        private void OnEnable()
        {
            Application.onBeforeRender += RefreshBeforeRender;
            Camera.onPreCull += CameraReady;
            RenderPipelineManager.beginCameraRendering += PipelineCameraReady;
            RetireLegacy();
        }

        private void OnDisable()
        {
            Application.onBeforeRender -= RefreshBeforeRender;
            Camera.onPreCull -= CameraReady;
            RenderPipelineManager.beginCameraRendering -= PipelineCameraReady;
            SetRenderVisibility(false);
            RestoreLegacy();
        }

        private void OnDestroy()
        {
            if (mesh != null) Destroy(mesh);
            if (material != null) Destroy(material);
            if (labelMaterial != null) Destroy(labelMaterial);
            if (geometryRoot != null) Destroy(geometryRoot.gameObject);
            if (userLandingReference != null) Destroy(userLandingReference.gameObject);
        }

        private void RetireLegacy()
        {
            if (sourceCanvas == null) return;
            foreach (var old in sourceCanvas.GetComponentsInChildren<FaaPitchLadderGraphic>(true))
            {
                if (!retiredObjects.ContainsKey(old.gameObject)) retiredObjects.Add(old.gameObject, old.gameObject.activeSelf);
                old.gameObject.SetActive(false); // Also hides its children/labels, not just its mesh.
            }
            foreach (var old in sourceCanvas.GetComponentsInChildren<Graphic>(true))
            {
                string name = old.gameObject.name;
                if (name != "Scale" && name != "ScaleIteration2" && name != "FPV" && name != "Miniature Aircraft") continue;
                if (!retiredGraphics.ContainsKey(old)) retiredGraphics.Add(old, old.enabled);
                old.enabled = false;
            }
        }

        private void RestoreLegacy()
        {
            foreach (var entry in retiredObjects) if (entry.Key != null) entry.Key.SetActive(entry.Value);
            foreach (var entry in retiredGraphics) if (entry.Key != null) entry.Key.enabled = entry.Value;
            retiredObjects.Clear();
            retiredGraphics.Clear();
        }

        private void LateUpdate()
        {
            if (flightData == null && Time.unscaledTime >= nextSourceSearch)
            {
                nextSourceSearch = Time.unscaledTime + 1f;
                flightData = FindFirstObjectByType<XPlane12ApiHudBridge>();
            }
            RefreshPresentation();
        }

        [BeforeRenderOrder(300)]
        private void RefreshBeforeRender()
        {
            if (isActiveAndEnabled && view != null && (lastRefreshFrame != Time.frameCount ||
                lastViewPosition != view.transform.position || lastViewRotation != view.transform.rotation))
                RefreshPresentation();
        }
        private void PipelineCameraReady(ScriptableRenderContext context, Camera camera) => CameraReady(camera);
        private void CameraReady(Camera camera)
        {
            if (!isActiveAndEnabled) return;
            if (camera == view) RefreshBeforeRender(); // Final pose; avoid rebuilding the same mesh three times each frame.
            SetRenderVisibility(camera == view && frameVisible);
        }

        private void SetRenderVisibility(bool visible)
        {
            if (meshRenderer != null) meshRenderer.enabled = visible;
            // Use native mesh text. Canvas batches can be culled before a camera's
            // pre-cull callback when several radar/scene cameras share the frame.
            foreach (var renderer in labelRenderers) if (renderer != null) renderer.enabled = visible;
        }

        private bool EnsureBuilt()
        {
            if (mesh != null) return true;
            Shader shader = Resources.Load<Shader>("Shaders/FaaRotorcraftConformal");
            if (shader == null || !shader.isSupported)
            {
                if (!warnedShader) Debug.LogError("[FAA rotorcraft HUD] Conformal line shader unavailable; scene cues suppressed.");
                warnedShader = true;
                return false;
            }
            var go = new GameObject("FAA Rotorcraft World Cues", typeof(MeshFilter), typeof(MeshRenderer));
            geometryRoot = go.transform;
            geometryRoot.SetParent(transform, false);
            mesh = new Mesh { name = "FAA rotorcraft cue mesh", indexFormat = IndexFormat.UInt32 };
            mesh.MarkDynamic();
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            material = new Material(shader) { name = "FAA rotorcraft lines (runtime)", enableInstancing = true };
            meshRenderer = go.GetComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.allowOcclusionWhenDynamic = false;
            meshRenderer.sortingOrder = 5040;
            var labelRoot = new GameObject("FAA World Cue Labels");
            labelRoot.transform.SetParent(geometryRoot, false);
            worldLabelRoot = labelRoot.transform;
            var font = TMP_Settings.defaultFontAsset;
            if (font != null)
            {
                labelMaterial = new Material(font.material) { name = "FAA world text (runtime)", enableInstancing = true };
                // A resource material keeps the overlay shader included in player builds.
                // The regular TMP mobile shader uses unity_GUIZTestMode, not _ZTestMode.
                var overlayTemplate = Resources.Load<Material>("Shaders/FaaRotorcraftText");
                if (overlayTemplate != null) labelMaterial.shader = overlayTemplate.shader;
                else Debug.LogError("[FAA rotorcraft HUD] Missing overlay text material; world labels may be occluded.");
            }
            return true;
        }

        public void RefreshPresentation()
        {
            frameVisible = view != null && !view.orthographic && aircraft != null && sourceCanvas != null &&
                sourceCanvas.isActiveAndEnabled && (primaryHudRoot == null || primaryHudRoot.gameObject.activeInHierarchy);
            if (visibilityGroups != null)
                foreach (var group in visibilityGroups)
                    if (group != null && group.alpha <= .001f) frameVisible = false;
            if (!frameVisible || !EnsureBuilt())
            {
                SetRenderVisibility(false);
                return;
            }
            eyePosition = view.transform.position;
            lastRefreshFrame = Time.frameCount;
            lastViewPosition = eyePosition;
            lastViewRotation = view.transform.rotation;
            geometryRoot.SetPositionAndRotation(eyePosition, Quaternion.identity);
            geometryRoot.localScale = Vector3.one;
            radius = Mathf.Min(Mathf.Max(10f, angularDistanceMeters), view.farClipPlane * .75f);
            if (radius <= view.nearClipPlane * 2f)
            {
                frameVisible = false;
                SetRenderVisibility(false);
                return;
            }
            vertices.Clear(); colors.Clear(); triangles.Clear(); usedLabels = 0;
            VisibleSceneCueCount = 0;
            var data = flightData != null ? flightData.LatestFlightData : null;
            bool fresh = flightData != null && FaaRotorcraftCueMath.Fresh(flightData.IsFeedHealthy,
                flightData.LastPacketAgeSeconds, maximumPacketAgeSeconds);
            HasLiveAttitude = fresh && data != null && data.attitudeValid &&
                FaaRotorcraftCueMath.Finite(data.pitch) && FaaRotorcraftCueMath.Finite(data.roll) &&
                FaaRotorcraftCueMath.Finite(data.heading);
            Vector3 velocity = Vector3.zero;
            HasLiveVelocity = HasLiveAttitude && data.groundVelocityValid &&
                FaaRotorcraftCueMath.TryGroundVelocity(data.groundSpeed, data.track, data.verticalSpeed, out velocity);
            isHover = HasLiveVelocity && FaaRotorcraftCueMath.UseHover(data.groundSpeed, isHover);
            string flightState = !HasLiveAttitude ? "ATT / FPV UNAVAILABLE - NO FRESH VALID DATA" :
                !HasLiveVelocity ? "FPV UNAVAILABLE - GROUND TRACK / VELOCITY REQUIRED" : isHover ? "HOVER" : "FORWARD FLIGHT";

            if (HasLiveAttitude)
            {
                // The screenshot-style attitude inset is explicitly non-conformal. Avoid a
                // duplicate horizon when selected; FPV/FPA and georeferenced cues stay calibrated.
                if(FaaSpatialWorkspace.Current==null||!FaaSpatialWorkspace.Current.UsesClassicAttitude)
                {
                    DrawAttitude(data.heading);
                    DrawBoresight();
                }
                if (showFpa) DrawFpa(HasLiveVelocity && !isHover ? data.track : data.heading);
                if (HasLiveVelocity && !isHover)
                {
                    Vector3 direction = EarthVector(velocity).normalized;
                    if (FaaRotorcraftCueMath.InView(view, direction)) DrawFpv(direction);
                    else flightState = "FPV OUT OF VIEW"; // Never pin a normal FPV to a false direction at the edge.
                }
                if (showSceneCues) DrawSceneReferences();
                if (showHover && isHover) DrawHover(velocity, data.heading);
            }
            for (int i = usedLabels; i < labels.Count; i++) labels[i].gameObject.SetActive(false);
            mesh.Clear();
            mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            string ra = HasLiveAttitude && data.altitudeAGLValid ? data.altitudeAGL.ToString("F0") + " FT" : "--";
            string q1 = HasLiveAttitude && data.engine1TorqueValid && FaaRotorcraftCueMath.Finite(data.engine1Torque) ? data.engine1Torque.ToString("F0") : "--";
            string q2 = HasLiveAttitude && data.engine2TorqueValid && FaaRotorcraftCueMath.Finite(data.engine2Torque) ? data.engine2Torque.ToString("F0") : "--";
            string nr = HasLiveAttitude && data.rotorNRValid && FaaRotorcraftCueMath.Finite(data.rotorNR) ? data.rotorNR.ToString("F0") : "--";
            // y_agl is geometric height above ground, not proof of a radar-altimeter measurement.
            // The duplicate green status/FPA button strip was removed. Diagnostic state
            // remains available to tests/inspector; the flight and scene cues keep rendering.
            dataStatus = flightState + "   |   HAGL " + ra + "   Q1/Q2 " + q1 + "/" + q2 + "%   NR " + nr + "%";
            SetRenderVisibility(true);
        }

        private Vector3 EarthVector(Vector3 direction) => earthFrame != null ? earthFrame.rotation * direction : direction;
        private Vector3 Ray(float elevation, float bearing) => EarthVector(FaaRotorcraftCueMath.EarthRay(elevation, bearing));

        private void DrawAttitude(float heading)
        {
            // Full earth horizon, including side-window scans. Gaps are intentional, not edge clamping.
            for (int b = -180; b < 180; b += 3)
                if (Mathf.Abs(b) > 3) Angular(Ray(0, heading + b), Ray(0, heading + b + 3), cueColor);
            for (int pitch = -80; pitch <= 80; pitch += 5)
            {
                if (pitch == 0) continue;
                for (int side = -1; side <= 1; side += 2)
                {
                    for (int s = 0; s < 6; s++)
                    {
                        if (pitch < 0 && s % 2 != 0) continue;
                        Angular(Ray(pitch, heading + side * (2.8f + s * .55f)),
                            Ray(pitch, heading + side * (2.8f + (s + 1) * .55f)), cueColor * new Color(1, 1, 1, .8f));
                    }
                    Angular(Ray(pitch, heading + side * 6.1f), Ray(pitch - Mathf.Sign(pitch) * .5f, heading + side * 6.1f), cueColor);
                    WorldLabel(pitch.ToString(), Ray(pitch, heading + side * 7.5f) * radius, .65f);
                }
            }
            for (float pitch = -7.5f; pitch <= 7.5f; pitch += 5f)
                for (int side = -1; side <= 1; side += 2)
                    Angular(Ray(pitch, heading + side * 5.3f), Ray(pitch, heading + side * 6.1f), cueColor * new Color(1, 1, 1, .5f));
        }

        private void DrawBoresight()
        {
            // Aircraft reference, explicitly NOT head-fixed and NOT a geographic location.
            Vector3 direction = aircraft.forward;
            if (!FaaRotorcraftCueMath.InView(view, direction)) return;
            Vector3 left = (direction - aircraft.right * .012f - aircraft.up * .004f).normalized;
            Vector3 right = (direction + aircraft.right * .012f - aircraft.up * .004f).normalized;
            Angular(left, direction, cueColor);
            Angular(direction, right, cueColor);
        }

        private void DrawFpa(float bearing)
        {
            Color tint = cueColor * new Color(1, 1, 1, .64f);
            for (int side = -1; side <= 1; side += 2)
                for (int dash = 0; dash < 5; dash++)
                    Angular(Ray(selectedFpaDegrees, bearing + side * (2f + dash * 1.6f)),
                        Ray(selectedFpaDegrees, bearing + side * (2.8f + dash * 1.6f)), tint);
            WorldLabel("FPA " + selectedFpaDegrees.ToString("+0.0;-0.0;0.0"),
                Ray(selectedFpaDegrees, bearing + 11f) * radius, .55f);
        }

        private void DrawFpv(Vector3 direction)
        {
            Vector3 center = direction * radius;
            float unit = radius * Mathf.Tan(.35f * Mathf.Deg2Rad);
            Ring(center, unit, cueColor);
            Vector3 right = view.transform.right, up = view.transform.up;
            Line(center - right * unit, center - right * unit * 3f, cueColor);
            Line(center + right * unit, center + right * unit * 3f, cueColor);
            Line(center + up * unit, center + up * unit * 2f, cueColor);
        }

        private void DrawSceneReferences()
        {
            // Bounded authored/explicit references only. No synthetic wire/terrain hazard detections.
            foreach (var anchor in FaaRotorcraftCueAnchor.Active)
            {
                if (VisibleSceneCueCount >= 24) break;
                if (anchor == null || !anchor.TryPosition(out Vector3 position)) continue;
                bool landing = anchor.kind == FaaRotorcraftCueAnchor.CueType.LandingArea;
                bool hoverTarget = anchor.kind == FaaRotorcraftCueAnchor.CueType.HoverReference;
                if ((landing || hoverTarget) && !anchor.selected) continue;
                Vector3 center = position - eyePosition;
                float distance = center.magnitude;
                if (distance <= view.nearClipPlane + .1f || distance > anchor.maximumRangeMeters || distance > view.farClipPlane * .9f) continue;
                if (!FaaRotorcraftCueMath.InView(view, center)) continue;
                VisibleSceneCueCount++;
                float size = distance * Mathf.Tan(.42f * Mathf.Deg2Rad);
                Vector3 r = view.transform.right * size, u = view.transform.up * size;
                bool obstacle = anchor.kind == FaaRotorcraftCueAnchor.CueType.Obstacle;
                Color tint = obstacle ? new Color(1f, .77f, .32f, .9f) : cueColor;
                if (obstacle)
                {
                    Line(center + u, center - u - r, tint);
                    Line(center - u - r, center - u + r, tint);
                    Line(center - u + r, center + u, tint);
                    if (anchor.lineEnd != null && FaaRotorcraftCueMath.Finite(anchor.lineEnd.position))
                        Line(center, anchor.lineEnd.position - eyePosition, tint);
                }
                else
                {
                    Line(center + u, center + r, tint); Line(center + r, center - u, tint);
                    Line(center - u, center - r, tint); Line(center - r, center + u, tint);
                }
                if (landing && FaaRotorcraftCueMath.Finite(anchor.areaMeters.x) && FaaRotorcraftCueMath.Finite(anchor.areaMeters.y) &&
                    anchor.areaMeters.x > 0f && anchor.areaMeters.y > 0f)
                {
                    Vector3 right = anchor.transform.right * Mathf.Min(1000f, anchor.areaMeters.x) * .5f;
                    Vector3 forward = anchor.transform.forward * Mathf.Min(1000f, anchor.areaMeters.y) * .5f;
                    Line(center - right - forward, center + right - forward, tint);
                    Line(center + right - forward, center + right + forward, tint);
                    Line(center + right + forward, center - right + forward, tint);
                    Line(center - right + forward, center - right - forward, tint);
                }
                string id = string.IsNullOrWhiteSpace(anchor.identifier) ? "REF" : anchor.identifier;
                if (id.Length > 18) id = id.Substring(0, 18);
                string prefix = landing ? "LZ REF " : hoverTarget ? "HOVER REF " : obstacle ? "OBS " : "WPT ";
                WorldLabel(prefix + id + " " + distance.ToString("F0") + "m", center - view.transform.up * size * 2.3f, .55f);
            }
        }

        private void DrawHover(Vector3 velocity, float heading)
        {
            // This separate plan-view instrument stays head-fixed. It is not a forward-looking FPV.
            Vector3 center = view.transform.rotation * FaaRotorcraftCueMath.EarthRay(-12f, 15f) * radius;
            float scale = radius * Mathf.Tan(1.8f * Mathf.Deg2Rad);
            Vector3 right = view.transform.right, up = view.transform.up;
            Color dim = cueColor * new Color(1, 1, 1, .6f);
            Ring(center, scale, dim);
            Line(center - right * scale, center + right * scale, dim);
            Line(center - up * scale, center + up * scale, dim);
            Vector2 drift = FaaRotorcraftCueMath.HoverVelocity(velocity, heading);
            float fullScale = Mathf.Max(1f, hoverFullScaleKnots);
            bool inScale = drift.magnitude <= fullScale;
            if (inScale)
            {
                Vector3 endpoint = center + (right * drift.x + up * drift.y) * (scale / fullScale);
                Line(center, endpoint, cueColor);
                Ring(endpoint, scale * .1f, cueColor);
            }
            WorldLabel("HOVER " + fullScale.ToString("F0") + "kt" + (inScale ? "" : " OFF SCALE"), center - up * scale * 1.45f, .6f);
        }

        private void Angular(Vector3 a, Vector3 b, Color tint) => Line(a * radius, b * radius, tint);
        private void Ring(Vector3 center, float size, Color tint)
        {
            Vector3 right = view.transform.right * size, up = view.transform.up * size;
            for (int i = 0; i < 40; i++)
            {
                float a = i * Mathf.PI * 2f / 40f, b = (i + 1) * Mathf.PI * 2f / 40f;
                Line(center + right * Mathf.Cos(a) + up * Mathf.Sin(a), center + right * Mathf.Cos(b) + up * Mathf.Sin(b), tint);
            }
        }

        private void Line(Vector3 a, Vector3 b, Color tint)
        {
            if (!FaaRotorcraftCueMath.Finite(a) || !FaaRotorcraftCueMath.Finite(b)) return;
            Vector3 segment = b - a;
            if (segment.sqrMagnitude < .000001f) return;
            Vector3 middle = (a + b) * .5f;
            Vector3 side = Vector3.Cross(segment, middle).normalized;
            if (side.sqrMagnitude < .5f) return;
            float width = Mathf.Max(view.nearClipPlane, middle.magnitude) * Mathf.Tan(strokeDegrees * Mathf.Deg2Rad) * .5f;
            Vector3 n = side * width;
            int start = vertices.Count;
            vertices.Add(a - n); vertices.Add(a + n); vertices.Add(b + n); vertices.Add(b - n);
            for (int i = 0; i < 4; i++) colors.Add(tint);
            triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
            triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
        }

        private void WorldLabel(string value, Vector3 relativePosition, float heightDegrees)
        {
            if (!FaaRotorcraftCueMath.InView(view, relativePosition, .045f)) return;
            if (usedLabels == labels.Count)
            {
                var go = new GameObject("FAA Cue Label " + usedLabels, typeof(RectTransform));
                go.transform.SetParent(worldLabelRoot, false);
                var text = go.AddComponent<TextMeshPro>();
                text.font = TMP_Settings.defaultFontAsset;
                if (labelMaterial != null) text.fontSharedMaterial = labelMaterial;
                text.fontSize = 16f; text.alignment = TextAlignmentOptions.Center;
                text.textWrappingMode = TextWrappingModes.NoWrap;
                text.richText = false;
                text.rectTransform.sizeDelta = new Vector2(340f, 24f);
                // Native TMP text uses world units, not the UI's pixel-sized font metrics.
                worldTextLineHeight = Mathf.Max(.01f, text.GetPreferredValues("0").y);
                var renderer = go.GetComponent<MeshRenderer>();
                renderer.sortingOrder = 5041;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                labelRenderers.Add(renderer);
                labels.Add(text);
            }
            var label = labels[usedLabels++];
            label.gameObject.SetActive(true);
            label.text = value; label.color = cueColor;
            label.rectTransform.localPosition = relativePosition;
            label.rectTransform.rotation = view.transform.rotation;
            float scale = relativePosition.magnitude * Mathf.Tan(heightDegrees * Mathf.Deg2Rad) / worldTextLineHeight;
            label.rectTransform.localScale = Vector3.one * scale;
        }

        public void SetSelectedFpa(float degrees) => selectedFpaDegrees = FaaRotorcraftCueMath.SelectFpa(selectedFpaDegrees, degrees);
        public void SetSceneCuesVisible(bool visible) => showSceneCues = visible;
        public void SetHoverVisible(bool visible) => showHover = visible;
        public void SetFpaVisible(bool visible) => showFpa = visible;

        /// <summary>User action: place a declared 20m reference area on a raycast surface, never a landing-safety assessment.</summary>
        public void MarkLandingReference()
        {
            if (view == null || !HasLiveAttitude) { Feedback("LZ NOT MARKED: fresh attitude and camera required"); return; }
            Ray ray = new Ray(view.transform.position, view.transform.forward);
            int count = UnityEngine.Physics.RaycastNonAlloc(ray, landingHits, Mathf.Min(15000f, view.farClipPlane * .85f),
                landingSurfaceLayers, QueryTriggerInteraction.Ignore);
            // A saturated hit buffer is incomplete; fail closed instead of choosing a possibly occluded surface.
            if (count == landingHits.Length) { Feedback("LZ NOT MARKED: ambiguous surface intersections"); return; }
            int nearest = -1;
            float best = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                var hit = landingHits[i];
                if (hit.transform == null || aircraft != null && (hit.transform == aircraft || hit.transform.IsChildOf(aircraft))) continue;
                if (hit.distance < best) { nearest = i; best = hit.distance; }
            }
            if (nearest < 0) { Feedback("LZ NOT MARKED: look at a loaded surface with a collider"); return; }
            RaycastHit surface = landingHits[nearest];
            // This only avoids wall placement. It is not a rotorcraft landing slope limit.
            if (Vector3.Dot(surface.normal, EarthVector(Vector3.up)) < .5f)
            { Feedback("LZ NOT MARKED: selected surface is wall-like"); return; }
            ClearLandingReference();
            var go = new GameObject("Pilot-selected LZ reference (not surveyed)");
            go.transform.SetParent(surface.transform, true);
            Vector3 forward = Vector3.ProjectOnPlane(aircraft != null ? aircraft.forward : view.transform.forward, surface.normal);
            if (forward.sqrMagnitude < .001f) forward = Vector3.ProjectOnPlane(view.transform.up, surface.normal);
            go.transform.SetPositionAndRotation(surface.point + surface.normal * .05f, Quaternion.LookRotation(forward, surface.normal));
            userLandingReference = go.AddComponent<FaaRotorcraftCueAnchor>();
            userLandingReference.kind = FaaRotorcraftCueAnchor.CueType.LandingArea;
            userLandingReference.identifier = "PILOT";
            userLandingReference.Select();
            Feedback("LZ REFERENCE MARKED: declared 20m box, not a surveyed pad or landing clearance");
        }

        private void Feedback(string message) { actionFeedback = message; feedbackUntil = Time.unscaledTime + 5f; }

        public void ClearLandingReference()
        {
            if (userLandingReference == null) return;
            userLandingReference.gameObject.SetActive(false);
            Destroy(userLandingReference.gameObject);
            userLandingReference = null;
        }
    }
}
