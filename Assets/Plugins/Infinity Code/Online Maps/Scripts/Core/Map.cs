/*         INFINITY CODE         */
/*   https://infinity-code.com   */

#if !UNITY_WEBGL
using System.Threading;
#endif

using System;
using System.Linq;
using System.Reflection;
using OnlineMaps.Webservices;
using UnityEngine;

namespace OnlineMaps
{
    /// <summary>
    /// The main class. With it, you can control the map.
    /// </summary>
    [AddComponentMenu("Infinity Code/Online Maps/Map")]
    [Serializable]
    public class Map : MonoBehaviour, ISerializationCallbackReceiver, ISavableAdvanced
    {
        /// <summary>
        /// The current version of Online Maps
        /// </summary>
        public const string version = "4.2.1.1";

        #region Actions

        /// <summary>
        /// The event is called when the map starts.
        /// </summary>
        public static Action<Map> OnStart;

        /// <summary>
        /// Event caused at the end of OnGUI method
        /// </summary>
        public Action OnGUIAfter;

        /// <summary>
        /// Event caused at the beginning of OnGUI method
        /// </summary>
        public Action OnGUIBefore;

        /// <summary>
        /// The event is invoked at the end LateUpdate.
        /// </summary>
        public Action OnLateUpdateAfter;

        /// <summary>
        /// The event is called at the start LateUpdate.
        /// </summary>
        public Action OnLateUpdateBefore;

        /// <summary>
        /// Event caused when the user change map position.
        /// </summary>
        public Action OnLocationChanged;

        /// <summary>
        /// Event which is called after the redrawing of the map.
        /// </summary>
        public Action OnMapUpdated;

        /// <summary>
        /// Event is called before Update.
        /// </summary>
        public Action OnUpdateBefore;

        /// <summary>
        /// Event is called after Update.
        /// </summary>
        public Action OnUpdateLate;

        /// <summary>
        /// Event caused when the user change map zoom.
        /// </summary>
        public Action OnZoomChanged;

        #endregion

        #region Fields

        private static Map _instance;

        /// <summary>
        /// Allows drawing of map.
        /// Important: The interaction with the map, add or remove markers and drawing elements, automatically allowed to redraw the map.
        /// Use lockRedraw, to prohibit the redrawing of the map.
        /// </summary>
        public bool allowRedraw;

        /// <summary>
        /// Allows you to block all user interactions with the map, markers, drawing elements. But you can still interact with the map using the API.
        /// </summary>
        public bool blockAllInteractions;

        /// <summary>
        /// Tiles for the specified number of parent levels will be loaded.
        /// </summary>
        [Range(0, 20)]
        [Tooltip("Tiles for the specified number of parent levels will be loaded")]
        public int countParentLevels = 5;

        /// <summary>
        /// URL of custom provider.
        /// Support tokens:
        /// {x} - tile x
        /// {y} - tile y
        /// {zoom} - zoom level
        /// {quad} - uniquely identifies a single tile at a particular level of detail.
        /// </summary>
        public string customProviderURL = "http://localhost/{zoom}/{y}/{x}";

        /// <summary>
        /// URL of custom traffic provider.
        /// Support tokens:
        /// {x} - tile x
        /// {y} - tile y
        /// {zoom} - zoom level
        /// {quad} - uniquely identifies a single tile at a particular level of detail.
        /// </summary>
        public string customTrafficProviderURL = "http://localhost/{zoom}/{y}/{x}";

        /// <summary>
        /// Texture displayed until the tile is not loaded.
        /// </summary>
        [Tooltip("The texture that will be displayed until the tile is loaded")]
        public Texture2D defaultTileTexture;

        /// <summary>
        /// Specifies whether to dispatch the event.
        /// </summary>
        public bool dispatchEvents = true;

        /// <summary>
        /// Color, which is used until the tile is not loaded, unless specified field defaultTileTexture.
        /// </summary>
        [Tooltip("The color that will be displayed until the tile is loaded.\nImportant: if Default Tile Texture is specified, this value will be ignored.")]
        public Color emptyColor = Color.gray;

        /// <summary>
        /// Specifies whether to display the labels on the map.
        /// </summary>
        public bool labels = true;

        /// <summary>
        /// Language of the labels on the map.
        /// </summary>
        public string language = "en";

