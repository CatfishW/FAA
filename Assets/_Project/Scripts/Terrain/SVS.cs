using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SVS : MonoBehaviour
{
    public GameObject MainCamera;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {

    }

    public void UpdateTerrain(float latitude, float longitude, float altitude, float heading, float pitch, float roll)
    {
        //Debug.Log("latitude is " + latitude);
        //Debug.Log("longitude is " + longitude);

        
        float latM = ((latitude + 180) % 1) * 1000;//Negate negative Lat value, Remove whole number, multiply by length of terrain
        float longM = ((longitude + 180) % 1) * 1000;//Negate negative Long value, Remove whole number, multiply by width of terrain

        //Debug.Log("adjusted latitude is " + latM/1000);
        //Debug.Log("adjusted longitude is " + longM/1000);

        //Move Terrain to correct lat/long.
        //Ranges are from 0 to -1000 x and 0 to -1000 z to be on top of terrain.
        //About 68 feet to 1 unity unit in this case. Can be more precisely configured.
        this.transform.position = new Vector3(-longM, -altitude/68 + 1, -latM);
        Debug.Log("Pitch: " + pitch + "; Heading: " + heading + "; Roll: " + roll);
        MainCamera.transform.rotation = Quaternion.Euler(new Vector3(-pitch, heading, -roll));//Add adjusted rotation to camera rotation
    }
}
