using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RegisterTarget : MonoBehaviour
{
    // Start is called before the first frame update
    public RadarManager radarManager;
    void Start()
    {
        try{
        radarManager.RegisterTarget(transform, Color.red);
        }
        catch{
            
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
