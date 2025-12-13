/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using UnityEngine;

namespace OnlineMaps
{
    /// <summary>
    /// Provides various geographical mathematical functions.
    /// </summary>
    public static class GeoMath
    {
        /// <summary>
        /// Get the center point and best zoom for the array of markers.
        /// </summary>
        /// <param name="markers">Array of markers.</param>
        /// <param name="inset">Inset for finding a cropped zoom level.</param>
        /// <returns>Center point and best zoom.</returns>
        public static (GeoPoint center, int zoom) CenterPointAndZoom(Marker[] markers, Vector2 inset = default)
        {
            CenterPointAndZoom(markers, out GeoPoint center, out int zoom, inset);
            return (center, zoom);
        }
        
        /// <summary>
        /// Get the center point and best zoom for the array of markers.
        /// </summary>
        /// <param name="markers">Array of markers.</param>
        /// <param name="center">Center point.</param>
        /// <param name="zoom">Best zoom.</param>
        /// <param name="inset">Inset for finding a cropped zoom level.</param>
        public static void CenterPointAndZoom(Marker[] markers, out GeoPoint center, out int zoom, Vector2 inset = default)
        {
            center = new GeoPoint();
            zoom = Constants.MinZoom;
            if (markers == null || markers.Length == 0) return;

            Map map = Map.instance;
            Projection projection = map.view.projection;

            GeoPoint p = markers[0].location;
            double minX = p.x;
            double minY = p.y;
            double maxX = p.x;
            double maxY = p.y;

            for (int i = 1; i < markers.Length; i++)
            {
                Marker marker = markers[i];
                p = marker.location;

                double rx = p.x - minX;
                if (rx > 180) p.x -= 360;
                else if (rx < -180) p.x += 360;

                if (p.x < minX) minX = p.x;
                if (p.x > maxX) maxX = p.x;

                if (p.y < minY) minY = p.y;
                if (p.y > maxY) maxY = p.y;
            }

            double sx = maxX - minX;
            double sy = maxY - minY;

            center = new GeoPoint(sx / 2 + minX, sy / 2 + minY);

            if (center.x < -180) center.x += 360;
            else if (center.x > 180) center.x -= 360;

            int width = map.control.width;
            int height = map.control.height;
            double xTileOffset = inset.x * width;
            double yTileOffset = inset.y * height;

            float countX = width / (float)Constants.TileSize / 2;
            float countY = height / (float)Constants.TileSize / 2;

            bool useZoomMin = false;

            for (int z = Constants.MaxZoom; z > Constants.MinZoom; z--)
            {
                bool success = true;

                TilePoint t = projection.LocationToTile(center.x, center.y, z);
                int _mx = 1 << z;
                int hx = 1 << (z - 1);

                for (int i = 0; i < markers.Length; i++)
                {
                    Marker marker = markers[i];
                    TilePoint tm = marker.location.ToTile(map, z);

                    if (tm.x - t.x < -hx) tm.x += _mx;
                    else if (tm.x - t.x > hx) tm.x -= _mx;

                    tm.x -= t.x - countX;
                    tm.y -= t.y - countY;
                    tm.x *= Constants.TileSize;
                    tm.y *= Constants.TileSize;

                    if (marker is Marker2D)
                    {
                        useZoomMin = true;
                        Marker2D m = marker as Marker2D;
                        Vector2Int ip = m.GetAlignedPosition((int)tm.x, (int)tm.y);
                        if (ip.x < xTileOffset || ip.y < yTileOffset || ip.x + m.width > width - xTileOffset || ip.y + m.height > height - yTileOffset)
                        {
                            success = false;
                            break;
                        }
                    }
                    else if (marker is Marker3D)
                    {
                        if (tm.x < xTileOffset || tm.y < yTileOffset || tm.x > width - xTileOffset || tm.y > height - yTileOffset)
                        {
                            success = false;
                            break;
                        }
                    }
                    else
                    {
                        throw new Exception("Wrong marker type");
                    }
                }

                if (success)
                {
                    zoom = z;
                    if (useZoomMin) zoom -= 1;
                    return;
                }
            }

            zoom = Constants.MinZoom;
        }

        /// <summary>
        /// Get the center point and best zoom for the array of coordinates.
        /// </summary>
        /// <param name="locations">Array of coordinates</param>
        /// <param name="inset">Inset for finding a cropped zoom level.</param>
        /// <returns>Center point and best zoom.</returns>
        public static (GeoPoint center, int zoom) CenterPointAndZoom(GeoPoint[] locations, Vector2 inset = default)
        {
            CenterPointAndZoom(locations, out GeoPoint center, out int zoom, inset);
            return (center, zoom);
        }

