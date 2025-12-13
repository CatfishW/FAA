using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HeliportMarker : MonoBehaviour
{
    public GameObject Marker;
    public GameObject approachPath;
    //public Color lineColor;
    public Material lineMat;
    public float LatTest;
    public float LonTest;
    //public List<Vector3> heliportList = new List<Vector3>();

    public bool displayCorners = false;  //Set to true if you want to display corners on the outline. Mainly for debugging purposes

    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public Vector3[] PlaceMarker(Vector3[]markerLocations, Vector3 center, string locationName)
    {
        List<Vector3> lineLocations = new List<Vector3>(5);
        //Vector3[] lineLocations = new Vector3[4]();
        float firstLat = markerLocations[0].x;
        float firstLon = markerLocations[0].z;
        //float unityFirstLatitude = ((firstLat + 180) % 1) * 1000;
        //float unityFirstLongitude = ((firstLon + 180) % 1) * 1000;
        //Vector3 firstValue = ReturnY(unityFirstLongitude, unityFirstLatitude);
        
        List <float> heightList = new List<float>();
        int i = 1;
        foreach (Vector3 position in markerLocations) //Loop through and convert the heights, adding heights to a list to sort by highest
        {
           
            float tempLat = position.z;
            float tempLon = position.x;

            LatTest = position.z;
            LonTest = position.x;

            float unityLatitude = ((tempLat + 180) % 1) * 1000;
            float unityLongitude = ((tempLon + 180) % 1) * 1000;
            
            Vector3 helipadLocation = ReturnY(unityLatitude, unityLongitude);

            //helipadLocation.y = firstValue.y; //Keep the y values the same
            if (displayCorners) //Debug purposes
            {
                var temp = Instantiate(Marker, helipadLocation, Quaternion.identity);
                temp.name = locationName + " HelipadCorner " + i;
            }
            lineLocations.Add(helipadLocation);
            heightList.Add(helipadLocation.y);
            i++;
        }

        float maxHeight = Mathf.Max(heightList.ToArray()); //Get the max height for the list
 

        for (int j = 0; j < lineLocations.Count; j++ ) //Set all coordiantes to have the same height
        {
            //lineLocations[j].y = maxHeight;
            Vector3 tempPos = lineLocations[j];
            tempPos.y = maxHeight;
            lineLocations[j] = tempPos;

        }
        



        ////Approach path instantiation
        //float centerLat = ((center.x + 180) % 1) * 1000;
        //float centerLon = ((center.z + 180) % 1) * 1000;
        //Vector3 centerUnityLocation = ReturnY(centerLat, centerLon);
        //centerUnityLocation.y = firstValue.y; 

        ////Instantiate(approachPath, centerUnityLocation, Quaternion.identity).name = locationName + " approach path";

        Vector3[] tempLine = lineLocations.ToArray();
        DrawLine(tempLine, locationName);

        return tempLine;

    }

    public Vector3 ReturnY(float z, float x)
    {
        float raycastStartHeight = 1000;
            RaycastHit hit;

            Ray landingray = new Ray(new Vector3(z, raycastStartHeight, x), Vector3.down);

        if (Physics.Raycast(landingray, out hit))
        {
            return hit.point;
        }
        else
        {
            return new Vector3(0, 0, 0);
        }

    }


    public void DrawLine(Vector3[] listOfPoints, string locationName)
    {
        GameObject line = new GameObject();
        line.name = locationName + " outline";
        line.transform.position = listOfPoints[1];
        LineRenderer lr = line.AddComponent<LineRenderer>();

        lr.positionCount = 5;
        lr.SetPositions(listOfPoints);
       // lr.startColor = lineColor;
        //lr.endColor = lineColor;
        lr.enabled = true;
        lr.material = lineMat;
        lr.widthMultiplier = 0.1f; //prev value 0.03
        //lr.SetPositions = (1, point2);

    }

}
