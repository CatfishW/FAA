/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using OnlineMaps;
using UnityEngine;

namespace OnlineMapsExamples
{
    /// <summary>
    /// Example of how to intercept preparation of style for drawing tooltips.
    /// </summary>
    [AddComponentMenu(Utils.ExampleMenuPath + "ModifyTooltipStyleExample")]
    public class ModifyTooltipStyleExample : MonoBehaviour
    {
        private void Start()
        {
            // Subscribe to the event preparation of tooltip style.
            GUITooltipDrawer.OnPrepareTooltipStyle += OnPrepareTooltipStyle;
        }

        private void OnPrepareTooltipStyle(ref GUIStyle style)
        {
            // Change the style settings.
            style.fontSize = Screen.width / 50;
        }
    }
}
