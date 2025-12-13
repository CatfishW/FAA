/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System.Collections.Generic;
using OnlineMaps;
using UnityEngine;

namespace OnlineMapsExamples
{
    /// <summary>
    /// Example of use Drawing API.
    /// </summary>
    [AddComponentMenu(Utils.ExampleMenuPath + "DrawingAPI Example")]
    public class DrawingAPI_Example : MonoBehaviour
    {
        /// <summary>
        /// Reference to the map. If not specified, the current instance will be used.
        /// </summary>
        public Map map;

        private void Start()
        {
            // If map is not specified, use the current instance.
            if (!map && !(map = Map.instance))
            {
                Debug.LogError("Map not found");
                return;
            }

            List<GeoPoint> lineLocations = new List<GeoPoint>
            {
                //Geographic coordinates
                new GeoPoint(3, 3),
                new GeoPoint(5, 3),
                new GeoPoint(4, 4),
                new GeoPoint(9.3f, 6.5f)
            };

            List<GeoPoint> polyLocations = new List<GeoPoint>
            {
                //Geographic coordinates
                new GeoPoint(0, 0),
                new GeoPoint(1, 0),
                new GeoPoint(2, 2),
                new GeoPoint(0, 1)
            };

            // Draw line
            Line line = new Line(lineLocations, Color.green, 5);
            map.drawingElementManager.Add(line);

            // Draw filled transparent poly
            Polygon polygon = new Polygon(polyLocations, Color.red, 1, new Color(1, 1, 1, 0.5f));
            map.drawingElementManager.Add(polygon);

            // Draw filled rectangle
            Rectangle rect = new Rectangle(
                new GeoPoint(2, 2), // Location of top-left corner of the rectangle
                new Vector2d(1, 1), // Size of the rectangle in degrees
                Color.green, // Border color
                1, // Border width
                Color.blue // Background color
            );
            map.drawingElementManager.Add(rect);
        }
    }
}