using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Diagnostics;
using UnityEditor;

public class ObstacleController : MonoBehaviour
{
    public ObstacleData data;
    public GameObject UIEditor;
    Color objColor;
    List<float> latitudes;
    List<float> longitudes;
    List<float> altitudes;
    List<string> types;
    bool posChanged;
    Vector3 previousPos;
    public GameObject obstacleContainer;
    public GameObject obstacleMarker;
    public GameObject poleMarker;
    public GameObject buildingMarker;
    public GameObject stackMarker;
    public GameObject windmillMarker;
    public GameObject trackObject;
    public Stopwatch timer; //Debug
    public float obsUpdateDistance; //Distance player can go before updating obstacles in view
    public float obstacleShowDistance; //Distance away from a player that an obstacle will show 
    void Start()
    {
        timer = new Stopwatch();
        objColor = UIEditor.GetComponent<ColorPicker>().DesColor;
        //timer.Start();
        latitudes = new List<float>();
        longitudes = new List<float>();
        altitudes = new List<float>();
        latitudes = data.Latitudes;
        longitudes = data.Longitudes;
        altitudes = data.Altitudes;
        types = data.Types;
        previousPos = new Vector3(0, 0, 0);
        List<GameObject> obstacleTracker = new List<GameObject>();
        //timer.Stop();
        //UnityEngine.Debug.Log("TIME ELAPSED FOR LOOP: " + timer.Elapsed );

    }


    void Update()
    {
        Vector3 currentPos = trackObject.transform.position;
     
        if (Vector3.Distance(previousPos, currentPos) > obsUpdateDistance) //Update check to display new obstacles
        {
           //UpdateObstacles(currentPos);
            previousPos = currentPos;
            UnityEngine.Debug.Log("UPDATING OBSTACLES");
            ShowHideObstacles();
        }

        //----------------
        //if (Input.GetKeyDown("space")) //Debug to generate obstacles
        //{
        //    UnityEngine.Debug.Log("Generating Obs");
        //    GenerateObstacles(39f, -74f);
        //}
        //----------------
    }

    public void GenerateObstacles(float latitude, float longitude)
    {
        UnityEngine.Debug.Log("Generating Obstacles");
        List<Obstacle> ObstacleList = ReturnObstaclesInChunk(latitude, longitude);

        foreach (Obstacle item in ObstacleList)
        {
            Vector3 realVector = new Vector3(item.Longitude, item.Altitude, item.Latitude);
            Vector3 unityVec = RealtoUnity.Convert(realVector);
            float obstacleTerrainHeight = returnY(unityVec.x, unityVec.z); //Get y @ top of terrain to place object at
            unityVec = new Vector3(unityVec.x, obstacleTerrainHeight, unityVec.z);
            float objHeight = RealtoUnity.Convert(new Vector3(0, item.Altitude, 0)).y; 

            switch (item.Type)
            {
                case "UTILITY POLE":
                        GameObject poleObj = Instantiate(poleMarker, unityVec, Quaternion.identity); //Instantiate prefab. Will be switch statement by type
                        poleObj.tag = "ObstacleMarker";
                        poleObj.name = item.Type;
                        poleObj.transform.parent = obstacleContainer.transform;
                        poleObj.GetComponent<MeshRenderer>().material.SetColor("_Color", objColor);
                        //poleObj.transform.localScale = new Vector3(objHeight / 2, objHeight, objHeight / 2); //Need to work on this
                        break;

                case "BLDG":
                    GameObject buildingObj = Instantiate(buildingMarker, unityVec, Quaternion.identity); //Instantiate prefab. Will be switch statement by type
                    buildingObj.tag = "ObstacleMarker";
                    buildingObj.name = item.Type;
                    buildingObj.transform.parent = obstacleContainer.transform;
                    buildingObj.GetComponent<MeshRenderer>().material.SetColor("_Color", objColor);
                    //poleObj.transform.localScale = new Vector3(objHeight / 2, objHeight, objHeight / 2); //Need to work on this
                    break;
                case "WINDMILL":
                    GameObject windmillObj = Instantiate(windmillMarker, unityVec, Quaternion.identity); //Instantiate prefab. Will be switch statement by type
                    windmillObj.tag = "ObstacleMarker";
                    windmillObj.name = item.Type;
                    windmillObj.transform.parent = obstacleContainer.transform;
                    windmillObj.GetComponent<MeshRenderer>().material.SetColor("_Color", objColor);
                    break;

                case "STACK":
                    GameObject stackObj = Instantiate(stackMarker, unityVec, Quaternion.identity); //Instantiate prefab. Will be switch statement by type
                    stackObj.tag = "ObstacleMarker";
                    stackObj.name = item.Type;
                    stackObj.transform.parent = obstacleContainer.transform;
                    stackObj.GetComponent<MeshRenderer>().material.SetColor("_Color", objColor);
                    break;

                default:
                        GameObject obj = Instantiate(obstacleMarker, unityVec, Quaternion.identity); //Instantiate prefab. Will be switch statement by type
                        obj.tag = "ObstacleMarker";
                        obj.name = item.Type;
                        obj.transform.parent = obstacleContainer.transform;
                        obj.GetComponent<MeshRenderer>().material.SetColor("_Color", objColor);
                    //obj.transform.localScale = new Vector3(objHeight / 2, objHeight, objHeight / 2); //Need to work on this
                    break;
            }

           

        }
    }

    public void DestroyAllObstacles()
    {
        //foreach (GameObject item in obstacleTracker)
        //{
        //    Destroy(item);
        //}
        //obstacleTracker.Clear();
    }

