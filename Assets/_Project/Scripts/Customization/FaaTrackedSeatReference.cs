using UnityEngine;

namespace FAA.Customization
{
    /// <summary>
    /// Stable seated origin in the actual XR tracking space. Never follows live head pose.
    /// Works with a stationary MR cockpit or a tracking-origin rig parented to a moving virtual aircraft.
    /// A camera may not be directly parented to the aircraft: the current FAA scene uses _Gameplay.
    /// </summary>
    public sealed class FaaTrackedSeatReference
    {
        private Transform stage;
        private Vector3 neutralPosition;
        private Quaternion neutralRotation = Quaternion.identity;
        private bool captured;
        public void Capture(Transform head)
        {
            if (head == null) return;
            stage = head.parent;
            neutralPosition = stage != null ? stage.InverseTransformPoint(head.position) : head.position;
            Vector3 up = stage != null ? stage.up : Vector3.up;
            Vector3 forward = Vector3.ProjectOnPlane(head.forward, up);
            if (forward.sqrMagnitude < .001f) forward = stage != null ? stage.forward : Vector3.forward;
            Quaternion worldRotation = Quaternion.LookRotation(forward.normalized, up);
            neutralRotation = stage != null ? Quaternion.Inverse(stage.rotation) * worldRotation : worldRotation;
            captured = true;
        }
        public bool Apply(Transform target)
        {
            if (!captured || target == null) return false;
            target.SetPositionAndRotation(stage != null ? stage.TransformPoint(neutralPosition) : neutralPosition,
                stage != null ? stage.rotation * neutralRotation : neutralRotation);
            target.localScale = Vector3.one;
            return true;
        }
    }
}
