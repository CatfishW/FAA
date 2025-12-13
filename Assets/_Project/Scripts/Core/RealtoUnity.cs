using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RealtoUnity : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public static Vector3 Convert(Vector3 realPos)
    {
        Vector3 UnityVector;

        float unityLatitude = ((realPos.z + 180) % 1) * 1000;
        float unityLongitude = ((realPos.x + 180) % 1) * 1000;
        float altitude = realPos.y * 0.00443f + 3.5f;

        UnityVector = new Vector3(unityLongitude, altitude, unityLatitude);

        return UnityVector;
    }

}
