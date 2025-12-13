/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using System.Collections.Generic;
using OnlineMaps;
using UnityEngine;
using Object = UnityEngine.Object;

namespace OnlineMaps
{
    /// <summary>
    /// Draws the map on the tileset
    /// </summary>
    public class TileSetDrawer: MapDrawer
    {
        private GameObject gameObject;
        private BoxCollider boxCollider;
        private MeshCollider meshCollider;
        private MeshFilter meshFilter;
        private MeshRenderer renderer;
        private Mesh tilesetMesh;

        private Vector3[] vertices;
        private Vector2[] uv;
        private int[] triangles;

        private bool firstUpdate = true;
        private bool hasTrafficProp;
        private bool hasOverlayBackProp;
        private bool hasOverlayBackAlphaProp;
        private bool hasOverlayFrontProp;
        private bool hasOverlayFrontAlphaProp;
        private TilesetMeshProps meshProps = new TilesetMeshProps();
        private Color32[] overlayFrontBuffer;
        private Material tileMaterial;
        
        /// <summary>
        /// The control associated with the tileset.
        /// </summary>
        public TileSetControl control { get; private set; }
        
        /// <summary>
        /// The GameObject used for drawing elements.
        /// </summary>
        public GameObject drawingsGameObject { get; private set; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="control">The control associated with the tileset.</param>
        public TileSetDrawer(TileSetControl control)
        {
            this.control = control;
        }
        
        private void CacheMaterialProperties()
        {
            Material fMaterial = renderer.materials[0];
            hasTrafficProp = fMaterial.HasProperty("_TrafficTex");
            hasOverlayBackProp = fMaterial.HasProperty("_OverlayBackTex");
            hasOverlayBackAlphaProp = fMaterial.HasProperty("_OverlayBackAlpha");
            hasOverlayFrontProp = fMaterial.HasProperty("_OverlayFrontTex");
            hasOverlayFrontAlphaProp = fMaterial.HasProperty("_OverlayFrontAlpha");
        }

        public override void Dispose()
        {
            if (tilesetMesh) Object.Destroy(tilesetMesh);
            if (boxCollider) Object.Destroy(boxCollider);
            if (meshCollider) Object.Destroy(meshCollider);
            if (meshFilter) Object.Destroy(meshFilter);
            if (renderer) Object.Destroy(renderer);
            if (drawingsGameObject) Object.Destroy(drawingsGameObject);
            
            map = null;
            control = null;
            gameObject = null;
            boxCollider = null;
            meshCollider = null;
            meshFilter = null;
            renderer = null;
            tilesetMesh = null;
        }

        public override void Draw()
        {
            StateProps state = map.buffer.renderState;
            bool hasElevation = control.hasElevation;
            Vector2 sizeInScene = control.sizeInScene;
            GeoRect rect = map.view.rect;

            int w1 = map.view.countTilesX;
            int h1 = map.view.countTilesY;

            int subMeshVX = 1;
            int subMeshVZ = 1;
            
            if (hasElevation)
            {
                int res = control.elevationResolution;
                if (w1 < res) subMeshVX = res % w1 == 0 ? res / w1 : res / w1 + 1;
                if (h1 < res) subMeshVZ = res % h1 == 0 ? res / h1 : res / h1 + 1;
            }

            float zoomFactor = state.zoomFactor;

            double subMeshSizeX = sizeInScene.x / w1 / zoomFactor;
            double subMeshSizeY = sizeInScene.y / h1 / zoomFactor;

            int w = w1 + 2;
            int h = h1 + 2;

            if (vertices.Length != w * h * (subMeshVX + 1) * (subMeshVZ + 1))
            {
                ReinitMapMesh();
                CacheMaterialProperties();
            }

            Material[] materials = renderer.materials;
            TilePoint tlTile = state.centerMercator.ToTile(state.intZoom) - map.buffer.bufferPosition;
            tlTile.Subtract(w1 / 2d * zoomFactor, h1 / 2d * zoomFactor);
            tlTile.x = (tlTile.x + state.countTiles) % state.countTiles;

            meshProps.Set(subMeshSizeX, subMeshSizeY, subMeshVX, subMeshVZ);
            meshProps.w = w;
            meshProps.h = h;
            meshProps.startPosX = subMeshSizeX * tlTile.x;
            meshProps.startPosZ = -subMeshSizeY * tlTile.y;
            meshProps.yScale = ElevationManagerBase.GetElevationScale(rect, control.elevationManager);
            meshProps.rect = rect;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    UpdateMapSubMesh(x, y, materials);
                }
            }
            
