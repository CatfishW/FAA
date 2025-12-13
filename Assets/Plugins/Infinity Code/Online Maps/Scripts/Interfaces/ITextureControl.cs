/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using UnityEngine;

namespace OnlineMaps
{
    /// <summary>
    /// Interface for texture control.
    /// </summary>
    public interface ITextureControl : IControl
    {
        /// <summary>
        /// Gets the height of the texture.
        /// </summary>
        int height { get; }

        /// <summary>
        /// Gets or sets the map associated with the texture control.
        /// </summary>
        Map map { get; }

        /// <summary>
        /// Gets or sets the texture.
        /// </summary>
        Texture2D texture { get; set; }

        /// <summary>
        /// Gets the width of the texture.
        /// </summary>
        int width { get; }

        /// <summary>
        /// Sets the default texture.
        /// </summary>
        void SetDefaultTexture();

        /// <summary>
        /// Sets the specified texture.
        /// </summary>
        /// <param name="texture">The texture to set.</param>
        void SetTexture(Texture2D texture);
    }
}