/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using System.Collections.Generic;
using UnityEngine;

namespace OnlineMaps
{
    /// <summary>
    /// Drawer for rendering polygons using a dynamic mesh in Unity.
    /// </summary>
    public class PolygonDynamicMeshDrawer : DynamicMeshDrawerBase<Polygon>
    {
        private static float[] internalPoints;
        private static List<int> internalIndices;
        private static int countInternalPoints;
        private static List<int> fillTriangles;
        
        protected override void DrawElement(Polygon polygon, int index)
        {
            ElementData data = GetElementData(polygon.borderColor, polygon.backgroundColor);
            GenerateLineMesh(polygon.borderWidth, true, false);

            Mesh mesh = data.mesh;
            mesh.Clear();

            if (vertices.Count < 4) return;

            Vector3 v1 = (vertices[0] + vertices[3]) / 2;
            Vector3 v2 = (vertices[vertices.Count - 3] + vertices[vertices.Count - 2]) / 2;
            if ((v1.x - v2.x) * (v1.x - v2.x) + (v1.z - v2.z) * (v1.z - v2.z) < float.Epsilon)
            {
                Vector3 v0 = vertices[0];
                v1 = vertices[1];
                v2 = vertices[2];
                Vector3 v3 = vertices[3];
                Vector3 vs1 = vertices[vertices.Count - 1];
                Vector3 vs2 = vertices[vertices.Count - 2];
                Vector3 vs3 = vertices[vertices.Count - 3];
                Vector3 vs4 = vertices[vertices.Count - 4];
                Vector3 nv1 = Vector3.zero, nv2 = Vector3.zero;
                int s1 = Geometry.GetIntersectionPointOfTwoLines(v0.x, v0.z, v1.x, v1.z, vs4.x, vs4.z, vs3.x, vs3.z, out nv1.x, out nv1.z);
                int s2 = Geometry.GetIntersectionPointOfTwoLines(v3.x, v3.z, v2.x, v2.z, vs1.x, vs1.z, vs2.x, vs2.z, out nv2.x, out nv2.z);

                if (s1 == 1 && s2 == 1)
                {
                    if (hasElevation)
                    {
                        nv1.y = elevationManager.GetElevationValue(nv1.x, nv1.z, bestElevationYScale, mapRect);
                        nv2.y = elevationManager.GetElevationValue(nv2.x, nv2.z, bestElevationYScale, mapRect);
                    }

                    vertices[0] = vertices[vertices.Count - 3] = nv1;
                    vertices[3] = vertices[vertices.Count - 2] = nv2;
                }
                else
                {
                    vertices[0] = vertices[vertices.Count - 3] = (vertices[0] + vertices[vertices.Count - 3]) / 2;
                    vertices[3] = vertices[vertices.Count - 2] = (vertices[3] + vertices[vertices.Count - 2]) / 2;
                }
            }

            FillPolygon(polygon);

            mesh.subMeshCount = 2;

            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uv);

            mesh.SetTriangles(triangles.ToArray(), 0);
            if (fillTriangles.Count > 0) mesh.SetTriangles(fillTriangles.ToArray(), 1);

            UpdateMaterialsQueue(data, index);
        }

        private void FillPolygon(Polygon polygon)
        {
            if (fillTriangles == null) fillTriangles = new List<int>(128);
            else fillTriangles.Clear();

            if (element.checkMapBoundaries || polygon.backgroundColor.a == 0 || vertices.Count == 0) return;

            float l1 = 0;
            float l2 = 0;

            for (int i = 0; i < vertices.Count / 4 - 1; i++)
            {
                Vector3 p11 = vertices[i * 4];
                Vector3 p12 = vertices[(i + 1) * 4];

                Vector3 p21 = vertices[i * 4 + 3];
                Vector3 p22 = vertices[(i + 1) * 4 + 3];

                l1 += (p11 - p12).magnitude;
                l2 += (p21 - p22).magnitude;
            }

            bool side = l2 < l1;
            int off1 = side ? 3 : 0;
            int off2 = side ? 2 : 1;

            Vector2 lastPoint = Vector2.zero;

            if (internalIndices == null) internalIndices = new List<int>(128);
            else internalIndices.Clear();

            if (internalPoints == null) internalPoints = new float[128];
            countInternalPoints = 0;
            int ipl = internalPoints.Length;

            float w = polygon.borderWidth / 2;
            w *= w;
            for (int i = 0, j = 0; i < vertices.Count / 4; i++, j += 4)
            {
                Vector3 p = vertices[j + off1];
                Vector2 p2 = new Vector2(p.x, p.z);
                if (i > 0)
                {
                    if ((lastPoint - p2).sqrMagnitude > w)
                    {
                        internalIndices.Add(j + off1);

                        internalPoints[countInternalPoints++] = p2.x;
                        internalPoints[countInternalPoints++] = p2.y;
                        if (ipl == countInternalPoints) Array.Resize(ref internalPoints, ipl *= 2);

                        lastPoint = p2;
                    }
                }
                else
                {
                    internalIndices.Add(j + off1);

                    internalPoints[countInternalPoints++] = p2.x;
                    internalPoints[countInternalPoints++] = p2.y;
                    if (ipl == countInternalPoints) Array.Resize(ref internalPoints, ipl *= 2);

                    lastPoint = p2;
                }

                p = vertices[j + off2];
                p2 = new Vector2(p.x, p.z);
                if ((lastPoint - p2).sqrMagnitude > w)
                {
                    internalIndices.Add(j + off2);

                    internalPoints[countInternalPoints++] = p2.x;
                    internalPoints[countInternalPoints++] = p2.y;
                    if (ipl == countInternalPoints) Array.Resize(ref internalPoints, ipl *= 2);

                    lastPoint = p2;
                }
            }

            if (Math.Abs(internalPoints[0] - internalPoints[countInternalPoints - 2]) < float.Epsilon &&
                Math.Abs(internalPoints[1] - internalPoints[countInternalPoints - 1]) < float.Epsilon)
            {
                countInternalPoints -= 2;
            }

            Geometry.Triangulate(internalPoints, countInternalPoints / 2, fillTriangles);

            if (fillTriangles.Count > 2)
            {
                for (int i = 0; i < fillTriangles.Count; i++) fillTriangles[i] = internalIndices[fillTriangles[i]];

                Vector3 side1 = vertices[fillTriangles[1]] - vertices[fillTriangles[0]];
                Vector3 side2 = vertices[fillTriangles[2]] - vertices[fillTriangles[0]];
                Vector3 perp = Vector3.Cross(side1, side2);

                bool reversed = perp.y < 0;
                if (reversed) fillTriangles.Reverse();
            }
            else fillTriangles.Clear();
        }
    }
}