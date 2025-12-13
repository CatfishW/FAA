using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FPVOrb : MonoBehaviour
{
    public float Aircraft_Heading;
    public float hpath_Heading;
    public float HeadingHpathDiff;
    public float sensitivityFactor;
    public Vector3 startScale;
    // Start is called before the first frame update
    void Start()
    {
        startScale = transform.localScale;
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void UpdateFPV(float hpath, float vpath, float courseHeading, float psi, float true_psi, float magDeviation, float airspeed)
    {
        if(airspeed > 15f)
        {
            transform.localScale = startScale;
            //vpath float 660+ no degrees The pitch the aircraft actually flies
            //hpath float 660+ no degrees The heading the aircraft actually flies.
            float beta = psi - hpath;
            //Debug.Log("hpath: " + hpath.ToString() + " vpath: " + vpath.ToString() + "Aircraft heading: " + courseHeading.ToString());
            //Debug.Log("psi: " + psi.ToString() + " true_psi: " + true_psi.ToString() + "Aircraft heading: " + courseHeading.ToString());

            // Debug.Log("Beta: " + (psi - hpath).ToString() ); 

            HeadingHpathDiff = courseHeading - hpath;
            hpath_Heading = hpath;

            Aircraft_Heading = courseHeading;

            //Debug.Log("Difference between course heading and hpath: " + tempNum.ToString());
            //transform.rotation = Quaternion.Euler(new Vector3(pitch, 0f, roll));
            transform.rotation = Quaternion.Euler(new Vector3(-vpath * sensitivityFactor, -beta * sensitivityFactor, 0f));
        }
        else
        {
            transform.localScale = Vector3.zero;
        }
    }

}
