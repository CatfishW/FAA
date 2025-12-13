/*         INFINITY CODE         */
/*   https://infinity-code.com   */

namespace OnlineMaps
{
    /// <summary>
    /// Interface for interactive elements
    /// </summary>
    public interface IInteractiveElement
    {
        /// <summary>
        /// Gets or sets the manager of the interactive element.
        /// </summary>
        IInteractiveElementManager manager { get; set; }

        /// <summary>
        /// Gets or sets the value associated with the specified key.
        /// </summary>
        /// <param name="key">The key of the value to get or set.</param>
        /// <returns>The value associated with the specified key.</returns>
        public object this[string key] { get; set; }

        /// <summary>
        /// Destroys the instance of the interactive element.
        /// </summary>
        void DestroyInstance();

        /// <summary>
        /// Dispose the current interactive item.
        /// </summary>
        void Dispose();
    }
}