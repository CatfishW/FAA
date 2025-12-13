using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*
 * Grant Morfitt
 * 
 */
public class SphereProjection : MonoBehaviour
{
    //public GameObject myCube;
    public Camera myCam;


    public bool DEBUG_ON;

    public List<Vector3> HELIPADPOINT_LIST; //This is going to be permanant

    //public float Latitude2, Longitude2;

    public GameObject projectFromPoint;
    public Vector2 HELICOPTER_LATLON; //Temporary location of helicopter
    //public float helicopterAltitude; //Temporary helicopter altitude
    //public float helicopterHeading;
    //List<Line> sphereLocationsToRender; //List that is consumed by GL.Lines function

    public float TEST_RADIUS;
    public Vector3 RunwayRotationOffset = new Vector3();
    List<Line> PreProcessOutlineList = new List<Line>();

    List<Line> LineList = new List<Line>();

    List<GameObject> testingCubeList = new List<GameObject>();
    void Start()
    {
        //Used to display runway/helipad in unity scene
        //testingCubeList.Add(GameObject.CreatePrimitive(PrimitiveType.Cube));
        //testingCubeList.Add(GameObject.CreatePrimitive(PrimitiveType.Cube));
        //testingCubeList.Add(GameObject.CreatePrimitive(PrimitiveType.Cube));
        //testingCubeList.Add(GameObject.CreatePrimitive(PrimitiveType.Cube));
        //--------------------------------------------
        //sphereLocationsToRender = new List<Line>();

        HELICOPTER_LATLON = new Vector2((float)39.46319538746391, (float)-74.56770911363704);

        //Acy runway 31 locations
        HELIPADPOINT_LIST = new List<Vector3>(); //Latitude(Deg), Longitude(Deg), Altitude(MSL). 

        //Each line is drawn individually
        // Runway 31/13

        Line lineOne = new Line(new Vector3(39.45136514886047f, -74.55897156996998f, 19.2024f), new Vector3(39.45089496973433f, -74.55928878161691f, 19.2024f));
        PreProcessOutlineList.Add(lineOne);

        Line lineTwo = new Line(new Vector3(39.45089496973433f, -74.55928878161691f, 19.2024f), new Vector3(39.46428307602309f, -74.59180818678979f, 22.86f));
        PreProcessOutlineList.Add(lineTwo);

        Line lineThree = new Line(new Vector3(39.46428307602309f, -74.59180818678979f, 22.86f), new Vector3(39.46476618395125f, -74.59147096375797f, 22.86f));
        PreProcessOutlineList.Add(lineThree);

        Line lineFour = new Line(new Vector3(39.46476618395125f, -74.59147096375797f, 22.86f), new Vector3(39.45136514886047f, -74.55897156996998f, 19.2024f));
        PreProcessOutlineList.Add(lineFour);



        //--------------------------------------------
        // Runway 04/22
        /*
        
        #HELIPADPOINT_LIST.Add(new Vector3(39.44984666219336f, -74.58540889724551f, 18.5928f)); //Runway length
        HELIPADPOINT_LIST.Add(new Vector3(39.464734722923154f, -74.57520654359402f, 20.4216f));

        HELIPADPOINT_LIST.Add(new Vector3(39.44965090279487f, -74.58495121270428f, 18.5928f));
        HELIPADPOINT_LIST.Add(new Vector3(39.46454353171687f, -74.57474039158234f, 20.4216f));//Runway length

        HELIPADPOINT_LIST.Add(new Vector3(39.464542292163294f, -74.574740844978f, 20.4216f));
        HELIPADPOINT_LIST.Add(new Vector3(39.464735108251936f, -74.57520769735143f, 20.4216f));

        HELIPADPOINT_LIST.Add(new Vector3(39.44984281400634f, -74.58540935896782f, 18.5928f));
        HELIPADPOINT_LIST.Add(new Vector3(39.44965277746688f, -74.5849472981126f, 18.5928f)); */
        Line lineFive = new Line(new Vector3(39.44984666219336f, -74.58540889724551f, 18.5928f), new Vector3(39.464734722923154f, -74.57520654359402f, 20.4216f));
        Line lineSix = new Line(new Vector3(39.44965090279487f, -74.58495121270428f, 18.5928f), new Vector3(39.46454353171687f, -74.57474039158234f, 20.4216f));
        Line lineSeven = new Line(new Vector3(39.464542292163294f, -74.574740844978f, 20.4216f), new Vector3(39.464735108251936f, -74.57520769735143f, 20.4216f));
        Line lineEight = new Line(new Vector3(39.44984281400634f, -74.58540935896782f, 18.5928f), new Vector3(39.44965277746688f, -74.5849472981126f, 18.5928f));

        PreProcessOutlineList.Add(lineFive);
        PreProcessOutlineList.Add(lineSix);
    }



    void Update()
    {

    }

