/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using System.Linq;
using System.Text;
using UnityEngine;

namespace OnlineMaps
{
    /// <summary>
    /// Provider of tiles.
    /// </summary>
    public class TileProvider
    {
        /// <summary>
        /// Allows to replace tokens in the URL.
        /// </summary>
        public Func<MapType, string, string> OnReplaceToken;

        private static TileProvider[] _providers;

        /// <summary>
        /// ID of provider
        /// </summary>
        public readonly string id;

        /// <summary>
        /// Human-readable provider title.
        /// </summary>
        public readonly string title;

        /// <summary>
        /// Indicates that the provider supports multilanguage.
        /// </summary>
        public bool? hasLanguage;

        /// <summary>
        /// Indicates that the provider supports a map with labels.
        /// </summary>
        public bool? hasLabels;

        /// <summary>
        /// Indicates that the label is always enabled.
        /// </summary>
        public bool? labelsEnabled;

        /// <summary>
        /// Map projection.
        /// </summary>
        public Projection projection;

        /// <summary>
        /// Indicates that the provider uses HTTP.
        /// </summary>
        public bool? useHTTP;

        /// <summary>
        /// Index of current provider.
        /// </summary>
        public int index;

        /// <summary>
        /// Extension. Token {ext}, that is being replaced in the URL.
        /// </summary>
        public string ext;

        /// <summary>
        /// Property. Token {prop}, that is being replaced in the URL.
        /// </summary>
        public string prop;

        /// <summary>
        /// Property. Token {prop2}, that is being replaced in the URL.
        /// </summary>
        public string prop2;

        /// <summary>
        /// Indicates that the provider uses two letter language code.
        /// </summary>
        public bool twoLetterLanguage = true;

        /// <summary>
        /// Indicates whether to log the URL.
        /// </summary>
        public bool logUrl = false;

        /// <summary>
        /// Array of extra fields for the tile provider.
        /// </summary>
        public IExtraField[] extraFields;

        private string _url;

        /// <summary>
        /// Array of map types available for the current provider.
        /// </summary>
        public MapType[] types;

        public static MapType firstMapType => providers[0].types[0];

        public static TileProvider[] providers
        {
            get
            {
                if (_providers != null) return _providers;
                
                _providers = TileSources.providers;

                for (int i = 0; i < _providers.Length; i++)
                {
                    TileProvider provider = _providers[i];
                    provider.index = i;
                    for (int j = 0; j < provider.types.Length; j++)
                    {
                        MapType type = provider.types[j];
                        type.provider = provider;
                        type.fullID = provider.id + "." + type.id;
                        type.index = j;
                    }
                }
                
                return _providers;
            }
        }

        /// <summary>
        /// Gets / sets the URL pattern of tiles.
        /// </summary>
        public string url
        {
            get => _url;
            set
            {
                _url = value;
                if (!value.StartsWith("https")) useHTTP = true;
            }
        }

        public TileProvider(string title) : this(title.ToLower(), title)
        {
        }

        public TileProvider(string id, string title)
        {
            this.id = id.ToLower();
            this.title = title;
            projection = new SphericalMercator();
        }

        /// <summary>
        /// Appends map types to the provider.
        /// </summary>
        /// <param name="newTypes">Map types</param>
        public void AppendTypes(params MapType[] newTypes)
        {
            int l = types.Length;
            Array.Resize(ref types, l + newTypes.Length);
            for (int i = 0; i < newTypes.Length; i++)
            {
                MapType type = types[l + i] = newTypes[i];
                type.provider = this;
                type.index = l + i;
                type.fullID = id + "." + type.id;
            }
        }

        /// <summary>
        /// Creates a new map type with the specified title.
        /// </summary>
        /// <param name="title">Title of the map type. The ID of the map type is derived from the title by converting it to lowercase.</param>
        /// <returns>A new instance of the map type.</returns>
        public MapType CreateType(string title)
        {
            MapType type = new MapType(title.ToLower(), title);
            AppendTypes(type);
            return type;
        }

        /// <summary>
        /// Creates a new map type with the specified ID and title.
        /// </summary>
        /// <param name="id">ID of the map type.</param>
        /// <param name="title">Title of the map type.</param>
        /// <returns>A new instance of the map type.</returns>
        public MapType CreateType(string id, string title)
        {
            MapType type = new MapType(id, title);
            AppendTypes(type);
            return type;
        }

