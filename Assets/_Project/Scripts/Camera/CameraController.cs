using UnityEngine;

public class CameraController : MonoBehaviour
{
    public float rotationSpeed = 5.0f;
    public float smoothReturnSpeed = 2.0f;
    public Transform aircraftTransform;
    public KeyCode resetKey = KeyCode.R;
    [SerializeField] private bool disableWhenTargetMissing = true;
    
    private bool isRightMouseHeld = false;
    private Quaternion freeRotation;
    private bool warnedMissingTarget;
    
    void Start()
    {
        if (!ResolveAircraftTransform())
        {
            freeRotation = transform.rotation;
            WarnMissingTarget();
            if (disableWhenTargetMissing)
            {
                enabled = false;
            }
            return;
        }

        freeRotation = aircraftTransform.rotation;
    }

    void Update()
    {
        if (aircraftTransform == null && !ResolveAircraftTransform())
        {
            WarnMissingTarget();
            return;
        }

        if (Input.GetMouseButtonDown(1))
        {
            isRightMouseHeld = true;
            freeRotation = transform.rotation;
        }

        if (Input.GetMouseButtonUp(1))
        {
            isRightMouseHeld = false;
        }

        if (Input.GetKeyDown(resetKey))
        {
            freeRotation = aircraftTransform.rotation;
            transform.rotation = aircraftTransform.rotation;
        }

        if (isRightMouseHeld)
        {
            float yawChange = rotationSpeed * Input.GetAxis("Mouse X");
            float pitchChange = -rotationSpeed * Input.GetAxis("Mouse Y");

            freeRotation *= Quaternion.Euler(pitchChange, yawChange, 0);

            Vector3 angles = freeRotation.eulerAngles;
            if (angles.x > 180f)
            {
                angles.x -= 360f;
            }

            angles.x = Mathf.Clamp(angles.x, -80.0f, 80.0f);
            freeRotation = Quaternion.Euler(angles);
            transform.rotation = freeRotation;
        }
        else
        {
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                aircraftTransform.rotation,
                smoothReturnSpeed * Time.deltaTime);
            freeRotation = transform.rotation;
        }
    }

    private bool ResolveAircraftTransform()
    {
        if (aircraftTransform != null)
        {
            return true;
        }

        AircraftControl.Core.AircraftController aircraftController = FindFirstObjectByType<AircraftControl.Core.AircraftController>();
        if (aircraftController != null)
        {
            aircraftTransform = aircraftController.transform;
            return true;
        }

        string[] candidateNames =
        {
            "X-Plane Ownship",
            "Aircraft",
            "Ownship",
            "OwnShip",
            "PlayerAircraft",
            "Helicopter_Robinson_R22_Red",
            "Helicopter"
        };

        foreach (string candidateName in candidateNames)
        {
            GameObject candidate = GameObject.Find(candidateName);
            if (candidate != null)
            {
                aircraftTransform = candidate.transform;
                return true;
            }
        }

        try
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                aircraftTransform = player.transform;
                return true;
            }
        }
        catch (UnityException)
        {
        }

        return false;
    }

    private void WarnMissingTarget()
    {
        if (warnedMissingTarget)
        {
            return;
        }

        warnedMissingTarget = true;
        Debug.LogWarning("[CameraController] No aircraft transform assigned or found. Free-look camera return is disabled until a target is assigned.", this);
    }
}
