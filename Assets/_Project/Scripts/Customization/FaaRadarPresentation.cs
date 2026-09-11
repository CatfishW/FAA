using FAA.XPlaneIntegration.Runtime;
using TMPro;
using TrafficRadar;
using TrafficRadar.Core;
using UnityEngine;
using UnityEngine.UI;
using WeatherRadar;

namespace FAA.Customization
{
    /// <summary>
    /// Shared instrument chrome. DISPLAY ON/OFF is local visibility, never a
    /// command to the simulator or a claim about TCAS/radar transmitter power.
    /// </summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class FaaRadarPresentation : MonoBehaviour
    {
        public const float HeaderClearance = 56f;
        public enum DataState { Preview, Live, Waiting, Stale, Standby, RadarOff, DisplayOff }

        [SerializeField] private FaaRadarKind radarKind;
        [SerializeField] private bool displayEnabled = true;
        [SerializeField] private XPlane12ApiHudBridge bridge;
        [SerializeField] private XPlaneOriginalWeatherRadarDisplay weatherDisplay;
        [SerializeField] private WeatherRadarDataProvider weatherData;
        [SerializeField] private TrafficRadarDisplay trafficDisplay;
        [SerializeField] private TrafficRadarController trafficController;
        private RectTransform _header, _footer, _messageRoot;
        private TMP_Text _title, _source, _switchLabel, _range, _detail, _message, _messageHint;
        private Image _switchBackground;
        private CanvasGroup _content;
        private float _nextRefresh;
        private float _visualAlpha = 1f;

        public bool IsDisplayOn => displayEnabled;
        public FaaRadarKind RadarKind => radarKind;
        public DataState CurrentState { get; private set; }
        public string StatusText => _source != null ? _source.text : string.Empty;

        private static readonly Color Text = new Color(.84f, .93f, .96f, 1f);
        private static readonly Color Muted = new Color(.52f, .66f, .71f, 1f);
        private static readonly Color Accent = new Color(.40f, .86f, .78f, 1f);
        private static readonly Color Caution = new Color(.98f, .73f, .34f, 1f);

        public void Configure(FaaRadarKind kind, XPlane12ApiHudBridge source)
        {
            radarKind = kind;
            bridge = source;
            ResolveReferences();
            EnsureChrome();
            RefreshPresentation();
        }

        private void OnEnable()
        {
            ResolveReferences();
            EnsureChrome();
            RefreshPresentation();
        }

        private void OnDisable()
        {
            if (_content != null) _content.alpha = 1f;
            if (trafficDisplay != null) trafficDisplay.InstrumentDisplayEnabled = true;
        }

        private void Update()
        {
            if (_header == null) EnsureChrome();
            if (!Application.isPlaying || Time.unscaledTime >= _nextRefresh)
            {
                _nextRefresh = Time.unscaledTime + .25f;
                RefreshPresentation();
            }
            float alpha = CurrentState == DataState.DisplayOff ? 0f :
                CurrentState == DataState.Live || CurrentState == DataState.Preview ? 1f : .12f;
            _visualAlpha = Application.isPlaying ? Mathf.MoveTowards(_visualAlpha, alpha, Time.unscaledDeltaTime * 6f) : alpha;
            if (_content != null) _content.alpha = _visualAlpha;
        }

        public void ToggleDisplay() => SetDisplayEnabled(!displayEnabled);

        public void SetDisplayEnabled(bool visible)
        {
            displayEnabled = visible;
            if (!visible && trafficDisplay != null) trafficDisplay.ClearNavigationPreview();
            RefreshPresentation();
        }

        public static DataState ResolveState(bool displayOn, bool playing, bool freshData,
            bool hasPreviousData, bool standby, bool knownRadarOff)
        {
            if (!displayOn) return DataState.DisplayOff;
            if (!playing) return DataState.Preview;
            // Stale aircraft power/mode must not masquerade as authoritative OFF/STBY.
            if (!freshData) return hasPreviousData ? DataState.Stale : DataState.Waiting;
            if (knownRadarOff) return DataState.RadarOff;
            if (standby) return DataState.Standby;
            return DataState.Live;
        }

        private void ResolveReferences()
        {
            if (radarKind == FaaRadarKind.Weather)
            {
                if (weatherDisplay == null) weatherDisplay = GetComponentInChildren<XPlaneOriginalWeatherRadarDisplay>(true);
                if (weatherData == null) weatherData = GetComponentInChildren<WeatherRadarDataProvider>(true);
                if (_content == null && weatherDisplay != null)
                    _content = EnsureContentGroup(weatherDisplay.gameObject);
            }
            else
            {
                if (trafficDisplay == null) trafficDisplay = GetComponentInChildren<TrafficRadarDisplay>(true);
                if (trafficController == null) trafficController = GetComponentInChildren<TrafficRadarController>(true);
                if (_content == null && trafficDisplay != null)
                    _content = EnsureContentGroup(trafficDisplay.gameObject);
            }
        }

        public static CanvasGroup EnsureContentGroup(GameObject content)
        {
            // Native Unity components can return a non-CLR-null missing-object
            // sentinel. Do not use ?? here: it can leave no actual CanvasGroup.
            if (!content.TryGetComponent<CanvasGroup>(out var group)) group = content.AddComponent<CanvasGroup>();
            return group;
        }

        private void EnsureChrome()
        {
            if (!(transform is RectTransform)) return;
            _header = Child(transform, "Radar Status Header");
            Image plate = _header.GetComponent<Image>() ?? _header.gameObject.AddComponent<Image>();
            FaaRadarVisualStyle.ApplyRounded(plate, new Color(.025f, .06f, .08f, .94f), 12);
            plate.raycastTarget = false;
            _title = Label(_header, "Instrument", 13f, TextAlignmentOptions.MidlineLeft);
            _source = Label(_header, "Data Status", 10f, TextAlignmentOptions.MidlineLeft);
            RectTransform toggleRect = Child(_header, "Display Toggle");
            _switchBackground = toggleRect.GetComponent<Image>() ?? toggleRect.gameObject.AddComponent<Image>();
            FaaRadarVisualStyle.ApplyRounded(_switchBackground, new Color(.04f, .17f, .17f, 1f), 8);
            _switchBackground.raycastTarget = true;
            Button button = toggleRect.GetComponent<Button>() ?? toggleRect.gameObject.AddComponent<Button>();
            button.onClick.RemoveListener(ToggleDisplay);
            button.onClick.AddListener(ToggleDisplay);
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            FaaRadarVisualStyle.ConfigureButton(button, _switchBackground);
            Label(toggleRect, "Display Caption", 8f, TextAlignmentOptions.Center).text = "DISPLAY";
            _switchLabel = Label(toggleRect, "State", 12f, TextAlignmentOptions.Center);
            _footer = Child(transform, "Radar Status Footer");
            _range = Label(_footer, "Range", 12f, TextAlignmentOptions.MidlineLeft);
            _detail = Label(_footer, "Mode", 10f, TextAlignmentOptions.MidlineRight);
            _messageRoot = Child(transform, "Radar Availability");
            var background = _messageRoot.GetComponent<Image>() ?? _messageRoot.gameObject.AddComponent<Image>();
            FaaRadarVisualStyle.ApplyRounded(background, new Color(.025f, .06f, .08f, .94f), 10);
            background.raycastTarget = false;
            _message = Label(_messageRoot, "Message", 15f, TextAlignmentOptions.Center);
            _messageHint = Label(_messageRoot, "Hint", 10f, TextAlignmentOptions.Center);
            // Chrome must not intercept radar taps. Only the explicit display switch is interactive.
            foreach (var graphic in _footer.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
            LayoutChrome();
        }

        private void LayoutChrome()
        {
            float width = Mathf.Clamp(((RectTransform)transform).rect.width, 180f, 420f);
            Dock(_header, new Vector2(.5f, 1f), new Vector2(0, 30f), new Vector2(width, 46f));
            Dock(_title.rectTransform, new Vector2(0, .5f), new Vector2(14f, 8f), new Vector2(width - 106f, 20f), new Vector2(0, .5f));
            Dock(_source.rectTransform, new Vector2(0, .5f), new Vector2(14f, -10f), new Vector2(width - 106f, 16f), new Vector2(0, .5f));
            var toggle = _switchBackground.rectTransform;
            Dock(toggle, new Vector2(1f, .5f), new Vector2(-9f, 0f), new Vector2(72f, 34f), new Vector2(1f, .5f));
            Dock(toggle.Find("Display Caption") as RectTransform, new Vector2(.5f, .5f), new Vector2(0, 9f), new Vector2(66f, 10f));
            Dock(_switchLabel.rectTransform, new Vector2(.5f, .5f), new Vector2(0, -5f), new Vector2(66f, 18f));
            Dock(_footer, new Vector2(.5f, 0f), new Vector2(0f, -12f), new Vector2(width, 24f));
            Dock(_range.rectTransform, new Vector2(0, .5f), new Vector2(4f, 0f), new Vector2(115f, 24f), new Vector2(0, .5f));
            Dock(_detail.rectTransform, new Vector2(1f, .5f), new Vector2(-4f, 0), new Vector2(width - 126f, 24f), new Vector2(1f, .5f));
            Dock(_messageRoot, new Vector2(.5f, .5f), Vector2.zero, new Vector2(Mathf.Min(width - 20f, 232f), 68f));
            Dock(_message.rectTransform, new Vector2(.5f, .5f), new Vector2(0, 11f), new Vector2(_messageRoot.rect.width - 12f, 24f));
            Dock(_messageHint.rectTransform, new Vector2(.5f, .5f), new Vector2(0, -13f), new Vector2(_messageRoot.rect.width - 12f, 24f));
        }

        public void RefreshPresentation()
        {
            if (_header == null) return;
            ResolveReferences();
            LayoutChrome();
            bool weather = radarKind == FaaRadarKind.Weather;
            bool feed = bridge != null && bridge.IsFeedHealthy && bridge.LastPacketAgeSeconds <= 6f;
            bool previous = weather ? weatherDisplay != null && weatherDisplay.HasUsableTexture : bridge != null && bridge.LatestFlightData != null;
            bool standby = weather && weatherData != null && weatherData.RadarData.currentMode == RadarMode.STBY;
            bool poweredOff = weather && weatherDisplay != null && weatherDisplay.HasRadarPowerState && !weatherDisplay.IsRadarPowered;
            bool fresh = feed && (!weather || standby || poweredOff || (weatherDisplay != null && weatherDisplay.HasFreshTexture));
            CurrentState = ResolveState(displayEnabled, Application.isPlaying, fresh, previous, standby, poweredOff);
            if (trafficDisplay != null)
            {
                trafficDisplay.InstrumentDisplayEnabled = displayEnabled;
                trafficDisplay.UseExternalRangeReadout = true;
            }
            _title.text = weather ? "WEATHER" : "TRAFFIC";
            // A texture's existence is not proof of a live feed or known transmitter power.
            string source = weather && (weatherDisplay == null || weatherDisplay.IsProceduralTexture || !Application.isPlaying) ? "SIM WX" : "X-PLANE";
            string connection = !Application.isPlaying ? "EDITOR PREVIEW" : fresh ? "DATA LIVE" : previous ? "DATA STALE" : "WAITING FOR DATA";
            _source.text = source + "  ·  " + connection;
            _source.color = Application.isPlaying && !fresh ? Caution : Muted;
            _switchLabel.text = displayEnabled ? "●  ON" : "○  OFF";
            _switchLabel.color = displayEnabled ? Accent : Muted;
            _switchBackground.color = displayEnabled ? new Color(.035f, .16f, .16f, 1f) : new Color(.06f, .085f, .10f, 1f);
            float range = weather ? (weatherData != null ? weatherData.RadarData.currentRange : 160f) : (trafficDisplay != null ? trafficDisplay.RangeNM : 40f);
            _range.text = $"{range:0.#} <size=9>NM RANGE</size>";
            _range.color = Text;
            if (weather)
            {
                float tilt = weatherData != null ? weatherData.RadarData.tiltAngle : 0f;
                string mode = weatherData != null ? weatherData.RadarData.currentMode.ToString().Replace("_", "+") : "WX";
                // Unknown power remains explicitly unknown, even with fresh generated imagery.
                string power = feed && weatherDisplay != null && weatherDisplay.HasRadarPowerState ? (weatherDisplay.IsRadarPowered ? "PWR ON" : "PWR OFF") : "PWR ?";
                _detail.text = $"{mode}  ·  TILT {tilt:+0.0;-0.0;0.0}°  ·  {power}";
            }
            else
            {
                string orientation = trafficDisplay != null && trafficDisplay.TrackUpModeEnabled ? "TRK UP" : "NORTH UP";
                string count = fresh && trafficController != null ? trafficController.TargetCount.ToString() : "—";
                _detail.text = $"{orientation}  ·  {count} TGT";
            }
            _detail.color = Muted;
            bool message = CurrentState != DataState.Live && CurrentState != DataState.Preview;
            bool turbulenceMode = weather && weatherData != null && TurbulenceEvidence.IsTurbulenceMode(weatherData.RadarData.currentMode);
            bool turbulenceUnavailable = turbulenceMode && CurrentState == DataState.Live;
            if (turbulenceUnavailable)
            {
                // A fresh rain texture must not claim the turbulence channel is live.
                _source.text = source + " · TURB SCAN UNAVAILABLE";
                _source.color = Caution;
                if (weatherData.RadarData.currentMode == RadarMode.TURB) message = true;
                else _detail.text = "WX+T · RAIN ONLY · NO TURB SCAN";
            }
            _messageRoot.gameObject.SetActive(message);
            _message.text = CurrentState switch
            {
                DataState.DisplayOff => "DISPLAY OFF", DataState.RadarOff => "RADAR POWER OFF",
                DataState.Standby => "STANDBY", DataState.Stale => "DATA STALE", _ => "WAITING FOR DATA"
            };
            _message.color = CurrentState == DataState.Stale || CurrentState == DataState.Waiting ? Caution : Text;
            _messageHint.text = CurrentState switch
            {
                DataState.DisplayOff => "Use DISPLAY ON above to restore",
                DataState.RadarOff => "X-Plane reports transmitter off",
                DataState.Standby => "Select a weather mode to resume",
                _ => "Do not use this picture for guidance"
            };
            if (turbulenceUnavailable && weatherData.RadarData.currentMode == RadarMode.TURB)
            {
                _message.text = "TURB SCAN UNAVAILABLE";
                _message.fontSize = 12f;
                _message.color = Caution;
                _messageHint.text = TurbulenceEvidence.Explanation(bridge != null ? bridge.LatestSnapshot?.Weather : null, feed);
                _messageHint.fontSize = 10f;
                _messageHint.textWrappingMode = TextWrappingModes.Normal;
            }
            else _message.fontSize = 15f;
            SuppressLegacyReadouts();
        }

        private void SuppressLegacyReadouts()
        {
            foreach (var label in GetComponentsInChildren<TMP_Text>(true))
            {
                string key = label.name.Replace(" ", "");
                if (key == "RangeLabel" || key == "TiltLabel" || key == "ModeLabel" || key == "TextureStatusLabel" || key == "SourceLabel" || key == "TextureAgeLabel")
                    label.gameObject.SetActive(false);
            }
            Transform power = weatherDisplay != null ? weatherDisplay.transform.parent.Find("WeatherPowerBadge") : null;
            if (power != null) power.gameObject.SetActive(false);
        }

        private static RectTransform Child(Transform parent, string name)
        {
            Transform found = parent.Find(name);
            GameObject go = found != null ? found.gameObject : new GameObject(name, typeof(RectTransform));
            if (found == null) go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        private static TMP_Text Label(Transform parent, string name, float size, TextAlignmentOptions align)
        {
            var rect = Child(parent, name);
            var label = rect.GetComponent<TextMeshProUGUI>() ?? rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.fontSize = size;
            label.enableAutoSizing = false;
            label.fontStyle = FontStyles.Normal;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.alignment = align;
            label.color = Text;
            label.faceColor = Color.white;
            label.canvasRenderer.SetColor(Color.white);
            label.raycastTarget = false;
            return label;
        }

        private static void Dock(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size, Vector2? pivot = null)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot ?? new Vector2(.5f, .5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
        }
    }
}
