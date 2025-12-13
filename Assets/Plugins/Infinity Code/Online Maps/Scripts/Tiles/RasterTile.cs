/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using System.Text.RegularExpressions;
using UnityEngine;

namespace OnlineMaps
{
    /// <summary>
    /// Implements work with raster tiles
    /// </summary>
    public class RasterTile : Tile
    {
        /// <summary>
        /// Buffer default colors.
        /// </summary>
        public static Color32[] defaultColors;

        /// <summary>
        /// The texture that is used until the tile is loaded.
        /// </summary>
        public static Texture2D emptyColorTexture;

        /// <summary>
        /// Labels is used in tile?
        /// </summary>
        public bool labels;

        /// <summary>
        /// Language is used in tile?
        /// </summary>
        public string language;

        /// <summary>
        /// The map type used for the tile.
        /// </summary>
        public MapType mapType;

        /// <summary>
        /// This event occurs when the tile gets colors based on parent colors.
        /// </summary>
        public Action<RasterTile> OnSetColor;

        /// <summary>
        /// Traffic texture.
        /// </summary>
        public Texture2D trafficTexture;

        /// <summary>
        /// Instance of the traffic loader.
        /// </summary>
        public WebRequest trafficWWW;

        private Color32[] _colors;
        private Texture2D _texture;
        private TrafficProvider _trafficProvider;
        private string _trafficURL;
        private byte[] labelData;
        private Color32[] labelColors;
        public Color32[] mergedColors;


        /// <summary>
        /// Array of colors of the tile.
        /// </summary>
        public Color32[] colors
        {
            get
            {
                if (mergedColors != null) return mergedColors;
                if (_colors != null) return _colors;
                return defaultColors;
            }
            set
            {
                _colors = value;
                hasColors = _colors != null;
            }
        }

        /// <summary>
        /// Provider of the traffic textures
        /// </summary>
        public TrafficProvider trafficProvider
        {
            get => _trafficProvider;
            set
            {
                _trafficProvider = value;
                _trafficURL = null;
            }
        }

        /// <summary>
        ///  URL of the traffic texture
        /// </summary>
        public string trafficURL
        {
            get
            {
                if (string.IsNullOrEmpty(_trafficURL))
                {
                    if (trafficProvider.isCustom) _trafficURL = Regex.Replace(map.customTrafficProviderURL, @"{\w+}", CustomTrafficProviderReplaceToken);
                    else _trafficURL = trafficProvider.GetURL(this);
                }

                return _trafficURL;
            }
            set => _trafficURL = value;
        }

        public override Texture2D texture
        {
            get => _texture;
            set
            {
                if (!value)
                {
                    _texture = value;
                    status = TileStatus.disposed;
                    return;
                }

                if (!map)
                {
                    _texture = value;
                    status = TileStatus.loaded;
                    return;
                }
                
                if (map.control.resultIsTexture)
                {
                    ApplyTexture(value);
                    map.buffer.ApplyTile(this);
                    Utils.Destroy(value);
                }
                else
                {
                    _texture = value;
                    TileSetControl tileSetControl = map.control as TileSetControl;
                    if (tileSetControl && tileSetControl.compressTextures) _texture.Compress(true);
                    status = TileStatus.loaded;
                }
            }
        }

        public override string url
        {
            get
            {
                if (string.IsNullOrEmpty(_url))
                {
                    if (mapType.isCustom) _url = Regex.Replace(map.customProviderURL, @"{\w+}", CustomProviderReplaceToken);
                    else _url = mapType.GetURL(this);
                }

                return _url;
            }
        }

        /// <summary>
        /// Initializes a new instance of the RasterTile class.
        /// </summary>
        /// <param name="x">The x-coordinate of the tile.</param>
        /// <param name="y">The y-coordinate of the tile.</param>
        /// <param name="zoom">The zoom level of the tile.</param>
        /// <param name="map">The map instance.</param>
        /// <param name="isMapTile">Indicates whether this is a map tile.</param>
        public RasterTile(int x, int y, int zoom, Map map, bool isMapTile = true) : base(x, y, zoom, map, isMapTile)
        {
            _trafficProvider = map.trafficProvider;
            mapType = map.activeType;

            labels = map.labels;
            language = map.language;
        }

        /// <summary>
        /// Applies the colors to the child tiles.
        /// </summary>
        public void ApplyColorsToChildren()
        {
            if (OnSetColor != null) OnSetColor(this);
        }

        private void ApplyLabelTexture()
        {
            Texture2D t = new Texture2D(Constants.TileSize, Constants.TileSize);
            t.LoadImage(labelData);
            labelData = null;
            labelColors = t.GetPixels32();

            if (map.control.resultIsTexture)
            {
#if !UNITY_WEBGL
                if (map.renderInThread) ThreadManager.AddThreadAction(MergeColors);
                else MergeColors();
#else
                MergeColors();
#endif
                Utils.Destroy(t);
            }
            else
            {
                _colors = _texture.GetPixels32();
                MergeColors();
                t.SetPixels32(mergedColors);
                _texture = t;
                mergedColors = null;
            }
        }

        /// <summary>
        /// Applies the given texture to the tile.
        /// </summary>
        /// <param name="texture">The texture to apply.</param>
        public void ApplyTexture(Texture2D texture)
        {
            _colors = texture.GetPixels32();
            status = TileStatus.loaded;
            hasColors = true;
        }

