using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace OnlineMaps
{
    /// <summary>
    /// Class of map type
    /// </summary>
    public class MapType
    {
        /// <summary>
        /// ID of map type
        /// </summary>
        public readonly string id;

        /// <summary>
        /// Array of extra fields for the map type.
        /// </summary>
        public TileProvider.IExtraField[] extraFields;

        /// <summary>
        /// Full ID of the map type.
        /// </summary>
        public string fullID;

        /// <summary>
        /// Human-readable map type title.
        /// </summary>
        public readonly string title;

        /// <summary>
        /// Reference to provider instance.
        /// </summary>
        public TileProvider provider;

        /// <summary>
        /// Index of map type
        /// </summary>
        public int index;

        /// <summary>
        /// Indicates that this is a custom provider.
        /// </summary>
        public bool isCustom;

        private bool hasWithoutLabels;
        private bool hasWithLabels;

        private string _ext;
        private bool? _hasLanguage;
        private bool? _hasLabels;
        private bool? _labelsEnabled;
        private string _urlWithLabels;
        private string _urlWithoutLabels;
        private bool? _useHTTP;
        private string _variantWithLabels;
        private string _variantWithoutLabels;
        private string _propWithLabels;
        private string _propWithoutLabels;
        private string _prop2;
        private bool? _logUrl;

        /// <summary>
        /// Extension. Token {ext}, that is being replaced in the URL.
        /// </summary>
        public string ext
        {
            get
            {
                if (!string.IsNullOrEmpty(_ext)) return _ext;
                if (!string.IsNullOrEmpty(provider.ext)) return provider.ext;
                return string.Empty;
            }
            set => _ext = value;
        }

        /// <summary>
        /// Indicates that the map type supports multilanguage.
        /// </summary>
        public bool hasLanguage
        {
            get
            {
                if (_hasLanguage.HasValue) return _hasLanguage.Value;
                if (provider.hasLanguage.HasValue) return provider.hasLanguage.Value;
                return false;
            }
            set => _hasLanguage = value;
        }

        /// <summary>
        /// Indicates that the provider supports a map with labels.
        /// </summary>
        public bool hasLabels
        {
            get
            {
                if (_hasLabels.HasValue) return _hasLabels.Value;
                if (provider.hasLabels.HasValue) return provider.hasLabels.Value;
                return false;
            }
            set => _hasLabels = value;
        }

        /// <summary>
        /// Indicates that the label is always enabled.
        /// </summary>
        public bool labelsEnabled
        {
            get
            {
                if (_labelsEnabled.HasValue) return _labelsEnabled.Value;
                if (provider.labelsEnabled.HasValue) return provider.labelsEnabled.Value;
                return false;
            }
            set => _labelsEnabled = value;
        }

        /// <summary>
        /// Indicates whether to log the URL.
        /// </summary>
        public bool logUrl
        {
            get
            {
                if (_logUrl.HasValue) return _logUrl.Value;
                return provider.logUrl;
            }
            set { _logUrl = value; }
        }

        /// <summary>
        /// Property. Token {prop} when label enabled, that is being replaced in the URL.
        /// </summary>
        public string propWithLabels
        {
            get
            {
                if (!string.IsNullOrEmpty(_propWithLabels)) return _propWithLabels;
                return provider.prop;
            }
            set
            {
                _propWithLabels = value;
                labelsEnabled = true;
                hasWithLabels = true;
                if (hasWithoutLabels) hasLabels = true;
            }
        }

        /// <summary>
        /// Property. Token {prop} when label disabled, that is being replaced in the URL.
        /// </summary>
        public string propWithoutLabels
        {
            get
            {
                if (!string.IsNullOrEmpty(_propWithoutLabels)) return _propWithoutLabels;
                return provider.prop;
            }
            set
            {
                _propWithoutLabels = value;
                hasWithoutLabels = true;
                if (hasWithLabels) hasLabels = true;
            }
        }

        /// <summary>
        /// Property. Token {prop2}, that is being replaced in the URL.
        /// </summary>
        public string prop2
        {
            get => string.IsNullOrEmpty(_prop2) ? provider.prop2 : _prop2;
            set => _prop2 = value;
        }

        /// <summary>
        /// Variant. Token {variant}, that is being replaced in the URL.
        /// </summary>
        public string variant
        {
            set
            {
                _variantWithoutLabels = value;
                _variantWithLabels = value;
                hasLabels = true;
                hasWithLabels = true;
                hasWithoutLabels = true;
                labelsEnabled = true;
            }
        }

        /// <summary>
        /// Variant. Token {variant} when label enabled, that is being replaced in the URL.
        /// </summary>
        public string variantWithLabels
        {
            get => _variantWithLabels;
            set
            {
                _variantWithLabels = value;
                labelsEnabled = true;
                hasWithLabels = true;
                if (hasWithoutLabels) hasLabels = true;
            }
        }

        /// <summary>
        /// Variant. Token {variant} when label disabled, that is being replaced in the URL.
        /// </summary>
        public string variantWithoutLabels
        {
            get => _variantWithoutLabels;
            set
            {
                _variantWithoutLabels = value;
                hasWithoutLabels = true;
                if (hasWithLabels) hasLabels = true;
            }
        }

        /// <summary>
        /// Gets / sets the URL pattern of tiles with labels.
        /// </summary>
        public string urlWithLabels
        {
            get => _urlWithLabels;
            set
            {
                _urlWithLabels = value;
                labelsEnabled = true;
                hasWithLabels = true;
                if (hasWithoutLabels) hasLabels = true;
                if (!value.StartsWith("https")) _useHTTP = true;
            }
        }

        /// <summary>
        /// Gets / sets the URL pattern of tiles without labels.
        /// </summary>
        public string urlWithoutLabels
        {
            get => _urlWithoutLabels;
            set
            {
                _urlWithoutLabels = value;
                hasWithoutLabels = true;
                if (hasWithLabels) hasLabels = true;
                if (!value.StartsWith("https")) _useHTTP = true;
            }
        }

        /// <summary>
        /// Indicates that the map type uses HTTP.
        /// </summary>
        public bool useHTTP
        {
            get
            {
                if (_useHTTP.HasValue) return _useHTTP.Value;
                if (provider.useHTTP.HasValue) return provider.useHTTP.Value;
                return false;
            }
            set => _useHTTP = value;
        }

        /// <summary>
        /// Gets or sets the value of the extra field.
        /// </summary>
        /// <param name="token">Token (ID) of the extra field.</param>
        public string this[string token]
        {
            get
            {
                if (extraFields != null)
                {
                    foreach (TileProvider.IExtraField f in extraFields)
                    {
                        TileProvider.ExtraField field = f as TileProvider.ExtraField;
                        if (field != null && field.token == token) return field.value;
                    }
                }

                if (provider.extraFields != null)
                {
                    foreach (TileProvider.IExtraField f in provider.extraFields)
                    {
                        TileProvider.ExtraField field = f as TileProvider.ExtraField;
                        if (field != null && field.token == token) return field.value;
                    }
                }

                return null;
            }
            set
            {
                foreach (TileProvider.IExtraField f in extraFields)
                {
                    TileProvider.ExtraField field = f as TileProvider.ExtraField;
                    if (field != null && field.token == token)
                    {
                        field.value = value;
                        return;
                    }
                }

                foreach (TileProvider.IExtraField f in provider.extraFields)
                {
                    TileProvider.ExtraField field = f as TileProvider.ExtraField;
                    if (field != null && field.token == token)
                    {
                        field.value = value;
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="title">Human-readable map type title.</param>
        public MapType(string title) : this(title.ToLower(), title)
        {
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="id">ID of map type.</param>
        /// <param name="title">Human-readable map type title.</param>
        public MapType(string id, string title)
        {
            this.id = id;
            this.title = title;
        }

        public string GetSettings()
        {
            if (provider.extraFields == null && extraFields == null) return null;

            StringBuilder builder = new StringBuilder();
            if (extraFields != null)
            {
                foreach (TileProvider.IExtraField field in extraFields)
                {
                    field.SaveSettings(builder);
                }
            }
            
            if (provider.extraFields != null)
            {
                foreach (TileProvider.IExtraField field in provider.extraFields)
                {
                    field.SaveSettings(builder);
                }
                
            }
            return builder.ToString();
        }

        /// <summary>
        /// Gets the URL to download the tile texture
        /// </summary>
        /// <param name="tile">Instance of tile</param>
        /// <returns>URL to tile texture</returns>
        public string GetURL(Tile tile)
        {
            RasterTile rTile = tile as RasterTile;
            bool useLabels = hasLabels ? rTile.labels : labelsEnabled;
            if (useLabels)
            {
                if (!string.IsNullOrEmpty(_urlWithLabels)) return GetURL(tile, _urlWithLabels, true);
                if (!string.IsNullOrEmpty(provider.url)) return GetURL(tile, provider.url, true);
                return GetURL(tile, _urlWithoutLabels, false);
            }

            if (!string.IsNullOrEmpty(_urlWithoutLabels)) return GetURL(tile, _urlWithoutLabels, false);
            if (!string.IsNullOrEmpty(provider.url)) return GetURL(tile, provider.url, false);
            return GetURL(tile, _urlWithLabels, true);
        }

        private string GetURL(Tile tile, string url, bool labels)
        {
            url = Regex.Replace(url, @"{\w+}", delegate(Match match)
            {
                string v = match.Value.ToLower().Trim('{', '}');

                if (Tile.OnReplaceURLToken != null)
                {
                    string ret = Tile.OnReplaceURLToken(tile, v);
                    if (ret != null) return ret;
                }

                if (v == "zoom") return tile.zoom.ToString();
                if (v == "z") return tile.zoom.ToString();
                if (v == "x") return tile.x.ToString();
                if (v == "y") return tile.y.ToString();
                if (v == "quad") return Utils.TileToQuadKey(tile.x, tile.y, tile.zoom);
                if (v == "lng") return (tile as RasterTile).language;
                if (v == "ext") return ext;
                if (v == "prop") return labels ? propWithLabels : propWithoutLabels;
                if (v == "prop2") return prop2;
                if (v == "variant") return labels ? variantWithLabels : variantWithoutLabels;
                if (TryUseExtraFields(ref v)) return v;
                return v;
            });
            url = Regex.Replace(url, @"{rnd(\d+)-(\d+)}", delegate(Match match)
            {
                int v1 = int.Parse(match.Groups[1].Value);
                int v2 = int.Parse(match.Groups[2].Value);
                return Random.Range(v1, v2 + 1).ToString();
            });
            if (logUrl) Debug.Log(url);
            return url;
        }

        public void LoadSettings(string settings)
        {
            if (string.IsNullOrEmpty(settings)) return;

            TryLoadExtraFields(settings, extraFields);
            TryLoadExtraFields(settings, provider.extraFields);
        }
        
        /// <summary>
        /// Sets the value of an extra field by its title.
        /// </summary>
        /// <param name="title">The title of the extra field.</param>
        /// <param name="value">The value to set for the extra field.</param>
        /// <returns>True if the field was found and the value was set; otherwise, false.</returns>
        public bool SetExtraFieldValue(string title, string value)
        {
            if (extraFields == null) return false;
        
            foreach (TileProvider.IExtraField field in extraFields)
            {
                TileProvider.ExtraField ef = field as TileProvider.ExtraField;
                if (ef == null || ef.title != title) continue;
                ef.value = value;
                return true;
            }
        
            return false;
        }

        public override string ToString()
        {
            return fullID;
        }

        private void TryLoadExtraFields(string settings, TileProvider.IExtraField[] fields)
        {
            if (fields == null) return;

            int i = 0;
            while (i < settings.Length)
            {
                int titleLength = int.Parse(settings.Substring(i, 2));
                i += 2;
                string title = settings.Substring(i, titleLength);
                i += titleLength;

                int contentLengthSize = int.Parse(settings.Substring(i, 1));
                i++;
                int contentSize = int.Parse(settings.Substring(i, contentLengthSize));
                i += contentLengthSize;

                foreach (TileProvider.IExtraField field in fields)
                {
                    if (field.TryLoadSettings(title, settings, i, contentSize))
                    {
                        break;
                    }
                }
                i += contentSize;
            }
        }

        public bool TryUseExtraFields(ref string token)
        {
            if (extraFields != null)
            {
                foreach (TileProvider.IExtraField field in extraFields)
                {
                    string value;
                    if (field.GetTokenValue(token, false, out value))
                    {
                        token = value;
                        return true;
                    }
                }
            }

            if (provider.extraFields != null)
            {
                foreach (TileProvider.IExtraField field in provider.extraFields)
                {
                    string value;
                    if (field.GetTokenValue(token, false, out value))
                    {
                        token = value;
                        return true;
                    }
                }
            }

            if (provider.OnReplaceToken != null)
            {
                string value = provider.OnReplaceToken(this, token);
                if (value != null)
                {
                    token = value;
                    return true;
                }
            }

            return false;
        }
    }
}