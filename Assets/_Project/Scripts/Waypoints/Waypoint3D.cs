using UnityEngine;
using TMPro;

public class Waypoint3D : MonoBehaviour
{
    public Transform aircraft; // Reference to the aircraft
    public TMP_Text distanceText; // Reference to TextMeshPro to display distance
    public TMP_Text altitudeText; // Reference to TextMeshPro to display altitude
    public TMP_Text callSignText; // Reference to TextMeshPro to display call sign
    public string callSign = "GXY123"; // Sample call sign
    private GameObject IndicatorOffScreen;

    void Update()
    {
        try{
            // Calculate distance from the aircraft to the waypoint
            float distance = Vector3.Distance(aircraft.position, transform.position);
            
            // Calculate relative altitude
            float altitude = transform.position.y - aircraft.position.y;

            // Update text values
            
                distanceText.text = $"Distance: {distance:F1} km";
                altitudeText.text = $"Altitude: {altitude:F1} ft";
                callSignText.text = $"Call Sign: {callSign}";
                //find game object that contains IndicatorOffScreen:Waypoint 
                if(IndicatorOffScreen == null)
                {
                    IndicatorOffScreen = GameObject.Find("IndicatorOffScreen:Waypoint");
                }
                else{
                    Debug.Log("IndicatorOffScreen:Waypoint found");
                    //move distance text to IndicatorOffScreen:Waypoint as child
                    distanceText.transform.SetParent(IndicatorOffScreen.transform);
                    //add y offset to distance text
                    distanceText.transform.localPosition = new Vector3(0, 5, 0);
                }
        }
        catch{

        }
    }
}
