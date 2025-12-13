using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AltitudeRecord : MonoBehaviour
{
    // Variable to store the altitude
    private float altitude;
    public AltitudeHUD altitudeHUD;

    // Start is called before the first frame update
    void Start()
    {
        // Initialize altitude
        altitude = transform.position.y;
        if (altitudeHUD == null)
        {
            altitudeHUD = FindObjectOfType<AltitudeHUD>();
        }
    }

    // Update is called once per frame
    void Update()
    {
        // Update the altitude based on the aircraft's position
        altitude = transform.position.y;
        altitudeHUD.UpdateAltitude(altitude);
        //Debug.Log("Current Altitude: " + altitude);
    }
}