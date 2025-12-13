/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System.Linq;
using UnityEditor;
using UnityEngine;

namespace OnlineMaps.Editors
{
    public partial class MapEditor
    {
        private GUIContent cTooltipTexture;
        private GUIContent[] cTrafficProviders;
        private SerializedProperty pCountParentLevels;
        private SerializedProperty pCustomTrafficProviderURL;
        private SerializedProperty pDefaultTileTexture;
        private SerializedProperty pEmptyColor;
        private SerializedProperty pRedrawOnPlay;
        private SerializedProperty pShowMarkerTooltip;
        private SerializedProperty pTooltipTexture;
        private SerializedProperty pTraffic;
        private SerializedProperty pTrafficProviderID;

        private TrafficProvider[] trafficProviders;
        private int trafficProviderIndex;
        
        private bool showAdvanced;

        private void CacheAdvancedProperties()
        {
            cTooltipTexture = new GUIContent("Tooltip Background");
            
            pCustomTrafficProviderURL = serializedObject.FindProperty("customTrafficProviderURL");
            pCountParentLevels = serializedObject.FindProperty("countParentLevels");
            pDefaultTileTexture = serializedObject.FindProperty("defaultTileTexture");
            pEmptyColor = serializedObject.FindProperty("emptyColor");
            pRedrawOnPlay = serializedObject.FindProperty("redrawOnPlay");
            pShowMarkerTooltip = serializedObject.FindProperty("showMarkerTooltip");
            pTooltipTexture = serializedObject.FindProperty("tooltipBackgroundTexture");
            pTraffic = serializedObject.FindProperty("traffic");
            pTrafficProviderID = serializedObject.FindProperty("trafficProviderID");

            if (!pTooltipTexture.objectReferenceValue)
            {
                pTooltipTexture.objectReferenceValue = EditorUtils.LoadAsset<Texture2D>("Textures/Tooltip.psd");
            }
            
            trafficProviders = TrafficProvider.GetProviders();
            cTrafficProviders = trafficProviders.Select(p => new GUIContent(p.title)).ToArray();
            trafficProviderIndex = 0;
            for (int i = 0; i < trafficProviders.Length; i++)
            {
                if (trafficProviders[i].id != pTrafficProviderID.stringValue) continue;
                
                trafficProviderIndex = i;
                break;
            }
        }
        
        private void DrawAdvanced()
        {
            float oldWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 160;

            if (control && control.resultIsTexture)
            {
                EditorUtils.PropertyField(pRedrawOnPlay, "Redraw the map immediately after the start of the scene");
            }

            EditorGUI.BeginChangeCheck();
            EditorUtils.PropertyField(pCountParentLevels, "Tiles for the specified number of parent levels will be loaded");
            if (EditorGUI.EndChangeCheck())
            {
                pCountParentLevels.intValue = Mathf.Clamp(pCountParentLevels.intValue, 0, 20);
            }

            EditorUtils.PropertyField(pTraffic, "Display traffic jams");
            if (pTraffic.boolValue)
            {
                EditorGUI.BeginChangeCheck();
                trafficProviderIndex = EditorGUILayout.Popup(TempContent.Get("Traffic Provider"), trafficProviderIndex, cTrafficProviders);
                if (EditorGUI.EndChangeCheck()) pTrafficProviderID.stringValue = trafficProviders[trafficProviderIndex].id;
                if (trafficProviders[trafficProviderIndex].isCustom) EditorGUILayout.PropertyField(pCustomTrafficProviderURL, TempContent.Get("URL"));
            }

            EditorUtils.PropertyField(pEmptyColor, "The color that will be displayed until the tile is loaded.\nImportant: if Default Tile Texture is specified, this value will be ignored.");

            EditorGUI.BeginChangeCheck();
            EditorUtils.PropertyField(pDefaultTileTexture, "The texture that will be displayed until the tile is loaded");
            if (EditorGUI.EndChangeCheck()) ControlBase2DEditor<ControlBase2D>.CheckAPITextureImporter(pDefaultTileTexture);

            EditorUtils.PropertyField(pTooltipTexture, cTooltipTexture, "Tooltip background texture");
            EditorUtils.PropertyField(pShowMarkerTooltip, "Tooltip display rule");

            EditorGUIUtility.labelWidth = oldWidth;
        }
        
        private void DrawAdvancedBlock()
        {
            EditorGUILayout.BeginVertical(GUI.skin.box);
            showAdvanced = EditorUtils.Foldout(showAdvanced, "Advanced");
            if (showAdvanced) DrawAdvanced();
            EditorGUILayout.EndVertical();
        }
    }
}