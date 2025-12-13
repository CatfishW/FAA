using System.Collections;
using System.Collections.Generic;
using UnityEngine;



public static class WarningColor : object
{

 

    public static Color Warning2(float lower, float upper, Color cLower, Color cUpper, float baseVal)       //green -> red
    {
        Color result;
        float range = upper - lower;                        
        float i = (baseVal - lower) / range;               
        result = Color.Lerp(cLower,cUpper, i);

        return result;

    }


    public static Color Warning3(float lower, float upper, Color cLower, Color cUpper, float baseVal, Color cMiddle, float middle)  //middle = target full yellow value
    {
       
        Color result = Color.green;
        float range = upper - lower;
        float i = (baseVal - lower) / range;
        if(i < middle)
        {
            result = Color.Lerp(cLower, cMiddle, i*2f);
        }
        if(i >= middle)
        {
            result = Color.Lerp(cMiddle, cUpper, (i - middle) * 2f);
        }

        return result;
    }

}
