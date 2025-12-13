/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using UnityEditor;
using UnityEngine;

namespace OnlineMaps.Editors
{
    [CustomEditor(typeof(NGUITextureControl), true)]
    public class NGUITextureControlEditor : ControlBase2DEditor<NGUITextureControl>
    {
        private void DrawNoColliderWarning()
        {
#if NGUI
            EditorGUILayout.BeginVertical(GUI.skin.box);

            EditorGUILayout.HelpBox("Potential problem detected:\nGameObject has no BoxCollider, so you can not control the map.", MessageType.Warning);
            if (GUILayout.Button("Add BoxCollider"))
            {
                BoxCollider bc = map.gameObject.AddComponent<BoxCollider>();
                UITexture uiTexture = map.GetComponent<UITexture>();
                if (uiTexture != null) bc.size = uiTexture.localSize;
                warningLayoutItem.Remove("noCollider");
            }

            EditorGUILayout.EndVertical();
#endif
        }

        protected override void GenerateLayoutItems()
        {
            base.GenerateLayoutItems();

            if (map.GetComponent<BoxCollider>() == null) warningLayoutItem.Create("noCollider", DrawNoColliderWarning);
        }

        public override void OnInspectorGUI()
        {
#if !NGUI
            if (GUILayout.Button("Enable NGUI"))
            {
                if (EditorUtility.DisplayDialog("Enable NGUI", "You have NGUI in your project?", "Yes, I have NGUI", "Cancel"))
                {
                    EditorUtils.AddCompilerDirective("NGUI");
                }
            }
#else
            base.OnInspectorGUI();
#endif
        }
    }
}