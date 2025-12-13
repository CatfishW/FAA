/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace OnlineMaps
{
    /// <summary>
    /// Double version of Vector2 class.
    /// </summary>
    [Serializable]
    public struct Vector2d
    {
        /// <summary>
        /// X or longitude
        /// </summary>
        [Alias("lng")]
        public double x;

        /// <summary>
        /// Y or latitude
        /// </summary>
        [Alias("lat")]
        public double y;

        /// <summary>
        /// Returns the length of this vector
        /// </summary>
        public double magnitude => Math.Sqrt(x * x + y * y);

        /// <summary>
        /// Returns the squared length of this vector
        /// </summary>
        public double sqrMagnitude => x * x + y * y;

        /// <summary>
        /// Returns a vector with zero magnitude.
        /// </summary>
        public static Vector2d zero => new(0, 0);

        /// <summary>
        /// Returns this vector with a magnitude of 1.
        /// </summary>
        public Vector2d normalized
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                Vector2d v = new Vector2d(x, y);
                v.Normalize();
                return v;
            }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="x">X or longitude</param>
        /// <param name="y">Y or latitude</param>
        public Vector2d(double x, double y)
        {
            this.x = x;
            this.y = y;
        }

        /// <summary>
        /// Calculates the dot product of two vectors.
        /// </summary>
        /// <param name="lhs">The first vector.</param>
        /// <param name="rhs">The second vector.</param>
        /// <returns>The dot product of the two vectors.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Dot(Vector2d lhs, Vector2d rhs)
        {
            return lhs.x * rhs.x + lhs.y * rhs.y;
        }


        /// <summary>
        /// Linearly interpolates between two vectors.
        /// </summary>
        /// <param name="a">The start vector.</param>
        /// <param name="b">The end vector.</param>
        /// <param name="t">The interpolation factor, typically between 0 and 1.</param>
        /// <returns>The interpolated vector.</returns>
        public static Vector2d Lerp(Vector2d a, Vector2d b, double t)
        {
            if (t < 0) t = 0;
            else if (t > 1) t = 1;
            return new Vector2d(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t);
        }

        /// <summary>
        /// Normalizes this vector.
        /// </summary>
        public void Normalize()
        {
            double m = magnitude;
            if (m > 9.999999747378752E-06) this /= m;
            else this = zero;
        }

        /// <summary>
        /// Returns the hash code for this vector.
        /// </summary>
        /// <returns>The hash code for this vector.</returns>
        public override int GetHashCode()
        {
            return x.GetHashCode() ^ y.GetHashCode() << 2;
        }

        /// <summary>
        /// Determines whether the specified object is equal to the current object.
        /// </summary>
        /// <param name="other">The object to compare with the current object.</param>
        /// <returns>true if the specified object is equal to the current object; otherwise, false.</returns>
        public override bool Equals(object other)
        {
            if (!(other is Vector2d)) return false;
            Vector2d vector2 = (Vector2d)other;
            return x.Equals(vector2.x) && y.Equals(vector2.y);
        }

        /// <summary>
        /// Returns a string that represents the current object.
        /// </summary>
        /// <returns>A string that represents the current object.</returns>
        public override string ToString()
        {
            return "(" + x.ToString(Culture.numberFormat) + ", " + y.ToString(Culture.numberFormat) + ")";
        }

        /// <summary>
        /// Implicitly converts a Vector2d to a Vector2.
        /// </summary>
        /// <param name="val">The Vector2d to convert.</param>
        public static implicit operator Vector2(Vector2d val)
        {
            return new Vector2((float)val.x, (float)val.y);
        }

        /// <summary>
        /// Implicitly converts a Vector2 to a Vector2d.
        /// </summary>
        /// <param name="vector">The Vector2 to convert.</param>
        public static implicit operator Vector2d(Vector2 vector)
        {
            return new Vector2d(vector.x, vector.y);
        }

        /// <summary>
        /// Subtracts one Vector2d from another.
        /// </summary>
        /// <param name="v1">The first vector.</param>
        /// <param name="v2">The second vector.</param>
        /// <returns>The result of the subtraction.</returns>
        public static Vector2d operator -(Vector2d v1, Vector2d v2)
        {
            return new Vector2d(v1.x - v2.x, v1.y - v2.y);
        }

        /// <summary>
        /// Subtracts a Vector2 from a Vector2d.
        /// </summary>
        /// <param name="v1">The Vector2d.</param>
        /// <param name="v2">The Vector2.</param>
        /// <returns>The result of the subtraction.</returns>
        public static Vector2d operator -(Vector2d v1, Vector2 v2)
        {
            return new Vector2d(v1.x - v2.x, v1.y - v2.y);
        }

        /// <summary>
        /// Subtracts a Vector2d from a Vector2.
        /// </summary>
        /// <param name="v1">The Vector2.</param>
        /// <param name="v2">The Vector2d.</param>
        /// <returns>The result of the subtraction.</returns>
        public static Vector2d operator -(Vector2 v1, Vector2d v2)
        {
            return new Vector2d(v1.x - v2.x, v1.y - v2.y);
        }

        /// <summary>
        /// Adds two Vector2d instances.
        /// </summary>
        /// <param name="v1">The first vector.</param>
        /// <param name="v2">The second vector.</param>
        /// <returns>The result of the addition.</returns>
        public static Vector2d operator +(Vector2d v1, Vector2d v2)
        {
            return new Vector2d(v1.x + v2.x, v1.y + v2.y);
        }

        /// <summary>
        /// Adds a Vector2 to a Vector2d.
        /// </summary>
        /// <param name="v1">The Vector2d.</param>
        /// <param name="v2">The Vector2.</param>
        /// <returns>The result of the addition.</returns>
        public static Vector2d operator +(Vector2d v1, Vector2 v2)
        {
            return new Vector2d(v1.x + v2.x, v1.y + v2.y);
        }

        /// <summary>
        /// Adds a Vector2d to a Vector2.
        /// </summary>
        /// <param name="v1">The Vector2.</param>
        /// <param name="v2">The Vector2d.</param>
        /// <returns>The result of the addition.</returns>
        public static Vector2d operator +(Vector2 v1, Vector2d v2)
        {
            return new Vector2d(v1.x + v2.x, v1.y + v2.y);
        }

        /// <summary>
        /// Determines whether two Vector2d instances are equal.
        /// </summary>
        /// <param name="lhs">The first vector.</param>
        /// <param name="rhs">The second vector.</param>
        /// <returns>true if the vectors are equal; otherwise, false.</returns>
        public static bool operator ==(Vector2d lhs, Vector2d rhs)
        {
            return (lhs - rhs).sqrMagnitude < double.Epsilon;
        }

        /// <summary>
        /// Determines whether two Vector2d instances are not equal.
        /// </summary>
        /// <param name="lhs">The first vector.</param>
        /// <param name="rhs">The second vector.</param>
        /// <returns>true if the vectors are not equal; otherwise, false.</returns>
        public static bool operator !=(Vector2d lhs, Vector2d rhs)
        {
            return (lhs - rhs).sqrMagnitude >= double.Epsilon;
        }

        /// <summary>
        /// Multiplies a Vector2d by a scalar.
        /// </summary>
        /// <param name="a">The vector.</param>
        /// <param name="d">The scalar.</param>
        /// <returns>The result of the multiplication.</returns>
        public static Vector2d operator *(Vector2d a, double d)
        {
            return new Vector2d(a.x * d, a.y * d);
        }

        /// <summary>
        /// Divides a Vector2d by a scalar.
        /// </summary>
        /// <param name="a">The vector.</param>
        /// <param name="d">The scalar.</param>
        /// <returns>The result of the division.</returns>
        public static Vector2d operator /(Vector2d a, double d)
        {
            return new Vector2d(a.x / d, a.y / d);
        }
    }
}