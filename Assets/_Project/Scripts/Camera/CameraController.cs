using UnityEngine;

public class CameraController : MonoBehaviour
{
    public float rotationSpeed = 5.0f;
    public float smoothReturnSpeed = 2.0f; // Speed to return to aircraft orientation
    public Transform aircraftTransform; // Reference to the aircraft's transform
    public KeyCode resetKey = KeyCode.R; // Key to instantly reset camera orientation
    
    private bool isRightMouseHeld = false;
    private Quaternion freeRotation; // The rotation when in free-look mode
    
    void Start()
    {
        // Initialize with aircraft's rotation
        freeRotation = aircraftTransform.rotation;
    }

    void Update()
    {
        try {
            // Handle input
            if (Input.GetMouseButtonDown(1))
            {
                isRightMouseHeld = true;
                // Store current rotation as starting point for free rotation
                freeRotation = transform.rotation;
            }
            if (Input.GetMouseButtonUp(1))
            {
                isRightMouseHeld = false;
            }
            
            // Reset camera orientation when reset key is pressed
            if (Input.GetKeyDown(resetKey))
            {
                freeRotation = aircraftTransform.rotation;
                transform.rotation = aircraftTransform.rotation;
            }
            
            // Always follow aircraft position
            //transform.position = aircraftTransform.position;
            
            if (isRightMouseHeld)
            {
                // Calculate rotation around world axes based on mouse input
                float yawChange = rotationSpeed * Input.GetAxis("Mouse X");
                float pitchChange = -rotationSpeed * Input.GetAxis("Mouse Y");
                
                // Apply rotations to our free rotation quaternion
                freeRotation *= Quaternion.Euler(pitchChange, yawChange, 0);
                
                // Extract pitch for clamping
                Vector3 angles = freeRotation.eulerAngles;
                // Adjust angles to -180 to 180 range for proper clamping
                if (angles.x > 180) angles.x -= 360;
                // Clamp pitch
                angles.x = Mathf.Clamp(angles.x, -80.0f, 80.0f);
                // Apply clamped rotation
                freeRotation = Quaternion.Euler(angles);
                
                // Apply the free rotation
                transform.rotation = freeRotation;
            }
            else
            {
                // Smoothly return to aircraft orientation when not actively controlling
                transform.rotation = Quaternion.Slerp(transform.rotation, 
                                                     aircraftTransform.rotation, 
                                                     smoothReturnSpeed * Time.deltaTime);
                // Update our free rotation to match the current rotation
                freeRotation = transform.rotation;
            }
        }
        catch (System.Exception e) {
            Debug.LogError($"Camera Controller Error: {e.Message}");
        }
    }
}