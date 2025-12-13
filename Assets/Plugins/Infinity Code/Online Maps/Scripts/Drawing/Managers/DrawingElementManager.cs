/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using UnityEngine;

namespace OnlineMaps
{
    /// <summary>
    /// This component manages drawing elements.
    /// </summary>
    [AddComponentMenu("")]
    public class DrawingElementManager: InteractiveElementManager<DrawingElementManager, DrawingElement>
    {
        protected override void OnEnable()
        {
            base.OnEnable();

            _instance = this;
        }
    }
}