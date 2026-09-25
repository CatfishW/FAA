using UnityEngine;
using UnityEngine.InputSystem;

namespace FAA.Customization
{
    public sealed partial class FaaSpatialWorkspace
    {
        private bool rightDragHeld;
        private bool rightDragCaptured;
        private string rightDragId;
        private Vector2 rightDragStartPixel;
        private Vector3 rightDragStartPoint, rightDragCenter;
        private float rightDragDepth;
        public bool RightDragActive => rightDragCaptured;
        public string RightDragPanel => rightDragId;

        // Input is sampled before LateUpdate camera-look processing. Direct mouse dragging is an
        // explicit action; gesture lock does not disable it. The camera rejects the same owned press.
        private void Update()
        {
            if (!Initialized || View == null) return;
            var mouse = Mouse.current;
            ProcessRightDrag(mouse != null && mouse.rightButton.isPressed,
                mouse != null ? mouse.position.ReadValue() : Vector2.zero, Application.isFocused);
        }
        public bool ProcessRightDrag(bool held, Vector2 screen, bool focused)
        {
            if (!focused || !Initialized || View == null || NativeXr || !FaaSpatialLayoutMath.Finite(screen.x) || !FaaSpatialLayoutMath.Finite(screen.y))
            { CancelRightDrag(); rightDragHeld = held; return false; }
            bool began = held && !rightDragHeld; rightDragHeld = held;
            if (!held) { bool captured = rightDragCaptured; EndRightDrag(); return captured; }
            if (began)
            {
                if (IsManipulating || WebcamSizing) return false;
                Ray ray = View.ScreenPointToRay(screen);
                // Only actual visible panel graphics are draggable, not the empty reserve envelope.
                var dispatcher = new FaaWorkspacePointerDispatcher(this, 90);
                if (!dispatcher.TryHit(ray, out var hit, out _) || hit.gameObject == null) return false;
                FaaSpatialRadarPanel selected = null;
                foreach (var candidate in InteractivePanels)
                    if (hit.gameObject.transform.IsChildOf(candidate.Canvas.transform)) { selected = candidate; break; }
                if (selected == null) return false;
                var plane = new Plane(selected.Radar.forward, selected.WorldCenter);
                if (!plane.Raycast(ray, out float depth) || depth <= 0) return false;
                LaptopCamera?.CancelSizing();
                rightDragCaptured = true; rightDragId = selected.Id; rightDragStartPixel = screen;
                rightDragDepth = depth; rightDragStartPoint = CockpitFrame.InverseTransformPoint(ray.GetPoint(depth));
                rightDragCenter = CockpitFrame.InverseTransformPoint(selected.WorldCenter);
                View.GetComponent<AircraftControl.Camera.AircraftCameraController>()?.SetPanelPointerCapture(true);
            }
            if (!rightDragCaptured) return false;
            var panel = GetPanel(rightDragId);
            if (panel == null || !panel.Canvas.isActiveAndEnabled || !panel.Radar.gameObject.activeInHierarchy)
            { EndRightDrag(); return false; }
            if ((screen - rightDragStartPixel).sqrMagnitude > 9f)
            {
                Vector3 point = CockpitFrame.InverseTransformPoint(View.ScreenPointToRay(screen).GetPoint(rightDragDepth));
                MovePanel(rightDragId, CockpitFrame.TransformPoint(rightDragCenter + point - rightDragStartPoint));
                RefreshTransforms();
            }
            return true;
        }
        public void CancelRightDrag()
        {
            rightDragCaptured = false; rightDragId = null;
            if (View != null) View.GetComponent<AircraftControl.Camera.AircraftCameraController>()?.SetPanelPointerCapture(false);
        }
        private void EndRightDrag()
        {
            bool changed = rightDragCaptured;
            CancelRightDrag();
            if (changed) SaveNow();
        }
    }
}
