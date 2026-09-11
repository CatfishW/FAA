using UnityEngine;
using AircraftControl.Core;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
#endif

namespace AircraftControl.Camera
{
    /// <summary>
    /// Enhanced aircraft camera controller with smooth mouse movement and multiple view modes.
    /// Features:
    /// - Right-click to look around freely
    /// - Smooth return to forward view when releasing mouse
    /// - Multiple camera modes (Cockpit, Chase, Free)
    /// - Configurable sensitivity and smoothing
    /// 
    /// Setup:
    /// 1. Add this component to your camera
    /// 2. Assign the aircraft transform
    /// 3. Configure camera mode and settings
    /// </summary>
    [AddComponentMenu("Aircraft Control/Aircraft Camera Controller")]
    [DefaultExecutionOrder(11100)]
    public class AircraftCameraController : MonoBehaviour
    {
        #region Camera Modes
        
        public enum CameraMode
        {
            Cockpit,    // Fixed position in cockpit, rotates with look input
            Chase,      // Follows behind aircraft
            Free        // Free orbit around aircraft
        }
        
        #endregion
        
        #region Inspector Settings
        
        [Header("Target")]
        [Tooltip("The aircraft transform to follow")]
        [SerializeField] private Transform aircraftTransform;
        
        [Header("Camera Mode")]
        [SerializeField] private CameraMode cameraMode = CameraMode.Cockpit;
        
        [Header("Mouse Settings")]
        [Tooltip("Mouse sensitivity for looking around")]
        [Range(0.5f, 10f)]
        [SerializeField] private float mouseSensitivity = 3f;
        
        [Tooltip("Smoothing factor for mouse input (lower = smoother)")]
        [Range(0.01f, 1f)]
        [SerializeField] private float mouseSmoothing = 0.15f;
        
        [Tooltip("Speed to return to aircraft heading when not looking")]
        [Range(0.5f, 10f)]
        [SerializeField] private float returnSpeed = 2.5f;
        
        [Header("Look Limits")]
        [Tooltip("Maximum pitch angle (degrees up)")]
        [Range(30f, 89f)]
        [SerializeField] private float maxPitch = 80f;
        
        [Tooltip("Minimum pitch angle (degrees down)")]
        [Range(-89f, -30f)]
        [SerializeField] private float minPitch = -70f;
        
        [Tooltip("Maximum yaw angle from center (degrees left/right)")]
        [Range(90f, 180f)]
        [SerializeField] private float maxYaw = 160f;
        
        [Header("Chase Mode Settings")]
        [Tooltip("Distance behind aircraft")]
        [SerializeField] private float chaseDistance = 10f;
        
        [Tooltip("Height above aircraft")]
        [SerializeField] private float chaseHeight = 3f;
        
        [Tooltip("Chase position smoothing")]
        [Range(0.01f, 1f)]
        [SerializeField] private float chaseSmoothing = 0.1f;

        [Tooltip("Chase rotation smoothing")]
        [Range(0f, 1f)]
        [SerializeField] private float chaseRotationSmoothing = 0.12f;
        
        [Header("Cockpit Settings")]
        [Tooltip("Offset from aircraft pivot for cockpit camera")]
        [SerializeField] private Vector3 cockpitOffset = new Vector3(0f, 1.5f, 2f);

        [Tooltip("Smooth cockpit position to reduce jitter (0 = instant)")]
        [Range(0f, 1f)]
        [SerializeField] private float cockpitPositionSmoothing = 0.1f;

        [Tooltip("Smooth cockpit rotation to reduce jitter (0 = instant)")]
        [Range(0f, 1f)]
        [SerializeField] private float cockpitRotationSmoothing = 0.15f;

        [Header("HUD First Person Compensation")]
        [Tooltip("Aircraft controller used to derive climb/dive information for HUD-ready motion")]
        [SerializeField] private AircraftController aircraftController;

        [Tooltip("Auto-assign AircraftController from the aircraft transform if left empty")]
        [SerializeField] private bool autoFindAircraftController = true;

