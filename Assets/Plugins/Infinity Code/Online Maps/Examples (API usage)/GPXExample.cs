/*         INFINITY CODE         */
/*   https://infinity-code.com   */

#if !UNITY_WEBGL
using System.IO;
#endif

using System.Text;
using OnlineMaps;
using UnityEngine;

namespace OnlineMapsExamples
{
    /// <summary>
    /// Example of work with GPX.
    /// </summary>
    [AddComponentMenu(Utils.ExampleMenuPath + "GPXExample")]
    public class GPXExample : MonoBehaviour
    {
        /// <summary>
        /// Creates a new GPX object.
        /// </summary>
        private void CreateNewGPX()
        {
            // Creates a new GPX object.
            GPXObject gpx = new GPXObject("Infinity Code");

            // Creates a meta.
            GPXObject.Meta meta = gpx.metadata = new GPXObject.Meta();
            meta.author = new GPXObject.Person
            {
                email = new GPXObject.EMail("support", "infinity-code.com"),
                name = "Infinity Code"
            };

            // Creates a bounds
            meta.bounds = new GPXObject.Bounds(30, 10, 40, 20);

            // Creates a copyright
            meta.copyright = new GPXObject.Copyright("Infinity Code")
            {
                year = 2016
            };

            // Creates a links
            meta.links.Add(new GPXObject.Link("https://infinity-code.com/assets/online-maps")
            {
                text = "Product Page"
            });

            // Creates a waypoints
            gpx.waypoints.AddRange(new[]
            {
                new GPXObject.Waypoint(31, 12)
                {
                    description = "Point 1",
                },
                new GPXObject.Waypoint(35, 82)
                {
                    description = "Point 2"
                }
            });

            // Creates a waypoints extensions
            foreach (GPXObject.Waypoint wpt in gpx.waypoints)
            {
                XML ext = wpt.extensions = new XML("extensions");
                ext.Create("myField", wpt.description + "_1");
            }

            // Log GPX XML object
            Debug.Log(gpx);
        }

        /// <summary>
        /// Load GPX data from the file.
        /// </summary>
        private void LoadData()
        {
#if !UNITY_WEBGL
            string filename = "test.gpx";
            if (File.Exists(filename))
            {
                // Load data string
                string data = File.ReadAllText(filename, Encoding.UTF8);

                // Trying to to load GPX.
                GPXObject gpx = GPXObject.Load(data);

                // Log GPX XML object
                Debug.Log(gpx);
            }
#endif
        }

        private void Start()
        {
            // Load GPX data from the file.
            LoadData();

            // Creates a new GPX object.
            CreateNewGPX();
        }
    }
}