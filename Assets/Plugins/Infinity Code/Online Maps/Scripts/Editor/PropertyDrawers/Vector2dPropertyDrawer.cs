/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using UnityEditor;
using UnityEngine;

namespace OnlineMaps.Editors
{
    [CustomPropertyDrawer(typeof(Vector2d))]
    public class Vector2dPropertyDrawer : PropertyDrawer
    {
        private static GUIContent xContent = new GUIContent("X");
        private static GUIContent yContent = new GUIContent("Y");

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
            EditorGUI.PropertyField(line, x, xContent);
            
            line.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            EditorGUI.PropertyField(line, y, yContent);
            EditorGUIUtility.labelWidth += 15;
        }
    }
}