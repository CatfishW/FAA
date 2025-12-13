/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using UnityEngine;

namespace OnlineMaps
{
    /// <summary>
    /// Class implements the basic functionality control of the 2D map.
    /// </summary>
    [Serializable]
    public abstract class ControlBase2D : ControlBase, ITextureControl
    {
        /// <summary>
        /// Texture, which is used to draw the map. To change this value use SetTexture method.
        /// </summary>
        [SerializeField]
        protected Texture2D _texture;
        
        /// <summary>
        /// Singleton instance of ControlBase2D control.
        /// </summary>
        public new static ControlBase2D instance => _instance as ControlBase2D;

        /// <summary>
        /// Indicates whether it is possible to get the screen coordinates store. True - for 2D map.
        /// </summary>
        public override bool allowMarkerScreenRect => true;

        public override int height => _texture ? _texture.height : 0;

        /// <summary>
        /// Texture, which is used to draw the map.
        /// </summary>
        public Texture2D texture
        {
            get => _texture;
            set => SetTexture(value);
        }
        
        public override int width => _texture ? _texture.width : 0;

        public override void OnAwakeBefore()
        {
            base.OnAwakeBefore();
            
            mapDrawer = new TextureDrawer(this);
            mapDrawer.Initialize();
        }
        
        /// <summary>
        /// Sets the default texture for the map. The texture must be readable.
        /// </summary>
        public void SetDefaultTexture()
        {
            if (!texture) return;
            if (texture.width * texture.height != map.defaultColors.Length) return;
            
            texture.SetPixels(map.defaultColors);
            texture.Apply();
        }

        /// <summary>
        /// Sets the texture for the map. The texture must be readable.
        /// </summary>
        /// <param name="texture">The texture to set.</param>
        public virtual void SetTexture(Texture2D texture)
        {
            _texture = texture;
            map.view.SetSize(texture.width, texture.height);
        }

        public override void UpdateControl()
        {
            base.UpdateControl();

            if (!texture) return;
            
            texture.SetPixels32(map.buffer.frontBuffer);
            texture.Apply(false);
        }
    }
}