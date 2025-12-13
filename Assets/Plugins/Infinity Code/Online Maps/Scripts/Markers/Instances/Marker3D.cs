/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace OnlineMaps
{
    /// <summary>
    /// 3D marker class.<br/>
    /// Can be used only when the source display - Plane or Tileset.
    /// </summary>
    [Serializable]
    public class Marker3D : Marker
    {
        /// <summary>
        /// Allows you to set a custom function to check the map boundaries.
        /// </summary>
        public Func<bool> OnCheckMapBoundaries;

        /// <summary>
        /// Altitude (meters).
        /// </summary>
        public float? altitude;

        /// <summary>
        /// Type of altitude
        /// </summary>
        public AltitudeType altitudeType = AltitudeType.absolute;

        /// <summary>
        /// Marker prefab GameObject.
        /// </summary>
        public GameObject prefab
        {
            get => _prefab;
            set
            {
                if (_prefab == value) return;
                _prefab = value;
                if (Utils.isPlaying && !Utils.doNotReinitFlag) Reinit();
            }
        }

        /// <summary>
        /// How marker size will be calculated.
        /// </summary>
        public SizeType sizeType = SizeType.scene;

        [SerializeField]
        protected GameObject _prefab;
        
        private Vector3 _relativePosition;
        private GameObject _usedPrefab;
        private bool _visible = true;
        
        /// <summary>
        /// Need to check the map boundaries?<br/>
        /// It allows you to make 3D markers, which are active outside the map.
        /// </summary>
        public bool checkMapBoundaries { get; set; } = true;
        
        /// <summary>
        /// Reference of 3D control.
        /// </summary>
        public ControlBase3D control { get; set; }

        /// <summary>
        /// Gets or sets marker enabled.
        /// </summary>
        /// <value>
        /// true if enabled, false if not.
        /// </value>
        public override bool enabled
        {
            set
            {
                if (_enabled != value)
                {
                    _enabled = value;

                    if (!value) visible = false;
                    else if (Utils.isPlaying) Update();

                    if (OnEnabledChange != null) OnEnabledChange(this);
                }
            }
        }

        protected ElevationManagerBase elevationManager => control.elevationManager;

        protected bool hasElevation => elevationManager && elevationManager.enabled;
        
        /// <summary>
        /// Specifies whether the marker is initialized.
        /// </summary>
        public bool initialized { get; set; }
        
        /// <summary>
        /// The instance.
        /// </summary>
        public GameObject instance { get; set; }

        /// <summary>
        /// Returns the position of the marker relative to Texture.
        /// </summary>
        /// <value>
        /// The relative position.
        /// </value>
        public Vector3 relativePosition => enabled ? _relativePosition : Vector3.zero;

        /// <summary>
        /// Gets or sets local rotation of 3D marker.
        /// </summary>
        public Quaternion localRotation
        {
            get => transform? transform.localRotation : Quaternion.identity;
            set
            {
                if (!transform) return;
                transform.localRotation = value;
                _rotation = value.eulerAngles.y;
            }
        }

        /// <summary>
        /// Y rotation of 3D marker (degree).
        /// </summary>
        public override float rotation
        {
            set
            {
                _rotation = value;
                localRotation = Quaternion.Euler(0, value, 0);
            }
        }

        /// <summary>
        /// Gets the instance transform.
        /// </summary>
        /// <value>
        /// The transform.
        /// </value>
        public Transform transform => instance? instance.transform: null;

        private bool visible
        {
            get => _visible;
            set
            {
                if (_visible == value) return;
                _visible = value;
                instance.SetActive(value);
            }
        }

        /// <summary>
        /// Constructor of 3D marker
        /// </summary>
        public Marker3D()
        {
        
        }

        /// <summary>
        /// Create 3D marker from an existing GameObject in scene.
        /// </summary>
        /// <param name="instance">GameObject to be used as a 3D marker.</param>
        public Marker3D(GameObject instance):this()
        {
            _usedPrefab = _prefab = instance;
            this.instance = instance;
            instance.AddComponent<Marker3DInstance>().marker = this;
            Update();
        }

        public override void DestroyInstance()
        {
            base.DestroyInstance();

            if (instance != null)
            {
                Utils.Destroy(instance);
                instance = null;
            }
        }

        /// <summary>
        /// Initializes this object.
        /// </summary>
        /// <param name="parent">
        /// The parent transform.
        /// </param>
        public void Init(Transform parent)
        {
            if (instance) Utils.Destroy(instance);

            if (!_prefab)
            {
                instance = GameObject.CreatePrimitive(PrimitiveType.Cube);
                instance.transform.localScale = Vector3.one;
            }
            else instance = Object.Instantiate(_prefab);

            _usedPrefab = _prefab;
        
            instance.transform.parent = parent;
            instance.transform.localRotation = Quaternion.Euler(0, _rotation, 0);

            instance.layer = map.gameObject.layer;
            instance.AddComponent<Marker3DInstance>().marker = this;
            _visible = false;
            instance.SetActive(false);
            initialized = true;

            control = map.control3D;

            Update();

            if (OnInitComplete != null) OnInitComplete(this);
        }

        public override void LookToLocation(GeoPoint location, float rotationOffset = 90)
        {
            rotation = (float)this.location.AngleInMercator(map, location) - rotationOffset;
        }

        /// <summary>
        /// Reinitialize this object.
        /// </summary>
        public void Reinit() => Reinit(map.view.rect, map.view.intZoom);

        /// <summary>
        /// Reinitialize this object.
        /// </summary>
        /// <param name="rect">Map rect</param>
        /// <param name="zoom">Map zoom</param>
        public void Reinit(GeoRect rect, int zoom)
        {
            if (instance)
            {
                Transform parent = instance.transform.parent;
                Utils.Destroy(instance);
                Init(parent);
            }

            _usedPrefab = _prefab;
            Update(rect, zoom);
            if (OnInitComplete != null) OnInitComplete(this);
        }
        
        /// <summary>
        /// Sets the prefab for the marker.
        /// </summary>
        /// <param name="gameObject">The GameObject to use as the marker's prefab.</param>
        /// <param name="isExistInScene">
        /// If true, the GameObject already exists in the scene and will be used directly as the marker instance.
        /// </param>
        public void SetPrefab(GameObject gameObject, bool isExistInScene = false)
        {
            prefab = gameObject;
            if (isExistInScene)
            {
                instance = gameObject;
                _usedPrefab = gameObject;
            }
        }

        public override JSONItem ToJSON()
        {
            return base.ToJSON().AppendObject(new
            {
                _prefab = _prefab ? _prefab.GetInstanceID() : 0,
                rotationY = _rotation,
                sizeType = (int)sizeType
            });
        }

        /// <summary>
        /// Updates marker instance.
        /// </summary>
        public override void Update() => Update(map.view.rect, map.view.intZoom);

        /// <summary>
        /// Updates marker instance.
        /// </summary>
        /// <param name="rect">Map rect</param>
        /// <param name="zoom">Zoom of the map</param>
        public override void Update(GeoRect rect, int zoom)
        {
            if (!control.meshFilter) return;

            TileRect tileRect = map.view.GetTileRect(zoom);

            float bestYScale = ElevationManagerBase.GetElevationScale(rect, control.elevationManager);
            Update(control.meshFilter.sharedMesh.bounds, rect, zoom, tileRect, bestYScale);
        }

        /// <summary>
        /// Updates marker instance.
        /// </summary>
        /// <param name="bounds">Bounds of the map mesh</param>
        /// <param name="rect">Coordinates of the area</param>
        /// <param name="zoom">Zoom of the map</param>
        /// <param name="tileRect">Tile rect of the area</param>
        /// <param name="bestYScale">Best y scale for current map view</param>
        public void Update(Bounds bounds, GeoRect rect, int zoom, TileRect tileRect, float bestYScale)
        {
            if (!enabled) return;
            if (!instance) Init(control.marker3DManager.container);

            if (!range.Contains(zoom)) visible = false;
            else if (OnCheckMapBoundaries != null) visible = OnCheckMapBoundaries();
            else if (checkMapBoundaries) visible = rect.ContainsWrapped(location);
            else visible = true;

            if (!visible) return;

            if (_prefab != _usedPrefab) Reinit(rect, zoom);

            TilePoint t = location.ToTile(map, zoom);

            int maxX = 1 << zoom;

            double sx = tileRect.width;
            double mpx = t.x - tileRect.left;
            if (sx <= 0) sx += maxX;

            if (checkMapBoundaries)
            {
                if (mpx < 0) mpx += maxX;
                else if (mpx > maxX) mpx -= maxX;
            }
            else
            {
                double dx1 = Math.Abs(mpx - tileRect.left);
                double dx2 = Math.Abs(mpx - tileRect.right);
                double dx3 = Math.Abs(mpx - tileRect.right + maxX);
                if (dx1 > dx2 && dx1 > dx3) mpx += maxX;
            }

            double px = mpx / sx;
            double pz = (tileRect.top - t.y) / -tileRect.height;

            _relativePosition = new Vector3((float)px, 0, (float)pz);

            TileSetControl tsControl = control as TileSetControl;

            if (tsControl)
            {
                px = -tsControl.sizeInScene.x / 2 - (px - 0.5) * tsControl.sizeInScene.x;
                pz = tsControl.sizeInScene.y / 2 + (pz - 0.5) * tsControl.sizeInScene.y;
            }
            else
            {
                Vector3 center = bounds.center;
                Vector3 size = bounds.size;
                px = center.x - (px - 0.5) * size.x / map.transform.lossyScale.x;
                pz = center.z + (pz - 0.5) * size.z / map.transform.lossyScale.z;
            }

            Vector3 oldPosition = instance.transform.localPosition;
            float y = 0;

            bool elevationActive = hasElevation;

            if (altitude.HasValue)
            {
                float yScale = ElevationManagerBase.GetElevationScale(rect, control.elevationManager);
                y = altitude.Value;
                if (altitudeType == AltitudeType.relative && tsControl && elevationActive) y += elevationManager.GetUnscaledElevationValue(px, pz, rect);
                y *= yScale;

                if (tsControl && elevationActive)
                {
                    if (elevationManager.bottomMode == ElevationBottomMode.minValue) y -= elevationManager.minValue * bestYScale;
                    y *= elevationManager.scale;
                }
            }
            else if (tsControl && elevationActive)
            {
                y = elevationManager.GetElevationValue(px, pz, bestYScale, rect);
            }

            Vector3 newPosition = new Vector3((float)px, y, (float)pz);

            if (sizeType == SizeType.meters)
            {
                Vector2d d = rect.Distances();
                if (tsControl) d.y = tsControl.sizeInScene.y / d.y / 1000;
                d.y *= scale;
                float fd = (float)d.y;

                instance.transform.localScale = new Vector3(fd, fd, fd);
            }

            if (oldPosition != newPosition) instance.transform.localPosition = newPosition;
        }

        /// <summary>
        /// Type of 3d marker size
        /// </summary>
        public enum SizeType
        {
            /// <summary>
            /// Uses transform.scale of marker instance. Same for each zoom level
            /// </summary>
            scene,

            /// <summary>
            /// Scale is 1 for zoom - 20, and is halved every previous zoom
            /// </summary>
            realWorld,

            /// <summary>
            /// Specific marker size in meters
            /// </summary>
            meters
        }
    }
}