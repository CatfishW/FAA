/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using UnityEngine;

namespace OnlineMaps
{
    /// <summary>
    /// Class to limit the position and zoom of the map.
    /// </summary>
    [AddComponentMenu("Infinity Code/Online Maps/Plugins/Limits")]
    [Serializable]
    [Plugin("Limits")]
    public class Limits : MonoBehaviour, ISavable
    {
        /// <summary>
        /// The minimum zoom value.
        /// </summary>
        public float minZoom = Constants.MinZoom;

        /// <summary>
        /// The maximum zoom value. 
        /// </summary>
        public float maxZoom = Constants.MaxZoomExt;

        /// <summary>
        /// The minimum latitude value.
        /// </summary>
        public float minLatitude = -90;

        /// <summary>
        /// The maximum latitude value. 
        /// </summary>
        public float maxLatitude = 90;

        /// <summary>
        /// The minimum longitude value.
        /// </summary>
        public float minLongitude = -180;

        /// <summary>
        /// The maximum longitude value. 
        /// </summary>
        public float maxLongitude = 180;

        /// <summary>
        /// Type of limitation position map.
        /// </summary>
        public LocationRangeType locationRangeType = LocationRangeType.center;

        /// <summary>
        /// Flag indicating that need to limit the zoom.
        /// </summary>
        public bool useZoomRange;

        /// <summary>
        /// Flag indicating that need to limit the position.
        /// </summary>
        public bool useLocationRange;
        
        private Map map;

        private void OnEnable()
        {
            map = GetComponent<Map>();
        }

        private void Start()
        {
            if (useZoomRange) map.view.zoomRange = new LimitedRange(minZoom, maxZoom);
            if (useLocationRange) map.view.locationRange = new LocationRange(minLatitude, minLongitude, maxLatitude, maxLongitude, locationRangeType);
        }
    }
}