/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using UnityEditor;
using UnityEngine;

namespace OnlineMaps.Editors
{
    [CustomPropertyDrawer(typeof(GeoPoint))]
    public class GeoPointPropertyDrawer : PropertyDrawer
    {
        private static GUIContent xContent = new GUIContent("Longitude");
        private static GUIContent yContent = new GUIContent("Latitude");

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight * 3 + EditorGUIUtility.standardVerticalSpacing * 2;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            SerializedProperty x = property.FindPropertyRelative("x");
            SerializedProperty y = property.FindPropertyRelative("y");
            
            Rect line = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            EditorGUI.LabelField(line, label);
            
            line.xMin += 15;
            EditorGUIUtility.labelWidth -= 15;
            line.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            
            EditorGUI.BeginChangeCheck();
            EditorGUI.PropertyField(line, y, yContent);
            if (EditorGUI.EndChangeCheck())
            {
                if (y.doubleValue < -90) y.doubleValue = -90;
                else if (y.doubleValue > 90) y.doubleValue = 90;
            }
            
            line.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            
            EditorGUI.BeginChangeCheck();
            EditorGUI.PropertyField(line, x, xContent);
            if (EditorGUI.EndChangeCheck())
            {
                if (x.doubleValue < -180) x.doubleValue += 360;
                else if (x.doubleValue > 180) x.doubleValue -= 360;
            }
            
            
            EditorGUIUtility.labelWidth += 15;
        }
    }
}