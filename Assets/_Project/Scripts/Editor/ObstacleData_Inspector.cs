using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;


[CustomEditor(typeof(ObstacleData))]
public class ObstacleData_Inspector : Editor
{
    // Start is called before the first frame update
    
    public override void OnInspectorGUI()
    {
        if (GUILayout.Button("Hiding Data To Prevent Lag")) { };

    }
}
