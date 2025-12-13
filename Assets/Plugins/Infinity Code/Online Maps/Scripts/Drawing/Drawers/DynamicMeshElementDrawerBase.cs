/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace OnlineMaps
{
    /// <summary>
    /// Abstract base class for dynamic mesh drawing in OnlineMaps.
    /// Provides core logic for managing mesh data, drawing elements, and handling map-related calculations.
    /// Inherit from this class to implement custom dynamic mesh drawers.
    /// </summary>
    public abstract class DynamicMeshElementDrawerBase : DrawingElementDrawerBase
    {
        /// <summary>
        /// List of mesh vertices.
        /// </summary>
        protected static List<Vector3> vertices = new List<Vector3>();
        
        /// <summary>
        /// List of mesh normals.
        /// </summary>
        protected static List<Vector3> normals = new List<Vector3>();
        
        /// <summary>
        /// List of mesh triangle indices.
        /// </summary>
        protected static List<int> triangles = new List<int>();
        
        /// <summary>
        /// List of mesh UV coordinates.
        /// </summary>
        protected static List<Vector2> uv = new List<Vector2>();
        
        /// <summary>
        /// List of local points used for mesh calculations.
        /// </summary>
        protected static List<Vector2> localPoints = new List<Vector2>();

        /// <summary>
        /// Reference to the map drawer (TileSetDrawer).
        /// </summary>
        protected static TileSetDrawer mapDrawer;
        
        /// <summary>
        /// The current drawing element being processed.
        /// </summary>
        protected static DrawingElement element;
        
        /// <summary>
        /// Reference to the current map.
        /// </summary>
        protected static Map map;
        
        /// <summary>
        /// Reference to the current map view.
        /// </summary>
        protected static MapView mapView;
        
        /// <summary>
        /// The rectangle representing the current map area.
        /// </summary>
        protected static GeoRect mapRect;
        
        /// <summary>
        /// Reference to the dynamic mesh control.
        /// </summary>
        protected static ControlBaseDynamicMesh control;
        
        /// <summary>
        /// Reference to the elevation manager.
        /// </summary>
        protected static ElevationManagerBase elevationManager;
        
        /// <summary>
        /// The best elevation Y scale for the current map area.
        /// </summary>
        protected static float bestElevationYScale;
        
        /// <summary>
        /// Indicates if elevation is enabled.
        /// </summary>
        protected static bool hasElevation;
        
        /// <summary>
        /// Array of Mercator points for the current drawing element.
        /// </summary>
        protected static MercatorPoint[] mercatorPoints;
        
        /// <summary>
        /// The size of the map in the scene.
        /// </summary>
        protected static Vector2 sizeInScene;
        
        private static Dictionary<Type, DynamicMeshElementDrawerBase> drawers = new Dictionary<Type, DynamicMeshElementDrawerBase>();
        private static Dictionary<DrawingElement, ElementData> elementData = new Dictionary<DrawingElement, ElementData>();

        private static void AddLineSegment(Vector3 s1, Vector3 s2, Vector3 prevS1, Vector3 prevS2)
        {
            int ti = vertices.Count;
            vertices.AddRange(new[] { prevS1, s1, s2, prevS2 });
            normals.AddRange(new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up });
            uv.AddRange(new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) });
            triangles.AddRange(new[] { ti, ti + 1, ti + 2, ti, ti + 2, ti + 3 });
        }

        /// <summary>
        /// Calculates local points for the current drawing element based on Mercator coordinates,
        /// map zoom, and tile positions. Handles map wrapping, optimization, and optionally closes
        /// the shape by connecting the last point to the first. Populates the localPoints list
        /// with the calculated points for mesh generation.
        /// </summary>
        /// <param name="closed">If true, closes the shape by connecting the last point to the first.</param>
        /// <param name="optimize">If true, skips redundant points for optimization.</param>
        protected static void CalculateLocalPoints(bool closed = false, bool optimize = true)
        {
            if (mercatorPoints.Length < 2) return; 
            
            int zoom = mapView.intZoom;
            float zoomFactor = mapView.zoomFactor;
            int maxTiles = mapView.maxTiles;
            int halfMax = maxTiles / 2;

            TilePoint st = mapRect.topLeft.ToTile(map);

            double ppx = 0;
            double scaleX = Constants.TileSize * sizeInScene.x / map.buffer.renderState.width / zoomFactor;
            double scaleY = Constants.TileSize * sizeInScene.y / map.buffer.renderState.height / zoomFactor;
            
            bool isOptimized = false;
            int mapTileWidth = map.control.width / Constants.TileSize / 2;
            
            MercatorPoint p = mercatorPoints[0];
            TilePoint tpr = p.ToTile(zoom);
            TilePoint tp = tpr - st;

            double ox = tp.x - mapTileWidth;
            if (ox < -halfMax) tp.x += maxTiles;
            else if (ox > halfMax) tp.x -= maxTiles;
            
            ppx = tp.x;

            Vector2 lp = new Vector2((float)(tp.x * scaleX), (float)(tp.y * scaleY));
            localPoints.Add(lp);

            for (int i = 1; i < mercatorPoints.Length; i++)
            {
                p = mercatorPoints[i];
                tp = p.ToTile(zoom);
                isOptimized = false;

                if (optimize)
                {
                    if ((tpr - tp).sqrMagnitude < 0.0001)
                    {
                        isOptimized = true;
                        continue;
                    }
                }

                tpr = tp;
                tp -= st;
                
                ox = tp.x - ppx;
                int maxIt = 3;
                while (maxIt-- > 0)
                {
                    if (ox < -halfMax)
                    {
                        tp.x += maxTiles;
                        ox += maxTiles;
                    }
                    else if (ox > halfMax)
                    {
                        tp.x -= maxTiles;
                        ox -= maxTiles;
                    }
                    else break;
                }

                ppx = tp.x;

                lp = new Vector2((float)(tp.x * scaleX), (float)(tp.y * scaleY));
                localPoints.Add(lp);
            }

            if (isOptimized)
            {
                tp -= st;

                if (mercatorPoints.Length == 0)
                {
                    ox = tp.x - mapTileWidth;
                    if (ox < -halfMax) tp.x += maxTiles;
                    else if (ox > halfMax) tp.x -= maxTiles;
                }
                else
                {
                    ox = tp.x - ppx;
                    int maxIt = 3;
                    while (maxIt-- > 0)
                    {
                        if (ox < -halfMax)
                        {
                            tp.x += maxTiles;
                            ox += maxTiles;
                        }
                        else if (ox > halfMax)
                        {
                            tp.x -= maxTiles;
                            ox -= maxTiles;
                        }
                        else break;
                    }
                }

                double rx1 = tp.x * scaleX;
                double ry1 = tp.y * scaleY;

                Vector2 np = new Vector2((float)rx1, (float)ry1);
                localPoints.Add(np);
            }

            if (closed && (localPoints[0] - localPoints[localPoints.Count - 1]).magnitude > sizeInScene.x / 256)
            {
                localPoints.Add(localPoints[0]);
            }
        }

        /// <summary>
        /// Draws the specified drawing element using the appropriate dynamic mesh drawer.
        /// </summary>
        /// <param name="element">The drawing element to draw.</param>
        /// <param name="index">The index of the element in the drawing sequence.</param>
        public static void Draw(DrawingElement element, int index)
        {
            GetDrawer(element)?.DrawElement(element, index);
        }

        /// <summary>
        /// Draws the specified drawing element at the given index.
        /// Implement this method in derived classes to handle the actual drawing logic for the element.
        /// </summary>
        /// <param name="element">The drawing element to draw.</param>
        /// <param name="index">The index of the element in the drawing sequence.</param>
        /// <returns>True if the element was drawn successfully; otherwise, false.</returns>
        protected abstract bool DrawElement(DrawingElement element, int index);

        protected static void DrawActivePoints(List<Vector2> activePoints, float width)
        {
            if (activePoints.Count < 2)
            {
                activePoints.Clear();
                return;
            }

            List<Vector2> points = activePoints;

            if (element.splitToPieces) points = SplitToPieces(points);

            float w2 = width * 2;

            Vector3 prevS1, prevS2;

            int c = points.Count - 1;
            bool extraPointAdded = false;

            DrawFirstAndLastActivePoints(points, 0, -points[0].x, points[0].y, width, out prevS1, out prevS2);
            for (int i = 1; i < c; i++)
            {
                DrawIntermediateActicePoints(width, points, ref i, w2, ref extraPointAdded, ref c, ref prevS1, ref prevS2);
            }
            
            DrawFirstAndLastActivePoints(points, c, -points[c].x, points[c].y, width, out Vector3 s1, out Vector3 s2);
            AddLineSegment(s1, s2, prevS1, prevS2);

            activePoints.Clear();
        }

        protected static void DrawFirstAndLastActivePoints(List<Vector2> points, int i, float px, float pz, float width, out Vector3 s1, out Vector3 s2)
        {
            float p1x, p1z, p2x, p2z;

            if (i == 0)
            {
                p1x = px;
                p1z = pz;
                p2x = -points[1].x;
                p2z = points[1].y;
            }
            else
            {
                p1x = -points[i - 1].x;
                p1z = points[i - 1].y;
                p2x = px;
                p2z = pz;
            }

            float a = Geometry.Angle2DRad(p1x, p1z, p2x, p2z, 90);

            float offX = Mathf.Cos(a) * width;
            float offZ = Mathf.Sin(a) * width;

            float s1x = px + offX;
            float s1z = pz + offZ;
            float s2x = px - offX;
            float s2z = pz - offZ;

            float s1y = 0;
            float s2y = 0; 

            if (hasElevation)
            {
                s1y = elevationManager.GetElevationValue(s1x, s1z, bestElevationYScale, mapRect);
                s2y = elevationManager.GetElevationValue(s2x, s2z, bestElevationYScale, mapRect);
            }

            s1 = new Vector3(s1x, s1y, s1z);
            s2 = new Vector3(s2x, s2y, s2z);
        }

        private static void DrawIntermediateActicePoints(float width, List<Vector2> points, ref int i, float w2, ref bool extraPointAdded, ref int c, ref Vector3 prevS1, ref Vector3 prevS2)
        {
            Vector3 s1, s2;

            Vector2 p = points[i];
            Vector2 pp = points[i - 1];
            Vector2 np = points[i + 1];
            
            float px = -p.x;
            float pz = p.y;
            
            float p1x = -pp.x;
            float p1z = pp.y;
            float p2x = -np.x;
            float p2z = np.y;

            float a1 = Geometry.Angle2DRad(p1x, p1z, px, pz, 90);
            float a3 = Geometry.AngleOfTriangle(pp, np, p) * Mathf.Rad2Deg;
            if (a3 < 60 && !extraPointAdded)
            {
                points.Insert(i + 1, Vector2.Lerp(p, np, 0.001f));
                points[i] = Vector2.Lerp(p, pp, 0.001f);
                c++;
                i--;
                extraPointAdded = true;
                return;
            }

            extraPointAdded = false;
            float a2 = Geometry.Angle2DRad(px, pz, p2x, p2z, 90);

            float off1x = Mathf.Cos(a1) * width;
            float off1z = Mathf.Sin(a1) * width;
            float off2x = Mathf.Cos(a2) * width;
            float off2z = Mathf.Sin(a2) * width;

            float p21x = px + off1x;
            float p21z = pz + off1z;
            float p22x = px - off1x;
            float p22z = pz - off1z;
            float p31x = px + off2x;
            float p31z = pz + off2z;
            float p32x = px - off2x;
            float p32z = pz - off2z;

            float is1x, is1z, is2x, is2z;
                
            int state1 = Geometry.GetIntersectionPointOfTwoLines(p1x + off1x, p1z + off1z, p21x, p21z, p31x, p31z, p2x + off2x, p2z + off2z, out is1x, out is1z);
            int state2 = Geometry.GetIntersectionPointOfTwoLines(p1x - off1x, p1z - off1z, p22x, p22z, p32x, p32z, p2x - off2x, p2z - off2z, out is2x, out is2z);

            if (state1 == 1 && state2 == 1)
            {
                float o1x = is1x - px;
                float o1z = is1z - pz;
                float o2x = is2x - px;
                float o2z = is2z - pz;

                float m1 = Mathf.Sqrt(o1x * o1x + o1z * o1z);
                float m2 = Mathf.Sqrt(o2x * o2x + o2z * o2z);

                if (m1 > w2)
                {
                    is1x = o1x / m1 * w2 + px;
                    is1z = o1z / m1 * w2 + pz;
                }
                if (m2 > w2)
                {
                    is2x = o2x / m2 * w2 + px;
                    is2z = o2z / m2 * w2 + pz;
                }

                float s1y = 0;
                float s2y = 0;

                if (hasElevation)
                {
                    s1y = elevationManager.GetElevationValue(is1x, is1z, bestElevationYScale, mapRect);
                    s2y = elevationManager.GetElevationValue(is2x, is2z, bestElevationYScale, mapRect);
                }

                s1 = new Vector3(is1x, s1y, is1z);
                s2 = new Vector3(is2x, s2y, is2z);
            }
            else
            {
                float po1x = p31x;
                float po1z = p31z;
                float po2x = p32x;
                float po2z = p32z;

                float s1y = 0;
                float s2y = 0;

                if (hasElevation)
                {
                    s1y = elevationManager.GetElevationValue(po1x, po1z, bestElevationYScale, mapRect);
                    s2y = elevationManager.GetElevationValue(po2x, po2z, bestElevationYScale, mapRect);
                }

                s1 = new Vector3(po1x, s1y, po1z);
                s2 = new Vector3(po2x, s2y, po2z);
            }
                
            AddLineSegment(s1, s2, prevS1, prevS2);

            prevS1 = s1;
            prevS2 = s2;
        }

        /// <summary>
        /// Frees static references used by the dynamic mesh drawer to help with memory management.
        /// Sets all static fields referencing map, map view, control, elevation manager, and mercator points to null.
        /// </summary>
        public static void FreeReferences()
        {
            mapDrawer = null;
            element = null;
            
            map = null;
            mapView = null;
            control = null;
            elevationManager = null;
            mercatorPoints = null;
        }

        /// <summary>
        /// Returns the appropriate DynamicMeshDrawerBase instance for the specified element.
        /// If a drawer for the element's type already exists, it is returned; otherwise, a new instance is created and cached.
        /// </summary>
        /// <param name="element">The drawing element for which to get the dynamic mesh drawer.</param>
        /// <returns>The DynamicMeshDrawerBase instance for the specified element.</returns>
        public static DynamicMeshElementDrawerBase GetDrawer(DrawingElement element)
        {
            DynamicMeshElementDrawerBase drawer;
            if (drawers.TryGetValue(element.GetType(), out drawer)) return drawer;

            Type drawerType = element.dynamicMeshDrawerType;
            drawer = Activator.CreateInstance(drawerType) as DynamicMeshElementDrawerBase;
            drawers[element.GetType()] = drawer;
            return drawer;
        }

        /// <summary>
        /// Gets or creates the ElementData for the current drawing element,
        /// initializing the GameObject, Mesh, and Materials as needed.
        /// Updates material colors and textures based on the provided parameters.
        /// </summary>
        /// <param name="borderColor">The color for the border material.</param>
        /// <param name="backgroundColor">
        /// The color for the background material. If not specified, the default color is used.
        /// </param>
        /// <param name="borderTexture">The texture for the border material (optional).</param>
        /// <param name="backgroundTexture">The texture for the background material (optional).</param>
        /// <returns>The ElementData associated with the current drawing element.</returns>
        protected static ElementData GetElementData(Color borderColor, Color backgroundColor = default, Texture borderTexture = null, Texture backgroundTexture = null)
        {
            ElementData data;
            if (elementData.TryGetValue(element, out data))
            {
                data.materials[0].color = borderColor;
                if (backgroundColor != default) data.materials[1].color = backgroundColor;
                return data;
            }

            GameObject gameObject = new GameObject(element.name);
            gameObject.transform.parent = mapDrawer.drawingsGameObject.transform;
            gameObject.transform.localPosition = new Vector3(0, element.yOffset, 0);
            gameObject.transform.localRotation = Quaternion.Euler(Vector3.zero);
            gameObject.transform.localScale = Vector3.one;
            gameObject.layer = mapDrawer.drawingsGameObject.layer;

            MeshFilter meshFilter = gameObject.AddComponent<MeshFilter>();
            MeshRenderer renderer = gameObject.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            
            Mesh mesh = new Mesh {name = element.name};
            meshFilter.mesh = mesh;
            
            Material[] materials = new Material[element.createBackgroundMaterial?2: 1];
            
            Shader shader = control.drawingShader;
            Material borderMaterial = materials[0] = new Material(shader);
            borderMaterial.shader = shader;
            borderMaterial.color = borderColor;
            borderMaterial.mainTexture = borderTexture;

            if (element.createBackgroundMaterial)
            {
                Material backgroundMaterial = materials[1] = new Material(shader);
                backgroundMaterial.shader = shader;
                if (backgroundColor != default) backgroundMaterial.color = backgroundColor;
                backgroundMaterial.mainTexture = backgroundTexture;
            }

            renderer.materials = materials;
            for (int i = 0; i < materials.Length; i++) materials[i].renderQueue = shader.renderQueue + element.renderQueueOffset;
            
            data = new ElementData
            {
                element = element,
                gameObject = gameObject,
                materials = materials,
                mesh = mesh
            };
            elementData[element] = data;

            if (element.OnInitMesh != null) element.OnInitMesh(element, renderer);

            return data;
        }

        protected static void GenerateLineMesh(float width, bool closed = false, bool optimize = true)
        {
            CalculateLocalPoints(closed, optimize);
            List<Vector2> activePoints = new List<Vector2>(localPoints.Count);

            long maxTiles = mapView.maxTiles;
            float maxSize = maxTiles * Constants.TileSize * sizeInScene.x / control.width / mapView.zoomFactor;
            float halfSize = maxSize / 2;

            float lastPointX = 0;
            float lastPointY = 0;

            float sizeX = sizeInScene.x;
            float sizeY = sizeInScene.y;

            Vector2[] intersections = new Vector2[4];
            bool needExtraPoint = false;
            float extraX = 0, extraY = 0;

            bool isEntireWorld = map.buffer.renderState.width == maxTiles * Constants.TileSize;

            for (int i = 0; i < localPoints.Count; i++)
            {
                Vector2 p = localPoints[i];
                float px = p.x;
                float py = p.y;

                if (needExtraPoint)
                {
                    activePoints.Add(new Vector2(extraX, extraY));

                    float ox = extraX - lastPointX;
                    if (ox > halfSize) lastPointX += maxSize;
                    else if (ox < -halfSize) lastPointX -= maxSize;

                    activePoints.Add(new Vector2(lastPointX, lastPointY));

                    needExtraPoint = false;
                }

                if (i > 0 && element.checkMapBoundaries)
                {
                    int countIntersections = 0;

                    float ox = px - lastPointX;
                    while (Math.Abs(ox) > halfSize)
                    {
                        if (ox < 0)
                        {
                            px += maxSize;
                            ox += maxSize;
                        }
                        else if (ox > 0)
                        {
                            px -= maxSize;
                            ox -= maxSize;
                        }
                    }

                    float crossTopX, crossTopY, crossLeftX, crossLeftY, crossBottomX, crossBottomY, crossRightX, crossRightY;

                    bool hasCrossTop =      Geometry.LineIntersection(lastPointX, lastPointY, px, py, 0,     0,     sizeX, 0,     out crossTopX,    out crossTopY);
                    bool hasCrossBottom =   Geometry.LineIntersection(lastPointX, lastPointY, px, py, 0,     sizeY, sizeX, sizeY, out crossBottomX, out crossBottomY);
                    bool hasCrossLeft =     Geometry.LineIntersection(lastPointX, lastPointY, px, py, 0,     0,     0,     sizeY, out crossLeftX,   out crossLeftY);
                    bool hasCrossRight =    Geometry.LineIntersection(lastPointX, lastPointY, px, py, sizeX, 0,     sizeX, sizeY, out crossRightX,  out crossRightY);

                    if (hasCrossTop)
                    {
                        intersections[0] = new Vector2(crossTopX, crossTopY);
                        countIntersections++;
                    }
                    if (hasCrossBottom)
                    {
                        intersections[countIntersections] = new Vector2(crossBottomX, crossBottomY);
                        countIntersections++;
                    }
                    if (hasCrossLeft)
                    {
                        intersections[countIntersections] = new Vector2(crossLeftX, crossLeftY);
                        countIntersections++;
                    }
                    if (hasCrossRight)
                    {
                        intersections[countIntersections] = new Vector2(crossRightX, crossRightY);
                        countIntersections++;
                    }

                    if (countIntersections == 1) activePoints.Add(intersections[0]);
                    else if (countIntersections == 2)
                    {
                        Vector2 lastPoint = new Vector2(lastPointX, lastPointY);
                        int minIndex = (lastPoint - intersections[0]).sqrMagnitude < (lastPoint - intersections[1]).sqrMagnitude? 0: 1;
                        activePoints.Add(intersections[minIndex]);
                        activePoints.Add(intersections[1 - minIndex]);
                    }

                    if (hasCrossLeft)
                    {
                        needExtraPoint = Geometry.LineIntersection(lastPointX + maxSize, lastPointY, px + maxSize, py, sizeX, 0, sizeX, sizeY, out extraX, out extraY);
                    }
                    else if (hasCrossRight)
                    {
                        needExtraPoint = Geometry.LineIntersection(lastPointX - maxSize, lastPointY, px - maxSize, py, 0, 0, 0, sizeY, out extraX, out extraY);
                    }
                    else if (isEntireWorld)
                    {
                        if (px < 0)
                        {
                            DrawActivePoints(activePoints, width);
                            px += maxSize;

                        }
                        else if (px > sizeX)
                        {
                            DrawActivePoints(activePoints, width);
                            px -= maxSize;
                        }
                    }
                }

                if (!element.checkMapBoundaries || px >= 0 && py >= 0 && px <= sizeX && py <= sizeY) activePoints.Add(new Vector2(px, py));
                else if (activePoints.Count > 0) DrawActivePoints(activePoints, width);

                lastPointX = px;
                lastPointY = py;
            }

            if (needExtraPoint)
            {
                activePoints.Add(new Vector2(extraX, extraY));

                float ox = extraX - lastPointX;
                if (ox > halfSize) lastPointX += maxSize;
                else if (ox < -halfSize) lastPointX -= maxSize;

                activePoints.Add(new Vector2(lastPointX, lastPointY));
            }
            if (activePoints.Count > 0) DrawActivePoints(activePoints, width);
        }

        /// <summary>
        /// Initializes the map drawer and related static references for dynamic mesh drawing.
        /// Sets up references to the map, map view, control, elevation manager, and calculates
        /// the best elevation Y scale and map area rectangle for rendering.
        /// </summary>
        /// <param name="drawer">The map drawer to initialize (must implement IMapDrawer).</param>
        public static void InitMapDrawer(IMapDrawer drawer)
        {
            mapDrawer = drawer as TileSetDrawer;
            map = drawer.map;
            mapView = map.view;
            control = map.control as ControlBaseDynamicMesh;
            elevationManager = control.elevationManager;

            mapRect = mapView.rect.rightFixed;
            
            bestElevationYScale = ElevationManagerBase.GetElevationScale(mapRect, elevationManager);
            hasElevation = control.hasElevation;
            sizeInScene = control.sizeInScene;
        }

        /// <summary>
        /// Prepares the drawer for rendering the specified drawing element.
        /// Clears all mesh and point lists, and sets the current element and its Mercator points.
        /// </summary>
        /// <param name="el">The drawing element to prepare for rendering.</param>
        protected virtual void Prepare(DrawingElement el)
        {
            vertices.Clear();
            normals.Clear();
            triangles.Clear();
            uv.Clear();
            localPoints.Clear();

            element = el;
            mercatorPoints = element.mercatorPoints;
        }

        private static List<Vector2> SplitToPieces(List<Vector2> activePoints)
        {
            List<Vector2> newPoints = new List<Vector2>(activePoints.Count);
            float d = sizeInScene.x / 4;
            Vector2 p1 = activePoints[0];
            newPoints.Add(p1);

            for (int i = 1; i < activePoints.Count; i++)
            {
                Vector2 p2 = activePoints[i];
                if ((p2 - p1).sqrMagnitude < d) newPoints.Add(p2);
                else SplitToPieces(newPoints, p1, p2, d);

                p1 = p2;
            }

            return newPoints;
        }

        private static void SplitToPieces(List<Vector2> points, Vector2 p1, Vector2 p2, float d)
        {
            Vector2 c = (p1 + p2) / 2;
            if ((p1 - c).sqrMagnitude < d) points.Add(c);
            else SplitToPieces(points, p1, c, d);

            if ((c - p2).sqrMagnitude < d) points.Add(p2);
            else SplitToPieces(points, c, p2, d);
        }

        /// <summary>
        /// Tries to get the <see cref="ElementData"/> associated with the specified <see cref="DrawingElement"/>.
        /// Returns the element data if it exists; otherwise, returns null.
        /// </summary>
        /// <param name="element">The drawing element to look up.</param>
        /// <returns>The associated <see cref="ElementData"/>, or null if not found.</returns>
        public static ElementData TryGetElementData(DrawingElement element)
        {
            return elementData.GetValueOrDefault(element);
        }

        protected static void UpdateMaterialsQueue(ElementData data, int index)
        {
            foreach (Material material in data.materials)
            {
                material.renderQueue = control.drawingShader.renderQueue + element.renderQueueOffset + index;
            }
        }

        /// <summary>
        /// Stores data related to a drawing element, including its mesh, GameObject, and materials.
        /// Manages the lifecycle and state of the element's rendering objects.
        /// </summary>
        public class ElementData
        {
            /// <summary>
            /// The associated drawing element.
            /// </summary>
            public DrawingElement element;
        
            /// <summary>
            /// The mesh used for rendering the element.
            /// </summary>
            public Mesh mesh;
        
            /// <summary>
            /// The GameObject representing the element in the scene.
            /// </summary>
            public GameObject gameObject;
        
            /// <summary>
            /// The materials used for rendering the element.
            /// </summary>
            public Material[] materials;
        
            /// <summary>
            /// Gets or sets whether the element's GameObject is active in the scene.
            /// </summary>
            public bool active
            {
                get => gameObject.activeSelf;
                set => gameObject.SetActive(value);
            }
        
            /// <summary>
            /// Destroys the GameObject, mesh, and materials associated with this element, and removes it from the element data dictionary.
            /// </summary>
            public void Destroy()
            {
                if (gameObject)
                {
                    Object.Destroy(gameObject);
                    gameObject = null;
                }
        
                if (mesh)
                {
                    Object.Destroy(mesh);
                    mesh = null;
                }
        
                if (materials != null)
                {
                    foreach (Material material in materials)
                    {
                        if (material) Object.Destroy(material);
                    }
                    materials = null;
                }
        
                elementData.Remove(element);
                element = null;
            }
        
            /// <summary>
            /// Sets the active state of the element's GameObject.
            /// </summary>
            /// <param name="value">True to activate, false to deactivate.</param>
            public void SetActive(bool value)
            {
                gameObject.SetActive(value);
            }
        
            /// <summary>
            /// Sets the name of the GameObject and mesh.
            /// </summary>
            /// <param name="name">The new name.</param>
            public void SetName(string name)
            {
                gameObject.name = name;
                mesh.name = name;
            }
        }
    }
    
    /// <summary>
    /// Generic abstract base class for dynamic mesh drawers for a specific <see cref="DrawingElement"/> type.
    /// Inherit from this class to implement custom mesh drawing logic for elements of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The type of <see cref="DrawingElement"/> this drawer handles.</typeparam>
    public abstract class DynamicMeshDrawerBase<T> : DynamicMeshElementDrawerBase where T : DrawingElement
    {
        protected override bool DrawElement(DrawingElement element, int index)
        {
            ElementData data = TryGetElementData(element);
            if (!Validate(mapDrawer, element))
            {
                data?.SetActive(false);
                return false;
            }
            data?.SetActive(true);
            Prepare(element);
            DrawElement((T)element, index);
            return true;
        }

        /// <summary>
        /// Draws the specified drawing element of type <typeparamref name="T"/> at the given index.
        /// Implement this method in derived classes to handle the actual drawing logic for the element.
        /// </summary>
        /// <param name="element">The drawing element to draw.</param>
        /// <param name="index">The index of the element in the drawing sequence.</param>
        protected abstract void DrawElement(T element, int index);
    }
}