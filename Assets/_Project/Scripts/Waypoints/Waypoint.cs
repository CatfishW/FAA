using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Waypoint
{
    public string Name;
    public float Latitude;
    public float Longitude;
    public float Altitude;
    public bool UpdateFlag;
    // Start is called before the first frame update

    public Waypoint(string Name, float Latitude, float Altitude, float Longitude, bool UpdateFlag) //Constructor
    {
        this.Name = Name;
        this.Latitude = Latitude;
        this.Longitude = Longitude;
        this.Latitude = Latitude;
        this.Altitude = Altitude;
        this.UpdateFlag = UpdateFlag;
    }
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public Vector3 toVector3()
    {
        Vector3 tempVec = new Vector3(Latitude, Altitude, Longitude);
        return tempVec;
    }


}
