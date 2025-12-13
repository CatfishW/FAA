/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using UnityEditor;

namespace OnlineMaps.Editors
{
    [CustomEditor(typeof(BingMapsElevationManager), true)]
    public class BingMapsElevationManagerEditor : SinglePartElevationManagerEditor
    {
        private KeyManager keyManager;

        private void CheckKey()
        {
            if (!keyManager)
            {
                EditorGUILayout.HelpBox("Potential problem detected:\nCannot find Key Manager component.", MessageType.Warning);
            }
            else if (string.IsNullOrEmpty(keyManager.bingMaps))
            {
                EditorGUILayout.HelpBox("Potential problem detected:\nKey Manager / Bing Maps is empty.", MessageType.Warning);
            }
        }
        
        protected override void GenerateLayoutItems()
        {
            base.GenerateLayoutItems();

            rootLayoutItem.Create("keyWarning", CheckKey);
        }

        protected override void OnEnableLate()
        {
            base.OnEnableLate();

            keyManager = (target as BingMapsElevationManager).GetComponent<KeyManager>();
            if (!keyManager) keyManager = Compatibility.FindObjectOfType<KeyManager>();
        }
    }
}