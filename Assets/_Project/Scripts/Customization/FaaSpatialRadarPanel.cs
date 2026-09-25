using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace FAA.Customization
{
    /// <summary>
    /// Moves the ORIGINAL radar canvas, including controls, status and drawers. No duplicated radar/telemetry.
    /// The original hierarchy is untouched; disabled/native->desktop restores the captured Canvas exactly.
    /// </summary>
    public sealed class FaaSpatialRadarPanel
    {
        public readonly string Id;
        public readonly Canvas Canvas;
        public readonly RectTransform Radar;
        public readonly FaaSpatialLayoutEntry Layout;
        public bool IsUtility { get; }
        private readonly float physicalWidth;
        private readonly FaaRadarGroupFootprint footprint = new();
        public Vector2 ProtectedPhysicalSize { get; private set; }
        public float PhysicalWidth => physicalWidth;
        public float PhysicalHeight => physicalWidth * Radar.rect.height / Mathf.Max(1f,Radar.rect.width);
        public FaaSpatialLayoutEntry DefaultLayout { get; private set; }
        public bool IsSpatial { get; private set; }
        public GameObject Handle => handle != null ? handle.gameObject : null;
        public Vector3 WorldCenter => Radar.TransformPoint(Radar.rect.center);

        private readonly FaaSpatialWorkspace owner;
        private readonly RectTransform canvasRect;
        private readonly UnityEngine.UI.CanvasScaler scaler;
        private readonly GraphicRaycaster raycaster;
        private TrackedDeviceGraphicRaycaster trackedRaycaster;
        private bool ownsTrackedRaycaster;
        private readonly Vector3[] corners = new Vector3[4];
        private readonly CanvasGroup[] visibility;
        private RectTransform handle;
        private CanvasState original;

        private struct CanvasState
        {
            public RenderMode mode;
            public Camera camera;
            public Vector2 anchorMin, anchorMax, pivot, size, anchored;
            public Vector3 position, rotationEuler, scale;
            public float planeDistance;
            public bool scalerEnabled;
        }

        public FaaSpatialRadarPanel(FaaSpatialWorkspace workspace, string id, Canvas canvas, RectTransform radar, float yaw,
            bool utility = false, float widthMeters = .42f)
        {
            owner = workspace; Id = id; Canvas = canvas; Radar = radar;
            IsUtility=utility;physicalWidth=widthMeters;
            canvasRect = (RectTransform)canvas.transform;
            scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>();
            raycaster = canvas.GetComponent<GraphicRaycaster>();
            visibility = radar.GetComponentsInParent<CanvasGroup>(true);
            Layout = new FaaSpatialLayoutEntry { id = id,
                yaw = Mathf.Sign(yaw) * (utility ? FaaPeripheralPanelLayout.DefaultYaw : 95f),
                elevation = utility ? FaaPeripheralPanelLayout.DefaultElevation : -32f,
                distance = utility ? FaaPeripheralPanelLayout.DefaultDistance : 1.7f, scale = 1f };
            DefaultLayout = Layout.Copy();
            BuildHandle();
        }

        public void SetSpatial(bool value)
        {
            if (Canvas == null || Radar == null || IsSpatial == value) return;
            if (value)
            {
                original = new CanvasState
                {
                    mode = Canvas.renderMode, camera = Canvas.worldCamera, planeDistance = Canvas.planeDistance,
                    anchorMin = canvasRect.anchorMin, anchorMax = canvasRect.anchorMax, pivot = canvasRect.pivot,
                    size = canvasRect.sizeDelta, anchored = canvasRect.anchoredPosition,
                    position = canvasRect.localPosition, rotationEuler = canvasRect.localEulerAngles, scale = canvasRect.localScale,
                    scalerEnabled = scaler != null && scaler.enabled
                };
                trackedRaycaster = Canvas.GetComponent<TrackedDeviceGraphicRaycaster>();
                if (trackedRaycaster == null)
                {
                    trackedRaycaster = Canvas.gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();
                    ownsTrackedRaycaster = true;
                }
            }
            else RestoreCanvas();
            IsSpatial = value;
        }

        private void RestoreCanvas()
        {
            if (Canvas == null) return;
            Canvas.renderMode = original.mode; Canvas.worldCamera = original.camera; Canvas.planeDistance = original.planeDistance;
            canvasRect.anchorMin = original.anchorMin; canvasRect.anchorMax = original.anchorMax;
            canvasRect.pivot = original.pivot; canvasRect.sizeDelta = original.size;
            canvasRect.anchoredPosition = original.anchored; canvasRect.localPosition = original.position;
            canvasRect.localEulerAngles = original.rotationEuler; canvasRect.localScale = original.scale;
            if (scaler != null) scaler.enabled = original.scalerEnabled;
            if (ownsTrackedRaycaster && trackedRaycaster != null) Object.Destroy(trackedRaycaster);
            ownsTrackedRaycaster = false;
        }

        public void Apply(Transform cockpit, Camera view, bool editing)
        {
            if (Canvas == null || Radar == null) return;
            ProtectedPhysicalSize = IsUtility ? new Vector2(physicalWidth, PhysicalHeight) : footprint.Measure(Canvas, Radar) * (2f * physicalWidth);
            FaaPeripheralPanelLayout.Protect(Layout,ProtectedPhysicalSize.x,ProtectedPhysicalSize.y,DefaultLayout.yaw);
            if (handle != null)
            {
                handle.gameObject.SetActive(editing && Radar.gameObject.activeInHierarchy);
                handle.sizeDelta = new Vector2(IsUtility ? Radar.rect.width-32f : Mathf.Max(180f, Radar.rect.width), 30f);
                handle.SetAsLastSibling();
            }
            if (!IsSpatial || cockpit == null || view == null) return;
            if (scaler != null) scaler.enabled = false;
            if(Canvas.renderMode != RenderMode.WorldSpace)Canvas.renderMode = RenderMode.WorldSpace;
            if(Canvas.worldCamera != view)Canvas.worldCamera = view;
            var centreAnchor = new Vector2(.5f,.5f);
            if (canvasRect.anchorMin != centreAnchor) canvasRect.anchorMin = centreAnchor;
            if (canvasRect.anchorMax != centreAnchor) canvasRect.anchorMax = centreAnchor;
            if (canvasRect.pivot != centreAnchor) canvasRect.pivot = centreAnchor;
            if (canvasRect.sizeDelta != new Vector2(1920,1080)) canvasRect.sizeDelta = new Vector2(1920,1080);
            Vector3 local = FaaSpatialLayoutMath.Position(Layout);
            Vector3 position = cockpit.TransformPoint(local);
            Quaternion rotation = cockpit.rotation * Quaternion.LookRotation(local.normalized, Vector3.up);
            // Keep a constant physical diameter when the existing radar switches to its detailed-map layout.
            float metersPerUnit = physicalWidth * Layout.scale / Mathf.Max(100f, Radar.rect.width * Mathf.Abs(Radar.localScale.x));
            Vector3 parentScale = canvasRect.parent != null ? canvasRect.parent.lossyScale : Vector3.one;
            canvasRect.rotation = rotation;
            canvasRect.localScale = new Vector3(metersPerUnit / Mathf.Max(.0001f, Mathf.Abs(parentScale.x)),
                metersPerUnit / Mathf.Max(.0001f, Mathf.Abs(parentScale.y)), metersPerUnit / Mathf.Max(.0001f, Mathf.Abs(parentScale.z)));
            // Absolute positioning without subtracting large world-space coordinates each frame.
            if (FaaCanvasLocalGeometry.TryMatrix(Radar,canvasRect,out var contentMatrix))
                canvasRect.position = position - canvasRect.TransformVector(contentMatrix.MultiplyPoint3x4(Radar.rect.center));
        }

        public bool TryRay(Ray ray, Camera view, out float distance, out Vector3 hit, bool includeHeader = true)
        {
            hit = Vector3.zero; distance = 0f;
            if (Radar == null || !Radar.gameObject.activeInHierarchy || Canvas == null || !Canvas.isActiveAndEnabled) return false;
            foreach (var group in visibility)
                if (group != null && group.isActiveAndEnabled && group.alpha <= .001f) return false;
            if (IsSpatial)
            {
                var plane = new Plane(Radar.forward, WorldCenter);
                if (!plane.Raycast(ray, out distance) || distance <= 0f || distance > 10f) return false;
                hit = ray.GetPoint(distance);
                Vector3 local = Radar.InverseTransformPoint(hit);
                Rect bounds = Radar.rect;
                if (includeHeader) bounds = Rect.MinMaxRect(bounds.xMin, bounds.yMin - 25f, bounds.xMax, bounds.yMax + 105f);
                return bounds.Contains(new Vector2(local.x, local.y));
            }
            if (view == null) return false;
            Vector3 screen = view.WorldToScreenPoint(ray.GetPoint(2f));
            Camera uiCamera = Canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : Canvas.worldCamera;
            if (screen.z <= 0f || !RectTransformUtility.RectangleContainsScreenPoint(Radar, screen, uiCamera)) return false;
            distance = 2f; hit = ray.GetPoint(distance); return true;
        }

        public void Reset()
        {
            Layout.yaw = DefaultLayout.yaw; Layout.elevation = DefaultLayout.elevation;
            Layout.distance = DefaultLayout.distance; Layout.scale = DefaultLayout.scale;
        }

        private void BuildHandle()
        {
            var go = new GameObject("FAA Layout Grip - " + Id, typeof(RectTransform), typeof(Image), typeof(FaaWorkspaceDragHandle));
            handle = (RectTransform)go.transform; handle.SetParent(Radar, false);
            handle.anchorMin = handle.anchorMax = new Vector2(.5f, 1f);
            handle.anchoredPosition = new Vector2(0f, IsUtility ? -14f : 85f); handle.sizeDelta = new Vector2(Radar.rect.width, 30);
            go.GetComponent<Image>().color = new Color(.03f, .21f, .23f, .96f);
            go.GetComponent<FaaWorkspaceDragHandle>().Configure(owner, Id, false);
            var labelGo = new GameObject("Grip Caption", typeof(RectTransform)); labelGo.transform.SetParent(handle, false);
            var text = labelGo.AddComponent<TextMeshProUGUI>(); text.font = TMP_Settings.defaultFontAsset;
            text.text = "RIGHT-DRAG TO MOVE  /  XR PINCH THIS HANDLE"; text.fontSize = IsUtility ? 13 : 10;
            text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false;
            text.rectTransform.anchorMin = Vector2.zero; text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
            handle.gameObject.SetActive(false);
        }

        public void Dispose()
        {
            SetSpatial(false);
            if (handle != null) Object.Destroy(handle.gameObject);
        }
    }
}
