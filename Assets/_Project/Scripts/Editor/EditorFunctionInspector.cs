using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(EditorFunctionScene))]
public class EditorFunctionInspector : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        var script = serializedObject.targetObject as EditorFunctionScene;
        if (GUILayout.Button("Run event function specified"))
            script.functionToCall.Invoke();
    }
}
