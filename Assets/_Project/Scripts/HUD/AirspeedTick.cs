using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class AirspeedTick : MonoBehaviour
{
    public int airspeed;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void setAirspeed(int newAirspeed)
    {
        airspeed = newAirspeed;
        if(airspeed >= 0)
        {
            transform.GetComponent<Text>().text = airspeed.ToString();
        }
        else
        {
            transform.GetComponent<Text>().text = "";
        }
    }

    public int getAirspeed()
    {
        return airspeed;
    }
}
