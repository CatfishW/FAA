using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace TrafficRadar
{
    /// <summary>
    /// Provider for FAA VFR Sectional Chart tiles from ArcGIS services.
    /// Fetches and caches aeronautical chart tiles for background display.
    /// </summary>
    public class FAASectionalChartProvider : MonoBehaviour
    {
        [Header("Service Settings")]
        [Tooltip("FAA ArcGIS MapServer URL for sectional charts")]
        [SerializeField] private string tileServerUrl = "https://tiles.arcgis.com/tiles/ssFJjBXIUyZDrSYZ/arcgis/rest/services/VFR_Sectional/MapServer";
        
        [Tooltip("Tile request timeout in seconds")]
        [SerializeField] private float requestTimeout = 15f;
        

        [Header("Tile Settings")]
        [Tooltip("Zoom level for tiles (4-12)")]
        [Range(4, 12)]
        [SerializeField] private int zoomLevel = 8;
        
        [Tooltip("Tile size in pixels")]
        [SerializeField] private int tileSize = 256;

        [Header("Cache Settings")]
        [Tooltip("Maximum number of tiles to cache")]
        [SerializeField] private int maxCachedTiles = 50;
        
        [Tooltip("Cache expiration time in seconds")]
        [SerializeField] private float cacheExpirationSeconds = 3600f; // 1 hour

        [Header("Fallback")]
        [Tooltip("Use procedural background when tiles unavailable")]
        [SerializeField] private bool useProceduralFallback = true;
        
        [SerializeField] private Color fallbackBackgroundColor = new Color(0.1f, 0.15f, 0.2f, 1f);

        // Events
        public event System.Action<Texture2D> OnChartTileLoaded;
        public event System.Action<string> OnLoadError;

        // Cache
        private Dictionary<string, CachedTile> tileCache = new Dictionary<string, CachedTile>();
        private Texture2D currentCompositeTexture;
        private int compositeSize = 512;

        // State
        private bool isLoading;
        private float lastFetchLat;
        private float lastFetchLon;

        private struct CachedTile
        {
            public Texture2D texture;
            public float timestamp;
        }

        #region Properties

        public bool IsLoading => isLoading;
        public Texture2D CurrentTexture => currentCompositeTexture;
        
        public int ZoomLevel
        {
            get => zoomLevel;
            set => zoomLevel = Mathf.Clamp(value, 4, 12);
        }

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            CreateCompositeTexture();
        }

        private void OnDestroy()
        {
            // Clean up textures
            foreach (var tile in tileCache.Values)
            {
                if (tile.texture != null)
                    Destroy(tile.texture);
            }
            tileCache.Clear();

            if (currentCompositeTexture != null)
                Destroy(currentCompositeTexture);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Fetch chart tiles for the specified location.
        /// </summary>
        public void FetchChartTiles(float latitude, float longitude, float rangeNM)
        {
            if (isLoading)
                return;

            // Adjust zoom based on range
            zoomLevel = GetZoomForRange(rangeNM);
            
            lastFetchLat = latitude;
            lastFetchLon = longitude;

            StartCoroutine(FetchTilesCoroutine(latitude, longitude));
        }

        /// <summary>
        /// Clear the tile cache.
        /// </summary>
        public void ClearCache()
        {
            foreach (var tile in tileCache.Values)
            {
                if (tile.texture != null)
                    Destroy(tile.texture);
            }
            tileCache.Clear();
        }

        /// <summary>
        /// Set the composite texture size.
        /// </summary>
        public void SetCompositeSize(int size)
        {
            compositeSize = size;
            CreateCompositeTexture();
        }

        #endregion

        #region Private Methods

        private void CreateCompositeTexture()
        {
            if (currentCompositeTexture != null)
                Destroy(currentCompositeTexture);

            currentCompositeTexture = new Texture2D(compositeSize, compositeSize, TextureFormat.RGBA32, false);
            currentCompositeTexture.wrapMode = TextureWrapMode.Clamp;
            currentCompositeTexture.filterMode = FilterMode.Bilinear;

            // Fill with fallback color
            Color32[] pixels = new Color32[compositeSize * compositeSize];
            Color32 bgColor = fallbackBackgroundColor;
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = bgColor;
            }
            currentCompositeTexture.SetPixels32(pixels);
            currentCompositeTexture.Apply();
        }

        private IEnumerator FetchTilesCoroutine(float latitude, float longitude)
        {
            isLoading = true;

            // Get the center tile coordinates
            var centerTile = LatLonToTile(latitude, longitude, zoomLevel);
            
            // Fetch center tile and surrounding tiles (3x3 grid)
            List<(int x, int y, int offsetX, int offsetY)> tilesToFetch = new List<(int, int, int, int)>();
            
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    tilesToFetch.Add((centerTile.x + dx, centerTile.y + dy, dx, dy));
                }
            }

            List<Texture2D> fetchedTiles = new List<Texture2D>();
            List<(int offsetX, int offsetY)> tileOffsets = new List<(int, int)>();

            foreach (var tile in tilesToFetch)
            {
                string cacheKey = $"{zoomLevel}_{tile.x}_{tile.y}";
                
                // Check cache first
                if (tileCache.TryGetValue(cacheKey, out CachedTile cached))
                {
                    if (Time.time - cached.timestamp < cacheExpirationSeconds && cached.texture != null)
                    {
                        fetchedTiles.Add(cached.texture);
                        tileOffsets.Add((tile.offsetX, tile.offsetY));
                        continue;
                    }
                }

                // Fetch from server
                yield return FetchSingleTile(tile.x, tile.y, (tex) =>
                {
                    if (tex != null)
                    {
                        // Add to cache
                        tileCache[cacheKey] = new CachedTile { texture = tex, timestamp = Time.time };
                        fetchedTiles.Add(tex);
                        tileOffsets.Add((tile.offsetX, tile.offsetY));

                        // Manage cache size
                        TrimCache();
                    }
                });
            }

            // Composite tiles into single texture
            if (fetchedTiles.Count > 0)
            {
                CompositeTiles(fetchedTiles, tileOffsets);
            }
            else if (useProceduralFallback)
            {
                GenerateProceduralBackground(latitude, longitude);
            }

            isLoading = false;
            OnChartTileLoaded?.Invoke(currentCompositeTexture);
        }

        private IEnumerator FetchSingleTile(int x, int y, System.Action<Texture2D> callback)
        {
            // FAA ArcGIS tile URL format
            string url = $"{tileServerUrl}/tile/{zoomLevel}/{y}/{x}";

            using (var request = UnityWebRequestTexture.GetTexture(url))
            {
                request.timeout = Mathf.RoundToInt(requestTimeout);
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    Texture2D tex = DownloadHandlerTexture.GetContent(request);
                    callback?.Invoke(tex);
                }
                else
                {
                    Debug.LogWarning($"[FAASectionalChartProvider] Failed to fetch tile: {request.error}");
                    OnLoadError?.Invoke(request.error);
                    callback?.Invoke(null);
                }
            }
        }

        private void CompositeTiles(List<Texture2D> tiles, List<(int offsetX, int offsetY)> offsets)
        {
            int tilePixelSize = compositeSize / 3;
            Color32[] compositePixels = new Color32[compositeSize * compositeSize];
            
            // Fill with fallback color first
            Color32 bgColor = fallbackBackgroundColor;
            for (int i = 0; i < compositePixels.Length; i++)
            {
                compositePixels[i] = bgColor;
            }

            for (int i = 0; i < tiles.Count; i++)
            {
                var tile = tiles[i];
                var offset = offsets[i];

                if (tile == null) continue;

                // Calculate position in composite
                int startX = (offset.offsetX + 1) * tilePixelSize;
                int startY = (offset.offsetY + 1) * tilePixelSize;

                // Sample and copy pixels
                for (int ty = 0; ty < tilePixelSize; ty++)
                {
                    for (int tx = 0; tx < tilePixelSize; tx++)
                    {
                        int destX = startX + tx;
                        int destY = startY + ty;

                        if (destX >= 0 && destX < compositeSize && destY >= 0 && destY < compositeSize)
                        {
                            // Sample from tile texture
                            float sampleX = (float)tx / tilePixelSize;
                            float sampleY = (float)ty / tilePixelSize;
                            Color c = tile.GetPixelBilinear(sampleX, sampleY);
                            compositePixels[destY * compositeSize + destX] = c;
                        }
                    }
                }
            }

            currentCompositeTexture.SetPixels32(compositePixels);
            currentCompositeTexture.Apply();
        }

        private void GenerateProceduralBackground(float latitude, float longitude)
        {
            Color32[] pixels = new Color32[compositeSize * compositeSize];
            
            // Create a subtle grid pattern
            for (int y = 0; y < compositeSize; y++)
            {
                for (int x = 0; x < compositeSize; x++)
                {
                    Color32 c = fallbackBackgroundColor;
                    
                    // Add grid lines
                    if (x % 32 == 0 || y % 32 == 0)
                    {
                        c = new Color32(
                            (byte)Mathf.Min(255, c.r + 20),
                            (byte)Mathf.Min(255, c.g + 20),
                            (byte)Mathf.Min(255, c.b + 20),
                            c.a
                        );
                    }

                    pixels[y * compositeSize + x] = c;
                }
            }

            currentCompositeTexture.SetPixels32(pixels);
            currentCompositeTexture.Apply();
        }

        private (int x, int y) LatLonToTile(float lat, float lon, int zoom)
        {
            int n = 1 << zoom;
            int x = (int)((lon + 180.0f) / 360.0f * n);
            int y = (int)((1.0f - Mathf.Log(Mathf.Tan(lat * Mathf.Deg2Rad) +
                1.0f / Mathf.Cos(lat * Mathf.Deg2Rad)) / Mathf.PI) / 2.0f * n);
            return (x, y);
        }

        private int GetZoomForRange(float rangeNM)
        {
            // Map range to appropriate zoom level
            if (rangeNM <= 5) return 11;
            if (rangeNM <= 10) return 10;
            if (rangeNM <= 20) return 9;
            if (rangeNM <= 40) return 8;
            if (rangeNM <= 80) return 7;
            return 6;
        }

        private void TrimCache()
        {
            if (tileCache.Count <= maxCachedTiles)
                return;

            // Remove oldest entries
            List<string> keysToRemove = new List<string>();
            float oldestTime = float.MaxValue;
            string oldestKey = null;

            foreach (var kvp in tileCache)
            {
                if (kvp.Value.timestamp < oldestTime)
                {
                    oldestTime = kvp.Value.timestamp;
                    oldestKey = kvp.Key;
                }
            }

            if (oldestKey != null)
            {
                if (tileCache[oldestKey].texture != null)
                    Destroy(tileCache[oldestKey].texture);
                tileCache.Remove(oldestKey);
            }
        }

        #endregion
    }
}