        [Tooltip("Blend toward the flight-path pitch so HUD feels anchored to real motion (0 = attitude only, 1 = full flight-path)")]
        [Range(0f, 1f)]
        [SerializeField] private float flightPathBlend = 0.6f;

        [Tooltip("Maximum pitch offset (degrees) contributed by flight-path blending")]
        [SerializeField] private float maxPitchCompensation = 12f;

        [Tooltip("Maximum vertical head-lag offset in meters when pulling positive/negative flight path angles")]
        [SerializeField] private float verticalOffsetMeters = 0.15f;

        [Tooltip("Vertical speed in m/s that produces full head-lag offset")]
        [SerializeField] private float verticalSpeedForFullOffset = 25f;

        [Tooltip("How quickly climb/dive compensation reacts (higher = snappier)")]
        [SerializeField] private float compensationSmoothing = 4f;
        
        [Header("Input")]
        [Tooltip("Mouse button to hold for free look (0=left, 1=right, 2=middle)")]
        [SerializeField] private int lookButton = 1;
        
        [Tooltip("Key to reset camera to forward view")]
        [SerializeField] private KeyCode resetKey = KeyCode.R;
        
        [Tooltip("Key to cycle camera modes")]
        [SerializeField] private KeyCode cycleModeKey = KeyCode.V;
        
        [Header("Debug")]
        [SerializeField] private bool showDebugInfo = false;
        
        #endregion
        
        #region Private Fields
        
        private bool _isLookActive;
        private bool _lookWasHeld;
        private float _returnElapsed;
        private Vector2 _returnStart;
        [SerializeField, Min(0.1f)] private float autoReturnSeconds = 0.8f;
        private float _currentPitch;
        private float _currentYaw;
        
        // Smoothed mouse input
        private float _smoothedMouseX;
        private float _smoothedMouseY;
        
        // Target for smooth return
        private float _targetPitch;
        private float _targetYaw;
        
        // Chase mode
        private Vector3 _chaseVelocity;
        
        // Free rotation storage
        private Quaternion _freeRotation;
        private Quaternion _lastAircraftRotation;

        // First person HUD compensation
        private float _smoothedFlightPathPitch;
        private float _currentVerticalOffset;
        private Quaternion _aircraftReferenceRotation = Quaternion.identity;
        
        #endregion
        
        #region Properties
        
        /// <summary>
        /// Current camera mode
        /// </summary>
        public CameraMode Mode
        {
            get => cameraMode;
            set => SetCameraMode(value);
        }
        
        /// <summary>
        /// Whether the user is currently looking around
        /// </summary>
        public bool IsLookActive => _isLookActive;
        public Transform AircraftTransform => aircraftTransform;

        /// <summary>
        /// The aircraft-relative cockpit viewing rotation with the manual
        /// look-around offset removed. Conformal HUDs use this reference so
        /// their boresight remains tied to the aircraft while the pilot looks
        /// through the side windows.
        /// </summary>
        public Quaternion AircraftReferenceRotation => _aircraftReferenceRotation;
        
        #endregion
        
        #region Unity Lifecycle
        
        private void Start()
        {
            if (!ResolveTarget())
            {
                Debug.LogWarning("[AircraftCameraController] Waiting for an aircraft target; view will bind when it becomes available.");
                return;
            }

            if (autoFindAircraftController && aircraftController == null)
            {
                aircraftController = aircraftTransform.GetComponent<AircraftController>()
                    ?? aircraftTransform.GetComponentInParent<AircraftController>();
            }
            
            // Initialize rotation tracking
            _freeRotation = aircraftTransform.rotation;
            _lastAircraftRotation = aircraftTransform.rotation;
            _currentPitch = 0f;
            _currentYaw = 0f;
            _smoothedFlightPathPitch = GetAircraftPitchDegrees();
            _aircraftReferenceRotation = aircraftTransform.rotation;
        }
        
