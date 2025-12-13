using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class WaypointInfo : MonoBehaviour
{
    public Text nameText;
    public Text distanceText;
    public Text timeText;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    /*
             nav1_nav_id
             nav_1_distance
             nav_1_eta
             nav2_nav_id
             nav_2_distance
             nav_2_eta
             gps_nav_id
             gps_distance
             gps_eta
    */
    public void UpdateText(int navsourceselected, string nav1id, double nav1distance, double nav1eta, string nav2id, double nav2distance, double nav2eta, string gpsid, double gpsdistance, double gpseta)
    {
        switch (navsourceselected)
        {
            case 0: //Nav1       
                nav1eta = System.Math.Round(nav1eta, 2);
                nav1distance = System.Math.Round(nav1distance, 2);

                nameText.text = nav1id;
                timeText.text = nav1eta.ToString() + " min";
                distanceText.text = nav1distance.ToString() + " nm";

                break;
            case 1: //Nav2
                nav2eta = System.Math.Round(nav2eta, 2);
                nav2distance = System.Math.Round(nav2distance, 2);

                nameText.text = nav2id;
                timeText.text = nav2eta.ToString() + " min";
                distanceText.text = nav2distance.ToString() + " nm";

                break;
            case 2: //GPS
                gpseta = System.Math.Round(gpseta, 2);
                gpsdistance = System.Math.Round(gpsdistance, 2);

                nameText.text = nav2id;

                if (gpseta.ToString() == "Infinity")
                {
                    timeText.text = "0 min";
                }
                else
                {
                    timeText.text = gpseta.ToString() + " min";
                }

                distanceText.text = gpsdistance.ToString() + " nm";

                break;
        }
    }

}
