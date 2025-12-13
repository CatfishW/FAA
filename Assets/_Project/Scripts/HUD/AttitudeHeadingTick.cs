using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class AttitudeHeadingTick : MonoBehaviour
{
    public Text MyText;
    public int Num;
    

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void SetText(float NumToConvert)
    {
        NumToConvert = (NumToConvert + 360) % 360;
        //Debug.Log("Modded Rounded number is " + NumToConvert);
        if ((int)NumToConvert == 0)//Cardinal Exceptions
        {
            MyText.text = "N";
            Num = 0;
            MyText.fontSize = 160;
           // MyText.GetComponent <RectTransform>().position = new Vector3(MyText.GetComponent<RectTransform>().position.x, 0.04f, MyText.GetComponent<RectTransform>().position.z);
        }
        else if ((int)NumToConvert == 90)
        {
            MyText.text = "E";
            Num = 90;
            MyText.fontSize = 160;
           // MyText.GetComponent<RectTransform>().position = new Vector3(MyText.GetComponent<RectTransform>().position.x, 0.04f, MyText.GetComponent<RectTransform>().position.z);
        }
        else if ((int)NumToConvert == 180)
        {
            MyText.text = "S";
            Num = 180;
            MyText.fontSize = 160;
            //MyText.GetComponent<RectTransform>().position = new Vector3(MyText.GetComponent<RectTransform>().position.x, 0.04f, MyText.GetComponent<RectTransform>().position.z);
        }
        else if ((int)NumToConvert == 270)
        {
            MyText.text = "W";
            Num = 270;
            MyText.fontSize = 160;
           // MyText.GetComponent<RectTransform>().position = new Vector3(MyText.GetComponent<RectTransform>().position.x, 0.04f, MyText.GetComponent<RectTransform>().position.z);
        }
        else
        {
            MyText.text = NumToConvert.ToString();
            Num = (int)NumToConvert;
            MyText.fontSize = 110;
            //MyText.GetComponent<RectTransform>().position = new Vector3(MyText.GetComponent<RectTransform>().position.x, 0.02f, MyText.GetComponent<RectTransform>().position.z);
        }
    }

    public float GetNum()
    {
        return Num;
    }
}
