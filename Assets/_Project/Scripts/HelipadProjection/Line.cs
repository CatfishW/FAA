using System.Collections;
using System.Collections.Generic;
using UnityEngine;
/* 
 * Helper class for generating Lines
 * Stores lines. Fun stuff
 * 
 * Grant Morfitt
 * 
 */
public class Line
{
    // Start is called before the first frame update
    //Vector3 pointA, pointB;
    private Vector3 _pointA;
    private Vector3 _pointB;

    public Line(Vector3 pointA = default(Vector3), Vector3 pointB= default(Vector3))
    {
        _pointA = pointA;
        _pointB = pointB;
    }

    public Vector3 GetPointA()
    {
        return _pointA;
    }

    public Vector3 GetPointB()
    {
        return _pointB;
    }

    public void SetPointA(Vector3 tempVec)
    {
        _pointA = tempVec;
    }
    public void SetPointB(Vector3 tempVec)
    {
        _pointB = tempVec;
    }


}
