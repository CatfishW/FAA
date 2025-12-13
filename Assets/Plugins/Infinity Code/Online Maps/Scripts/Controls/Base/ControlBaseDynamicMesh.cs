/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using UnityEngine;

namespace OnlineMaps
{
    /// <summary>
    /// The base class that implements the display of the map on the dynamic mesh.
    /// </summary>
    [WizardControlHelper(MapTarget.mesh)]
    public abstract class ControlBaseDynamicMesh : ControlBase3D
    {
        #region Variables

        /// <summary>
        /// Event that occurs after the map mesh has been updated.
        /// </summary>
        public Action OnMeshUpdated;

        /// <summary>
        /// The event is called after updating the map mesh.
        /// </summary>
        public Action OnUpdateMeshAfter;

        /// <summary>
        /// The event is called before updating the map mesh
        /// </summary>
        public Action OnUpdateMeshBefore;

        /// <summary>
        /// Type of checking 2D markers on visibility.
        /// </summary>
        public TileSetCheckMarker2DVisibility checkMarker2DVisibility = TileSetCheckMarker2DVisibility.pivot;
        
        /// <summary>
        /// Shader of drawing elements.
        /// </summary>
        public Shader drawingShader;

        /// <summary>
        /// Resolution of the elevation map.
        /// </summary>
        public int elevationResolution = 32;

        /// <summary>
        /// Material that will be used for marker.
        /// </summary>
        public Material markerMaterial;

        /// <summary>
        /// Shader of markers.
        /// </summary>
        public Shader markerShader;

        /// <summary>
        /// Size of the map in the scene
        /// </summary>
        public Vector2 sizeInScene = new Vector2(Constants.DefaultMapSize, Constants.DefaultMapSize);
        
        [SerializeField]
        protected int _width = Constants.DefaultMapSize;

        [SerializeField]
        protected int _height = Constants.DefaultMapSize;

        private Vector2Int _bufferPosition;
        private Vector2 lastSizeInScene;

        #endregion

        #region Properties

        /// <summary>
        /// The center point of the map (without elevations) in local space.
        /// </summary>
        public Vector3 center => new(sizeInScene.x / -2, 0, sizeInScene.y / 2);

        public override int height => _height;

        /// <summary>
        /// Singleton instance of ControlBaseDynamicMesh.
        /// </summary>
        public new static ControlBaseDynamicMesh instance => _instance as ControlBaseDynamicMesh;

        public override MapTarget resultType => MapTarget.mesh;

        public override int width => _width;

        #endregion

        #region Methods

        public override Vector2d LocationToLocal(double longitude, double latitude)
        {
            const short tileSize = Constants.TileSize;
            
            TilePoint d = map.view.projection.LocationToTile(longitude, latitude, map.view.intZoom);
            d -= map.view.topLeftTile;

            int maxTiles = map.view.maxTiles;
            if (d.x < -(maxTiles >> 1)) d.x += maxTiles;
            if (d.x < 0 && width == (long)maxTiles * tileSize) d.x += map.view.countTilesX;
            d *= tileSize / map.view.zoomFactor;
            return d;
        }

        public override Vector2 LocationToScreen(double lng, double lat)
        {
            Vector2d p = LocationToLocal(lng, lat);
            p.x /= width;
            p.y /= height;
            Rect mapRect = GetScreenRect();
            p.x = mapRect.x + mapRect.width * p.x;
            p.y = mapRect.y + mapRect.height - mapRect.height * p.y;
            return p;
        }

        /// <summary>
        /// Converts geographical coordinates to position in world space.
        /// </summary>
        /// <param name="location">Geographical coordinates.</param>
        /// <returns>Position in world space.</returns>
        public Vector3 LocationToWorld(GeoPoint location) => LocationToWorld(location.x, location.y);

        /// <summary>
        /// Converts geographical coordinates to position in world space.
        /// </summary>
        /// <param name="longitude">Longitude</param>
        /// <param name="latitude">Latitude</param>
        /// <returns></returns>
        public Vector3 LocationToWorld(double longitude, double latitude)
        {
            Vector2d p = LocationToLocal(longitude, latitude);

            double px = -p.x / width * sizeInScene.x;
            double pz = p.y / height * sizeInScene.y;

            Vector3 offset = transform.rotation * new Vector3((float)px, 0, (float)pz);
            offset.Scale(map.transform.lossyScale);

            return map.transform.position + offset;
        }

