/*         INFINITY CODE         */
/*   https://infinity-code.com   */

namespace OnlineMaps
{
    /// <summary>
    /// Interface for managing interactive elements on the map.
    /// </summary>
    public interface IInteractiveElementManager
    {
        /// <summary>
        /// Gets the map associated with the interactive elements.
        /// </summary>
        Map map { get; }

        /// <summary>
        /// Gets the mouse controller for handling mouse interactions.
        /// </summary>
        MouseController mouseController { get; }

        /// <summary>
        /// Gets the index of the specified interactive element.
        /// </summary>
        /// <param name="element">The interactive element to find the index of.</param>
        /// <returns>The index of the interactive element.</returns>
        int IndexOf(IInteractiveElement element);
    }
}