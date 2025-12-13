using System;
using System.Threading;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Options;
using FlexBuffers;
using UnityEngine;
using System.Collections;
using System.IO;
using UnityEngine.UI;
using CSF;
using FlatBuffers;
using System.Collections.Generic;
public class WaypointController : MonoBehaviour
{

    String IP;
    MqttFactory factory;
    IMqttClient mqttClient;
    IMqttClientOptions options;
    AutoResetEvent semaphore;
    TimeSpan receiveTimeout;
    //================
    public GameObject WaypointStart;
    public GameObject WaypointMarker;
    bool newData;
    [SerializeField]
    private List<Waypoint> waypointList;
    Data data;
    bool hasRun;
    bool waypointChanged;
    List<String> previousItems;
    public Text waypointText;
    bool statusRunning;


    private void OnEnable()
    {
    }
    private void OnDisable()
    {
    }
    void Start()
    {
        previousItems = new List<String>();
        waypointList = new List<Waypoint>();
        hasRun = false;
        newData = false;
        statusRunning = false;
        //----------
        String ValidIP = "";
        bool found = false;
        String PersistentPath = Application.persistentDataPath;
        DirectoryInfo dir = new DirectoryInfo(Application.persistentDataPath);
        FileInfo[] info = dir.GetFiles("*.*");

        foreach (FileInfo f in info)
        {
            String[] FilePath = f.ToString().Split('\\');
            String FileName = FilePath[FilePath.Length - 1]; //Get just end of file path.
            //Debug.Log(FileName);

            if (FileName.Split('.').Length == 4)//Valid IPs have 4 periods in it.
            {
                found = true;
                ValidIP = FileName;
            }
        }
        if (found)
        {
            Debug.Log("IP Detected! IP is " + ValidIP);
        }
        else
        {
            Debug.Log("No valid IP detected in persistent data path. Setting to 192.168.72.249");//Flight Simulator IP at the FAA
            ValidIP = "192.168.72.249"; //Flight Simulator IP at the FAA
        }
        Debug.Log(PersistentPath);
        factory = new MqttFactory();
        mqttClient = factory.CreateMqttClient();
        options = new MqttClientOptionsBuilder()
                .WithClientId("")
                .WithTcpServer(ValidIP, 1883)
                .WithCleanSession()
                .Build();

        mqttClient.ConnectAsync(options, CancellationToken.None);

        semaphore = new AutoResetEvent(false);
        receiveTimeout = TimeSpan.FromSeconds(0.02f);

        mqttClient.UseConnectedHandler(async e =>
        {
            Debug.Log("### WAYPOINT CONNECTED WITH SERVER ###");

            // Subscribe to a topic
            await mqttClient.SubscribeAsync(new TopicFilterBuilder().WithTopic("XP-S76-Route").Build());

            Debug.Log("### WAYPOINT SUBSCRIBED ###");
        });
        
        //---------------------------------------------------------------------------------

        mqttClient.UseApplicationMessageReceivedHandler(e => //Recieves data packet
        {
            semaphore.Set(); //Unsure if necessary
            waypointChanged = false;
            var buf = new ByteBuffer(e.ApplicationMessage.Payload);

            data = CSF.Data.GetRootAsData(buf);
            newData = true;
        }); //end of mqttClient.UseApplicationMessageReceivedHandler

        StartCoroutine(UpdatePoints());

    }

    // Update is called once per frame
    void Update()
    {
        semaphore.WaitOne((int)receiveTimeout.TotalMilliseconds, true);

        if (newData) //Ensures we're not trying to parse incomplete data. 
        {
            for (int i = 0; i < previousItems.Count; i++)
            {
                var current_waypoint = data.Flightplan(i).Value; //Current waypoint
                String currentName = current_waypoint.Name; //Current name

                if (previousItems[i] != currentName) //If previously stored item is equal to current flightplan name
                {
                    waypointChanged = true; //Gonna update the waypoint list if it has changed
                    previousItems.Clear(); //Reset the prev items list
                }
                //else
                // waypointChanged = false;
            }

            if (waypointChanged == true || hasRun == false)  //Update waypointlist if waypoint has changed
            {
                //Debug.Log("Updating Saved Waypoints");

                waypointList.Clear(); //Dispose of old waypoints or we'll just keep adding them to the end. Keeps list updated
                
                    for (int j = 0; j < data.FlightplanLength; j++)
                    {
                        var current_waypont = data.Flightplan(j).Value;
                        previousItems.Add(current_waypont.Name); //Add the current values to previousItems to check if waypoint has changed
                        Waypoint tempWaypoint = new Waypoint(current_waypont.Name, current_waypont.Latitude, current_waypont.Altitude, current_waypont.Longitude, false);
                        waypointList.Add(tempWaypoint);
                    }

                //newData = true;
                hasRun = true;
            }

            for (int i = 0; i < data.FlightplanLength; i++) //Check if altitude != 0
            {
                var current_flightplanWaypoint = data.Flightplan(i).Value;

                if (current_flightplanWaypoint.Altitude != 0)
                {
                    //Debug.Log("NAME: " + current_flightplanWaypoint.Name + " ALT: " + current_flightplanWaypoint.Altitude);
                    for (int j = 0; j < waypointList.Count; j++)//Loop through list of waypoints
                    {
                        var current_wayPointList = waypointList[j];
                        if (current_flightplanWaypoint.Name == current_wayPointList.Name) //If this is the correct one, we're going to set the altitude in the list
                        {
                            waypointList[j].Altitude = current_flightplanWaypoint.Altitude;
                            waypointList[j].UpdateFlag = true; //Trigger flag so the point gets updated
                        }
                    }
                    if (!statusRunning)
                    {
                        StartCoroutine(StatusUpdate());
                    }    
                }


            }
            newData = false;
        }


    }


