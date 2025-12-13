/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using OnlineMaps;
using UnityEngine;

namespace OnlineMapsExamples
{
    /// <summary>
    /// Example how to create your own marker drawer for uGUI
    /// </summary>
    [AddComponentMenu(Utils.ExampleMenuPath + "uGUIMarkerDrawer")]
    public class uGUIMarkerDrawer : MonoBehaviour
    {
        private static uGUIMarkerDrawer instance;

        /// <summary>
        /// Reference to the control
        /// </summary>
        public ControlBase control;

        /// <summary>
        /// Reference to container of the markers
        /// </summary>
        public RectTransform container;

        /// <summary>
        /// Prefab of the marker
        /// </summary>
        public GameObject prefab;

        /// <summary>
        /// Reference to the canvas
        /// </summary>
        private Canvas canvas;

        /// <summary>
        /// Gets a world camera
        /// </summary>
        private Camera worldCamera
        {
            get
            {
                if (canvas.renderMode == RenderMode.ScreenSpaceOverlay) return null;
                return canvas.worldCamera;
            }
        }

        /// <summary>
        /// This method is called each time the script is enabled
        /// </summary>
        private void OnEnable()
        {
            // Save the references to this drawer and canvas
            instance = this;
            canvas = container.GetComponentInParent<Canvas>();
            
            if (control != null)
            {
                // Create and register a new marker drawer
                control.marker2DDrawer = new Drawer(control);
            }
        }

        /// <summary>
        /// This method is called when the script starts
        /// </summary>
        private void Start()
        {
            if (!control && !(control = ControlBase.instance))
            {
                Debug.LogError("Can't find Control");
                return;
            }
            
            // Create and register a new marker drawer
            control.marker2DDrawer = new Drawer(control);
        }

        /// <summary>
        /// Drawer of the markers on canvas
        /// </summary>
        public class Drawer : Marker2DDrawer
        {
            /// <summary>
            /// Reference to Control
            /// </summary>
            private ControlBase control;

            /// <summary>
            /// Constructor of drawer
            /// </summary>
            /// <param name="control">Reference to Control</param>
            public Drawer(ControlBase control)
            {
                this.control = control;
                map = control.map;

                // Subscribe to draw markers event
                map.OnMapUpdated += OnDrawMarkers;
            }

            /// <summary>
            /// Dispose the drawer
            /// </summary>
            public override void Dispose()
            {
                // Unsubscribe from draw marker event
                if (control != null) map.OnMapUpdated -= OnDrawMarkers;

                // Clear the reference
                control = null;
            }

            /// <summary>
            /// This method is called when drawing markers
            /// </summary>
            private void OnDrawMarkers()
            {
                // Get corners of the map
                GeoRect rect = map.view.rect;

                // Draw each markers
                foreach (Marker2D marker in control.marker2DManager)
                {
                    DrawMarker(marker, rect);
                }
            }

            /// <summary>
            /// This method is called to draw each marker
            /// </summary>
            /// <param name="marker">Marker</param>
            /// <param name="rect">Rect of the map</param>
            private void DrawMarker(Marker2D marker, GeoRect rect)
            {
                // Get coordinates of the marker
                GeoPoint location = marker.location;

                // Get instance of marker from custom data
                GameObject markerInstance = marker.GetData<GameObject>("instance");

                // If marker outside the map
                if (!rect.Contains(location))
                {
                    // If there is an instance, destroy it
                    if (markerInstance)
                    {
                        Utils.Destroy(markerInstance);
                        marker["instance"] = null;
                    }

                    return;
                }

                // If there is no instance, create it and put the reference to custom data
                if (!markerInstance)
                {
                    marker["instance"] = markerInstance = Instantiate(instance.prefab);
                    (markerInstance.transform as RectTransform).SetParent(instance.container);
                    markerInstance.transform.localScale = Vector3.one;
                }

                // Convert geographic coordinates to screen position
                Vector2 screenPosition = control.LocationToScreen(location);

                // Get rect transform of the instance
                RectTransform markerRectTransform = markerInstance.transform as RectTransform;

                // Add half height to align the marker to the bottom
                screenPosition.y += markerRectTransform.rect.height / 2;

                // Convert screen space to local space in the canvas
                Vector2 point;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(markerRectTransform.parent as RectTransform, screenPosition, instance.worldCamera, out point);

                // Set position of the marker instance
                markerRectTransform.localPosition = point;
            }
        }
    }
}