        private void LateUpdate()
        {
            if (!ResolveTarget()) return;
#if ENABLE_INPUT_SYSTEM
            // A real HMD (or explicit XR simulator test) owns its pose. Never
            // spring a physically tracked head back to the aircraft direction.
            var trackedPose = GetComponent<TrackedPoseDriver>();
            if (trackedPose != null && trackedPose.isActiveAndEnabled) return;
#endif
            
            // Check for mode cycle
            if (KeyPressed(cycleModeKey))
            {
                CycleCameraMode();
            }
            
            // Check for reset
            if (KeyPressed(resetKey))
            {
                ResetView();
            }
            
            // Handle look input
            HandleLookInput();
            
            // Update camera based on mode
            switch (cameraMode)
            {
                case CameraMode.Cockpit:
                    UpdateCockpitCamera();
                    break;
                case CameraMode.Chase:
                    UpdateChaseCamera();
                    break;
                case CameraMode.Free:
                    UpdateFreeCamera();
                    break;
            }
        }
        
        #endregion
        
        #region Input Handling
        
        private void HandleLookInput()
        {
            bool held;
            Vector2 delta;
#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            held = mouse != null && (lookButton == 0 ? mouse.leftButton.isPressed :
                lookButton == 2 ? mouse.middleButton.isPressed : mouse.rightButton.isPressed);
            delta = mouse != null ? mouse.delta.ReadValue() * 0.1f : Vector2.zero;
#else
            held = Input.GetMouseButton(lookButton);
            delta = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
#endif
            ProcessLookInput(held && Application.isFocused, delta,
                EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(), Time.unscaledDeltaTime);
        }

        private void ProcessLookInput(bool held, Vector2 delta, bool overUi, float dt)
        {
            bool wasActive = _isLookActive;
            // Only a press that starts outside UI can capture the view. Dragging
            // a chart or slider must not turn the camera after leaving that UI.
            if (!held) _isLookActive = false;
            else if (!_lookWasHeld) _isLookActive = !overUi;
            _lookWasHeld = held;
            if (_isLookActive)
            {
                if (!wasActive) _freeRotation = aircraftTransform != null ? aircraftTransform.rotation : transform.rotation;
                _currentYaw = Mathf.Clamp(_currentYaw + delta.x * mouseSensitivity, -maxYaw, maxYaw);
                _currentPitch = Mathf.Clamp(_currentPitch - delta.y * mouseSensitivity, minPitch, maxPitch);
                _returnStart = new Vector2(_currentPitch, _currentYaw);
                _returnElapsed = 0f;
                return;
            }
            _returnElapsed += Mathf.Max(0f, dt);
            Vector2 offset = ReturnLookOffset(_returnStart, _returnElapsed, autoReturnSeconds);
            _currentPitch = offset.x;
            _currentYaw = offset.y;
            _smoothedMouseX = _smoothedMouseY = 0f;
        }

        public static Vector2 ReturnLookOffset(Vector2 start, float elapsed, float duration)
        {
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.1f, duration));
            return start * (1f - t * t * (3f - 2f * t));
        }

        private static bool KeyPressed(KeyCode key)
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && System.Enum.TryParse(key.ToString(), true, out Key inputKey)
                && inputKey != Key.None && Keyboard.current[inputKey].wasPressedThisFrame;
#else
            return Input.GetKeyDown(key);
