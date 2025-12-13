using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Torque_Pointer : MonoBehaviour
{
    public ColorPicker UIEditor;
    Color userColor;
    public float warningValue; //Value that we begin to display a warning color
    public Text TorqueDisplay;
    public Text TorqueDisplay2;
    public GameObject TorquePointer;
    public GameObject TorquePointer2;
    public float TorqueTest;

    public GameObject Dial1;
    public GameObject Dial2;

    public GameObject dialParent;
    public GameObject barParent;

    public bool usingDial;
    //public GameObject TorquePointer1;
    // Start is called before the first frame update
    private float timer = 0f;
    private float updateInterval = 5f;
    private float randomTorque1 = 0f;
    private float randomTorque2 = 0f;

    void Start()
    {
        warningValue = 100f;
        usingDial = false;
        //SwitchModes();
        timer = updateInterval;
        randomTorque1 = Random.Range(0f, 130f);
        randomTorque2 = Random.Range(0f, 130f);
    }

    // Update is called once per frame
    void Update()
    {
        timer += Time.deltaTime;
        if (timer >= updateInterval)
        {
            randomTorque1 = Random.Range(0f, 130f);
            randomTorque2 = Random.Range(0f, 130f);
            timer = 0f;
        }
        UpdateEngineTorque(randomTorque1, randomTorque2);
    }

    public Color LerpColor(float inputVal)
    {
        /*warningValue is the value we want to start transitioning to the warning color
         * Input value won't output until warning value
         * 100->120 is 20 units, so I divide by 20 to get a value between 0 & 1
        */
        Color returnColor = UIEditor.hudColor;
        //input val never greater than warning val
        //
        //returnColor = Color.Lerp(UIEditor.DesColor, Color.red, ((inputVal - warningValue) / 20.0f));
        float testVal = (inputVal - warningValue) / 20.0f;

        
        if (testVal > 0.5 )
        {
            returnColor = Color.Lerp(Color.yellow, Color.red, ((inputVal - warningValue) / 10.0f) - 1.0f);
        }
        if (testVal <= 0.5)
        {
            returnColor = Color.Lerp(UIEditor.hudColor, Color.yellow, (inputVal - warningValue) / 10.0f);
        }

        return returnColor;
    }
    public void UpdateGaugeColor(float torqueVal1, float torqueVal2)
    {
        //Going to change the green gauge to red to alert user of overtorque situation
        //if (torqueVal < warningValue) //Don't want to update the stored color if we're lerping it
        //{
        //    userColor = UIColor.DesColor;
        //}
        if (torqueVal1 >= warningValue || torqueVal2 >= warningValue)
        {
            float higherTorqueVal; //Color Priority is given to the highest torque.
            if(torqueVal1 > torqueVal2)
            {
                higherTorqueVal = torqueVal1;
            }
            else
            {
                higherTorqueVal = torqueVal2;
            }
            Color tempColor = LerpColor(higherTorqueVal);
            foreach (Transform child in transform)
            {
                if (child.gameObject.GetComponent<Text>() != null)
                {
                    child.gameObject.GetComponent<Text>().color = tempColor;
                }

                if (child.gameObject.GetComponent<Image>() != null)
                {
                    child.gameObject.GetComponent<Image>().color = tempColor;
                }
            }

        }
    }
    public void UpdateEngineTorque(float Torque1, float Torque2)
    {

        //Debug.Log(Torque2);
        //Debug.Log(Torque1);
        UpdateGaugeColor(Torque1, Torque2);
        if (Torque1 > 120f)
        {
            Torque1 = 120f;
        }
        if (Torque2 > 120f)
        {
            Torque2 = 120f;
        }
        string EngineNumberToString1 = "";
        float candidate = Mathf.Round((Torque1 * 10f) / 10f); //Round 1 decimal point.
        if (candidate < 100 && candidate.ToString().Length == 2) //Make sure .0 is appended on multiples of 10!
        {
            EngineNumberToString1 = candidate.ToString() + ".0";
        }
        else if (candidate == 100)
        {
            EngineNumberToString1 = candidate.ToString() + ".0";
        }
        else
        {
            EngineNumberToString1 = candidate.ToString();
        }
        TorqueDisplay.text = EngineNumberToString1 + "%";


        string EngineNumberToString2 = "";
        float candidate2 = Mathf.Round((Torque2 * 10f) / 10f); //Round 1 decimal point.
        if (candidate < 100 && candidate.ToString().Length == 2) //Make sure .0 is appended on multiples of 10!
        {
            EngineNumberToString2 = candidate2.ToString() + ".0";
        }
        else if (candidate == 100)
        {
            EngineNumberToString2 = candidate2.ToString() + ".0";
        }
        else
        {
            EngineNumberToString2 = candidate2.ToString();
        }
        TorqueDisplay2.text = EngineNumberToString2 + "%";

        //100% is roughly at .22f, 120% is at .24f
        if (!usingDial)
        {
            TorquePointer.transform.localPosition = new Vector3(TorquePointer.transform.localPosition.x, Torque1 / 120f * .24f, TorquePointer.transform.localPosition.z);
            TorquePointer2.transform.localPosition = new Vector3(TorquePointer2.transform.localPosition.x, Torque2 / 120f * .24f, TorquePointer2.transform.localPosition.z);
        }
        else
        {
            Dial1.transform.localEulerAngles = new Vector3(0f, 0f, -Torque1 * (200f / 100f));//200 degrees of rotation per 100 percent
            Dial2.transform.localEulerAngles = new Vector3(0f, 0f, -Torque2 * (200f / 100f));//200 degrees of rotation per 100 percent
        }
    }


public void SwitchModes()
    {
        usingDial = !usingDial;
        if (usingDial)
        {
            dialParent.transform.localScale = new Vector3(1f, 1f, 1f);
            barParent.transform.localScale = new Vector3(0f, 0f, 0f);
        }
        else
        {
            dialParent.transform.localScale = new Vector3(0f, 0f, 0f);
            barParent.transform.localScale = new Vector3(1f, 1f, 1f);
        }
    }
}
