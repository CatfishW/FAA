using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class AP_Mode : MonoBehaviour
{

    public Text Collective_Active;
    public Text Collective_Armed;

    public Text Roll_Active;
    public Text Roll_Armed;

    public Text Pitch_Active;
    public Text Pitch_Armed;

    public Text Cpl;


    void Start()
    {

    }


    // Update is called once per frame
    void Update()
    {

    }


    // Update is called once per frame
    public void UpdateAP_Mode(string Collective_Active_String, string Collective_Armed_String, string Roll_Active_String, string Roll_Armed_String, string Pitch_Active_String, string Pitch_Armed_String, float cps_float)
    {
        Collective_Active.text = Collective_Active_String;
        Collective_Armed.text = Collective_Armed_String;

        Roll_Active.text = Roll_Active_String;
        Roll_Armed.text = Roll_Armed_String;

        Pitch_Active.text = Pitch_Active_String;
        Pitch_Armed.text = Pitch_Armed_String;

        if(cps_float == 1)
        {
            Cpl.text = "CPL";
        }
        else
        {
            Cpl.text = "";
        }
    }
}
