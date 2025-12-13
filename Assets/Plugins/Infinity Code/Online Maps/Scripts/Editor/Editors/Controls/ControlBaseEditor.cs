/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using UnityEditor;
using UnityEngine;

namespace OnlineMaps.Editors
{
    [CustomEditor(typeof(ControlBase), true)]
    public abstract class ControlBaseEditor<T> : FormattedEditor
        where T : ControlBase
    {
        protected Map map;
        protected T control;

        protected LayoutItem warningLayoutItem;
        protected virtual bool hasSettings => false;

        protected override void CacheSerializedFields()
        {
            
        }

        private static Map DrawNoMapError(ControlBase control)
        {
            EditorGUILayout.BeginVertical(GUI.skin.box);

            EditorGUILayout.HelpBox("Problem detected:\nCan not find OnlineMaps component.", MessageType.Error);

            Map map = null;
            if (GUILayout.Button("Add OnlineMaps Component"))
            {
                map = control.gameObject.AddComponent<Map>();
                UnityEditorInternal.ComponentUtility.MoveComponentUp(map);
            }

            EditorGUILayout.EndVertical();
            return map;
        }

        protected override void GenerateLayoutItems()
        {
            base.GenerateLayoutItems();

            warningLayoutItem = rootLayoutItem.Create("WarningArea");
            warningLayoutItem.Create("NoMapError", () => map = DrawNoMapError(control)).OnValidateDraw += () => !map;
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            map = null;
            control = null;
        }

        protected override void OnEnableBefore()
        {
            base.OnEnableBefore();

            control = (T)target;
            map = control.GetComponent<Map>();
            if (!control.GetComponent<Marker2DManager>()) control.gameObject.AddComponent<Marker2DManager>();
        }

        protected override void OnSetDirty()
        {
            base.OnSetDirty();

            EditorUtility.SetDirty(map);
            EditorUtility.SetDirty(control);

            if (Utils.isPlaying) map.Redraw();
        }
    }
}