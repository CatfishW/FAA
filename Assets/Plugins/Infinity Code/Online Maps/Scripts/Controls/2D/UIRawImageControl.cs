/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using UnityEngine;
using UnityEngine.UI;

namespace OnlineMaps
{
    /// <summary>
    /// Class control the map for the uGUI UI RawImage.
    /// </summary>
    [AddComponentMenu("Infinity Code/Online Maps/Controls/UI RawImage Control")]
    public class UIRawImageControl : ControlBaseUI<RawImage>
    {
        /// <summary>
        /// Singleton instance of UIRawImageControl control.
        /// </summary>
        public new static UIRawImageControl instance
        {
            get { return _instance as UIRawImageControl; }
        }

        public override void SetTexture(Texture2D texture)
        {
            base.SetTexture(texture);
            image.texture = texture;
        }
    }
}