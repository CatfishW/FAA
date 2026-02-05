using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;
using System.Collections.Generic;

namespace WeatherVisualization3D
{
    /// <summary>
    /// Real-time 2D radar data provider using RainViewer API.
    /// Fetches global precipitation radar tiles and converts them to 3D volumetric weather data.
    /// Free tier: Unlimited (no API key required for basic usage)
    /// Documentation: https://www.rainviewer.com/api.html
    /// </summary>
    [AddComponentMenu("Weather/3D/RainViewer 3D Provider")]
    public class RainViewer3DProvider : MonoBehaviour, IWeatherDataSource, IRadarTextureSource
    {
        #region Configuration

        [Header("RainViewer API Settings")]
        [Tooltip("Tile size for radar imagery (256 or 512)")]
        [SerializeField] private int tileSize = 512;

        [Tooltip("Zoom level for tile requests (4-10). Higher = more detail, smaller area")]
        [SerializeField] [Range(4, 10)] private int zoomLevel = 6;

        [Tooltip("Tile grid radius (1=3x3 grid, 2=5x5 grid, 3=7x7 grid)")]
        [SerializeField] [Range(1, 3)] private int tileRadius = 2;

        [Tooltip("Color scheme for radar display (1-8)")]
        [SerializeField] [Range(1, 8)] private int colorScheme = 2;

        [Tooltip("Smoothing of radar data (0=none, 1=smooth)")]
        [SerializeField] [Range(0, 1)] private int smoothing = 1;

        [Tooltip("Snow display option (0=hide snow, 1=show snow)")]
        [SerializeField] [Range(0, 1)] private int snow = 1;

        [Header("Update Settings")]
        [Tooltip("How often to check for new radar data (seconds)")]
        [SerializeField] private float updateCheckInterval = 120f;

        [Tooltip("Request timeout in seconds")]
        [SerializeField] private float requestTimeout = 15f;

        [Header("Coverage Settings")]
        [Tooltip("Coverage range in nautical miles")]
        [SerializeField] private float coverageRangeNM = 160f;

        [Tooltip("Maximum altitude for weather visualization (feet)")]
        [SerializeField] private float maxAltitudeFt = 55000f;

        [Header("3D Volume Settings")]
        [Tooltip("Configuration for volumetric weather rendering")]
        [SerializeField] private WeatherVolumeConfig volumeConfig;

        [Tooltip("Enable automatic 2D to 3D conversion")]
        [SerializeField] private bool autoConvertTo3D = true;

        [Header("Debug")]
        [SerializeField] private bool debugMode = false;

        #endregion

        #region Private Fields

        // Current state
        private float _latitude = 39.7392f;  // Default: Denver
        private float _longitude = -104.9903f;
        private float _altitudeFt = 5000f;
        private float _headingDegrees = 0f;

        // Data
        private WeatherVolumeData _currentData;
        private Texture2D _radarTexture;
        private WeatherDataMapper _dataMapper;

        // API state
        private string _lastRadarPath = "";
        private long _lastRadarTimestamp = 0;
        private bool _isRequesting = false;
        private bool _isInitialized = false;
        private DataSourceStatus _status = DataSourceStatus.Uninitialized;

        // Caching
        private Dictionary<string, Texture2D> _tileCache = new Dictionary<string, Texture2D>();
        private float _lastUpdateCheckTime;
        private Texture2D _cachedComposite;
        private float _cachedCenterLat;
        private float _cachedCenterLon;

        // Coroutine tracking
        private Coroutine _updateRoutine;

        #endregion

        #region IWeatherDataSource Implementation

        public string SourceName => "RainViewer 3D Radar";

        public DataSourceStatus Status
        {
            get => _status;
            private set
            {
                if (_status != value)
                {
                    _status = value;
                    OnStatusChanged?.Invoke(_status);
                    if (debugMode)
                        Debug.Log($"[RainViewer3D] Status changed to: {_status}");
                }
            }
        }

        public bool IsDataValid => Status == DataSourceStatus.Active && _currentData != null;

        public WeatherVolumeData CurrentData => _currentData;

        public event Action<WeatherVolumeData> OnDataUpdated;
        public event Action<DataSourceStatus> OnStatusChanged;

        #endregion

        #region IRadarTextureSource Implementation

