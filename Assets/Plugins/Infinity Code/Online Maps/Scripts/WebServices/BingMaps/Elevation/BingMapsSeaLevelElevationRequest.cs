/*         INFINITY CODE         */
/*   https://infinity-code.com   */

namespace OnlineMaps.Webservices
{
    /// <summary>
    /// Represents a request to get sea level elevation data for a set of points.
    /// </summary>
    public class BingMapsSeaLevelElevationRequest : BingMapsPointsElevationRequest
    {
        protected override string urlToken => "SeaLevel";

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="points">An array of geographical points.</param>
        /// <param name="heights">The height reference system (default is sea level).</param>
        /// <param name="output">The output format (default is JSON).</param>
        public BingMapsSeaLevelElevationRequest(GeoPoint[] points, Heights heights = Heights.sealevel) : base(points, heights)
        {
        }
    }
}