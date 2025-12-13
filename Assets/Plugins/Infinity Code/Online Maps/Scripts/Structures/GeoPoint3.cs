/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using UnityEngine;

namespace OnlineMaps
{
    /// <summary>
    /// Represents a geographical point with longitude, latitude, and altitude.
    /// </summary>
    [Serializable]
    public struct GeoPoint3
    {
        /// <summary>
        /// The longitude of the GeoPoint3.
        /// </summary>
        [Alias("lng")]
        public double x;

        /// <summary>
        /// The latitude of the GeoPoint3.
        /// </summary>
        [Alias("lat")]
        public double y;

        /// <summary>
        /// The altitude of the GeoPoint3.
        /// </summary>
        [Alias("alt")]
        public double z;

        /// <summary>
        /// Checks if the GeoPoint3 is at the origin (0, 0, 0).
        /// </summary>
        public bool isZero => x == 0 && y == 0 && z == 0;

        /// <summary>
        /// Gets or sets the longitude.
        /// </summary>
        public double longitude
        {
            get => x;
            set => x = value;
        }

        /// <summary>
        /// Gets or sets the latitude.
        /// </summary>
        public double latitude
        {
            get => y;
            set => y = value;
        }

        /// <summary>
        /// Gets or sets the altitude.
        /// </summary>
        public double altitude
        {
            get => z;
            set => z = value;
        }

        /// <summary>
        /// Gets the magnitude of the GeoPoint3.
        /// </summary>
        public double magnitude => Math.Sqrt(x * x + y * y + z * z);

        /// <summary>
        /// Gets a GeoPoint3 at the origin (0, 0, 0).
        /// </summary>
        public static GeoPoint3 zero => new(0, 0, 0);

        /// <summary>
        /// Initializes a new instance of the GeoPoint3 struct with longitude and latitude.
        /// </summary>
        /// <param name="longitude">The longitude.</param>
        /// <param name="latitude">The latitude.</param>
        public GeoPoint3(double longitude, double latitude)
        {
            x = longitude;
            y = latitude;
            z = 0;
        }

        /// <summary>
        /// Initializes a new instance of the GeoPoint3 struct with longitude, latitude, and altitude.
        /// </summary>
        /// <param name="longitude">The longitude.</param>
        /// <param name="latitude">The latitude.</param>
        /// <param name="altitude">The altitude.</param>
        public GeoPoint3(double longitude, double latitude, double altitude)
        {
            x = longitude;
            y = latitude;
            z = altitude;
        }

        /// <summary>
        /// Calculates the angle between this GeoPoint3 and the origin.
        /// </summary>
        /// <returns>The angle in degrees.</returns>
        public double Angle() => Geometry.Angle2D(zero, this);

        /// <summary>
        /// Calculates the angle between this GeoPoint3 and another GeoPoint3.
        /// </summary>
        /// <param name="p">The other GeoPoint3.</param>
        /// <returns>The angle in degrees.</returns>
        public double Angle(GeoPoint3 p) => Geometry.Angle2D(this, p);

        /// <summary>
        /// Calculates the angle between two GeoPoint3 instances.
        /// </summary>
        /// <param name="p1">The first GeoPoint3.</param>
        /// <param name="p2">The second GeoPoint3.</param>
        /// <returns>The angle in degrees.</returns>
        public static double Angle(GeoPoint3 p1, GeoPoint3 p2) => Geometry.Angle2D(p1, p2);

        /// <summary>
        /// Calculates the distance between this GeoPoint3 and another GeoPoint3.
        /// </summary>
        /// <param name="p">The other GeoPoint3.</param>
        /// <returns>The distance.</returns>
        public double Distance(GeoPoint3 p) => GeoMath.Distance(this, p);

        /// <summary>
        /// Calculates the distance between two GeoPoint3 instances.
        /// </summary>
        /// <param name="p1">The first GeoPoint3.</param>
        /// <param name="p2">The second GeoPoint3.</param>
        /// <returns>The distance.</returns>
        public static double Distance(GeoPoint3 p1, GeoPoint3 p2) => GeoMath.Distance(p1, p2);

        /// <summary>
        /// Determines whether this instance and a specified object are equal.
        /// </summary>
        /// <param name="obj">The object to compare with the current instance.</param>
        /// <returns>true if the specified object is equal to the current instance; otherwise, false.</returns>
        public override bool Equals(object obj) => obj is GeoPoint3 other && Equals(other);