        public Texture2D RadarTexture => _radarTexture;
        public event Action<Texture2D> OnRadarTextureUpdated;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (volumeConfig == null)
            {
                volumeConfig = WeatherVolumeConfig.CreateDefault();
                if (debugMode)
                    Debug.Log("[RainViewer3D] Created default volume config");
            }

            _dataMapper = new WeatherDataMapper(volumeConfig);
        }

        private void OnDestroy()
        {
            StopUpdates();
            ClearCache();

            if (_currentData != null)
            {
                _currentData.Dispose();
                _currentData = null;
            }

            if (_radarTexture != null)
            {
                Destroy(_radarTexture);
                _radarTexture = null;
            }
        }

        #endregion

        #region IWeatherDataSource Methods

        public void Initialize()
        {
            if (_isInitialized) return;

            Status = DataSourceStatus.Initializing;

            // Initialize volume data
            Vector3Int resolution = volumeConfig != null ? volumeConfig.volumeResolution : new Vector3Int(64, 32, 64);
            _currentData = new WeatherVolumeData(resolution);
            _currentData.CoverageNM = coverageRangeNM;
            _currentData.MaxAltitudeFt = maxAltitudeFt;
            _currentData.DataSource = SourceName;

            // Initialize radar texture
            int textureSize = tileSize * (tileRadius * 2 + 1);
            _radarTexture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
            _radarTexture.filterMode = FilterMode.Bilinear;
            _radarTexture.wrapMode = TextureWrapMode.Clamp;

            _isInitialized = true;
            Status = DataSourceStatus.Active;

            if (debugMode)
                Debug.Log($"[RainViewer3D] Initialized with resolution {resolution}, texture size {textureSize}x{textureSize}");
        }

        public void StartUpdates()
        {
            if (!_isInitialized)
                Initialize();

            StopUpdates(); // Ensure no duplicates
            _updateRoutine = StartCoroutine(UpdateRoutine());

            if (debugMode)
                Debug.Log("[RainViewer3D] Started update routine");
        }

        public void StopUpdates()
        {
            if (_updateRoutine != null)
            {
                StopCoroutine(_updateRoutine);
                _updateRoutine = null;
            }

            if (_isRequesting)
            {
                StopAllCoroutines();
                _isRequesting = false;
            }

            if (Status == DataSourceStatus.Active)
                Status = DataSourceStatus.Paused;
        }

        public void ForceRefresh()
        {
            if (!_isInitialized)
                Initialize();

            if (!_isRequesting)
                StartCoroutine(FetchAndProcessData());
        }

        public void SetPosition(float latitude, float longitude, float altitudeFt)
        {
            bool positionChanged =
                Mathf.Abs(_latitude - latitude) > 0.001f ||
                Mathf.Abs(_longitude - longitude) > 0.001f;

            _latitude = latitude;
            _longitude = longitude;
            _altitudeFt = altitudeFt;

            if (_currentData != null)
            {
                _currentData.CenterPosition = new Vector3(_longitude, 0, _latitude);
            }

            // Check if we moved outside cached coverage
            if (positionChanged && _cachedComposite != null)
            {
                float distFromCenter = CalculateDistance(_latitude, _longitude, _cachedCenterLat, _cachedCenterLon);
                float coverageKm = coverageRangeNM * 1.852f;

                if (distFromCenter > coverageKm * 0.5f) // 50% threshold
                {
                    if (debugMode)
                        Debug.Log("[RainViewer3D] Position moved outside cached coverage, fetching new data");
                    ForceRefresh();
                }
            }
        }

        public void SetRange(float rangeNM)
        {
            coverageRangeNM = rangeNM;

            if (_currentData != null)
                _currentData.CoverageNM = rangeNM;

            // Adjust zoom level based on range
            int newZoom = CalculateZoomLevel(rangeNM);
            if (newZoom != zoomLevel)
            {
                zoomLevel = newZoom;
                if (debugMode)
                    Debug.Log($"[RainViewer3D] Range changed to {rangeNM}nm, adjusting zoom to {zoomLevel}");
                ForceRefresh();
            }
        }

        public void SetHeading(float headingDegrees)
        {
            _headingDegrees = headingDegrees;

            if (_currentData != null)
                _currentData.Heading = headingDegrees;
        }

        #endregion

        #region Update Routine

        private IEnumerator UpdateRoutine()
        {
            // Initial fetch
            yield return FetchAndProcessData();

            while (true)
            {
                // Check for new data periodically
                if (Time.time - _lastUpdateCheckTime > updateCheckInterval)
                {
                    _lastUpdateCheckTime = Time.time;
                    yield return CheckForNewData();
                }

                yield return new WaitForSeconds(5f);
            }
        }

