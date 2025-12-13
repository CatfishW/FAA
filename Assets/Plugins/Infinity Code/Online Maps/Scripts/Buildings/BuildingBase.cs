/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using System.Collections.Generic;
using System.Linq;
using OnlineMaps.Webservices;
using UnityEngine;

namespace OnlineMaps
{
    /// <summary>
    /// Base class of buildings.
    /// </summary>
    public abstract class BuildingBase : MonoBehaviour, IDataContainer
    {
        /// <summary>
        /// Indices of the roof vertices.
        /// </summary>
        public static List<int> roofIndices;

        /// <summary>
        /// Default shader for the building.
        /// </summary>
        protected static Shader defaultShader;

        /// <summary>
        /// Default material for the building walls.
        /// </summary>
        protected static Material defaultWallMaterial;

        /// <summary>
        /// Default material for the building roof.
        /// </summary>
        protected static Material defaultRoofMaterial;

        /// <summary>
        /// List of vertices for the building.
        /// </summary>
        protected static List<Vector3> vertices;

        /// <summary>
        /// List of UV coordinates for the building.
        /// </summary>
        protected static List<Vector2> uvs;

        /// <summary>
        /// List of wall triangles for the building.
        /// </summary>
        protected static List<int> wallTriangles;

        /// <summary>
        /// List of roof triangles for the building.
        /// </summary>
        protected static List<int> roofTriangles;

        /// <summary>
        /// List of roof vertices for the building.
        /// </summary>
        protected static List<Vector3> roofVertices;

        /// <summary>
        /// Events that occur when user click on the building.
        /// </summary>
        public Action<BuildingBase> OnClick;

        /// <summary>
        /// Events that occur when dispose building.
        /// </summary>
        public Action<BuildingBase> OnDispose;

        /// <summary>
        /// Events that occur when user press on the building.
        /// </summary>
        public Action<BuildingBase> OnPress;

        /// <summary>
        /// Events that occur when user release on the building.
        /// </summary>
        public Action<BuildingBase> OnRelease;

        /// <summary>
        /// Geographical coordinates of the center point.
        /// </summary>
        public GeoPoint centerLocation;

        /// <summary>
        /// Mercator coordinates of the center point.
        /// </summary>
        public MercatorPoint centerMercator;

        /// <summary>
        /// Geographical coordinates of the boundaries of the building.
        /// </summary>
        public GeoRect geoBounds;

        /// <summary>
        /// Mercator coordinates of the boundaries of the building.
        /// </summary>
        public MercatorRect mercatorBounds;

        /// <summary>
        /// ID of building.
        /// </summary>
        public string id;

        /// <summary>
        /// Initial size of building in scene.
        /// </summary>
        public Vector2 initialSizeInScene;

        /// <summary>
        /// Zoom, in which this building was created.
        /// </summary>
        public float initialZoom;

        /// <summary>
        /// Array of building meta key-value pair.
        /// </summary>
        public MetaInfo[] metaInfo;

        /// <summary>
        /// Perimeter of building.
        /// </summary>
        public float perimeter;

        /// <summary>
        /// Building way.
        /// </summary>
        public OSMOverpassResult.Way way;

        /// <summary>
        /// Building nodes.
        /// </summary>
        public List<OSMOverpassResult.Node> nodes;


        /// <summary>
        /// Collider of building.
        /// </summary>
        protected Collider buildingCollider;

        /// <summary>
        /// Reference to the Buildings container.
        /// </summary>
        protected Buildings container;

        /// <summary>
        /// Indicates that the building has an error.
        /// </summary>
        protected bool hasErrors = false;

        private bool isPressed;
        private int lastTouchCount;
        private Vector2 pressPoint;
        
        /// <summary>
        /// Gets custom data dictionary
        /// </summary>
        public Dictionary<string, object> customData { get; private set; } = new Dictionary<string, object>();
        