    IEnumerator UpdatePoints() //Check if flag is true, update waypoint if true
    {
        while (true)
        {
            for (int j = 0; j < waypointList.Count; j++)//Loop through list of waypoints
            {
                if (waypointList[j].UpdateFlag == true)
                {
                    DestroyMarker(waypointList[j].Name);
                    CreateMarker(waypointList[j], true);

                }

            }
        //Vector3 vecA = WaypointStart.transform.position; //Set in editor
        //Vector3 vecB = RealtoUnity.Convert(waypointList[waypointList.Count - 1].toVector3()); //waypoint => Vector3(real) => Vector3(unity)
        //X,Y,Z

        ////Don't interpolate and make new points if distance < x value
        ////Calculate distance between the two vectors
        //List<Waypoint> interpolatedList = InterpolatePoints(vecA, vecB, 100);
        //CreateMarkers(interpolatedList, false);
             yield return new WaitForSeconds(2);

        }
    }

    IEnumerator StatusUpdate() //Used to inform user that waypoint page is captured from FMS
    {
        waypointText.text = "Page Captured";
        yield return new WaitForSeconds(1.5f);
        waypointText.text = "";
        statusRunning = false;
    }

    public void QuitGame()
    {
        Application.Quit();
    }

    public List<Waypoint> InterpolatePoints(Vector3 A, Vector3 B, int numPoints)
    {
        List<Waypoint> returnList = new List<Waypoint>();

        for (float j = 0; j < 1; j += 0.1f)
        {
            Vector3 tempVec = Vector3.Lerp(A, B, j);
            Waypoint tempWay = new Waypoint("ToNextPoint", tempVec.x, tempVec.y, tempVec.z, false);
            returnList.Add(tempWay);
        }

        return returnList;
    }

    public void CreateAllMarkers(List<Waypoint> waypoints, bool convertUnits)
    {
        //convertunits to true if you want the marker function to convert from real to unity units
        for (int i = 0; i < waypoints.Count; i++)
        {
            Waypoint tempWaypoint = waypoints[i];
            if (convertUnits)
            {
                Vector3 tempVec = new Vector3(tempWaypoint.Latitude, tempWaypoint.Altitude, tempWaypoint.Longitude);
                Vector3 unityVec = RealtoUnity.Convert(tempVec); //Converts to unity coordinates
                Debug.Log("Creating Markers with converted units");
                Instantiate(WaypointMarker, unityVec, Quaternion.identity).name = tempWaypoint.Name;
            }
            else
            {
                Vector3 unityVec = new Vector3(tempWaypoint.Latitude, tempWaypoint.Altitude, tempWaypoint.Longitude);
                Debug.Log("Creating Markers non converted");
                Instantiate(WaypointMarker, unityVec, Quaternion.identity).name = tempWaypoint.Name;
            }
        }
    }

    public void CreateMarker(Waypoint waypoint, bool convertUnits)
    {
        //convertunits to true if you want the marker function to convert from real to unity units
        //Waypoint tempWaypoint = waypoints[i];
        if (convertUnits)
        {
            Vector3 tempVec = new Vector3(waypoint.Longitude, waypoint.Altitude, waypoint.Latitude);
            Vector3 unityVec = RealtoUnity.Convert(tempVec); //Converts to unity coordinates
            Debug.Log("Creating Markers with converted units");
            Instantiate(WaypointMarker, unityVec, Quaternion.identity).name = waypoint.Name;
        }
        else
        {
            Vector3 unityVec = new Vector3(waypoint.Longitude, waypoint.Altitude, waypoint.Latitude);
            Debug.Log("Creating Markers non converted");
            Instantiate(WaypointMarker, unityVec, Quaternion.identity).name = waypoint.Name;
        }

        waypoint.UpdateFlag = false; //Set back to false since it has been updated
    }
    public void DestroyMarker(string Name)
    {
        GameObject[] markers;
        markers = GameObject.FindGameObjectsWithTag("WaypointMarker");
        //Debug.Log("Destroy Marker Command called");
        for (int i = 0; i < markers.Length; i++)
        {
            if (markers[i].name == Name)
            {
                Destroy(markers[i]);
            }
        }
    }
    public void DestroyAllMarkers()
    {
        GameObject[] markers;
        markers = GameObject.FindGameObjectsWithTag("WaypointMarker");
        Debug.Log("Destroy Marker Command called");
        for (int i = 0; i < markers.Length; i++)
        {
            Destroy(markers[i]);
            //markers[i].SetActive(false);
        }
    }

}