            if (!hasElevation) meshProps.minY = meshProps.maxY = 0;

            tilesetMesh.vertices = vertices;
            tilesetMesh.uv = uv;

            tilesetMesh.RecalculateBounds();

            if (hasElevation || firstUpdate)
            {
                UpdateCollider(rect, meshProps.yScale, sizeInScene);
                firstUpdate = false;
            }
        }

        private void DrawDrawingElements()
        {
            if (control.drawingElementManager.count == 0) return;
            if (control.drawingMode != TileSetDrawingMode.meshes) return;
            if (!drawingsGameObject) InitDrawingsMesh();
            
            List<DrawingElement> items = control.drawingElementManager.items;
            DynamicMeshElementDrawerBase.InitMapDrawer(this);
            for (int i = 0; i < items.Count; i++)
            {
                DynamicMeshElementDrawerBase.Draw(items[i], i);
            }
            DynamicMeshElementDrawerBase.FreeReferences();
        }

        public override void DrawElements()
        {
            DrawDrawingElements();
            if (control.OnDrawMarkers != null) control.OnDrawMarkers();
        }

        private void GenerateTileVertices(TilesetMeshProps p, int x, int y, int i)
        {
            bool hasElevation = control.hasElevation;
            Vector2 sizeInScene = control.sizeInScene;
            
            float sizeInSceneX = -sizeInScene.x;
            float sizeInSceneZ = sizeInScene.y;

            double spx = p.startPosX - x * p.subMeshSizeX;
            double spz = p.startPosZ + y * p.subMeshSizeY;

            int subMeshVX = p.subMeshVX;
            int subMeshVZ = p.subMeshVZ;
            double uvX = p.uvX;
            double uvZ = p.uvZ;
            double cellSizeX = p.cellSizeX;
            double cellSizeY = p.cellSizeY;

            Vector3 v1 = new Vector3();
            Vector2 v2 = new Vector2();

            for (int ty = 0; ty <= subMeshVZ; ty++)
            {
                double uvy = 1 - uvZ * ty;
                double pz = spz + ty * cellSizeY;

                if (pz < 0)
                {
                    uvy = uvZ * ((pz + cellSizeY) / cellSizeY - 1) + uvy;
                    pz = 0;
                }
                else if (pz > sizeInSceneZ)
                {
                    uvy = uvZ * ((pz - sizeInSceneZ) / cellSizeY) + uvy;
                    pz = sizeInSceneZ;
                }

                for (int tx = 0; tx <= subMeshVX; tx++)
                {
                    double uvx = uvX * tx;
                    double px = spx - tx * cellSizeX;

                    if (px > 0)
                    {
                        uvx = uvX * (px - cellSizeX) / cellSizeX + uvx + uvX;
                        px = 0;
                    }
                    else if (px < sizeInSceneX)
                    {
                        uvx = uvX * ((px - sizeInSceneX) / cellSizeX - 1) + uvx + uvX;
                        px = sizeInSceneX;
                    }

                    v1.x = (float) px;
                    v1.z = (float) pz;

                    float fux = (float) uvx;
                    float fuy = (float) uvy;

                    if (hasElevation)
                    {
                        float fy = v1.y = control.elevationManager.GetElevationValue(px, pz, p.yScale, p.rect);
                        if (fy < p.minY) p.minY = fy;
                        if (fy > p.maxY) p.maxY = fy;
                    }

                    if (fux < 0) fux = 0;
                    else if (fux > 1) fux = 1;

                    if (fuy < 0) fuy = 0;
                    else if (fuy > 1) fuy = 1;

                    v2.x = fux;
                    v2.y = fuy;

                    vertices[i] = v1;
                    uv[i++] = v2;
                }
            }
        }

