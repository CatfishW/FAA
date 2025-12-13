/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System.Text;

namespace OnlineMaps.Webservices
{
    /// <summary>
    /// Represents a request to get elevation data for a polyline defined by geographical points.
    /// </summary>
    public class BingMapsPolylineElevationRequest : BingMapsPointsElevationRequest
    {
        private int samples;

        protected override string urlToken => "Polyline";

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="points">An array of geographical points defining the polyline.</param>
        /// <param name="samples">The number of samples along the polyline.</param>
        /// <param name="heights">The height reference system (default is sea level).</param>
        public BingMapsPolylineElevationRequest(GeoPoint[] points, int samples, Heights heights = Heights.sealevel) : base(points, heights)
        {
            this.samples = samples;
        }

        protected override void GenerateUrl(StringBuilder builder)
        {
            base.GenerateUrl(builder);

            builder.Append("&samp=").Append(samples);
        }
    }
}