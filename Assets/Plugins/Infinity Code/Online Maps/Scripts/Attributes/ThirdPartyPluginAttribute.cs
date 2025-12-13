/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;

namespace OnlineMaps
{
    /// <summary>
    /// Attribute to mark third-party plugins.
    /// </summary>
    public class ThirdPartyPluginAttribute : PluginAttribute
    {
        /// <summary>
        /// Initializes a new instance of the ThirdPartyPluginAttribute class.
        /// </summary>
        /// <param name="title">The title of the plugin.</param>
        /// <param name="requiredType">The required type for the plugin.</param>
        /// <param name="enabledByDefault">Indicates whether the plugin is enabled by default.</param>
        public ThirdPartyPluginAttribute(string title, Type requiredType, bool enabledByDefault = false) : base(title, requiredType, enabledByDefault)
        {
        }

        /// <summary>
        /// Initializes a new instance of the ThirdPartyPluginAttribute class.
        /// </summary>
        /// <param name="title">The title of the plugin.</param>
        /// <param name="requiredType">The required type for the plugin.</param>
        /// <param name="group">The group to which the plugin belongs.</param>
        public ThirdPartyPluginAttribute(string title, Type requiredType, string group) : base(title, requiredType, group)
        {
        }
    }
}