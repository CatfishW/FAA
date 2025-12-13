/*         INFINITY CODE         */
/*   https://infinity-code.com   */

namespace OnlineMaps.Webservices
{
    /// <summary>
    /// Speed limit result
    /// </summary>
    public class GoogleSpeedLimitResult
    {
        /// <summary>
        /// A unique identifier for a place. All place IDs returned by the Google Maps Roads API correspond to road segments.
        /// </summary>
        public string placeId;

        /// <summary>
        /// The speed limit for that road segment.
        /// </summary>
        public int speedLimit;

        /// <summary>
        /// Returns either KPH or MPH.
        /// </summary>
        public string units;

        public static GoogleSpeedLimitResult[] Parse(string response)
        {
            JSONObject json = JSONObject.ParseObject(response);
            return json["speedLimits"].Deserialize<GoogleSpeedLimitResult[]>();
        }
    }
}