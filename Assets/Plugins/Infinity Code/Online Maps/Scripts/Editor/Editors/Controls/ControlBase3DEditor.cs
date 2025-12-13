/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using UnityEditor;
using UnityEngine;

namespace OnlineMaps.Editors
{
    [CustomEditor(typeof(ControlBase3D), true)]
    public class ControlBase3DEditor<T> : ControlBaseEditor<T>
        where T: ControlBase3D
    {
        protected SerializedProperty pMarker2DMode;
        protected SerializedProperty pMarker2DSize;
        protected SerializedProperty pActiveCamera;

        protected override bool hasSettings => true;

        protected override void CacheSerializedFields()
        {
            base.CacheSerializedFields();

            pMarker2DMode = serializedObject.FindProperty("marker2DMode");
            pMarker2DSize = serializedObject.FindProperty("marker2DSize");
            pActiveCamera = serializedObject.FindProperty("activeCamera");
        }

        protected override void GenerateLayoutItems()
        {
            base.GenerateLayoutItems();

            rootLayoutItem.Create(pActiveCamera).content = new GUIContent("Camera");
            LayoutItem markerMode = rootLayoutItem.Create(pMarker2DMode);
            markerMode.OnChangedInPlaymode += () =>
            {
                if (pMarker2DMode.enumValueIndex == (int) Marker2DMode.billboard) control.marker2DDrawer = new MarkerBillboardDrawer(control as ControlBaseDynamicMesh);
                else control.marker2DDrawer = new MarkerFlatDrawer(control as ControlBaseDynamicMesh);
                map.Redraw();
            };

            markerMode.Create(pMarker2DSize).OnValidateDraw += () => pMarker2DMode.enumValueIndex == (int)Marker2DMode.billboard;
        }

        protected override void OnEnableBefore()
        {
            base.OnEnableBefore();

            if (control.GetComponent<Marker3DManager>() == null) control.gameObject.AddComponent<Marker3DManager>();
        }
    }
}