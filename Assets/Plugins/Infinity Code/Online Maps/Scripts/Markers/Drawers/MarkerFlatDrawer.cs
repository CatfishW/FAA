/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace OnlineMaps
{
    /// <summary>
    /// Implements the display of flat 2D markers on dynamic mesh control
    /// </summary>
    public class MarkerFlatDrawer : Marker2DMeshDrawer
    {
        /// <summary>
        /// Checks if 2D marker is visible
        /// </summary>
        public Predicate<Marker2D> OnCheckMarker2DVisibility;

        /// <summary>
        /// Called when generating vertices of markers
        /// </summary>
        public Action<Marker2D, List<Vector3>, int> OnGenerateMarkerVertices;

        /// <summary>
        /// Gets the marker offset along the Y axis from the map
        /// </summary>
        public Func<Marker2D, float> OnGetFlatMarkerOffsetY;

        /// <summary>
        /// Called after setting the value for marker mesh
        /// </summary>
        public Action<Mesh, Renderer> OnSetMarkersMesh;

        /// <summary>
        /// Allows you to change the order of drawing markers
        /// </summary>
        public IComparer<Marker2D> markerComparer;

        private List<Vector3> markersVertices;
        private List<FlatMarker> usedMarkers;
        private List<Texture> usedTextures;
        private List<List<int>> usedTexturesMarkerIndex;
        private int usedMarkersCount;
        private int meshIndex;
        private float[] offsets;
        private Bounds tilesetBounds;
        private float yScale;
        private float zoom;
        private GeoRect mapRect;
        private bool elevationActive;
        private Vector3 transformPosition;
        private Vector2 sceneToLocal;
        private float zoomFactor;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="control">Reference to dynamic mesh control</param>
        public MarkerFlatDrawer(ControlBaseDynamicMesh control)
        {
            this.control = control;
            map = control.map;
            control.OnDrawMarkers += OnDrawMarkers;
        }

        public override void Dispose()
        {
            base.Dispose();

            control.OnDrawMarkers -= OnDrawMarkers;
            control = null;

            if (markersGameObjects != null)
            {
                foreach (GameObject go in markersGameObjects)
                {
                    Utils.Destroy(go);
                }
            }
                
            if (usedMarkers != null)
            {
                foreach (FlatMarker flatMarker in usedMarkers)
                {
                    flatMarker.Dispose();
                }
            }

            markerComparer = null;
            markersGameObjects = null;
            markersMeshes = null;
            markersRenderers = null;
            markersVertices = null;
            usedMarkers = null;

            OnCheckMarker2DVisibility = null;
            OnGenerateMarkerVertices = null;
            OnGetFlatMarkerOffsetY = null;
            OnSetMarkersMesh = null;
        }

        private bool DrawMarker(Marker2D marker, Map map, float offsetY)
        {
            TilePoint topLeftTile = map.view.topLeftTile;
            int countTiles = map.buffer.renderState.countTiles;

            TilePoint tp = marker.GetTilePosition();
            Vector2 offset = marker.GetAlignOffset();
            offset *= marker.scale;

            tp.x -= topLeftTile.x;
            if (tp.x < 0) tp.x += countTiles;

            tp.x = tp.x * zoomFactor - offset.x;
            tp.y = (tp.y - topLeftTile.y) * zoomFactor - offset.y;

            if (!marker.texture)
            {
                marker.texture = control.marker2DManager.defaultTexture;
                marker.Init();
            }

            if (!marker.texture)
            {
                Debug.LogError("Marker texture is null");
                return false;
            }

            float markerWidth = marker.texture.width * marker.scale;
            float markerHeight = marker.texture.height * marker.scale;

            float rx1 = (float)(tp.x * sceneToLocal.x);
            float ry1 = (float)(tp.y * sceneToLocal.y);
            float rx2 = (float)((tp.x + markerWidth) * sceneToLocal.x);
            float ry2 = (float)((tp.y + markerHeight) * sceneToLocal.y);

            Vector3 center = new Vector3((float)((tp.x + offset.x) * sceneToLocal.x), 0, (float)((tp.y + offset.y) * sceneToLocal.y));

            Vector3 p1 = new Vector3(rx1 - center.x, 0, ry1 - center.z);
            Vector3 p2 = new Vector3(rx2 - center.x, 0, ry1 - center.z);
            Vector3 p3 = new Vector3(rx2 - center.x, 0, ry2 - center.z);
            Vector3 p4 = new Vector3(rx1 - center.x, 0, ry2 - center.z);

            float angle = Mathf.Repeat(marker.rotation, 360);

            if (Math.Abs(angle) > float.Epsilon)
            {
                Matrix4x4 matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0, angle, 0), Vector3.one);

                p1 = matrix.MultiplyPoint(p1) + center;
                p2 = matrix.MultiplyPoint(p2) + center;
                p3 = matrix.MultiplyPoint(p3) + center;
                p4 = matrix.MultiplyPoint(p4) + center;
            }
            else
            {
                p1 += center;
                p2 += center;
                p3 += center;
                p4 += center;
            }

            if (control.checkMarker2DVisibility == TileSetCheckMarker2DVisibility.bounds)
            {
                Vector3 markerCenter = (p2 + p4) / 2;
                Vector3 markerSize = p4 - p2;
                if (!tilesetBounds.Intersects(new Bounds(markerCenter, markerSize))) return false;
            }

            float y = elevationActive ? elevationManager.GetElevationValue((rx1 + rx2) / 2, (ry1 + ry2) / 2, yScale, mapRect) : 0;
            p1.y = p2.y = p3.y = p4.y = y + offsetY;

            int vIndex = markersVertices.Count;

            markersVertices.Add(p1);
            markersVertices.Add(p2);
            markersVertices.Add(p3);
            markersVertices.Add(p4);

            usedMarkers.Add(new FlatMarker(marker, p1 + transformPosition, p2 + transformPosition, p3 + transformPosition, p4 + transformPosition));

            if (OnGenerateMarkerVertices != null) OnGenerateMarkerVertices(marker, markersVertices, vIndex);

            if (marker.texture == control.marker2DManager.defaultTexture)
            {
                usedTexturesMarkerIndex[0].Add(usedMarkersCount);
            }
            else
            {
                int textureIndex = usedTextures.IndexOf(marker.texture);
                if (textureIndex != -1)
                {
                    usedTexturesMarkerIndex[textureIndex].Add(usedMarkersCount);
                }
                else
                {
                    usedTextures.Add(marker.texture);
                    usedTexturesMarkerIndex.Add(new List<int>(32));
                    usedTexturesMarkerIndex[usedTexturesMarkerIndex.Count - 1].Add(usedMarkersCount);
                }
            }

            IncreaseMarkerCount();

            return true;
        }

        public override Marker2D GetMarkerFromScreen(Vector2 screenPosition)
        {
            if (usedMarkers == null || usedMarkers.Count == 0) return null;

            RaycastHit hit;
            Ray ray = control.currentCamera.ScreenPointToRay(screenPosition);
            if (!control.mapDrawer.Raycast(ray, out hit, Constants.MaxRaycastDistance)) return null;

            for (int i = usedMarkers.Count - 1; i >= 0; i--)
            {
                FlatMarker m = usedMarkers[i];
                if (m.Contains(hit.point, control.transform)) return m.marker;
            }

            return null;
        }

        private IEnumerable<Marker2D> GetMarkers()
        {
            Marker2DManager manager = control.marker2DManager;
            if (!manager.enabled) return new List<Marker2D>();

            if (OnCheckMarker2DVisibility != null)
            {
                return manager.Where(m => m.enabled && m.range.Contains(zoom) && OnCheckMarker2DVisibility(m));
            }

            if (control.checkMarker2DVisibility == TileSetCheckMarker2DVisibility.bounds) return manager;

            return manager.Where(m =>
            {
                if (!m.enabled || !m.range.Contains(zoom)) return false;
                return mapRect.ContainsWrapped(m.location);
            });
        }

        private void IncreaseMarkerCount()
        {
            if (++usedMarkersCount < 16250) return;

            SetMarkersMesh();
            meshIndex++;
            markersVertices.Clear();
            usedMarkersCount = 0;
            usedTextures.Clear();
            usedTextures.Add(control.marker2DManager.defaultTexture);
            usedTexturesMarkerIndex.Clear();
            usedTexturesMarkerIndex.Add(new List<int>(32));
        }

        private void OnDrawMarkers()
        {
            if (markersGameObjects == null) InitMarkersMesh(0);

            Prepare();

            IEnumerable<Marker2D> markers = GetMarkers();
            markers = SortMarkers(markers);
            int index = 0;

            foreach (Marker2D marker in markers)
            {
                float offsetY = offsets != null? offsets[index++] : 0;
                if (!DrawMarker(marker, map, offsetY)) return;
            }

            SetMarkersMesh();
        }

        private void Prepare()
        {
            if (usedMarkers == null) usedMarkers = new List<FlatMarker>(32);
            else
            {
                for (int i = 0; i < usedMarkers.Count; i++) usedMarkers[i].Dispose();
                usedMarkers.Clear();
            }

            usedTextures = new List<Texture>(32) { control.marker2DManager.defaultTexture };
            usedTexturesMarkerIndex = new List<List<int>>(32) { new List<int>(32) };

            if (markersVertices == null) markersVertices = new List<Vector3>(64);
            else markersVertices.Clear();

            foreach (Mesh mesh in markersMeshes) mesh.Clear();
            usedMarkersCount = 0;
            meshIndex = 0;
            offsets = null;
            
            StateProps state = map.buffer.renderState;
            mapRect = map.view.rect.rightFixed;
            Vector2 sizeInScene = control.sizeInScene;
            zoom = state.intZoom;
            yScale = ElevationManagerBase.GetElevationScale(mapRect, _elevationManager);
            
            sceneToLocal = new Vector2(-sizeInScene.x / state.width, sizeInScene.y / state.height);

            tilesetBounds = new Bounds(
                new Vector3(sizeInScene.x / -2, 0, sizeInScene.y / 2),
                new Vector3(sizeInScene.x, 0, sizeInScene.y));
            
            transformPosition = control.transform.position;
            elevationActive = hasElevation;
            zoomFactor = Constants.TileSize / state.zoomFactor;
        }

        private void SetMarkersMesh()
        {
            Vector2[] markersUV = new Vector2[markersVertices.Count];
            Vector3[] markersNormals = new Vector3[markersVertices.Count];

            Vector2 uvp1 = new Vector2(1, 1);
            Vector2 uvp2 = new Vector2(0, 1);
            Vector2 uvp3 = new Vector2(0, 0);
            Vector2 uvp4 = new Vector2(1, 0);

            for (int i = 0; i < usedMarkersCount; i++)
            {
                int vi = i * 4;
                markersNormals[vi] = Vector3.up;
                markersNormals[vi + 1] = Vector3.up;
                markersNormals[vi + 2] = Vector3.up;
                markersNormals[vi + 3] = Vector3.up;

                markersUV[vi] = uvp2;
                markersUV[vi + 1] = uvp1;
                markersUV[vi + 2] = uvp4;
                markersUV[vi + 3] = uvp3;
            }

            if (markersGameObjects == null) InitMarkersMesh(meshIndex);

            Mesh markersMesh = markersMeshes.Count > meshIndex ? markersMeshes[meshIndex] : null;
            if (!markersMesh) markersMesh = InitMarkersMesh(meshIndex);

            markersMesh.SetVertices(markersVertices);

            markersMesh.uv = markersUV;
            markersMesh.normals = markersNormals;

            Renderer markersRenderer = markersRenderers[meshIndex];
            if (markersRenderer.materials.Length != usedTextures.Count) markersRenderer.materials = new Material[usedTextures.Count];

            markersMesh.subMeshCount = usedTextures.Count;

            for (int i = 0; i < usedTextures.Count; i++)
            {
                int markerCount = usedTexturesMarkerIndex[i].Count;
                int[] markersTriangles = new int[markerCount * 6];

                for (int j = 0; j < markerCount; j++)
                {
                    int vi = usedTexturesMarkerIndex[i][j] * 4;
                    int vj = j * 6;

                    markersTriangles[vj + 0] = vi;
                    markersTriangles[vj + 1] = vi + 1;
                    markersTriangles[vj + 2] = vi + 2;
                    markersTriangles[vj + 3] = vi;
                    markersTriangles[vj + 4] = vi + 2;
                    markersTriangles[vj + 5] = vi + 3;
                }

                markersMesh.SetTriangles(markersTriangles, i);

                Material material = markersRenderer.materials[i];
                if (!material)
                {
                    if (control.markerMaterial) material = markersRenderer.materials[i] = new Material(control.markerMaterial);
                    else material = markersRenderer.materials[i] = new Material(control.markerShader);
                }

                if (material.mainTexture != usedTextures[i])
                {
                    if (control.markerMaterial)
                    {
                        material.shader = control.markerMaterial.shader;
                        material.CopyPropertiesFromMaterial(control.markerMaterial);
                        material.name = control.markerMaterial.name;
                    }
                    else
                    {
                        material.shader = control.markerShader;
                        material.color = Color.white;
                    }

                    material.SetTexture(ShaderProperties.MainTex, usedTextures[i]);
                }
            }

            if (OnSetMarkersMesh != null) OnSetMarkersMesh(markersMesh, markersRenderer);
        }

        private IEnumerable<Marker2D> SortMarkers(IEnumerable<Marker2D> markers)
        {
            if (markerComparer != null) return markers.OrderBy(m => m, markerComparer);

            markers = markers.OrderBy(m => 90 - m.location.y);
            if (OnGetFlatMarkerOffsetY == null) return markers;

            SortedMarker[] sortedMarkers = markers.Select(m => new SortedMarker
            {
                marker = m,
                offset = OnGetFlatMarkerOffsetY(m)
            }).ToArray();

            offsets = new float[sortedMarkers.Length];
            Marker2D[] nMarkers = new Marker2D[sortedMarkers.Length];
            int i = 0;
            foreach (SortedMarker sm in sortedMarkers.OrderBy(m => m.offset))
            {
                nMarkers[i] = sm.marker;
                offsets[i] = sm.offset;
                i++;
                sm.Dispose();
            }

            markers = nMarkers;

            return markers;
        }

        internal class FlatMarker
        {
            public Marker2D marker;
            private double[] poly;

            public FlatMarker(Marker2D marker, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 p4)
            {
                this.marker = marker;
                poly = new double[] { p1.x, p1.z, p2.x, p2.z, p3.x, p3.z, p4.x, p4.z };
            }

            public bool Contains(Vector3 point, Transform transform)
            {
                Vector3 p = Quaternion.Inverse(transform.rotation) * (point - transform.position);
                p.x /= transform.lossyScale.x;
                p.z /= transform.lossyScale.z;
                p += transform.position;
                return Geometry.IsPointInPolygon(poly, p.x, p.z);
            }

            public void Dispose()
            {
                marker = null;
                poly = null;
            }
        }

        internal class SortedMarker
        {
            public Marker2D marker;
            public float offset;

            public void Dispose()
            {
                marker = null;
            }
        }
    }
}