        /// <summary>
        /// Converts geographical coordinates to position in world space with elevation.
        /// </summary>
        /// <param name="longitude">Longitude</param>
        /// <param name="latitude">Latitude</param>
        /// <returns>Position in world space.</returns>
        public Vector3 LocationToWorld3(double longitude, double latitude) => LocationToWorld3(longitude, latitude, map.view.rect);

        /// <summary>
        /// Converts geographical coordinates to position in world space with elevation.
        /// </summary>
        /// <param name="location">Geographical coordinates.</param>
        /// <param name="rect">Rect of the area.</param>
        /// <returns>Position in world space.</returns>
        public Vector3 LocationToWorld3(GeoPoint location, GeoRect rect) => LocationToWorld3(location.x, location.y, rect);

        /// <summary>
        /// Converts geographical coordinates to position in world space with elevation.
        /// </summary>
        /// <param name="lng">Longitude</param>
        /// <param name="lat">Latitude</param>
        /// <param name="rect">Rect of the area.</param>
        /// <returns>Position in world space.</returns>
        public Vector3 LocationToWorld3(double lng, double lat, GeoRect rect)
        {
            Vector2d p = LocationToLocal(lng, lat);

            p.x *= -sizeInScene.x / width;
            p.y *= sizeInScene.y / height;

            float yScale = ElevationManagerBase.GetElevationScale(rect, elevationManager);
            float y = hasElevation ? elevationManager.GetElevationValue(p.x, p.y, yScale, rect) : 0;

            Vector3 offset = transform.rotation * new Vector3((float)p.x, y, (float)p.y);
            offset.Scale(map.transform.lossyScale);

            return map.transform.position + offset;
        }

        /// <summary>
        /// Converts geographical coordinates to position in world space with elevation.
        /// </summary>
        /// <param name="location">Geographical coordinates.</param>
        /// <returns>Position in world space.</returns>
        public Vector3 LocationToWorld3(GeoPoint3 location) => LocationToWorld3(location.x, location.y, location.altitude);

        /// <summary>
        /// Converts geographical coordinates to position in world space with elevation.
        /// </summary>
        /// <param name="lng">Longitude</param>
        /// <param name="lat">Latitude</param>
        /// <param name="altitude">Altitude</param>
        /// <returns>Position in world space.</returns>
        public Vector3 LocationToWorld3(double lng, double lat, double altitude) => LocationToWorld3(lng, lat, altitude, map.view.rect);

        /// <summary>
        /// Converts geographical coordinates to position in world space with elevation.
        /// </summary>
        /// <param name="lng">Longitude</param>
        /// <param name="lat">Latitude</param>
        /// <param name="altitude">Altitude</param>
        /// <param name="rect">Rect of the area.</param>
        /// <returns>Position in world space.</returns>
        public Vector3 LocationToWorld3(double lng, double lat, double altitude, GeoRect rect)
        {
            Vector2d p = LocationToLocal(lng, lat);

            p.x *= -sizeInScene.x / width;
            p.y *= sizeInScene.y / height;
            
            double y = altitude;

            if (hasElevation)
            {
                float yScale = ElevationManagerBase.GetElevationScale(rect, elevationManager);
                y *= yScale;
                if (elevationManager.bottomMode == ElevationBottomMode.minValue) y -= elevationManager.minValue * yScale;
                y *= elevationManager.scale;
            }

            Vector3 offset = transform.rotation * new Vector3((float)p.x, (float)y, (float)p.y);
            offset.Scale(map.transform.lossyScale);

            return map.transform.position + offset;
        }

        protected override void OnEnableLate()
        {
            base.OnEnableLate();

            lastSizeInScene = sizeInScene;

            if (marker2DMode == Marker2DMode.flat)
            {
                marker2DDrawer = new MarkerFlatDrawer(this);
            }
            else
            {
                marker2DDrawer = new MarkerBillboardDrawer(this);
            }
        }

        protected virtual void ReinitMapMesh()
        {

        }

        /// <summary>
        /// Sets the size of the map.
        /// </summary>
        /// <param name="width">New width of the map.</param>
        /// <param name="height">New height of the map.</param>
        public virtual void SetSize(int width, int height)
        {
            _width = width;
            _height = height;
            
            map?.view?.SetSize(_width, _height);
        }

        public override void UpdateControl()
        {
            base.UpdateControl();

            if (sizeInScene != lastSizeInScene)
            {
                ReinitMapMesh();
                lastSizeInScene = sizeInScene;
            }
        }

        #endregion

        #region Obsolete

        [Obsolete]
        public Vector2Int bufferPosition => map.buffer.bufferPosition;

        #endregion
    }
}