using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Glideslope : MonoBehaviour
{
    public GameObject glideslopeBar;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {

    }

    public void UpdateGlideslope(float dots)
    {
        float dotnum = dots * 0.09f;
        glideslopeBar.transform.localPosition = new Vector3(glideslopeBar.transform.localPosition.x, dotnum, glideslopeBar.transform.localPosition.z);
    }


}