#endif
        }

        private bool ResolveTarget()
        {
            if (aircraftTransform != null) return true;
            if (aircraftController == null && autoFindAircraftController)
                aircraftController = FindFirstObjectByType<AircraftController>();
            if (aircraftController == null) return false;
            SetTarget(aircraftController.transform);
            return true;
        }

        private void OnApplicationFocus(bool focused)
        {
            if (focused) return;
            _isLookActive = false;
            _returnStart = new Vector2(_currentPitch, _currentYaw);
            _returnElapsed = 0f;
        }
        
        #endregion
        
        #region Camera Mode Updates
        
        private void UpdateCockpitCamera()
        {
            // Position in cockpit
            Vector3 cockpitPosition = aircraftTransform.TransformPoint(cockpitOffset);

            // Calculate base rotation and then apply climb/dive compensation so HUD stays believable in first person
            Quaternion aircraftRotation = aircraftTransform.rotation;
            Vector3 aircraftEuler = aircraftRotation.eulerAngles;
            float basePitch = GetAircraftPitchDegrees();
            float compensatedPitch = GetCompensatedPitch(basePitch);
            aircraftEuler.x = -compensatedPitch; // Invert so nose-up points camera to the sky
            Quaternion compensatedRotation = Quaternion.Euler(aircraftEuler);
            _aircraftReferenceRotation = compensatedRotation;

            // Apply vertical head-lag offset driven by climb/dive
            cockpitPosition += GetVerticalOffset(compensatedRotation);
            float positionLerp = GetSmoothingFactor(cockpitPositionSmoothing);
            if (cockpitPositionSmoothing <= 0f)
            {
                transform.position = cockpitPosition;
            }
            else
            {
                transform.position = Vector3.Lerp(transform.position, cockpitPosition, positionLerp);
            }
            
            // Combine compensated aircraft rotation with look offset (local rotation)
            Quaternion lookOffset = Quaternion.Euler(_currentPitch, _currentYaw, 0f);
            Quaternion targetRotation = compensatedRotation * lookOffset;
            // The manual offset already has a timed smooth return. Filtering
            // aircraft heading here introduces a second, misleading view lag.
            transform.rotation = targetRotation;
        }
        
        private void UpdateChaseCamera()
        {
            _aircraftReferenceRotation = aircraftTransform.rotation;
            // Calculate desired position behind aircraft
            Vector3 desiredPosition = aircraftTransform.position 
                - aircraftTransform.forward * chaseDistance 
                + Vector3.up * chaseHeight;
            
            // Smooth follow
            transform.position = Vector3.SmoothDamp(
                transform.position, 
                desiredPosition, 
                ref _chaseVelocity, 
                chaseSmoothing
            );
            
            // Look at aircraft with look offset
            Vector3 lookTarget = aircraftTransform.position;
            Quaternion baseLookRotation = Quaternion.LookRotation(lookTarget - transform.position);
            Quaternion lookOffset = Quaternion.Euler(_currentPitch, _currentYaw, 0f);
            
            Quaternion targetRotation = baseLookRotation * lookOffset;
            float rotationLerp = GetSmoothingFactor(chaseRotationSmoothing);
            if (chaseRotationSmoothing <= 0f)
            {
                transform.rotation = targetRotation;
            }
            else
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationLerp);
            }
        }
        
        private void UpdateFreeCamera()
        {
            _lastAircraftRotation = aircraftTransform.rotation;
            _aircraftReferenceRotation = aircraftTransform.rotation;
            
            // Update free rotation with aircraft movement and look input
            if (!_isLookActive)
            {
                // Follow aircraft rotation smoothly
                _freeRotation = Quaternion.Slerp(_freeRotation, aircraftTransform.rotation, returnSpeed * Time.deltaTime);
            }
            
            // Apply look offset
            Quaternion lookOffset = Quaternion.Euler(_currentPitch, _currentYaw, 0f);
            transform.rotation = _freeRotation * lookOffset;
            
            // Position follows aircraft
            transform.position = aircraftTransform.position;
        }
        
        #endregion
        
        #region Public Methods
        
        /// <summary>
        /// Set the camera mode
        /// </summary>
        public void SetCameraMode(CameraMode mode)
        {
            cameraMode = mode;
            ResetView();
            
            if (showDebugInfo)
            {
                Debug.Log($"[AircraftCameraController] Mode changed to: {mode}");
            }
        }
        
        /// <summary>
        /// Cycle through available camera modes
        /// </summary>
        public void CycleCameraMode()
        {
            int nextMode = ((int)cameraMode + 1) % 3;
            SetCameraMode((CameraMode)nextMode);
        }
        
        /// <summary>
        /// Reset camera view to forward
        /// </summary>
        public void ResetView()
        {
            _returnStart = Vector2.zero;
            _returnElapsed = 0f;
            _currentPitch = 0f;
            _currentYaw = 0f;
            _targetPitch = 0f;
            _targetYaw = 0f;
            _smoothedMouseX = 0f;
            _smoothedMouseY = 0f;
            
            if (aircraftTransform != null)
            {
                _freeRotation = aircraftTransform.rotation;
                _lastAircraftRotation = aircraftTransform.rotation;
            }
        }
        
        /// <summary>
        /// Set the target aircraft transform
        /// </summary>
        public void SetTarget(Transform target)
        {
            aircraftTransform = target;
            if (autoFindAircraftController && target != null)
                aircraftController = target.GetComponent<AircraftController>() ?? target.GetComponentInParent<AircraftController>();
            if (target != null)
            {
                _freeRotation = target.rotation;
                _lastAircraftRotation = target.rotation;
            }
        }
        
        /// <summary>
        /// Set look sensitivity
        /// </summary>
        public void SetSensitivity(float sensitivity)
        {
            mouseSensitivity = Mathf.Clamp(sensitivity, 0.5f, 10f);
        }
        
        #endregion
        
        #region Debug
        
        private void OnGUI()
        {
            if (!showDebugInfo) return;
            
            GUILayout.BeginArea(new Rect(Screen.width - 260, 10, 250, 150));
            GUILayout.BeginVertical("box");
            
            GUILayout.Label("=== Camera Controller ===");
            GUILayout.Label($"Mode: {cameraMode}");
            GUILayout.Label($"Looking: {_isLookActive}");
            GUILayout.Label($"Pitch: {_currentPitch:F1}° | Yaw: {_currentYaw:F1}°");
            GUILayout.Label($"[RMB] Look | [R] Reset | [V] Cycle Mode");
            
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        #endregion

        #region Helpers

        private float GetAircraftPitchDegrees()
        {
            if (aircraftController != null && aircraftController.State != null)
            {
                return aircraftController.State.Pitch;
            }

            if (aircraftTransform == null)
            {
                return 0f;
            }

            Vector3 forward = aircraftTransform.forward;
            float horizontalMag = new Vector2(forward.x, forward.z).magnitude;
            return Mathf.Atan2(forward.y, horizontalMag) * Mathf.Rad2Deg;
        }

        private float GetCompensatedPitch(float basePitch)
        {
            if (aircraftController == null || aircraftController.State == null || flightPathBlend <= 0f)
            {
                return basePitch;
            }

            var state = aircraftController.State;
            float flightPathPitch = Mathf.Atan2(state.VerticalSpeedMps, Mathf.Max(state.GroundSpeedMps, 0.1f)) * Mathf.Rad2Deg;
            float clampedPitch = Mathf.Clamp(flightPathPitch, basePitch - maxPitchCompensation, basePitch + maxPitchCompensation);
            _smoothedFlightPathPitch = Mathf.Lerp(_smoothedFlightPathPitch, clampedPitch, Time.deltaTime * compensationSmoothing);
            return Mathf.Lerp(basePitch, _smoothedFlightPathPitch, flightPathBlend);
        }

        private Vector3 GetVerticalOffset(Quaternion referenceRotation)
        {
            if (aircraftController == null || aircraftController.State == null || verticalOffsetMeters <= 0f)
            {
                return Vector3.zero;
            }

            var state = aircraftController.State;
            float normalizedVs = 0f;
            if (verticalSpeedForFullOffset > 0.01f)
            {
                normalizedVs = Mathf.Clamp(state.VerticalSpeedMps / verticalSpeedForFullOffset, -1f, 1f);
            }

            float targetOffset = -normalizedVs * verticalOffsetMeters; // Positive climb pushes pilot down into the seat
            _currentVerticalOffset = Mathf.Lerp(_currentVerticalOffset, targetOffset, Time.deltaTime * compensationSmoothing);
            return referenceRotation * (Vector3.up * _currentVerticalOffset);
        }

        private static float NormalizeAngle(float angle)
        {
            angle %= 360f;
            if (angle > 180f) angle -= 360f;
            return angle;
        }

        private static float GetSmoothingFactor(float smoothing)
        {
            if (smoothing <= 0f)
            {
                return 1f;
            }

            return Mathf.Clamp01(smoothing * 60f * Time.deltaTime);
        }
        
        #endregion
    }
}
