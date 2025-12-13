/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace OnlineMaps
{
    /// <summary>
    /// Represents a geographic point with longitude and latitude.
    /// </summary>
    [Serializable]
    public struct GeoPoint
    {
        /// <summary>
        /// Longitude of the geographic point.
        /// </summary>
        [Alias("lng")]
        public double x;

        /// <summary>
        /// Latitude of the geographic point.
        /// </summary>
        [Alias("lat")]
        public double y;

        /// <summary>
        /// Indicates whether the geographic point is at the origin (0, 0).
        /// </summary>
        public bool isZero => x == 0 && y == 0;

        /// <summary>
        /// Gets or sets the longitude of the geographic point.
        /// </summary>
        public double longitude
        {
            get => x;
            set => x = value;
        }

        /// <summary>
        /// Gets or sets the latitude of the geographic point.
        /// </summary>
        public double latitude
        {
            get => y;
            set => y = value;
        }

        /// <summary>
        /// Gets the magnitude of the geographic point.
        /// </summary>
        public double magnitude => Math.Sqrt(longitude * longitude + latitude * longitude);

        /// <summary>
        /// Gets a geographic point at the origin (0, 0).
        /// </summary>
        public static GeoPoint zero => new(0, 0);

        /// <summary>
        /// Initializes a new instance of the GeoPoint struct with the specified longitude and latitude.
        /// </summary>
        /// <param name="longitude">The longitude of the geographic point.</param>
        /// <param name="latitude">The latitude of the geographic point.</param>
        public GeoPoint(double longitude, double latitude)
        {
            x = longitude;
            y = latitude;
        }

        /// <summary>
        /// Adds the specified delta values to the longitude and latitude of the geographic point.
        /// </summary>
        /// <param name="deltaX">The delta value to add to the longitude.</param>
        /// <param name="deltaY">The delta value to add to the latitude.</param>
        public void Add(double deltaX, double deltaY)
        {
            x += deltaX;
            y += deltaY;
        }

        /// <summary>
        /// Calculates the angle from the origin (0, 0) to this geographic point.
        /// </summary>
        /// <returns>The angle in degrees.</returns>
        public double Angle() => Geometry.Angle2D(zero, this);

        /// <summary>
        /// Calculates the angle from this geographic point to another geographic point.
        /// </summary>
        /// <param name="p">The other geographic point.</param>
        /// <returns>The angle in degrees.</returns>
        public double Angle(GeoPoint p) => Geometry.Angle2D(this, p);

        /// <summary>
        /// Calculates the angle from this geographic point to another geographic point in Mercator projection.
        /// </summary>
        /// <param name="map">The map</param>
        /// <param name="p">The other geographic point.</param>
        /// <returns>The angle in degrees.</returns>
        public double AngleInMercator(Map map, GeoPoint p)
        {
            MercatorPoint mp1 = ToMercator(map);
            MercatorPoint mp2 = p.ToMercator(map);

            double o = mp2.x - mp1.x;
            if (Math.Abs(o) > 0.5) mp1.x += Math.Sign(o);
            
            return Geometry.Angle2D(mp1, mp2);
        }

        /// <summary>
        /// Calculates the distance from this geographic point to another geographic point.
        /// </summary>
        /// <param name="p">The other geographic point.</param>
        /// <returns>The distance in meters.</returns>
        public double Distance(GeoPoint p) => GeoMath.Distance(this, p);

        /// <summary>
        /// Calculates the distance between two geographic points.
        /// </summary>
        /// <param name="p1">The first geographic point.</param>
        /// <param name="p2">The second geographic point.</param>
        /// <returns>The distance in meters.</returns>
        public static double Distance(GeoPoint p1, GeoPoint p2) => GeoMath.Distance(p1, p2);

        /// <summary>
        /// Calculates a distant geographic point from this point given a distance and angle.
        /// </summary>
        /// <param name="distance">The distance in meters.</param>
        /// <param name="angle">The angle in degrees.</param>
        /// <returns>A new GeoPoint representing the distant coordinate.</returns>
        public GeoPoint Distant(double distance, double angle) => GeoMath.DistantLocation(this, distance, angle);

        /// <summary>
        /// Determines whether the specified object is equal to the current object.
        /// </summary>
        /// <param name="obj">The object to compare with the current object.</param>
        /// <returns>true if the specified object is equal to the current object; otherwise, false.</returns>
        public override bool Equals(object obj) => obj is GeoPoint other && Equals(other);

        /// <summary>
        /// Determines whether the specified GeoPoint is equal to the current GeoPoint.
        /// </summary>
        /// <param name="other">The GeoPoint to compare with the current GeoPoint.</param>
        /// <returns>true if the specified GeoPoint is equal to the current GeoPoint; otherwise, false.</returns>
        public bool Equals(GeoPoint other) => x.Equals(other.x) && y.Equals(other.y);
        
        /// <summary>
        /// Converts an enumerable collection of geographic points to an array of GeoPoint.
        /// </summary>
        /// <param name="map">The map to use for the conversion.</param>
        /// <param name="collection">The collection of geographic points.</param>
        /// <returns>An array of GeoPoint representing the converted coordinates.</returns>
        public static GeoPoint[] FromEnumerable(Map map, IEnumerable collection)
        {
            List<GeoPoint> list = new List<GeoPoint>();
            int valueType = -1; // 0 - Vector2, 1 - Vector2d, 2 - GeoPoint, 3 - TilePoint, 4 - MercatorPoint, 5 - float, 6 - double
            object v1 = null;
            int i = -1;
            GeoPoint gp = new GeoPoint();
            
            foreach (object p in collection)
            {
                if (valueType == -1) valueType = GetValueType(p);
                i++;
                
                if (valueType == 0)
                {
                    Vector2 tp = (Vector2)p;
                    gp = new GeoPoint(tp.x, tp.y);
                }
                else if (valueType == 1)
                {
                    Vector2d tp = (Vector2d)p;
                    gp = new GeoPoint(tp.x, tp.y);
                }
                else if (valueType == 2)
                {
                    gp = (GeoPoint)p;
                }
                else if (valueType == 3)
                {
                    TilePoint tp = (TilePoint)p;
                    gp = tp.ToLocation(map);
                }
                else if (valueType == 4)
                {
                    MercatorPoint mp = (MercatorPoint)p;
                    gp = mp.ToLocation(map);
                }
                else 
                {
                    if (i % 2 == 1)
                    {
                        if (valueType == 5)
                        {
                            gp = new GeoPoint((float)v1, (float)p);
                        }
                        else if (valueType == 6)
                        {
                            gp = new GeoPoint((double)v1, (double)p);
                        }
                    }
                    else
                    {
                        v1 = p;
                        continue;
                    }
                }
                
                list.Add(gp);
            }
            
            return list.ToArray();
        }

        /// <summary>
        /// Serves as the default hash function.
        /// </summary>
        /// <returns>A hash code for the current object.</returns>
        public override int GetHashCode() => HashCode.Combine(x, y);
        
        private static int GetValueType(object p)
        {
            return p switch
            {
                Vector2 => 0,
                Vector2d => 1,
                GeoPoint => 2,
                TilePoint => 3,
                MercatorPoint => 4,
                float => 5,
                double => 6,
                _ => -1
            };
        }

        /// <summary>
        /// Creates a new GeoPoint with the specified latitude and longitude.
        /// </summary>
        /// <param name="latitude">The latitude of the geographic point.</param>
        /// <param name="longitude">The longitude of the geographic point.</param>
        /// <returns>A new GeoPoint with the specified latitude and longitude.</returns>
        public static GeoPoint LatLng(double latitude, double longitude) => new(longitude, latitude);

        /// <summary>
        /// Linearly interpolates between this GeoPoint and another GeoPoint.
        /// </summary>
        /// <param name="p">The target GeoPoint.</param>
        /// <param name="t">The interpolation factor, typically between 0 and 1.</param>
        /// <returns>A new GeoPoint that is the result of the linear interpolation.</returns>
        public GeoPoint Lerp(GeoPoint p, double t)
        {
            if (t < 0) t = 0;
            else if (t > 1) t = 1;
            return new GeoPoint(x + (p.x - x) * t, y + (p.y - y) * t);
        }

        /// <summary>
        /// Linearly interpolates between two GeoPoints.
        /// </summary>
        /// <param name="p1">The first GeoPoint.</param>
        /// <param name="p2">The second GeoPoint.</param>
        /// <param name="t">The interpolation factor, typically between 0 and 1.</param>
        /// <returns>A new GeoPoint that is the result of the linear interpolation.</returns>
        public static GeoPoint Lerp(GeoPoint p1, GeoPoint p2, double t)
        {
            if (t < 0) t = 0;
            else if (t > 1) t = 1;
            return new GeoPoint(p1.x + (p2.x - p1.x) * t, p1.y + (p2.y - p1.y) * t);
        }

        /// <summary>
        /// Gets the squared magnitude of the geographic point.
        /// </summary>
        /// <returns>The squared magnitude.</returns>
        public double SqrMagnitude() => x * x + y * y;

        /// <summary>
        /// Gets the squared magnitude between this geographic point and another.
        /// </summary>
        /// <param name="p">The other geographic point.</param>
        /// <returns>The squared magnitude.</returns>
        public double SqrMagnitude(GeoPoint p) => (x - p.x) * (x - p.x) + (y - p.y) * (y - p.y);

        /// <summary>
        /// Gets the squared magnitude between two geographic points.
        /// </summary>
        /// <param name="p1">The first geographic point.</param>
        /// <param name="p2">The second geographic point.</param>
        /// <returns>The squared magnitude.</returns>
        public static double SqrMagnitude(GeoPoint p1, GeoPoint p2) => (p1.x - p2.x) * (p1.x - p2.x) + (p1.y - p2.y) * (p1.y - p2.y);

        /// <summary>
        /// Converts geographical coordinate to position in the scene relative to the top-left corner of the map in map space.
        /// </summary>
        /// <param name="map">The map to use for the conversion.</param>
        /// <returns>Scene position (in map space).</returns>
        public Vector2d ToLocal(Map map) => map.control.LocationToLocal(this);
        
        /// <summary>
        /// Converts geographical coordinate to position in the scene relative to the top-left corner of the map in map space.
        /// </summary>
        /// <param name="control">The control to use for the conversion.</param>
        /// <returns>Scene position (in map space).</returns>
        public Vector2d ToLocal(ControlBase control) => control.LocationToLocal(this);
        
        /// <summary>
        /// Converts this geographic point to Mercator coordinates using the specified map.
        /// </summary>
        /// <param name="map">The map to use for the conversion.</param>
        /// <returns>A MercatorPoint representing the converted coordinates.</returns>
        public MercatorPoint ToMercator(Map map) => map.view.projection.LocationToMercator(x, y);

        /// <summary>
        /// Converts this geographic point to Mercator coordinates using the specified projection.
        /// </summary>
        /// <param name="projection">The projection to use for the conversion.</param>
        /// <returns>A MercatorPoint representing the converted coordinates.</returns>
        public MercatorPoint ToMercator(Projection projection) => projection.LocationToMercator(x, y);

        /// <summary>
        /// Converts this geographic point to screen coordinates using the specified map.
        /// </summary>
        /// <param name="map">The map to use for the conversion.</param>
        /// <returns>Screen position.</returns>
        public Vector2 ToScreen(Map map) => map.control.LocationToScreen(this);
        
        /// <summary>
        /// Converts this geographic point to screen coordinates using the specified control.
        /// </summary>
        /// <param name="control">The control to use for the conversion.</param>
        /// <returns>Screen position.</returns>
        public Vector2 ToScreen(ControlBase control) => control.LocationToScreen(this);
        
        public override string ToString() => $"(longitude: {longitude.ToString(Culture.numberFormat)}, latitude: {latitude.ToString(Culture.numberFormat)})";

        /// <summary>
        /// Converts this GeoPoint to tile coordinates using the specified map.
        /// </summary>
        /// <param name="map">The map to use for the conversion.</param>
        /// <param name="zoom">Optional zoom level. If not provided, the zoom level of the map is used.</param>
        /// <returns>A TilePoint representing the converted coordinates.</returns>
        public TilePoint ToTile(Map map, int? zoom = null)
        {
            return map.view.projection.LocationToTile(x, y, zoom != null ? zoom.Value : map.view.intZoom);
        }

        /// <summary>
        /// Converts this GeoPoint to tile coordinates using the specified projection.
        /// </summary>
        /// <param name="projection">The projection to use for the conversion.</param>
        /// <param name="zoom">The zoom level.</param>
        /// <returns>A TilePoint representing the converted coordinates.</returns>
        public TilePoint ToTile(Projection projection, int zoom) => projection.LocationToTile(x, y, zoom);

        /// <summary>
        /// Converts geographical coordinates to position in world space.
        /// </summary>
        /// <param name="map">The map to use for the conversion.</param>
        /// <returns>World position.</returns>
        public Vector3 ToWorld(Map map)
        {
            ControlBaseDynamicMesh control = map.control as ControlBaseDynamicMesh;
            if (!control) throw new Exception("Dynamic Mesh Control is required");
            return control.LocationToWorld(this);
        }
        
        /// <summary>
        /// Converts geographical coordinates to position in world space.
        /// </summary>
        /// <param name="control">The control to use for the conversion.</param>
        /// <returns>World position.</returns>
        public Vector3 ToWorld(ControlBaseDynamicMesh control) => control.LocationToWorld(this);

        /// <summary>
        /// Implicitly converts a Vector2 to a GeoPoint.
        /// </summary>
        /// <param name="v">The Vector2 to convert.</param>
        /// <returns>A new GeoPoint with the same coordinates as the Vector2.</returns>
        public static implicit operator GeoPoint(Vector2 v) => new(v.x, v.y);

        /// <summary>
        /// Implicitly converts a GeoPoint to a Vector2d.
        /// </summary>
        /// <param name="v">The GeoPoint to convert.</param>
        /// <returns>A new Vector2d with the same coordinates as the GeoPoint.</returns>
        public static implicit operator Vector2d(GeoPoint v) => new(v.longitude, v.latitude);

        /// <summary>
        /// Implicitly converts a Vector3 to a GeoPoint.
        /// </summary>
        /// <param name="v">The Vector3 to convert.</param>
        /// <returns>A new GeoPoint with the same coordinates as the Vector3.</returns>
        public static implicit operator GeoPoint(Vector3 v) => new(v.x, v.y);

        /// <summary>
        /// Implicitly converts a GeoPoint to a Vector2.
        /// </summary>
        /// <param name="v">The GeoPoint to convert.</param>
        /// <returns>A new Vector2 with the same coordinates as the GeoPoint.</returns>
        public static implicit operator Vector2(GeoPoint v) => new((float)v.longitude, (float)v.latitude);

        /// <summary>
        /// Implicitly converts a Vector2d to a GeoPoint.
        /// </summary>
        /// <param name="v">The Vector2d to convert.</param>
        /// <returns>A new GeoPoint with the same coordinates as the Vector2d.</returns>
        public static implicit operator GeoPoint(Vector2d v) => new(v.x, v.y);

        /// <summary>
        /// Implicitly converts a GeoPoint to a Vector3.
        /// </summary>
        /// <param name="v">The GeoPoint to convert.</param>
        /// <returns>A new Vector3 with the same coordinates as the GeoPoint.</returns>
        public static implicit operator Vector3(GeoPoint v) => new((float)v.longitude, (float)v.latitude);

        /// <summary>
        /// Adds two GeoPoints.
        /// </summary>
        /// <param name="v1">The first GeoPoint.</param>
        /// <param name="v2">The second GeoPoint.</param>
        /// <returns>A new GeoPoint that is the sum of the two GeoPoints.</returns>
        public static GeoPoint operator +(GeoPoint v1, GeoPoint v2) => new(v1.x + v2.x, v1.y + v2.y);

        /// <summary>
        /// Adds a GeoPoint and a Vector2d.
        /// </summary>
        /// <param name="v1">The GeoPoint.</param>
        /// <param name="v2">The Vector2d.</param>
        /// <returns>A new GeoPoint that is the sum of the GeoPoint and the Vector2d.</returns>
        public static GeoPoint operator +(GeoPoint v1, Vector2d v2) => new(v1.x + v2.x, v1.y + v2.y);

        /// <summary>
        /// Subtracts one GeoPoint from another.
        /// </summary>
        /// <param name="v1">The first GeoPoint.</param>
        /// <param name="v2">The second GeoPoint.</param>
        /// <returns>A new Vector2d that is the difference between the two GeoPoints.</returns>
        public static Vector2d operator -(GeoPoint v1, GeoPoint v2) => new(v1.x - v2.x, v1.y - v2.y);

        /// <summary>
        /// Subtracts a Vector2d from a GeoPoint.
        /// </summary>
        /// <param name="v1">The GeoPoint.</param>
        /// <param name="v2">The Vector2d.</param>
        /// <returns>A new GeoPoint that is the difference between the GeoPoint and the Vector2d.</returns>
        public static GeoPoint operator -(GeoPoint v1, Vector2d v2) => new(v1.x - v2.x, v1.y - v2.y);

        /// <summary>
        /// Determines whether two GeoPoints are equal.
        /// </summary>
        /// <param name="v1">The first GeoPoint.</param>
        /// <param name="v2">The second GeoPoint.</param>
        /// <returns>true if the GeoPoints are equal; otherwise, false.</returns>
        public static bool operator ==(GeoPoint v1, GeoPoint v2) => SqrMagnitude(v1, v2) < double.Epsilon;

        /// <summary>
        /// Determines whether two GeoPoints are not equal.
        /// </summary>
        /// <param name="v1">The first GeoPoint.</param>
        /// <param name="v2">The second GeoPoint.</param>
        /// <returns>true if the GeoPoints are not equal; otherwise, false.</returns>
        public static bool operator !=(GeoPoint v1, GeoPoint v2) => SqrMagnitude(v1, v2) >= double.Epsilon;

        /// <summary>
        /// Multiplies a GeoPoint by a scalar.
        /// </summary>
        /// <param name="v1">The GeoPoint.</param>
        /// <param name="d">The scalar value.</param>
        /// <returns>A new GeoPoint that is the product of the GeoPoint and the scalar.</returns>
        public static GeoPoint operator *(GeoPoint v1, double d) => new(v1.x * d, v1.y * d);

        /// <summary>
        /// Divides a GeoPoint by a scalar.
        /// </summary>
        /// <param name="v1">The GeoPoint.</param>
        /// <param name="d">The scalar value.</param>
        /// <returns>A new GeoPoint that is the quotient of the GeoPoint and the scalar.</returns>
        public static GeoPoint operator /(GeoPoint v1, double d) => new(v1.x / d, v1.y / d);
    }
}