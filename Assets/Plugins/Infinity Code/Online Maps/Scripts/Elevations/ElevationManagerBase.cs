/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using UnityEngine;

namespace OnlineMaps
{
    /// <summary>
    /// Base class for components that implement elevations
    /// </summary>
    [DisallowMultipleComponent]
    public abstract class ElevationManagerBase : MonoBehaviour
    {
        #region Variables

        /// <summary>
        /// Instance of the elevation manager base class.
        /// </summary>
        protected static ElevationManagerBase _instance;

        /// <summary>
        /// Called when downloading elevation data failed.
        /// </summary>
        public Action<string> OnElevationFails;

        /// <summary>
        /// Called when downloading of elevation data began.
        /// </summary>
        public Action OnElevationRequested;

        /// <summary>
        /// Called when elevation data has been updated.
        /// </summary>
        public Action OnElevationUpdated;

        /// <summary>
        /// Called when downloading of elevation data for an area begins.
        /// </summary>
        public Action<GeoRect> OnGetElevation;

        /// <summary>
        /// The rule for calculating the lowest point of the map mesh.
        /// </summary>
        public ElevationBottomMode bottomMode = ElevationBottomMode.zero;

        /// <summary>
        /// Scale of elevation values.
        /// </summary>
        public float scale = 1;

        /// <summary>
        /// Range when elevations will be shown.
        /// </summary>
        public LimitedRange zoomRange = new LimitedRange(11, Constants.MaxZoomExt);

        /// <summary>
        /// Lock yScale value.
        /// </summary>
        public bool lockYScale;

        /// <summary>
        /// Fixed yScale value.
        /// </summary>
        public float yScaleValue = 1;

        /// <summary>
        /// Size of the map in the scene.
        /// </summary>
        protected Vector2 _sizeInScene;

        /// <summary>
        /// Position of the elevation buffer.
        /// </summary>
        protected Vector2Int elevationBufferPosition;

        private ControlBaseDynamicMesh _control;
        private Map _map;

        #endregion

        #region Properties

        #region Static

        protected Vector2Int bufferPosition => map.buffer.bufferPosition;

        protected ControlBaseDynamicMesh control => map.control as ControlBaseDynamicMesh;

        /// <summary>
        /// Instance of elevation manager
        /// </summary>
        public static ElevationManagerBase instance => _instance;

        protected Map map
        {
            get
            {
                if (!_map) _map = GetComponent<Map>();
                return _map;
            }
        }

        protected Vector2 sizeInScene => _sizeInScene;

        /// <summary>
        /// Elevation manager is active?
        /// </summary>
        public static bool isActive => _instance && _instance.enabled;

        /// <summary>
        /// Are elevations used for map?
        /// </summary>
        public static bool useElevation => isActive && _instance.zoomRange.Contains(_instance.map.view.intZoom) && _instance.hasData;

        #endregion

        /// <summary>
        /// Elevation manager has elevation data.
        /// </summary>
        public abstract bool hasData { get; }

        /// <summary>
        /// The maximum elevation value.
        /// </summary>
        public short maxValue { get; set; }

        /// <summary>
        /// The minimum elevation value.
        /// </summary>
        public short minValue { get; set; }

        #endregion

        #region Methods

        /// <summary>
        /// Cancel current elevation request.
        /// </summary>
        public abstract void CancelCurrentElevationRequest();

        /// <summary>
        /// Returns yScale for an area.
        /// </summary>
        /// <returns>yScale for an area</returns>
        public float GetElevationScale()
        {
            return GetElevationScale(map.view.rect);
        }

        /// <summary>
        /// Returns yScale for an area.
        /// </summary>
        /// <param name="rect">Area</param>
        /// <returns>yScale for an area</returns>
        public float GetElevationScale(GeoRect rect)
        {
            return !lockYScale ? GetElevationScale(control, rect) : yScaleValue;
        }

        /// <summary>
        /// Returns yScale for an area.
        /// </summary>
        /// <param name="manager">Elevation manager</param>
        /// <param name="rect">Area</param>
        /// <returns>yScale for an area</returns>
        public static float GetElevationScale(GeoRect rect = default, ElevationManagerBase manager = null)
        {
            if (!manager) manager = _instance;
            if (manager && rect == default) rect = manager.map.view.rect;
            
            if (manager && manager.lockYScale) return manager.yScaleValue;

            Map map = manager ? manager.map : Map.instance;
            if (!map) return 0;

            ControlBaseDynamicMesh control = map.control as ControlBaseDynamicMesh;
            if (!control) return 0;

            return GetElevationScale(control, rect);
        }

        private static float GetElevationScale(ControlBaseDynamicMesh control, GeoRect rect)
        {
            Vector2d d = GeoMath.Distances(rect.topLeft, rect.bottomRight);
            d.x = d.x / control.sizeInScene.x * control.width;
            d.y = d.y / control.sizeInScene.y * control.height;
            return (float)Math.Min(control.width / d.x, control.height / d.y) / 1000;
        }

