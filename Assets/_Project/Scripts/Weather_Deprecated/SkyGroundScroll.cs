using UnityEngine;

public class SkyGroundScroll : MonoBehaviour
{
    public RectTransform horizon; // The blue/brown element
    public Transform rollIndicator; // The roll frame
    public float pitchSensitivity = 10f; // Adjust for fine-tuning
    public float rollSensitivity = 1f;

    private float pitch; // Simulated pitch angle
    private float roll; // Simulated roll angle

    void Update()
    {
        // Fetch pitch and roll values (replace with actual data source)
        pitch = Mathf.Sin(Time.time) * 10f; // Example pitch oscillation
        roll = Mathf.Cos(Time.time) * 45f; // Example roll oscillation

        // Apply pitch to move the horizon
        horizon.localPosition = new Vector3(
            horizon.localPosition.x,
            pitch * pitchSensitivity,
            horizon.localPosition.z
        );

        // Apply roll to rotate the indicator
        rollIndicator.localRotation = Quaternion.Euler(0, 0, -roll * rollSensitivity);
    }
}
