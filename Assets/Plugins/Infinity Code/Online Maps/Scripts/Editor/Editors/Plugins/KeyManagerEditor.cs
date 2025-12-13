/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using UnityEditor;

namespace OnlineMaps.Editors
{
    [CustomEditor(typeof(KeyManager))]
    public class KeyManagerEditor : Editor
    {
        private void DrawProperties()
        {
            serializedObject.Update();
            SerializedProperty it = serializedObject.GetIterator();
            it.Next(true);

            while (it.NextVisible(false))
            {
                if (it.name == "m_Script") continue;
                EditorGUILayout.PropertyField(it);
            }
            
            serializedObject.ApplyModifiedProperties();
        }

        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("This component contains keys to web services, but not tiles.\nThe keys to the tiles are specified in the Map component after the map type.", MessageType.Info);
            EditorGUILayout.HelpBox("Here is the name of the service, not an indication that you need to enter something in this field.", MessageType.Info);

            DrawProperties();
        }
    }
}