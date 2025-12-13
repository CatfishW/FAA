/*         INFINITY CODE         */
/*   https://infinity-code.com   */

#if NGUI

using OnlineMaps;
using UnityEngine;

namespace OnlineMapsExamples
{
    [AddComponentMenu(Utils.ExampleMenuPath + "NGUICustomTooltipExample")]
    public class NGUICustomTooltipExample : MonoBehaviour
    {
        public GameObject tooltipPrefab;
        public GameObject container;

        private GameObject tooltip;
        private UIWidget widget;
        private UILabel label;


        // Use this for initialization
	    private void Start ()
        {
            Marker.OnMarkerDrawTooltip = delegate { };
            Marker2DManager.CreateItem(GeoPoint.zero, "Hello World");
            Map.instance.OnUpdateLate += OnUpdateLate;
        }

        private void OnUpdateLate()
        {
            Marker tooltipMarker = TooltipDrawerBase.tooltipMarker;
            if (tooltipMarker != null && !string.IsNullOrEmpty(tooltipMarker.label))
            {
                if (!tooltip)
                {
                    tooltip = Instantiate(tooltipPrefab) as GameObject;
                    tooltip.transform.parent = container.transform;
                    tooltip.transform.localScale = Vector3.one;
                    widget = tooltip.GetComponent<UIWidget>();
                    label = widget.GetComponentInChildren<UILabel>();
                }

                Vector2 screenPosition = ControlBase.instance.LocationToScreen(tooltipMarker.location);

                float ratio = (float)widget.root.activeHeight / Screen.height;
                float width = Mathf.Ceil(Screen.width * ratio);

                screenPosition.x = (screenPosition.x / Screen.width - 0.5f) * width;
                screenPosition.y = (screenPosition.y / Screen.height - 0.5f) * widget.root.activeHeight;

                label.text = tooltipMarker.label;

                Vector2 buttonOffset = new Vector2(-widget.width / 2, widget.height);
                widget.SetRect(screenPosition.x + buttonOffset.x, screenPosition.y + buttonOffset.y, widget.width, widget.height);
            }
            else if (tooltip)
            {
                Utils.Destroy(tooltip);
                tooltip = null;
            }
        }
    }
}

#endif