/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using UnityEngine;

namespace OnlineMaps.Editors
{
    public static class Styles
    {
        private static GUIStyle _warningStyle;
        
        public static GUIStyle warningStyle
        {
            get
            {
                if (_warningStyle != null) return _warningStyle;
                
                _warningStyle = new GUIStyle(GUI.skin.label)
                {
                    normal =
                    {
                        textColor = Color.red
                    },
                    fontStyle = FontStyle.Bold
                };

                return _warningStyle;
            }
        }
    }
}