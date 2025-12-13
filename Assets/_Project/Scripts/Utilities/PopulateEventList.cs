using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[ExecuteInEditMode]
public class PopulateEventList : MonoBehaviour
{
    public List<UnityEvent> events;
    // Start is called before the first frame update
    void Awake()
    {
        //GameObject DisplayMode = GameObject.Find("DisplayMode");
        //GameObject UIEditor = GameObject.Find("UIEditor");
        //GameObject TerrainCutout = GameObject.Find("TerrainCutout");
        //GameObject ipInputField = GameObject.Find("ipInputField");
        //GameObject Canvas = GameObject.Find("Canvas");
        //GameObject Controller = GameObject.Find("Controller");
        //GameObject ObstacleContainer = GameObject.Find("ObstacleContainer");
        //GameObject TerrainController = GameObject.Find("TerrainController");
        //GameObject BodyOffset = GameObject.Find("BodyOffset");


        //events[0].AddListener(DisplayMode.GetComponent<Display_Mode>().Cycle);
        
        //events[2].AddListener(UIEditor.GetComponent<HideHud>().FullHideHUD);
        //events[3].AddListener(TerrainCutout.GetComponent<FOV>().Hide);

        //events[5].AddListener(UIEditor.GetComponent<Brightness>().DecreaseBrightness);
        //events[6].AddListener(TerrainCutout.GetComponent<FOV>().DecreaseFOV);
        
        //events[9].AddListener(TerrainCutout.GetComponent<FOV>().IncreaseFOV);

        //events[15].AddListener(ipInputField.GetComponent<manualIP>().UpdateIPInfo);

        //events[23].AddListener(Canvas.GetComponent<LockHud>().Lock);

        //events[27].AddListener(Controller.GetComponent<WaypointController>().DestroyAllMarkers);
        //events[28].AddListener(Controller.GetComponent<ResetScene>().ResetSceneFunction);
        //events[29].AddListener(UIEditor.GetComponent<ColorPicker>().Toggle);
        //events[30].AddListener(ObstacleContainer.GetComponent<HideObstacles>().HideObstaclesFunction);

        //events[32].AddListener(TerrainController.GetComponent<TerrainController>().ToggleVision);
        //events[33].AddListener(BodyOffset.GetComponent<SaveBodyOffset>().ResetTransform);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
