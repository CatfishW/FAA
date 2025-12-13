/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System.Collections.Generic;
using System.Linq;
using OnlineMaps;
using UnityEngine;

namespace OnlineMapsExamples
{
    /// <summary>
    /// Example of how to change the sort order of the markers.
    /// </summary>
    [AddComponentMenu(Utils.ExampleMenuPath + "TilesetMarkerDepthExample")]
    public class TilesetMarkerDepthExample : MonoBehaviour
    {
        /// <summary>
        /// Reference to the control. If not specified, the current instance will be used.
        /// </summary>
        public TileSetControl control;
        
        private void Start()
        {
            // If the control is not specified, get the current instance.
            if (!control && !(control = GetComponent<TileSetControl>()))
            {
                Debug.LogError("TileSetControl not found");
                return;
            }

            // Create markers.
            control.marker2DManager.Create(0, 0);
            control.marker2DManager.Create(0, 0.01f);
            control.marker2DManager.Create(0, -0.01f);

            // Sets a new comparer.
            MarkerFlatDrawer drawer = control.marker2DDrawer as MarkerFlatDrawer;
            if (drawer != null) drawer.markerComparer = new MarkerComparer();

            // Get the center point and zoom the best for all markers.
            (GeoPoint center, int zoom) = GeoMath.CenterPointAndZoom(control.marker2DManager.ToArray());

            // Change the position and zoom of the map.
            control.map.view.SetCenter(center, zoom);
        }

        /// <summary>
        /// Defines a new comparer.
        /// </summary>
        public class MarkerComparer : IComparer<Marker2D>
        {
            public int Compare(Marker2D m1, Marker2D m2)
            {
                if (m1.location.y > m2.location.y) return -1;
                if (m1.location.y < m2.location.y) return 1;
                return 0;
            }
        }
    }
}