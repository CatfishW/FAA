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
    /// Component that controls the buildings.
    /// </summary>
    [AddComponentMenu("Infinity Code/Online Maps/Plugins/Buildings")]
    [Serializable]
    [Plugin("Buildings", typeof(ControlBaseDynamicMesh))]
    public class Buildings : MonoBehaviour, ISavableAdvanced
    {
        #region Actions

        /// <summary>
        /// The event, which occurs when all buildings have been created.
        /// </summary>
        public Action OnAllBuildingsCreated;

        /// <summary>
        /// The event, which occurs when creating of the building.
        /// </summary>
        public Action<BuildingBase> OnBuildingCreated;

        /// <summary>
        /// The event, which occurs when disposing of the building.
        /// </summary>
        public Action<BuildingBase> OnBuildingDispose;

        /// <summary>
        /// This event allows you to intercept the calculation of the center point of the building, and return your own center point.
        /// </summary>
        public Func<List<Vector3>, Vector3> OnCalculateBuildingCenter;

        /// <summary>
        /// This event is triggered before create a building.
        /// Return TRUE - if you want to create this building, FALSE - avoid creating this building.
        /// </summary>
        public Predicate<BuildingsNodeData> OnCreateBuilding;

        /// <summary>
        /// This event is fired when the height of the building is unknown. It allows you to control the height of buildings. Return - the height of buildings.
        /// </summary>
        public Func<OSMOverpassResult.Way, float> OnGenerateBuildingHeight;

        /// <summary>
        /// The event, which occurs when the new building was received.
        /// </summary>
        public Action OnNewBuildingsReceived;

        /// <summary>
        /// This event is triggered when preparing to create a building, and allows you to make the necessary changes and additions to the way and used nodes.
        /// </summary>
        public Action<OSMOverpassResult.Way, List<OSMOverpassResult.Node>> OnPrepareBuildingCreation;

        /// <summary>
        /// This event is called when creating a request to OSM Overpass API.
        /// </summary>
        public Func<string, GeoPoint, GeoPoint, string> OnPrepareRequest;

        /// <summary>
        /// The event, which occurs after the response has been received.
        /// </summary>
        public Action OnRequestComplete;

        /// <summary>
        /// The event, which occurs when the request is failed.
        /// </summary>
        public Action OnRequestFailed;

        /// <summary>
        /// The event, which occurs when the request for a building sent.
        /// </summary>
        public Action OnRequestSent;

        /// <summary>
        /// This event is triggered before show a building. Return TRUE - if you want to show this building, FALSE - do not show this building.
        /// </summary>
        public Predicate<BuildingBase> OnShowBuilding;

        #endregion

        #region Fields

        #region Static

        private static Buildings _instance;

        /// <summary>
        /// Rate of requests to OSM Overpass API.
        /// </summary>
        public static float requestRate = 0.1f;

        #endregion

        #region Public

        /// <summary>
        /// Range levels of buildings, if the description of the building is not specified.
        /// </summary>
        public LimitedRange levelsRange = new LimitedRange(3, 7, 1, 100);

        /// <summary>
        /// Height of the building level.
        /// </summary>
        public float levelHeight = 4.5f;

        /// <summary>
        /// Need to generate a collider?
        /// </summary>
        public bool generateColliders = true;

        /// <summary>
        /// Scale height of the building.
        /// </summary>
        public float heightScale = 1;

        /// <summary>
        /// Materials of buildings.
        /// </summary>
        public BuildingMaterial[] materials;

        /// <summary>
        /// The maximum number of active buildings (0 - unlimited).
        /// </summary>
        public int maxActiveBuildings;

        /// <summary>
        /// The maximum number of buildings (0 - unlimited).
        /// </summary>
        public int maxBuilding;

        /// <summary>
        /// Minimal height of the building.
        /// </summary>
        public float minHeight = 4.5f;

        /// <summary>
        /// Instance of the request
        /// </summary>
        public OSMOverpassRequest osmRequest;

        /// <summary>
        /// Use the Color tag for buildings?
        /// </summary>
        public bool useColorTag = false;

        /// <summary>
        /// Use the Height tag for buildings?
        /// </summary>
        public bool useHeightTag = true;

        /// <summary>
        /// Range of zoom, in which the building will be created.
        /// </summary>
        public LimitedRange zoomRange = new LimitedRange(19, Constants.MaxZoomExt);

        #endregion

        #region Private

        private float _heightScale;
        private Vector2Int bottomRight;
        private float lastRequestTime = -10000;
        private bool needUpdatePosition;
        private bool needUpdateScale;
        private Queue<BuildingsNodeData> newBuildingsData;
        private string requestData;
        private SavableItem[] savableItems;
        private bool sendBuildingsReceived;
        private Vector2Int topLeft;

        private Dictionary<string, BuildingBase> unusedBuildings;
        private Vector2 lastSizeInScene;

        #endregion

        #endregion

        #region Properties

        /// <summary>
        /// Instance of Buildings
        /// </summary>
        public static Buildings instance => _instance;

        /// <summary>
        /// Returns the active (visible) building.
        /// </summary>
        public IEnumerable<BuildingBase> activeBuildings => buildings.Select(b => b.Value);

        /// <summary>
        /// Dictionary of buildings.
        /// </summary>
        public Dictionary<string, BuildingBase> buildings { get; private set; }

        /// <summary>
        /// Container for buildings.
        /// </summary>
        public GameObject container { get; private set; }

        /// <summary>
        /// Reference to the control.
        /// </summary>
        public ControlBaseDynamicMesh control { get; private set; }

        /// <summary>
        /// Reference to the map.
        /// </summary>
        public Map map { get; private set; }

        /// <summary>
        /// Reference to the map view.
        /// </summary>
        public MapView mapView { get; private set;}

        #endregion

        #region Methods

        /// <summary>
        /// Creates a building.
        /// </summary>
        /// <param name="data">Building data</param>
        public void CreateBuilding(BuildingsNodeData data)
        {
            if (OnCreateBuilding != null && !OnCreateBuilding(data)) return;
            if (buildings.ContainsKey(data.way.id) || unusedBuildings.ContainsKey(data.way.id)) return;

            int initialZoom = map.buffer.renderState.intZoom;

            BuildingBase building = BuildingBuiltIn.Create(this, data.way, data.nodes);
            if (!building) return;

            building.LoadMeta(data.way);
            if (OnBuildingCreated != null) OnBuildingCreated(building);
            unusedBuildings.Add(data.way.id, building);
            if (Math.Abs(map.buffer.lastState.zoom - initialZoom) > float.Epsilon) UpdateBuildingScale(building);
            building.transform.localScale.Scale(new Vector3(1, heightScale, 1));
        }

        private void GenerateBuildings()
        {
            float startTicks = Time.realtimeSinceStartup;
            const float maxTicks = 0.05f;

            lock (newBuildingsData)
            {
                int newBuildingIndex = newBuildingsData.Count;
                int needCreate = newBuildingIndex;

                while (newBuildingIndex > 0)
                {
                    if (maxBuilding > 0 && unusedBuildings.Count + buildings.Count >= maxBuilding) break;

                    newBuildingIndex--;
                    BuildingsNodeData data = newBuildingsData.Dequeue();
                    CreateBuilding(data);
                    needUpdatePosition = true;

                    data.Dispose();

                    if (Time.realtimeSinceStartup - startTicks > maxTicks) break;
                }

                if (needCreate > 0 &&
                    (newBuildingIndex == 0 || (maxBuilding > 0 && unusedBuildings.Count + buildings.Count >= maxBuilding)) &&
                    OnAllBuildingsCreated != null) OnAllBuildingsCreated();
            }

            BuildingBase.roofIndices = null;
        }

        /// <summary>
        /// Gets the savable items for the component.
        /// </summary>
        public SavableItem[] GetSavableItems()
        {
            if (savableItems != null) return savableItems;

            savableItems = new[]
            {
                new SavableItem("buildings", "Buildings", SaveSettings)
                {
                    loadCallback = LoadSettings
                }
            };

            return savableItems;
        }

        /// <summary>
        /// Parses the OSM data and adds buildings to the queue for creation.
        /// </summary>
        /// <param name="osmData">OSM data</param>
        public void LoadBuildingsFromOSM(string osmData)
        {
            Action action = () =>
            {
                OSMOverpassResult result = new OSMOverpassResult(osmData);

                lock (newBuildingsData)
                {
                    MoveRelationsToWays(result);
                }

                sendBuildingsReceived = true;
            };

#if !UNITY_WEBGL
            ThreadManager.AddThreadAction(action);
#else
            action();
#endif
        }

        private void LoadSettings(JSONObject json)
        {
            json.DeserializeObject(this);
        }

        private void MoveRelationToWay(OSMOverpassResult.Relation relation, OSMOverpassResult result, List<string> waysInRelation)
        {
            if (relation.members.Count == 0) return;

            OSMOverpassResult.Way way = new OSMOverpassResult.Way();
            List<string> nodeRefs = new List<string>();

            List<OSMOverpassResult.RelationMember> members = relation.members.Where(m => m.type == "way" && m.role == "outer").ToList();
            if (members.Count == 0) return;

            OSMOverpassResult.Way relationWay;
            if (!result.ways.TryGetValue(members[0].reference, out relationWay) || relationWay == null) return;

            nodeRefs.AddRange(relationWay.nodeRefs);
            members.RemoveAt(0);

            while (members.Count > 0)
            {
                if (!MoveRelationMemberToWay(nodeRefs, members, result.ways)) break;
            }

            waysInRelation.AddRange(relation.members.Select(m => m.reference));
            way.nodeRefs = nodeRefs;
            way.id = relation.id;
            way.tags = relation.tags;
            newBuildingsData.Enqueue(new BuildingsNodeData(way, result.nodes));
        }

        private static bool MoveRelationMemberToWay(List<string> nodeRefs, List<OSMOverpassResult.RelationMember> members, Dictionary<string, OSMOverpassResult.Way> ways)
        {
            string lastRef = nodeRefs[nodeRefs.Count - 1];

            int memberIndex = -1;
            for (int i = 0; i < members.Count; i++)
            {
                OSMOverpassResult.RelationMember member = members[i];
                OSMOverpassResult.Way w = ways[member.reference];
                if (w.nodeRefs[0] == lastRef)
                {
                    nodeRefs.AddRange(w.nodeRefs.Skip(1));
                    memberIndex = i;
                    break;
                }

                if (w.nodeRefs[w.nodeRefs.Count - 1] == lastRef)
                {
                    List<string> refs = w.nodeRefs;
                    refs.Reverse();
                    nodeRefs.AddRange(refs.Skip(1));
                    memberIndex = i;
                    break;
                }
            }

            if (memberIndex != -1) members.RemoveAt(memberIndex);
            else return false;
            return true;
        }

        /// <summary>
        /// Moves buildings from relations to ways.
        /// </summary>
        /// <param name="result">OSM result</param>
        public void MoveRelationsToWays(OSMOverpassResult result)
        {
            List<string> waysInRelation = new List<string>();

            foreach (OSMOverpassResult.Relation relation in result.relations) MoveRelationToWay(relation, result, waysInRelation);

            foreach (string id in waysInRelation)
            {
                OSMOverpassResult.Way way;
                if (!result.ways.TryGetValue(id, out way)) continue;

                way.Dispose();
                result.ways.Remove(id);
            }

            foreach (KeyValuePair<string, OSMOverpassResult.Way> pair in result.ways)
            {
                newBuildingsData.Enqueue(new BuildingsNodeData(pair.Value, result.nodes));
            }
        }

        private void OnBuildingRequestFailed(WebService<string> webService)
        {
            if (OnRequestFailed != null)
            {
                try
                {
                    OnRequestFailed();
                }
                catch
                {
                }
            }

            osmRequest = null;
        }

        private void OnBuildingRequestSuccess(WebService<string> request)
        {
            string response = request.response;
            if (response.Length < 300)
            {
                if (OnRequestFailed != null)
                {
                    try
                    {
                        OnRequestFailed();
                    }
                    catch
                    {
                    }
                }

                return;
            }

            LoadBuildingsFromOSM(response);

            if (OnRequestComplete != null)
            {
                try
                {
                    OnRequestComplete();
                }
                catch
                {
                }
            }

            osmRequest = null;
        }

        private void OnDisable()
        {
            RemoveAllBuildings();
            Utils.Destroy(container);

            if (osmRequest != null)
            {
                osmRequest.OnComplete = null;
                osmRequest = null;
            }

            sendBuildingsReceived = false;
            topLeft = Vector2Int.zero;
            bottomRight = Vector2Int.zero;

            if (map)
            {
                map.OnLocationChanged -= OnMapPositionChanged;
                map.OnZoomChanged -= OnMapZoomChanged;
                map.OnLateUpdateAfter -= OnUpdate;
            }
        }

        private void OnEnable()
        {
            map = GetComponent<Map>();
            mapView = map.view;
            control = map.control as ControlBaseDynamicMesh;

            bool isFirstEnable = !_instance;
            _instance = this;

            buildings = new Dictionary<string, BuildingBase>();
            unusedBuildings = new Dictionary<string, BuildingBase>();
            newBuildingsData = new Queue<BuildingsNodeData>();

            container = new GameObject("Buildings");
            container.transform.parent = transform;
            container.transform.localPosition = Vector3.zero;
            container.transform.localRotation = Quaternion.Euler(Vector3.zero);
            container.transform.localScale = Vector3.one;

            if (!isFirstEnable) Start();
        }

        private void OnMapPositionChanged()
        {
            needUpdatePosition = true;
        }

        private void OnMapZoomChanged()
        {
            needUpdateScale = true;
        }

        private void OnUpdate()
        {
            if (sendBuildingsReceived)
            {
                if (OnNewBuildingsReceived != null) OnNewBuildingsReceived();
                sendBuildingsReceived = false;
            }

            GenerateBuildings();
            UpdateBuildings();
        }

        private void RemoveAllBuildings()
        {
            foreach (KeyValuePair<string, BuildingBase> building in buildings)
            {
                if (OnBuildingDispose != null) OnBuildingDispose(building.Value);
                building.Value.Dispose();
                Utils.Destroy(building.Value.gameObject);
            }

            foreach (KeyValuePair<string, BuildingBase> building in unusedBuildings)
            {
                if (OnBuildingDispose != null) OnBuildingDispose(building.Value);
                building.Value.Dispose();
                Utils.Destroy(building.Value.gameObject);
            }

            buildings.Clear();
            unusedBuildings.Clear();
            newBuildingsData.Clear();
        }

        private void RequestNewBuildings()
        {
            GeoPoint tl = mapView.projection.TileToLocation(topLeft.x, topLeft.y, mapView.intZoom);
            GeoPoint br = mapView.projection.TileToLocation(bottomRight.x, bottomRight.y, mapView.intZoom);

            requestData = string.Format(Culture.numberFormat,
                "(way[{4}]({0},{1},{2},{3});relation[{4}]({0},{1},{2},{3}););out;>;out skel qt;",
                br.y, tl.x, tl.y, br.x, "'building'");
            if (OnPrepareRequest != null) requestData = OnPrepareRequest(requestData, tl, br);
        }

        private JSONItem SaveSettings()
        {
            JSONItem json = JSON.Serialize(new
            {
                zoomRange,
                levelsRange,
                levelHeight,
                minHeight,
                heightScale,
                maxBuilding,
                maxActiveBuildings,
                generateColliders,
                useColorTag,
                materials
            });

            return json;
        }

        private void SendRequest()
        {
            if (osmRequest != null || string.IsNullOrEmpty(requestData)) return;

            osmRequest = new OSMOverpassRequest(requestData);
            osmRequest.OnSuccess += OnBuildingRequestSuccess;
            osmRequest.OnFailed += OnBuildingRequestFailed;
            osmRequest.Send();
            if (OnRequestSent != null) OnRequestSent();
            lastRequestTime = Time.time;
            requestData = null;
        }

        private void Start()
        {
            _heightScale = heightScale;
            lastSizeInScene = control.sizeInScene;

            container.layer = map.gameObject.layer;

            map.OnLocationChanged += OnMapPositionChanged;
            map.OnZoomChanged += OnMapZoomChanged;
            map.OnLateUpdateAfter += OnUpdate;

            UpdateBuildings();
        }

        private void UpdateBuildings()
        {
            if (!zoomRange.Contains(mapView.zoom))
            {
                RemoveAllBuildings();
                return;
            }

            if (Math.Abs(heightScale - _heightScale) > float.Epsilon || lastSizeInScene != control.sizeInScene)
            {
                needUpdateScale = true;
            }

            TileRect r = mapView.GetTileRect();

            Vector2Int newTopLeft = new Vector2Int((int)Math.Round(r.left - 2), (int)Math.Round(r.top - 2));
            Vector2Int newBottomRight = new Vector2Int((int)Math.Round(r.right + 2), (int)Math.Round(r.bottom + 2));

            if (newTopLeft != topLeft || newBottomRight != bottomRight)
            {
                topLeft = newTopLeft;
                bottomRight = newBottomRight;
                RequestNewBuildings();
            }

            if (lastRequestTime + requestRate < Time.time) SendRequest();

            if (needUpdateScale)
            {
                UpdateBuildingsScale();
            }
            else if (needUpdatePosition)
            {
                UpdateBuildingsPosition();
            }

            needUpdatePosition = false;
            needUpdateScale = false;
        }

        private void UpdateBuildingsPosition()
        {
            GeoRect rect = mapView.rect;
            MercatorRect mercatorRect = mapView.mercatorRect;

            List<string> unusedKeys = new List<string>();

            foreach (KeyValuePair<string, BuildingBase> pair in buildings)
            {
                if (!mercatorRect.Intersects(pair.Value.mercatorBounds)) unusedKeys.Add(pair.Key);
                else UpdateBuildingPosition(pair.Value, control, rect);
            }

            List<string> usedKeys = new List<string>();
            List<string> destroyKeys = new List<string>();

            TilePoint t = mapView.centerTile;

            float maxDistance = (Mathf.Pow(mapView.countTilesX >> 1, 2) + Mathf.Pow(mapView.countTilesY >> 1, 2)) * 4;

            foreach (KeyValuePair<string, BuildingBase> pair in unusedBuildings)
            {
                BuildingBase building = pair.Value;
                if (mercatorRect.Intersects(building.mercatorBounds))
                {
                    usedKeys.Add(pair.Key);
                    UpdateBuildingPosition(building, control, rect);
                    building.gameObject.SetActive(true);
                }
                else
                {
                    TilePoint tc = building.centerMercator.ToTile(mapView.intZoom);
                    if ((tc - t).sqrMagnitude > maxDistance) destroyKeys.Add(pair.Key);
                }
            }

            for (int i = 0; i < unusedKeys.Count; i++)
            {
                string key = unusedKeys[i];
                BuildingBase value = buildings[key];
                value.gameObject.SetActive(false);
                unusedBuildings.Add(key, value);
                buildings.Remove(key);
            }

            for (int i = 0; i < usedKeys.Count; i++)
            {
                if (maxActiveBuildings > 0 && buildings.Count >= maxActiveBuildings) break;

                string key = usedKeys[i];
                BuildingBase value = unusedBuildings[key];

                if (OnShowBuilding != null && !OnShowBuilding(value)) continue;
                value.gameObject.SetActive(true);
                buildings.Add(key, value);
                unusedBuildings.Remove(key);
            }

            for (int i = 0; i < destroyKeys.Count; i++)
            {
                string key = destroyKeys[i];
                BuildingBase value = unusedBuildings[key];
                if (OnBuildingDispose != null) OnBuildingDispose(value);
                value.Dispose();
                Utils.Destroy(value.gameObject);
                unusedBuildings.Remove(key);
            }
        }

        private static void UpdateBuildingPosition(BuildingBase building, ControlBaseDynamicMesh control, GeoRect rect)
        {
            Vector3 newPosition = control.LocationToWorld3(building.centerLocation.x, building.centerLocation.y, rect);
            building.transform.position = newPosition;
        }

        private void UpdateBuildingScale(BuildingBase building)
        {
            Vector3 s = control.sizeInScene;
            Vector3 c = new Vector3(s.x / building.initialSizeInScene.x, 1, s.y / building.initialSizeInScene.y);
            c.y = (c.x + c.z) / 2 * instance.heightScale;

            if (Math.Abs(building.initialZoom - mapView.zoom) < float.Epsilon) s = c;
            else if (building.initialZoom < mapView.zoom) s = c * Mathf.Pow(2, mapView.zoom - building.initialZoom);
            else if (building.initialZoom > mapView.zoom) s = c / Mathf.Pow(2, building.initialZoom - mapView.zoom);

            building.transform.localScale = s;
        }

        private void UpdateBuildingsScale()
        {
            lastSizeInScene = control.sizeInScene;
            UpdateBuildingsPosition();
            foreach (KeyValuePair<string, BuildingBase> building in buildings) UpdateBuildingScale(building.Value);
            foreach (KeyValuePair<string, BuildingBase> building in unusedBuildings) UpdateBuildingScale(building.Value);
        }

        #endregion
    }
}