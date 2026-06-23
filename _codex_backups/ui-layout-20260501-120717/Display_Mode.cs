using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Display_Mode : MonoBehaviour
{
    public string mode_disp;
    public int declutter_mode;

    [SerializeField]
    List<GameObject> declutter0 = new List<GameObject>();
    [SerializeField]
    List<GameObject> declutter1 = new List<GameObject>();
    [SerializeField]
    List<GameObject> declutter2 = new List<GameObject>();
    [SerializeField]
    List<GameObject> declutter3 = new List<GameObject>();
    [SerializeField]
    List<GameObject> declutter4 = new List<GameObject>();

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

    private readonly Dictionary<GameObject, Vector3> defaultScales = new Dictionary<GameObject, Vector3>();

    private void OnEnable()
    {
    }

    private void OnDisable()
    {
    }

    void Start()
    {
        declutter_mode = 0;
        EnsureDeclutterLists();
        ClearDeclutterLists();
        defaultScales.Clear();

        AP_Block = FindHudObject("AP_Block");
        NAV_Block = FindHudObject("NAV_Block");
        WindPanel = FindHudObject("WindPanel");
        HeadingPanel = FindHudObject("HeadingPanel");
        TorquePanel = FindHudObject("TorquePanel");
        Airspeed = FindHudObject("Airspeed");
        Alt = FindHudObject("Alt");
        VSpeedPanel = FindHudObject("VSpeedPanel");
        WaypointInfo_Block = FindHudObject("Waypoint Info_Block");
        RPMPanel = FindHudObject("RPMPanel");
        Glideslope = FindHudObject("Glideslope");
        LocalizerLine = FindHudObject("LocalizerLine");
        SimpleNumDisplays = FindHudObject("SimpleNumDisplays");
        AttitudeHUD = FindHudObject("AttitudePanel");
        AttitudeHUDNew = FindHudObject("AttitudePanelNew");
        MasterTick = FindHudObject("MasterTick");
        AirspeedPanel = FindHudObject("AirspeedPanel");
        AltPanel = FindHudObject("AltPanel");
        SkidSlipInd = FindHudObject("SkidSlipInd");

        AddIfPresent(declutter0, AP_Block, NAV_Block, WindPanel, HeadingPanel, TorquePanel, Airspeed, Alt, VSpeedPanel,
            WaypointInfo_Block, RPMPanel, Glideslope, SimpleNumDisplays, AttitudeHUD, AttitudeHUDNew, MasterTick,
            AirspeedPanel, AltPanel, SkidSlipInd);

        AddIfPresent(declutter1, NAV_Block, WindPanel, TorquePanel, VSpeedPanel, WaypointInfo_Block, RPMPanel,
            Glideslope, LocalizerLine, SimpleNumDisplays);

        AddIfPresent(declutter2, AP_Block, HeadingPanel, Airspeed, Alt);
        AddIfPresent(declutter3, VSpeedPanel, AttitudeHUD, AttitudeHUDNew, SkidSlipInd);
        AddIfPresent(declutter4, MasterTick, AirspeedPanel, AltPanel);
    }

    public void Cycle()
    {
        EnsureDeclutterLists();

        declutter_mode++;
        if (declutter_mode > 4)
        {
            declutter_mode = 0;
        }

        if (declutter_mode == 0)
        {
            ResetAllScales();
        }
        else if (declutter_mode == 1)
        {
            SetScale(declutter1, Vector3.zero);
        }
        else if (declutter_mode == 2)
        {
            SetScale(declutter2, Vector3.zero);
        }
        else if (declutter_mode == 3)
        {
            SetScale(declutter3, Vector3.zero);
        }
        else if (declutter_mode == 4)
        {
            SetScale(declutter4, Vector3.zero);
        }
    }

    public void ResetHud()
    {
        declutter_mode = 0;
        ResetAllScales();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.M))
        {
            Cycle();
        }
    }

    private void EnsureDeclutterLists()
    {
        if (declutter0 == null) declutter0 = new List<GameObject>();
        if (declutter1 == null) declutter1 = new List<GameObject>();
        if (declutter2 == null) declutter2 = new List<GameObject>();
        if (declutter3 == null) declutter3 = new List<GameObject>();
        if (declutter4 == null) declutter4 = new List<GameObject>();
    }

    private void ClearDeclutterLists()
    {
        declutter0.Clear();
        declutter1.Clear();
        declutter2.Clear();
        declutter3.Clear();
        declutter4.Clear();
    }

    private GameObject FindHudObject(string objectName)
    {
        GameObject hudObject = GameObject.Find(objectName);
        if (hudObject != null && !defaultScales.ContainsKey(hudObject))
        {
            defaultScales.Add(hudObject, hudObject.transform.localScale);
        }

        return hudObject;
    }

    private static void AddIfPresent(List<GameObject> targetList, params GameObject[] objects)
    {
        if (targetList == null || objects == null)
        {
            return;
        }

        foreach (GameObject obj in objects)
        {
            if (obj != null && !targetList.Contains(obj))
            {
                targetList.Add(obj);
            }
        }
    }

    private void ResetAllScales()
    {
        foreach (KeyValuePair<GameObject, Vector3> entry in defaultScales)
        {
            if (entry.Key != null)
            {
                entry.Key.transform.localScale = entry.Value;
            }
        }
    }

    private static void SetScale(List<GameObject> objects, Vector3 scale)
    {
        if (objects == null)
        {
            return;
        }

        foreach (GameObject obj in objects)
        {
            if (obj != null)
            {
                obj.transform.localScale = scale;
            }
        }
    }
}
