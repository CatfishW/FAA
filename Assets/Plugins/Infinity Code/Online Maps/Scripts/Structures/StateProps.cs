/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using UnityEngine;

namespace OnlineMaps
{
    /// <summary>
    /// The main properties of the map
    /// </summary>
    public struct StateProps
    {
        /// <summary>
        /// Coordinates of the center point
        /// </summary>
        public GeoPoint center;
        
        /// <summary>
        /// Mercator coordinates of the center point
        /// </summary>
        public MercatorPoint centerMercator;

        /// <summary>
        /// Rect of the view
        /// </summary>
        public GeoRect rect;

        /// <summary>
        /// Width of the map
        /// </summary>
        public int width;

        /// <summary>
        /// Height of the map
        /// </summary>
        public int height;

        /// <summary>
        /// The scaling factor for zoom
        /// </summary>
        public float zoomFactor;

        /// <summary>
        /// The fractional part of zoom
        /// </summary>
        public float zoomFractional;

        /// <summary>
        /// The number of tiles in the current zoom level
        /// </summary>
        public int countTiles;

        /// <summary>
        /// The integer part of zoom
        /// </summary>
        public int intZoom;

        /// <summary>
        /// Float zoom
        /// </summary>
        public float zoom;

        public StateProps(Map map)
        {
            width = map.control.width;
            height = map.control.height;
            center = map.view.center;
            centerMercator = map.view.centerMercator;
            rect = map.view.rect;
            
            zoom = map.view.zoom;
            intZoom = (int) zoom;
            zoomFractional = zoom - intZoom;
            zoomFactor = Mathf.Pow(2, -zoomFractional);
            countTiles = 1 << intZoom;

        }
    }
}