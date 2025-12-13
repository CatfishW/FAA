/*         INFINITY CODE         */
/*   https://infinity-code.com   */

namespace OnlineMaps.Webservices
{
    /// <summary>
    /// Snap to road result
    /// </summary>
    public class GoogleSnapToRoadResult
    {
        /// <summary>
        /// Contains a latitude and longitude value.
        /// </summary>
        public Location location;

        /// <summary>
        /// An integer that indicates the corresponding value in the original request. <br/>
        /// Each value in the request should map to a snapped value in the response. <br/>
        /// However, if you've set interpolate=true, then it's possible that the response will contain more coordinates than the request. <br/>
        /// Interpolated values will not have an originalIndex. <br/>
        /// These values are indexed from 0, so a point with an originalIndex of 4 will be the snapped value of the 5th latitude/longitude passed to the path parameter.
        /// </summary>
        public int originalIndex;

        /// <summary>
        /// A unique identifier for a place. All place IDs returned by the Google Maps Roads API correspond to road segments.
        /// </summary>
        public string placeId;

        public static GoogleSnapToRoadResult[] Parse(string response)
        {
            JSONObject json = JSONObject.ParseObject(response);
            return json["snappedPoints"].Deserialize<GoogleSnapToRoadResult[]>();
        }

        /// <summary>
        /// Latitude and longitude value
        /// </summary>
        public class Location
        {
            /// <summary>
            /// Longitude
            /// </summary>
            public double longitude;

            /// <summary>
            /// Latitude
            /// </summary>
            public double latitude;
        }
    }
}