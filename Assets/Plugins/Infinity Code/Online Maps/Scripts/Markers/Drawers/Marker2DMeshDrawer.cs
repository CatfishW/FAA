/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System.Collections.Generic;
using UnityEngine;

namespace OnlineMaps
{
    /// <summary>
    /// Base class for drawing markers on a mesh
    /// </summary>
    public abstract class Marker2DMeshDrawer : Marker2DDrawer
    {
        protected ControlBaseDynamicMesh control;
        protected List<GameObject> markersGameObjects;
        protected List<Mesh> markersMeshes;
        protected List<Renderer> markersRenderers;

        protected Mesh InitMarkersMesh(int index)
        {
            string name = "Markers";
            if (index > 0) name += index;
            GameObject go = new GameObject(name);
            go.transform.parent = control.transform;
            go.layer = control.gameObject.layer;

            if (control is TileSetControl)
            {
                go.transform.localPosition = new Vector3(0, control.sizeInScene.magnitude / 2896, 0);
                go.transform.localRotation = Quaternion.Euler(Vector3.zero);
            }
            else go.transform.localPosition = new Vector3(0, 0.2f, 0);
            go.transform.localScale = Vector3.one;

            if (markersGameObjects == null) markersGameObjects = new List<GameObject>();
            if (markersRenderers == null) markersRenderers = new List<Renderer>();
            if (markersMeshes == null) markersMeshes = new List<Mesh>();

            markersGameObjects.Add(go);
            markersRenderers.Add(go.AddComponent<MeshRenderer>());

            MeshFilter meshFilter = go.AddComponent<MeshFilter>();
            Mesh mesh = new Mesh();
            mesh.name = "MarkersMesh";
            mesh.MarkDynamic();
            meshFilter.mesh = mesh;
            markersMeshes.Add(mesh);

            return mesh;
        }
    }
}