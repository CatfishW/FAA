using System.Collections.Generic;
using UnityEngine;

namespace FAA.Customization
{
    /// <summary>
    /// Explicit, scene-registered reference. This component does not claim that a landing area is safe,
    /// surveyed, obstacle-free or approved. Attach to real scene geometry; no auto-generated hazards.
    /// Keep the object under the same floating-origin/georeference hierarchy as its terrain.
    /// </summary>
    [DisallowMultipleComponent, AddComponentMenu("FAA/HUD/Rotorcraft Scene Cue Anchor")]
    public sealed class FaaRotorcraftCueAnchor : MonoBehaviour
    {
        public enum CueType { LandingArea, Waypoint, Obstacle, HoverReference }
        public CueType kind = CueType.Waypoint;
        public string identifier = "REF";
        [Tooltip("Require explicit selection for touchdown/hover references.")]
        public bool selected;
        [Tooltip("Declared reference area in scene metres; not an automatically measured helipad.")]
        public Vector2 areaMeters = new Vector2(20f, 20f);
        [Tooltip("Disable when this source is invalid, unregistered or unavailable.")]
        public bool positionValid = true;
        [Tooltip("Optional second endpoint for a surveyed/modelled wire or linear obstacle.")]
        public Transform lineEnd;
        [Min(1f)] public float maximumRangeMeters = 15000f;

        private static readonly HashSet<FaaRotorcraftCueAnchor> Registered = new();
        public static IEnumerable<FaaRotorcraftCueAnchor> Active => Registered;
        private void OnEnable() => Registered.Add(this);
        private void OnDisable() => Registered.Remove(this);
        private void OnDestroy() => Registered.Remove(this);

        public bool TryPosition(out Vector3 position)
        {
            position = transform.position;
            return isActiveAndEnabled && positionValid && FaaRotorcraftCueMath.Finite(position);
        }

        public void Select()
        {
            foreach (var anchor in Registered)
                if (anchor != null && (anchor.kind == CueType.LandingArea || anchor.kind == CueType.HoverReference))
                    anchor.selected = false;
            selected = true;
        }
    }
}