        /// <summary>
        /// Gets / sets custom data value by key
        /// </summary>
        /// <param name="key">Custom data key</param>
        /// <returns>Custom data value</returns>
        public object this[string key]
        {
            get => customData.GetValueOrDefault(key);
            set => customData[key] = value;
        }

        /// <summary>
        /// Checks ignore the building.
        /// </summary>
        /// <param name="way">Building way.</param>
        /// <returns>TRUE - ignore building, FALSE - generate building.</returns>
        protected static bool CheckIgnoredBuildings(OSMOverpassResult.Way way)
        {
            string buildingType = way.GetTagValue("building");
            if (buildingType == "bridge" || buildingType == "roof") return true;

            string layer = way.GetTagValue("layer");
            if (!string.IsNullOrEmpty(layer) && int.Parse(layer) < 0) return true;

            return false;
        }

        /// <summary>
        /// Creates a new child GameObject, with the specified name.
        /// </summary>
        /// <param name="container">Reference to Buildings</param>
        /// <param name="id">Name of GameObject.</param>
        /// <returns></returns>
        protected static GameObject CreateGameObject(Buildings container, string id)
        {
            GameObject buildingGameObject = new GameObject(id);
            buildingGameObject.SetActive(false);

            buildingGameObject.transform.parent = container.container.transform;
            buildingGameObject.layer = container.gameObject.layer;
            return buildingGameObject;
        }

        /// <summary>
        /// Dispose of building.
        /// </summary>
        public virtual void Dispose()
        {
            if (OnDispose != null) OnDispose(this);

            customData = null;

            OnClick = null;
            OnDispose = null;
            OnPress = null;
            OnRelease = null;

            buildingCollider = null;
            container = null;
            metaInfo = null;
        }

        /// <summary>
        /// Gets the data by key.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <typeparam name="T">The type of the data.</typeparam>
        /// <returns>The data.</returns>
        public T GetData<T>(string key)
        {
            object val = customData.GetValueOrDefault(key);
            return val != null ? (T)val : default;
        }

        /// <summary>
        /// Parses a height value from a string and converts it to meters.
        /// </summary>
        /// <param name="str">The string containing the height value.</param>
        /// <param name="height">The parsed height value in meters.</param>
        /// <returns>True if the height was successfully parsed; otherwise, false.</returns>
        protected static bool GetHeightFromString(string str, ref float height)
        {
            if (string.IsNullOrEmpty(str)) return false;

            int l = str.Length;
            if (l > 2 && str[l - 2] == 'c' && str[l - 1] == 'm')
            {
                if (TryGetFloat(str, 0, l - 2, out height))
                {
                    height /= 10;
                    return true;
                }

                return false;
            }

            if (l > 1 && str[l - 1] == 'm') return TryGetFloat(str, 0, l - 1, out height);
            return TryGetFloat(str, 0, l, out height);
        }

        /// <summary>
        /// Converts a list of nodes into a list of points in Unity World Space
        /// </summary>
        /// <param name="nodes">List of nodes</param>
        /// <param name="container">Reference to Buildings</param>
        /// <returns>List of points in Unity World Space</returns>
        protected static List<Vector3> GetLocalPoints(List<OSMOverpassResult.Node> nodes, Buildings container)
        {
            Map map = container.map;
            MapView mapView = container.mapView;
            ControlBaseDynamicMesh control = container.control;
            Projection projection = mapView.projection;
            MercatorPoint tl = mapView.topLeft.ToMercator(map);
            
            double res = (long)mapView.maxTiles * Constants.TileSize / mapView.zoomFactor;
            
            double sw = res * control.sizeInScene.x / control.width;
            double sh = res * control.sizeInScene.y / control.height;

            List<Vector3> localPoints = nodes.Select(n => n.ToMercator(projection))
                .Select(p => new Vector3(
                    (float)((tl.x - p.x) * sw), 
                    0, 
                    (float)((p.y - tl.y) * sh)))
                .ToList();

            if (localPoints.Count < 3) return localPoints;

            if (localPoints.First() == localPoints.Last())
            {
                localPoints.RemoveAt(localPoints.Count - 1);
                if (localPoints.Count < 3) return localPoints;
            }

            for (int i = 0; i < localPoints.Count; i++)
            {
                int prev = i - 1;
                if (prev < 0) prev = localPoints.Count - 1;

                int next = i + 1;
                if (next >= localPoints.Count) next = 0;

                float a1 = Geometry.Angle2D(localPoints[prev], localPoints[i]);
                float a2 = Geometry.Angle2D(localPoints[i], localPoints[next]);

                if (Mathf.Abs(a1 - a2) >= 5) continue;

                localPoints.RemoveAt(i);
                i--;
            }

            return localPoints;
        }

