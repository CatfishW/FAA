/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;

namespace OnlineMaps
{
    /// <summary>
    /// Class of range.
    /// </summary>
    [Serializable]
    public class LimitedRange
    {
        /// <summary>
        /// Maximum value.
        /// </summary>
        public float max = float.MaxValue;

        /// <summary>
        /// Minimum value.
        /// </summary>
        public float min = float.MinValue;

        /// <summary>
        /// Maximum limit.
        /// Uses in inspector.
        /// </summary>
        public float maxLimit = Constants.MaxZoomExt;

        /// <summary>
        /// Minimum limit.
        /// Uses in inspector.
        /// </summary>
        public float minLimit = Constants.MinZoom;

        /// <summary>
        /// Default constructor.
        /// </summary>
        public LimitedRange()
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="min">Minimum value.</param>
        /// <param name="max">Maximum value.</param>
        /// <param name="minLimit">Minimum limit.</param>
        /// <param name="maxLimit">Maximum limit.</param>
        public LimitedRange(float min = float.MinValue, float max = float.MaxValue, float minLimit = Constants.MinZoom, float maxLimit = Constants.MaxZoomExt)
        {
            this.min = min;
            this.max = max;
            this.maxLimit = maxLimit;
            this.minLimit = minLimit;
        }

        /// <summary>
        /// Checks and limits value.
        /// </summary>
        /// <param name="value">Value</param>
        /// <returns>Value corresponding to the specified range.</returns>
        public float CheckAndFix(float value)
        {
            if (value < min) value = min;
            if (value > max) value = max;
            return value;
        }
        
        
        /// <summary>
        /// Checks if the specified value is within the range [min, max].
        /// </summary>
        /// <param name="value">The value to check.</param>
        /// <returns>True if the value is within the range; otherwise, false.</returns>
        public bool Contains(float value)
        {
            return value >= min && value <= max;
        }

        /// <summary>
        /// Checks whether the number in the range.
        /// </summary>
        /// <param name="value">Value</param>
        /// <returns>True - if the number is in the range, false - if not.</returns>
        [Obsolete("Use Contains method instead.")]
        public bool InRange(float value)
        {
            return value >= min && value <= max;
        }

        /// <summary>
        /// Converts a range to string.
        /// </summary>
        /// <returns>String</returns>
        public override string ToString()
        {
            return string.Format("Min: {0}, Max: {1}", min, max);
        }

        /// <summary>
        /// Updates the minimum and maximum values.
        /// </summary>
        /// <param name="newMin">Minimum value.</param>
        /// <param name="newMax">Maximum value.</param>
        /// <returns>True - if the range is changed, false - if not changed.</returns>
        public bool Update(float newMin, float newMax)
        {
            bool changed = false;
            if (Math.Abs(newMin - min) > float.Epsilon)
            {
                min = newMin;
                changed = true;
            }

            if (Math.Abs(newMax - max) > float.Epsilon)
            {
                max = newMax;
                changed = true;
            }

            return changed;
        }
    }
}