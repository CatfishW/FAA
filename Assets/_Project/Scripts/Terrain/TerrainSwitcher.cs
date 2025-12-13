using System.Collections;
using System.Collections.Generic;
using UnityEngine;
//[RequireComponent(HelicopterMarker)]
public class TerrainSwitcher : MonoBehaviour
{
    public GameObject SEA;
    public GameObject PHL;
    public GameObject NYC;
    public GameObject AC;
    public bool hasRun;
    [SerializeField]
    private int TerrainIndex;
    public HeliportMarker heliportMarker;
    public ObstacleController obstacleController;
    public bool isEnabled;
    // Start is called before the first frame update
    void Start()
    {
        //TerrainIndex = 4;
        isEnabled = true;
        hasRun = false;
    }

    // Update is called once per frame
    void Update()
    {/*
        if(Input.GetKeyDown(KeyCode.Q))
        {
            SEA.SetActive(true);
        }
        if (Input.GetKeyDown(KeyCode.W))
        {
            PHL.SetActive(true);
        }
        if (Input.GetKeyDown(KeyCode.E))
        {
            NYC.SetActive(true);
        }
        if (Input.GetKeyDown(KeyCode.R))
        {
            AC.SetActive(true);
        }*/
    }

    public void ChooseTerrain(float latitude, float longitude)
    {
        if(hasRun == false)
        {
            Debug.Log("Terrain lat is: " + (int)latitude);
            Debug.Log("Terrain long is: " + (int)longitude);

            if ((int)longitude == -122 && (int)latitude == 47)
            {
                SEA.SetActive(true);
                TerrainIndex = 1;
                Debug.Log("Setting terrain to Seattle.");
            }
            if ((int)longitude == -75 && (int)latitude == 39)
            {
                PHL.SetActive(true);
                TerrainIndex = 2;
                Debug.Log("Setting terrain to Philadelphia.");
            }
            if ((int)longitude == -73 && (int)latitude == 40)
            {
                NYC.SetActive(true);
                TerrainIndex = 3;
                Debug.Log("Setting terrain to Ney York City.");
            }
            if ((int)longitude == -74 && (int)latitude == 39)
            {
                AC.SetActive(true);
                TerrainIndex = 4;
                Debug.Log("Setting terrain to Atlantic City.");
                //map.Initialize(new Mapbox.Utils.Vector2d(39.459172495501114, -74.57884039498946), 15);

                //-------------------------
                Vector3[] positionsACYHelipad = new Vector3[5] { new Vector3(39.46485f, 0, -74.56568f), new Vector3(39.46504f, 0, -74.56595f), new Vector3(39.46484f, 0, -74.56618f), new Vector3(39.46465f, 0, -74.56592f), new Vector3(39.46485f, 0, -74.56568f) };
                Vector3 center = new Vector3(39.46448f, 0f, -74.5659f);
                //heliportMarker.PlaceMarker(positionsACYHelipad,center, "ACY"); //Create helipad marker for ACY

                Vector3[] positionsACYRunway22 = new Vector3[5] { new Vector3(39.449844f, 0, -74.58539f), new Vector3(39.449635f, 0, -74.584885f), new Vector3(39.464527f, 0, -74.5747f), new Vector3(39.464718f, 0, -74.57517f), new Vector3(39.449844f, 0, -74.58539f) };
                Vector3[] ACYRunway31 = new Vector3[5] { new Vector3(39.450863f, 0, -74.559326f), new Vector3(39.451393f, 0, -74.55899f), new Vector3(39.464237f, 0, -74.59177f), new Vector3(39.464752f, 0, -74.59146f), new Vector3(39.450863f, 0, -74.559326f) };
               
                //heliportMarker.PlaceMarker(positionsACYRunway22, new Vector3(0,0,0), "ACY");
                //heliportMarker.PlaceMarker(ACYRunway31, new Vector3(0, 0, 0), "ACY");
                //--------------------------
            }
            else
            {
                Debug.Log("Current coordinates have no associated terrain.");
            }
            obstacleController.GenerateObstacles((int)latitude, (int)longitude); //Obstacles are raycasted to surface, has to come after terrain
        }
        hasRun = true;
        

    }

    public void ToggleSVS()
    {
        if (TerrainIndex == 1)
        {
            if (SEA.activeSelf)
            {
                SEA.SetActive(false);
            }
            else
            {
                SEA.SetActive(true);
            }
        }
        else if (TerrainIndex == 2)
        {
            if (PHL.activeSelf)
            {
                PHL.SetActive(false);
            }
            else
            {
                PHL.SetActive(true);
            }
        }
        else if (TerrainIndex == 3)
        {
            if (NYC.activeSelf)
            {
                NYC.SetActive(false);
            }
            else
            {
                NYC.SetActive(true);
            }
        }
        else if (TerrainIndex == 4)
        {
            print("TERRAININDEX 4");
            if (AC.activeSelf)
            {
                print("disabling");
                AC.SetActive(false);
            }
            else
            {
                print("re-enabling");
                AC.SetActive(true);
            }
        }
    }
}
