/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using UnityEditor;
using UnityEngine;

namespace OnlineMaps.Editors
{
    [CustomPropertyDrawer(typeof(Marker2D))]
    public class Marker2DPropertyDrawer : MarkerBasePropertyDrawer
    {
        protected override int countFields => Utils.isPlaying? 10: 9;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            try
            {
                Rect rect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
                if (!DrawHeader(label, rect, property))
                {
                    EditorGUI.EndProperty();
                    return;
                }
                
                rect.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

                SerializedProperty location = DrawLocationProperty(property, "_location", ref rect);
                DrawProperty(property, "range", ref rect, TempContent.Get("Zooms"));

                EditorGUI.BeginChangeCheck();
                SerializedProperty pRot = DrawProperty(property, "_rotation", ref rect, TempContent.Get("Rotation"));
                if (EditorGUI.EndChangeCheck()) pRot.floatValue = Mathf.Repeat(pRot.floatValue, 360);

                DrawProperty(property, "_scale", ref rect);
                DrawProperty(property, "label", ref rect);
                DrawProperty(property, "align", ref rect);

                EditorGUI.BeginChangeCheck();
                SerializedProperty pTexture = DrawProperty(property, "_texture", ref rect);
                if (EditorGUI.EndChangeCheck()) OnTextureChanged(property, pTexture);

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

        private static void OnTextureChanged(SerializedProperty property, SerializedProperty pTexture)
        {
            EditorUtils.CheckMarkerTextureImporter(pTexture);

            if (!EditorApplication.isPlaying) return;
            
            string displayName = property.displayName;
            string indexStr = displayName.Substring(8);
            
            int index;
            if (!int.TryParse(indexStr, out index)) return;
            
            Marker2DManager manager = property.serializedObject.targetObject as Marker2DManager;
            if (!manager) return;
            
            Marker2D marker = manager[index];
            if (marker == null) return;
            
            marker.texture = pTexture.objectReferenceValue as Texture2D;
            manager.map.Redraw();
        }
    }
}