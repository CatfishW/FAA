/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using UnityEngine;

namespace OnlineMaps
{
    /// <summary>
    /// Class control the map for the TileSet.
    /// TileSet - a dynamic mesh, created at runtime.
    /// </summary>
    [Serializable]
    [AddComponentMenu("Infinity Code/Online Maps/Controls/TileSet Control")]
    public class TileSetControl : ControlBaseDynamicMesh, ITileSetControl
    {
        #region Variables
        #region Actions
        /// <summary>
        /// The event, which occurs when the changed texture tile maps.
        /// </summary>
        public Action<Tile, Material> OnChangeMaterialTexture;

        /// <summary>
        /// The event that occurs after draw the tile.
        /// </summary>
        public Action<Tile, Material> OnDrawTile;

        /// <summary>
        /// The event that occurs after draw the tile. <br/>
        /// The difference with OnDrawTile is that it is always called for the original tile, whether or not it was loaded.
        /// </summary>
        public Action<Tile, Material> OnUpdateMapSubMeshLate;

        #endregion

        #region Public Fields

        /// <summary>
        /// Type of collider: box - for performance, mesh - for elevation.
        /// </summary>
        public ColliderType colliderType = ColliderType.fullMesh;

        /// <summary>
        /// Compress texture to reduce memory usage.
        /// </summary>
        public bool compressTextures = false;

        /// <summary>
        /// Plane by using which the map is dragged. Exists only during drag.
        /// </summary>
        public Plane? dragPlane;

        /// <summary>
        /// Drawing API mode (meshes or overlay).
        /// </summary>
        public TileSetDrawingMode drawingMode = TileSetDrawingMode.meshes;

        /// <summary>
        /// Whether the overlay from the parent tiles should be shown.
        /// </summary>
        public bool overlayFromParentTiles = true;

        /// <summary>
        /// Material that will be used for tile.
        /// </summary>
        public Material tileMaterial;

        /// <summary>
        /// Shader of map.
        /// </summary>
        public Shader tilesetShader;
        
        #endregion

        #region Private Fields

        /// <summary>
        /// Should the map use mipmaps for tiles.
        /// </summary>
        [SerializeField]
        private bool _mipmapForTiles;

        private IMapDrawer _mapDrawer;
        private RaycastHit lastRaycastHit;

        #endregion
        #endregion

        #region Properties

        public override Bounds bounds
        {
            get
            {
                Vector3 s = transform.lossyScale;
                Vector3 size = new Vector3(sizeInScene.x * s.x, ElevationManagerBase.GetElevationScale(map.view.rect, elevationManager), sizeInScene.y * s.z);
                size = transform.rotation * size;
                return new Bounds(transform.position + size / 2, size);
            }
        }

        /// <summary>
        /// Singleton instance of TileSetControl control.
        /// </summary>
        public new static TileSetControl instance => _instance as TileSetControl;
        
        public override bool mipmapForTiles
        {
            get => _mipmapForTiles;
            set => _mipmapForTiles = value;
        }

        public override MapTarget resultType => MapTarget.tileset;

        #endregion

        #region Methods

        public override bool HitTest(Vector2 position)
        {
#if NGUI
            if (UICamera.Raycast(position)) return false;
#endif
            Rect rect = currentCamera.rect;
            if (rect.width == 0 || rect.height == 0) return false;
            
            Ray ray = currentCamera.ScreenPointToRay(position);
            return mapDrawer.Raycast(ray, out lastRaycastHit, Constants.MaxRaycastDistance);
        }

        public override Vector2 LocationToScreen(double lng, double lat)
        {
            Vector2d p = LocationToLocal(lng, lat);
            p.x /= map.buffer.renderState.width;
            p.y /= map.buffer.renderState.height;

            double cpx = -sizeInScene.x * p.x;
            double cpy = sizeInScene.y * p.y;
            
            GeoRect r = map.view.rect;

            float elevation = 0;
            if (hasElevation)
            {
                float elevationScale = ElevationManagerBase.GetElevationScale(r, elevationManager);
                elevation = elevationManager.GetElevationValue(cpx, cpy, elevationScale, r);
            }

            Transform t = transform;
            Vector3 lossyScale = t.lossyScale;
            Vector3 worldPos = t.position + t.rotation * new Vector3((float)(cpx * lossyScale.x), elevation * lossyScale.y, (float)(cpy * lossyScale.z));

            return currentCamera.WorldToScreenPoint(worldPos);
        }

        public override void OnAwakeBefore()
        {
            base.OnAwakeBefore();

            mapDrawer = new TileSetDrawer(this);
            mapDrawer.Initialize();
            
            OnDrag += ProcessMapDrag;
            OnRelease += ProcessMapRelease;
        }

        protected override void OnDestroyLate()
        {
            base.OnDestroyLate();
            
            mapDrawer.Dispose();
            mapDrawer = null;
        }

        private void ProcessMapDrag()
        {
            if (dragPlane != null) return; 
            
            RaycastHit hit;
            Ray ray = currentCamera.ScreenPointToRay(InputManager.mousePosition);
            if (mapDrawer.Raycast(ray, out hit, Constants.MaxRaycastDistance))
            {
                dragPlane = new Plane(transform.up, new Vector3(0, hit.point.y, 0));
            }
        }

        private void ProcessMapRelease()
        {
            dragPlane = null;
        }