        private Texture GetEmptyTileTexture()
        {
            if (map.defaultTileTexture) return map.defaultTileTexture;
            if (RasterTile.emptyColorTexture) return RasterTile.emptyColorTexture;
        
            Texture tileTexture = RasterTile.emptyColorTexture = new Texture2D(1, 1, TextureFormat.ARGB32, control.mipmapForTiles);
            tileTexture.name = "Empty Texture";
            RasterTile.emptyColorTexture.SetPixel(0, 0, map.emptyColor);
            RasterTile.emptyColorTexture.Apply(false);

            return tileTexture;
        }

        private static (Texture, Vector2, Vector2) GetOverlayFrontTextureFromParent(Tile tile)
        {
            RasterTile t = tile.parent as RasterTile;

            while (t != null)
            {
                if (!t.overlayFrontTexture)
                {
                    t = t.parent as RasterTile;
                    continue;
                }

                int zoom = tile.zoom;
                int tx = tile.x;
                int ty = tile.y;

                int s = 1 << (zoom - t.zoom);
                float scale2 = 1f / s;
                Vector2 overlayTextureOffset = new Vector2(tx % s * scale2, (s - ty % s - 1) * scale2);
                Vector2 overlayTextureScale = new Vector2(scale2, scale2);
                return (t.overlayFrontTexture, overlayTextureOffset, overlayTextureScale);
            }

            return (null, Vector2.zero, Vector2.one);
        }

        private Tile GetTargetTile(Tile tile)
        {
            if (tile == null || tile.status == TileStatus.loaded) return tile;

            int tx = tile.x;
            int ty = tile.y;

            int zoom = tile.zoom;
            int z = zoom;

            while (z > 0)
            {
                z--;

                int s = 1 << (zoom - z);
                int ctx = tx / s;
                int cty = ty / s;

                Tile t;
                map.tileManager.GetTile(z, ctx, cty, out t);
                if (t != null && t.status == TileStatus.loaded) return t;
            }

            return null;
        }

        private void InitCollider(ColliderType colliderType)
        {
            if (colliderType == ColliderType.fullMesh || colliderType == ColliderType.simpleMesh)
            {
                meshCollider = gameObject.GetComponent<MeshCollider>();
                if (!meshCollider) meshCollider = gameObject.AddComponent<MeshCollider>();
                meshCollider.cookingOptions = MeshColliderCookingOptions.UseFastMidphase;
            }
            else if (colliderType == ColliderType.box || colliderType == ColliderType.flatBox)
            {
                boxCollider = gameObject.GetComponent<BoxCollider>();
                if (!boxCollider) boxCollider = gameObject.AddComponent<BoxCollider>();
            }
        }

        private void InitDrawingsMesh()
        {
            drawingsGameObject = new GameObject("Drawings");
            Transform transform = drawingsGameObject.transform;
            transform.parent = control.transform;
            transform.localPosition = new Vector3(0, control.sizeInScene.magnitude / 4344, 0);
            transform.localRotation = Quaternion.Euler(Vector3.zero);
            transform.localScale = Vector3.one;
            drawingsGameObject.layer = gameObject.layer;
        }

        private void InitMapSubMesh(ref Vector3[] normals, int x, int y, int w, int h, Vector2 subMeshSize, int subMeshVX, int subMeshVZ)
        {
            int i = (x + y * w) * (subMeshVX + 1) * (subMeshVZ + 1);

            Vector2 cellSize = new Vector2(subMeshSize.x / subMeshVX, subMeshSize.y / subMeshVZ);

            float sx = x > 0 && x < w - 1 ? cellSize.x : 0;
            float sy = y > 0 && y < h - 1 ? cellSize.y : 0;

            float nextY = subMeshSize.y * (y - 1);

            float uvX = 1f / subMeshVX;
            float uvZ = 1f / subMeshVZ;

            for (int ty = 0; ty <= subMeshVZ; ty++)
            {
                float nextX = -subMeshSize.x * (x - 1);
                float uvy = 1 - uvZ * ty;

                for (int tx = 0; tx <= subMeshVX; tx++)
                {
                    float uvx = 1 - uvX * tx;

                    vertices[i] = new Vector3(nextX, 0, nextY);
                    uv[i] = new Vector2(uvx, uvy);
                    normals[i++] = new Vector3(0.0f, 1f, 0.0f);
                
                    nextX -= sx;
                }

                nextY += sy;
            }
        }

