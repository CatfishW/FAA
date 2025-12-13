/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using UnityEditor;

namespace OnlineMaps.Editors
{
    [CustomEditor(typeof(BuildingBase), true)]
    public class BuildingBaseEditor : Editor
    {
        private BuildingBase building;

        private void OnEnable()
        {
            building = target as BuildingBase;
        }

        public override void OnInspectorGUI()
        {
            BuildingBase.MetaInfo[] metaInfo = building.metaInfo;
            if (metaInfo == null) EditorGUILayout.LabelField("Meta count: " + 0);
            else
            {
                int metaCount = metaInfo.Length;
                EditorGUILayout.LabelField("Meta count: " + metaCount);
                EditorGUI.BeginDisabledGroup(true);
                foreach (BuildingBase.MetaInfo item in metaInfo) EditorGUILayout.TextField(item.title, item.info);
                EditorGUI.EndDisabledGroup();
            }
        }
    }
}