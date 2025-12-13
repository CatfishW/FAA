/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using UnityEditor;
using UnityEngine;

namespace OnlineMaps.Editors
{
    [CustomEditor(typeof(Documentation))]
    public class DocumentationEditor : Editor
    {
        private static GUIStyle _centeredLabel;

        private static GUIStyle centeredLabel
        {
            get
            {
                if (_centeredLabel == null)
                {
                    _centeredLabel = new GUIStyle(EditorStyles.label)
                    {
                        alignment = TextAnchor.MiddleCenter
                    };
                }

                return _centeredLabel;
            }
        }

        private static void DrawDocumentation()
        {
            if (GUILayout.Button("Local Documentation"))
            {
                Links.OpenLocalDocumentation();
            }

            if (GUILayout.Button("Online Documentation"))
            {
                Links.OpenDocumentation();
            }

            GUILayout.Space(10);
        }

        private new static void DrawHeader()
        {
            GUILayout.Label("Online Maps", centeredLabel);
            GUILayout.Label("version: " + Map.version, centeredLabel);
            GUILayout.Space(10);
        }

        private void DrawRateAndReview()
        {
            EditorGUILayout.HelpBox("Please don't forget to leave a review on the store page if you liked Online Maps, this helps us a lot!", MessageType.Warning);

            if (GUILayout.Button("Rate & Review"))
            {
                Links.OpenReviews();
            }
        }

        private void DrawSupport()
        {
            if (GUILayout.Button("Support"))
            {
                Links.OpenSupport();
            }

            if (GUILayout.Button("Forum"))
            {
                Links.OpenForum();
            }

            GUILayout.Space(10);
        }

        public override void OnInspectorGUI()
        {
            DrawHeader();
            DrawDocumentation();
            DrawSupport();
            DrawRateAndReview();
        }
    }
}