        /// <summary>
        /// Creates a new provider, with the specified title.
        /// </summary>
        /// <param name="title">Provider title. Provider id = title.ToLower().</param>
        /// <returns>Instance of provider.</returns>
        public static TileProvider Create(string title)
        {
            TileProvider provider = new TileProvider(title)
            {
                types = Array.Empty<MapType>(),
                index = providers.Length
            };
            
            Array.Resize(ref _providers, _providers.Length + 1);
            _providers[_providers.Length - 1] = provider;
            return provider;
        }

        /// <summary>
        /// Creates a new map type, with the specified id.
        /// </summary>
        /// <param name="id">Map type ID. Format: providerID.mapTypeID</param>
        /// <returns>Instance of map type.</returns>
        public static MapType CreateMapType(string id)
        {
            string[] parts = id.Split('.');
            if (parts.Length != 2) throw new Exception($"Invalid map type ID: {id}. Expected format: providerID.mapTypeID");
            TileProvider provider = Get(parts[0]) ?? Create(parts[0]);
            if (provider.types.Any(t => t.id == parts[1])) throw new Exception($"Map type with ID {id} already exists.");
            MapType type = new MapType(parts[1], parts[1]);
            provider.AppendTypes(type);
            return type;
        }

        /// <summary>
        /// Creates a new map type, with the specified id.
        /// </summary>
        /// <param name="id">Map type ID. Format: providerID.mapTypeID</param>
        /// <param name="url">URL of the map type.</param>
        /// <returns>Instance of map type.</returns>
        public static MapType CreateMapType(string id, string url)
        {
            MapType type = CreateMapType(id);
            type.urlWithLabels = url;
            return type;
        }

        /// <summary>
        /// Gets an instance of a map type by ID.<br/>
        /// ID - providerID or providerID(dot)typeID.<br/>
        /// If the typeID is not specified returns the first map type of provider.<br/>
        /// If the provider ID is not found, returns the first map type of the first provider.<br/>
        /// Example: nokia or google.satellite
        /// </summary>
        /// <param name="mapTypeID">Map type ID.</param>
        /// <returns>Instance of map type</returns>
        public static MapType FindMapType(string mapTypeID)
        {
            if (string.IsNullOrEmpty(mapTypeID)) return firstMapType;

            int dotIndex = mapTypeID.IndexOf('.');
            TileProvider[] ps = providers;

            if (dotIndex == -1)
            {
                TileProvider p = ps.FirstOrDefault(p => p.id == mapTypeID);
                return p != null ? p.types[0] : firstMapType;
            }
            
            string providerID = mapTypeID.Substring(0, dotIndex);
            TileProvider provider = ps.FirstOrDefault(p => p.id == providerID);
            if (provider == null) return firstMapType;
            
            string typeID = mapTypeID.Substring(dotIndex + 1);
            MapType type = provider.types.FirstOrDefault(t => t.id == typeID);
            return type ?? provider.types[0];
        }

        /// <summary>
        /// Get provider by ID.
        /// </summary>
        /// <param name="id">Provider ID</param>
        /// <returns>Provider or null</returns>
        public static TileProvider Get(string id)
        {
            return _providers.FirstOrDefault(provider => provider.id == id);
        }

        /// <summary>
        /// Gets map type by index.
        /// </summary>
        /// <param name="index">Index of map type.</param>
        /// <param name="repeat">TRUE - Repeat index value, FALSE - Clamp index value.</param>
        /// <returns>Instance of map type.</returns>
        public MapType GetByIndex(int index, bool repeat = false)
        {
            if (repeat) index = Mathf.RoundToInt(Mathf.Repeat(index, types.Length - 1));
            else index = Mathf.Clamp(index, 0, types.Length);
            return types[index];
        }

        /// <summary>
        /// Gets array of provider titles.
        /// </summary>
        /// <returns>Array of provider titles</returns>
        public static string[] GetProvidersTitle()
        {
            return _providers.Select(p => p.title).ToArray();
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
        
            foreach (IExtraField field in extraFields)
            {
                ExtraField ef = field as ExtraField;
                if (ef == null || ef.title != title) continue;
                ef.value = value;
                return true;
            }
        
            return false;
        }

