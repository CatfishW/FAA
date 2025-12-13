/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using UnityEditor;
using UnityEngine;

namespace OnlineMaps.Editors
{
    [CustomEditor(typeof(Buildings))]
    public class BuildingsEditor:Editor
    {
        private Buildings buildings;
        private bool showMaterials;
        private SerializedProperty pZoomRange;
        private SerializedProperty pLevelsRange;
        private SerializedProperty pLevelHeight;
        private SerializedProperty pMinHeight;
        private SerializedProperty pHeightScale;
        private SerializedProperty pMaxBuilding;
        private SerializedProperty pMaxActiveBuildings;
        private SerializedProperty pGenerateColliders;
        private SerializedProperty pMaterials;
        private GUIContent cMinHeight;
        private GUIContent cMaxBuilding;
        private GUIContent cMaxActiveBuildings;
        private SerializedProperty pUseColorTag;
        private SerializedProperty pUseHeightTag;

        protected void CacheSerializedProperties()
        {
            pZoomRange = serializedObject.FindProperty("zoomRange");
            pLevelsRange = serializedObject.FindProperty("levelsRange");
            pLevelHeight = serializedObject.FindProperty("levelHeight");
            pMinHeight = serializedObject.FindProperty("minHeight");
            pHeightScale = serializedObject.FindProperty("heightScale");
            pMaxBuilding = serializedObject.FindProperty("maxBuilding");
            pMaxActiveBuildings = serializedObject.FindProperty("maxActiveBuildings");
            pMaterials = serializedObject.FindProperty("materials");
            pGenerateColliders = serializedObject.FindProperty("generateColliders");
            pUseColorTag = serializedObject.FindProperty("useColorTag");
            pUseHeightTag = serializedObject.FindProperty("useHeightTag");

            cMinHeight = new GUIContent("Min Building Height");
            cMaxBuilding = new GUIContent("Max Number of Buildings (0-unlimited)");
            cMaxActiveBuildings = new GUIContent("Max Number of Active Buildings (0-unlimited)");
        }

        public void OnEnable()
        {
            buildings = target as Buildings;
            if (buildings.materials == null) buildings.materials = new BuildingMaterial[0];

            CacheSerializedProperties();
        }

        public override void OnInspectorGUI()
        {
            bool dirty = false;

            serializedObject.Update();

            EditorGUILayout.PropertyField(pZoomRange, TempContent.Get("Zoom"));
            if (buildings.zoomRange.min < 17) EditorGUILayout.HelpBox("Can create a very large number of buildings. This may work slowly.", MessageType.Warning);
        
            EditorGUILayout.PropertyField(pLevelsRange, TempContent.Get("Floors"));

            EditorGUILayout.PropertyField(pLevelHeight, TempContent.Get("Floor Height (meters)"));
        
            EditorGUILayout.PropertyField(pMinHeight, cMinHeight);
            EditorGUILayout.PropertyField(pHeightScale);

            float labelWidth = EditorGUIUtility.labelWidth;
            if (EditorGUIUtility.labelWidth < 270) EditorGUIUtility.labelWidth = 270;
            
            EditorGUILayout.PropertyField(pMaxBuilding, cMaxBuilding);
            EditorGUILayout.PropertyField(pMaxActiveBuildings, cMaxActiveBuildings);
            
            EditorGUIUtility.labelWidth = labelWidth;
            EditorGUILayout.PropertyField(pGenerateColliders);
            EditorGUILayout.PropertyField(pUseColorTag);
            EditorGUILayout.PropertyField(pUseHeightTag);
            

            OnMaterialsGUI(ref dirty);

            serializedObject.ApplyModifiedProperties();
        }

        private void OnMaterialsGUI(ref bool dirty)
        {
            bool showMaterialGroup = showMaterials;
            if (showMaterialGroup) EditorGUILayout.BeginVertical(GUI.skin.box);

            showMaterials = EditorUtils.Foldout(showMaterials, "Materials");
            if (showMaterials)
            {
                int removedIndex = -1;
                for (int i = 0; i < pMaterials.arraySize; i++)
                {
                    EditorGUILayout.BeginVertical(GUI.skin.box);
                    BuildingMaterialPropertyDrawer.isRemoved = false;
                    EditorGUILayout.PropertyField(pMaterials.GetArrayElementAtIndex(i), TempContent.Get("Material " + (i + 1)));
                    if (BuildingMaterialPropertyDrawer.isRemoved) removedIndex = i;
                    EditorGUILayout.EndHorizontal();
                }

                if (removedIndex != -1)
                {
                    ArrayUtility.RemoveAt(ref buildings.materials, removedIndex);
                    dirty = true;
                }

                if (GUILayout.Button("Add Material"))
                {
                    ArrayUtility.Add(ref buildings.materials, new BuildingMaterial());
                    dirty = true;
                }
            }

            if (showMaterialGroup) EditorGUILayout.EndVertical();
        }
    }
}