        /// <summary>
        /// A flag that indicates that need to redraw the map.
        /// </summary>
        public bool needRedraw;

        /// <summary>
        /// Not interact under the GUI.
        /// </summary>
        [Tooltip("Should Online Maps ignore clicks if an IMGUI or uGUI element is under the cursor?")]
        public bool notInteractUnderGUI = true;

        /// <summary>
        /// ID of current map type.
        /// </summary>
        public string mapType;

        /// <summary>
        /// Server for requests to the Open Street Map Overpass API.
        /// </summary>
        public OSMOverpassServer osmServer = OSMOverpassServer.main;

        /// <summary>
        /// A flag that indicates whether to redraw the map at startup.
        /// </summary>
        [Tooltip("Redraw the map immediately after the start of the scene")]
        public bool redrawOnPlay;

        /// <summary>
        /// Render map in a separate thread. Recommended.
        /// </summary>
        [Tooltip("If you have any problems with multithreading, disable this field.")]
        public bool renderInThread = true;

        /// <summary>
        /// Template path in Resources, from where the tiles will be loaded. This field supports tokens.
        /// </summary>
        public string resourcesPath = "OnlineMapsTiles/{zoom}/{x}/{y}";

        /// <summary>
        /// Template path in Streaming Assets, from where the tiles will be loaded. This field supports tokens.
        /// </summary>
        public string streamingAssetsPath = "OnlineMapsTiles/{zoom}/{x}/{y}.png";

        /// <summary>
        /// Indicates when the marker will show tips.
        /// </summary>
        [Tooltip("Tooltip display rule")]
        public ShowMarkerTooltip showMarkerTooltip = ShowMarkerTooltip.onHover;

        /// <summary>
        /// Specifies from where the tiles should be loaded (Online, Resources, Online and Resources).
        /// </summary>
        [Tooltip("Source of tiles")]
        public MapSource source = MapSource.Online;

        /// <summary>
        /// Indicates that Unity need to stop playing when compiling scripts.
        /// </summary>
        [Tooltip("Should Online Maps stop playing when recompiling scripts?")]
        public bool stopPlayingWhenScriptsCompile = true;

        /// <summary>
        /// Background texture of tooltip
        /// </summary>
        [Tooltip("Tooltip background texture")]
        public Texture2D tooltipBackgroundTexture;

        /// <summary>
        /// Specifies whether to draw traffic
        /// </summary>
        [Tooltip("Display traffic jams")]
        public bool traffic;

        /// <summary>
        /// ID of current traffic provider
        /// </summary>
        public string trafficProviderID = "googlemaps";

        /// <summary>
        /// Specifies is necessary to use software JPEG decoder.
        /// Use only if you have problems with hardware decoding of JPEG.
        /// </summary>
        [Tooltip("If you have problems decoding JPEG images, use software decoder.\nKeep in mind that this greatly affects performance.")]
        public bool useSoftwareJPEGDecoder;

        #endregion

        #region Private Fields

        [SerializeField]
        private string _activeTypeSettings;

        private Buffer _buffer;

        [SerializeField]
        private MapView _view = new MapView();

#if NETFX_CORE
        private ThreadWINRT renderThread;
#elif !UNITY_WEBGL
        private Thread renderThread;
#endif

        private MapType _activeType;
        private ControlBase _control;
        private ControlBase3D _control3D;
        private bool _labels;
        private string _language;
        private string _mapType;
        private bool _traffic;
        private string _trafficProviderID;
        private LimitedRange _zoomRange;
        private SavableItem[] savableItems;

        #endregion

        #region Properties

        /// <summary>
        /// Singleton instance of map.
        /// </summary>
        public static Map instance => _instance;

        /// <summary>
        /// Active type of map.
        /// </summary>
        public MapType activeType
        {
            get
            {
                if (_activeType != null && _activeType.fullID == mapType) return _activeType;

                _activeType = TileProvider.FindMapType(mapType) ?? TileProvider.firstMapType;
                view.projection = _activeType.provider.projection;
                mapType = _activeType.fullID;
                return _activeType;
            }
            set
            {
                if (_activeType == value) return;

                _activeType = value;
                view.projection = _activeType.provider.projection;
                _mapType = mapType = value.fullID;

                if (Application.isPlaying) RedrawImmediately();
            }
        }

