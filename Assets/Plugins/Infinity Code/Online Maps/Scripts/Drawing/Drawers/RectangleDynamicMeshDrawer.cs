/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System.Collections.Generic;
using UnityEngine;

namespace OnlineMaps
{
    /// <summary>
    /// Drawer for rendering dynamic rectangular meshes on the map.
    /// </summary>
    public class RectangleDynamicMeshDrawer : DynamicMeshDrawerBase<Rectangle>
    {
        private static List<Vector2> activePoints;
        private static List<int> backTriangles;
        
        protected override void DrawElement(Rectangle rect, int index)
        {
            ElementData data = GetElementData(rect.borderColor, rect.backgroundColor);
            Material[] materials = data.materials;
            if (materials.Length > 1 && materials[1].mainTexture != rect.backgroundTexture) materials[1].mainTexture = rect.backgroundTexture;

            CalculateLocalPoints(true, false);

            Rect rect1 = new Rect(localPoints[0].x, localPoints[2].y, localPoints[2].x - localPoints[0].x, localPoints[0].y - localPoints[2].y);
            Rect rect2 = new Rect(0, 0, sizeInScene.x, sizeInScene.y);

            bool ignoreLeft = false;
            bool ignoreRight = false;
            bool ignoreTop = false;
            bool ignoreBottom = false;
            int countIgnore = 0;

            if (rect.checkMapBoundaries)
            {
                if (!rect2.Overlaps(rect1))
                {
                    if (data.active) data.active = false;
                    return;
                }
                if (!data.active) data.active = true;

                for (int i = 0; i < localPoints.Count; i++)
                {
                    Vector2 point = localPoints[i];
                    if (point.x < 0)
                    {
                        point.x = 0;
                        if (!ignoreLeft) countIgnore++;
                        ignoreLeft = true;
                    }
                    if (point.y < 0)
                    {
                        point.y = 0;
                        if (!ignoreTop) countIgnore++;
                        ignoreTop = true;
                    }
                    if (point.x > sizeInScene.x)
                    {
                        point.x = sizeInScene.x;
                        if (!ignoreRight) countIgnore++;
                        ignoreRight = true;
                    }
                    if (point.y > sizeInScene.y)
                    {
                        point.y = sizeInScene.y;
                        if (!ignoreBottom) countIgnore++;
                        ignoreBottom = true;
                    }

                    localPoints[i] = point;
                }
            }

            if (backTriangles == null) backTriangles = new List<int>(6);
            else backTriangles.Clear();

            if (!rect.checkMapBoundaries || !rect.backgroundTexture)
            {
                uv.AddRange(new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) });
            }
            else
            {
                float uvx1 = Mathf.Max(-rect1.x / rect1.width, 0);
                float uvy1 = 1 - Mathf.Max(-rect1.y / rect1.height, 0);
                float uvx2 = Mathf.Min((rect2.width - rect1.x) / rect1.width, 1);
                float uvy2 = 1 - Mathf.Min((rect2.height - rect1.y) / rect1.height, 1);
                uv.AddRange(new []{ new Vector2(uvx1, uvy2), new Vector2(uvx2, uvy2), new Vector2(uvx2, uvy1), new Vector2(uvx1, uvy1) });
            }

            vertices.Add(new Vector3(-localPoints[0].x, -0.05f, localPoints[0].y));
            vertices.Add(new Vector3(-localPoints[1].x, -0.05f, localPoints[1].y));
            vertices.Add(new Vector3(-localPoints[2].x, -0.05f, localPoints[2].y));
            vertices.Add(new Vector3(-localPoints[3].x, -0.05f, localPoints[3].y));

            if (!ignoreTop)
            {
                vertices[2] += new Vector3(0, 0, rect.borderWidth);
                vertices[3] += new Vector3(0, 0, rect.borderWidth);
            }

