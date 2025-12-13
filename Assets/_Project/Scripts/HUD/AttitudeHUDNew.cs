using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AttitudeHUDNew : MonoBehaviour
{
    public GameObject HorizonImage;

    //public GameObject toFollow;
    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
    }

    public void UpdatePitch(float pitch)//In degrees
    {
        HorizonImage.transform.localPosition = new Vector3(HorizonImage.transform.localPosition.x, -pitch * 0.02f, 0f); //0.1 units Y per 5 degrees
        //HorizonImage.transform.LookAt(toFollow.transform, Vector3.down);
        //HorizonImage.transform.LookAt(toFollow.transform, Vector3.up);
    }

    //public void UpdateRoll(float newRoll)
    //{
    //    AttitudePivot.transform.localRotation = Quaternion.Euler(this.transform.localEulerAngles.x, this.transform.localEulerAngles.y, newRoll);
    //}
}
