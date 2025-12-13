/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;

namespace OnlineMaps
{
    /// <summary>
    /// Alias of field used during deserialization.
    /// </summary>
    public class AliasAttribute : Attribute
    {
        /// <summary>
        /// Aliases
        /// </summary>
        public readonly string[] aliases;

        /// <summary>
        /// If true, the original field name will be ignored.
        /// </summary>
        public readonly bool ignoreFieldName;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="ignoreFieldName">If true, the original field name will be ignored.</param>
        /// <param name="aliases">Aliases</param>
        public AliasAttribute(bool ignoreFieldName, params string[] aliases)
        {
            if (aliases == null || aliases.Length == 0) throw new Exception("You must use at least one alias.");

            this.ignoreFieldName = ignoreFieldName;
            this.aliases = aliases;
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="aliases">Aliases</param>
        public AliasAttribute(params string[] aliases) : this(false, aliases)
        {
            
        }
    }
}