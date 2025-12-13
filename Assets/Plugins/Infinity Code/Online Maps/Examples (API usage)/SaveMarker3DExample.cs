/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using OnlineMaps;
using UnityEngine;

namespace OnlineMapsExamples
{
    /// <summary>
    /// Example of runtime saving 3D markers to PlayerPrefs, and loading of 3D markers from PlayerPrefs.
    /// </summary>
    [AddComponentMenu(Utils.ExampleMenuPath + "SaveMarker3DExample")]
    public class SaveMarker3DExample : MonoBehaviour
    {
        /// <summary>
        /// Reference to the map control. If not specified, the current instance will be used.
        /// </summary>
        public ControlBase3D control;
        
        /// <summary>
        /// Key in PlayerPrefs
        /// </summary>
        private static string prefsKey = "markers";

        /// <summary>
        /// Prefab of the marker
        /// </summary>
        public GameObject markerPrefab;

        /// <summary>
        /// Scale of the markers
        /// </summary>
        public int markerScale = 20;

        /// <summary>
        /// Use this for initialization
        /// </summary>
        private void Start()
        {
            // If the control is not specified, get the current instance.
            if (!control && !(control = ControlBase3D.instance))
            {
                Debug.LogError("Control not found");
                return;
            }
            
            // Try load markers
            TryLoadMarkers();

            // Subscribe to OnMapClick event
            control.OnClick += OnMapClick;
        }

        private void OnGUI()
        {
            if (GUI.Button(new Rect(5, 5, 100, 30), "Save markers"))
            {
                // Save markers to PlayerPrefs
                SaveMarkers();
            }
        }

        /// <summary>
        /// The event, which is called when the user clicked on the map.
        /// </summary>
        private void OnMapClick()
        {
            // Create new marker
            Marker3D marker = control.marker3DManager.Create(control.ScreenToLocation(), markerPrefab);
            marker.scale = markerScale;
        }

        /// <summary>
        /// Saves markers to PlayerPrefs as xml string
        /// </summary>
        private void SaveMarkers()
        {
            // Create XMLDocument and first child
            XML xml = new XML("Markers");

            // Save markers data
            foreach (Marker3D marker in control.marker3DManager)
            {
                // Create marker node
                xml.Create("Marker", marker.location);
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
                // Gets coordinates
                GeoPoint location = node.Value<GeoPoint>();

                // Create marker
                Marker3D marker = control.marker3DManager.Create(location, markerPrefab);
                marker.scale = markerScale;
            }
        }
    }
}