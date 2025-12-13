/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using UnityEditor;
using UnityEngine;

namespace OnlineMaps.Editors
{
    [CustomEditor(typeof(UserLocation), true)]
    public class UserLocationEditor : UserLocationEditorBase
    {
        private SerializedProperty desiredAccuracy;
        private SerializedProperty updateDistance;
        private SerializedProperty requestPermissionRuntime;

        protected override void CacheSerializedProperties()
        {
            base.CacheSerializedProperties();
            desiredAccuracy = serializedObject.FindProperty("desiredAccuracy");
            updateDistance = serializedObject.FindProperty("updateDistance");
            requestPermissionRuntime = serializedObject.FindProperty("requestPermissionRuntime");
        }

        public override void CustomInspectorGUI()
        {
            EditorGUILayout.PropertyField(desiredAccuracy, TempContent.Get("Desired Accuracy (meters)"));
            EditorGUILayout.PropertyField(updateDistance);
            EditorGUILayout.PropertyField(requestPermissionRuntime);
        }

        public override void CustomUpdateLocationGUI()
        {
        }
    }
}