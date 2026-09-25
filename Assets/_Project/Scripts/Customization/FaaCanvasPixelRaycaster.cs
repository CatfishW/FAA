using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FAA.Customization
{
    /// <summary>
    /// Normal EventSystem mouse/touch path. The canvas pixel rect, not desktop Screen dimensions,
    /// is authoritative for a fixed-resolution Game view. Keeps Graphic/CanvasGroup/mask filters.
    /// Native world/camera-space and XR raycasting retain their existing Unity implementations.
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public sealed class FaaCanvasPixelRaycaster : GraphicRaycaster
    {
        private Canvas target;
        private readonly List<Graphic> hits = new();
        public override void Raycast(PointerEventData eventData, List<RaycastResult> resultAppendList)
        {
            if (target == null) target = GetComponent<Canvas>();
            if (target == null || !target.isActiveAndEnabled || eventData == null) return;
            if (target.renderMode != RenderMode.ScreenSpaceOverlay)
            { base.Raycast(eventData, resultAppendList); return; }
            if (eventData.displayIndex != target.targetDisplay || !target.pixelRect.Contains(eventData.position)) return;
            var graphics = GraphicRegistry.GetRaycastableGraphicsForCanvas(target);
            hits.Clear();
            // Unity's IndexedSet exposes IList but deliberately does not implement GetEnumerator.
            for (int graphicIndex = 0; graphicIndex < graphics.Count; graphicIndex++)
            {
                var graphic = graphics[graphicIndex];
                if (graphic == null || !graphic.isActiveAndEnabled || !graphic.raycastTarget || graphic.canvasRenderer.cull || graphic.depth < 0) continue;
                if (ignoreReversedGraphics && Vector3.Dot(Vector3.forward, graphic.transform.forward) <= 0) continue;
                if (!RectTransformUtility.RectangleContainsScreenPoint(graphic.rectTransform, eventData.position, null)) continue;
                if (graphic.Raycast(eventData.position, null)) hits.Add(graphic);
            }
            hits.Sort((a, b) => b.depth.CompareTo(a.depth));
            foreach (var graphic in hits)
            {
                resultAppendList.Add(new RaycastResult
                {
                    gameObject = graphic.gameObject, module = this, distance = 0,
                    screenPosition = eventData.position, worldPosition = graphic.transform.position,
                    worldNormal = graphic.transform.forward, index = resultAppendList.Count,
                    depth = graphic.depth, sortingLayer = target.sortingLayerID,
                    sortingOrder = target.sortingOrder, displayIndex = target.targetDisplay
                });
            }
        }
    }
}