        /// <summary>
        /// Returns the elevation value for a point in the scene relative to the left-top corner of the map.
        /// </summary>
        /// <param name="x">Point X</param>
        /// <param name="z">Point Z</param>
        /// <param name="yScale">Scale factor</param>
        /// <param name="rect">Area</param>
        /// <returns>Elevation value</returns>
        public abstract float GetElevationValue(double x, double z, float yScale, GeoRect rect);

        /// <summary>
        /// Returns the scaled elevation value for a point in the scene relative to left-top corner of the map.
        /// </summary>
        /// <param name="x">Point X</param>
        /// <param name="z">Point Y</param>
        /// <param name="yScale">Scale factor</param>
        /// <returns>Elevation value</returns>
        public static float GetElevation(double x, double z, float? yScale = null)
        {
            if (!_instance || !_instance.enabled) return 0;

            GeoRect r = _instance.map.view.rect;

            if (!yScale.HasValue) yScale = _instance.GetElevationScale(r);
            return GetElevation(x, z, yScale.Value, r);
        }

        /// <summary>
        /// Returns the scaled elevation value for a point in the scene relative to left-top corner of the map.
        /// </summary>
        /// <param name="x">Point X</param>
        /// <param name="z">Point Y</param>
        /// <param name="yScale">Scale factor</param>
        /// <param name="rect">Area</param>
        /// <returns>Elevation value</returns>
        public static float GetElevation(double x, double z, float yScale, GeoRect rect)
        {
            if (!_instance || !_instance.enabled) return 0;
            return _instance.GetElevationValue(x, z, yScale, rect);
        }

        /// <summary>
        /// Returns the maximum known elevation value
        /// </summary>
        /// <param name="yScale">Scale factor</param>
        /// <returns>Maximum known elevation value</returns>
        public float GetMaxElevation(float yScale)
        {
            return hasData ? maxValue * yScale * scale : 0;
        }

        /// <summary>
        /// Returns the minimum known elevation value
        /// </summary>
        /// <param name="yScale">Scale factor</param>
        /// <returns>Minimum known elevation value</returns>
        public float GetMinElevation(float yScale)
        {
            return hasData ? minValue * yScale * scale : 0;
        }

        /// <summary>
        /// Returns the unscaled elevation value for a point in the scene relative to left-top corner of the map.
        /// </summary>
        /// <param name="x">Point X</param>
        /// <param name="z">Point Z</param>
        /// <returns>Elevation value</returns>
        public static float GetUnscaledElevation(double x, double z)
        {
            if (!_instance || !_instance.enabled) return 0;

            GeoRect r = _instance.map.view.rect;
            return GetUnscaledElevation(x, z, r);
        }

        /// <summary>
        /// Returns the unscaled elevation value for a point in the scene relative to left-top corner of the map.
        /// </summary>
        /// <param name="x">Point X</param>
        /// <param name="z">Point Z</param>
        /// <param name="rect">Area</param>
        /// <returns>Elevation value</returns>
        public static float GetUnscaledElevation(double x, double z, GeoRect rect)
        {
            if (_instance && _instance.enabled) return _instance.GetUnscaledElevationValue(x, z, rect);
            return 0;
        }

        /// <summary>
        /// Returns the unscaled elevation value for a point in the scene relative to the left-top corner of the map.
        /// </summary>
        /// <param name="x">Point X</param>
        /// <param name="z">Point Z</param>
        /// <param name="rect">Area</param>
        /// <returns>Elevation value</returns>
        public abstract float GetUnscaledElevationValue(double x, double z, GeoRect rect);

        /// <summary>
        /// Returns the unscaled elevation value for a coordinate.
        /// </summary>
        /// <param name="lng">Longitude</param>
        /// <param name="lat">Latitude</param>
        /// <returns>Elevation value</returns>
        public static float GetUnscaledElevationByCoordinate(double lng, double lat)
        {
            if (!_instance || !_instance.enabled) return 0;

            ControlBaseDynamicMesh control = _instance.control;
            GeoRect r = _instance.map.view.rect;
            Vector2d p = control.LocationToLocal(lng, lat);
            p.x = -control.sizeInScene.x / control.width;
            p.y = control.sizeInScene.y / control.height;

            return _instance.GetUnscaledElevationValue(p.x, p.y, r);
        }

        /// <summary>
        /// Downloads new elevation data for area
        /// </summary>
        public abstract void RequestNewElevationData();

        /// <summary>
        /// Called when the script instance is being loaded.
        /// </summary>
        protected virtual void Start()
        {
        }

        /// <summary>
        /// Called every frame, if the MonoBehaviour is enabled.
        /// </summary>
        protected virtual void Update()
        {
        }

        /// <summary>
        /// Updates the minimum and maximum elevation values.
        /// </summary>
        protected virtual void UpdateMinMax()
        {
        }

        #endregion
    }
}