        /// <summary>
        /// Reference to the current draw buffer.
        /// </summary>
        public Buffer buffer
        {
            get
            {
                if (_buffer == null) _buffer = new Buffer(this);
                return _buffer;
            }
        }

        /// <summary>
        /// The current state of the drawing buffer.
        /// </summary>
        public BufferStatus bufferStatus => buffer.status;

        /// <summary>
        /// Gets the current control.
        /// </summary>
        public ControlBase control
        {
            get
            {
                if (!_control) _control = GetComponent<ControlBase>();
                return _control;
            }
        }

        /// <summary>
        /// Gets the current 3D control.
        /// </summary>
        public ControlBase3D control3D
        {
            get
            {
                if (!_control3D) _control3D = GetComponent<ControlBase3D>();
                return _control3D;
            }
        }

        /// <summary>
        /// Gets the default colors of the map.
        /// </summary>
        public Color[] defaultColors { get; private set; }

        /// <summary>
        /// Gets the drawing element manager from control.
        /// </summary>
        public DrawingElementManager drawingElementManager => control.drawingElementManager;

        /// <summary>
        /// Gets or sets the geographical coordinates of the map's center.
        /// </summary>
        public GeoPoint location
        {
            get => view.center;
            set => view.center = value;
        }

        /// <summary>
        /// Prohibits drawing of maps.
        /// Important: Do not forget to disable this restriction. Otherwise, the map will never be redrawn.
        /// </summary>
        public bool lockRedraw { get; set; }

        /// <summary>
        /// Gets the markers manager from control.
        /// </summary>
        public Marker2DManager marker2DManager => control.marker2DManager;

        /// <summary>
        /// Gets the 3D markers manager from control.
        /// </summary>
        public Marker3DManager marker3DManager => control3D ? control3D.marker3DManager : null;

        /// <summary>
        /// Reference to tile manager
        /// </summary>
        public TileManager tileManager { get; private set; }

        /// <summary>
        /// Reference to tooltip drawer
        /// </summary>
        public TooltipDrawerBase tooltipDrawer { get; set; }

        /// <summary>
        /// Provider of traffic jams
        /// </summary>
        public TrafficProvider trafficProvider { get; set; }


        /// <summary>
        /// Gets the current view of the map.
        /// </summary>
        public MapView view => _view;

        #endregion

        #region Methods

        public void Awake()
        {
            _instance = this;
            tileManager = new TileManager(this);

            if (!control)
            {
                Debug.LogError("Can not find a Control.");
                return;
            }

            view.Init(this);
            view.SetSize(control.width, control.height);
            view.UpdateBounds();
            control.OnAwakeBefore();

            ITextureControl textureControl = control as ITextureControl;
            if (textureControl != null)
            {
                if (textureControl.texture) defaultColors = textureControl.texture.GetPixels();

                if (!defaultTileTexture)
                {
                    RasterTile.defaultColors = new Color32[Constants.SqrTileSize];
                    for (int i = 0; i < Constants.SqrTileSize; i++) RasterTile.defaultColors[i] = emptyColor;
                }
                else RasterTile.defaultColors = defaultTileTexture.GetPixels32();
            }
        }

        private void CheckBaseProps()
        {
            if (mapType != _mapType)
            {
                activeType = TileProvider.FindMapType(mapType);
                _mapType = mapType = activeType.fullID;
                _buffer?.UnloadOldTypes();
                Redraw();
            }

            CheckLanguageAndLabelProps();
            CheckTrafficProps();
        }

        private void CheckLanguageAndLabelProps()
        {
            if (_language == language && _labels == labels) return;

            _labels = labels;
            _language = language;

            if (_buffer != null)
            {
                _buffer.Dispose();
                _buffer = null;
#if NETFX_CORE
                if (renderThread != null) renderThread.Dispose();
#endif
#if !UNITY_WEBGL
                renderThread = null;
#endif
            }

            Redraw();
        }

        private void CheckBufferComplete()
        {
            if (buffer.status != BufferStatus.complete) return;
            if (buffer.needUnloadTiles) buffer.UnloadOldTiles();

            tileManager.UnloadUnusedTiles();

            if (allowRedraw) UpdateControl();
            buffer.status = BufferStatus.wait;
        }

