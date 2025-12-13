using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Display_Mode : MonoBehaviour
{
    public string mode_disp;
    public int declutter_mode;

    [SerializeField]
    List<GameObject> declutter0;
    [SerializeField]
    List<GameObject> declutter1;
    [SerializeField]
    List<GameObject> declutter2;
    [SerializeField]
    List<GameObject> declutter3;
    [SerializeField]
    List<GameObject> declutter4;

    GameObject AP_Block;
    GameObject NAV_Block;
    GameObject WindPanel;
    GameObject HeadingPanel;
    GameObject TorquePanel;
    GameObject Airspeed;
    GameObject Alt;
    GameObject VSpeedPanel;
    GameObject WaypointInfo_Block; 
    GameObject RPMPanel;
    GameObject Glideslope;
    GameObject LocalizerLine;
    GameObject SimpleNumDisplays;
    GameObject AttitudeHUD;
    GameObject AttitudeHUDNew;
    GameObject MasterTick;
    GameObject AirspeedPanel;
    GameObject AltPanel;
    GameObject SkidSlipInd;

    private Vector3 AP_Block_Scale_Default;
    private Vector3 NAV_Block_Scale_Default;
    private Vector3 Wind_Panel_Scale_Default;
    private Vector3 Heading_Panel_Scale_Default;

    private void OnEnable()
    {
    }
    private void OnDisable()
    {
    }
    // Start is called before the first frame update
    void Start()
    {
        declutter_mode = 0;
        AP_Block = GameObject.Find("AP_Block");
        NAV_Block = GameObject.Find("NAV_Block");
        WindPanel = GameObject.Find("WindPanel");
        HeadingPanel = GameObject.Find("HeadingPanel");
        TorquePanel = GameObject.Find("TorquePanel");
        Airspeed = GameObject.Find("Airspeed");
        Alt = GameObject.Find("Alt");
        VSpeedPanel = GameObject.Find("VSpeedPanel");
        WaypointInfo_Block = GameObject.Find("Waypoint Info_Block");  //ask ardit
        RPMPanel = GameObject.Find("RPMPanel");
        Glideslope = GameObject.Find("Glideslope");
        LocalizerLine = GameObject.Find("LocalizerLine");
        SimpleNumDisplays = GameObject.Find("SimpleNumDisplays");
        AttitudeHUD = GameObject.Find("AttitudePanel");
        AttitudeHUDNew = GameObject.Find("AttitudePanelNew");
        MasterTick = GameObject.Find("MasterTick");
        AirspeedPanel = GameObject.Find("AirspeedPanel");
        AltPanel = GameObject.Find("AltPanel");
        SkidSlipInd = GameObject.Find("SkidSlipInd");

        AP_Block_Scale_Default = AP_Block.transform.localScale;
        NAV_Block_Scale_Default = NAV_Block.transform.localScale;
        Wind_Panel_Scale_Default = WindPanel.transform.localScale;
        Heading_Panel_Scale_Default = HeadingPanel.transform.localScale;

        declutter0.Add(AP_Block);
        declutter0.Add(NAV_Block);
        declutter0.Add(WindPanel);
        declutter0.Add(HeadingPanel);
        declutter0.Add(TorquePanel);
        declutter0.Add(Airspeed);
        declutter0.Add(Alt);
        declutter0.Add(VSpeedPanel);
        declutter0.Add(WaypointInfo_Block);
        declutter0.Add(RPMPanel);
        declutter0.Add(Glideslope);
        declutter0.Add(SimpleNumDisplays);
        declutter0.Add(AttitudeHUD);
        declutter0.Add(AttitudeHUDNew);
        declutter0.Add(MasterTick);
        declutter0.Add(AirspeedPanel);
        declutter0.Add(AltPanel);
        declutter0.Add(SkidSlipInd);

        declutter1.Add(NAV_Block);
        declutter1.Add(WindPanel);
        declutter1.Add(TorquePanel);
        declutter1.Add(VSpeedPanel);
        declutter1.Add(WaypointInfo_Block);
        declutter1.Add(RPMPanel);
        declutter1.Add(Glideslope);
        declutter1.Add(LocalizerLine);
        declutter1.Add(SimpleNumDisplays);

        declutter2.Add(AP_Block);
        declutter2.Add(HeadingPanel);
        declutter2.Add(Airspeed);
        declutter2.Add(Alt);

        declutter3.Add(VSpeedPanel);
        declutter3.Add(AttitudeHUD);
        declutter3.Add(AttitudeHUDNew);
        declutter3.Add(SkidSlipInd);

        declutter4.Add(MasterTick);
        declutter4.Add(AirspeedPanel);
        declutter4.Add(AltPanel);

    }

    public void Cycle()
    {

            declutter_mode++;
        if (declutter_mode > 4)
        {                            //cycle on 1 numpad
            declutter_mode = 0;
        }
        if (declutter_mode == 0)

        {
            foreach (GameObject obj in declutter0) //Declutter 0 is the default declutter mode so everything displays
            {
              obj.transform.localScale = new Vector3(1f, 1f, 1f);
            }
        }


        if (declutter_mode == 1)
        {
            foreach (GameObject obj in declutter1)
            {
                obj.transform.localScale = new Vector3(0f, 0f, 0f);
            }
        }

        if (declutter_mode == 2)
        {
            foreach (GameObject obj in declutter2)
            {
                obj.transform.localScale = new Vector3(0f, 0f, 0f);
            }
        }
        if (declutter_mode == 3)
        {
            foreach (GameObject obj in declutter3)
            {

                obj.transform.localScale = new Vector3(0f, 0f, 0f);
            }
        }

        if (declutter_mode == 4)
        {
            foreach (GameObject obj in declutter4)
            {

                obj.transform.localScale = new Vector3(0f, 0f, 0f);
            }
        }

    }


    public void ResetHud()     
    {
        declutter_mode = 0;
    }
    // Update is called once per frame
    void Update()
    {
        if(Input.GetKeyDown(KeyCode.M))
        {
            Cycle();
        }
    }
}
