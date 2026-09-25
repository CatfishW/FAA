using UnityEngine;

namespace FAA.Customization
{
    public sealed partial class FaaSpatialWorkspace
    {
        /// <summary>World-space input, monotonic realtime seconds. No simulated hardware or flight data.</summary>
        public bool SubmitGesture(int pointer, Ray ray, Vector3 position, bool tracked, bool pinched, double now)
        {
            if (WebcamSizing || RightDragActive) return false;
            if (pointer < 0 || pointer >= gestureStates.Length) return false;
            var state = gestureStates[pointer];
            bool valid = tracked && FaaSpatialLayoutMath.Finite(position) && FaaSpatialLayoutMath.Finite(ray.origin) &&
                FaaSpatialLayoutMath.Finite(ray.direction) && ray.direction.sqrMagnitude > .5f && !double.IsNaN(now) && !double.IsInfinity(now);
            bool gap = state.hasSample && (now < state.sampleTime || now - state.sampleTime > .3);
            if (!valid || gap && state.target != null)
            {
                EndGesture(pointer, true); state.blockedUntilRelease = true; state.hasSample = false; return false;
            }
            bool began = pinched && !state.pinched;
            state.ray = ray; state.hand = position; state.sampleTime = now; state.hasSample = true;
            if (!pinched)
            {
                bool captured = state.target != null;
                EndGesture(pointer, false); state.pinched = false; state.blockedUntilRelease = false; return captured;
            }
            state.pinched = true;
            if (!EditMode || state.blockedUntilRelease) return false;
            if (began && TryPickLayoutTarget(ray, out string id, out float depth))
            {
                Select(id); state.target = id; state.startHand = position; state.startUp = View.transform.up;
                state.startDirection = ray.direction; state.depth = depth; state.scale = GetEntry(id).scale;
                var panel = GetPanel(id);
                if (panel != null)
                {
                    if (!SpatialPanelsEnabled && !panel.IsUtility) SetSpatialPanels(true);
                    state.target = id; state.pinched = true; state.blockedUntilRelease = false;
                    state.depth = Vector3.Distance(ray.origin, panel.WorldCenter);
                    state.offset = panel.WorldCenter - ray.GetPoint(state.depth);
                }
                for (int other = 0; other < gestureStates.Length; other++)
                {
                    if (other == pointer) continue;
                    var partner = gestureStates[other];
                    if (partner.target != id || !partner.pinched || now - partner.sampleTime > .2) continue;
                    float separation = Vector3.Distance(partner.hand, position);
                    if (separation < .04f) continue;
                    pairA = other; pairB = pointer; pairDistance = separation; pairScale = GetEntry(id).scale;
                    break;
                }
            }
            if (state.target == null) return false;
            if (pairA >= 0 && pairB >= 0)
            {
                var a = gestureStates[pairA]; var b = gestureStates[pairB];
                if (a.target != null && a.target == b.target && FaaSpatialLayoutMath.TryResize(pairScale, pairDistance,
                    Vector3.Distance(a.hand, b.hand), out float scale)) SetScale(a.target, scale);
                return true;
            }
            if (GetPanel(state.target) != null)
            {
                float reach = Mathf.Clamp(state.depth + Vector3.Dot(position - state.startHand, state.startDirection) * 2f, .35f, 5f);
                MovePanel(state.target, ray.GetPoint(reach) + state.offset);
            }
            else
            {
                float growth = Vector3.Dot(position - state.startHand, state.startUp) * 3f;
                SetScale(state.target, state.scale * Mathf.Exp(Mathf.Clamp(growth, -2f, 2f)));
            }
            RefreshTransforms(); return true;
        }
    }
}