        /// <summary>
        /// Get the center point and best zoom for the array of coordinates.
        /// </summary>
        /// <param name="locations">Array of coordinates</param>
        /// <param name="center">Center coordinate</param>
        /// <param name="zoom">Best zoom</param>
        /// <param name="inset">Inset for finding a cropped zoom level.</param>
        public static void CenterPointAndZoom(GeoPoint[] locations, out GeoPoint center, out int zoom, Vector2 inset = default)
        {
            center = new GeoPoint();
            zoom = Constants.MinZoom;
            if (locations == null || locations.Length == 0) return;

            Map map = Map.instance;

            GeoPoint p = locations[0];
            GeoRect r = new GeoRect(p);
            for (int i = 1; i < locations.Length; i++) r.Encapsulate(locations[i]);

            center = r.center;

            if (center.x < -180) center.x += 360;
            else if (center.x > 180) center.x -= 360;

            float width = map.control.width;
            float height = map.control.height;
            double xTileOffset = inset.x * width;
            double yTileOffset = inset.y * height;

            float countX = width / Constants.TileSize / 2;
            float countY = height / Constants.TileSize / 2;

            for (int z = Constants.MaxZoom; z > Constants.MinZoom; z--)
            {
                bool success = true;

                int mx = 1 << z;
                int hx = 1 << (z - 1);
                TilePoint tc = center.ToTile(map, z);

                for (int i = 0; i < locations.Length; i++)
                {
                    TilePoint t = locations[i].ToTile(map, z);

                    if (t.x - tc.x < -hx) t.x += mx;
                    else if (t.x - tc.x > hx) t.x -= mx;

                    t.x = (t.x - tc.x + countX) * Constants.TileSize;
                    t.y = (t.y - tc.y + countY) * Constants.TileSize;

                    if (t.x < xTileOffset || t.y < yTileOffset || t.x > width - xTileOffset || t.y > height - yTileOffset)
                    {
                        success = false;
                        break;
                    }
                }

                if (success)
                {
                    zoom = z;
                    return;
                }
            }

            zoom = Constants.MinZoom;
        }

        /// <summary>
        /// The distance between two geographical coordinates.
        /// </summary>
        /// <param name="point1">Coordinate 1</param>
        /// <param name="point2">Coordinate 2</param>
        /// <returns>Distance (km)</returns>
        public static double Distance(GeoPoint point1, GeoPoint point2)
        {
            return Distance(point1.x, point1.y, point2.x, point2.y);
        }

        /// <summary>
        /// The distance between two geographical coordinates.
        /// </summary>
        /// <param name="point1">Coordinate 1</param>
        /// <param name="point2">Coordinate 2</param>
        /// <returns>Distance (km)</returns>
        public static double Distance(Vector2 point1, Vector2 point2)
        {
            return Distance(point1.x, point1.y, point2.x, point2.y);
        }

        /// <summary>
        /// The distance between two geographical coordinates.
        /// </summary>
        /// <param name="point1">Coordinate 1</param>
        /// <param name="point2">Coordinate 2</param>
        /// <returns>Distance (km)</returns>
        public static double Distance(GeoPoint3 point1, GeoPoint3 point2)
        {
            return Distance(point1.x, point1.y, point1.z, point2.x, point2.y, point2.z);
        }

        /// <summary>
        /// The distance between two geographical coordinates.
        /// </summary>
        /// <param name="x1">Longitude 1</param>
        /// <param name="y1">Latitude 1</param>
        /// <param name="x2">Longitude 2</param>
        /// <param name="y2">Latitude 2</param>
        /// <returns>Distance (km)</returns>
        public static double Distance(double x1, double y1, double x2, double y2)
        {
            double scfY = Math.Sin(y1 * Constants.Deg2Rad);
            double sctY = Math.Sin(y2 * Constants.Deg2Rad);
            double ccfY = Math.Cos(y1 * Constants.Deg2Rad);
            double cctY = Math.Cos(y2 * Constants.Deg2Rad);
            double cX = Math.Cos((x1 - x2) * Constants.Deg2Rad);
            double sizeX1 = Math.Abs(Constants.EarthRadius * Math.Acos(scfY * scfY + ccfY * ccfY * cX));
            double sizeX2 = Math.Abs(Constants.EarthRadius * Math.Acos(sctY * sctY + cctY * cctY * cX));
            double sizeX = (sizeX1 + sizeX2) / 2.0;
            double sizeY = Constants.EarthRadius * Math.Acos(scfY * sctY + ccfY * cctY);
            if (double.IsNaN(sizeX)) sizeX = 0;
            if (double.IsNaN(sizeY)) sizeY = 0;
            return Math.Sqrt(sizeX * sizeX + sizeY * sizeY);
        }

