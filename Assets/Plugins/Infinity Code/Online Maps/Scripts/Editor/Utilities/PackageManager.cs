/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using UnityEditor;

namespace OnlineMaps.Editors
{
    public class PackageManager
    {
        public const string ThirdPartyPath = EditorUtils.MenuPath + "Third-Party/";
        
        [MenuItem(ThirdPartyPath + "Fingers Touch Gestures Connector", false, 1)]
        public static void ImportFingersTouchGesturesConnector()
        {
            EditorUtils.ImportPackage("Packages/FingersTouchGestures-Connector.unitypackage", 
                new EditorUtils.Warning
                {
                    title = "Fingers Touch Gestures Connector",
                    message = "You have Fingers Touch Gestures in your project?",
                    ok = "Yes, I have a Fingers Touch Gestures"
                },
                "Could not find Fingers Touch Gestures Connector."
            );
        }
        
        [MenuItem(ThirdPartyPath + "Playmaker Integration Kit", false, 1)]
        public static void ImportPlayMakerIntegrationKit()
        {
            EditorUtils.ImportPackage("Packages/OnlineMaps-Playmaker-Integration-Kit.unitypackage", 
                new EditorUtils.Warning
                {
                    title = "Playmaker Integration Kit",
                    message = "You have Playmaker in your project?",
                    ok = "Yes, I have a Playmaker"
                },
                "Could not find Playmaker Integration Kit."
            );
        }
        
        [MenuItem(ThirdPartyPath + "Real World Terrain Connector", false, 1)]
        public static void ImportRealWorldTerrainConnector()
        {
            EditorUtils.ImportPackage("Packages/RealWorldTerrain-Connector.unitypackage", 
                new EditorUtils.Warning
                {
                    title = "Real World Terrain Connector",
                    message = "You have Real World Terrain in your project?",
                    ok = "Yes, I have a Real World Terrain"
                },
                "Could not find Real World Terrain Connector."
            );
        }
        
        [MenuItem(ThirdPartyPath + "TouchScript Connector", false, 1)]
        public static void ImportTouchScriptConnector()
        {
            EditorUtils.ImportPackage("Packages/TouchScript-Connector.unitypackage", 
                new EditorUtils.Warning
                {
                    title = "TouchScript Connector",
                    message = "You have TouchScript in your project?",
                    ok = "Yes, I have a TouchScript"
                },
                "Could not find TouchScript Connector."
            );
        }
        
        [MenuItem(ThirdPartyPath + "uPano Connector", false, 1)]
        public static void ImportUPanoConnector()
        {
            EditorUtils.ImportPackage("Packages/uPano-Connector.unitypackage", 
                new EditorUtils.Warning
                {
                    title = "uPano Connector",
                    message = "You have uPano and uPano Google Street View Service in your project?",
                    ok = "Yes, I have uPano"
                },
                "Could not find uPano Connector."
            );
        }

        [MenuItem(ThirdPartyPath + "Visual Scripting Integration Kit", false, 1)]
        public static void ImportVisualScriptingIntegrationKit()
        {
            EditorUtils.ImportPackage("Packages/OnlineMaps-Visual-Scripting-Integration-Kit.unitypackage", 
                new EditorUtils.Warning
                {
                    title = "Visual Scripting Integration Kit",
                    message = "You have Visual Scripting in your project?",
                    ok = "Yes, I have a Visual Scripting"
                },
                "Could not find Visual Scripting Integration Kit."
            );
        }
    }
}