        /// <summary>
        /// Checks the size of the tile texture.
        /// </summary>
        /// <param name="texture">Tile texture</param>
        public void CheckTextureSize(Texture2D texture)
        {
            if (texture == null) return;
            if (map.control.resultIsTexture && mapType.isCustom && (texture.width != 256 || texture.height != 256))
            {
                Debug.LogError(string.Format("Size tiles {0}x{1}. Expected to 256x256. Please check the URL.", texture.width, texture.height));
                status = TileStatus.error;
            }
        }

        private string CustomTrafficProviderReplaceToken(Match match)
        {
            string v = match.Value.ToLower().Trim('{', '}');

            if (OnReplaceTrafficURLToken != null)
            {
                string ret = OnReplaceTrafficURLToken(this, v);
                if (ret != null) return ret;
            }

            if (v == "zoom") return zoom.ToString();
            if (v == "x") return x.ToString();
            if (v == "y") return y.ToString();
            if (v == "quad") return Utils.TileToQuadKey(x, y, zoom);
            return v;
        }

        public override void Destroy()
        {
            base.Destroy();
            
            if (trafficWWW != null)
            {
                trafficWWW.Dispose();
                trafficWWW = null;
            }
            
            if (_texture)
            {
                if (loadedFromResources) Resources.UnloadAsset(_texture);
                else Utils.Destroy(_texture);
                texture = null;
            }

            if (trafficTexture)
            {
                Utils.Destroy(trafficTexture);
                trafficTexture = null;
            }

            mapType = null;
            _trafficProvider = null;
            _colors = null;
            mergedColors = null;
            labelData = null;
            labelColors = null;
            OnSetColor = null;
        }

        public override void DownloadComplete()
        {
            base.DownloadComplete();

            if (www == null) Debug.Log(status + "  " + this);
            else
            {
                data = www.bytes;
                LoadTexture();
                data = null;
            }
        }

        public override Color GetPixel(float u, float v)
        {
            if (!map.control.resultIsTexture)
            {
                return texture.GetPixelBilinear(u, v);
            }

            int row = (int)(v * Constants.TileSize);
            return colors[(int)((row + v) * Constants.TileSize)];
        }

        /// <summary>
        /// Loads the texture for the tile.
        /// </summary>
        public void LoadTexture()
        {
            if (status == TileStatus.error) return;

            Texture2D texture = new Texture2D(Constants.TileSize, Constants.TileSize);
            if (map.useSoftwareJPEGDecoder) LoadTexture(texture, data);
            else
            {
                texture.LoadImage(data);
                texture.wrapMode = TextureWrapMode.Clamp;
            }

            CheckTextureSize(texture);

            if (status != TileStatus.error)
            {
                ApplyTexture(texture);
                if (labelData != null) ApplyLabelTexture();
            }

            Utils.Destroy(texture);
        }

        /// <summary>
        /// Loads the texture from the given byte array.
        /// </summary>
        /// <param name="texture">The texture to load the data into.</param>
        /// <param name="bytes">The byte array containing the texture data.</param>
        public static void LoadTexture(Texture2D texture, byte[] bytes)
        {
            if (bytes[0] == 0xFF)
            {
                Color32[] colors = JPEGDecoder.GetColors(bytes);
                texture.SetPixels32(colors);
                texture.Apply();
            }
            else texture.LoadImage(bytes);
        }

        protected override void LoadTileFromWWW(WebRequest www)
        {
            if (!map) return;

            if (map.control.resultIsTexture)
            {
                DownloadComplete();
                if (status != TileStatus.error) map.buffer.ApplyTile(this);
            }
            else
            {
                Texture2D tileTexture = new Texture2D(256, 256, TextureFormat.RGB24, map.control.mipmapForTiles)
                {
                    wrapMode = TextureWrapMode.Clamp
                };

                if (map.useSoftwareJPEGDecoder) LoadTexture(tileTexture, www.bytes);
                else www.LoadImageIntoTexture(tileTexture);

                tileTexture.name = zoom + "x" + x + "x" + y;

                CheckTextureSize(tileTexture);

                if (status != TileStatus.error && status != TileStatus.disposed)
                {
                    texture = tileTexture;
                }
            }

            if (status != TileStatus.error && status != TileStatus.disposed)
            {
                if (OnTileDownloaded != null) OnTileDownloaded(this);
            }

            MarkLoaded();
            map.Redraw();
        }

        private void MergeColors()
        {
            try
            {
                if (status == TileStatus.error || status == TileStatus.disposed) return;
                if (labelColors == null || _colors == null || labelColors.Length != _colors.Length) return;

                Color32[] mColors = new Color32[_colors.Length];

                for (int i = 0; i < _colors.Length; i++)
                {
                    Color32 lColor = labelColors[i];
                    float a = lColor.a;
                    if (a > 0)
                    {
                        mColors[i] = Color32.Lerp(_colors[i], lColor, a);
                        mColors[i].a = 255;
                    }
                    else mColors[i] = _colors[i];
                }

                mergedColors = mColors;
                labelColors = null;
            }
            catch
            {
            }
        }

        /// <summary>
        /// Handles the completion of the label download.
        /// </summary>
        /// <returns>True if the label texture is applied successfully; otherwise, false.</returns>
        public bool OnLabelDownloadComplete()
        {
            labelData = trafficWWW.bytes;
            if (status == TileStatus.loaded)
            {
                ApplyLabelTexture();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Sets the label data for the tile.
        /// </summary>
        /// <param name="bytes">The byte array containing the label data.</param>
        /// <returns>True if the label texture is applied successfully; otherwise, false.</returns>
        public bool SetLabelData(byte[] bytes)
        {
            labelData = bytes;
            if (status == TileStatus.loaded)
            {
                ApplyLabelTexture();
                return true;
            }

            return false;
        }
    }
}