        private bool HitTest()
        {
            if (!buildingCollider) return false;

            ControlBaseDynamicMesh control = container.control;
            Ray ray = control.currentCamera.ScreenPointToRay(InputManager.mousePosition);
            return buildingCollider.Raycast(ray, out RaycastHit _, Constants.MaxRaycastDistance);
        }

        /// <summary>
        /// Loads the metadata from the XML.
        /// </summary>
        /// <param name="item">Object that contains meta description.</param>
        public void LoadMeta(OSMOverpassResult.OSMBase item)
        {
            metaInfo = new MetaInfo[item.tags.Count];
            for (int i = 0; i < item.tags.Count; i++)
            {
                OSMOverpassResult.Tag tag = item.tags[i];
                metaInfo[i] = new MetaInfo()
                {
                    info = tag.value,
                    title = tag.key
                };
            }
        }

        /// <summary>
        /// This method is called when you press a building.
        /// </summary>
        protected void OnBasePress()
        {
            isPressed = true;
            if (OnPress != null) OnPress(this);
            pressPoint = InputManager.mousePosition;
        }

        /// <summary>
        /// This method is called when you release a building.
        /// </summary>
        protected void OnBaseRelease()
        {
            isPressed = false;
            if (OnRelease != null) OnRelease(this);
            if ((pressPoint - InputManager.mousePosition).magnitude < 10)
            {
                if (OnClick != null) OnClick(this);
            }
        }

        private static bool TryGetFloat(string s, int index, int count, out float result)
        {
            result = 0;
            long n = 0;
            bool hasDecimalPoint = false;
            bool neg = false;
            long decimalV = 1;
            for (int x = 0; x < count; x++, index++)
            {
                char c = s[index];
                if (c == '.') hasDecimalPoint = true;
                else if (c == '-') neg = true;
                else if (c < '0' || c > '9') return false;
                else
                {
                    n *= 10;
                    n += c - '0';
                    if (hasDecimalPoint) decimalV *= 10;
                }
            }

            if (neg) n = -n;

            result = n / (float)decimalV;

            return true;
        }

        private void Update()
        {
            int touchCount = InputManager.touchCount;
            if (touchCount == lastTouchCount) return;

            if (touchCount == 1)
            {
                if (HitTest()) OnBasePress();
            }
            else if (touchCount == 0)
            {
                if (isPressed && HitTest()) OnBaseRelease();
                isPressed = false;
            }

            lastTouchCount = touchCount;
        }

        /// <summary>
        /// Type the building's roof.
        /// </summary>
        protected enum RoofType
        {
            /// <summary>
            /// Dome roof.
            /// </summary>
            dome,

            /// <summary>
            /// Flat roof.
            /// </summary>
            flat
        }

        /// <summary>
        /// Building meta key-value pair.
        /// </summary>
        public struct MetaInfo
        {
            /// <summary>
            /// Meta value.
            /// </summary>
            public string info;

            /// <summary>
            /// Meta key.
            /// </summary>
            public string title;
        }
    }
}