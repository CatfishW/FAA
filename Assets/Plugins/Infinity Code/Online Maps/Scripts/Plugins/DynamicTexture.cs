/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using UnityEngine;

namespace OnlineMaps
{
    /// <summary>
    /// Dynamically creates texture for 2D Controls.
    /// </summary>
    [AddComponentMenu("Infinity Code/Online Maps/Plugins/Dynamic Texture")]
    [Plugin("Dynamic Texture", typeof(ControlBase2D))]
    public class DynamicTexture: MonoBehaviour
    {
        /// <summary>
        /// Width of the texture. Must be 256 * N.
        /// </summary>
        public int width = 512;

        /// <summary>
        /// Height of the texture. Must be 256 * N.
        /// </summary>
        public int height = 512;

        public void Start()
        {
            if (GetComponent<AdjustToScreen>()) return;
            
            ITextureControl textureControl = GetComponent<ITextureControl>();
            if (textureControl == null) throw new Exception("ITextureControl component not found.");

            if (width < 256) throw new Exception("Width must be greater than or equal to 256.");
            if (height < 256) throw new Exception("Height must be greater than or equal to 256.");

            width = Mathf.ClosestPowerOfTwo(width);
            height = Mathf.ClosestPowerOfTwo(height);

            Texture2D texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            texture.name = "Dynamic Map Texture";
            textureControl.SetTexture(texture);
        }
    }
}