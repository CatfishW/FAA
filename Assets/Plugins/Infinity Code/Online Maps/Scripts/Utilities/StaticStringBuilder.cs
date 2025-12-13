/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System.Text;

namespace OnlineMaps
{
    /// <summary>
    /// Provides a static StringBuilder for reusable string operations.
    /// </summary>
    public static class StaticStringBuilder
    {
        private static StringBuilder builder = new StringBuilder();
    
        /// <summary>
        /// Starts the StringBuilder, optionally clearing its contents.
        /// </summary>
        /// <param name="clear">If true, clears the StringBuilder contents.</param>
        /// <returns>The static StringBuilder instance.</returns>
        public static StringBuilder Start(bool clear = true)
        {
            if (clear) builder.Length = 0;
            return builder;
        }
    }
}