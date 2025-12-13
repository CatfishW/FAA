/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using UnityEditor;
using UnityEngine;

namespace OnlineMaps.Editors
{
    [CustomEditor(typeof(Marker2DManager), true)]
    public class Marker2DManagerEditor: MarkerManagerBaseEditor<Marker2DManager, Marker2D>
    {
        private SerializedProperty defaultTexture;
        private SerializedProperty defaultAlign;
        private SerializedProperty allowAddMarkerByM;
        private SerializedProperty defaultScale;

        protected override void AddMarker()
        {
            GeoPoint center = map.view.center;
            if (!Utils.isPlaying)
            {
                Marker2D marker = new Marker2D
                {
                    location = center,
                    align = manager.defaultAlign,
                    scale = manager.defaultScale
                };
                manager.Add(marker);
            }
            else
            {
                Marker2D marker = manager.Create(center);
                marker.align = manager.defaultAlign;
                marker.scale = manager.defaultScale;
            }
        
            base.AddMarker();
        }

        protected override void DrawSettings()
        {
            base.DrawSettings();

            EditorGUI.BeginChangeCheck();

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(defaultTexture);
            if (EditorGUI.EndChangeCheck()) EditorUtils.CheckMarkerTextureImporter(defaultTexture);

            EditorGUILayout.PropertyField(defaultAlign);
            EditorGUILayout.PropertyField(defaultScale);
            EditorGUILayout.PropertyField(allowAddMarkerByM, TempContent.Get("Add Marker by M"));

            if (EditorGUI.EndChangeCheck()) isDirty = true;
        }

        protected override void OnEnableLate()
        {
            base.OnEnableLate();

            defaultTexture = serializedObject.FindProperty("defaultTexture");
            defaultAlign = serializedObject.FindProperty("defaultAlign");
            defaultScale = serializedObject.FindProperty("defaultScale");
            allowAddMarkerByM = serializedObject.FindProperty("allowAddMarkerByM");

            if (!defaultTexture.objectReferenceValue)
            {
                defaultTexture.objectReferenceValue = EditorUtils.LoadAsset<Texture2D>("Textures/Markers/DefaultMarker.png");
            }
        }
    }
}