        /// <summary>
        /// Interface for extra fields tile provider
        /// </summary>
        public interface IExtraField
        {
            /// <summary>
            /// Gets the token value.
            /// </summary>
            /// <param name="token">The token to get the value for.</param>
            /// <param name="useDefaultValue">Whether to use the default value.</param>
            /// <param name="value">The value of the token.</param>
            /// <returns>True if the token value was found, otherwise false.</returns>
            bool GetTokenValue(string token, bool useDefaultValue, out string value);

            /// <summary>
            /// Saves the settings to the provided StringBuilder.
            /// </summary>
            /// <param name="builder">The StringBuilder to save the settings to.</param>
            void SaveSettings(StringBuilder builder);

            /// <summary>
            /// Tries to load the settings from the provided string.
            /// </summary>
            /// <param name="title">The title of the settings.</param>
            /// <param name="settings">The settings string.</param>
            /// <param name="index">The index to start loading from.</param>
            /// <param name="contentSize">The size of the content to load.</param>
            /// <returns>True if the settings were successfully loaded, otherwise false.</returns>
            bool TryLoadSettings(string title, string settings, int index, int contentSize);
        }

        /// <summary>
        /// Class for extra field
        /// </summary>
        public class ExtraField : IExtraField
        {
            /// <summary>
            /// Title
            /// </summary>
            public string title;

            /// <summary>
            /// Value
            /// </summary>
            public string value;

            /// <summary>
            /// Default value
            /// </summary>
            public string defaultValue;

            /// <summary>
            /// Token (ID)
            /// </summary>
            public string token;

            /// <summary>
            /// Initializes a new instance of the ExtraField class with the specified title and token.
            /// </summary>
            /// <param name="title">The title of the extra field.</param>
            /// <param name="token">The token (ID) of the extra field.</param>
            public ExtraField(string title, string token)
            {
                this.title = title;
                this.token = token;
            }

            /// <summary>
            /// Initializes a new instance of the ExtraField class with the specified title, token, and default value.
            /// </summary>
            /// <param name="title">The title of the extra field.</param>
            /// <param name="token">The token (ID) of the extra field.</param>
            /// <param name="defaultValue">The default value of the extra field.</param>
            public ExtraField(string title, string token, string defaultValue) : this(title, token)
            {
                value = this.defaultValue = defaultValue;
            }

            /// <summary>
            /// Gets the token value.
            /// </summary>
            /// <param name="token">The token to get the value for.</param>
            /// <param name="useDefaultValue">Whether to use the default value.</param>
            /// <param name="value">The value of the token.</param>
            /// <returns>True if the token value was found, otherwise false.</returns>
            public bool GetTokenValue(string token, bool useDefaultValue, out string value)
            {
                value = null;

                if (this.token == token)
                {
                    value = useDefaultValue ? defaultValue : this.value;
                    return true;
                }

                return false;
            }

            public void SaveSettings(StringBuilder builder)
            {
                int titleLength = title.Length;
                if (titleLength < 10) builder.Append("0");
                builder.Append(titleLength);
                builder.Append(title);

                if (string.IsNullOrEmpty(value)) builder.Append(1).Append(1).Append(0);
                else
                {
                    StringBuilder dataBuilder = new StringBuilder();
                    int valueLength = value.Length;
                    dataBuilder.Append(valueLength.ToString().Length);
                    dataBuilder.Append(valueLength);
                    dataBuilder.Append(value);
                    builder.Append(dataBuilder.Length.ToString().Length);
                    builder.Append(dataBuilder.Length);
                    builder.Append(dataBuilder);
                }
            }

            /// <summary>
            /// Tries to load the settings from the provided string.
            /// </summary>
            /// <param name="title">The title of the settings.</param>
            /// <param name="settings">The settings string.</param>
            /// <param name="index">The index to start loading from.</param>
            /// <param name="contentSize">The size of the content to load.</param>
            /// <returns>True if the settings were successfully loaded, otherwise false.</returns>
            public bool TryLoadSettings(string title, string settings, int index, int contentSize)
            {
                if (this.title != title) return false;

                int lengthSize = int.Parse(settings.Substring(index, 1));
                if (lengthSize == 0) value = "";
                else
                {
                    index++;
                    int length = int.Parse(settings.Substring(index, lengthSize));
                    index += lengthSize;
                    value = settings.Substring(index, length);
                }

                return true;
            }
        }

