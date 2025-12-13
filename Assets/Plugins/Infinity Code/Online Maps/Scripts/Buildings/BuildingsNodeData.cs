/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System.Collections.Generic;
using OnlineMaps.Webservices;

namespace OnlineMaps
{
    /// <summary>
    /// It contains a dictionary of nodes and way of a building contour.
    /// </summary>
    public class BuildingsNodeData
    {
        /// <summary>
        /// Way of a building contour.
        /// </summary>
        public OSMOverpassResult.Way way;

        /// <summary>
        /// Dictionary of nodes.
        /// </summary>
        public Dictionary<string, OSMOverpassResult.Node> nodes;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="way">Way of a building contour.</param>
        /// <param name="nodes">Dictionary of nodes.</param>
        public BuildingsNodeData(OSMOverpassResult.Way way, Dictionary<string, OSMOverpassResult.Node> nodes)
        {
            this.way = way;
            this.nodes = nodes;
        }

        /// <summary>
        /// Disposes this object.
        /// </summary>
        public void Dispose()
        {
            way = null;
            nodes = null;
        }
    }
}