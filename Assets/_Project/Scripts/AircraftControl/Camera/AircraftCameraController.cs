using UnityEngine;
using AircraftControl.Core;

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
        
        [Header("Cockpit Settings")]
        [Tooltip("Offset from aircraft pivot for cockpit camera")]
        [SerializeField] private Vector3 cockpitOffset = new Vector3(0f, 1.5f, 2f);

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
        
        #endregion
        
        #region Unity Lifecycle
        
        private void Start()
        {
            if (aircraftTransform == null)
            {
                Debug.LogError("[AircraftCameraController] No aircraft transform assigned!");
                enabled = false;
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
            _smoothedFlightPathPitch = NormalizeAngle(aircraftTransform.rotation.eulerAngles.x);
        }
        
        private void LateUpdate()
        {
            if (aircraftTransform == null) return;
            
            // Check for mode cycle
            if (Input.GetKeyDown(cycleModeKey))
            {
                CycleCameraMode();
            }
            
            // Check for reset
            if (Input.GetKeyDown(resetKey))
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
            bool wasLookActive = _isLookActive;
            _isLookActive = Input.GetMouseButton(lookButton);
            
            if (_isLookActive)
            {
                // Get raw mouse input
                float rawMouseX = Input.GetAxis("Mouse X");
                float rawMouseY = Input.GetAxis("Mouse Y");
                
                // Smooth the input
                float smoothFactor = mouseSmoothing * 60f * Time.deltaTime;
                _smoothedMouseX = Mathf.Lerp(_smoothedMouseX, rawMouseX, smoothFactor);
                _smoothedMouseY = Mathf.Lerp(_smoothedMouseY, rawMouseY, smoothFactor);
                
                // Apply to look angles
                _currentYaw += _smoothedMouseX * mouseSensitivity;
                _currentPitch -= _smoothedMouseY * mouseSensitivity;
                
                // Clamp angles
                _currentPitch = Mathf.Clamp(_currentPitch, minPitch, maxPitch);
                _currentYaw = Mathf.Clamp(_currentYaw, -maxYaw, maxYaw);
            }
            else
            {
                // Smooth return to center
                float returnFactor = returnSpeed * Time.deltaTime;
                
                _currentPitch = Mathf.Lerp(_currentPitch, _targetPitch, returnFactor);
                _currentYaw = Mathf.Lerp(_currentYaw, _targetYaw, returnFactor);
                
                // Clear smoothed values
                _smoothedMouseX = Mathf.Lerp(_smoothedMouseX, 0f, returnFactor);
                _smoothedMouseY = Mathf.Lerp(_smoothedMouseY, 0f, returnFactor);
            }
            
            // Store start rotation when starting to look
            if (_isLookActive && !wasLookActive)
            {
                _freeRotation = transform.rotation;
            }
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
            float basePitch = NormalizeAngle(aircraftEuler.x);
            float compensatedPitch = GetCompensatedPitch(basePitch);
            aircraftEuler.x = compensatedPitch;
            Quaternion compensatedRotation = Quaternion.Euler(aircraftEuler);

            // Apply vertical head-lag offset driven by climb/dive
            cockpitPosition += GetVerticalOffset(compensatedRotation);
            transform.position = cockpitPosition;
            
            // Combine compensated aircraft rotation with look offset (local rotation)
            Quaternion lookOffset = Quaternion.Euler(_currentPitch, _currentYaw, 0f);
            transform.rotation = compensatedRotation * lookOffset;
        }
        
        private void UpdateChaseCamera()
        {
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
            
            transform.rotation = baseLookRotation * lookOffset;
        }
        
        private void UpdateFreeCamera()
        {
            // Track aircraft rotation change
            Quaternion aircraftDelta = aircraftTransform.rotation * Quaternion.Inverse(_lastAircraftRotation);
            _lastAircraftRotation = aircraftTransform.rotation;
            
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
        
        #endregion
    }
}
