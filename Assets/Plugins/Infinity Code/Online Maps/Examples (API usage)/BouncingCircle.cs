/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using System.Collections.Generic;
using OnlineMaps;
using UnityEngine;

namespace OnlineMapsExamples
{
    /// <summary>
    /// Example how to draw bouncing circle on the map.
    /// </summary>
    [AddComponentMenu(Utils.ExampleMenuPath + "BouncingCircle")]
    public class BouncingCircle : MonoBehaviour
    {
        /// <summary>
        /// Reference to the map. If not set, the current instance will be used.
        /// </summary>
        public Map map;

        /// <summary>
        /// Animation curve
        /// </summary>
        public AnimationCurve curve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        
        /// <summary>
        /// Duration of animation
        /// </summary>
        public float duration = 3;

        /// <summary>
        /// Radius of the circle
        /// </summary>
        public float radiusKM = 0.1f;

        /// <summary>
        /// Number of segments
        /// </summary>
        public int segments = 32;

        private List<BoundingItem> items;

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
            GeoPoint point = map.control.ScreenToLocation();

            // Create a new marker under cursor
            map.marker2DManager.Create(point, "Marker " + map.marker2DManager.count);

            if (items == null) items = new List<BoundingItem>();

            items.Add(new BoundingItem (this, point));
        }
        
        private void Update()
        {
            // If there are no items, we do not need to update them
            if (items == null || items.Count == 0) return;

            // Update items and remove completed
            items.RemoveAll(i => i.Update());

            // Redraw map
            map.Redraw();
        }

        /// <summary>
        /// Class that stores information about the circle
        /// </summary>
        public class BoundingItem
        {
            /// <summary>
            /// Center of the circle
            /// </summary>
            public GeoPoint center;

            /// <summary>
            /// Is the animation completed?
            /// </summary>
            public bool finished;

            /// <summary>
            /// Array of points of the circle
            /// </summary>
            public MercatorPoint[] points;

            /// <summary>
            /// Reference to the BouncingCircle instance
            /// </summary>
            private BouncingCircle instance;

            /// <summary>
            /// Animation progress
            /// </summary>
            private float progress;

            /// <summary>
            /// Polygon that displays the circle
            /// </summary>
            private Polygon polygon;

            /// <summary>
            /// Constructor
            /// </summary>
            /// <param name="instance">Reference to the BouncingCircle instance</param>
            /// <param name="center">Center of the circle</param>
            public BoundingItem(BouncingCircle instance, GeoPoint center)
            {
                this.instance = instance;
                this.center = center;
                
                // Create an array of points
                points = new MercatorPoint[instance.segments];
                
                // Create a polygon
                polygon = new Polygon(points, Color.red, 3);
                instance.map.drawingElementManager.Add(polygon);
            }

            /// <summary>
            /// Finish animation
            /// </summary>
            private void Finish()
            {
                progress = 1;
                finished = true;
                    
                // Remove polygon
                instance.map.drawingElementManager.Remove(polygon);
            }

            /// <summary>
            /// Update circle
            /// </summary>
            /// <returns>True - animation is completed, False - otherwise</returns>
            public bool Update()
            {
                // Update animation progress
                progress += Time.deltaTime / instance.duration;
                if (progress >= 1)
                {
                    Finish();
                    return true;
                }

                // Calculate radius
                float radius = instance.radiusKM * instance.curve.Evaluate(progress);

                // Find the coordinate at the desired distance
                GeoPoint distantCoordinate = center.Distant(radius, 90);

                // Get the projection of the map
                Projection projection = instance.map.view.projection;
                
                // Convert the coordinate under cursor to tile position
                MercatorPoint m1 = center.ToMercator(projection);

                // Convert remote coordinate to tile position
                MercatorPoint m2 = distantCoordinate.ToMercator(projection);

                // Calculate radius in tiles
                double r = m2.x - m1.x;

                int segments = points.Length;

                // Calculate a step
                double step = 360d / segments;

                // Calculate each point of circle
                for (int i = 0; i < segments; i++)
                {
                    double px = m1.x + Math.Cos(step * i * Constants.Deg2Rad) * r;
                    double py = m1.y + Math.Sin(step * i * Constants.Deg2Rad) * r;
                    points[i] = new MercatorPoint(px, py);
                }
                
                // Update polygon
                polygon.SetPoints(points);

                return false;
            }
        }
    }
}