        /// <summary>
        /// Resize map
        /// </summary>
        /// <param name="width">Width (pixels)</param>
        /// <param name="height">Height (pixels)</param>
        /// <param name="changeSizeInScene">Change the size of the map in the scene or leave the same.</param>
        public void Resize(int width, int height, bool changeSizeInScene = true)
        {
            Resize(width, height, changeSizeInScene? new Vector2(width, height) : sizeInScene);
        }

        /// <summary>
        /// Resize map
        /// </summary>
        /// <param name="width">Width (pixels)</param>
        /// <param name="height">Height (pixels)</param>
        /// <param name="sizeX">Size X (in scene)</param>
        /// <param name="sizeZ">Size Z (in scene)</param>
        public void Resize(int width, int height, float sizeX, float sizeZ)
        {
            Resize(width, height, new Vector2(sizeX, sizeZ));
        }

        /// <summary>
        /// Resize map
        /// </summary>
        /// <param name="width">Width (pixels)</param>
        /// <param name="height">Height (pixels)</param>
        /// <param name="sizeInScene">Size in scene (X-X, Y-Z)</param>
        public void Resize(int width, int height, Vector2 sizeInScene)
        {
            SetSize(width, height);
            this.sizeInScene = sizeInScene;

            ReinitMapMesh();
            map.Redraw();
        }

        protected override JSONItem SaveSettings()
        {
            return base.SaveSettings().AppendObject(new
            {
                checkMarker2DVisibility,
                tileMaterial,
                tilesetShader,
                drawingShader,
                markerMaterial,
                markerShader
            });
        }

        public override bool ScreenToLocation(Vector2 position, out GeoPoint point)
        {
            point = GeoPoint.zero;

            if (!HitTest(position)) return false;
            point = WorldToLocation(lastRaycastHit.point);
            return true;
        }

        internal override bool ScreenToLocationInternal(out GeoPoint point)
        {
            Vector2 position = InputManager.mousePosition;
            if (dragPlane == null) return ScreenToLocation(position, out point);

            point = GeoPoint.zero;

            float distance;
            Ray ray = currentCamera.ScreenPointToRay(position);
            if (!dragPlane.Value.Raycast(ray, out distance)) return false;

            point = WorldToLocation(ray.GetPoint(distance));
            return true;
        }

        public override bool ScreenToTile(Vector2 position, out TilePoint tilePoint)
        {
            tilePoint = TilePoint.zero;
            if (!HitTest(position)) return false;
            tilePoint = WorldToTile(lastRaycastHit.point);
            return true;
        }

        internal override bool ScreenToTileInternal(Vector2 position, out TilePoint tilePoint)
        {
            if (dragPlane == null) return ScreenToTile(position, out tilePoint);

            tilePoint = TilePoint.zero;

            float distance;
            Ray ray = currentCamera.ScreenPointToRay(position);
            if (!dragPlane.Value.Raycast(ray, out distance)) return false;

            tilePoint = WorldToTile(ray.GetPoint(distance));
            return true;
        }

        public override void UpdateControl()
        {
            base.UpdateControl();

            if (OnUpdateMeshBefore != null) OnUpdateMeshBefore();
            mapDrawer.Draw();
            if (OnMeshUpdated != null) OnMeshUpdated();
            if (OnUpdateMeshAfter != null) OnUpdateMeshAfter();
            
            mapDrawer.DrawElements();
        }

        /// <summary>
        /// Returns the geographical coordinates by world position.
        /// </summary>
        /// <param name="position">World position</param>
        /// <returns>Geographical coordinates</returns>
        public GeoPoint WorldToLocation(Vector3 position)
        {
            Vector3 boundsSize = new Vector3(sizeInScene.x, 0, sizeInScene.y);
            boundsSize.Scale(transform.lossyScale);
            Vector3 size = new Vector3(0, 0, sizeInScene.y * transform.lossyScale.z) - Quaternion.Inverse(transform.rotation) * (position - transform.position);

            size.x /= boundsSize.x;
            size.z /= boundsSize.z;

            Vector2 r = new Vector3(size.x - .5f, size.z - .5f);

            float zoomFactor = map.view.zoomFactor;
            int countX = map.buffer.renderState.width / Constants.TileSize;
            int countY = map.buffer.renderState.height / Constants.TileSize;
            
            TilePoint t = map.view.centerTile;
            t.Add(countX * r.x * zoomFactor, -countY * r.y * zoomFactor);
            return t.ToLocation(map);
        }

        /// <summary>
        /// Returns the tile coordinates by world position.
        /// </summary>
        /// <param name="worldPosition">World position</param>
        /// <returns>Tile coordinates</returns>
        public TilePoint WorldToTile(Vector3 worldPosition)
        {
            Vector3 boundsSize = new Vector3(sizeInScene.x, 0, sizeInScene.y);
            Transform t = transform;
            boundsSize.Scale(t.lossyScale);
            Vector3 size = new Vector3(0, 0, boundsSize.z) - Quaternion.Inverse(t.rotation) * (worldPosition - t.position);

            size.x /= boundsSize.x;
            size.z /= boundsSize.z;

            Vector2 r = new Vector2(size.x - .5f, size.z - .5f);

            float zoomFactor = map.view.zoomFactor;
            int countX = map.buffer.renderState.width / Constants.TileSize;
            int countY = map.buffer.renderState.height / Constants.TileSize;

            TilePoint centerTile = map.view.centerTile;
            centerTile.Add(countX * r.x * zoomFactor, -countY * r.y * zoomFactor);
            return centerTile;
        }

        #endregion
    }
}