using UnityEngine;

namespace FAA.Customization
{
    public sealed partial class FaaSpatialWorkspace
    {
        private sealed class GestureState
        {
            public bool pinched, blockedUntilRelease, hasSample;
            public string target;
            public Ray ray;
            public Vector3 hand, startHand, offset, startUp, startDirection;
            public float depth, scale;
            public double sampleTime;
        }
        private readonly GestureState[] gestureStates = { new(), new(), new(), new() };
        private int pairA = -1, pairB = -1;
        private float pairDistance, pairScale;
        private string mouseTarget;
        private Vector2 mouseStart;
        private Vector3 mouseOffset;
        private float mouseDepth, mouseScale;
        private bool mouseResize;
        public bool IsManipulating
        {
            get
            {
                if (RightDragActive) return true;
                if (mouseTarget != null) return true;
                foreach (var state in gestureStates) if (state.target != null) return true;
                return false;
            }
        }
        public bool HasGestureCapture(int pointer) => pointer >= 0 && pointer < gestureStates.Length && gestureStates[pointer].target != null;
        public bool TryPickLayoutTarget(Ray ray, out string id, out float distance)
        {
            id = null; distance = float.PositiveInfinity;
            if (!EditMode || View == null) return false;
            foreach (var panel in InteractivePanels)
                if (panel.TryRay(ray, View, out float depth, out _) && depth < distance)
                { id = panel.Id; distance = depth; }
            if(GetPanel(id)!=null)return true;
            foreach (var module in modules)
                if (module.TryRay(ray, View, out float moduleDistance))
                { id = module.Id; distance = moduleDistance; break; }
            return id != null;
        }
        public void BeginMouseManipulation(string id, Vector2 screen, bool resizeOnly)
        {
            LaptopCamera?.CancelSizing();
            if (!EditMode || View == null || GetEntry(id) == null) return;
            var panel = GetPanel(id);
            if (panel != null && !panel.IsUtility && !SpatialPanelsEnabled) SetSpatialPanels(true);
            Select(id); mouseTarget = id; mouseStart = screen; mouseScale = GetEntry(id).scale;
            mouseResize = resizeOnly || panel == null;
            if (panel != null)
            {
                Ray ray = View.ScreenPointToRay(screen);
                mouseDepth = Vector3.Distance(ray.origin, panel.WorldCenter);
                mouseOffset = panel.WorldCenter - ray.GetPoint(mouseDepth);
            }
        }
        public void UpdateMouseManipulation(Vector2 screen)
        {
            if (!EditMode || mouseTarget == null || View == null) return;
            if (mouseResize)
            {
                Vector2 delta = screen - mouseStart;
                SetScale(mouseTarget, mouseScale * Mathf.Exp(Mathf.Clamp((delta.x + delta.y) * .004f, -2f, 2f)));
            }
            else MovePanel(mouseTarget, View.ScreenPointToRay(screen).GetPoint(mouseDepth) + mouseOffset);
            RefreshTransforms();
        }
        public void EndMouseManipulation()
        {
            if (mouseTarget == null) return;
            mouseTarget = null; MarkChanged();
        }
        public void CancelManipulation()
        {
            CancelRightDrag();
            mouseTarget = null; pairA = pairB = -1;
            foreach (var state in gestureStates)
            {
                state.target = null; state.blockedUntilRelease = state.pinched; state.hasSample = false;
            }
        }
        private void EndGesture(int pointer, bool cancelled)
        {
            var state = gestureStates[pointer];
            if (pointer == pairA || pointer == pairB)
            {
                int other = pointer == pairA ? pairB : pairA;
                if (other >= 0)
                {
                    gestureStates[other].target = null;
                    gestureStates[other].blockedUntilRelease = true;
                }
                pairA = pairB = -1;
            }
            if (state.target != null) MarkChanged();
            state.target = null;
            if (cancelled) state.blockedUntilRelease = true;
        }
    }
}
