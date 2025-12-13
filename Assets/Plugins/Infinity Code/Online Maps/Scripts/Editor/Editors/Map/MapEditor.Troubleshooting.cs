/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using OnlineMaps.Webservices;
using UnityEditor;
using UnityEngine;

namespace OnlineMaps.Editors
{
    public partial class MapEditor
    {
        private GUIContent cUseSoftwareJPEGDecoder;
        private SerializedProperty pNotInteractUnderGUI;
        private SerializedProperty pOSMServer;
        private SerializedProperty pStopPlayingWhenScriptsCompile;
        private SerializedProperty pUseSoftwareJPEGDecoder;
        private bool showTroubleshooting;

#if !UNITY_WEBGL
        private SerializedProperty pRenderInThread;
#endif

        private void CacheTroubleshootingProperties()
        {
            cUseSoftwareJPEGDecoder = new GUIContent("Software JPEG Decoder");
            pNotInteractUnderGUI = serializedObject.FindProperty("notInteractUnderGUI");
            pOSMServer = serializedObject.FindProperty("osmServer");
            pStopPlayingWhenScriptsCompile = serializedObject.FindProperty("stopPlayingWhenScriptsCompile");
            pUseSoftwareJPEGDecoder = serializedObject.FindProperty("useSoftwareJPEGDecoder");

#if !UNITY_WEBGL
            pRenderInThread = serializedObject.FindProperty("renderInThread");
#endif
        }

        private void DrawTroubleshooting()
        {
            EditorGUILayout.BeginHorizontal(GUI.skin.box);
            float oldWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 220;
            GUILayout.Label("Use this props only if you have a problem!!!", Styles.warningStyle);
            EditorGUILayout.EndHorizontal();
            EditorGUI.BeginChangeCheck();

            EditorUtils.PropertyField(pUseSoftwareJPEGDecoder, cUseSoftwareJPEGDecoder, "If you have problems decoding JPEG images, use software decoder.\nKeep in mind that this greatly affects performance.");

#if !UNITY_WEBGL
            if (control && control.resultIsTexture) EditorUtils.PropertyField(pRenderInThread, "If you have any problems with multithreading, disable this field.");
#endif

            EditorUtils.PropertyField(pNotInteractUnderGUI, "Should Online Maps ignore clicks if an IMGUI or uGUI element is under the cursor?");
            EditorUtils.PropertyField(pStopPlayingWhenScriptsCompile, "Should Online Maps stop playing when recompiling scripts?");
            EditorGUI.BeginChangeCheck();
            EditorUtils.PropertyField(pOSMServer, TempContent.Get("Overpass Server"));
            if (EditorGUI.EndChangeCheck() && EditorApplication.isPlaying) OSMOverpassRequest.InitOSMServer((OSMOverpassServer)pOSMServer.enumValueIndex);

            EditorGUIUtility.labelWidth = oldWidth;

            if (EditorGUI.EndChangeCheck()) isDirty = true;
        }

        private void DrawTroubleshootingBlock()
        {
            EditorGUILayout.BeginVertical(GUI.skin.box);
            showTroubleshooting = EditorUtils.Foldout(showTroubleshooting, "Troubleshooting");
            if (showTroubleshooting) DrawTroubleshooting();
            EditorGUILayout.EndVertical();
        }
    }
}