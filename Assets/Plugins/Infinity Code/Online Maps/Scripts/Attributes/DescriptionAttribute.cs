/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;

namespace OnlineMaps
{
    /// <summary>
    /// Attribute for description of the field.
    /// </summary>
    public class DescriptionAttribute : Attribute
    {
        private string name;

        /// <summary>
        /// Description of the field.
        /// </summary>
        public string description => name;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="name">Description of the field.</param>
        public DescriptionAttribute(string name)
        {
            this.name = name;
        }
    }
}