/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using System.Text;

namespace OnlineMaps.Webservices
{
    /// <summary>
    /// Represents a request to get elevation data for a rectangular area defined by geographical bounds.
    /// </summary>
    public class BingMapsBoundsElevationRequest : BingMapsElevationRequestBase
    {
        private GeoRect rect;
        private int rows;
        private int cols;

        protected override string urlToken => "Bounds";

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="rect">The geographical bounds of the area.</param>
        /// <param name="rows">The number of rows in the grid.</param>
        /// <param name="cols">The number of columns in the grid.</param>
        /// <param name="heights">The height reference system (default is sea level).</param>
        public BingMapsBoundsElevationRequest(GeoRect rect, int rows, int cols, Heights heights = Heights.sealevel) : base(heights)
        {
            this.rect = rect;
            this.rows = rows;
            this.cols = cols;

            if (rows < 2) throw new Exception("Rows must be >= 2.");
            if (cols < 2) throw new Exception("Cols must be >= 2.");
            if (rows * cols > 1024) throw new Exception("The number of rows and columns can define a maximum of 1024 locations (rows * cols <= 1024).");
        }

        protected override void GenerateUrl(StringBuilder builder)
        {
            base.GenerateUrl(builder);

            builder.Append("&bounds=");
            AppendLocation(builder, rect.bottomLeft);
            builder.Append(",");
            AppendLocation(builder, rect.topRight);
            builder.Append("&rows=").Append(rows).Append("&cols=").Append(cols);
        }
    }
}