    public void UpdateHelipadMarker(Vector2 helicopterLatLon, float helicopterAltitude, float helicopterHeading, float magneticVariation, float pitch, float roll)
    {
        //Debug.Log("PreProcessList Count: " + (PreProcessOutlineList.Count).ToString() );

        LineList.Clear(); //This is a processed of lines

        helicopterAltitude /= 3.281f;  //Convert from feet to meters


        //sphereLocationsToRender.Clear();
        //Latitude 1 is location of helicopter.
        LatLong Coordinate1 = new LatLong(helicopterLatLon.x, helicopterLatLon.y, helicopterAltitude);
        //Latitude 2 is location of marker points

        //Debug.Log(PreProcessOutlineList.Count);

        for (int i = 0; i < PreProcessOutlineList.Count; i++)
        {
            //We need to calculate each point individually before adding back to the Line object/class
            //Calculate the point/distance/bearing vector and change the current line back to that after finish

            //Line myLine = LineList[i];
            Line myLine = PreProcessOutlineList[i];
            Vector3 pointA = myLine.GetPointA();
            Vector3 pointB = myLine.GetPointB();

            //Do pointA first
            //-----------A--------------
            LatLong CoordinateA2 = new LatLong(pointA.x, pointA.y, pointA.z); //lat/lon/altitude
            double pointADistance = LatLong.CalculateDistance(Coordinate1, CoordinateA2); //Distance to point
            double pointABearing = LatLong.CalculateBearing(Coordinate1, CoordinateA2); //+ 180 ; //Bearing to point
            Vector3 tempVectorA = CalculateSphereLocation(pointADistance, pointABearing, pointA.z, helicopterAltitude, helicopterHeading);

            Quaternion offsetrotA = Quaternion.Euler(RunwayRotationOffset.x + pitch, RunwayRotationOffset.y + magneticVariation, RunwayRotationOffset.z - roll);

            Vector3 pointALocation = offsetrotA * tempVectorA;

            //-----------B--------------
            LatLong CoordinateB2 = new LatLong(pointB.x, pointB.y, pointB.z); //lat/lon/altitude
            double pointBDistance = LatLong.CalculateDistance(Coordinate1, CoordinateB2); //Distance to point
            double pointBBearing = LatLong.CalculateBearing(Coordinate1, CoordinateB2); //+ 180 ; //Bearing to point
            Vector3 tempVectorB = CalculateSphereLocation(pointBDistance, pointBBearing, pointB.z, helicopterAltitude, helicopterHeading);

            Quaternion offsetrotB = Quaternion.Euler(RunwayRotationOffset.x + pitch, RunwayRotationOffset.y + magneticVariation, RunwayRotationOffset.z - roll);

            Vector3 pointBLocation = offsetrotB * tempVectorB;

            LineList.Add(new Line(pointALocation, pointBLocation));


        }

        //foreach (Vector3 currentPoint in HELIPADPOINT_LIST) //Loop through list of helipad coordian\tes and calculate bearing/distance to helicopter
        //{                                                   //Then calculate the point it intersects with the sphere and add it to a list to send to the renderer


        //    LatLong Coordinate2 = new LatLong(currentPoint.x, currentPoint.y, currentPoint.z);
        //    double pointDistance = LatLong.CalculateDistance(Coordinate1, Coordinate2); //Distance to point
        //    double pointBearing = LatLong.CalculateBearing(Coordinate1, Coordinate2); //+ 180 ; //Bearing to point

        //    Vector3 tempVector = CalculateSphereLocation(pointDistance, pointBearing, currentPoint.z, helicopterAltitude, helicopterHeading);

        //    Quaternion offsetrot = Quaternion.Euler(RunwayRotationOffset.x + pitch, RunwayRotationOffset.y + magneticVariation, RunwayRotationOffset.z + roll);

        //    //rot is used to add an offset to the tempvector

        //    //Debug.Log("Point distance for point " + HELIPADPOINT_LIST.IndexOf(currentPoint).ToString() + " || " + pointDistance);
        //    //Debug.Log("Point bearing for point " + HELIPADPOINT_LIST.IndexOf(currentPoint).ToString() + " || " +  pointBearing);

        //    sphereLocationsToRender.Add(offsetrot * tempVector); // Add to list to be projected on sphere

        //    if (DEBUG_ON)
        //    {
        //        //Debug.DrawLine(myCam.transform.position, objectPoint, Color.blue);
        //    }

        //    //if (DEBUG_ON)
        //    //{
        //    //    Debug.DrawLine(myCam.transform.position, rot * tempVector, Color.yellow);
        //    //}


        //}
        DrawLines.ClearPointList();
        DrawLines.SetPointList(LineList); //Send to be drawn as GL.Lines
    }


    private Vector3 CalculateSphereLocation(double distance, double bearing, double pointAltitude, double helicopterAltitude, double helicopterHeading)
    {
        /*
         * Calculate the location of a marker projected on the sphere of radius 2.2. 
         * 
         * Takes vector calculated from difference between helicopter location and point location and
         * places it down the z axis. This vector is then rotated by the bearing offset and treated as though the helicopter is at 0
         * Hence the subtraction of pointaltitude - helicopterAltitude. The helicopter will most likely be above the point at all times
         * and so it will be below the user. 


        //DISTANCE IS IN METERS
        /* You have to subtract point altitude from helicopter altitude and negate it 
         * This is because the helicopter is ABOVE the point, and since the camera is stationary at 0,0,0
         * the point will appear to be below
         */

        Vector3 objectPoint = new Vector3(0, (float)(pointAltitude - helicopterAltitude), (float)distance); //Point we want to draw a ray to 
        Vector3 rotatedUnityPoint = Quaternion.AngleAxis(((float)bearing - (float)helicopterHeading), Vector3.up) * objectPoint; //Rotate that point around y axis by calculated bearing

        //Debug.DrawLine(myCam.transform.position, rotatedUnityPoint, Color.yellow);

        return rotatedUnityPoint; //ProjectPointOnSphere(rotatedUnityPoint, projectFromPoint.transform.position, TEST_RADIUS);

        //Debug.DrawLine(myCam.transform.position, objectPoint, Color.red);

    }


}
