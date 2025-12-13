/*         INFINITY CODE         */
/*   https://infinity-code.com   */

namespace OnlineMaps
{
    /// <summary>
    /// Drawer for dynamically rendering line elements as meshes on the map.
    /// </summary>
    public class LineDynamicMeshDrawer : DynamicMeshDrawerBase<Line>
    {
        protected override void DrawElement(Line element, int index)
        {
            ElementData data = GetElementData(element.color, default, element.texture);
            GenerateLineMesh(element.width);

            data.mesh.Clear();

            data.mesh.SetVertices(vertices);
            data.mesh.SetNormals(normals);
            data.mesh.SetUVs(0, uv);

            data.mesh.SetTriangles(triangles.ToArray(), 0);

            UpdateMaterialsQueue(data, index);
        }
    }
}