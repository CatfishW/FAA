using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class WindDirection : MonoBehaviour
{
    public Text windspeedText;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void UpdateWind(float windHeading, float heliHeading, float windSpeed)
    {
        windSpeed = (int)windSpeed;
        this.transform.localRotation = Quaternion.Euler(0f, 0f, -((windHeading - heliHeading) + 180f));//Wind HPR is at a 180 degree offset from Heli HPR.
        //Negative because heading rotation and Unity Rotation are opposites. Unity increases CCW, heading increases CW.
        windspeedText.text = windSpeed + " kts";
        //Debug.Log("heliHeading is " + heliHeading);
        //Debug.Log("Wind heading is " + windHeading);
    }
}