    float returnY(float x, float z) //Long is z, Lat is x
    {
        float raycastStartHeight = 1000;
        RaycastHit hit;

        Ray landingray = new Ray(new Vector3(x, raycastStartHeight, z), Vector3.down);

        if (Physics.Raycast(landingray, out hit))
        {
            return hit.point.y;
        }
        else
        {
            return 0f; 
        }

    }

    List<Obstacle> ReturnObstaclesInChunk(float latitude, float longitude)
    {
        /*
        Terrain chunk is 1 degree lat by one degree lon
        Therefore the bound to check is current lat + 1, current lon +1
        Caveat if either is negative, so we check ++/-+/+-/-- cases with lat/lons before checking if obstacles are within
        
        Input: latitude longtude float
        Output: List of obstacles within said chunk
        */
        List<Obstacle> returnList = new List<Obstacle>();

        int latL, latU, lonL, lonU; //Bounds for lat/lon | latL(ower) latU(pper)
        latL = (int)latitude;
        lonL = (int)longitude;

        //Check if either is negative. This will change the upper bound
        if (Mathf.Sign(latitude) == -1)
        {
            latU = latL -1 ;
        } else
        {
            latU = latL + 1;
        }

        if (Mathf.Sign(longitude) == -1)
        {
            lonU = lonL - 1;
        } else
        {
            lonU = lonL + 1;
        }
        //===============
        if (Mathf.Sign(latitude) == 1 && Mathf.Sign(longitude) == 1) //Positive lat positive long
        { 
            for (int i = 0; i < latitudes.Count; i++) //Lat/Lons should have some # of indexes
            {
                float tempLat = latitudes[i];
                float tempLon = longitudes[i];
                float tempAlt = altitudes[i];
                string tempType = types[i];

                if ( (tempLat >= latL && tempLat <= latU)&&( tempLon >= lonL && tempLon <= lonU) ) //If within terrain chunk 
                {
                    Obstacle tempObs = new Obstacle(tempLon, tempAlt, tempLat, tempType);
                    returnList.Add(tempObs);
                }

            }
        }
       
        if ((Mathf.Sign(latitude) == -1 && Mathf.Sign(longitude) == 1))//Positive long negative lat
        {
            for (int i = 0; i < latitudes.Count; i++) //Lat/Lons should have some # of indexes
            {
                float tempLat = latitudes[i];
                float tempLon = longitudes[i];
                float tempAlt = altitudes[i];
                string tempType = types[i];

                if ((tempLat <= latL && tempLat >= latU) && (tempLon >= lonL && tempLon <= lonU)) //If within terrain chunk 
                {
                    Obstacle tempObs = new Obstacle(tempLon, tempAlt, tempLat, tempType);
                    returnList.Add(tempObs);
                }

            }
        }

        if ((Mathf.Sign(latitude) == 1 && Mathf.Sign(longitude) == -1))//negative long positive lat
        {
            for (int i = 0; i < latitudes.Count; i++) //Lat/Lons should have some # of indexes
            {
                float tempLat = latitudes[i];
                float tempLon = longitudes[i];
                float tempAlt = altitudes[i];
                string tempType = types[i];

                if ((tempLat >= latL && tempLat <= latU) && (tempLon <= lonL && tempLon >= lonU)) //If within terrain chunk 
                {
                    Obstacle tempObs = new Obstacle(tempLon, tempAlt, tempLat, tempType);
                    returnList.Add(tempObs);
                }

            }
        }

        if ((Mathf.Sign(latitude) == -1 && Mathf.Sign(longitude) == -1))//negative long negative lat
        {
            for (int i = 0; i < latitudes.Count; i++) //Lat/Lons should have some # of indexes
            {
                float tempLat = latitudes[i];
                float tempLon = longitudes[i];
                float tempAlt = altitudes[i];
                string tempType = types[i];

                if ((tempLat <= latL && tempLat >= latU) && (tempLon <= lonL && tempLon >= lonU)) //If within terrain chunk 
                {
                    Obstacle tempObs = new Obstacle(tempLon, tempAlt, tempLat, tempType);
                    returnList.Add(tempObs);
                }

            }
        }
        return returnList;
    }


    public void ShowHideObstacles()
    {
        GameObject[] obstacles;
        obstacles = GameObject.FindGameObjectsWithTag("ObstacleMarker");
        foreach (GameObject item in obstacles) //Hide the objects that are out of the viewing window
        {
            float tempnum = Vector3.Distance(new Vector3(item.transform.position.x,0, item.transform.position.z), new Vector3(trackObject.transform.position.x, 0f , trackObject.transform.position.z) );

            if (tempnum < obstacleShowDistance && tempnum >10f)
            {
                item.GetComponent<MeshRenderer>().enabled = true;
            }
            else
            {
                item.GetComponent<MeshRenderer>().enabled = false;
            }

        }
    }

    //List<Vector3> ReturnMarkersInDistance(int radius, Vector3 currentPos)
    //{
    //    List<Vector3> returnList = new List<Vector3>();

    //    for (int i = 0; i < latitudes.Count; i++) //Lat/Lons should have some # of indexes
    //    {
    //        Vector3 tempVec = RealtoUnity.Convert(new Vector3(longitudes[i], 0, latitudes[i])); //Return unity units
    //        Vector3 tempPos = new Vector3(currentPos.x, 0, currentPos.z);                      //Altitudes @ 0 for both currentPos and obstacles, to prevent height from causing larger distance from taller obstacles

    //        if (Vector3.Distance(tempVec,tempPos) < radius)
    //        {
    //            returnList.Add(new Vector3(longitudes[i], altitudes[i], latitudes[i]));
    //        }

    //    }

    //    return returnList;
    //}

}
