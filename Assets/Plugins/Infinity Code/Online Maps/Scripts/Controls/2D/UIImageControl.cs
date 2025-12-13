/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using UnityEngine;
using UnityEngine.UI;

namespace OnlineMaps
{
    /// <summary>
    /// Class control the map for the uGUI UI Image.
    /// </summary>
    [AddComponentMenu("Infinity Code/Online Maps/Controls/UI Image Control")]
    public class UIImageControl : ControlBaseUI<Image>
    {
        /// <summary>
        /// Singleton instance of UIImageControl control.
        /// </summary>
        public new static UIImageControl instance => _instance as UIImageControl;

        public override void SetTexture(Texture2D texture)
        {
            base.SetTexture(texture);
            
            if (!Utils.isPlaying) return;
            image.sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.zero);
        }
    }
}