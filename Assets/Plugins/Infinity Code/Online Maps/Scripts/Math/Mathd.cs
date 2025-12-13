/*         INFINITY CODE         */
/*   https://infinity-code.com   */

namespace OnlineMaps
{
    /// <summary>
    /// Provides mathematical functions for double precision values.
    /// </summary>
    public static class Mathd
    {
        /// <summary>
        /// Clamps a value between 0 and a maximum double value.
        /// </summary>
        /// <param name="value">The value to clamp.</param>
        /// <param name="max">The maximum value.</param>
        /// <returns>The clamped value.</returns>
        public static double Clamp(double value, double max)
        {
            if (value < 0) return 0;
            if (value > max) return max;
            return value;
        }

        /// <summary>
        /// Clamps a value between a minimum double and maximum double value.
        /// </summary>
        /// <param name="value">Value</param>
        /// <param name="min">Minimum</param>
        /// <param name="max">Maximum</param>
        /// <returns>Value between a minimum and maximum.</returns>
        public static double Clamp(double value, double min, double max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        /// <summary>
        /// Clamps a value between 0 and 1.
        /// </summary>
        /// <param name="value">The value to clamp.</param>
        /// <returns>The clamped value.</returns>
        public static double Clamp01(double value)
        {
            if (value < 0) return 0;
            if (value > 1) return 1;
            return value;
        }

        /// <summary>
        /// Linearly interpolates between two values.
        /// </summary>
        /// <param name="value1">The start value.</param>
        /// <param name="value2">The end value.</param>
        /// <param name="t">The interpolation factor, typically between 0 and 1.</param>
        /// <returns>The interpolated value.</returns>
        public static double Lerp(double value1, float value2, double t)
        {
            return value1 + (value2 - value1) * Clamp01(t);
        }

        /// <summary>
        /// Repeats a value within the range [0, max).
        /// </summary>
        /// <param name="value">The value to repeat.</param>
        /// <param name="max">The maximum value.</param>
        /// <returns>The repeated value within the range [0, max).</returns>
        public static double Repeat(double value, double max)
        {
            return Repeat(value, 0, max);
        }

        /// <summary>
        /// Repeats a value within the range [min, max).
        /// </summary>
        /// <param name="value">The value to repeat.</param>
        /// <param name="min">The minimum value.</param>
        /// <param name="max">The maximum value.</param>
        /// <returns>The repeated value within the range [min, max).</returns>
        public static double Repeat(double value, double min, double max)
        {
            double range = max - min;
            while (value < min) value += range;
            while (value > max) value -= range;
            return value;
        }

        /// <summary>
        /// Repeats a value within the range [0, 1).
        /// </summary>
        /// <param name="value">The value to repeat.</param>
        /// <returns>The repeated value within the range [0, 1).</returns>
        public static double Repeat01(double value)
        {
            while (value < 0) value++;
            while (value > 1) value--;
            return value;
        }
    }
}