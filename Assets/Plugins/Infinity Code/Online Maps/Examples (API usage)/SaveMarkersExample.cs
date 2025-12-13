/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using OnlineMaps;
using UnityEngine;

namespace OnlineMapsExamples
{
    /// <summary>
    /// Example of runtime saving 2D markers to PlayerPrefs, and loading of 2D markers from PlayerPrefs.
    /// </summary>
    [AddComponentMenu(Utils.ExampleMenuPath + "SaveMarkersExample")]
    public class SaveMarkersExample : MonoBehaviour
    {
        /// <summary>
        /// Reference to the map control. If not specified, the current instance will be used.
        /// </summary>
        public ControlBase control;
        
        /// <summary>
        /// Key in PlayerPrefs
        /// </summary>
        private static string prefsKey = "markers";

        /// <summary>
        /// Use this for initialization
        /// </summary>
        private void Start()
        {
            // If the control is not specified, get the current instance.
            if (!control && !(control = ControlBase.instance))
            {
                Debug.LogError("Control not found");
                return;
            }
            
            // Try load markers
            TryLoadMarkers();
        }

        /// <summary>
        /// Saves markers to PlayerPrefs as xml string
        /// </summary>
        public void SaveMarkers()
        {
            // Create XMLDocument and first child
            XML xml = new XML("Markers");

            // Save markers data
            foreach (Marker2D marker in control.marker2DManager)
            {
                // Create marker node
                XML markerNode = xml.Create("Marker");
                markerNode.Create("Location", marker.location);
                markerNode.Create("Label", marker.label);
            }

            // Save xml string
            PlayerPrefs.SetString(prefsKey, xml.outerXml);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Try load markers from PlayerPrefs
        /// </summary>
        private void TryLoadMarkers()
        {
            // If the key does not exist, returns.
            if (!PlayerPrefs.HasKey(prefsKey)) return;

            // Load xml string from PlayerPrefs
            string xmlData = PlayerPrefs.GetString(prefsKey);

            // Load xml document
            XML xml = XML.Load(xmlData);

            // Load markers
            foreach (XML node in xml)
            {
                // Gets coordinates and label
                GeoPoint position = node.Get<GeoPoint>("Location");
                string label = node.Get<string>("Label");

                // Create marker
                control.marker2DManager.Create(position, label);
            }
        }
    }
}