/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using OnlineMaps;
using UnityEngine;

namespace OnlineMapsExamples
{
    /// <summary>
    /// Example of storing custom data in the marker.
    /// </summary>
    [AddComponentMenu(Utils.ExampleMenuPath + "MarkerCustomDataExample")]
    public class MarkerCustomDataExample : MonoBehaviour
    {
        private void Start()
        {
            // Create a new markers.
            Marker2D marker1 = Marker2DManager.CreateItem(GeoPoint.zero, "Marker 1");
            Marker2D marker2 = Marker2DManager.CreateItem(new GeoPoint(10, 0), "Marker 2");

            // Create new XML and store it in customData.
            XML xml1 = new XML("MarkerData");
            xml1.Create("ID", "marker1");
            marker1["data"] = xml1;

            XML xml2 = new XML("MarkerData");
            xml2.Create("ID", "marker2");
            marker2["data"] = xml2;
            
            // You can also store data of any type.
            marker1["str_data"] = "Some data";
            marker1["int_data"] = 123;
            marker1["marker2"] = marker2;

            // Subscribe to click event.
            marker1.OnClick += OnMarkerClick;
            marker2.OnClick += OnMarkerClick;
        }

        private void OnMarkerClick(Marker marker)
        {
            // Try get XML from customData.
            XML xml = marker["data"] as XML;
            
            // or you can use this way
            xml = marker.GetData<XML>("data");
            
            if (xml == null)
            {
                Debug.Log("The marker does not contain XML.");
                return;
            }

            // Show xml in console.
            Debug.Log(xml.outerXml);
            Debug.Log(xml.Get("ID"));
        }
    }
}