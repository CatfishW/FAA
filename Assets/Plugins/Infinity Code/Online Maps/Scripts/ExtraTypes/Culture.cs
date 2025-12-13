/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System.Globalization;

namespace OnlineMaps
{
    public static class Culture
    {
        /// <summary>
        /// Gets the CultureInfo object that is culture-independent (invariant).
        /// </summary>
        public static CultureInfo cultureInfo => CultureInfo.InvariantCulture;

        /// <summary>
        /// Gets the NumberFormatInfo object that is culture-independent (invariant).
        /// </summary>
        public static NumberFormatInfo numberFormat => cultureInfo.NumberFormat;
    }
}