        /// <summary>
        /// The distance between two geographical coordinates with altitude.
        /// </summary>
        /// <param name="x1">Longitude 1</param>
        /// <param name="y1">Latitude 1</param>
        /// <param name="a1">Altitude 1 (km)</param>
        /// <param name="x2">Longitude 2</param>
        /// <param name="y2">Latitude 2</param>
        /// <param name="a2">Altitude 2 (km)</param>
        /// <returns>Distance (km)</returns>
        public static double Distance(double x1, double y1, double a1, double x2, double y2, double a2)
        {
            double r = Constants.EarthRadius + Math.Min(a1, a2);
            double scfY = Math.Sin(y1 * Constants.Deg2Rad);
            double sctY = Math.Sin(y2 * Constants.Deg2Rad);
            double ccfY = Math.Cos(y1 * Constants.Deg2Rad);
            double cctY = Math.Cos(y2 * Constants.Deg2Rad);
            double cX = Math.Cos((x1 - x2) * Constants.Deg2Rad);
            double sizeX1 = Math.Abs(r * Math.Acos(scfY * scfY + ccfY * ccfY * cX));
            double sizeX2 = Math.Abs(r * Math.Acos(sctY * sctY + cctY * cctY * cX));
            double dx = (sizeX1 + sizeX2) / 2.0;
            double dy = r * Math.Acos(scfY * sctY + ccfY * cctY);
            if (double.IsNaN(dx)) dx = 0;
            if (double.IsNaN(dy)) dy = 0;
            double d = Math.Sqrt(dx * dx + dy * dy);
            double hd = Math.Abs(a1 - a2);
            return Math.Sqrt(d * d + hd * hd);
        }

        /// <summary>
        /// The distance between two geographical coordinates.
        /// </summary>
        /// <param name="point1">Coordinate 1</param>
        /// <param name="point2">Coordinate 2</param>
        /// <returns>Distance (km)</returns>
        public static Vector2d Distances(GeoPoint point1, GeoPoint point2)
        {
            double dx, dy;
            Distances(point1.x, point1.y, point2.x, point2.y, out dx, out dy);
            return new Vector2d(dx, dy);
        }

        /// <summary>
        /// The distance between two geographical coordinates.
        /// </summary>
        /// <param name="point1">Coordinate 1</param>
        /// <param name="point2">Coordinate 2</param>
        /// <returns>Distance (km)</returns>
        public static Vector2d Distances(Vector2 point1, Vector2 point2)
        {
            double dx, dy;
            Distances(point1.x, point1.y, point2.x, point2.y, out dx, out dy);
            return new Vector2d(dx, dy);
        }

        /// <summary>
        /// The distance between two geographical coordinates.
        /// </summary>
        /// <param name="point1">Coordinate 1</param>
        /// <param name="point2">Coordinate 2</param>
        /// <returns>Distance (km)</returns>
        public static Vector3d Distances(GeoPoint3 point1, GeoPoint3 point2)
        {
            double dx, dy, dz;
            Distances(point1.x, point1.y, point1.z, point2.x, point2.y, point2.z, out dx, out dy, out dz);
            return new Vector3d(dx, dy, dz);
        }

        /// <summary>
        /// The distance between two geographical coordinates.
        /// </summary>
        /// <param name="x1">Longitude 1</param>
        /// <param name="y1">Latitude 1</param>
        /// <param name="x2">Longitude 2</param>
        /// <param name="y2">Latitude 2</param>
        /// <param name="dx">Longitude distance (km)</param>
        /// <param name="dy">Latitude distance (km)</param>
        public static void Distances(double x1, double y1, double x2, double y2, out double dx, out double dy)
        {
            double scfY = Math.Sin(y1 * Constants.Deg2Rad);
            double sctY = Math.Sin(y2 * Constants.Deg2Rad);
            double ccfY = Math.Cos(y1 * Constants.Deg2Rad);
            double cctY = Math.Cos(y2 * Constants.Deg2Rad);
            double cX = Math.Cos((x1 - x2) * Constants.Deg2Rad);
            double sizeX1 = Math.Abs(Constants.EarthRadius * Math.Acos(scfY * scfY + ccfY * ccfY * cX));
            double sizeX2 = Math.Abs(Constants.EarthRadius * Math.Acos(sctY * sctY + cctY * cctY * cX));
            dx = (sizeX1 + sizeX2) / 2.0;
            dy = Constants.EarthRadius * Math.Acos(scfY * sctY + ccfY * cctY);
            if (double.IsNaN(dx)) dx = 0;
            if (double.IsNaN(dy)) dy = 0;
        }

