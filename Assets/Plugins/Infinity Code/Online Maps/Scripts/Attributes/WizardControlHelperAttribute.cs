/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;

namespace OnlineMaps
{
    /// <summary>
    /// Declares a type of control in the wizard.
    /// </summary>
    public class WizardControlHelperAttribute : Attribute
    {
        /// <summary>
        /// Result type
        /// </summary>
        public MapTarget resultType;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="resultType">Result type</param>
        public WizardControlHelperAttribute(MapTarget resultType)
        {
            this.resultType = resultType;
        }
    }
}