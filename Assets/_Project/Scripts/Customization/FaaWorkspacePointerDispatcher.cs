using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FAA.Customization
{
    /// <summary>Independent hand pointers using the existing EventSystem; no replacement input module.</summary>
    public sealed class FaaWorkspacePointerDispatcher
    {
        private readonly FaaSpatialWorkspace owner;
        private readonly int pointerId;
        private PointerEventData data;
        private readonly List<RaycastResult> results = new();
        private readonly List<Graphic> graphics = new(256);
        private GameObject hover, pressed, drag;
        private bool down, dragging;
        private Vector2 previous;
        public bool HasPress => down;
        public FaaWorkspacePointerDispatcher(FaaSpatialWorkspace workspace, int id) { owner = workspace; pointerId = -2200 - id; }

        public bool TryHit(Ray ray, out RaycastResult result, out Vector2 screen)
        {
            result = default; screen = default;
            if (owner.View == null || EventSystem.current == null) return false;
            if (data == null || data.currentInputModule != EventSystem.current.currentInputModule)
                data = new PointerEventData(EventSystem.current) { pointerId = pointerId, button = PointerEventData.InputButton.Left };
            // Head-fixed recovery/menu controls take priority only when a real UI graphic is hit.
            if (TryCanvas(owner.ControlsCanvas, ray, out result, out screen)) return true;
            float nearest = float.PositiveInfinity;
            foreach (var panel in owner.InteractivePanels)
            {
                if (!TryCanvas(panel.Canvas, ray, out RaycastResult candidate, out Vector2 point)) continue;
                float distance = candidate.distance;
                if (distance >= nearest) continue;
                nearest = distance; result = candidate; screen = point;
            }
            return result.gameObject != null;
        }

        private bool TryCanvas(Canvas canvas, Ray ray, out RaycastResult result, out Vector2 screen)
        {
            result = default; screen = default;
            if (canvas == null || !canvas.isActiveAndEnabled) return false;
            Vector3 world;
            if (canvas.renderMode == RenderMode.WorldSpace)
            {
                var plane = new Plane(canvas.transform.forward, canvas.transform.position);
                if (!plane.Raycast(ray, out float distance) || distance <= 0f || distance > 10f) return false;
                world = ray.GetPoint(distance);
            }
            else
            {
                float depth = canvas.renderMode == RenderMode.ScreenSpaceCamera ? canvas.planeDistance : .9f;
                var plane = new Plane(owner.View.transform.forward, owner.View.transform.position + owner.View.transform.forward * depth);
                if (!plane.Raycast(ray, out float distance) || distance <= 0f) return false;
                world = ray.GetPoint(distance);
            }
            Vector3 pixel = owner.View.WorldToScreenPoint(world);
            if (pixel.z <= 0f) return false;
            screen = new Vector2(pixel.x, pixel.y);
            data.position = screen; results.Clear(); EventSystem.current.RaycastAll(data, results);
            foreach (var candidate in results)
            {
                if (candidate.gameObject == null) continue;
                if (candidate.gameObject.transform == canvas.transform || candidate.gameObject.transform.IsChildOf(canvas.transform))
                { result = candidate; return true; }
            }
            // In a scaled Editor Game view, Canvas/camera pixels can be 3840x2160 while
            // Display.main still reports the desktop window dimensions. GraphicRaycaster's
            // overlay viewport check then rejects a valid render-space point. Use the same
            // Graphic hit/filter rules without the unrelated desktop viewport rejection.
            graphics.Clear(); canvas.GetComponentsInChildren(false, graphics);
            Graphic best = null;
            int bestOrder = int.MinValue, bestDepth = int.MinValue;
            for (int graphicIndex = 0; graphicIndex < graphics.Count; graphicIndex++)
            {
                var graphic = graphics[graphicIndex];
                if (graphic == null || !graphic.isActiveAndEnabled || !graphic.raycastTarget ||
                    graphic.canvasRenderer.cull || graphic.canvas == null) continue;
                Camera eventCamera = graphic.canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : graphic.canvas.worldCamera;
                if (!RectTransformUtility.RectangleContainsScreenPoint(graphic.rectTransform, screen, eventCamera) ||
                    !graphic.Raycast(screen, eventCamera)) continue;
                int order = graphic.canvas.sortingOrder;
                // A just-opened menu can have depth=-1 until its first rendered frame.
                // Active hierarchy order supplies its draw order without admitting hidden objects.
                int drawDepth = graphic.depth >= 0 ? graphic.depth : graphicIndex;
                if (order < bestOrder || order == bestOrder && drawDepth <= bestDepth) continue;
                best = graphic; bestOrder = order; bestDepth = drawDepth;
            }
            if (best == null) return false;
            var module = best.canvas.GetComponent<GraphicRaycaster>() ?? canvas.GetComponent<GraphicRaycaster>();
            if (module == null || !module.isActiveAndEnabled) return false;
            result = new RaycastResult
            {
                gameObject = best.gameObject, module = module, screenPosition = screen,
                worldPosition = world, worldNormal = canvas.transform.forward,
                distance = Vector3.Distance(ray.origin, world), depth = bestDepth,
                sortingOrder = bestOrder, sortingLayer = best.canvas.sortingLayerID
            };
            return true;
        }

        public bool ShouldUseUi(RaycastResult hit)
        {
            if (hit.gameObject == null) return false;
            if (owner.ControlsCanvas != null && hit.gameObject.transform.IsChildOf(owner.ControlsCanvas.transform)) return true;
            foreach(var panel in owner.InteractivePanels)
                if(hit.gameObject.transform.IsChildOf(panel.Canvas.transform))
                    return !owner.EditMode || hit.gameObject.GetComponent<FaaWorkspaceDragHandle>()==null;
            // Ordinary radar taps/map drags stay UI gestures when the layout is locked.
            return !owner.EditMode;
        }

        public void Process(bool valid, bool held, RaycastResult hit, Vector2 screen)
        {
            if (EventSystem.current == null) { Cancel(); return; }
            if (data == null) data = new PointerEventData(EventSystem.current) { pointerId = pointerId, button = PointerEventData.InputButton.Left };
            if (!valid) { Cancel(); return; }
            GameObject target = hit.gameObject;
            data.delta = screen - previous; previous = screen;
            data.position = screen; data.pointerCurrentRaycast = hit;
            if (hover != target)
            {
                if (hover != null) ExecuteEvents.ExecuteHierarchy(hover, data, ExecuteEvents.pointerExitHandler);
                hover = target;
                if (hover != null) ExecuteEvents.ExecuteHierarchy(hover, data, ExecuteEvents.pointerEnterHandler);
            }
            if (held && !down)
            {
                down = true; dragging = false;
                data.pressPosition = screen; data.pointerPressRaycast = hit; data.eligibleForClick = true;
                data.pointerPress = pressed = ExecuteEvents.ExecuteHierarchy(target, data, ExecuteEvents.pointerDownHandler);
                if (pressed == null) data.pointerPress = pressed = ExecuteEvents.GetEventHandler<IPointerClickHandler>(target);
                data.pointerDrag = drag = ExecuteEvents.GetEventHandler<IDragHandler>(target);
                if (drag != null) ExecuteEvents.Execute(drag, data, ExecuteEvents.initializePotentialDrag);
            }
            else if (held && down && drag != null)
            {
                if (!dragging && Vector2.Distance(data.pressPosition, screen) >= 8f)
                {
                    dragging = true; data.dragging = true; data.eligibleForClick = false;
                    ExecuteEvents.Execute(drag, data, ExecuteEvents.beginDragHandler);
                }
                if (dragging) ExecuteEvents.Execute(drag, data, ExecuteEvents.dragHandler);
            }
            else if (!held && down) Release(target, true);
        }

        private void Release(GameObject target, bool allowClick)
        {
            // Clear ownership BEFORE dispatch: an Edit/Lock button may call CancelPointers recursively.
            GameObject oldPressed = pressed, oldDrag = drag;
            bool wasDragging = dragging;
            down = dragging = false; pressed = drag = null;
            if (oldPressed != null) ExecuteEvents.Execute(oldPressed, data, ExecuteEvents.pointerUpHandler);
            if (wasDragging && oldDrag != null) ExecuteEvents.Execute(oldDrag, data, ExecuteEvents.endDragHandler);
            if (allowClick && !wasDragging && oldPressed != null &&
                ExecuteEvents.GetEventHandler<IPointerClickHandler>(target) == oldPressed)
                ExecuteEvents.Execute(oldPressed, data, ExecuteEvents.pointerClickHandler);
            data.pointerPress = data.pointerDrag = null; data.dragging = data.eligibleForClick = false;
        }
        public void Cancel()
        {
            if (data == null) return;
            if (down) Release(null, false);
            if (hover != null) ExecuteEvents.ExecuteHierarchy(hover, data, ExecuteEvents.pointerExitHandler);
            hover = null;
        }
    }
}
