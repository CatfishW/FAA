using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HUDDamp : MonoBehaviour
{
    [SerializeField]
    public Transform LeaderObject;
    [SerializeField]
    public Transform FollowerObject;
    Quaternion rot;

    //public int dampFactor;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        try{
            //this.transform.position = Vector3.Lerp(FollowObject.position, DriverTransform.position, 0.01f);
            rot = Quaternion.Lerp(FollowerObject.rotation, LeaderObject.rotation, 0.1f);
            FollowerObject.rotation = rot;
            //transform.rotation = rot;
        }
        catch{
            
        }
    }
}