        /// <summary>
        /// Checks the map size and adjusts the zoom level if necessary.
        /// </summary>
        /// <param name="z">The current zoom level.</param>
        /// <returns>The adjusted zoom level.</returns>
        public float CheckMapSize(float z)
        {
            int iz = (int)z;
            long max = (1L << iz) * Constants.TileSize;
            if (max < control.width || max < control.height) return CheckMapSize(iz + 1);

            return z;
        }

        private void CheckTrafficProps()
        {
            if (traffic == _traffic && trafficProviderID == _trafficProviderID) return;

            _traffic = traffic;

            _trafficProviderID = trafficProviderID;
            trafficProvider = TrafficProvider.GetByID(trafficProviderID);

            Tile[] tiles;
            lock (Tile.lockTiles)
            {
                tiles = tileManager.tiles.ToArray();
            }

            if (traffic)
            {
                foreach (Tile tile in tiles)
                {
                    RasterTile rTile = tile as RasterTile;
                    rTile.trafficProvider = trafficProvider;
                    rTile.trafficWWW = new WebRequest(rTile.trafficURL);
                    rTile.trafficWWW["tile"] = tile;
                    rTile.trafficWWW.OnComplete += TileManager.OnTrafficWWWComplete;
                    if (rTile.trafficTexture != null)
                    {
                        Utils.Destroy(rTile.trafficTexture);
                        rTile.trafficTexture = null;
                    }

                    rTile.mergedColors = null;
                }
            }
            else
            {
                foreach (Tile tile in tiles)
                {
                    RasterTile rTile = tile as RasterTile;
                    if (rTile.trafficTexture != null)
                    {
                        Utils.Destroy(rTile.trafficTexture);
                        rTile.trafficTexture = null;
                    }

                    rTile.trafficWWW = null;
                    rTile.mergedColors = null;
                }
            }

            Redraw();
        }

        /// <summary>
        /// Dispatch map events.
        /// </summary>
        /// <param name="evs">Events you want to dispatch.</param>
        public void DispatchEvent(params Events[] evs)
        {
            if (!dispatchEvents) return;

            foreach (Events ev in evs)
            {
                if (ev == Events.changedPosition && OnLocationChanged != null) OnLocationChanged();
                else if (ev == Events.changedZoom && OnZoomChanged != null) OnZoomChanged();
            }
        }

        /// <summary>
        /// Gets drawing element from screen.
        /// </summary>
        /// <param name="screenPosition">Screen position.</param>
        /// <returns>Drawing element</returns>
        public DrawingElement GetDrawingElement(Vector2 screenPosition)
        {
            GeoPoint l = control.ScreenToLocation(screenPosition);
            return control.drawingElementManager.LastOrDefault(el => el.HitTest(l, view.intZoom));
        }

        public SavableItem[] GetSavableItems()
        {
            if (savableItems != null) return savableItems;

            savableItems = new[]
            {
                new SavableItem("map", "Map settings", SaveSettings)
                {
                    priority = 100,
                    loadCallback = Load
                }
            };

            return savableItems;
        }

        private void LateUpdate()
        {
            if (OnLateUpdateBefore != null) OnLateUpdateBefore();

            if (!control || lockRedraw) return;
            StartBuffer();
            CheckBufferComplete();

            if (OnLateUpdateAfter != null) OnLateUpdateAfter();
        }