        /// <summary>
        /// Group of toggle extra fields
        /// </summary>
        public class ToggleExtraGroup : IExtraField
        {
            /// <summary>
            /// Array of extra fields
            /// </summary>
            public IExtraField[] fields;

            /// <summary>
            /// Group title
            /// </summary>
            public string title;

            /// <summary>
            /// Group value
            /// </summary>
            public bool value;

            /// <summary>
            /// Group ID
            /// </summary>
            public string id;

            /// <summary>
            /// Initializes a new instance of the ToggleExtraGroup class with the specified title and value.
            /// </summary>
            /// <param name="title">The title of the group.</param>
            /// <param name="value">The value of the group.</param>
            public ToggleExtraGroup(string title, bool value = false)
            {
                this.title = title;
                this.value = value;
            }

            /// <summary>
            /// Initializes a new instance of the ToggleExtraGroup class with the specified title, value, and fields.
            /// </summary>
            /// <param name="title">The title of the group.</param>
            /// <param name="value">The value of the group.</param>
            /// <param name="fields">The array of extra fields.</param>
            public ToggleExtraGroup(string title, bool value, IExtraField[] fields) : this(title, value)
            {
                this.fields = fields;
            }

            /// <summary>
            /// Gets the token value.
            /// </summary>
            /// <param name="token">The token to get the value for.</param>
            /// <param name="useDefaultValue">Whether to use the default value.</param>
            /// <param name="value">The value of the token.</param>
            /// <returns>True if the token value was found, otherwise false.</returns>
            public bool GetTokenValue(string token, bool useDefaultValue, out string value)
            {
                value = null;
                if (fields == null) return false;

                foreach (IExtraField field in fields)
                {
                    if (field.GetTokenValue(token, this.value || useDefaultValue, out value)) return true;
                }

                return false;
            }

            public void SaveSettings(StringBuilder builder)
            {
                int titleLength = title.Length;
                if (titleLength < 10) builder.Append("0");
                builder.Append(titleLength);
                builder.Append(title);

                StringBuilder dataBuilder = new StringBuilder();
                dataBuilder.Append(value ? 1 : 0);

                if (fields != null)
                    foreach (IExtraField field in fields)
                        field.SaveSettings(dataBuilder);

                builder.Append(dataBuilder.Length.ToString().Length);
                builder.Append(dataBuilder.Length);
                builder.Append(dataBuilder);
            }

            public bool TryLoadSettings(string title, string settings, int index, int contentSize)
            {
                if (this.title != title) return false;

                value = settings.Substring(index, 1) == "1";

                int i = index + 1;
                while (i < index + contentSize)
                {
                    int titleLength = int.Parse(settings.Substring(i, 2));
                    i += 2;
                    string fieldTitle = settings.Substring(i, titleLength);
                    i += titleLength;

                    int contentLengthSize = int.Parse(settings.Substring(i, 1));
                    i++;
                    int contentLength = int.Parse(settings.Substring(i, contentLengthSize));
                    i += contentLengthSize;

                    foreach (IExtraField field in fields)
                        if (field.TryLoadSettings(fieldTitle, settings, i, contentLength))
                            break;

                    i += contentLength;
                }

                return true;
            }
        }

        /// <summary>
        /// Represents a label field for extra fields in a tile provider.
        /// </summary>
        public class LabelField : IExtraField
        {
            /// <summary>
            /// The label of the field.
            /// </summary>
            public string label;

            /// <summary>
            /// Initializes a new instance of the LabelField class with the specified label.
            /// </summary>
            /// <param name="label">The label of the field.</param>
            public LabelField(string label)
            {
                this.label = label;
            }

            /// <summary>
            /// Gets the token value.
            /// </summary>
            /// <param name="token">The token to get the value for.</param>
            /// <param name="useDefaultValue">Whether to use the default value.</param>
            /// <param name="value">The value of the token.</param>
            /// <returns>True if the token value was found, otherwise false.</returns>
            public bool GetTokenValue(string token, bool useDefaultValue, out string value)
            {
                value = null;
                return false;
            }

            public void SaveSettings(StringBuilder builder)
            {
            }

            public bool TryLoadSettings(string title, string settings, int index, int contentSize)
            {
                return false;
            }
        }
    }
}