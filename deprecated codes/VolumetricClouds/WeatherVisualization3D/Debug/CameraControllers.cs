using UnityEngine;

namespace WeatherVisualization3D
{
    /// <summary>
    /// Simple free-fly camera controller for testing.
    /// WASD to move, right-click + mouse to look, Space/Ctrl for up/down.
    /// </summary>
    [AddComponentMenu("Weather Visualization 3D/Debug/Free Fly Camera")]
    public class FreeFlyCamera : MonoBehaviour
    {
        [Header("Movement")]
        [Tooltip("Base movement speed in units per second")]
        public float moveSpeed = 5000f;
        
        [Tooltip("Speed multiplier when holding Shift")]
        public float fastMoveMultiplier = 3f;
        
        [Tooltip("Vertical movement speed")]
        public float verticalSpeed = 3000f;
        
        [Header("Look")]
        [Tooltip("Mouse sensitivity for looking")]
        public float lookSensitivity = 2f;
        
        [Tooltip("Invert vertical mouse movement")]
        public bool invertY = false;
        
        private float rotationX = 0f;
        private float rotationY = 0f;
        private bool cursorLocked = false;
        
        private void Start()
        {
            Vector3 rot = transform.eulerAngles;
            rotationX = rot.y;
            rotationY = rot.x;
        }
        
        private void Update()
        {
            // Toggle cursor lock with Escape
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                cursorLocked = !cursorLocked;
                Cursor.lockState = cursorLocked ? CursorLockMode.Locked : CursorLockMode.None;
                Cursor.visible = !cursorLocked;
            }
            
            // Right-click to look
            if (Input.GetMouseButton(1))
            {
                rotationX += Input.GetAxis("Mouse X") * lookSensitivity;
                float yDelta = Input.GetAxis("Mouse Y") * lookSensitivity * (invertY ? 1f : -1f);
                rotationY = Mathf.Clamp(rotationY + yDelta, -90f, 90f);
                
                transform.rotation = Quaternion.Euler(rotationY, rotationX, 0f);
            }
            
            // Movement
            float currentSpeed = moveSpeed;
            if (Input.GetKey(KeyCode.LeftShift))
                currentSpeed *= fastMoveMultiplier;
            
            Vector3 move = Vector3.zero;
            
            if (Input.GetKey(KeyCode.W)) move += transform.forward;
            if (Input.GetKey(KeyCode.S)) move -= transform.forward;
            if (Input.GetKey(KeyCode.A)) move -= transform.right;
            if (Input.GetKey(KeyCode.D)) move += transform.right;
            
            if (Input.GetKey(KeyCode.Space)) move += Vector3.up;
            if (Input.GetKey(KeyCode.LeftControl)) move -= Vector3.up;
            
            transform.position += move.normalized * currentSpeed * Time.deltaTime;
        }
    }

    /// <summary>
    /// Orbital camera for viewing the weather from outside.
    /// Automatically rotates around a target point.
    /// </summary>
    [AddComponentMenu("Weather Visualization 3D/Debug/Orbital Test Camera")]
    public class OrbitalTestCamera : MonoBehaviour
    {
        [Header("Target")]
        [Tooltip("Point to orbit around")]
        public Vector3 targetPoint = Vector3.zero;
        
        [Header("Orbit")]
        [Tooltip("Distance from target")]
        public float distance = 50000f;
        
        [Tooltip("Auto-rotation speed in degrees per second")]
        public float rotationSpeed = 5f;
        
        [Tooltip("Zoom speed from scroll wheel")]
        public float zoomSpeed = 10000f;
        
        [Header("Limits")]
        [Tooltip("Minimum distance from target")]
        public float minDistance = 1000f;
        
        [Tooltip("Maximum distance from target")]
        public float maxDistance = 200000f;
        
        private float angle = 0f;
        private float pitch = 30f;
        
        private void Update()
        {
            // Auto rotate
            angle += rotationSpeed * Time.deltaTime;
            
            // Manual control
            if (Input.GetKey(KeyCode.Q)) angle -= rotationSpeed * 5f * Time.deltaTime;
            if (Input.GetKey(KeyCode.E)) angle += rotationSpeed * 5f * Time.deltaTime;
            if (Input.GetKey(KeyCode.W)) pitch = Mathf.Clamp(pitch - rotationSpeed * 5f * Time.deltaTime, 5f, 85f);
            if (Input.GetKey(KeyCode.S)) pitch = Mathf.Clamp(pitch + rotationSpeed * 5f * Time.deltaTime, 5f, 85f);
            
            // Zoom
            distance -= Input.GetAxis("Mouse ScrollWheel") * zoomSpeed;
            distance = Mathf.Clamp(distance, minDistance, maxDistance);
            
            // Calculate position
            float x = distance * Mathf.Sin(angle * Mathf.Deg2Rad) * Mathf.Cos(pitch * Mathf.Deg2Rad);
            float y = distance * Mathf.Sin(pitch * Mathf.Deg2Rad);
            float z = distance * Mathf.Cos(angle * Mathf.Deg2Rad) * Mathf.Cos(pitch * Mathf.Deg2Rad);
            
            transform.position = targetPoint + new Vector3(x, y, z);
            transform.LookAt(targetPoint);
        }
        
        /// <summary>
        /// Set the target point to orbit around.
        /// </summary>
        public void SetTarget(Vector3 target)
        {
            targetPoint = target;
        }
        
        /// <summary>
        /// Set the orbit distance.
        /// </summary>
        public void SetDistance(float newDistance)
        {
            distance = Mathf.Clamp(newDistance, minDistance, maxDistance);
        }
    }
}
