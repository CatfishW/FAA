using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Obstacle
{
    // Start is called before the first frame update
    public float Latitude;
    public float Longitude;
    public float Altitude;
    public string Type;
    // Update is called once per frame
    public Obstacle(float Longitude, float Altitude, float Latitude, string Type) //Constructor
    {
        this.Latitude = Latitude;
        this.Longitude = Longitude;
        this.Latitude = Latitude;
        this.Altitude = Altitude;
        this.Type = Type;
    }

}
