/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using UnityEditor;
using UnityEngine;

namespace OnlineMaps.Editors
{
    [CustomPropertyDrawer(typeof(Marker3D))]
    public class Marker3DPropertyDrawer : MarkerBasePropertyDrawer
    {
        public static float? isRotationChanged;
        protected override int countFields => Utils.isPlaying? 10: 9;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            try
            {
                Rect rect = new Rect(position.x, position.y, position.width, 16);

                if (!DrawHeader(label, rect, property))
                {
                    EditorGUI.EndProperty();
                    return;
                }
                
                rect.y += EditorGUIUtility.singleLineHeight;

                SerializedProperty location = DrawLocationProperty(property, "_location", ref rect);
                DrawProperty(property, "range", ref rect, TempContent.Get("Zooms"));

                DrawProperty(property, "_scale", ref rect);
                DrawProperty(property, "sizeType", ref rect);

                EditorGUI.BeginChangeCheck();
                DrawProperty(property, "_rotation", ref rect);
                if (EditorGUI.EndChangeCheck() && Utils.isPlaying) isRotationChanged = property.FindPropertyRelative("_rotation").floatValue;

                DrawProperty(property, "label", ref rect);
                
                EditorGUI.BeginChangeCheck();
                DrawProperty(property, "_prefab", ref rect);
                if (EditorGUI.EndChangeCheck() && Utils.isPlaying)
                {
                    
                }

                DrawCenterButton(rect, location);
            }
            catch (ExitGUIException)
            {
                throw;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }


            EditorGUI.EndProperty();
        }
    }
}