        /// <summary>
        /// The distance between two geographical coordinates with altitude.
        /// </summary>
        /// <param name="x1">Longitude 1</param>
        /// <param name="y1">Latitude 1</param>
        /// <param name="a1">Altitude 1</param>
        /// <param name="x2">Longitude 2</param>
        /// <param name="y2">Latitude 2</param>
        /// <param name="a2">Altitude 2</param>
        /// <param name="dx">Longitude distance (km)</param>
        /// <param name="dy">Latitude distance (km)</param>
        /// <param name="da">Altitude distance</param>
        public static void Distances(double x1, double y1, double a1, double x2, double y2, double a2, out double dx, out double dy, out double da)
        {
            double r = Constants.EarthRadius + Math.Min(a1, a2);
            double scfY = Math.Sin(y1 * Constants.Deg2Rad);
            double sctY = Math.Sin(y2 * Constants.Deg2Rad);
            double ccfY = Math.Cos(y1 * Constants.Deg2Rad);
            double cctY = Math.Cos(y2 * Constants.Deg2Rad);
            double cX = Math.Cos((x1 - x2) * Constants.Deg2Rad);
            double sizeX1 = Math.Abs(r * Math.Acos(scfY * scfY + ccfY * ccfY * cX));
            double sizeX2 = Math.Abs(r * Math.Acos(sctY * sctY + cctY * cctY * cX));
            dx = (sizeX1 + sizeX2) / 2.0;
            dy = r * Math.Acos(scfY * sctY + ccfY * cctY);
            if (double.IsNaN(dx)) dx = 0;
            if (double.IsNaN(dy)) dy = 0;
            da = Math.Abs(a1 - a2);
        }

        /// <summary>
        /// The distance between the edges of a geographical rectangle.
        /// </summary>
        /// <param name="rect">The geographical rectangle.</param>
        /// <returns>A Vector2d representing the distances (km) between the edges of the rectangle.</returns>
        public static Vector2d Distances(GeoRect rect)
        {
            double dx, dy;
            Distances(rect.left, rect.top, rect.right, rect.bottom, out dx, out dy);
            return new Vector2d(dx, dy);
        }

        /// <summary>
        /// Calculates a distant geographical coordinate from a given point, distance, and angle.
        /// </summary>
        /// <param name="longitude">The longitude of the starting point.</param>
        /// <param name="latitude">The latitude of the starting point.</param>
        /// <param name="distance">The distance to the new point (in kilometers).</param>
        /// <param name="angle">The angle from the starting point to the new point (in degrees).</param>
        /// <returns>A new GeoPoint representing the distant coordinate.</returns>
        public static GeoPoint DistantLocation(double longitude, double latitude, double distance, double angle)
        {
            double d = distance / Constants.EarthRadius;
            double a = angle * Constants.Deg2Rad;

            double f1 = latitude * Constants.Deg2Rad;
            double l1 = longitude * Constants.Deg2Rad;

            double sinF1 = Math.Sin(f1);
            double cosF1 = Math.Cos(f1);
            double sinD = Math.Sin(d);
            double cosD = Math.Cos(d);
            double sinA = Math.Sin(a);
            double cosA = Math.Cos(a);

            double sinF2 = sinF1 * cosD + cosF1 * sinD * cosA;
            double f2 = Math.Asin(sinF2);
            double y = sinA * sinD * cosF1;
            double x = cosD - sinF1 * sinF2;
            double l2 = l1 + Math.Atan2(y, x);

            return new GeoPoint((l2 * Constants.Rad2Deg + 540) % 360 - 180, f2 * Constants.Rad2Deg);
        }

        /// <summary>
        /// Calculates a distant geographical coordinate from a given point, distance, and angle.
        /// </summary>
        /// <param name="location">The starting geographical coordinate.</param>
        /// <param name="distance">The distance to the new point (in kilometers).</param>
        /// <param name="angle">The angle from the starting point to the new point (in degrees).</param>
        /// <returns>A new GeoPoint representing the distant coordinate.</returns>
        public static GeoPoint DistantLocation(GeoPoint location, double distance, double angle)
        {
            return DistantLocation(location.x, location.y, distance, angle);
        }
    }
}