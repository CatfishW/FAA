/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using OnlineMaps;
using UnityEngine;
using UnityEngine.UI;

namespace OnlineMapsExamples
{
    /// <summary>
    /// Example of how to make a tooltip using uGUI for all markers
    /// </summary>
    [AddComponentMenu(Utils.ExampleMenuPath + "uGUICustomTooltipForAllMarkersExample")]
    public class uGUICustomTooltipForAllMarkersExample : MonoBehaviour
    {
        /// <summary>
        /// Reference to the control
        /// </summary>
        public ControlBase control;
        
        /// <summary>
        /// Prefab of the tooltip
        /// </summary>
        public GameObject tooltipPrefab;

        /// <summary>
        /// Container for tooltip
        /// </summary>
        public Canvas container;

        private GameObject tooltip;
        private string currentLabel;

        private void Start()
        {
            if (!control && !(control = ControlBase.instance))
            {
                Debug.LogError("Can't find Control");
                return;
            }
            
            control.marker2DManager.Create(GeoPoint.zero, "Marker 1");
            control.marker2DManager.Create(new GeoPoint(1, 1), "Marker 2");
            control.marker2DManager.Create(new GeoPoint(2, 1), "Marker 3");
            Marker.OnMarkerDrawTooltip = delegate { };

            control.map.OnUpdateLate += OnUpdateLate;
        }

        private void OnUpdateLate()
        {
            Marker2D tooltipMarker = TooltipDrawerBase.tooltipMarker as Marker2D;
            if (tooltipMarker != null && !string.IsNullOrEmpty(tooltipMarker.label))
            {
                if (!tooltip)
                {
                    tooltip = Instantiate(tooltipPrefab);
                    (tooltip.transform as RectTransform).SetParent(container.transform);
                }
                Vector2 screenPosition = control.LocationToScreen(tooltipMarker.location);
                screenPosition.y += tooltipMarker.height;
                Vector2 point;
                Camera cam = container.renderMode == RenderMode.ScreenSpaceOverlay ? null : container.worldCamera;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(container.transform as RectTransform, screenPosition, cam, out point);
                (tooltip.transform as RectTransform).localPosition = point;

                if (currentLabel != tooltipMarker.label)
                {
                    currentLabel = tooltipMarker.label;
                    tooltip.GetComponentInChildren<Text>().text = tooltipMarker.label;
                }
            }
            else
            {
                Utils.Destroy(tooltip);
                tooltip = null;
            }
        }
    }
}