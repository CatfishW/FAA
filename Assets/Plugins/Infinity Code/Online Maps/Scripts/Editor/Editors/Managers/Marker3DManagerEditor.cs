/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using UnityEditor;
using UnityEngine;

namespace OnlineMaps.Editors
{
    [CustomEditor(typeof(Marker3DManager), true)]
    public class Marker3DManagerEditor : MarkerManagerBaseEditor<Marker3DManager, Marker3D>
    {
        private SerializedProperty allowAddMarker3DByN;
        private SerializedProperty defaultPrefab;
        private SerializedProperty defaultScale;

        protected override void AddMarker()
        {
            GeoPoint center = map.view.center;
            if (!Utils.isPlaying)
            {
                Marker3D marker = new Marker3D
                {
                    location = center
                };
                manager.Add(marker);
            }
            else
            {
                manager.Create(center, manager.defaultPrefab);
            }
        
            base.AddMarker();
        }

        protected override void DrawItem(int i, ref int removedIndex)
        {
            base.DrawItem(i, ref removedIndex);

            if (Marker3DPropertyDrawer.isRotationChanged.HasValue)
            {
                manager[i].rotation = Marker3DPropertyDrawer.isRotationChanged.Value;
                Marker3DPropertyDrawer.isRotationChanged = null;
            }
        }

        protected override void DrawSettings()
        {
            base.DrawSettings();

            EditorGUI.BeginChangeCheck();

            EditorGUILayout.PropertyField(defaultPrefab);
            EditorGUILayout.PropertyField(defaultScale);
            EditorGUILayout.PropertyField(allowAddMarker3DByN, TempContent.Get("Add Marker3D by N"));

            if (EditorGUI.EndChangeCheck()) isDirty = true;
        }

        protected override void OnEnableLate()
        {
            base.OnEnableLate();

            allowAddMarker3DByN = serializedObject.FindProperty("allowAddMarker3DByN");
            defaultPrefab = serializedObject.FindProperty("defaultPrefab");
            defaultScale = serializedObject.FindProperty("defaultScale");
        }
    }
}