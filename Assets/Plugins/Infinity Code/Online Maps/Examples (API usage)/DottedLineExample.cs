/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using System.Collections.Generic;
using OnlineMaps;
using UnityEngine;

namespace OnlineMapsExamples
{
    /// <summary>
    /// Example how to draw a dotted line in tileset.
    /// </summary>
    [AddComponentMenu(Utils.ExampleMenuPath + "DottedLineExample")]
    public class DottedLineExample : MonoBehaviour
    {
        /// <summary>
        /// Reference to the control. If not specified, the current instance will be used.
        /// </summary>
        public TileSetControl control;
        
        /// <summary>
        /// The thickness of the line.
        /// </summary>
        public float size = 10;

        /// <summary>
        /// Scale UV.
        /// </summary>
        public Vector2 uvScale = new Vector2(2, 1);

        /// <summary>
        /// The material used for line drawing.
        /// </summary>
        public Material material;

        private GeoPoint[] locations;
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private Mesh mesh;

        private float _size;

        private void Start()
        {
            // If control is not specified, use the current instance.
            if (!control && !(control = TileSetControl.instance))
            {
                Debug.LogError("TileSetControl not found");
                return;
            }
            
            // Create a new GameObject.
            GameObject container = new GameObject("Dotted Line");

            // Create a new Mesh.
            meshFilter = container.AddComponent<MeshFilter>();
            meshRenderer = container.AddComponent<MeshRenderer>();

            mesh = meshFilter.sharedMesh = new Mesh();
            mesh.name = "Dotted Line";
            mesh.MarkDynamic();

            meshRenderer.sharedMaterial = material;

            // Init coordinates of points.
            locations = new GeoPoint[5];

            locations[0] = new GeoPoint();
            locations[1] = new GeoPoint(3, 0);
            locations[2] = new GeoPoint(3, 3);
            locations[3] = new GeoPoint(4, 4);
            locations[4] = new GeoPoint(1, 6);

            // Subscribe to events of map.
            control.map.OnLocationChanged += UpdateLine;
            control.map.OnZoomChanged += UpdateLine;

            // Initial update line.
            UpdateLine();
        }

        private void Update()
        {
            // If size changed, then update line.
            if (Math.Abs(size - _size) > float.Epsilon) UpdateLine();
        }

        private void UpdateLine()
        {
            _size = size;

            float totalDistance = 0;
            Vector3 lastPosition = Vector3.zero;

            List<Vector3> vertices = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<Vector3> normals = new List<Vector3>();
            List<int> triangles = new List<int>();

            List<Vector3> positions = new List<Vector3>();

            for (int i = 0; i < locations.Length; i++)
            {
                // Get world position by coordinates
                Vector3 position = locations[i].ToWorld(control);
                positions.Add(position);

                if (i != 0)
                {
                    // Calculate angle between coordinates.
                    float a = Geometry.Angle2DRad(lastPosition, position, 90);

                    // Calculate offset
                    Vector3 off = new Vector3(Mathf.Cos(a) * size, 0, Mathf.Sin(a) * size);

                    // Init vertices, normals and triangles.
                    int vCount = vertices.Count;

                    vertices.Add(lastPosition + off);
                    vertices.Add(lastPosition - off);
                    vertices.Add(position + off);
                    vertices.Add(position - off);

                    normals.Add(Vector3.up);
                    normals.Add(Vector3.up);
                    normals.Add(Vector3.up);
                    normals.Add(Vector3.up);

                    triangles.Add(vCount);
                    triangles.Add(vCount + 3);
                    triangles.Add(vCount + 1);
                    triangles.Add(vCount);
                    triangles.Add(vCount + 2);
                    triangles.Add(vCount + 3);

                    totalDistance += (lastPosition - position).magnitude;
                }

                lastPosition = position;
            }

            float tDistance = 0;

            for (int i = 1; i < positions.Count; i++)
            {
                float distance = (positions[i - 1] - positions[i]).magnitude;

                // Updates UV
                uvs.Add(new Vector2(tDistance / totalDistance, 0));
                uvs.Add(new Vector2(tDistance / totalDistance, 1));

                tDistance += distance;

                uvs.Add(new Vector2(tDistance / totalDistance, 0));
                uvs.Add(new Vector2(tDistance / totalDistance, 1));
            }

            // Update mesh
            mesh.vertices = vertices.ToArray();
            mesh.normals = normals.ToArray();
            mesh.uv = uvs.ToArray();
            mesh.triangles = triangles.ToArray();
            mesh.RecalculateBounds();

            // Scale texture
            Vector2 scale = new Vector2(totalDistance / size, 1);
            scale.Scale(uvScale);
            meshRenderer.material.mainTextureScale = scale;
        }
    }
}