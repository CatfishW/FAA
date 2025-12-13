/*           INFINITY CODE           */
/*     https://infinity-code.com     */

#if (!UNITY_WP_8_1 && !UNITY_WEBGL) || UNITY_EDITOR
#define ALLOW_FILECACHE
#elif UNITY_WEBGL && UNITY_2020_2_OR_NEWER
#define ALLOW_FILECACHE
#endif

#if ALLOW_FILECACHE
using System.IO;
#endif

using System;
using System.Text;
using UnityEngine;

namespace OnlineMaps
{
    /// <summary>
    /// Class for caching tiles in memory and the file system.
    /// </summary>
    [AddComponentMenu("Infinity Code/Online Maps/Plugins/Cache")]
    [Plugin("Cache", true)]
    public partial class Cache : MonoBehaviour, ISavableAdvanced
    {
        private static Cache _instance;
        private static StringBuilder _stringBuilder;

        /// <summary>
        /// Event occurs when loading the tile from the file cache or memory cache.
        /// </summary>
        public Action<Tile> OnLoadedFromCache;

        private SavableItem[] savableItems;

        /// <summary>
        /// The reference to an instance of the cache.
        /// </summary>
        public static Cache instance => _instance;

        /// <summary>
        /// Clear all caches.
        /// </summary>
        public void ClearAllCaches()
        {
            ClearMemoryCache();
            ClearFileCache();
        }

        /// <summary>
        /// Gets the savable items for the component.
        /// </summary>
        /// <returns>An array of savable items.</returns>
        public SavableItem[] GetSavableItems()
        {
            if (savableItems != null) return savableItems;

            savableItems = new[]
            {
                new SavableItem("cache", "Cache", SaveSettings)
                {
                    loadCallback = LoadSettings
                }
            };

            return savableItems;
        }

        /// <summary>
        /// Gets a reusable instance of a StringBuilder.
        /// </summary>
        /// <returns>A StringBuilder instance.</returns>
        protected static StringBuilder GetStringBuilder()
        {
            if (_stringBuilder == null) _stringBuilder = new StringBuilder();
            else _stringBuilder.Length = 0;

            return _stringBuilder;
        }

        private void LoadSettings(JSONObject json)
        {
            json.DeserializeObject(this);
        }

        private void OnDestroy()
        {
            TileManager.OnPreloadTiles -= OnPreloadTiles;
            TileManager.OnLoadFromCache -= OnStartDownloadTileM;
            Tile.OnTileDownloaded -= OnTileDownloaded;
        }

        private void OnDisable()
        {
            if (saveFileCacheAtlasCoroutine != null)
            {
                StopCoroutine(saveFileCacheAtlasCoroutine);
                if (fileCacheAtlas != null) fileCacheAtlas.Save(this);
            }

            if (saveCustomCacheAtlasCoroutine != null)
            {
                StopCoroutine(saveCustomCacheAtlasCoroutine);
                if (customCacheAtlas != null) customCacheAtlas.Save(this);
            }
        }

        private void OnEnable()
        {
            _instance = this;
        }

        private void OnPreloadTiles(TileManager tileManager)
        {
            Tile[] tiles;

            lock (Tile.lockTiles)
            {
                tiles = tileManager.tiles.ToArray();
            }

            float start = Time.realtimeSinceStartup;
            for (int i = 0; i < tiles.Length; i++)
            {
                Tile tile = tiles[i];
                if (tile.status != TileStatus.idle || tile.cacheChecked) continue;
                if (!TryLoadFromCache(tile)) tile.cacheChecked = true;
                else if (OnLoadedFromCache != null) OnLoadedFromCache(tile);
                if (Time.realtimeSinceStartup - start > 0.02) return;
            }
        }

        private void OnStartDownloadTileM(Tile tile)
        {
            if (TryLoadFromCache(tile))
            {
                if (OnLoadedFromCache != null) OnLoadedFromCache(tile);
            }
            else
            {
                if (TileManager.OnStartDownloadTile != null) TileManager.OnStartDownloadTile(tile);
                else TileManager.StartDownloadTile(tile);
            }
        }

        private void OnTileDownloaded(Tile tile)
        {
            if (useMemoryCache) AddMemoryCacheItem(tile);
            if (useFileCache) AddFileCacheItem(tile, tile.www.bytes);
        }

        private JSONItem SaveSettings()
        {
            return JSON.Serialize(new
            {
                useMemoryCache,
                maxMemoryCacheSize,
                memoryCacheUnloadRate,

                useFileCache,
                maxFileCacheSize,
                fileCacheUnloadRate,
                fileCacheLocation,
                fileCacheCustomPath,
                fileCacheTilePath
            });
        }

        private void Start()
        {
            TileManager.OnLoadFromCache += OnStartDownloadTileM;
            TileManager.OnPreloadTiles += OnPreloadTiles;
            Tile.OnTileDownloaded += OnTileDownloaded;
        }

        /// <summary>
        /// Base class for cache atlas.
        /// </summary>
        /// <typeparam name="T">Type of cache item</typeparam>
        public abstract class CacheAtlas<T> where T : CacheItem
        {
            /// <summary>
            /// Version of the atlas.
            /// </summary>
            protected const short ATLAS_VERSION = 1;

