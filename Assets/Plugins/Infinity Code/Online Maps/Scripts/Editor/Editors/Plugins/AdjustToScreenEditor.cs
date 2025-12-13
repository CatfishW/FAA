/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using UnityEditor;

namespace OnlineMaps.Editors
{
    [CustomEditor(typeof(AdjustToScreen))]
    public class AdjustToScreenEditor : Editor
    {
        private bool showUIImageWarning = false;

        private void OnEnable()
        {
            showUIImageWarning = (target as AdjustToScreen).GetComponent<UIImageControl>();
        }

        public override void OnInspectorGUI()
        {
            if (showUIImageWarning)
            {
                EditorGUILayout.HelpBox("Important: Adjust to Screen does not always work stably for UI Image Control. It looks like a bug in Unity Editor, and we have not yet found a way to work around this. Recommended to use another control.", MessageType.Warning);
            }

            base.OnInspectorGUI();
        }
    }
}