            if (!ignoreBottom)
            {
                vertices[0] -= new Vector3(0, 0, rect.borderWidth);
                vertices[1] -= new Vector3(0, 0, rect.borderWidth);
            }

            if (!ignoreLeft)
            {
                vertices[0] -= new Vector3(rect.borderWidth, 0, 0);
                vertices[3] -= new Vector3(rect.borderWidth, 0, 0);
            }

            if (!ignoreRight)
            {
                vertices[1] += new Vector3(rect.borderWidth, 0, 0);
                vertices[2] += new Vector3(rect.borderWidth, 0, 0);
            }

            normals.Add(Vector3.up);
            normals.Add(Vector3.up);
            normals.Add(Vector3.up);
            normals.Add(Vector3.up);

            backTriangles.Add(0);
            backTriangles.Add(2);
            backTriangles.Add(1);
            backTriangles.Add(0);
            backTriangles.Add(3);
            backTriangles.Add(2);

            if (activePoints == null) activePoints = new List<Vector2>();
            else activePoints.Clear();

            if (countIgnore == 0)
            {
                activePoints.Add(localPoints[0] + new Vector2(rect.borderWidth, 0));
                activePoints.Add(localPoints[1]);
                activePoints.Add(localPoints[2]);
                activePoints.Add(localPoints[3]);
                activePoints.Add(localPoints[0] + new Vector2(0, rect.borderWidth));
                DrawActivePoints(activePoints, rect.borderWidth);
            }
            else if (countIgnore == 1)
            {
                int off = 0;
                if (ignoreTop) off = 3;
                else if (ignoreRight) off = 2;
                else if (ignoreBottom) off = 1;

                for (int i = 0; i < 4; i++)
                {
                    int ci = i + off;
                    if (ci > 3) ci -= 4;
                    activePoints.Add(localPoints[ci]);
                }
                DrawActivePoints(activePoints, rect.borderWidth);
            }
            else if (countIgnore == 2)
            {
                if (ignoreBottom && ignoreTop)
                {
                    activePoints.Add(localPoints[1]);
                    activePoints.Add(localPoints[2]);
                    DrawActivePoints(activePoints, rect.borderWidth);
                    activePoints.Add(localPoints[3]);
                    activePoints.Add(localPoints[0]);
                    DrawActivePoints(activePoints, rect.borderWidth);
                }
                else if (ignoreLeft && ignoreRight)
                {
                    activePoints.Add(localPoints[0]);
                    activePoints.Add(localPoints[1]);
                    DrawActivePoints(activePoints, rect.borderWidth);
                    activePoints.Add(localPoints[2]);
                    activePoints.Add(localPoints[3]);
                    DrawActivePoints(activePoints, rect.borderWidth);
                }
                else
                {
                    DrawActivePointsCI3(rect, ignoreTop, ignoreRight, ignoreBottom);
                }
            }
            else if (countIgnore == 3)
            {
                DrawActivePointsCI3(rect, ignoreTop, ignoreRight, ignoreBottom);
            }
            else if (countIgnore == 4)
            {
                DrawActivePoints(activePoints, rect.borderWidth);
            }

            Mesh mesh = data.mesh;
            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uv);
            mesh.subMeshCount = 2;

            data.active = true;

            mesh.SetTriangles(triangles.ToArray(), 0);
            mesh.SetTriangles(backTriangles.ToArray(), 1);

            UpdateMaterialsQueue(data, index);
        }
        
        private void DrawActivePointsCI3(Rectangle rect, bool ignoreTop, bool ignoreRight, bool ignoreBottom)
        {
            int off = 0;

            if (ignoreTop) off = 3;
            else if (ignoreRight) off = 2;
            else if (ignoreBottom) off = 1;

            for (int i = 0; i < 2; i++)
            {
                int ci = i + off;
                if (ci > 3) ci -= 4;
                activePoints.Add(localPoints[ci]);
            }

            DrawActivePoints(activePoints, rect.borderWidth);
        }
    }
}