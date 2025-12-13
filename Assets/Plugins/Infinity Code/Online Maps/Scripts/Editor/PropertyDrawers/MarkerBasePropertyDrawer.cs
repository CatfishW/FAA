/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using UnityEditor;
using UnityEngine;

namespace OnlineMaps.Editors
{
    public abstract class MarkerBasePropertyDrawer : PropertyDrawer
    {
        public static bool isRemoved = false;
        public static bool? isEnabledChanged;

        protected virtual int countFields => 0;

        protected void DrawCenterButton(Rect rect, SerializedProperty location)
        {
            rect.height = EditorGUIUtility.singleLineHeight;

            if (!Utils.isPlaying || !GUI.Button(rect, "Center")) return;
            
            SerializedProperty longitude = location.FindPropertyRelative("x");
            SerializedProperty latitude = location.FindPropertyRelative("y");
            Map.instance.view.SetCenter(longitude.doubleValue, latitude.doubleValue);
        }

        protected bool DrawHeader(GUIContent label, Rect rect, SerializedProperty property)
        {
            SerializedProperty pExpand = property.FindPropertyRelative("expand");
            pExpand.boolValue = EditorGUI.Toggle(new Rect(rect.x, rect.y, 16, rect.height), string.Empty, pExpand.boolValue, EditorStyles.foldout);

            SerializedProperty pEnabled = property.FindPropertyRelative("_enabled");

            EditorGUI.BeginChangeCheck();
            bool newEnabled = EditorGUI.ToggleLeft(new Rect(rect.x + 16, rect.y, rect.width - 36, rect.height), label, pEnabled.boolValue);
            if (EditorGUI.EndChangeCheck())
            {
                if (Utils.isPlaying) isEnabledChanged = newEnabled;
                else pEnabled.boolValue = newEnabled;
            }

            if (GUI.Button(new Rect(rect.x + rect.width - 20, rect.y, 20, rect.height), "X")) isRemoved = true;

            return pExpand.boolValue;
        }

        protected SerializedProperty DrawLocationProperty(SerializedProperty property, string name, ref Rect rect)
        {
            SerializedProperty prop = property.FindPropertyRelative(name);
            SerializedProperty x = prop.FindPropertyRelative("x");
            SerializedProperty y = prop.FindPropertyRelative("y");
            
            float propertyHeight = EditorGUI.GetPropertyHeight(y);
            rect.height = propertyHeight;
            EditorGUI.PropertyField(rect, y, TempContent.Get("Latitude"));
            rect.y += propertyHeight + EditorGUIUtility.standardVerticalSpacing;
            
            propertyHeight = EditorGUI.GetPropertyHeight(x);
            rect.height = propertyHeight;
            EditorGUI.PropertyField(rect, x, TempContent.Get("Longitude"));
            rect.y += propertyHeight + EditorGUIUtility.standardVerticalSpacing;
            
            return prop;
        }

        protected SerializedProperty DrawProperty(SerializedProperty property, string name, ref Rect rect, GUIContent label = null)
        {
            SerializedProperty prop = property.FindPropertyRelative(name);
            float propertyHeight = EditorGUI.GetPropertyHeight(prop, label);
            rect.height = propertyHeight;
            EditorGUI.PropertyField(rect, prop, label);
            rect.y += propertyHeight + EditorGUIUtility.standardVerticalSpacing;
            return prop;
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!property.FindPropertyRelative("expand").boolValue) return EditorGUIUtility.singleLineHeight;
            return countFields * EditorGUIUtility.singleLineHeight + (countFields - 1) * EditorGUIUtility.standardVerticalSpacing;
        }
    }
}