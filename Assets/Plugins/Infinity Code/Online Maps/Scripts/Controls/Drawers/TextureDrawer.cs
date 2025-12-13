/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using UnityEngine;

namespace OnlineMaps
{
    /// <summary>
    /// Draws the map on the texture.
    /// </summary>
    public class TextureDrawer : MapDrawer
    {
        private ITextureControl control;
        
        private Collider _collider;
        
        public Collider collider
        {
            get
            {
                if (!_collider) _collider = (control as MonoBehaviour).GetComponent<Collider>();
                return _collider;
            }
        }
        
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="control">The control to draw the texture on.</param>
        public TextureDrawer(ITextureControl control)
        {
            this.control = control;
            map = control.map;
        }

        public override void Dispose()
        {
            control = null;
            map = null;
        }

        public override void Draw()
        {
            
        }

        public override void DrawElements()
        {
            
        }

        public override void Initialize()
        {
            
        }

        public override bool Raycast(Ray ray, out RaycastHit hit, int maxRaycastDistance)
        {
            hit = new RaycastHit();
            return collider && collider.Raycast(ray, out hit, maxRaycastDistance);
        }
    }
}