        public void Load(JSONItem json)
        {
            (json as JSONObject).DeserializeObject(this, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            view.SetCenter(
                json.V<float>("longitude"),
                json.V<float>("latitude"),
                json.V<float>("zoom"));
        }

        public void OnAfterDeserialize()
        {
            try
            {
                activeType.LoadSettings(_activeTypeSettings);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(exception.Message + "\n" + exception.StackTrace);
            }
        }

        public void OnBeforeSerialize()
        {
            _activeTypeSettings = activeType.GetSettings();
        }

        private void OnDestroy()
        {
            ThreadManager.Dispose();

            if (_buffer != null)
            {
                _buffer.Dispose();
                _buffer = null;
            }
#if NETFX_CORE
            if (renderThread != null) renderThread.Dispose();
#endif
#if !UNITY_WEBGL
            renderThread = null;
#endif
            if (tileManager != null)
            {
                tileManager.Dispose();
                tileManager = null;
            }

            _control = null;

            if (defaultColors != null)
            {
                ITextureControl textureControl = control as ITextureControl;
                textureControl?.SetDefaultTexture();
                Texture2D texture = textureControl?.texture;
                if (texture)
                {
                    if (texture.width * texture.height == defaultColors.Length)
                    {
                        texture.SetPixels(defaultColors);
                        texture.Apply();
                    }
                }
            }

            OnLocationChanged = null;
            OnZoomChanged = null;
            OnMapUpdated = null;
            OnUpdateBefore = null;
            OnUpdateLate = null;
        }

        private void OnDisable()
        {
            ThreadManager.Dispose();

            if (_buffer != null)
            {
                _buffer.Dispose();
                _buffer = null;
            }

#if NETFX_CORE
            if (renderThread != null) renderThread.Dispose();
#endif
#if !UNITY_WEBGL
            renderThread = null;
#endif

            _control = null;

            if (_instance == this) _instance = null;
        }

        private void OnEnable()
        {
            Utils.isPlaying = true;
            _instance = this;

            tooltipDrawer = new GUITooltipDrawer(this);

            activeType = TileProvider.FindMapType(mapType);
            _mapType = mapType = activeType.fullID;
            if (tileManager == null) tileManager = new TileManager(this);

            trafficProvider = TrafficProvider.GetByID(trafficProviderID);

            if (language == "") language = activeType.provider.twoLetterLanguage ? "en" : "eng";

            _language = language;
            _labels = labels;
            _traffic = traffic;
            _trafficProviderID = trafficProviderID;

            OSMOverpassRequest.InitOSMServer(osmServer);

            view.UpdateBounds();
        }

#if !ONLINEMAPS_NOGUI
        private void OnGUI()
        {
            if (OnGUIBefore != null) OnGUIBefore();
            if (OnGUIAfter != null) OnGUIAfter();
        }

#endif

        /// <summary>
        /// Full redraw map.
        /// </summary>
        public void Redraw()
        {
            needRedraw = true;
            allowRedraw = true;
        }

        /// <summary>
        /// Stops the current process map generation, clears all buffers and completely redraws the map.
        /// </summary>
        public void RedrawImmediately()
        {
            ThreadManager.Dispose();

            if (renderInThread)
            {
                if (_buffer != null)
                {
                    _buffer.Dispose();
                    _buffer = null;
                }

#if NETFX_CORE
                if (renderThread != null) renderThread.Dispose();
#endif
#if !UNITY_WEBGL
                renderThread = null;
#endif
            }
            else
            {
                buffer.status = BufferStatus.wait;
                StartBuffer();
            }

            Redraw();
        }

        private JSONItem SaveSettings()
        {
            JSONObject json = JSON.Serialize(new
            {
                view.longitude,
                view.latitude,
                view.zoom,
                source,
                mapType,
                labels,
                traffic,
                redrawOnPlay,
                emptyColor,
                defaultTileTexture,
                tooltipBackgroundTexture,
                showMarkerTooltip,
                useSoftwareJPEGDecoder,
                countParentLevels
            }) as JSONObject;

            if (activeType.isCustom) json.Add("customProviderURL", customProviderURL);

            return json;
        }

        private void Start()
        {
            if (OnStart != null) OnStart(this);
            if (redrawOnPlay) allowRedraw = true;
            needRedraw = true;
        }

        private void StartBuffer()
        {
            if (!allowRedraw || !needRedraw) return;
            if (buffer.status != BufferStatus.wait) return;

            buffer.status = BufferStatus.start;

            if (!control.resultIsTexture) renderInThread = false;

#if !UNITY_WEBGL
            if (renderInThread)
            {
                if (renderThread == null)
                {
#if NETFX_CORE
                    renderThread = new ThreadWINRT(buffer.GenerateFrontBuffer);
#else
                    renderThread = new Thread(buffer.GenerateFrontBuffer);
#endif
                    renderThread.Start();
                }
            }
            else buffer.GenerateFrontBuffer();
#else
            buffer.GenerateFrontBuffer();
#endif

            needRedraw = false;
        }

        private void Update()
        {
            ThreadManager.ExecuteMainThreadActions();

            if (OnUpdateBefore != null) OnUpdateBefore();

            CheckBaseProps();
            tileManager.StartDownloading();

            if (OnUpdateLate != null) OnUpdateLate();
        }

        private void UpdateControl()
        {
            TileManager.OnPreloadTiles?.Invoke(tileManager);
            control.UpdateControl();

            if (OnMapUpdated != null) OnMapUpdated();
        }

        #endregion
    }
}