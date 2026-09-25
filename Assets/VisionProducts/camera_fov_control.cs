using UnityEngine;

public class camera_fov_control: MonoBehaviour
{
    static private float vFOV = 33;
    static private float hFOV = 53;

    static public float VFOV
    {
        get { return vFOV; }
        set { vFOV = value; }
    }
    static public float HFOV
    {
        set { hFOV = value; }
        get { return hFOV; }
    }

    private Camera cam;
    // Start is called before the first frame update
    void Start()
    {
        cam = GetComponent<Camera>();
        cam.aspect = Mathf.Abs(Mathf.Tan(Mathf.Deg2Rad * hFOV / 2) / Mathf.Tan(Mathf.Deg2Rad * vFOV / 2));
        cam.fieldOfView = vFOV;
    }

    private void Update()
    {
        cam = GetComponent<Camera>();
        cam.aspect = Mathf.Abs(Mathf.Tan(Mathf.Deg2Rad * hFOV / 2) / Mathf.Tan(Mathf.Deg2Rad * vFOV / 2));
        cam.fieldOfView = vFOV;
    }
}
