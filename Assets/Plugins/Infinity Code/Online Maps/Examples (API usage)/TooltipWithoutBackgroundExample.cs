/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using OnlineMaps;
using UnityEngine;

namespace OnlineMapsExamples
{
    /// <summary>
    /// Example of how to draw tooltip without a background.
    /// </summary>
    [AddComponentMenu(Utils.ExampleMenuPath + "TooltipWithoutBackgroundExample")]
    public class TooltipWithoutBackgroundExample : MonoBehaviour
    {
        private void Start()
        {
            // Subscribe to the event preparation of tooltip style.
            GUITooltipDrawer.OnPrepareTooltipStyle += OnPrepareTooltipStyle;
        }

        private void OnPrepareTooltipStyle(ref GUIStyle style)
        {
            // Hide background.
            style.normal.background = null;
        }
    }
}