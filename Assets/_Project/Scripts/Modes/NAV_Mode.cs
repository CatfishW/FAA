using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class NAV_Mode : MonoBehaviour
{
    public Text text;


    // Start is called before the first frame update
    void Start()
    {
        
    }

    void Update()
    {

    }


    public void UpdateText(int navigationMode)
    {
        switch (navigationMode)
        {
            case 0:
                text.text = "NAV1 Selected";
                
                break;
            case 1:
                text.text = "NAV2 Selected";
                
                break;
            case 2:
                text.text = "GPS Selected";

                break;
        }


    }

   
   

}