        /// <summary>
        /// Determines whether this instance and another specified GeoPoint3 are equal.
        /// </summary>
        /// <param name="other">The GeoPoint3 to compare with the current instance.</param>
        /// <returns>true if the specified GeoPoint3 is equal to the current instance; otherwise, false.</returns>
        public bool Equals(GeoPoint3 other) => x.Equals(other.x) && y.Equals(other.y) && z.Equals(other.z);

        /// <summary>
        /// Returns the hash code for this instance.
        /// </summary>
        /// <returns>A hash code for the current instance.</returns>
        public override int GetHashCode() => HashCode.Combine(x, y, z);

        /// <summary>
        /// Creates a new GeoPoint3 with specified latitude, longitude, and altitude.
        /// </summary>
        /// <param name="latitude">The latitude.</param>
        /// <param name="longitude">The longitude.</param>
        /// <param name="altitude">The altitude.</param>
        /// <returns>A new GeoPoint3 instance.</returns>
        public static GeoPoint3 LatLng(double latitude, double longitude, double altitude) => new(longitude, latitude, altitude);

        /// <summary>
        /// Linearly interpolates between this GeoPoint3 and another GeoPoint3 by a specified amount.
        /// </summary>
        /// <param name="p">The other GeoPoint3.</param>
        /// <param name="t">The interpolation factor.</param>
        /// <returns>A new interpolated GeoPoint3.</returns>
        public GeoPoint3 Lerp(GeoPoint3 p, double t)
        {
            if (t < 0) t = 0;
            else if (t > 1) t = 1;
            return new GeoPoint3(
                x + (p.x - x) * t,
                y + (p.y - y) * t,
                z + (p.z - z) * t);
        }

        /// <summary>
        /// Linearly interpolates between two GeoPoint3 instances by a specified amount.
        /// </summary>
        /// <param name="p1">The first GeoPoint3.</param>
        /// <param name="p2">The second GeoPoint3.</param>
        /// <param name="t">The interpolation factor.</param>
        /// <returns>A new interpolated GeoPoint3.</returns>
        public static GeoPoint3 Lerp(GeoPoint3 p1, GeoPoint3 p2, double t)
        {
            if (t < 0) t = 0;
            else if (t > 1) t = 1;
            return new GeoPoint3(
                p1.x + (p2.x - p1.x) * t,
                p1.y + (p2.y - p1.y) * t,
                p1.z + (p2.z - p1.z) * t);
        }

        /// <summary>
        /// Calculates the squared magnitude of this GeoPoint3.
        /// </summary>
        /// <returns>The squared magnitude.</returns>
        public double SqrMagnitude() => x * x + y * y + z * z;

        /// <summary>
        /// Calculates the squared magnitude between this GeoPoint3 and another GeoPoint3.
        /// </summary>
        /// <param name="p">The other GeoPoint3.</param>
        /// <returns>The squared magnitude.</returns>
        public double SqrMagnitude(GeoPoint3 p)
        {
            return (x - p.x) * (x - p.x) + (y - p.y) * (y - p.y) + (z - p.z) * (z - p.z);
        }

        /// <summary>
        /// Calculates the squared magnitude between two GeoPoint3 instances.
        /// </summary>
        /// <param name="p1">The first GeoPoint3.</param>
        /// <param name="p2">The second GeoPoint3.</param>
        /// <returns>The squared magnitude.</returns>
        public static double SqrMagnitude(GeoPoint3 p1, GeoPoint3 p2)
        {
            return (p1.x - p2.x) * (p1.x - p2.x) + (p1.y - p2.y) * (p1.y - p2.y) + (p1.z - p2.z) * (p1.z - p2.z);
        }
        
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
        /// Converts this geographic point to screen coordinates using the specified control.
        /// </summary>
        /// <param name="control">The control to use for the conversion.</param>
        /// <returns>Screen position.</returns>
        public Vector2 ToScreenPosition(ControlBase control) => control.LocationToScreen(this);

        /// <summary>
        /// Converts this geographic point to screen coordinates using the specified map.
        /// </summary>
        /// <param name="map">The map to use for the conversion.</param>
        /// <returns>Screen position.</returns>
        public Vector2 ToScreenPosition(Map map) => map.control.LocationToScreen(this);

        /// <summary>
        /// Returns a string representation of the GeoPoint3.
        /// </summary>
        /// <returns>A string representation of the GeoPoint3.</returns>
        public override string ToString() => $"(longitude: {longitude.ToString(Culture.numberFormat)}, latitude: {latitude.ToString(Culture.numberFormat)}, altitude: {altitude.ToString(Culture.numberFormat)})";

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
        public Vector3 ToWorldPosition(Map map)
        {
            ControlBaseDynamicMesh control = map.control as ControlBaseDynamicMesh;
            if (!control) throw new Exception("Dynamic Mesh Control is required");
            return control.LocationToWorld3(this);
        }
        
