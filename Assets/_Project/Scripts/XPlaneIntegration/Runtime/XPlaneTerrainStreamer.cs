using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AircraftControl.Core;
using FAA.Geo;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace FAA.XPlaneIntegration.Runtime
{
    /// <summary>
    /// Streams the installed simulator's DSF elevation through an SSH-forwarded,
    /// read-only service. No random terrain, implicit sea-level fallback, or
    /// aircraft-relative repositioning. Every vertex uses the ownship geo frame.
    /// </summary>
    [AddComponentMenu("X-Plane Integration/Runtime/Installed Scenery Terrain")]
    public sealed class XPlaneTerrainStreamer : MonoBehaviour
    {
        [Header("Read-only elevation source")]
        [SerializeField] private string serviceUrl = "http://127.0.0.1:12679";
        [SerializeField] private AircraftController aircraft;
        [SerializeField] private XPlane12ApiHudBridge telemetry;
        [SerializeField] private GeoPosUnityPosProjectManager projection;
        [Header("Bounded streaming")]
        [Range(1, 3)] [SerializeField] private int tileRadius = 2;
        [Tooltip("Radius of 0.8-degree distant blocks. 2 gives roughly 4 degrees of coverage; detail increases near ownship.")]
        [Range(1, 3)] [SerializeField] private int distantTileRadius = 2;
        [Tooltip("Far clipping distance in metres. Coarse source terrain must also cover the distant view.")]
        [Range(60000, 200000)] [SerializeField] private float farClipMeters = 150000f;
        [Tooltip("129 samples across 0.1 degrees: approximately the installed 90m DEM resolution.")]
        [SerializeField] private int resolution = 129;
        [SerializeField] private float requestTimeoutSeconds = 12f;
        [SerializeField] private float refreshSeconds = 300f;
        [SerializeField] private bool showSourceStatus = true;
        [SerializeField] private bool hideLegacyTerrain = true;

        private sealed class Tile
        {
            public XPlaneTerrainTile data;
            public GameObject gameObject;
            public Mesh mesh;
            public double fetched;
        }

        private readonly Dictionary<Vector3Int, Tile> _tiles = new Dictionary<Vector3Int, Tile>();
        private readonly Dictionary<Vector3Int, double> _retryAt = new Dictionary<Vector3Int, double>();
        private readonly List<GameObject> _hiddenRoots = new List<GameObject>();
        private Dictionary<Vector3Int, int> _desired = new Dictionary<Vector3Int, int>();
        private readonly Queue<Vector3Int> _rebuildQueue = new Queue<Vector3Int>();
        private Coroutine _stream;
        private UnityWebRequest _request;
        private Material _material;
        private GameObject _root;
        private GameObject _statusCanvas;
        private Text _statusText;
        private Camera _camera;
        private float _originalFarClip;
        private Vector2Int _center;
        private bool _hasCenter;
        private bool _reproject;
        private bool _projectionBound;
        private double _networkRetryAt;
        private int _failures;
        private double _nextUiUpdate;
        private string _lastError = "Waiting for live position";
        private string _endpoint;
        private bool _endpointValid;
        private string _discoveredEndpoint;
        public string ServiceEndpoint => _endpoint ?? string.Empty;

        public static XPlaneTerrainStreamer Active { get; private set; }
        public int LoadedTileCount => _tiles.Count;
        public int DesiredTileCount => _desired.Count;
        public int ReadyTileCount => _desired.Keys.Count(key => _tiles.ContainsKey(key));
        public int VisibleTileCount => _tiles.Values.Count(tile => tile.gameObject.activeSelf);
        public string LastError => _lastError;
        public string SourceDescription => "Installed X-Plane DSF elevation / MSL / synthetic shading";
        public bool HasOwnshipCoverage => _hasCenter && _tiles.Keys.Any(key =>
            XPlaneTerrainLod.Contains(key, new Vector3Int(_center.x, _center.y, 1)));
        public bool UsesSimulatorAltitude => isActiveAndEnabled;

        private void OnEnable()
        {
            if (Active != null && Active != this) { enabled = false; return; }
            Active = this;
            _endpointValid = XPlaneTerrainConnection.Load(serviceUrl, out _endpoint, out string connectionError);
            if (!string.IsNullOrEmpty(_discoveredEndpoint)) _endpointValid = XPlaneTerrainConnection.TryNormalize(_discoveredEndpoint, out _endpoint);
            if (!_endpointValid) _lastError = connectionError;
            _stream = StartCoroutine(Stream());
        }

        private void Resolve()
        {
            if (aircraft == null) aircraft = FindFirstObjectByType<AircraftController>();
            if (telemetry == null) telemetry = FindFirstObjectByType<XPlane12ApiHudBridge>();
            if (projection == null) projection = GeoPosUnityPosProjectManager.Instance;
            if (projection != null && !_projectionBound)
            {
                projection.OnProjectionParametersChanged += ProjectionChanged;
                _projectionBound = true;
            }
        }

        private void ProjectionChanged() => _reproject = true;

        private void LateUpdate()
        {
            if (_reproject && projection != null)
            {
                _reproject = false;
                _rebuildQueue.Clear();
                // Move all anchors in the same frame as the ownship rebase,
                // then amortize the expensive vertex reprojection across frames.
                foreach (var entry in _tiles)
                {
                    PositionTile(entry.Value);
                    _rebuildQueue.Enqueue(entry.Key);
                }
            }
            if (_rebuildQueue.Count > 0 && projection != null && _tiles.TryGetValue(_rebuildQueue.Dequeue(), out Tile rebuild))
                Rebuild(rebuild);
            if (Time.realtimeSinceStartupAsDouble < _nextUiUpdate) return;
            _nextUiUpdate = Time.realtimeSinceStartupAsDouble + .5;
            if (!showSourceStatus) { if (_statusCanvas != null) _statusCanvas.SetActive(false); return; }
            EnsureStatusUi();
            if (_statusCanvas == null) return;
            _statusCanvas.SetActive(true);
            bool live = telemetry != null && telemetry.IsFeedHealthy;
            string state = !_endpointValid ? "CONFIGURATION ERROR" : !live ? "POSITION STALE" :
                !HasOwnshipCoverage && _failures > 0 ? "CONNECTION REQUIRED" : !HasOwnshipCoverage ? "NO COVERAGE" :
                _lastError.Length > 0 ? "PARTIAL / RETRY" : ReadyTileCount < _desired.Count ? "LOADING" : "READY";
            _statusText.text = $"TERRAIN · {state} · {ReadyTileCount}/{_desired.Count}\n" +
                (!_endpointValid ? "CHECK TerrainConnection.json / --terrain-url" :
                 !HasOwnshipCoverage && _failures > 0 ? "START TERRAIN TUNNEL · DATA CONNECTION IS SEPARATE" : "X-PLANE ELEVATION · NEAR / DISTANT LOD");
            _statusText.color = live && HasOwnshipCoverage && _lastError.Length == 0
                ? new Color(.63f, .83f, .78f) : new Color(1f, .73f, .32f);
        }

        private IEnumerator Stream()
        {
            while (enabled)
            {
                Resolve();
                if (!_endpointValid) { yield return new WaitForSecondsRealtime(1f); continue; }
                if (hideLegacyTerrain) HideLegacyTerrain();
                if (projection == null || aircraft?.State == null || telemetry == null || !telemetry.IsFeedHealthy)
                {
                    _lastError = "Waiting for live position";
                    yield return new WaitForSecondsRealtime(1f);
                    continue;
                }
                double latitude = aircraft.State.Latitude, longitude = aircraft.State.Longitude;
                if (!XPlaneTerrainTile.Finite(latitude) || !XPlaneTerrainTile.Finite(longitude) || Math.Abs(latitude) >= 85 || Math.Abs(longitude) > 180)
                {
                    _lastError = "Position outside supported terrain coverage";
                    yield return new WaitForSecondsRealtime(1f);
                    continue;
                }
                Vector2Int center = XPlaneTerrainTile.Key(latitude, longitude);
                if (!_hasCenter || _center != center) SetCenter(center);
                double now = Time.realtimeSinceStartupAsDouble;
                if (now < _networkRetryAt) { yield return new WaitForSecondsRealtime(.5f); continue; }
                Vector3Int? next = NextTile(now);
                if (!next.HasValue) { yield return new WaitForSecondsRealtime(.5f); continue; }
                yield return Fetch(next.Value);
                yield return null; // Mesh uploads are never multiplied within a single frame.
            }
        }

        private void SetCenter(Vector2Int center)
        {
            _center = center;
            _hasCenter = true;
            _desired = XPlaneTerrainLod.Select(center, tileRadius, distantTileRadius);
            ReconcileLod();
            foreach (var key in _retryAt.Keys.Where(k => !_desired.ContainsKey(k)).ToArray()) _retryAt.Remove(key);
        }

        private Vector3Int? NextTile(double now)
        {
            Vector3Int? result = null;
            double best = double.MaxValue;
            foreach (Vector3Int key in _desired.Keys)
            {
                if (_retryAt.TryGetValue(key, out double retry) && now < retry) continue;
                bool exists = _tiles.TryGetValue(key, out Tile tile);
                if (exists && now - tile.fetched < Mathf.Max(30f, refreshSeconds)) continue;
                double distance = XPlaneTerrainLod.Distance(_center, key);
                if (exists) distance += 1000; // Fill gaps before refreshing valid tiles.
                if (distance < best) { result = key; best = distance; }
            }
            return result;
        }

        private IEnumerator Fetch(Vector3Int key)
        {
            int requestedResolution = key.z == 1 ? (resolution == 33 || resolution == 65 ? resolution : 129) : _desired[key];
            string url = _endpoint + $"/v1/terrain/tile?lat_index={key.x}&lon_index={key.y}&resolution={requestedResolution}&span={key.z}";
            using (var request = UnityWebRequest.Get(url))
            {
                _request = request;
                request.timeout = Mathf.Clamp(Mathf.CeilToInt(requestTimeoutSeconds), 2, 30);
                yield return request.SendWebRequest();
                _request = null;
                double now = Time.realtimeSinceStartupAsDouble;
                if (request.result != UnityWebRequest.Result.Success)
                {
                    _lastError = request.responseCode == 422 ? "Installed scenery unavailable or unsupported" : "Terrain service unavailable";
                    _retryAt[key] = now + 30;
                    if (request.responseCode != 422)
                    {
                        _failures++;
                        _networkRetryAt = now + Math.Min(30, Math.Pow(2, Math.Min(5, _failures)));
                    }
                    yield break;
                }
                // Bounded payload, known schema, units, ordering and geographic identity.
                if (request.downloadedBytes > 2 * 1024 * 1024)
                {
                    _lastError = "Terrain response exceeds size limit";
                    _retryAt[key] = now + 30;
                    yield break;
                }
                XPlaneTerrainTile data = null;
                try { data = JsonUtility.FromJson<XPlaneTerrainTile>(request.downloadHandler.text); }
                catch (ArgumentException) { }
                if (data == null || data.Span != key.z || !data.Validate(key.x, key.y, requestedResolution, out _))
                {
                    _lastError = "Terrain response failed validation";
                    _retryAt[key] = now + 30;
                    yield break;
                }
                // Re-evaluate position after the async request: never flash a tile from before a teleport.
                if (aircraft?.State == null || XPlaneTerrainTile.Key(aircraft.State.Latitude, aircraft.State.Longitude) != _center)
                    yield break;
                EnsureRoot();
                if (_material == null) { _lastError = "Terrain shader unavailable"; _retryAt[key] = now + 30; yield break; }
                if (!_tiles.TryGetValue(key, out Tile tile))
                {
                    tile = new Tile { gameObject = new GameObject($"DSF {key.x}/{key.y} LOD {key.z}", typeof(MeshFilter), typeof(MeshRenderer)) };
                    tile.gameObject.transform.SetParent(_root.transform, false);
                    var renderer = tile.gameObject.GetComponent<MeshRenderer>();
                    renderer.sharedMaterial = _material;
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                    _tiles[key] = tile;
                }
                tile.data = data;
                tile.fetched = now;
                Rebuild(tile);
                ReconcileLod();
                _retryAt.Remove(key);
                _failures = 0;
                _networkRetryAt = 0;
                _lastError = _retryAt.Count == 0 ? string.Empty : "Some tiles unavailable; retrying";
            }
        }

        private void EnsureRoot()
        {
            if (_root == null) _root = new GameObject("X-Plane Generated Elevation (MSL)");
            if (_material == null)
            {
                Shader shader = Resources.Load<Shader>("Shaders/XPlaneTerrain");
                if (shader != null)
                {
                    _material = new Material(shader) { name = "X-Plane Synthetic Terrain" };
                    _material.SetFloat("_HazeDistance", Mathf.Clamp(farClipMeters, 60000, 200000) * .9f);
                }
            }
            if (_camera == null)
            {
                _camera = Camera.main;
                if (_camera != null)
                {
                    _originalFarClip = _camera.farClipPlane;
                    _camera.farClipPlane = Mathf.Max(_originalFarClip, Mathf.Clamp(farClipMeters, 60000, 200000));
                }
            }
        }

        private void Rebuild(Tile tile)
        {
            Mesh replacement = tile.data.BuildRenderMesh(projection, Mathf.Min(1500f, tile.data.Span * 200f));
            PositionTile(tile);
            tile.gameObject.GetComponent<MeshFilter>().sharedMesh = replacement;
            if (tile.mesh != null) Destroy(tile.mesh);
            tile.mesh = replacement;
        }

        private void PositionTile(Tile tile) => tile.gameObject.transform.position =
            projection.GeoToUnityPosition(tile.data.south, tile.data.west, 0f);

        private void ReconcileLod()
        {
            foreach (var key in _tiles.Keys.ToArray())
                if (XPlaneTerrainLod.CanRetire(key, _desired.Keys, _tiles.Keys)) RemoveTile(key);
            foreach (var entry in _tiles)
            {
                bool coveredByParent = _tiles.Keys.Any(other => other.z > entry.Key.z && XPlaneTerrainLod.Contains(other, entry.Key));
                entry.Value.gameObject.SetActive(!coveredByParent);
            }
        }

        private void HideLegacyTerrain()
        {
            // The old anchor can relocate Iowa terrain beneath an aircraft anywhere
            // in the world. Suppress that entire authored root, including its underlay.
            foreach (var anchor in FindObjectsByType<XPlaneMappedTerrainAnchor>(FindObjectsSortMode.None))
            {
                GameObject root = anchor.gameObject;
                if (root == gameObject || transform.IsChildOf(root.transform)) continue;
                if (root.activeSelf) { _hiddenRoots.Add(root); root.SetActive(false); }
            }
        }

        private void EnsureStatusUi()
        {
            if (_statusCanvas != null) return;
            _statusCanvas = new GameObject("X-Plane Terrain Source Status", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
            var canvas = _statusCanvas.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 2000;
            var scaler = _statusCanvas.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = .5f;
            var label = new GameObject("Source (non-interactive)", typeof(RectTransform), typeof(Text));
            label.transform.SetParent(_statusCanvas.transform, false);
            var rect = (RectTransform)label.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(22, -90);
            rect.sizeDelta = new Vector2(370, 36);
            _statusText = label.GetComponent<Text>();
            _statusText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _statusText.fontSize = 11;
            _statusText.raycastTarget = false;
            _statusText.supportRichText = false;
        }

        private void RemoveTile(Vector3Int key)
        {
            Tile tile = _tiles[key];
            if (tile.mesh != null) Destroy(tile.mesh);
            if (tile.gameObject != null) Destroy(tile.gameObject);
            _tiles.Remove(key);
        }

        [ContextMenu("Refresh Installed Terrain")]
        public void RefreshTerrain()
        {
            _endpointValid = XPlaneTerrainConnection.Load(serviceUrl, out _endpoint, out string connectionError);
            if (!string.IsNullOrEmpty(_discoveredEndpoint)) _endpointValid = XPlaneTerrainConnection.TryNormalize(_discoveredEndpoint, out _endpoint);
            if (!_endpointValid) _lastError = connectionError;
            foreach (var tile in _tiles.Values) tile.fetched = double.NegativeInfinity;
            _retryAt.Clear();
            _networkRetryAt = 0;
            _failures = 0;
        }

        public void UseDiscoveredSource(string endpoint)
        {
            if (!string.IsNullOrEmpty(endpoint) && !XPlaneTerrainConnection.TryNormalize(endpoint, out _)) return;
            if (_discoveredEndpoint == endpoint) return;
            bool wasEnabled = enabled;
            if (wasEnabled) enabled = false;
            _discoveredEndpoint = endpoint;
            if (wasEnabled) enabled = true;
        }

        private void OnDisable()
        {
            _request?.Abort();
            if (_stream != null) StopCoroutine(_stream);
            _request?.Dispose();
            _stream = null;
            _request = null;
            if (_projectionBound && projection != null) projection.OnProjectionParametersChanged -= ProjectionChanged;
            _projectionBound = false;
            foreach (var key in _tiles.Keys.ToArray()) RemoveTile(key);
            foreach (var root in _hiddenRoots) if (root != null) root.SetActive(true);
            _hiddenRoots.Clear();
            if (_camera != null) _camera.farClipPlane = _originalFarClip;
            _camera = null;
            if (_material != null) Destroy(_material);
            if (_root != null) Destroy(_root);
            if (_statusCanvas != null) Destroy(_statusCanvas);
            _material = null;
            _root = null;
            _statusCanvas = null;
            _statusText = null;
            _hasCenter = false;
            _desired.Clear();
            _rebuildQueue.Clear();
            _retryAt.Clear();
            _networkRetryAt = 0;
            _failures = 0;
            if (Active == this) Active = null;
        }
    }
}
