/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using UnityEngine;

namespace OnlineMaps
{
    /// <summary>
    /// Class implements the basic functionality control of the 3D map.
    /// </summary>
    [Serializable]
    [RequireComponent(typeof(Marker3DManager))]
    public abstract class ControlBase3D: ControlBase
    {
        #region Variables

        /// <summary>
        /// Called when drawing 3d markers.
        /// </summary>
        public Action OnUpdate3DMarkers;

        /// <summary>
        /// The camera you are using to display the map.
        /// </summary>
        public Camera activeCamera;

        /// <summary>
        /// Reference to the elevation manager
        /// </summary>
        public ElevationManagerBase elevationManager;

        /// <summary>
        /// Mode of 2D markers. Bake in texture or Billboard.
        /// </summary>
        public Marker2DMode marker2DMode = Marker2DMode.flat;

        /// <summary>
        /// Size of billboard markers.
        /// </summary>
        public float marker2DSize = 100;

        /// <summary>
        /// Reference to marker 3D manager
        /// </summary>
        public Marker3DManager marker3DManager;
        
        private Marker3DDrawer _marker3DDrawer;
        private MeshFilter _meshFilter;
        private Renderer _renderer;

        #endregion

        #region Properties

        /// <summary>
        /// Reference to current camera
        /// </summary>
        public Camera currentCamera => activeCamera ?? Camera.main;
        
        public abstract Bounds bounds { get; }
        
        /// <summary>
        /// Returns true when the elevation manager is available and enabled.
        /// </summary>
        public bool hasElevation => elevationManager && elevationManager.enabled;

        /// <summary>
        /// Singleton instance of ControlBase3D control.
        /// </summary>
        public new static ControlBase3D instance => _instance as ControlBase3D;

        /// <summary>
        /// MeshFilter of the map
        /// </summary>
        public MeshFilter meshFilter
        {
            get
            {
                if (!_meshFilter) _meshFilter = GetComponent<MeshFilter>();
                return _meshFilter;
            }
        }

        /// <summary>
        /// Get/set marker 3D drawer
        /// </summary>
        public Marker3DDrawer marker3DDrawer
        {
            get { return _marker3DDrawer; }
            set
            {
                if (_marker3DDrawer != null) _marker3DDrawer.Dispose();
                _marker3DDrawer = value;
            }
        }

        /// <summary>
        /// Reference to the renderer.
        /// </summary>
        public Renderer rendererInstance
        {
            get
            {
                if (!_renderer) _renderer = GetComponent<Renderer>();
                return _renderer;
            }
        }

        #endregion

        #region Methods

        protected override void AfterUpdate()
        {
            base.AfterUpdate();

            Vector2 inputPosition = InputManager.mousePosition;

            if (map.showMarkerTooltip == ShowMarkerTooltip.onHover && !map.blockAllInteractions)
            {
                MarkerInstanceBase markerInstance = GetBillboardMarkerFromScreen(inputPosition);
                if (markerInstance != null)
                {
                    TooltipDrawerBase.tooltip = markerInstance.marker.label;
                    TooltipDrawerBase.tooltipMarker = markerInstance.marker;
                }
            }
        }

        /// <summary>
        /// Gets billboard marker on the screen position.
        /// </summary>
        /// <param name="screenPosition">Screen position.</param>
        /// <returns>Marker instance or null.</returns>
        public MarkerInstanceBase GetBillboardMarkerFromScreen(Vector2 screenPosition)
        {
            //TODO: Find a way to refactory this method
            RaycastHit hit;
            if (Physics.Raycast(currentCamera.ScreenPointToRay(screenPosition), out hit, Constants.MaxRaycastDistance))
            {
                return hit.collider.gameObject.GetComponent<MarkerInstanceBase>();
            }
            return null;
        }

        public override IInteractiveElement GetInteractiveElement(Vector2 screenPosition)
        {
            if (IsCursorOnUIElement(screenPosition)) return null;

            //TODO: Find a way to refactory this method
            RaycastHit hit;
            if (Physics.Raycast(currentCamera.ScreenPointToRay(screenPosition), out hit, Constants.MaxRaycastDistance))
            {
                MarkerInstanceBase markerInstance = hit.collider.gameObject.GetComponent<MarkerInstanceBase>();
                if (markerInstance != null) return markerInstance.marker;
            }

            Marker2D marker = marker2DDrawer.GetMarkerFromScreen(screenPosition);
            if (marker != null) return marker;

            DrawingElement drawingElement = map.GetDrawingElement(screenPosition);
            return drawingElement;
        }

        public override Vector2 LocationToScreen(double lng, double lat)
        {
            Vector2d p = LocationToLocal(lng, lat);
            p.x /= width;
            p.y /= height;
            
            Vector3 worldPos = new Vector3(
                (float)(bounds.max.x - bounds.size.x * p.x),
                bounds.min.y,
                (float)(bounds.min.z + bounds.size.z * p.y)
            );

            return currentCamera.WorldToScreenPoint(worldPos);
        }

        protected override void  OnDestroyLate()
        {
            base.OnDestroyLate();

            marker3DDrawer = null;
            _collider = null;
            _meshFilter = null;
            _renderer = null;
        }

        protected override void OnEnableLate()
        {
            base.OnEnableLate();

            marker3DManager = GetComponent<Marker3DManager>();
            elevationManager = GetComponent<ElevationManagerBase>();

            Marker3DManager.Init();
            marker3DDrawer = new Marker3DDrawer(this);
            if (!activeCamera) activeCamera = Camera.main;
        }

        protected override JSONItem SaveSettings()
        {
            JSONItem json = base.SaveSettings();
            json.AppendObject(new
            {
                marker2DMode,
                marker2DSize,
                activeCamera
            });

            return json;
        }

        /// <summary>
        /// Updates the current control.
        /// </summary>
        public override void UpdateControl()
        {
            base.UpdateControl();
            
            if (OnDrawMarkers != null) OnDrawMarkers();
            if (OnUpdate3DMarkers != null) OnUpdate3DMarkers();
        }

        #endregion

        #region Obsolete
        
        private Collider _collider;

        [Obsolete]
        public new Collider collider
        {
            get
            {
                if (!_collider) _collider = GetComponent<Collider>();
                return _collider;
            }
        }

        #endregion
    }
}