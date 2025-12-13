/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace OnlineMaps.Editors
{
    public abstract class ControlBaseUIEditor<T, U> : ControlBase2DEditor<T>
        where T : ControlBaseUI<U>
        where U : MaskableGraphic
    {
#if !CURVEDUI
        protected void DrawCurvedUIWarning()
        {
            EditorGUILayout.HelpBox("To make the map work properly with Curved UI, enable integration.", MessageType.Info);
            if (GUILayout.Button("Enable Curved UI")) EditorUtils.AddCompilerDirective("CURVEDUI");
        }
#endif

        protected override void GenerateLayoutItems()
        {
            base.GenerateLayoutItems();

#if !CURVEDUI
            Type[] types = control.GetType().Assembly.GetTypes();
            for (int i = 0; i < types.Length; i++)
            {
                if (types[i].Namespace == "CurvedUI")
                {
                    warningLayoutItem.Create("curvedUIWarning", DrawCurvedUIWarning);
                    break;
                }
            }
#endif
        }
    }
}