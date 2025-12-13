using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PropRPMHUD : MonoBehaviour
{
    public Text EngineTorqueDisplay;
    public GameObject Dial;
    // Start is called before the first frame update
    public float PropRPM;
    public float max = 500f;
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }

    public void UpdatePropRPM(float newNumber)
    {
        PropRPM = newNumber;
        float PropRPMPercent = newNumber / max * 100;

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
        EngineTorqueDisplay.text = PropNumberToString +"%";
        PropRPMPercent = PropRPMPercent * 2.0197f;//205 degrees of freedom for 101.5 percent.
        Dial.transform.localRotation = Quaternion.Euler(0, 0, -PropRPMPercent); //Negative rotation to increase.
    }
}
