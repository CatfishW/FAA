using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class AltitudeAGL : MonoBehaviour
{
    string initialText;
    bool textPresent;
    public int maxVal;
    // Start is called before the first frame update
    void Start()
    {
        if (this.GetComponent<Text>())
        {
            textPresent = true;
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void UpdateText(float altitudeMSL,float altitudeAGL)
    {
        //int tempVal = (int) (altitudeMSL - (altitudeAGL)); //Altitude MSL is in meters
        int tempVal = (int)(altitudeAGL* 3.28084f);
        //Debug.Log("AGL: " + altitudeAGL + "MSL: " + altitudeMSL);
        if (textPresent == true)
        {
            if (tempVal > 0 && tempVal < maxVal)
            {
                this.GetComponent<Text>().text = "AGL " + tempVal.ToString();
            }
            if (tempVal <= 0 && tempVal < maxVal)
            {
                this.GetComponent<Text>().text = "AGL " + "0";
            }
            
            if (tempVal > maxVal)
            {
                this.GetComponent<Text>().text = " ";
            }
        }

    }
}
