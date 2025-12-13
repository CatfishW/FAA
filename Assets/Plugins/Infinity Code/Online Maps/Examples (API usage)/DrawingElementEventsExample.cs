/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System.Collections.Generic;
using OnlineMaps;
using UnityEngine;

namespace OnlineMapsExamples
{
    /// <summary>
    /// Example of using the events of DrawingElement.
    /// </summary>
    [AddComponentMenu(Utils.ExampleMenuPath + "DrawingElementEventsExample")]
    public class DrawingElementEventsExample : MonoBehaviour
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

            // Create a new rect element.
            Rectangle rect = new Rectangle(-119.0807f, 34.58658f, 3, 3, Color.black, 1f,
                Color.blue);

            // Subscribe to events.
            rect.OnClick += OnClick;
            rect.OnPress += OnPress;
            rect.OnRelease += OnRelease;
            rect.OnDoubleClick += OnDoubleClick;

            // Add element to map.
            map.drawingElementManager.Add(rect);

            // Create a new poly element.
            List<GeoPoint> polyPoints = new List<GeoPoint>
            {
                //Geographic coordinates
                new GeoPoint(0, 0),
                new GeoPoint(1, 0),
                new GeoPoint(2, 2),
                new GeoPoint(0, 1)
            };

            Polygon polygon = new Polygon(
                polyPoints, // List of points
                Color.red, // Border color
                1f // Border width
            );

            // Create tooltip for poly.
            polygon.tooltip = "Drawing Element";

            // Subscribe to events.
            polygon.OnClick += OnClick;
            polygon.OnPress += OnPress;
            polygon.OnRelease += OnRelease;
            polygon.OnDoubleClick += OnDoubleClick;

            // Add element to map.
            map.drawingElementManager.Add(polygon);
        }

        private void OnDoubleClick(DrawingElement drawingElement)
        {
            Debug.Log("OnDoubleClick");
        }

        private void OnRelease(DrawingElement drawingElement)
        {
            Debug.Log("OnRelease");
        }

        private void OnPress(DrawingElement drawingElement)
        {
            Debug.Log("OnPress");
        }

        private void OnClick(DrawingElement drawingElement)
        {
            Debug.Log("OnClick");
        }
    }
}