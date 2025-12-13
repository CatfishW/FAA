/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using System.Collections.Generic;
using UnityEngine;

namespace OnlineMaps.Webservices
{
    /// <summary>
    /// Result of Bing Maps Location API query.
    /// </summary>
    public class BingMapsLocationResult
    {
        /// <summary>
        /// Location name
        /// </summary>
        public string name;

        /// <summary>
        /// Coordinates of location (X - Longitude, Y - Latitude).
        /// </summary>
        public Vector2 location;

        /// <summary>
        /// Latitude
        /// </summary>
        public double latitude;

        /// <summary>
        /// Longitude
        /// </summary>
        public double longitude;

        /// <summary>
        /// Bounding box of location
        /// </summary>
        public Rect boundingBox;

        /// <summary>
        /// Entity type
        /// </summary>
        public string entityType;

        /// <summary>
        /// Dictonary of address parts.
        /// </summary>
        public Dictionary<string, string> address;

        /// <summary>
        /// Formatted address.
        /// </summary>
        public string formattedAddress;

        /// <summary>
        /// Confidence
        /// </summary>
        public string confidence;

        /// <summary>
        /// Match code
        /// </summary>
        public string matchCode;

        /// <summary>
        /// XML Node
        /// </summary>
        public XML node;

        /// <summary>
        /// Constuctor
        /// </summary>
        /// <param name="node">Node of result</param>
        public BingMapsLocationResult(XML node)
        {
            this.node = node;
            address = new Dictionary<string, string>();
            foreach (XML n in node)
            {
                if (n.name == "Name") name = n.Value();
                else if (n.name == "Point")
                {
                    latitude = n.Get<double>("Latitude");
                    longitude = n.Get<double>("Longitude");
                    location = new Vector2((float)longitude, (float)latitude);
                }
                else if (n.name == "BoundingBox")
                {
                    double slat = n.Get<double>("SouthLatitude");
                    double wlng = n.Get<double>("WestLongitude");
                    double nlat = n.Get<double>("NorthLatitude");
                    double elng = n.Get<double>("EastLongitude");

                    boundingBox = new Rect((float)wlng, (float)nlat, (float)(wlng - elng), (float)(nlat - slat));
                }
                else if (n.name == "EntityType") entityType = n.Value();
                else if (n.name == "Address")
                {
                    foreach (XML an in n)
                    {
                        if (an.name == "FormattedAddress") formattedAddress = an.Value();
                        else address.Add(an.name, an.Value());
                    }
                }
                else if (n.name == "Confidence") confidence = n.Value();
                else if (n.name == "MatchCode") matchCode = n.Value();
            }
        }

        public static BingMapsLocationResult[] Parse(string response)
        {
            try
            {
                XML xml = XML.Load(response.Substring(1));
                NamespaceManager nsmgr = xml.GetNamespaceManager("gx");
                string status = xml.Find<string>("//x:StatusDescription", nsmgr);

                if (status != "OK") return null;

                List<BingMapsLocationResult> results = new List<BingMapsLocationResult>();
                XMLList resNodes = xml.FindAll("//x:Location", nsmgr);

                foreach (XML node in resNodes)
                {
                    results.Add(new BingMapsLocationResult(node));
                }

                return results.ToArray();
            }
            catch (Exception exception)
            {
                Debug.Log(exception.Message + "\n" + exception.StackTrace);
            }

            return null;
        }
    }
}