/*         INFINITY CODE         */
/*   https://infinity-code.com   */

namespace OnlineMaps
{
    public interface ISavableAdvanced : ISavable
    {
        /// <summary>
        /// Returns an array of items to save
        /// </summary>
        /// <returns>Array of items to save</returns>
        SavableItem[] GetSavableItems();
    }
}