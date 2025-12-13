using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class RPM_Pointer : MonoBehaviour
{
    public bool usingDial;
    public Text RPMDisplay;
    public GameObject RPMPointer1;
    public GameObject RPMPointer2;
    public GameObject RPMTri;
    public GameObject Dial;

    public GameObject dialParent;
    public GameObject barParent;


    //public GameObject RPMPointer1;
    // Start is called before the first frame update
    public float PropRPM;
    //public float EngineRPM;
    private float timer = 0f;
    private float updateInterval = 5f;
    private float randomPropRPM = 0f;
    private float randomEngineRPM = 0f;

    void Start()
    {
        usingDial = false;
        //SwitchModes();
        timer = updateInterval;
        randomPropRPM = Random.Range(0f, 120f);
        randomEngineRPM = Random.Range(0f, 120f);
    }

    // Update is called once per frame
    void Update()
    {
        timer += Time.deltaTime;
        if (timer >= updateInterval)
        {
            randomPropRPM = Random.Range(0f, 120f);
            randomEngineRPM = Random.Range(0f, 120f);
            timer = 0f;
        }
        UpdatePropRPM(randomPropRPM);
        UpdateEngineRPM(randomEngineRPM);
    }

    public void UpdatePropRPM(float PropRPMin)
    {
        if(PropRPMin > 110f)
        {
            PropRPMin = 110f;
        }
        //Debug.Log("Prop RPM is" + newNumber);
        PropRPM = PropRPMin;
        float PropRPMPercent = PropRPMin;

        string PropNumberToString = "";
        float candidate = Mathf.Round(PropRPMPercent * 10f) / 10f; //Round 1 decimal point.
        if (candidate < 100 && candidate.ToString().Length == 2) //Make sure .0 is appended on multiples of 10!
        {
            PropNumberToString = candidate.ToString() + ".0";
        }
        else if (candidate == 100)
        {
            PropNumberToString = candidate.ToString() + ".0";
        }
        else
        {
            PropNumberToString = candidate.ToString();
        }
        RPMDisplay.text = PropNumberToString +"%";
        //Switch operations modes
        if(!usingDial)
        {
            RPMTri.transform.localPosition = new Vector3(RPMTri.transform.localPosition.x, PropRPMPercent / 110f * .240f, RPMTri.transform.localPosition.z);//110% is full bar
        }
        else
        {
            Dial.transform.localEulerAngles = new Vector3(0f, 0f, -PropRPMPercent*(200f/100f));//200 degrees of rotation per 100 percent
            //Debug.Log(PropRPMPercent);
        }
    }

    public void UpdateEngineRPM(float EngRPMin)
    {
        //Debug.Log("Engine RPM is" + newNumber2);
        float EngineRpm = EngRPMin;
        float EngineRPMPercent = EngRPMin;

        RPMPointer1.transform.localPosition = new Vector3(RPMPointer1.transform.localPosition.x, EngineRPMPercent / 110f * .208f, RPMPointer1.transform.localPosition.z);//110% is full bar
        RPMPointer2.transform.localPosition = new Vector3(RPMPointer2.transform.localPosition.x, EngineRPMPercent / 110f * .240f, RPMPointer2.transform.localPosition.z);
        //Dial.transform.localRotation = Quaternion.Euler(0, 0, -PropRPMPercent); //Negative rotation to increase.

        //total distance is .248
        //make bar 150% total
        //distance to move pointer = (rpm_percent/150) * .248 
    }

    public void SwitchModes()
    {
        usingDial = !usingDial;
        if(usingDial)
        {
            dialParent.transform.localScale = new Vector3(1f, 1f, 1f);
            barParent.transform.localScale = new Vector3(0f, 0f, 0f);
        }
        else
        {
            dialParent.transform.localScale = new Vector3(0f, 0f, 0f);
            barParent.transform.localScale = new Vector3(1f, 1f, 1f);
        }
    }
}
