/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System.Collections.Generic;
using UnityEngine;

namespace OnlineMaps.Webservices
{
    /// <summary>
    /// Result of Google Maps Elevation query.
    /// </summary>
    public class GoogleElevationResult
    {
        /// <summary>
        /// Elevation of the location in meters.
        /// </summary>
        public float elevation;

        /// <summary>
        /// Position for which elevation data is being computed. <br/>
        /// Note that for path requests, the set of location elements will contain the sampled points along the path.
        /// </summary>
        public Vector2 location;

        /// <summary>
        /// Maximum distance between data points from which the elevation was interpolated, in meters. <br/>
        /// This property will be missing if the resolution is not known. <br/>
        /// Note that elevation data becomes more coarse (larger resolution values) when multiple points are passed. <br/>
        /// To obtain the most accurate elevation value for a point, it should be queried independently.
        /// </summary>
        public float resolution;

        public GoogleElevationResult(){}

        public GoogleElevationResult(XML node)
        {
            elevation = node.Get<float>("elevation");
            resolution = node.Get<float>("resolution");

            XML locationNode = node["location"];
            location = new Vector2(locationNode.Get<float>("lng"), locationNode.Get<float>("lat"));
        }

        public static GoogleElevationResult[] Parse(string response)
        {
            XML xml = XML.Load(response);
            if (xml.isNull || xml.Get<string>("status") != "OK") return null;

            List<GoogleElevationResult> rList = new List<GoogleElevationResult>();
            foreach (XML node in xml.FindAll("result")) rList.Add(new GoogleElevationResult(node));

            return rList.ToArray();
        }
    }
}