        private void InitMapSubMeshTriangles(ref Material[] materials, int x, int y, int w, int subMeshVX, int subMeshVZ, Shader tileShader)
        {
            if (triangles == null) triangles = new int[subMeshVX * subMeshVZ * 6];
            int i = (x + y * w) * (subMeshVX + 1) * (subMeshVZ + 1);

            for (int ty = 0; ty < subMeshVZ; ty++)
            {
                int cy = ty * subMeshVX * 6;
                int py1 = i + ty * (subMeshVX + 1);
                int py2 = i + (ty + 1) * (subMeshVX + 1);

                for (int tx = 0; tx < subMeshVX; tx++)
                {
                    int ti = tx * 6 + cy;
                    int p1 = py1 + tx;
                    int p2 = p1 + 1;
                    int p3 = py2 + tx;
                    int p4 = p3 + 1;

                    triangles[ti] = p1;
                    triangles[ti + 1] = p2;
                    triangles[ti + 2] = p4;
                    triangles[ti + 3] = p1;
                    triangles[ti + 4] = p4;
                    triangles[ti + 5] = p3;
                }
            }

            tilesetMesh.SetTriangles(triangles, x + y * w);

            Material material = control.tileMaterial ? Object.Instantiate(control.tileMaterial) : new Material(tileShader);
            material.hideFlags = HideFlags.HideInInspector;

            if (map.defaultTileTexture) material.mainTexture = map.defaultTileTexture;
            materials[x + y * w] = material;
        }

        private void InitSimpleMeshCollider()
        {
            Mesh simpleMesh = new Mesh();
            simpleMesh.MarkDynamic();

            int res = control.hasElevation ? 6 : 1;
            int r2 = res + 1;
            Vector3[] clVertices = new Vector3[r2 * r2];
            int[] clTriangles = new int[res * res * 6];

            float sx = -control.sizeInScene.x / res;
            float sy = control.sizeInScene.y / res;

            int ti = 0;

            for (int y = 0; y < r2; y++)
            {
                for (int x = 0; x < r2; x++)
                {
                    clVertices[y * r2 + x] = new Vector3(sx * x, 0, sy * y);

                    if (x * y == 0) continue;
                    
                    int p4 = y * r2 + x;
                    int p3 = p4 - 1;
                    int p2 = p4 - r2;
                    int p1 = p2 - 1;

                    clTriangles[ti++] = p1;
                    clTriangles[ti++] = p2;
                    clTriangles[ti++] = p4;
                    clTriangles[ti++] = p1;
                    clTriangles[ti++] = p4;
                    clTriangles[ti++] = p3;
                }
            }

            simpleMesh.vertices = clVertices;
            simpleMesh.SetTriangles(clTriangles, 0);
            simpleMesh.RecalculateBounds();

            meshCollider.sharedMesh = simpleMesh;
        }

