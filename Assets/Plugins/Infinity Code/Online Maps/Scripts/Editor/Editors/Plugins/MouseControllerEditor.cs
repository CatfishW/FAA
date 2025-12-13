/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using UnityEditor;

namespace OnlineMaps.Editors
{
    [CustomEditor(typeof(MouseController))]
    public class MouseControllerEditor: Editor
    {
        private MouseController controller;
        
        private SerializedProperty allowUserControl;
        private SerializedProperty allowZoom;
        private SerializedProperty checkScreenSizeForWheelZoom;
        private SerializedProperty dragMarkerHoldingCTRL;
        private SerializedProperty longPressDelay;
        private SerializedProperty startDragDistance;
        private SerializedProperty zoomInOnDoubleClick;
        private SerializedProperty zoomMode;
        private SerializedProperty zoomSensitivity;
        private SerializedProperty zoomSpeed;

        private void OnEnable()
        {
            controller = target as MouseController;
            CacheSerializedFields();
        }

        private void CacheSerializedFields()
        {
             allowUserControl = serializedObject.FindProperty("allowUserControl");
             allowZoom = serializedObject.FindProperty("allowZoom");
             checkScreenSizeForWheelZoom = serializedObject.FindProperty("checkScreenSizeForWheelZoom");
             dragMarkerHoldingCTRL = serializedObject.FindProperty("dragMarkerHoldingCTRL");
             longPressDelay = serializedObject.FindProperty("longPressDelay");
             startDragDistance = serializedObject.FindProperty("startDragDistance");
             zoomInOnDoubleClick = serializedObject.FindProperty("zoomInOnDoubleClick");
             zoomMode = serializedObject.FindProperty("zoomMode");
             zoomSensitivity = serializedObject.FindProperty("zoomSensitivity");
             zoomSpeed = serializedObject.FindProperty("zoomSpeed");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            float labelWidth = EditorGUIUtility.labelWidth;
            float minWidth = 230;
            if (labelWidth < minWidth) EditorGUIUtility.labelWidth = minWidth;
            try
            {
                DrawProperties();
            }
            finally
            {
                EditorGUIUtility.labelWidth = labelWidth;
            }
            
            serializedObject.ApplyModifiedProperties();
        }

        private void DrawProperties()
        {
            EditorGUILayout.PropertyField(allowUserControl);
            
            EditorGUILayout.PropertyField(allowZoom);
            EditorGUI.indentLevel++;
            EditorGUI.BeginDisabledGroup(!allowZoom.boolValue);
            EditorGUILayout.PropertyField(zoomMode);
            EditorGUILayout.PropertyField(zoomInOnDoubleClick);
            EditorGUILayout.PropertyField(zoomSensitivity);
            EditorGUILayout.PropertyField(zoomSpeed);
            EditorGUILayout.PropertyField(checkScreenSizeForWheelZoom);
            EditorGUI.EndDisabledGroup();
            EditorGUI.indentLevel--;
            
            EditorGUILayout.PropertyField(dragMarkerHoldingCTRL);
            EditorGUILayout.PropertyField(startDragDistance);
            EditorGUILayout.PropertyField(longPressDelay);
        }
    }
}