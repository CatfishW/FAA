/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using UnityEditor;

namespace OnlineMaps.Editors
{
    [CustomEditor(typeof(ElevationManagerBase), true)]
    public abstract class ElevationManagerBaseEditor : FormattedEditor
    {
        protected SerializedProperty bottomMode;
        protected SerializedProperty scale;
        protected SerializedProperty zoomRange;
        protected SerializedProperty lockYScale;
        protected SerializedProperty yScaleValue;

        protected override void CacheSerializedFields()
        {
            bottomMode = serializedObject.FindProperty("bottomMode");
            scale = serializedObject.FindProperty("scale");
            zoomRange = serializedObject.FindProperty("zoomRange");
            lockYScale = serializedObject.FindProperty("lockYScale");
            yScaleValue = serializedObject.FindProperty("yScaleValue");
        }

        protected override void GenerateLayoutItems()
        {
            base.GenerateLayoutItems();

            rootLayoutItem.Create(bottomMode).OnChangedInPlaymode += RedrawMap;
            rootLayoutItem.Create(scale).OnChangedInPlaymode += RedrawMap;
            rootLayoutItem.Create(zoomRange).OnChangedInPlaymode += RedrawMap;
            rootLayoutItem.Create(lockYScale).OnChangedInPlaymode += RedrawMap;
            LayoutItem yScaleLI = rootLayoutItem.Create(yScaleValue);
            yScaleLI.OnChangedInPlaymode += RedrawMap;
            yScaleLI.OnValidateDraw += () => lockYScale.boolValue;
        }

        protected void RedrawMap()
        {
            Map map = (target as ElevationManagerBase).GetComponent<Map>();
            if (map == null) map = Map.instance;
            if (map != null) map.Redraw();
        }
    }
}