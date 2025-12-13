/*         INFINITY CODE         */
/*   https://infinity-code.com   */

namespace OnlineMaps
{
    /// <summary>
    /// Represents the properties of a tileset mesh.
    /// </summary>
    public class TilesetMeshProps
    {
        /// <summary>
        /// Size of a cell along the X axis.
        /// </summary>
        public double cellSizeX;

        /// <summary>
        /// Size of a cell along the Y axis.
        /// </summary>
        public double cellSizeY;

        /// <summary>
        /// UV coordinate along the X axis.
        /// </summary>
        public double uvX;

        /// <summary>
        /// UV coordinate along the Z axis.
        /// </summary>
        public double uvZ;

        /// <summary>
        /// Size of the sub-mesh along the X axis.
        /// </summary>
        public double subMeshSizeX;

        /// <summary>
        /// Size of the sub-mesh along the Y axis.
        /// </summary>
        public double subMeshSizeY;

        /// <summary>
        /// Number of vertices along the X axis in the sub-mesh.
        /// </summary>
        public int subMeshVX;

        /// <summary>
        /// Number of vertices along the Z axis in the sub-mesh.
        /// </summary>
        public int subMeshVZ;

        /// <summary>
        /// Width of the mesh.
        /// </summary>
        public int w;

        /// <summary>
        /// Height of the mesh.
        /// </summary>
        public int h;

        /// <summary>
        /// Starting position along the X axis.
        /// </summary>
        public double startPosX;

        /// <summary>
        /// Starting position along the Z axis.
        /// </summary>
        public double startPosZ;

        /// <summary>
        /// Scale along the Y axis.
        /// </summary>
        public float yScale;

        /// <summary>
        /// Minimum Y value.
        /// </summary>
        public float minY = float.PositiveInfinity;

        /// <summary>
        /// Maximum Y value.
        /// </summary>
        public float maxY = float.NegativeInfinity;

        /// <summary>
        /// Geographical rectangle.
        /// </summary>
        public GeoRect rect;

        /// <summary>
        /// Sets the properties of the tileset mesh.
        /// </summary>
        /// <param name="subMeshSizeX">Size of the sub-mesh along the X axis.</param>
        /// <param name="subMeshSizeY">Size of the sub-mesh along the Y axis.</param>
        /// <param name="subMeshVX">Number of vertices along the X axis in the sub-mesh.</param>
        /// <param name="subMeshVZ">Number of vertices along the Z axis in the sub-mesh.</param>
        public void Set(double subMeshSizeX, double subMeshSizeY, int subMeshVX, int subMeshVZ)
        {
            this.subMeshSizeX = subMeshSizeX;
            this.subMeshSizeY = subMeshSizeY;
            this.subMeshVX = subMeshVX;
            this.subMeshVZ = subMeshVZ;

            cellSizeX = subMeshSizeX / subMeshVX;
            cellSizeY = subMeshSizeY / subMeshVZ;

            uvX = 1.0 / subMeshVX;
            uvZ = 1.0 / subMeshVZ;

            minY = float.PositiveInfinity;
            maxY = float.NegativeInfinity;
        }
    }
}