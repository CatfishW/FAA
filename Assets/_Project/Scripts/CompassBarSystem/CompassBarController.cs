using UnityEngine;
using TMPro;

namespace CompassBarSystem
{
    /// <summary>
    /// Lightweight compass bar controller.
    /// - Scrolls a horizontal tape based on heading degrees.
    /// - Can source heading from a transform yaw (ignores camera orbit) or manual degrees.
    /// - Keeps the bar stable if only the camera moves by defaulting to a follow transform.
    ///
    /// To use:
    /// 1) Create a UI Image (or tape built with your preferred artwork) that repeats horizontally.
    /// 2) Set its RectTransform pivot/anchors to center and assign it to <see cref="compassTape"/>.
    /// 3) Set pixels per degree to match your art and cycle width to the texture/tape repeat length.
    /// 4) Assign a heading target (aircraft/body) and set heading mode to TransformYaw.
    /// </summary>
    [AddComponentMenu("UI/Compass Bar System/Compass Bar Controller")]
    public class CompassBarController : MonoBehaviour
    {
        public enum HeadingMode
        {
            TransformYaw,
            ManualDegrees
        }

        [Header("References")]
        [SerializeField] private RectTransform compassTape;
        [SerializeField] private TMP_Text headingReadout;

        [Header("Heading Source")]
        [SerializeField] private HeadingMode headingMode = HeadingMode.TransformYaw;
        [SerializeField] private Transform headingTarget;
        [Tooltip("Heading used when Heading Mode = Manual Degrees.")]
        [SerializeField] private float manualHeading;

        [Header("Motion")]
        [Tooltip("Pixels moved per heading degree.")]
        [SerializeField] private float pixelsPerDegree = 4f;
        [Tooltip("Width in pixels of a full 360° cycle of your tape artwork (e.g., 360 * pixelsPerDegree, or wider if you stacked repeats).")]
        [SerializeField] private float cycleWidthPixels = 1440f;
        [Tooltip("0 = snap, 1 = instantly follow. Typical 0.1-0.3 for smoothness.")]
        [Range(0f, 1f)]
        [SerializeField] private float smoothing = 0.15f;

        [Header("Readout")]
        [SerializeField] private bool showReadout = true;
        [SerializeField] private string readoutFormat = "{0:000}°";

        float displayedHeading;

        void Awake()
        {
            if (headingTarget == null && Camera.main != null)
            {
                // Default to camera transform only if nothing else is set; user should assign aircraft/root to decouple from camera orbiting.
                headingTarget = Camera.main.transform;
            }
            // Initialize display so the first frame does not jump.
            displayedHeading = GetTargetHeading();
            ApplyReadout();
            ApplyTapePosition();
        }

        void Update()
        {
            float targetHeading = GetTargetHeading();
            if (smoothing <= 0f)
            {
                displayedHeading = targetHeading;
            }
            else
            {
                // Exponential smoothing scaled by frame time for consistent feel.
                float lerpFactor = 1f - Mathf.Pow(1f - smoothing, Time.deltaTime * 60f);
                displayedHeading = Mathf.LerpAngle(displayedHeading, targetHeading, lerpFactor);
            }

            ApplyTapePosition();
            ApplyReadout();
        }

        public void SetHeadingDegrees(float headingDegrees)
        {
            manualHeading = headingDegrees;
        }

        float GetTargetHeading()
        {
            switch (headingMode)
            {
                case HeadingMode.TransformYaw:
                    if (headingTarget != null)
                    {
                        Vector3 forward = headingTarget.forward;
                        forward.y = 0f;
                        if (forward.sqrMagnitude > 0.0001f)
                        {
                            float yaw = Quaternion.LookRotation(forward).eulerAngles.y;
                            return Normalize360(yaw);
                        }
                    }
                    return Normalize360(manualHeading);

                case HeadingMode.ManualDegrees:
                default:
                    return Normalize360(manualHeading);
            }
        }

        void ApplyTapePosition()
        {
            if (compassTape == null) return;

            float offsetPixels = displayedHeading * pixelsPerDegree;
            // Keep values bounded to avoid floating drift in long sessions.
            float wrapped = Mathf.Repeat(offsetPixels, Mathf.Max(1f, cycleWidthPixels));
            float x = -wrapped; // Heading increases -> tape moves left, keeping the referenced heading centered.
            Vector2 pos = compassTape.anchoredPosition;
            pos.x = x;
            compassTape.anchoredPosition = pos;
        }

        void ApplyReadout()
        {
            if (!showReadout || headingReadout == null) return;
            int rounded = Mathf.RoundToInt(Normalize360(displayedHeading));
            headingReadout.text = string.Format(readoutFormat, rounded);
        }

        static float Normalize360(float degrees)
        {
            degrees %= 360f;
            if (degrees < 0f) degrees += 360f;
            return degrees;
        }
    }
}