            /// <summary>
            /// Capacity of the atlas.
            /// </summary>
            protected int capacity = 256;

            /// <summary>
            /// Count of items in the atlas.
            /// </summary>
            protected int count;

            /// <summary>
            /// Items of the atlas.
            /// </summary>
            protected T[] items;

            /// <summary>
            /// Name of the atlas.
            /// </summary>
            protected abstract string atlasName { get; }

            /// <summary>
            /// Size of the atlas.
            /// </summary>
            public int size { get; protected set; }

            /// <summary>
            /// Constructor
            /// </summary>
            public CacheAtlas()
            {
                size = 0;
                items = new T[capacity];
            }

            /// <summary>
            /// Checks whether the atlas contains the specified file.
            /// </summary>
            /// <param name="filename">Name of the file</param>
            /// <returns>True - contains, false - not contains.</returns>
            public bool Contains(string filename)
            {
                int hash = filename.GetHashCode();
                for (int i = 0; i < count; i++)
                {
                    if (items[i].hash == hash && items[i].key == filename) return true;
                }

                return false;
            }

            /// <summary>
            /// Creates a new item.
            /// </summary>
            /// <param name="filename">Name of the file</param>
            /// <param name="size">Size of the file</param>
            /// <param name="time">Creation time</param>
            /// <returns>Item</returns>
            public abstract T CreateItem(string filename, int size, long time);

            /// <summary>
            /// Deletes old items.
            /// </summary>
            /// <param name="cache">Cache</param>
            public abstract void DeleteOldItems(Cache cache);

            /// <summary>
            /// Loads the atlas from the cache.
            /// </summary>
            /// <param name="cache">Cache</param>
            public void Load(Cache cache)
            {
#if ALLOW_FILECACHE
                StringBuilder builder = cache.GetFileCacheFolder();
                builder.Append("/").Append(atlasName);
                string filename = builder.ToString();

                if (!File.Exists(filename)) return;

                FileStream stream = new FileStream(filename, FileMode.Open);
                BinaryReader reader = new BinaryReader(stream);

                byte c1 = reader.ReadByte();
                byte c2 = reader.ReadByte();

                if (c1 == 'T' && c2 == 'C')
                {
                    int cacheVersion = reader.ReadInt16();
                    if (cacheVersion > 0)
                    {
                        // For future versions
                    }
                }
                else stream.Position = 0;

                size = reader.ReadInt32();

                long l = stream.Length;
                while (stream.Position < l)
                {
                    filename = reader.ReadString();
                    int s = reader.ReadInt32();
                    long time = reader.ReadInt64();
                    T item = CreateItem(filename, s, time);
                    if (capacity <= count)
                    {
                        capacity *= 2;
                        Array.Resize(ref items, capacity);
                    }

                    items[count++] = item;
                }

                reader.Close();
#endif
            }

            /// <summary>
            /// Saves the atlas.
            /// </summary>
            /// <param name="cache">Cache</param>
            public void Save(Cache cache)
            {
#if ALLOW_FILECACHE
                StringBuilder builder = cache.GetFileCacheFolder();
                builder.Append("/").Append(atlasName);
                string filename = builder.ToString();

                FileInfo fileInfo = new FileInfo(filename);
                if (!Directory.Exists(fileInfo.DirectoryName)) Directory.CreateDirectory(fileInfo.DirectoryName);

                T[] itemsCopy = new T[items.Length];
                items.CopyTo(itemsCopy, 0);

#if !UNITY_WEBGL
                ThreadManager.AddThreadAction(() =>
                {
#endif
                try
                {
                    FileStream stream = new FileStream(filename, FileMode.Create);
                    BinaryWriter writer = new BinaryWriter(stream);

                    writer.Write((byte)'T');
                    writer.Write((byte)'C');
                    writer.Write(ATLAS_VERSION);

                    writer.Write(size);

                    for (int i = 0; i < count; i++)
                    {
                        T item = itemsCopy[i];
                        writer.Write(item.key);
                        writer.Write(item.size);
                        writer.Write(item.time);
                    }

                    writer.Close();
                }
                catch
                {
                }
#if !UNITY_WEBGL
                });
#endif
#endif
            }
        }

        /// <summary>
        /// Base class for cache item.
        /// </summary>
        public abstract class CacheItem
        {
            /// <summary>
            /// Size of the item.
            /// </summary>
            public int size;

            /// <summary>
            /// Hash of the key.
            /// </summary>
            public int hash;

            /// <summary>
            /// Key of the item.
            /// </summary>
            public string key;

            /// <summary>
            /// Creation time.
            /// </summary>
            public long time;

            /// <summary>
            /// Creates a new item.
            /// </summary>
            /// <returns>Item</returns>
            public static CacheItem Create()
            {
                return null;
            }
        }

        /// <summary>
        /// Location of the file cache
        /// </summary>
        public enum CacheLocation
        {
            /// <summary>
            /// Application.persistentDataPath
            /// </summary>
            persistentDataPath,

            /// <summary>
            /// Custom
            /// </summary>
            custom
        }
    }
}