        public override void Initialize()
        {
            map = control.map;
            gameObject = control.gameObject;
            
            Shader tileShader = control.tilesetShader;
            ColliderType colliderType = control.colliderType;
            Vector2 sizeInScene = control.sizeInScene;

            boxCollider = null;

            if (!tilesetMesh)
            {
                meshFilter = control.GetComponent<MeshFilter>();
                if (!meshFilter) meshFilter = gameObject.AddComponent<MeshFilter>();

                renderer = control.GetComponent<MeshRenderer>();
                if (!renderer) renderer = gameObject.AddComponent<MeshRenderer>();
                renderer.hideFlags = HideFlags.HideInInspector;

                tilesetMesh = new Mesh
                {
                    name = "Tileset"
                };
            }
            else
            {
                meshFilter = control.GetComponent<MeshFilter>();
                renderer = control.GetComponent<MeshRenderer>();
                tilesetMesh.Clear();
            }
            
            InitCollider(colliderType);

            int w1 = map.buffer.renderState.width / Constants.TileSize;
            int h1 = map.buffer.renderState.height / Constants.TileSize;

            int subMeshVX = 1;
            int subMeshVZ = 1;

            if (control.hasElevation)
            {
                int elevationResolution = control.elevationResolution;
                if (w1 < elevationResolution) subMeshVX = elevationResolution % w1 == 0 ? elevationResolution / w1 : elevationResolution / w1 + 1;
                if (h1 < elevationResolution) subMeshVZ = elevationResolution % h1 == 0 ? elevationResolution / h1 : elevationResolution / h1 + 1;
            }
            
            Vector2 subMeshSize = new Vector2(sizeInScene.x / w1, sizeInScene.y / h1);

            int w = w1 + 2;
            int h = h1 + 2;

            int countVertices = w * h * (subMeshVX + 1) * (subMeshVZ + 1);
            vertices = new Vector3[countVertices];
            uv = new Vector2[countVertices];
            Vector3[] normals = new Vector3[countVertices];
            Material[] materials = new Material[w * h];
            tilesetMesh.subMeshCount = w * h;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    InitMapSubMesh(ref normals, x, y, w, h, subMeshSize, subMeshVX, subMeshVZ);
                }
            }

            tilesetMesh.vertices = vertices;
            tilesetMesh.uv = uv;
            tilesetMesh.normals = normals;

            triangles = null;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    InitMapSubMeshTriangles(ref materials, x, y, w, subMeshVX, subMeshVZ, tileShader);
                }
            }

            triangles = null;

            renderer.materials = materials;
            CacheMaterialProperties();

            tilesetMesh.MarkDynamic();
            tilesetMesh.RecalculateBounds();
            meshFilter.sharedMesh = tilesetMesh;

            if (colliderType == ColliderType.fullMesh) meshCollider.sharedMesh = Object.Instantiate(tilesetMesh);
            else if (colliderType == ColliderType.simpleMesh)
            {
                InitSimpleMeshCollider();
                meshCollider.sharedMesh = meshCollider.sharedMesh;
            }
            else if (boxCollider)
            {
                boxCollider.center = new Vector3(-sizeInScene.x / 2, 0, sizeInScene.y / 2);
                boxCollider.size = new Vector3(sizeInScene.x, 0, sizeInScene.y);
            }
        }

        public override bool Raycast(Ray ray, out RaycastHit hit, int maxRaycastDistance)
        {
            if (boxCollider) return boxCollider.Raycast(ray, out hit, maxRaycastDistance);
            if (meshCollider) return meshCollider.Raycast(ray, out hit, maxRaycastDistance);
            hit = new RaycastHit();
            return false;
        }

        protected void ReinitMapMesh()
        {
            int w1 = map.view.countTilesX;
            int h1 = map.view.countTilesY;

            int subMeshVX = 1;
            int subMeshVZ = 1;

            if (control.hasElevation)
            {
                int elevationResolution = control.elevationResolution;
                if (w1 < elevationResolution) subMeshVX = elevationResolution % w1 == 0 ? elevationResolution / w1 : elevationResolution / w1 + 1;
                if (h1 < elevationResolution) subMeshVZ = elevationResolution % h1 == 0 ? elevationResolution / h1 : elevationResolution / h1 + 1;
            }

            int w = w1 + 2;
            int h = h1 + 2;

            Material[] materials = renderer.materials;
            CacheMaterialProperties();

            vertices = new Vector3[w * h * (subMeshVX + 1) * (subMeshVZ + 1)];
            uv = new Vector2[vertices.Length];
            Vector3[] normals = new Vector3[vertices.Length];
            Array.Resize(ref materials, w * h);

            for (int i = 0; i < normals.Length; i++) normals[i] = new Vector3(0, 1, 0);
            tilesetMesh.Clear();
            tilesetMesh.vertices = vertices;
            tilesetMesh.uv = uv;
            tilesetMesh.normals = normals;

            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] != null) continue;

                if (tileMaterial != null) materials[i] = Object.Instantiate(tileMaterial);
                else materials[i] = new Material(control.tilesetShader);
                materials[i].hideFlags = HideFlags.HideInInspector;

                if (map.defaultTileTexture != null) materials[i].mainTexture = map.defaultTileTexture;
            }

            tilesetMesh.subMeshCount = w * h;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (triangles == null) triangles = new int[subMeshVX * subMeshVZ * 6];
                    int i = (x + y * w) * (subMeshVX + 1) * (subMeshVZ + 1);

                    for (int ty = 0; ty < subMeshVZ; ty++)
                    {
                        int cy = ty * subMeshVX * 6;
                        int py1 = i + ty * (subMeshVX + 1);
                        int py2 = i + (ty + 1) * (subMeshVX + 1);

                        for (int tx = 0; tx < subMeshVX; tx++)
                        {
                            int ti = tx * 6 + cy;
                            int p1 = py1 + tx;
                            int p2 = py1 + tx + 1;
                            int p3 = py2 + tx;
                            int p4 = py2 + tx + 1;

                            triangles[ti] = p1;
                            triangles[ti + 1] = p2;
                            triangles[ti + 2] = p4;
                            triangles[ti + 3] = p1;
                            triangles[ti + 4] = p4;
                            triangles[ti + 5] = p3;
                        }
                    }

                    tilesetMesh.SetTriangles(triangles, x + y * w);
                }
            }

            triangles = null;
            renderer.materials = materials;
            firstUpdate = true;
        }

        private void SetBackOverlayTexture(Tile tile, Material material)
        {
            if (!hasOverlayBackProp) return;

            Vector2 overlayTextureOffset = Vector2.zero;
            Vector2 overlayTextureScale = Vector2.one;
            Texture2D overlayTexture = (tile as RasterTile).overlayBackTexture;

            int zoom = tile.zoom;
            int tx = tile.x;
            int ty = tile.y;

            if (overlayTexture == null && control.overlayFromParentTiles)
            {
                RasterTile t = tile.parent as RasterTile;

                while (t != null)
                {
                    if (t.overlayBackTexture != null)
                    {
                        int s = 1 << (zoom - t.zoom);
                        float scale2 = 1f / s;
                        overlayTextureOffset.x = tx % s * scale2;
                        overlayTextureOffset.y = (s - ty % s - 1) * scale2;
                        overlayTextureScale = new Vector2(scale2, scale2);

                        overlayTexture = t.overlayBackTexture;
                        break;
                    }

                    t = t.parent as RasterTile;
                }
            }

            material.SetTexture(ShaderProperties.OverlayBackTex, overlayTexture);
            material.SetTextureOffset(ShaderProperties.OverlayBackTex, overlayTextureOffset);
            material.SetTextureScale(ShaderProperties.OverlayBackTex, overlayTextureScale);

            if (hasOverlayBackAlphaProp) material.SetFloat(ShaderProperties.OverlayBackAlpha, tile.overlayBackAlpha);
        }

        private void SetEmptyMaterials(Material material)
        {
            if (map.defaultTileTexture != null) material.mainTexture = map.defaultTileTexture;
            else
            {
                if (RasterTile.emptyColorTexture == null)
                {
                    RasterTile.emptyColorTexture = new Texture2D(1, 1, TextureFormat.ARGB32, control.mipmapForTiles);
                    RasterTile.emptyColorTexture.name = "Empty Texture";
                    RasterTile.emptyColorTexture.SetPixel(0, 0, map.emptyColor);
                    RasterTile.emptyColorTexture.Apply(false);
                }

                material.mainTexture = RasterTile.emptyColorTexture;
            }

            if (hasTrafficProp) material.SetTexture(ShaderProperties.TrafficTex, null);
            if (hasOverlayBackProp) material.SetTexture(ShaderProperties.OverlayBackTex, null);
            if (hasOverlayFrontProp) material.SetTexture(ShaderProperties.OverlayFrontTex, null);
        }

        private void SetFrontOverlayTexture(Tile tile, Material material)
        {
            if (!hasOverlayFrontProp) return;
            
            Texture texture = UpdateFrontOverlayTexture(tile);

            Vector2 overlayTextureOffset = Vector2.zero;
            Vector2 overlayTextureScale = Vector2.one;
        
            if (control.overlayFromParentTiles && !texture)
            {
                (texture, overlayTextureOffset, overlayTextureScale) = GetOverlayFrontTextureFromParent(tile);
            }

            material.SetTexture(ShaderProperties.OverlayFrontTex, texture);
            material.SetTextureOffset(ShaderProperties.OverlayFrontTex, overlayTextureOffset);
            material.SetTextureScale(ShaderProperties.OverlayFrontTex, overlayTextureScale);

            if (hasOverlayFrontAlphaProp) material.SetFloat(ShaderProperties.OverlayFrontAlpha, tile.overlayFrontAlpha);
        }

        private void SetTileMaterials(Tile tile, Tile targetTile, Texture tileTexture, bool sendEvent, Material material, Vector2 offset, float scale)
        {
            if (targetTile == null)
            {
                SetEmptyMaterials(material);
                return;
            }

            if (tileTexture == null)
            {
                tileTexture = GetEmptyTileTexture();
                sendEvent = false;
            }

            material.mainTextureOffset = offset;
            material.mainTextureScale = new Vector2(scale, scale);

            if (material.mainTexture != tileTexture)
            {
                material.mainTexture = tileTexture;
                if (sendEvent && control.OnChangeMaterialTexture != null) control.OnChangeMaterialTexture(targetTile, material);
            }

            SetTrafficTexture(targetTile, material);
            SetBackOverlayTexture(tile, material);
            SetFrontOverlayTexture(tile, material);

            if (control.OnDrawTile != null) control.OnDrawTile(tile, material);
        }

        private void SetTrafficTexture(Tile tile, Material material)
        {
            if (!hasTrafficProp) return;

            if (!map.traffic)
            {
                material.SetTexture(ShaderProperties.TrafficTex, null);
                return;
            }

            Vector2 trafficTextureOffset = material.mainTextureOffset;
            Vector2 trafficTextureScale = material.mainTextureScale;
            Texture2D trafficTexture = (tile as RasterTile).trafficTexture;

            int zoom = tile.zoom;
            int tx = tile.x;
            int ty = tile.y;

            if (trafficTexture == null && control.overlayFromParentTiles)
            {
                RasterTile t = tile.parent as RasterTile;

                while (t != null)
                {
                    if (t.trafficTexture != null)
                    {
                        int s = 1 << (zoom - t.zoom);
                        float scale2 = 1f / s;
                        trafficTextureOffset.x = tx % s * scale2;
                        trafficTextureOffset.y = (s - ty % s - 1) * scale2;
                        trafficTextureScale = new Vector2(scale2, scale2);

                        trafficTexture = t.trafficTexture;
                        break;
                    }

                    t = t.parent as RasterTile;
                }
            }

            material.SetTexture(ShaderProperties.TrafficTex, trafficTexture);
            material.SetTextureOffset(ShaderProperties.TrafficTex, trafficTextureOffset);
            material.SetTextureScale(ShaderProperties.TrafficTex, trafficTextureScale);
        }

        private void UpdateCollider(GeoRect rect, float yScale, Vector2 sizeInScene)
        {
            if (meshCollider)
            {
                UpdateMeshCollider(rect, yScale);
            }
            else if (boxCollider)
            {
                boxCollider.center = new Vector3(-sizeInScene.x / 2, (meshProps.minY + meshProps.maxY) / 2, sizeInScene.y / 2);
                boxCollider.size = new Vector3(sizeInScene.x, control.colliderType == ColliderType.box ? meshProps.maxY - meshProps.minY : 0, sizeInScene.y);
            }
        }

        private Texture UpdateFrontOverlayTexture(Tile tile)
        {
            if (control.drawingMode != TileSetDrawingMode.overlay) return null;
            if (tile.overlayFrontTexture) return tile.overlayFrontTexture;
            if (tile.status != TileStatus.loaded || !tile.drawingChanged) return null;
            
            if (overlayFrontBuffer == null) overlayFrontBuffer = new Color32[Constants.SqrTileSize];
            else
            {
                Array.Clear(overlayFrontBuffer, 0, Constants.SqrTileSize);
            }
            
            Vector2Int currentBufferPosition = new Vector2Int(tile.x, tile.y);

            foreach (DrawingElement el in control.drawingElementManager)
            {
                BufferElementDrawerBase.Draw(
                    this,
                    el, 
                    overlayFrontBuffer, 
                    currentBufferPosition, 
                    Constants.TileSize, 
                    Constants.TileSize, 
                    tile.zoom);
            }

            if (!tile.overlayFrontTexture)
            {
                tile.overlayFrontTexture = new Texture2D(Constants.TileSize, Constants.TileSize, TextureFormat.ARGB32, control.mipmapForTiles);
                tile.overlayFrontTexture.wrapMode = TextureWrapMode.Clamp;
            }

            tile.overlayFrontTexture.SetPixels32(overlayFrontBuffer);
            tile.overlayFrontTexture.Apply(false);
            
            return tile.overlayFrontTexture;
        }

        private void UpdateMapSubMesh(int x, int y, Material[] materials)
        {
            TilesetMeshProps p = meshProps;
            int mi = x + y * p.w;
            int i = mi * (p.subMeshVX + 1) * (p.subMeshVZ + 1);

            Vector2Int bufferPosition = map.buffer.bufferPosition;
            int tx = x + bufferPosition.x;
            int ty = y + bufferPosition.y;

            int zoom = map.buffer.renderState.intZoom;
            int countTiles = map.buffer.renderState.countTiles;

            if (tx >= countTiles) tx -= countTiles;
            if (tx < 0) tx += countTiles;

            Tile tile;
            map.tileManager.GetTile(zoom, tx, ty, out tile);

            Vector2 offset = Vector2.zero;
            float scale = 1;
            Texture tileTexture = null;

            Tile targetTile = GetTargetTile(tile);
            bool sendEvent = true;

            if (targetTile != null)
            {
                if (tile != targetTile)
                {
                    sendEvent = false;
                    int s = 1 << (zoom - targetTile.zoom);
                    tileTexture = targetTile.texture;
                    scale = 1f / s;
                    offset.x = tx % s * scale;
                    offset.y = (s - ty % s - 1) * scale;
                }
                else tileTexture = tile.texture;
            }

            GenerateTileVertices(p, x, y, i);

            Material material = materials[mi];
            material.hideFlags = HideFlags.HideInInspector;

            SetTileMaterials(tile, targetTile, tileTexture, sendEvent, material, offset, scale);
            if (control.OnUpdateMapSubMeshLate != null) control.OnUpdateMapSubMeshLate(tile, material);
        }
        
        private void UpdateMeshCollider(GeoRect rect, float yScale)
        {
            if (control.colliderType == ColliderType.fullMesh)
            {
                if (meshCollider.sharedMesh) Utils.Destroy(meshCollider.sharedMesh);
                meshCollider.sharedMesh = Object.Instantiate(tilesetMesh);
            }
            else UpdateSimpleMeshCollider(rect, yScale);
        }

        private void UpdateSimpleMeshCollider(GeoRect rect, float yScale)
        {
            bool hasElevation = control.hasElevation;
            Vector2 sizeInScene = control.sizeInScene;

            int res = hasElevation ? 6 : 1;
            int r2 = res + 1;

            Vector3[] vertices = new Vector3[r2 * r2];
            float sx = -sizeInScene.x / res;
            float sy = sizeInScene.y / res;

            int[] triangles = new int[res * res * 6];
            int ti = 0;

            for (int y = 0; y < r2; y++)
            {
                for (int x = 0; x < r2; x++)
                {
                    float px = sx * x;
                    float pz = sy * y;

                    float py = 0;
                    if (hasElevation) py = control.elevationManager.GetElevationValue(px, pz, yScale, rect);

                    vertices[y * r2 + x] = new Vector3(sx * x, py, sy * y);

                    if (x != 0 && y != 0)
                    {
                        int p4 = y * r2 + x;
                        int p3 = p4 - 1;
                        int p2 = p4 - r2;
                        int p1 = p2 - 1;

                        triangles[ti++] = p1;
                        triangles[ti++] = p2;
                        triangles[ti++] = p4;
                        triangles[ti++] = p1;
                        triangles[ti++] = p4;
                        triangles[ti++] = p3;
                    }
                }
            }

            Mesh mesh = meshCollider.sharedMesh;
            mesh.Clear();
            mesh.vertices = vertices;
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            meshCollider.sharedMesh = mesh;
        }
    }
}