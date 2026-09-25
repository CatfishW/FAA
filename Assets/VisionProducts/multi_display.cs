using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class multi_display : MonoBehaviour
{
    //Enables additional displays for the hmd.
    void Start()
    {
        Debug.Log("Displays connected: " + Display.displays.Length);
        //Check for additional displays and activate each.
        for(int i = 1; i < Display.displays.Length;i++)
        {
            Display.displays[i].Activate();   
        }

    }
}
