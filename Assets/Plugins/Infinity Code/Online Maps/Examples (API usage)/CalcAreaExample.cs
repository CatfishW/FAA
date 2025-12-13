/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using System.Collections.Generic;
using System.Linq;
using OnlineMaps;
using UnityEngine;

namespace OnlineMapsExamples
{
    /// <summary>
    /// Example of calculating the size of area.
    /// </summary>
    [AddComponentMenu(Utils.ExampleMenuPath + "CalcAreaExample")]
    public class CalcAreaExample : MonoBehaviour
    {
        /// <summary>
        /// Reference to the map. If not specified, the current instance will be used.
        /// </summary>
        public Map map;
        
        /// <summary>
        /// Texture of marker
        /// </summary>
        public Texture2D markerTexture;

        /// <summary>
        /// Line width.
        /// </summary>
        public float borderWidth = 1;
        
        private float _borderWidth;
        private bool changed = false;
        private ControlBase control;
        private DrawingElementManager drawingElementManager;
        private List<Marker2D> markers = new List<Marker2D>();
        private Marker2DManager markerManager;
        private List<GeoPoint> markerPositions = new List<GeoPoint>();
        private Polygon polygon;
        
        public void Clear()
        {
            if (polygon != null)
            {
                drawingElementManager.Remove(polygon);
                polygon = null;
            }

            foreach (Marker2D marker in markers) markerManager.Remove(marker);
            markers.Clear();

            markerPositions.Clear();
            changed = true;
        }

        private void CheckMarkerPositions()
        {
            // Check the position of each marker.
            for (int i = 0; i < markers.Count; i++)
            {
                if (markerPositions[i] != markers[i].location)
                {
                    // If the position marker changed, then change the value in markerPositions. 
                    // In the polygon value changes automatically.
                    markerPositions[i] = markers[i].location;
                    changed = true;
                }
            }
        }

        private void OnMapPress()
        {
            if (!InputManager.GetKey(KeyCode.LeftShift)) return;
            
            // Get the geographical coordinates of the cursor.
            GeoPoint cursorLocation = control.ScreenToLocation();

            // Create a new marker at the specified coordinates.
            Marker2D marker = markerManager.Create(cursorLocation, markerTexture, "Marker " + (markerManager.count + 1));

            // Save marker and coordinates.
            markerPositions.Add(cursorLocation);
            markers.Add(marker);

            // Mark that markers changed.
            changed = true;
        }

        private void Start()
        {
            // If map is not specified, use the current instance.
            if (!map && !(map = Map.instance))
            {
                Debug.LogError("Map not found");
                return;
            }
            
            // Get references to the control, drawingElementManager and markerManager.
            control = map.control;
            drawingElementManager = control.drawingElementManager;
            markerManager = control.marker2DManager;

            _borderWidth = borderWidth;
            
            control.OnPress += OnMapPress;
        }

        private void Update()
        {
            if (Math.Abs(_borderWidth - borderWidth) > float.Epsilon)
            {
                _borderWidth = borderWidth;
                if (polygon != null)
                {
                    polygon.borderWidth = borderWidth;
                    map.Redraw();
                }
            }

            // Check the position of the markers.
            CheckMarkerPositions();

            // If nothing happens, then return.
            if (!changed) return;
            changed = false;

            // If the number of points is less than 3, then return.
            if (markers.Count < 3)
            {
                map.Redraw();
                return;
            }

            // If the polygon is not created, then create.
            if (polygon == null)
            {
                // For points, reference to markerPositions. 
                polygon = new Polygon(markerPositions, Color.black, borderWidth, new Color(1, 1, 1, 0.3f));

                // Add an element to the map.
                drawingElementManager.Add(polygon);
            }
            else
            {
                polygon.SetPoints(markerPositions);
            }

            // Calculates area of the polygon.
            // Important: this algorithm works correctly only if the lines do not intersect.
            double area = 0;

            // Triangulate points.
            int[] indexes = Geometry.Triangulate(markerPositions).ToArray();

            // Calculate the area of each triangle.
            for (int i = 0; i < indexes.Length / 3; i++)
            {
                // Get the points of the triangle.
                GeoPoint p1 = markerPositions[indexes[i * 3]];
                GeoPoint p2 = markerPositions[indexes[i * 3 + 1]];
                GeoPoint p3 = markerPositions[indexes[i * 3 + 2]];

                // Calculate the distance between points.
                double d1 = p1.Distance(p2);
                double d2 = p2.Distance(p3);
                double d3 = p3.Distance(p1);

                // Calculate the area.
                double p = (d1 + d2 + d3) / 2;
                area += Math.Sqrt(p * (p - d1) * (p - d2) * (p - d3));
            }

            Debug.Log("Area: " + area + " km^2");

            map.Redraw();
        }
    }
}