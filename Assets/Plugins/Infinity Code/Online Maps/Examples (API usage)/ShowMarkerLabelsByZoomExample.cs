/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using OnlineMaps;
using UnityEngine;

namespace OnlineMapsExamples
{
    /// <summary>
    /// Example of how to show marker labels, only in the zoom range.
    /// </summary>
    [AddComponentMenu(Utils.ExampleMenuPath + "ShowMarkerLabelsByZoomExample")]
    public class ShowMarkerLabelsByZoomExample : MonoBehaviour
    {
        /// <summary>
        /// Reference to the map. If not specified, the current instance will be used.
        /// </summary>
        public Map map;
        
        private void Start()
        {
            // If map is not specified, use the current instance.
            if (!map && !(map = Map.instance))
            {
                Debug.LogError("Map not found");
                return;
            }

            // Create a new markers.
            Marker2D marker1 = map.marker2DManager.Create(0, 0, null, "Marker 1");
            Marker2D marker2 = map.marker2DManager.Create(10, 0, null, "Marker 2");

            // Store data about labels.
            marker1["data"] = new ShowMarkerLabelsByZoomItem(marker1.label, new LimitedRange(3, 10));
            marker2["data"] = new ShowMarkerLabelsByZoomItem(marker2.label, new LimitedRange(8, 15));

            // Sunscribe to ChangeZoom event.
            map.OnZoomChanged += OnChangeZoom;
            OnChangeZoom();
        }

        private void OnChangeZoom()
        {
            foreach (Marker2D marker in map.marker2DManager)
            {
                ShowMarkerLabelsByZoomItem item = marker.GetData<ShowMarkerLabelsByZoomItem>("data");
                if (item == null) continue;

                // Update marker labels.
                marker.label = item.zoomRange.Contains(map.view.intZoom) ? item.label : "";
            }
        }

        public class ShowMarkerLabelsByZoomItem
        {
            /// <summary>
            /// Zoom range where you need to show the label.
            /// </summary>
            public LimitedRange zoomRange;

            /// <summary>
            /// Label.
            /// </summary>
            public string label;

            public ShowMarkerLabelsByZoomItem(string label, LimitedRange zoomRange)
            {
                this.label = label;
                this.zoomRange = zoomRange;
            }
        }
    }
}