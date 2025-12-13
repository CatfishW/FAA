/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using System.Collections.Generic;
using UnityEngine;

namespace OnlineMaps
{
    /// <summary>
    /// Provides various geometric calculations and utilities.
    /// </summary>
    public static class Geometry
    {
        /// <summary>
        /// Calculates the angle between two Mercator points in degrees.
        /// </summary>
        /// <param name="point1">The first Mercator point.</param>
        /// <param name="point2">The second Mercator point.</param>
        /// <returns>The angle between the two points in degrees.</returns>
        public static double Angle2D(MercatorPoint point1, MercatorPoint point2)
        {
            return Math.Atan2(point2.y - point1.y, point2.x - point1.x) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// Calculates the angle between two geographical points in degrees.
        /// </summary>
        /// <param name="point1">The first geographical point.</param>
        /// <param name="point2">The second geographical point.</param>
        /// <returns>The angle between the two points in degrees.</returns>
        public static double Angle2D(GeoPoint point1, GeoPoint point2)
        {
            return Math.Atan2(point2.y - point1.y, point2.x - point1.x) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// Calculates the angle between two geographical points with altitude in degrees.
        /// </summary>
        /// <param name="point1">The first geographical point with altitude.</param>
        /// <param name="point2">The second geographical point with altitude.</param>
        /// <returns>The angle between the two points in degrees.</returns>
        public static double Angle2D(GeoPoint3 point1, GeoPoint3 point2)
        {
            return Math.Atan2(point2.y - point1.y, point2.x - point1.x) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// The angle between the two points in degree.
        /// </summary>
        /// <param name="point1">Point 1</param>
        /// <param name="point2">Point 2</param>
        /// <returns>Angle in degree</returns>
        public static float Angle2D(Vector2 point1, Vector2 point2)
        {
            return Mathf.Atan2(point2.y - point1.y, point2.x - point1.x) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// Calculates the angle between two 2D points in degrees.
        /// </summary>
        /// <param name="point1">The first 2D point.</param>
        /// <param name="point2">The second 2D point.</param>
        /// <returns>The angle between the two points in degrees.</returns>
        public static double Angle2D(Vector2d point1, Vector2d point2)
        {
            return Math.Atan2(point2.y - point1.y, point2.x - point1.x) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// The angle between the two points in degree.
        /// </summary>
        /// <param name="point1">Point 1</param>
        /// <param name="point2">Point 2</param>
        /// <returns>Angle in degree</returns>
        public static float Angle2D(Vector3 point1, Vector3 point2)
        {
            return Mathf.Atan2(point2.z - point1.z, point2.x - point1.x) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// The angle between the two points in degree.
        /// </summary>
        /// <param name="p1x">Point 1 X</param>
        /// <param name="p1y">Point 1 Y</param>
        /// <param name="p2x">Point 2 X</param>
        /// <param name="p2y">Point 2 Y</param>
        /// <returns>Angle in degree</returns>
        public static double Angle2D(double p1x, double p1y, double p2x, double p2y)
        {
            return Math.Atan2(p2y - p1y, p2x - p1x) * Constants.Rad2Deg;
        }

        /// <summary>
        /// The angle between the three points in degree.
        /// </summary>
        /// <param name="point1">Point 1</param>
        /// <param name="point2">Point 2</param>
        /// <param name="point3">Point 3</param>
        /// <param name="unsigned">Return a positive result.</param>
        /// <returns>Angle in degree</returns>
        public static float Angle2D(Vector3 point1, Vector3 point2, Vector3 point3, bool unsigned = true)
        {
            float angle1 = Angle2D(point1, point2);
            float angle2 = Angle2D(point2, point3);
            float angle = angle1 - angle2;
            if (angle > 180) angle -= 360;
            if (angle < -180) angle += 360;
            if (unsigned) angle = Mathf.Abs(angle);
            return angle;
        }

        /// <summary>
        /// The angle between the two points in radians.
        /// </summary>
        /// <param name="point1">Point 1</param>
        /// <param name="point2">Point 2</param>
        /// <param name="offset">Result offset in degrees.</param>
        /// <returns>Angle in radians</returns>
        public static float Angle2DRad(Vector3 point1, Vector3 point2, float offset = 0)
        {
            return Mathf.Atan2(point2.z - point1.z, point2.x - point1.x) + offset * Mathf.Deg2Rad;
        }

        /// <summary>
        /// The angle between the two points in radians.
        /// </summary>
        /// <param name="p1x">Point 1 X</param>
        /// <param name="p1z">Point 1 Z</param>
        /// <param name="p2x">Point 2 X</param>
        /// <param name="p2z">Point 2 Z</param>
        /// <param name="offset">Result offset in degrees.</param>
        /// <returns>Angle in radians</returns>
        public static float Angle2DRad(float p1x, float p1z, float p2x, float p2z, float offset = 0)
        {
            return Mathf.Atan2(p2z - p1z, p2x - p1x) + offset * Mathf.Deg2Rad;
        }

        /// <summary>
        /// Calculates the angle of a triangle in radians.
        /// </summary>
        /// <param name="A">Point A</param>
        /// <param name="B">Point B</param>
        /// <param name="C">Point C</param>
        /// <returns>Angle in radians</returns>
        public static float AngleOfTriangle(Vector2 A, Vector2 B, Vector2 C)
        {
            float a = (B - C).magnitude;
            float b = (A - C).magnitude;
            float c = (A - B).magnitude;

            return Mathf.Acos((a * a + b * b - c * c) / (2 * a * b));
        }

        /// <summary>
        /// Calculates the point of intersection between two line segments defined by the given Vector2 objects.
        /// </summary>
        /// <param name="p1">The first endpoint of the first line segment.</param>
        /// <param name="p2">The second endpoint of the first line segment.</param>
        /// <param name="p3">The first endpoint of the second line segment.</param>
        /// <param name="p4">The second endpoint of the second line segment.</param>
        /// <returns>The point of intersection between the two line segments, or Vector2.zero if they do not intersect.</returns>
        public static Vector2 Crossing(Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4)
        {
            if (Math.Abs(p3.x - p4.x) < float.Epsilon)
            {
                float y = p1.y + (p2.y - p1.y) * (p3.x - p1.x) / (p2.x - p1.x);
                if (y > Mathf.Max(p3.y, p4.y) || y < Mathf.Min(p3.y, p4.y) || y > Mathf.Max(p1.y, p2.y) || y < Mathf.Min(p1.y, p2.y)) return Vector2.zero;
                return new Vector2(p3.x, y);
            }

            float x = p1.x + (p2.x - p1.x) * (p3.y - p1.y) / (p2.y - p1.y);
            if (x > Mathf.Max(p3.x, p4.x) || x < Mathf.Min(p3.x, p4.x) || x > Mathf.Max(p1.x, p2.x) || x < Mathf.Min(p1.x, p2.x)) return Vector2.zero;
            return new Vector2(x, p3.y);
        }

        /// <summary>
        /// Flip the negative dimensions of the rect.
        /// </summary>
        /// <param name="r">Rect.</param>
        public static void FlipNegative(ref Rect r)
        {
            if (r.width < 0) r.x -= r.width *= -1;
            if (r.height < 0) r.y -= r.height *= -1;
        }

        /// <summary>
        /// Calculates the intersection point of two 2D lines defined by two points each, and returns a value indicating the state of the calculation.
        /// </summary>
        /// <param name="p11">The first point on the first line.</param>
        /// <param name="p12">The second point on the first line.</param>
        /// <param name="p21">The first point on the second line.</param>
        /// <param name="p22">The second point on the second line.</param>
        /// <param name="state">The state of the calculation.</param>
        /// <returns>The intersection point of the two lines if it exists, or a Vector2.zero if the lines do not intersect.</returns>
        public static Vector2 GetIntersectionPointOfTwoLines(Vector2 p11, Vector2 p12, Vector2 p21, Vector2 p22, out int state)
        {
            Vector2 result = new Vector2();
            float m = (p22.x - p21.x) * (p11.y - p21.y) - (p22.y - p21.y) * (p11.x - p21.x);
            float n = (p22.y - p21.y) * (p12.x - p11.x) - (p22.x - p21.x) * (p12.y - p11.y);

            float Ua = m / n;

            if (Math.Abs(n) < float.Epsilon && Math.Abs(m) > float.Epsilon) state = -1;
            else if (Math.Abs(m) < float.Epsilon && Math.Abs(n) < float.Epsilon) state = 0;
            else
            {
                result.x = p11.x + Ua * (p12.x - p11.x);
                result.y = p11.y + Ua * (p12.y - p11.y);
                state = 1;
            }

            return result;
        }

        /// <summary>
        /// Calculates the intersection point of two 2D lines defined by four coordinates, and returns a value indicating the state of the calculation.
        /// </summary>
        /// <param name="p11x">The x-coordinate of the first point on the first line.</param>
        /// <param name="p11y">The y-coordinate of the first point on the first line.</param>
        /// <param name="p12x">The x-coordinate of the second point on the first line.</param>
        /// <param name="p12y">The y-coordinate of the second point on the first line.</param>
        /// <param name="p21x">The x-coordinate of the first point on the second line.</param>
        /// <param name="p21y">The y-coordinate of the first point on the second line.</param>
        /// <param name="p22x">The x-coordinate of the second point on the second line.</param>
        /// <param name="p22y">The y-coordinate of the second point on the second line.</param>
        /// <param name="resultx">An output parameter that will contain the x-coordinate of the intersection point if the calculation is successful, otherwise zero.</param>
        /// <param name="resulty">An output parameter that will contain the y-coordinate of the intersection point if the calculation is successful, otherwise zero.</param>
        /// <returns>A value indicating the state of the calculation.</returns>
        public static int GetIntersectionPointOfTwoLines(float p11x, float p11y, float p12x, float p12y, float p21x, float p21y, float p22x, float p22y, out float resultx, out float resulty)
        {
            int state;
            resultx = 0;
            resulty = 0;

            float m = (p22x - p21x) * (p11y - p21y) - (p22y - p21y) * (p11x - p21x);
            float n = (p22y - p21y) * (p12x - p11x) - (p22x - p21x) * (p12y - p11y);

            float Ua = m / n;

            if (Math.Abs(n) < float.Epsilon && Math.Abs(m) > float.Epsilon) state = -1;
            else if (Math.Abs(m) < float.Epsilon && Math.Abs(n) < float.Epsilon) state = 0;
            else
            {
                resultx = p11x + Ua * (p12x - p11x);
                resulty = p11y + Ua * (p12y - p11y);
                state = 1;
            }

            return state;
        }

        /// <summary>
        /// Calculates the intersection point of two 2D lines defined by two points each, and returns a value indicating the state of the calculation.
        /// </summary>
        /// <param name="p11">The first point on the first line.</param>
        /// <param name="p12">The second point on the first line.</param>
        /// <param name="p21">The first point on the second line.</param>
        /// <param name="p22">The second point on the second line.</param>
        /// <param name="state">The state of the calculation.</param>
        /// <returns>The intersection point of the two lines if it exists, or a Vector2.zero if the lines do not intersect.</returns>
        public static Vector2 GetIntersectionPointOfTwoLines(Vector3 p11, Vector3 p12, Vector3 p21, Vector3 p22, out int state)
        {
            return GetIntersectionPointOfTwoLines(new Vector2(p11.x, p11.z), new Vector2(p12.x, p12.z), new Vector2(p21.x, p21.z), new Vector2(p22.x, p22.z), out state);
        }

        /// <summary>
        /// Determines whether two rectangles intersect.
        /// </summary>
        /// <param name="a">The first rectangle.</param>
        /// <param name="b">The second rectangle.</param>
        /// <returns>True if the two rectangles intersect; otherwise, false.</returns>
        public static bool Intersect(Rect a, Rect b)
        {
            FlipNegative(ref a);
            FlipNegative(ref b);
            if (a.xMin >= b.xMax) return false;
            if (a.xMax <= b.xMin) return false;
            if (a.yMin >= b.yMax) return false;
            if (a.yMax <= b.yMin) return false;

            return true;
        }

        /// <summary>
        /// Determines if two line segments intersect and returns the intersection point.
        /// </summary>
        /// <param name="start1">The starting point of the first line segment.</param>
        /// <param name="end1">The ending point of the first line segment.</param>
        /// <param name="start2">The starting point of the second line segment.</param>
        /// <param name="end2">The ending point of the second line segment.</param>
        /// <param name="out_intersection">The intersection point if the line segments intersect, otherwise Vector2.zero.</param>
        /// <returns>True if the line segments intersect, otherwise false.</returns>
        public static bool LineIntersection(Vector2 start1, Vector2 end1, Vector2 start2, Vector2 end2, out Vector2 out_intersection)
        {
            out_intersection = Vector2.zero;

            Vector2 dir1 = end1 - start1;
            Vector2 dir2 = end2 - start2;

            float a1 = -dir1.y;
            float b1 = +dir1.x;
            float d1 = -(a1 * start1.x + b1 * start1.y);

            float a2 = -dir2.y;
            float b2 = +dir2.x;
            float d2 = -(a2 * start2.x + b2 * start2.y);

            float seg1_line2_start = a2 * start1.x + b2 * start1.y + d2;
            float seg1_line2_end = a2 * end1.x + b2 * end1.y + d2;

            float seg2_line1_start = a1 * start2.x + b1 * start2.y + d1;
            float seg2_line1_end = a1 * end2.x + b1 * end2.y + d1;

            if (seg1_line2_start * seg1_line2_end >= 0 || seg2_line1_start * seg2_line1_end >= 0) return false;

            float u = seg1_line2_start / (seg1_line2_start - seg1_line2_end);
            out_intersection = start1 + u * dir1;

            return true;
        }

        /// <summary>
        /// Determines whether a point with the given coordinates is inside the specified polygon.
        /// </summary>
        /// <param name="poly">The list of vertices that define the polygon.</param>
        /// <param name="x">The x-coordinate of the point to check.</param>
        /// <param name="y">The y-coordinate of the point to check.</param>
        /// <returns>true if the point is inside the polygon; otherwise, false.</returns>
        public static bool IsPointInPolygon(List<Vector2> poly, float x, float y)
        {
            int i, j;
            bool c = false;
            for (i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
            {
                if (((poly[i].y <= y && y < poly[j].y) || (poly[j].y <= y && y < poly[i].y)) &&
                    x < (poly[j].x - poly[i].x) * (y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                    c = !c;
            }

            return c;
        }

        /// <summary>
        /// Determines whether a point with the given coordinates is inside the specified polygon.
        /// </summary>
        /// <param name="poly">Array of vertices that define the polygon.</param>
        /// <param name="x">The x-coordinate of the point to check.</param>
        /// <param name="y">The y-coordinate of the point to check.</param>
        /// <returns>true if the point is inside the polygon; otherwise, false.</returns>
        public static bool IsPointInPolygon(double[] poly, double x, double y)
        {
            int i, j;
            bool c = false;
            int l = poly.Length / 2;
            for (i = 0, j = l - 1; i < l; j = i++)
            {
                int i2 = i * 2;
                int j2 = j * 2;
                int i2p = i2 + 1;
                int j2p = j2 + 1;
                if (((poly[i2p] <= y && y < poly[j2p]) || (poly[j2p] <= y && y < poly[i2p])) &&
                    x < (poly[j2] - poly[i2]) * (y - poly[i2p]) / (poly[j2p] - poly[i2p]) + poly[i2])
                    c = !c;
            }

            return c;
        }

        /// <summary>
        /// Determines whether a point is inside a given polygon.
        /// </summary>
        /// <param name="polygon">An array of Mercator points that define the polygon.</param>
        /// <param name="point">The Mercator point to check.</param>
        /// <returns>True if the point is inside the polygon; otherwise, false.</returns>
        public static bool IsPointInPolygon(MercatorPoint[] polygon, MercatorPoint point)
        {
            double x = point.x, y = point.y;
            int i, j;
            bool c = false;
            for (i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            {
                if (((polygon[i].y <= y && y < polygon[j].y) || (polygon[j].y <= y && y < polygon[i].y)) &&
                    x < (polygon[j].x - polygon[i].x) * (y - polygon[i].y) / (polygon[j].y - polygon[i].y) + polygon[i].x)
                    c = !c;
            }

            return c;
        }

        /// <summary>
        /// Determines if two line segments intersect and returns the intersection point.
        /// </summary>
        /// <param name="s1x">The x-coordinate of the starting point of the first line segment.</param>
        /// <param name="s1y">The y-coordinate of the starting point of the first line segment.</param>
        /// <param name="e1x">The x-coordinate of the ending point of the first line segment.</param>
        /// <param name="e1y">The y-coordinate of the ending point of the first line segment.</param>
        /// <param name="s2x">The x-coordinate of the starting point of the second line segment.</param>
        /// <param name="s2y">The y-coordinate of the starting point of the second line segment.</param>
        /// <param name="e2x">The x-coordinate of the ending point of the second line segment.</param>
        /// <param name="e2y">The y-coordinate of the ending point of the second line segment.</param>
        /// <param name="intX">The x-coordinate of the intersection point if the line segments intersect, otherwise 0.</param>
        /// <param name="intY">The y-coordinate of the intersection point if the line segments intersect, otherwise 0.</param>
        /// <returns>True if the line segments intersect, otherwise false.</returns>
        public static bool LineIntersection(float s1x, float s1y, float e1x, float e1y, float s2x, float s2y, float e2x, float e2y, out float intX, out float intY)
        {
            intX = 0;
            intY = 0;

            float dir1x = e1x - s1x;
            float dir1y = e1y - s1y;
            float dir2x = e2x - s2x;
            float dir2y = e2y - s2y;

            float a1 = -dir1y;
            float b1 = +dir1x;
            float d1 = -(a1 * s1x + b1 * s1y);

            float a2 = -dir2y;
            float b2 = +dir2x;
            float d2 = -(a2 * s2x + b2 * s2y);

            float seg1_line2_start = a2 * s1x + b2 * s1y + d2;
            float seg1_line2_end = a2 * e1x + b2 * e1y + d2;

            float seg2_line1_start = a1 * s2x + b1 * s2y + d1;
            float seg2_line1_end = a1 * e2x + b1 * e2y + d1;

            if (seg1_line2_start * seg1_line2_end >= 0 || seg2_line1_start * seg2_line1_end >= 0) return false;

            float u = seg1_line2_start / (seg1_line2_start - seg1_line2_end);
            intX = s1x + u * dir1x;
            intY = s1y + u * dir1y;

            return true;
        }

        ///<summary>
        /// Returns the intersection point of two line segments, represented by their endpoints, or Vector2.zero if the segments do not intersect.
        ///</summary>
        ///<param name="p1">The first endpoint of the first line segment</param>
        ///<param name="p2">The second endpoint of the first line segment</param>
        ///<param name="p3">The first endpoint of the second line segment</param>
        ///<param name="p4">The second endpoint of the second line segment</param>
        ///<returns>The intersection point of the two line segments, or Vector2.zero if the segments do not intersect</returns>
        public static Vector2 LineIntersection(Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4)
        {
            double x1lo, x1hi, y1lo, y1hi;

            double Ax = p2.x - p1.x;
            double Bx = p3.x - p4.x;

            if (Ax < 0)
            {
                x1lo = p2.x;
                x1hi = p1.x;
            }
            else
            {
                x1hi = p2.x;
                x1lo = p1.x;
            }

            if (Bx > 0)
            {
                if (x1hi < p4.x || p3.x < x1lo) return Vector2.zero;
            }
            else
            {
                if (x1hi < p3.x || p4.x < x1lo) return Vector2.zero;
            }

            double Ay = p2.y - p1.y;
            double By = p3.y - p4.y;

            if (Ay < 0)
            {
                y1lo = p2.y;
                y1hi = p1.y;
            }
            else
            {
                y1hi = p2.y;
                y1lo = p1.y;
            }

            if (By > 0)
            {
                if (y1hi < p4.y || p3.y < y1lo) return Vector2.zero;
            }
            else
            {
                if (y1hi < p3.y || p4.y < y1lo) return Vector2.zero;
            }

            double Cx = p1.x - p3.x;
            double Cy = p1.y - p3.y;
            double d = By * Cx - Bx * Cy;
            double f = Ay * Bx - Ax * By;

            if (f > 0)
            {
                if (d < 0 || d > f) return Vector2.zero;
            }
            else
            {
                if (d > 0 || d < f) return Vector2.zero;
            }

            double e = Ax * Cy - Ay * Cx;

            if (f > 0)
            {
                if (e < 0 || e > f) return Vector2.zero;
            }
            else
            {
                if (e > 0 || e < f) return Vector2.zero;
            }

            if (Math.Abs(f) < double.Epsilon) return Vector2.zero;

            Vector2 intersection;

            double num = d * Ax;
            double offset = SameSign(num, f) ? f * 0.5 : -f * 0.5;
            intersection.x = (float)(p1.x + (num + offset) / f);

            num = d * Ay;
            offset = SameSign(num, f) ? f * 0.5 : -f * 0.5;
            intersection.y = (float)(p1.y + (num + offset) / f);

            return intersection;
        }

        /// <summary>
        /// Returns the length of vector.
        /// </summary>
        /// <param name="p1x">Point 1 X</param>
        /// <param name="p1y">Point 1 Y</param>
        /// <param name="p2x">Point 2 X</param>
        /// <param name="p2y">Point 2 Y</param>
        /// <returns>Length of vector</returns>
        public static double Magnitude(double p1x, double p1y, double p2x, double p2y)
        {
            return Math.Sqrt((p2x - p1x) * (p2x - p1x) + (p2y - p1y) * (p2y - p1y));
        }

        /// <summary>
        /// Returns the nearest point on the segment.
        /// </summary>
        /// <param name="point">Point</param>
        /// <param name="lineStart">Start of the segment</param>
        /// <param name="lineEnd">End of segment</param>
        /// <returns>Nearest point</returns>
        public static Vector2d NearestPointStrict(Vector2d point, Vector2d lineStart, Vector2d lineEnd)
        {
            Vector2d fullDirection = lineEnd - lineStart;
            Vector2d lineDirection = fullDirection.normalized;
            double closestPoint = Vector2d.Dot(point - lineStart, lineDirection) / Vector2d.Dot(lineDirection, lineDirection);
            return lineStart + lineDirection * Mathd.Clamp(closestPoint, 0, fullDirection.magnitude);
        }

        /// <summary>
        /// Returns the nearest point on the segment.
        /// </summary>
        /// <param name="pointX">Point X</param>
        /// <param name="pointY">Point Y</param>
        /// <param name="lineStartX">Start X of the segment</param>
        /// <param name="lineStartY">Start Y of the segment</param>
        /// <param name="lineEndX">End X of the segment</param>
        /// <param name="lineEndY">End Y of the segment</param>
        /// <param name="nearestPointX">Nearest point X</param>
        /// <param name="nearestPointY">Nearest point Y</param>
        public static void NearestPointStrict(double pointX, double pointY, double lineStartX, double lineStartY, double lineEndX, double lineEndY, out double nearestPointX, out double nearestPointY)
        {
            double fdX = lineEndX - lineStartX;
            double fdY = lineEndY - lineStartY;
            double magnitude = Math.Sqrt(fdX * fdX + fdY * fdY);
            double ldX = fdX / magnitude;
            double ldY = fdY / magnitude;
            double lx = pointX - lineStartX;
            double ly = pointY - lineStartY;
            double closestPoint = (lx * ldX + ly * ldY) / (ldX * ldX + ldY * ldY);

            if (closestPoint < 0) closestPoint = 0;
            else if (closestPoint > magnitude) closestPoint = magnitude;

            nearestPointX = lineStartX + closestPoint * ldX;
            nearestPointY = lineStartY + closestPoint * ldY;
        }

        /// <summary>
        /// Returns true if both values have the same sign
        /// </summary>
        /// <param name="a">First value</param>
        /// <param name="b">Second value</param>
        /// <returns>True if both values have the same sign, false otherwise</returns>
        private static bool SameSign(double a, double b)
        {
            return a * b >= 0f;
        }

        /// <summary>
        /// Returns the square of the magnitude of the segment
        /// </summary>
        /// <param name="p1x">Point 1 X</param>
        /// <param name="p1y">Point 1 Y</param>
        /// <param name="p2x">Point 2 X</param>
        /// <param name="p2y">Point 2 Y</param>
        /// <returns>Square of the magnitude</returns>
        public static double SqrMagnitude(double p1x, double p1y, double p2x, double p2y)
        {
            return (p2x - p1x) * (p2x - p1x) + (p2y - p1y) * (p2y - p1y);
        }

        /// <summary>
        /// Triangulates list of points
        /// </summary>
        /// <param name="points">List of points</param>
        /// <returns>List of vertex numbers</returns>
        public static List<int> Triangulate(List<GeoPoint> points)
        {
            List<int> indices = new List<int>(18);

            int n = points.Count;
            if (n < 3) return indices;

            int[] V = new int[n];
            if (TriangulateArea(points) > 0)
            {
                for (int v = 0; v < n; v++) V[v] = v;
            }
            else
            {
                for (int v = 0; v < n; v++) V[v] = n - 1 - v;
            }

            int nv = n;
            int count = 2 * nv;

            for (int v = nv - 1; nv > 2;)
            {
                if (count-- <= 0) return indices;

                int u = v;
                if (nv <= u) u = 0;
                v = u + 1;
                if (nv <= v) v = 0;
                int w = v + 1;
                if (nv <= w) w = 0;

                if (!TriangulateSnip(points, u, v, w, nv, V)) continue;

                int s, t;
                indices.Add(V[u]);
                indices.Add(V[v]);
                indices.Add(V[w]);
                for (s = v, t = v + 1; t < nv; s++, t++) V[s] = V[t];
                nv--;
                count = 2 * nv;
            }

            indices.Reverse();
            return indices;
        }

        /// <summary>
        /// Triangulates points
        /// </summary>
        /// <param name="points">An array of points containing the values [x, y, x, y...]</param>
        /// <param name="countVertices">Number of vertices to be triangulated</param>
        /// <param name="indices">List where vertex indices will be written</param>
        /// <returns>Indices</returns>
        public static IEnumerable<int> Triangulate(float[] points, int countVertices, List<int> indices)
        {
            indices.Clear();

            int n = countVertices;
            if (n < 3) return indices;

            int[] V = new int[n];
            if (TriangulateArea(points, countVertices) > 0)
            {
                for (int v = 0; v < n; v++) V[v] = v;
            }
            else
            {
                for (int v = 0; v < n; v++) V[v] = n - 1 - v;
            }

            int nv = n;
            int count = 2 * nv;

            for (int v = nv - 1; nv > 2;)
            {
                if (count-- <= 0) return indices;

                int u = v;
                if (nv <= u) u = 0;
                v = u + 1;
                if (nv <= v) v = 0;
                int w = v + 1;
                if (nv <= w) w = 0;

                if (!TriangulateSnip(points, u, v, w, nv, V)) continue;

                int s, t;
                indices.Add(V[u]);
                indices.Add(V[v]);
                indices.Add(V[w]);
                for (s = v, t = v + 1; t < nv; s++, t++) V[s] = V[t];
                nv--;
                count = 2 * nv;
            }

            indices.Reverse();
            return indices;
        }

        private static double TriangulateArea(List<GeoPoint> points)
        {
            int n = points.Count;
            double A = 0;
            for (int p = n - 1, q = 0; q < n; p = q++)
            {
                GeoPoint p1 = points[p];
                GeoPoint p2 = points[q];
                A += p1.x * p2.y - p2.x * p1.y;
            }

            return A * 0;
        }

        private static float TriangulateArea(float[] points, int countVertices)
        {
            float A = 0.0f;
            int n = countVertices * 2;
            for (int p = n - 2, q = 0; q < n; p = q - 2)
            {
                float pvx = points[p];
                float pvy = points[p + 1];
                float qvx = points[q++];
                float qvy = points[q++];

                A += pvx * qvy - qvx * pvy;
            }

            return A * 0.5f;
        }

        private static bool TriangulateInsideTriangle(GeoPoint a, GeoPoint b, GeoPoint c, GeoPoint p)
        {
            double bp = (c.x - b.x) * (p.y - b.y) - (c.y - b.y) * (p.x - b.x);
            double ap = (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);
            double cp = (a.x - c.x) * (p.y - c.y) - (a.y - c.y) * (p.x - c.x);
            return bp >= 0.0f && cp >= 0.0f && ap >= 0.0f;
        }

        private static bool TriangulateInsideTriangle(float ax, float ay, float bx, float by, float cx, float cy, float px, float py)
        {
            float bp = (cx - bx) * (py - by) - (cy - by) * (px - bx);
            float ap = (bx - ax) * (py - ay) - (by - ay) * (px - ax);
            float cp = (ax - cx) * (py - cy) - (ay - cy) * (px - cx);
            return bp >= 0.0f && cp >= 0.0f && ap >= 0.0f;
        }

        private static bool TriangulateSnip(List<GeoPoint> points, int u, int v, int w, int n, int[] V)
        {
            Vector2 A = points[V[u]];
            Vector2 B = points[V[v]];
            Vector2 C = points[V[w]];
            if (Mathf.Epsilon > (B.x - A.x) * (C.y - A.y) - (B.y - A.y) * (C.x - A.x)) return false;
            for (int p = 0; p < n; p++)
            {
                if (p == u || p == v || p == w) continue;
                if (TriangulateInsideTriangle(A, B, C, points[V[p]])) return false;
            }

            return true;
        }

        private static bool TriangulateSnip(float[] points, int u, int v, int w, int n, int[] V)
        {
            int iu = V[u] * 2;
            int iv = V[v] * 2;
            int iw = V[w] * 2;

            float ax = points[iu];
            float ay = points[iu + 1];
            float bx = points[iv];
            float by = points[iv + 1];
            float cx = points[iw];
            float cy = points[iw + 1];

            if (Mathf.Epsilon > (bx - ax) * (cy - ay) - (by - ay) * (cx - ax)) return false;

            for (int p = 0; p < n; p++)
            {
                if (p == u || p == v || p == w) continue;

                int ip = V[p] * 2;
                if (TriangulateInsideTriangle(ax, ay, bx, by, cx, cy, points[ip], points[ip + 1])) return false;
            }

            return true;
        }
    }
}