        /// <summary>
        /// Converts geographical coordinates to position in world space.
        /// </summary>
        /// <param name="control">The control to use for the conversion.</param>
        /// <returns>World position.</returns>
        public Vector3 ToWorldPosition(ControlBaseDynamicMesh control) => control.LocationToWorld3(this);

        /// <summary>
        /// Implicitly converts a Vector2 to a GeoPoint3.
        /// </summary>
        /// <param name="v">The Vector2 to convert.</param>
        public static implicit operator GeoPoint3(Vector2 v) => new(v.x, v.y);

        /// <summary>
        /// Implicitly converts a GeoPoint3 to a Vector2.
        /// </summary>
        /// <param name="v">The GeoPoint3 to convert.</param>
        public static implicit operator Vector2(GeoPoint3 v) => new((float)v.longitude, (float)v.latitude);

        /// <summary>
        /// Implicitly converts a Vector3 to a GeoPoint3.
        /// </summary>
        /// <param name="v">The Vector3 to convert.</param>
        public static implicit operator GeoPoint3(Vector3 v) => new(v.x, v.y, v.z);

        /// <summary>
        /// Implicitly converts a GeoPoint3 to a Vector3.
        /// </summary>
        /// <param name="v">The GeoPoint3 to convert.</param>
        public static implicit operator Vector3(GeoPoint3 v) => new((float)v.longitude, (float)v.latitude, (float)v.altitude);

        /// <summary>
        /// Implicitly converts a GeoPoint to a GeoPoint3.
        /// </summary>
        /// <param name="v">The GeoPoint to convert.</param>
        public static implicit operator GeoPoint3(GeoPoint v) => new(v.longitude, v.latitude);

        /// <summary>
        /// Implicitly converts a GeoPoint3 to a GeoPoint.
        /// </summary>
        /// <param name="v">The GeoPoint3 to convert.</param>
        public static implicit operator GeoPoint(GeoPoint3 v) => new(v.longitude, v.latitude);

        /// <summary>
        /// Adds two GeoPoint3 instances.
        /// </summary>
        /// <param name="v1">The first GeoPoint3.</param>
        /// <param name="v2">The second GeoPoint3.</param>
        /// <returns>A new GeoPoint3 that is the sum of the two GeoPoint3 instances.</returns>
        public static GeoPoint3 operator +(GeoPoint3 v1, GeoPoint3 v2) => new(v1.x + v2.x, v1.y + v2.y, v1.z + v2.z);

        /// <summary>
        /// Subtracts one GeoPoint3 from another.
        /// </summary>
        /// <param name="v1">The first GeoPoint3.</param>
        /// <param name="v2">The second GeoPoint3.</param>
        /// <returns>A new GeoPoint3 that is the difference between the two GeoPoint3 instances.</returns>
        public static GeoPoint3 operator -(GeoPoint3 v1, GeoPoint3 v2) => new(v1.x - v2.x, v1.y - v2.y, v1.z - v2.z);

        /// <summary>
        /// Determines whether two GeoPoint3 instances are equal.
        /// </summary>
        /// <param name="v1">The first GeoPoint3.</param>
        /// <param name="v2">The second GeoPoint3.</param>
        /// <returns>true if the GeoPoint3 instances are equal; otherwise, false.</returns>
        public static bool operator ==(GeoPoint3 v1, GeoPoint3 v2) => SqrMagnitude(v1, v2) < double.Epsilon;

        /// <summary>
        /// Determines whether two GeoPoint3 instances are not equal.
        /// </summary>
        /// <param name="v1">The first GeoPoint3.</param>
        /// <param name="v2">The second GeoPoint3.</param>
        /// <returns>true if the GeoPoint3 instances are not equal; otherwise, false.</returns>
        public static bool operator !=(GeoPoint3 v1, GeoPoint3 v2) => SqrMagnitude(v1, v2) >= double.Epsilon;

        /// <summary>
        /// Divides a GeoPoint3 by a scalar.
        /// </summary>
        /// <param name="v1">The GeoPoint3.</param>
        /// <param name="d">The scalar value.</param>
        /// <returns>A new GeoPoint3 that is the quotient of the GeoPoint3 and the scalar.</returns>
        public static GeoPoint3 operator /(GeoPoint3 v1, double d) => new(v1.x / d, v1.y / d, v1.z / d);
    }
}