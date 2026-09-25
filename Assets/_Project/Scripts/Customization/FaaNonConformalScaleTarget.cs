using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace FAA.Customization
{
    /// <summary>Explicit non-conformal module only; never register a whole attitude/conformal ancestor.</summary>
    public sealed class FaaNonConformalScaleTarget
    {
        public readonly string Id, Caption;
        public readonly Transform Target;
        public readonly FaaSpatialLayoutEntry Layout;
        public readonly float DefaultScale;
        public Vector3 BaseScale { get; }
        private readonly Vector3 basePosition;
        private readonly Graphic[] graphics;
        private readonly CanvasGroup[] visibility;
        private readonly Vector3[] corners = new Vector3[4];

        public FaaNonConformalScaleTarget(string id, string caption, Transform target, float defaultScale)
        {
            Id = id; Caption = caption; Target = target; DefaultScale = defaultScale;
            BaseScale = target.localScale;
            basePosition = target.localPosition;
            Layout = new FaaSpatialLayoutEntry { id = id, scale = defaultScale };
            graphics = target.GetComponentsInChildren<Graphic>(true);
            visibility = target.GetComponentsInParent<CanvasGroup>(true);
        }
        public void Apply()
        {
            if (Target == null) return;
            Vector3 scale = BaseScale * FaaSpatialLayoutMath.Scale(Layout.scale);
            if (Target.localScale != scale) Target.localScale = scale;
        }
        public void Restore()
        {
            if (Target == null) return;
            Target.localScale = BaseScale; Target.localPosition = basePosition;
        }

        public bool PlaceScreenCenter(Camera view, Vector2 center)
        {
            if (Target == null || !(Target.parent is RectTransform parent) || !TryScreenBounds(view, out Rect bounds)) return false;
            Canvas canvas = Target.GetComponentInParent<Canvas>();
            Camera ui = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, bounds.center, ui, out Vector2 before) ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, center, ui, out Vector2 after)) return false;
            Vector2 delta = after - before;
            if (delta.sqrMagnitude > .00000001f) Target.localPosition += new Vector3(delta.x, delta.y, 0f);
            return true;
        }

        public bool TryLayoutBounds(RectTransform root, out Rect bounds)
        {
            bounds = default;
            if (Target == null || root == null || !Target.gameObject.activeInHierarchy) return false;
            bool any = false;
            Vector2 minimum = new Vector2(float.PositiveInfinity,float.PositiveInfinity), maximum = -minimum;
            foreach (var graphic in graphics)
            {
                if (graphic == null || !graphic.isActiveAndEnabled || graphic.color.a <= .001f ||
                    !FaaCanvasLocalGeometry.TryMatrix(graphic.transform,root,out var matrix)) continue;
                Rect rect = FaaCanvasLocalGeometry.TransformRect(graphic.rectTransform.rect,matrix);
                if (rect.width > root.rect.width || rect.height > root.rect.height) continue;
                minimum = Vector2.Min(minimum,rect.min); maximum = Vector2.Max(maximum,rect.max); any = true;
            }
            if (!any) return false;
            bounds = Rect.MinMaxRect(minimum.x-4,minimum.y-4,maximum.x+4,maximum.y+4);
            return true;
        }

        public bool PlaceLayoutCenter(RectTransform root, Vector2 centre)
        {
            if (!TryLayoutBounds(root,out Rect bounds) || !FaaCanvasLocalGeometry.TryMatrix(Target.parent,root,out var matrix)) return false;
            Vector2 delta = centre-bounds.center;
            // A sub-hundredth reference pixel is not a new layout. Avoid dirtying the Canvas at rest.
            if (delta.sqrMagnitude < .0001f) return true;
            Vector3 localDelta = matrix.inverse.MultiplyVector(new Vector3(delta.x,delta.y,0));
            if (FaaSpatialLayoutMath.Finite(localDelta)) Target.localPosition += localDelta;
            return true;
        }

        public bool TryRay(Ray ray, Camera view, out float distance)
        {
            distance = 0f;
            if (!TryScreenBounds(view, out Rect bounds)) return false;
            Canvas canvas = null;
            foreach (var graphic in graphics)
                if (graphic != null && graphic.isActiveAndEnabled && graphic.canvas != null) { canvas = graphic.canvas; break; }
            if (canvas == null) return false;
            float depth = canvas.renderMode == RenderMode.ScreenSpaceCamera ? canvas.planeDistance : 1.2f;
            var plane = canvas.renderMode == RenderMode.WorldSpace ? new Plane(canvas.transform.forward, canvas.transform.position) :
                new Plane(view.transform.forward, view.transform.position + view.transform.forward * depth);
            if (!plane.Raycast(ray, out distance) || distance <= 0f) return false;
            Vector3 screen = view.WorldToScreenPoint(ray.GetPoint(distance));
            return screen.z > 0f && bounds.Contains(new Vector2(screen.x, screen.y));
        }

        public bool TryScreenBounds(Camera view, out Rect bounds)
        {
            bounds = default;
            if (Target == null || !Target.gameObject.activeInHierarchy || view == null) return false;
            foreach (var group in visibility)
                if (group != null && group.isActiveAndEnabled && group.alpha <= .001f) return false;
            bool any = false;
            Vector2 minimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity), maximum = -minimum;
            foreach (Graphic g in graphics)
            {
                if (g == null || !g.isActiveAndEnabled || g.color.a <= .001f || g.canvas == null || !g.canvas.isActiveAndEnabled) continue;
                Camera uiCamera = g.canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : g.canvas.worldCamera;
                g.rectTransform.GetWorldCorners(corners);
                Vector2 a = RectTransformUtility.WorldToScreenPoint(uiCamera, corners[0]);
                Vector2 b = RectTransformUtility.WorldToScreenPoint(uiCamera, corners[2]);
                if (Mathf.Abs(b.x - a.x) > view.pixelWidth || Mathf.Abs(b.y - a.y) > view.pixelHeight) continue;
                minimum = Vector2.Min(minimum, Vector2.Min(a, b)); maximum = Vector2.Max(maximum, Vector2.Max(a, b)); any = true;
            }
            if (!any) return false;
            bounds = Rect.MinMaxRect(minimum.x - 8f, minimum.y - 8f, maximum.x + 8f, maximum.y + 8f);
            return bounds.width > 1f && bounds.height > 1f;
        }
    }
}
