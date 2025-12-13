using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PositionObject : MonoBehaviour
{
    public float objectLatitude;
    public float objectLongitude;
    public bool raycastSurface;
    public float raycastStartHeight;
    public float objectAltitude;

    // Start is called before the first frame update
    void Start()
    {
        /*
         * Input a latitude and longitude in real world space. Transforms to Unity units and moves the object the script is attached to 
         * to that lat/lon. If raycast is on, it will raycast from the start height down to the terrain. If it is not on then the user
         * can predefine the altitude in feet that is converted to unity space and the object is placed there.
         */ 
        float latitude = objectLatitude;
        float longitude = objectLongitude;

        float latM = ((latitude + 180) % 1) * 1000;//Negate negative Lat value, Remove whole number, multiply by length of terrain
        float longM = ((longitude + 180) % 1) * 1000;//Negate negative Long value, Remove whole number, multiply by width of terrain
                                                
        if (raycastSurface)
        {
            RaycastHit hit;
            Ray landingray = new Ray(new Vector3(latM, raycastStartHeight, longM), Vector3.down);

            if (Physics.Raycast(landingray, out hit))
            {
                transform.position = hit.point;
            }
        } else if (!raycastSurface)
        {
            transform.position = new Vector3(latM, objectAltitude * 0.00443f + 3.5f, longM);
        }
       

    }

    // Update is called once per frame
    void Update()
     {
      
        
     }

}





