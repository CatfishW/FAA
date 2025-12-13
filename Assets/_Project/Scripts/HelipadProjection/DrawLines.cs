using UnityEngine;
using System.Collections.Generic;
public class DrawLines : MonoBehaviour
{
    /*
     * Class for drawing lines passed into the pointlist
     * Grant Morfitt
     */

    public Vector3 TEST_OFFSET;
    static Material lineMaterial;
    private static List<Line> lineList = new List<Line>();
    private static List<Vector3> pointList = new List<Vector3>();

    static int NUM_VECTOR_SEGMENTS;
    public int NumberOfSegments;

    private void Start()
    {
        NUM_VECTOR_SEGMENTS = NumberOfSegments;
    }
    static void CreateLineMaterial()
    {
        if (!lineMaterial)
        {
            //Shader shader = Shader.Find("Hidden/Internal-Colored");
            Shader shader = Shader.Find("UI/Unlit/Transparent");
            lineMaterial = new Material(shader);
            lineMaterial.hideFlags = HideFlags.HideAndDontSave;
            // Turn on alpha blending
            lineMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            lineMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            // Turn backface culling off
            lineMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            // Turn off depth writes
            lineMaterial.SetInt("_ZWrite", 0);
        }
    }

    // Will be called after all regular rendering is done

    public void OnRenderObject()
    {
        if (pointList.Count > 0)
        {
            //Draws lines between each pair of vertices passed.
            //If you pass four vertices, A, B, C and D, two lines are drawn: one between A and B, and one between C and D.


            CreateLineMaterial();
            // Apply the line material
            lineMaterial.SetPass(0);

            GL.PushMatrix();

            // Set transformation matrix for drawing to
            // match our transform
            GL.Begin(GL.LINES);
            GL.MultMatrix(transform.localToWorldMatrix);
            //GL.Color(Color.green);
            GL.Color(new Color(255f,0f,0f));
            //new Color(38f, 255f, 111f)
            for (int i = 0; i < pointList.Count; i++)
            {
                //GL.Color(new Color((float)i, 0f, 0f));
                GL.Vertex3(pointList[i].x + TEST_OFFSET.x, pointList[i].y + TEST_OFFSET.y, pointList[i].z + TEST_OFFSET.z);
            }
                
            GL.End();


            // Draw lines
       
            GL.PopMatrix();

        }
    }

  

    public static void SetPointList(List<Line> inputList)
    {
       if (inputList.Count != 0)
        {
            lineList = new List<Line>(inputList);
            SegmentLines(lineList);
        }


    }

    public static void SegmentLines(List<Line> inputList)
    {
        //We have lines coming in from the SphereProject class. 
        //Lines are updated from the SetPointList function
        //Lines need to be converted to smaller segments of lines and projected
        pointList.Clear();


        
        foreach (Line currentLine in inputList)
        {

            Vector3 pointA = currentLine.GetPointA();


            Vector3 pointB = currentLine.GetPointB();


            Vector3 differenceVector = pointA - pointB; //Vector between points

            float differenceVectorLength = differenceVector.magnitude;
            float vectorSize = (differenceVectorLength / (float)NUM_VECTOR_SEGMENTS);
            //(length / segments) * i
            //i < numsegments
            //private static int segmentNums = NUM_VECTOR_SEGMENTS;

            Vector3 normalizedDifferenceVector = differenceVector.normalized;


            //Debug.DrawLine(new Vector3(0, 0, 0), normalizedDifferenceVector, Color.yellow);

            

            for (int i = 0; i < NUM_VECTOR_SEGMENTS; i++) //iterate over length of vector
            {
                

                Vector3 linePointB = pointB + vectorSize * i * normalizedDifferenceVector;

                Vector3 linePointA = pointB + vectorSize * (i+1) * normalizedDifferenceVector;

               // Debug.DrawLine(pointA, pointB, Color.green);
                
                Debug.DrawLine(pointA, pointB, Color.yellow);


                pointList.Add(ProjectPointOnSphere(linePointA, new Vector3(0, 0, 0), 2.2f));
                pointList.Add(ProjectPointOnSphere(linePointB, new Vector3(0, 0, 0), 2.2f));

            }


        }

    }
    private static Vector3 ProjectPointOnSphere(Vector3 pointPosition, Vector3 SpherePosition, float radius)
    {
        /*
         * Normalizes vector between pointPosition and spherePosition to the sphere of certain diameter 
         * 
         */

        //Vector3 p = pointPosition - SpherePosition;
        //float pLength = Mathf.Sqrt(p.x * p.x + p.y * p.y + p.z * p.z); //x^2 + y^2 + z^2 Length of vector from sphere to point
        //Vector3 q = (radius / Mathf.Abs(pLength)) * p; // Normalize vector to the sphere. V / |v|
        //Vector3 pointOnSphere = q; //+ myCam.transform.position; //Put point on sphere
        //return pointOnSphere;

        Vector3 point = pointPosition - SpherePosition;
        Vector3 normalizedPoint = point.normalized;

        return (normalizedPoint * radius);


    }
    public static void ClearPointList()
    {
        pointList.Clear();
    }
}