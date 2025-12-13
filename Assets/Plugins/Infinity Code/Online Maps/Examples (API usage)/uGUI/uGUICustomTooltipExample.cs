/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using OnlineMaps;
using UnityEngine;
using UnityEngine.UI;

namespace OnlineMapsExamples
{
    /// <summary>
    /// Example of how to make a tooltip using uGUI for a single marker
    /// </summary>
    [AddComponentMenu(Utils.ExampleMenuPath + "uGUICustomTooltipExample")]
    public class uGUICustomTooltipExample : MonoBehaviour
    {
        /// <summary>
        /// Reference to the control
        /// </summary>
        public TileSetControl control;
        
        /// <summary>
        /// Prefab of the tooltip
        /// </summary>
        public GameObject tooltipPrefab;

        /// <summary>
        /// Container for tooltip
        /// </summary>
        public Canvas container;

        public RawImageTouchForwarder forwarder;

        private Marker2D marker;
        private GameObject tooltip;

	    private void Start ()
        {
            if (!control && !(control = TileSetControl.instance))
            {
                Debug.LogError("Can't find Tileset Control");
                return;
            }
            
            marker = control.marker2DManager.Create(GeoPoint.zero, "Hello World");
            marker.OnDrawTooltip = delegate {  };

            control.map.OnUpdateLate += OnUpdateLate;
        }

        public Vector2 LocationToScreen(GeoPoint point, float yOffset)
        {
            Map map = control.map;
            Vector2d p = point.ToLocal(map);
            p.x /= map.buffer.renderState.width;
            p.y /= map.buffer.renderState.height;

            double cpx = -control.sizeInScene.x * p.x;
            double cpy = control.sizeInScene.y * p.y;

            float elevation = 0;
            if (control.hasElevation)
            {
                GeoRect r = map.view.rect;
                float elevationScale = ElevationManagerBase.GetElevationScale(r, control.elevationManager);
                elevation = control.elevationManager.GetElevationValue(cpx, cpy, elevationScale, r);
            }
            Vector3 worldPos = transform.position + transform.rotation * new Vector3((float)(cpx * transform.lossyScale.x), elevation * transform.lossyScale.y, (float)(cpy * transform.lossyScale.z));

            Camera cam = control.activeCamera ? control.activeCamera : Camera.main;
            return cam.WorldToScreenPoint(worldPos);
        }

        private void OnUpdateLate()
        {
            Marker tooltipMarker = TooltipDrawerBase.tooltipMarker;
            if (tooltipMarker == marker)
            {
                if (!tooltip)
                {
                    tooltip = Instantiate(tooltipPrefab) as GameObject;
                    (tooltip.transform as RectTransform).SetParent(container.transform);
                }
                Vector2 screenPosition = LocationToScreen(marker.location, marker.height + 10);
                if (forwarder) screenPosition = forwarder.MapToForwarderSpace(screenPosition);
                Vector2 point;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(container.transform as RectTransform, screenPosition, null, out point);
                (tooltip.transform as RectTransform).localPosition = point;
                tooltip.GetComponentInChildren<Text>().text = marker.label;

            }
            else if (tooltip)
            {
                Utils.Destroy(tooltip);
                tooltip = null;
            }
        }
    }
}