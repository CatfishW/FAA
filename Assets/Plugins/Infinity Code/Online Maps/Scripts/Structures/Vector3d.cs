/*         INFINITY CODE         */
/*   https://infinity-code.com   */

namespace OnlineMaps
{
    /// <summary>
    /// Represents a 3-dimensional vector with double precision.
    /// </summary>
    public struct Vector3d
    {
        /// <summary>
        /// The x component of the vector.
        /// </summary>
        public double x;
    
        /// <summary>
        /// The y component of the vector.
        /// </summary>
        public double y;
    
        /// <summary>
        /// The z component of the vector.
        /// </summary>
        public double z;
    
        /// <summary>
        /// Initializes a new instance of the Vector3d struct with the specified components.
        /// </summary>
        /// <param name="x">The x component of the vector.</param>
        /// <param name="y">The y component of the vector.</param>
        /// <param name="z">The z component of the vector.</param>
        public Vector3d(double x, double y, double z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public override string ToString()
        {
            return "(" + x.ToString(Culture.numberFormat) + ", " + y.ToString(Culture.numberFormat) + ", " + z.ToString(Culture.numberFormat) + ")";
        }
    }
}