        private IEnumerator CheckForNewData()
        {
            string apiUrl = "https://api.rainviewer.com/public/weather-maps.json";

            using (var request = UnityWebRequest.Get(apiUrl))
            {
                request.timeout = Mathf.RoundToInt(requestTimeout);
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    long newTimestamp = ParseTimestamp(request.downloadHandler.text);

                    if (newTimestamp > _lastRadarTimestamp)
                    {
                        if (debugMode)
                            Debug.Log($"[RainViewer3D] New data available (timestamp: {newTimestamp})");
                        yield return FetchAndProcessData();
                    }
                }
            }
        }

        private IEnumerator FetchAndProcessData()
        {
            _isRequesting = true;
            Status = DataSourceStatus.Initializing;

            // Step 1: Get API metadata
            string apiUrl = "https://api.rainviewer.com/public/weather-maps.json";

            using (var request = UnityWebRequest.Get(apiUrl))
            {
                request.timeout = Mathf.RoundToInt(requestTimeout);
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"[RainViewer3D] API request failed: {request.error}");
                    Status = DataSourceStatus.Error;
                    _isRequesting = false;
                    yield break;
                }

                string json = request.downloadHandler.text;
                string radarPath = ParseRadarPath(json);
                _lastRadarTimestamp = ParseTimestamp(json);

                if (string.IsNullOrEmpty(radarPath))
                {
                    Debug.LogError("[RainViewer3D] Failed to parse radar path from API response");
                    Status = DataSourceStatus.Error;
                    _isRequesting = false;
                    yield break;
                }

                _lastRadarPath = radarPath;

                // Step 2: Fetch tile grid
                yield return FetchTileGrid(radarPath);
            }

            _isRequesting = false;
        }

        private IEnumerator FetchTileGrid(string radarPath)
        {
            // Calculate center tile coordinates
            var centerTile = LatLonToTile(_latitude, _longitude, zoomLevel);
            int gridSize = tileRadius * 2 + 1;

            Texture2D[,] tiles = new Texture2D[gridSize, gridSize];
            int tilesLoaded = 0;
            int totalTiles = gridSize * gridSize;

            if (debugMode)
                Debug.Log($"[RainViewer3D] Fetching {gridSize}x{gridSize} tile grid at zoom {zoomLevel}");

            // Fetch all tiles
            for (int dy = -tileRadius; dy <= tileRadius; dy++)
            {
                for (int dx = -tileRadius; dx <= tileRadius; dx++)
                {
                    int tileX = centerTile.x + dx;
                    int tileY = centerTile.y + dy;

                    // Clamp tile coordinates to valid range
                    int maxTile = (1 << zoomLevel) - 1;
                    tileX = Mathf.Clamp(tileX, 0, maxTile);
                    tileY = Mathf.Clamp(tileY, 0, maxTile);

                    string tileUrl = BuildTileUrl(radarPath, tileX, tileY);
                    string cacheKey = $"{zoomLevel}_{tileX}_{tileY}";

                    // Check cache first
                    if (_tileCache.TryGetValue(cacheKey, out Texture2D cachedTile))
                    {
                        int gridX = dx + tileRadius;
                        int gridY = dy + tileRadius;
                        tiles[gridX, gridY] = cachedTile;
                        tilesLoaded++;
                        continue;
                    }

                    using (var tileRequest = UnityWebRequestTexture.GetTexture(tileUrl))
                    {
                        tileRequest.timeout = Mathf.RoundToInt(requestTimeout);
                        yield return tileRequest.SendWebRequest();

                        if (tileRequest.result == UnityWebRequest.Result.Success)
                        {
                            Texture2D tex = DownloadHandlerTexture.GetContent(tileRequest);
                            if (tex != null)
                            {
                                int gridX = dx + tileRadius;
                                int gridY = dy + tileRadius;
                                tiles[gridX, gridY] = tex;
                                tilesLoaded++;

                                // Cache the tile
                                if (!_tileCache.ContainsKey(cacheKey))
                                    _tileCache[cacheKey] = tex;
                            }
                        }
                        else if (debugMode)
                        {
                            Debug.LogWarning($"[RainViewer3D] Failed to fetch tile ({tileX}, {tileY}): {tileRequest.error}");
                        }
                    }
                }
            }

            if (debugMode)
                Debug.Log($"[RainViewer3D] Loaded {tilesLoaded}/{totalTiles} tiles");

            if (tilesLoaded > 0)
            {
                // Step 3: Composite tiles into single texture
                CompositeTiles(tiles, gridSize);

                // Update cache info
                _cachedCenterLat = _latitude;
                _cachedCenterLon = _longitude;

                // Step 4: Convert to 3D volume data
                if (autoConvertTo3D)
                {
                    ConvertTo3DVolume();
                }

                Status = DataSourceStatus.Active;
            }
            else
            {
                Status = DataSourceStatus.NoData;
            }

            // Clean up tile references (but keep cached tiles)
            for (int y = 0; y < gridSize; y++)
            {
                for (int x = 0; x < gridSize; x++)
                {
                    tiles[x, y] = null;
                }
            }
        }

        #endregion

        #region Data Processing

        private void CompositeTiles(Texture2D[,] tiles, int gridSize)
        {
            int compositeSize = tileSize * gridSize;

            // Create or resize composite texture
            if (_cachedComposite == null || _cachedComposite.width != compositeSize)
            {
                if (_cachedComposite != null)
                    Destroy(_cachedComposite);

                _cachedComposite = new Texture2D(compositeSize, compositeSize, TextureFormat.RGBA32, false);
                _cachedComposite.filterMode = FilterMode.Bilinear;
            }

            // Composite tiles (flip Y for correct geographic orientation)
            for (int gy = 0; gy < gridSize; gy++)
            {
                for (int gx = 0; gx < gridSize; gx++)
                {
                    Texture2D tile = tiles[gx, gy];
                    if (tile != null)
                    {
                        Color[] pixels = tile.GetPixels();
                        int destY = (gridSize - 1 - gy) * tileSize;
                        _cachedComposite.SetPixels(gx * tileSize, destY, tileSize, tileSize, pixels);
                    }
                }
            }

            _cachedComposite.Apply();

            // Crop to radar texture size (centered)
            CropToRadarTexture();
        }

        private void CropToRadarTexture()
        {
            if (_radarTexture == null) return;

            int targetSize = _radarTexture.width;
            int sourceSize = _cachedComposite.width;
            int offset = (sourceSize - targetSize) / 2;

            Color[] croppedPixels = _cachedComposite.GetPixels(offset, offset, targetSize, targetSize);
            _radarTexture.SetPixels(croppedPixels);
            _radarTexture.Apply();

            OnRadarTextureUpdated?.Invoke(_radarTexture);
        }

        private void ConvertTo3DVolume()
        {
            if (_radarTexture == null || _dataMapper == null) return;

            // Convert 2D radar to 3D volumetric data
            Vector3 centerPosition = new Vector3(_longitude, 0, _latitude);
            _currentData = _dataMapper.ConvertRadarTexture(_radarTexture, centerPosition, coverageRangeNM, _headingDegrees);

            if (_currentData != null)
            {
                _currentData.DataSource = SourceName;
                _currentData.LastUpdateTime = Time.time;

                OnDataUpdated?.Invoke(_currentData);

                if (debugMode)
                {
                    var stats = _currentData.CalculateStats();
                    Debug.Log($"[RainViewer3D] 3D volume updated - Cells: {stats.CellCount}, Coverage: {stats.CoveragePercent:F1}%");
                }
            }
        }

        #endregion

        #region Utility Methods

        private string BuildTileUrl(string radarPath, int x, int y)
        {
            // RainViewer tile URL format:
            // https://tilecache.rainviewer.com/{path}/{size}/{z}/{x}/{y}/{color}/{smooth}_{snow}.png
            return $"https://tilecache.rainviewer.com{radarPath}/{tileSize}/{zoomLevel}/{x}/{y}/{colorScheme}/{smoothing}_{snow}.png";
        }

        private string ParseRadarPath(string json)
        {
            try
            {
                // Parse the most recent radar path from RainViewer API response
                int radarIndex = json.IndexOf("\"radar\"");
                if (radarIndex < 0) return null;

                int pastIndex = json.IndexOf("\"past\"", radarIndex);
                if (pastIndex < 0) return null;

                // Get the last entry from the past array (most recent)
                string radarSection = json.Substring(pastIndex, json.IndexOf("\"radarNowcast\"") - pastIndex);

                int pathIndex = radarSection.LastIndexOf("\"path\"");
                if (pathIndex < 0) return null;

                int colonIndex = radarSection.IndexOf(":", pathIndex);
                int quoteStart = radarSection.IndexOf("\"", colonIndex + 1);
                int quoteEnd = radarSection.IndexOf("\"", quoteStart + 1);

                if (quoteStart > 0 && quoteEnd > quoteStart)
                {
                    return radarSection.Substring(quoteStart + 1, quoteEnd - quoteStart - 1);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[RainViewer3D] Parse error: {e.Message}");
            }
            return null;
        }

        private long ParseTimestamp(string json)
        {
            try
            {
                int radarIndex = json.IndexOf("\"radar\"");
                if (radarIndex < 0) return 0;

                int pastIndex = json.IndexOf("\"past\"", radarIndex);
                if (pastIndex < 0) return 0;

                string radarSection = json.Substring(pastIndex, json.IndexOf("\"radarNowcast\"") - pastIndex);

                int timeIndex = radarSection.LastIndexOf("\"time\"");
                if (timeIndex < 0) return 0;

                int colonIndex = radarSection.IndexOf(":", timeIndex);
                int commaIndex = radarSection.IndexOf(",", colonIndex);
                if (commaIndex < 0) commaIndex = radarSection.IndexOf("}", colonIndex);

                string timeStr = radarSection.Substring(colonIndex + 1, commaIndex - colonIndex - 1).Trim();
                return long.Parse(timeStr);
            }
            catch
            {
                return 0;
            }
        }

        private (int x, int y) LatLonToTile(float lat, float lon, int zoom)
        {
            int n = 1 << zoom;
            int x = (int)((lon + 180.0f) / 360.0f * n);
            float latRad = lat * Mathf.Deg2Rad;
            int y = (int)((1.0f - Mathf.Log(Mathf.Tan(latRad) + 1.0f / Mathf.Cos(latRad)) / Mathf.PI) / 2.0f * n);
            return (x, y);
        }

        private int CalculateZoomLevel(float rangeNM)
        {
            // Adjust zoom based on coverage range
            if (rangeNM <= 20) return 10;
            if (rangeNM <= 40) return 9;
            if (rangeNM <= 80) return 8;
            if (rangeNM <= 160) return 7;
            if (rangeNM <= 320) return 6;
            return 5;
        }

        private float CalculateDistance(float lat1, float lon1, float lat2, float lon2)
        {
            // Haversine formula for distance calculation
            const float R = 6371f; // Earth's radius in km
            float latRad1 = lat1 * Mathf.Deg2Rad;
            float latRad2 = lat2 * Mathf.Deg2Rad;
            float deltaLat = (lat2 - lat1) * Mathf.Deg2Rad;
            float deltaLon = (lon2 - lon1) * Mathf.Deg2Rad;

            float a = Mathf.Sin(deltaLat / 2) * Mathf.Sin(deltaLat / 2) +
                      Mathf.Cos(latRad1) * Mathf.Cos(latRad2) *
                      Mathf.Sin(deltaLon / 2) * Mathf.Sin(deltaLon / 2);
            float c = 2 * Mathf.Atan2(Mathf.Sqrt(a), Mathf.Sqrt(1 - a));

            return R * c;
        }

        private void ClearCache()
        {
            foreach (var tile in _tileCache.Values)
            {
                if (tile != null)
                    Destroy(tile);
            }
            _tileCache.Clear();

            if (_cachedComposite != null)
            {
                Destroy(_cachedComposite);
                _cachedComposite = null;
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// Get the current composite radar texture (all tiles combined)
        /// </summary>
        public Texture2D GetCompositeTexture()
        {
            return _cachedComposite;
        }

        /// <summary>
        /// Get the last fetched radar timestamp
        /// </summary>
        public long LastRadarTimestamp => _lastRadarTimestamp;

        /// <summary>
        /// Get cache statistics
        /// </summary>
        public string GetCacheStats()
        {
            return $"Tiles cached: {_tileCache.Count}, Last update: {_lastRadarTimestamp}";
        }

        /// <summary>
        /// Clear the tile cache and force fresh fetch
        /// </summary>
        public void ClearTileCache()
        {
            ClearCache();
            ForceRefresh();
        }

        /// <summary>
        /// Set color scheme for radar display
        /// </summary>
        public void SetColorScheme(int scheme)
        {
            colorScheme = Mathf.Clamp(scheme, 1, 8);
            ForceRefresh();
        }

        #endregion
    }
}
