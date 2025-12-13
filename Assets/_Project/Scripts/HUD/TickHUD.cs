using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TickHUD : MonoBehaviour
{
    public Text NumText;
    public int RealAlt;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {

    }

    public void SetAltitude(int Altitude)
    {
        //Round to nearest 200
        RealAlt = Altitude;
        if (RealAlt < 0)
        {
            NumText.text = "";
        }
        else
        {
            NumText.text = RealAlt.ToString();
        }
    }

    public void SetExactAltitude(int Altitude)
    {
        NumText.text = Altitude.ToString();
    }

    public int GetShownAltitude()
    {
        return RealAlt;
    }
}
