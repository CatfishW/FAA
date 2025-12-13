/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using OnlineMaps;
using UnityEngine;

namespace OnlineMapsExamples
{
    /// <summary>
    /// Example how to draw a circle around a marker
    /// </summary>
    [AddComponentMenu(Utils.ExampleMenuPath + "DrawCircleAroundMarker")]
    public class DrawCircleAroundMarker : MonoBehaviour
    {
        /// <summary>
        /// Reference to the map. If not specified, the current instance will be used.
        /// </summary>
        public Map map;
        
        /// <summary>
        /// Radius of the circle
        /// </summary>
        public float radiusKM = 0.1f;

        /// <summary>
        /// Number of segments
        /// </summary>
        public int segments = 32;

        /// <summary>
        /// This method is called when the script starts
        /// </summary>
        private void Start()
        {
            // If map is not specified, use the current instance.
            if (!map && !(map = Map.instance))
            {
                Debug.LogError("Map not found");
                return;
            }
            
            // Subscribe to click on map event
            map.control.OnClick += OnMapClick;
        }

        /// <summary>
        /// This method is called when a user clicks on a map
        /// </summary>
        private void OnMapClick()
        {
            // Get the coordinates under cursor
            ControlBase control = map.control;
            GeoPoint point = control.ScreenToLocation();

            // Create a new marker under cursor
            control.marker2DManager.Create(point, "Marker " + control.marker2DManager.count);

            // Get the coordinate at the desired distance
            GeoPoint distantCoordinate = point.Distant(radiusKM, 90);
            
            // Convert the coordinate under cursor to tile position
            TilePoint t1 = point.ToTile(map, 20);

            // Convert remote coordinate to tile position
            TilePoint t2 = distantCoordinate.ToTile(map, 20);

            // Calculate radius in tiles
            double r = t2.x - t1.x;

            // Create a new array for points
            Vector2d[] points = new Vector2d[segments];

            // Calculate a step
            double step = 360d / segments;

            // Calculate each point of circle
            for (int i = 0; i < segments; i++)
            {
                double px = t1.x + Math.Cos(step * i * Constants.Deg2Rad) * r;
                double py = t1.y + Math.Sin(step * i * Constants.Deg2Rad) * r;
                points[i] = map.view.projection.TileToLocation(px, py, 20);
            }

            // Create a new polygon to draw a circle
            Polygon polygon = new Polygon(points, Color.red, 3);
            control.drawingElementManager.Add(polygon);
        }
    }
}