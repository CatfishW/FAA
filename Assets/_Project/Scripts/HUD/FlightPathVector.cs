using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FlightPathVector : MonoBehaviour
{
    public float RelativePitch;
    public float RelativeHeading;
    Vector3 initialScale;
    // Start is called before the first frame update
    void Start()
    {
        initialScale = transform.localScale;
    }

    // Update is called once per frame
    void Update()
    {

    }

    public void UpdateFPV(float alpha, float beta, float airspeed)//alpha is vpath - pitch, beta is hpath - heading
    {
        if (airspeed > 0f)
        {
            if(transform.localScale != initialScale)
            {
                transform.localScale = initialScale;
            }
            RelativePitch = alpha;
            RelativeHeading = beta; //Heading needs to be inverted. Not sure why. Also beta is consistently 12 below heading.
                                             //Debug.Log("vpath " + RelativePitch);
                                             //Debug.Log("hpath " + RelativeHeading);
            transform.localPosition = new Vector3(RelativeHeading * 0.002f, RelativePitch * 0.002f);//Pitch needs to be more sensitive.
        }
        else
        {
            if(transform.localScale != Vector3.zero)
            transform.localScale = Vector3.zero;
        }
    }
}
