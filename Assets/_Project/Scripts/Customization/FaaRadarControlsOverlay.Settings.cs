using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using WeatherRadar;

namespace FAA.Customization
{
    public partial class FaaRadarControlsOverlay
    {
        public const float SettingsPanelWidth = 476f;
        public const float SettingsPanelHeight = 276f;
        private const float ReadableFocusWidth = 796f;
        private const float ReadableFocusHeight = 110f;
        private const float SettingWidth = 222f;
        private const string SettingsHint = "Choose a setting. Changes apply immediately; tap the radar again to close.";
        private const string FocusHint = "Drag to browse; release to return to your aircraft. Use Restore HUD to leave the map.";
        private int _weatherSettingsPage, _trafficSettingsPage;
        private readonly System.Collections.Generic.Dictionary<RectTransform, int> _builtSettingsPages = new();
        private TMP_Text _weatherHelp, _trafficHelp, _trafficSourceText;
        private TMP_Text _weatherPowerCaption, _trafficChartCaption, _trafficBackgroundCaption;

        public static Vector2 CalculateTrafficSettingsDock(Vector2 rootPosition, float rootWidth) =>
            new Vector2(rootPosition.x - rootWidth - 284f - 32f, Mathf.Max(16f, rootPosition.y));

        public void SelectSettingsPage(FaaRadarKind kind, int page)
        {
            if (kind == FaaRadarKind.Weather) _weatherSettingsPage = Mathf.Clamp(page, 0, 1);
            else _trafficSettingsPage = Mathf.Clamp(page, 0, 2);
            // Selecting a page is not a visibility toggle. Touch users should
            // never lose the panel after adjusting a setting.
            EnsureControlStrips();
            UpdateLabels();
        }

        private RectTransform PrepareSettings(RectTransform strip, string title, bool focused, int page, out TMP_Text help)
        {
            var layout = strip.GetComponent<VerticalLayoutGroup>();
            if (layout != null) layout.enabled = false;
            foreach (Transform child in strip) child.gameObject.SetActive(child.name == "Readable Settings");
            RectTransform root = SettingsRect(strip, "Readable Settings");
            StretchToParent(root);
            bool changed = !_builtSettingsPages.TryGetValue(root, out int previousPage) || previousPage != page;
            if (changed)
            {
                foreach (Transform child in root) child.gameObject.SetActive(false);
                _builtSettingsPages[root] = page;
            }

            if (!focused)
            {
                SettingsText(root, "Panel Title", title, 14f, new Vector2(14, 10), new Vector2(230, 22), PrimaryTextColor, true);
                SettingsText(root, "Close Hint", "Tap radar again to close", 10.5f, new Vector2(260, 10), new Vector2(202, 22), SecondaryTextColor);
            }
            var existing = root.Find("Help")?.GetComponent<TMP_Text>();
            string message = !changed && existing != null && !string.IsNullOrWhiteSpace(existing.text) ? existing.text : focused ? FocusHint : SettingsHint;
            help = SettingsText(root, "Help", message, 11f, new Vector2(14, focused ? 79 : 242),
                new Vector2(strip.sizeDelta.x - 28, 28), SecondaryTextColor);
            help.textWrappingMode = TextWrappingModes.Normal;
            return root;
        }

        private void SettingsTabs(RectTransform root, FaaRadarKind kind, int selected, TMP_Text help, params string[] names)
        {
            float width = (SettingsPanelWidth - 28f - (names.Length - 1) * 6f) / names.Length;
            for (int i = 0; i < names.Length; i++)
            {
                int page = i;
                Button tab = SettingButton(root, (kind == FaaRadarKind.Weather ? "WX" : "TCAS") + "SettingsTab" + i,
                    names[i], () => SelectSettingsPage(kind, page), width, 32f,
                    help, "Show " + names[i].ToLowerInvariant() + " settings.");
                Place((RectTransform)tab.transform, new Vector2(14f + i * (width + 6), 40), new Vector2(width, 32));
                tab.GetComponent<Image>().color = i == selected ? ButtonActiveColor : ButtonNormalColor;
                var underline = SettingsRect(tab.transform, "Selected Tab");
                var image = Component<Image>(underline.gameObject);
                image.color = FaaRadarVisualStyle.Accent;
                image.raycastTarget = false;
                Place(underline, new Vector2(12, 29), new Vector2(width - 24, 2));
                underline.gameObject.SetActive(i == selected);
            }
        }

        private RectTransform SettingCard(RectTransform parent, string name, int slot, string caption, out TMP_Text captionText)
        {
            var card = SettingsRect(parent, name);
            Place(card, new Vector2(12 + slot % 2 * 230, 82 + slot / 2 * 78), new Vector2(SettingWidth, 72));
            FaaRadarVisualStyle.ApplyRounded(Component<Image>(card.gameObject), new Color(.025f, .075f, .09f, 1f), 9);
            card.GetComponent<Image>().raycastTarget = false;
            captionText = SettingsText(card, "Setting Name", caption, 12f, new Vector2(10, 5), new Vector2(202, 20), SecondaryTextColor);
            return card;
        }

        private TMP_Text StepSetting(RectTransform root, string prefix, int slot, string caption, UnityAction down, UnityAction up,
            TMP_Text help, string decreaseHint, string increaseHint, string minus = "−", string plus = "+")
        {
            var card = SettingCard(root, prefix + "Group", slot, caption, out _);
            SettingButtonAt(card, prefix + "Down", minus, down, 10, 29, 42, 34, help, decreaseHint);
            var value = SettingsText(card, prefix + "Value", "—", 16f, new Vector2(56, 29), new Vector2(110, 34), PrimaryTextColor, true);
            value.alignment = TextAlignmentOptions.Center;
            SettingButtonAt(card, prefix + "Up", plus, up, 170, 29, 42, 34, help, increaseHint);
            return value;
        }

        private TMP_Text ChoiceSetting(RectTransform root, string group, string buttonName, int slot, string caption, string actionText,
            UnityAction action, TMP_Text help, string explanation, out TMP_Text captionText)
        {
            var card = SettingCard(root, group, slot, caption, out captionText);
            return GetButtonLabel(SettingButtonAt(card, buttonName, actionText, action, 10, 29, 202, 34, help, explanation));
        }

        private void EnsureReadableWeatherControls(RectTransform strip)
        {
            var root = PrepareSettings(strip, "WEATHER SETTINGS", false, _weatherSettingsPage, out _weatherHelp);
            SettingsTabs(root, FaaRadarKind.Weather, _weatherSettingsPage, _weatherHelp, "Radar", "Display");
            if (_weatherSettingsPage == 0)
            {
                _weatherRangeText = StepSetting(root, "WXRange", 0, "Range · nautical miles", WeatherRangeDown, WeatherRangeUp, _weatherHelp,
                    "Reduce the displayed weather range, in nautical miles.", "Increase the displayed weather range, in nautical miles.");
                _weatherModeText = ChoiceSetting(root, "WXModeGroup", "WXModeCycle", 1, "Radar mode · tap to change", "Weather", CycleWeatherMode,
                    _weatherHelp, "Cycle weather, weather + turbulence, turbulence, ground map and standby modes.", out _);
                _weatherTiltText = StepSetting(root, "WXTilt", 2, "Antenna tilt · degrees", WeatherTiltDown, WeatherTiltUp, _weatherHelp,
                    "Lower antenna tilt by 0.5°. Negative tilt points below the horizon.", "Raise antenna tilt by 0.5°. Positive tilt points above the horizon.", "−", "+");
                _weatherGainText = StepSetting(root, "WXGain", 3, "Echo gain · decibels", WeatherGainDown, WeatherGainUp, _weatherHelp,
                    "Reduce echo gain by 1 dB. Available range: −8 to +8 dB.", "Increase echo gain by 1 dB. Available range: −8 to +8 dB.");
            }
            else
            {
                _weatherPowerText = ChoiceSetting(root, "WXPowerGroup", "WXPowerToggle", 0, "Local display · on", "Turn display off",
                    ToggleWeatherProvider, _weatherHelp, "Hide or restore this picture. This does not change simulator transmitter power.", out _weatherPowerCaption);
                _weatherSizeText = StepSetting(root, "WXSize", 1, "Instrument size · pixels", WeatherSizeDown, WeatherSizeUp, _weatherHelp,
                    "Make the weather instrument smaller. This does not change its range.", "Make the weather instrument larger. This does not zoom the weather.");
                ChoiceSetting(root, "WXRefreshGroup", "WXRefresh", 2, "Weather data", "Refresh picture", RefreshWeatherTexture,
                    _weatherHelp, "Request a fresh weather picture from the current data source.", out _);
                var info = SettingCard(root, "WXDisplayInfo", 3, "Display is not transmitter power", out _);
                var note = SettingsText(info, "Note", "The simulator connection stays active\nwhen the local picture is hidden.", 11f,
                    new Vector2(10, 29), new Vector2(202, 35), SecondaryTextColor);
                note.textWrappingMode = TextWrappingModes.Normal;
            }
        }

        private void EnsureReadableTrafficControls(RectTransform strip)
        {
            bool focused = _trafficDisplay != null && _trafficDisplay.IsFullscreen;
            var root = PrepareSettings(strip, "TRAFFIC SETTINGS", focused, focused ? -1 : _trafficSettingsPage, out _trafficHelp);
            if (focused)
            {
                EnsureReadableFocus(root);
                return;
            }
            SettingsTabs(root, FaaRadarKind.Traffic, _trafficSettingsPage, _trafficHelp, "Radar", "Map", "Display");
            if (_trafficSettingsPage == 0)
            {
                _trafficRangeText = StepSetting(root, "TCASRange", 0, "Range · nautical miles", TrafficRangeDown, TrafficRangeUp, _trafficHelp,
                    "Reduce radar range. A manual range adjustment turns auto range off.", "Increase radar range. A manual range adjustment turns auto range off.");
                _trafficAutoText = ChoiceSetting(root, "TCASAutoGroup", "TCASAutoToggle", 1, "Range mode · tap to switch", "Automatic",
                    ToggleTrafficAutoRange, _trafficHelp, "Automatic fits nearby traffic; Manual keeps the range you select.", out _);
                _trafficMaxText = StepSetting(root, "TCASMax", 2, "Traffic symbol limit · aircraft", TrafficMaxTargetsDown, TrafficMaxTargetsUp,
                    _trafficHelp, "Show up to five fewer aircraft symbols. This limits the displayed list; it does not remove source traffic.",
                    "Show up to five more aircraft symbols. This is a display limit, not the detected traffic count.");
                _trafficFullscreenText = ChoiceSetting(root, "TCASViewGroup", "TCASFullscreenToggle", 3, "Map workspace", "Open full map",
                    ToggleTrafficFullscreen, _trafficHelp, "Maximize the map for browsing and confirmed navigation-target setup.", out _);
            }
            else if (_trafficSettingsPage == 1)
            {
                _trafficSourceText = ChoiceSetting(root, "TCASSourceGroup", "TCASSourceCycle", 0, "Map source · tap to change", "Sectional chart",
                    CycleTrafficMapSource, _trafficHelp, "Cycle the available chart and street-map sources. The current map stays visible while loading.", out _);
                _trafficModeText = ChoiceSetting(root, "TCASOrientationGroup", "TCASTrackToggle", 1, "Orientation · tap to switch", "Track up",
                    ToggleTrafficTrackMode, _trafficHelp, "Track up keeps your flight direction at the top. North up keeps north at the top.", out _);
                _trafficOpacityText = StepSetting(root, "TCASOpacity", 2, "Chart opacity · percent", TrafficOpacityDown, TrafficOpacityUp, _trafficHelp,
                    "Make the chart more transparent by 10 percentage points.", "Make the chart more opaque by 10 percentage points.");
                _trafficChartText = ChoiceSetting(root, "TCASChartGroup", "TCASChartToggle", 3, "Chart layer · visible", "Hide chart", ToggleTrafficChart,
                    _trafficHelp, "Hide or restore the chart layer without hiding traffic symbols.", out _trafficChartCaption);
            }
            else
            {
                _trafficRingsText = StepSetting(root, "TCASRings", 0, "Range guides · ring count", TrafficRingsDown, TrafficRingsUp, _trafficHelp,
                    "Remove one range ring. This does not change radar range.", "Add one range ring. This does not change radar range.");
                _trafficBackgroundText = ChoiceSetting(root, "TCASBackgroundGroup", "TCASBackgroundToggle", 1, "Dark backdrop · visible", "Hide backdrop",
                    ToggleTrafficBackground, _trafficHelp, "Toggle the dark contrast plate behind the radar. The chart has a separate visibility control.", out _trafficBackgroundCaption);
                _trafficSizeText = StepSetting(root, "TCASSize", 2, "Instrument size · pixels", TrafficSizeDown, TrafficSizeUp, _trafficHelp,
                    "Make the compact traffic instrument smaller; map range stays unchanged.", "Make the compact traffic instrument larger; map range stays unchanged.");
                ChoiceSetting(root, "TCASRefreshGroup", "TCASRefresh", 3, "Traffic data", "Refresh traffic", RefreshTraffic,
                    _trafficHelp, "Request an updated traffic list from the connected provider.", out _);
            }
        }

        private void EnsureReadableFocus(RectTransform root)
        {
            // One low band with named groups instead of the old SEC / O- / CTR / REST codes.
            _trafficFocusSourceText = FocusChoice(root, "TCASFocusSource", 12, 200, "Map source · change", "Sectional chart", CycleTrafficMapSource,
                "Switch the active chart or street-map source.");
            _trafficFocusOpacityText = FocusStep(root, "TCASFocusOpacity", 220, 154, "Chart opacity", TrafficOpacityDown, TrafficOpacityUp,
                "Make the chart more transparent.", "Make the chart more opaque.");
            _trafficFocusRangeText = FocusStep(root, "TCASFocusZoom", 382, 154, "Map range · NM", TrafficSizeUp, TrafficSizeDown,
                "Decrease range: zoom in for more chart detail.", "Increase range: zoom out to show a wider area.");
            FocusChoice(root, "TCASFocusRecenter", 544, 114, "Map position", "Center aircraft", RecenterTrafficMap, "Return the map center to your aircraft.");
            FocusChoice(root, "TCASFocusRestore", 666, 118, "Exit full map", "Restore HUD", ToggleTrafficFullscreen, "Return to the compact radar and restore the flight HUD.");
        }

        private TMP_Text FocusChoice(RectTransform root, string name, float x, float width, string caption, string text, UnityAction action, string explanation)
        {
            SettingsText(root, name + "Caption", caption, 11f, new Vector2(x, 10), new Vector2(width, 22), SecondaryTextColor);
            return GetButtonLabel(SettingButtonAt(root, name, text, action, x, 34, width, 36, _trafficHelp, explanation));
        }

        private TMP_Text FocusStep(RectTransform root, string name, float x, float width, string caption, UnityAction down, UnityAction up, string downHint, string upHint)
        {
            SettingsText(root, name + "Caption", caption, 11f, new Vector2(x, 10), new Vector2(width, 22), SecondaryTextColor);
            SettingButtonAt(root, name + "Down", "−", down, x, 34, 34, 36, _trafficHelp, downHint);
            var value = SettingsText(root, name + "Value", "—", 15f, new Vector2(x + 37, 34), new Vector2(width - 74, 36), PrimaryTextColor, true);
            value.alignment = TextAlignmentOptions.Center;
            SettingButtonAt(root, name + "Up", "+", up, x + width - 34, 34, 34, 36, _trafficHelp, upHint);
            return value;
        }

        private Button SettingButtonAt(RectTransform parent, string name, string text, UnityAction action, float x, float y,
            float width, float height, TMP_Text help, string explanation)
        {
            var button = SettingButton(parent, name, text, action, width, height, help, explanation);
            Place((RectTransform)button.transform, new Vector2(x, y), new Vector2(width, height));
            return button;
        }

        private Button SettingButton(RectTransform parent, string name, string text, UnityAction action, float width, float height,
            TMP_Text help, string explanation)
        {
            var button = EnsureButton(parent, name, text, () => { action(); UpdateLabels(); }, width);
            var rect = (RectTransform)button.transform;
            rect.sizeDelta = new Vector2(width, height);
            button.navigation = new Navigation { mode = Navigation.Mode.Automatic };
            var element = button.GetComponent<LayoutElement>();
            element.preferredHeight = element.minHeight = height;
            var label = GetButtonLabel(button);
            label.fontSize = 14f;
            label.fontStyle = FontStyles.Normal;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.faceColor = Color.white;
            label.canvasRenderer.SetColor(Color.white);
            var motion = button.GetComponent<FaaRadarButtonMotion>();
            motion.Configure(reducedMotion, 1.015f);
            bool focusedHelp = help == _trafficHelp && _trafficDisplay != null && _trafficDisplay.IsFullscreen;
            Component<FaaRadarControlHint>(button.gameObject).Configure(help, explanation, focusedHelp ? FocusHint : SettingsHint);
            return button;
        }

        private static T Component<T>(GameObject go) where T : UnityEngine.Component
        {
            if (!go.TryGetComponent<T>(out var component)) component = go.AddComponent<T>();
            return component;
        }

        private static RectTransform SettingsRect(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            var go = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform));
            if (existing == null) go.transform.SetParent(parent, false);
            go.SetActive(true);
            return go.GetComponent<RectTransform>();
        }

        private static TMP_Text SettingsText(Transform parent, string name, string text, float size, Vector2 position, Vector2 dimensions, Color color, bool bold = false)
        {
            var rect = SettingsRect(parent, name);
            Place(rect, position, dimensions);
            var label = Component<TextMeshProUGUI>(rect.gameObject);
            label.text = text;
            label.fontSize = size;
            label.enableAutoSizing = false;
            label.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.color = color;
            label.faceColor = Color.white;
            label.canvasRenderer.SetColor(Color.white);
            label.raycastTarget = false;
            return label;
        }

        private static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(position.x, -position.y);
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
        }

        public static string ReadableWeatherMode(RadarMode mode) => mode switch
        {
            RadarMode.WX => "Weather", RadarMode.WX_T => "Weather + turbulence",
            RadarMode.TURB => "Turbulence", RadarMode.MAP => "Ground map",
            RadarMode.STBY => "Standby", _ => "Unavailable"
        };

        public static string ReadableMapSource(string source) => source?.Trim().ToUpperInvariant() switch
        {
            "SECTIONAL" => "Sectional chart", "TERMINAL" or "TERMINAL AREA" => "Terminal area chart",
            "WORLD AERONAUTICAL" => "World aeronautical chart", "STREET" => "Street map",
            "CUSTOM" => "Custom map", null or "" => "No map source", _ => source
        };

        private void RefreshReadableValues()
        {
            if (_weatherDataProvider != null)
            {
                var data = _weatherDataProvider.RadarData;
                SetText(_weatherRangeText, $"{data.currentRange:0.#} NM");
                SetText(_weatherTiltText, $"{Signed(data.tiltAngle, "0.0")}°");
                SetText(_weatherGainText, $"{Signed(data.gainOffset, "0")} dB");
                SetText(_weatherModeText, ReadableWeatherMode(data.currentMode));
            }
            // Unity's unassigned native references are not CLR null; using
            // ?. here throws after an editor domain reload, before Configure.
            var weatherPresentation = _weatherRoot != null ? _weatherRoot.GetComponent<FaaRadarPresentation>() : null;
            bool weatherOn = weatherPresentation == null || weatherPresentation.IsDisplayOn;
            SetText(_weatherPowerText, weatherOn ? "Turn display off" : "Turn display on");
            SetText(_weatherPowerCaption, weatherOn ? "Local display · on" : "Local display · off");
            if (_trafficController != null)
            {
                SetText(_trafficRangeText, $"{_trafficController.RangeNM:0.#} NM");
                SetText(_trafficMaxText, $"{_trafficController.MaxTargets} aircraft");
                SetText(_trafficAutoText, _trafficController.AutoRangeEnabled ? "Automatic" : "Manual");
                SetButtonActive(_trafficAutoText, _trafficController.AutoRangeEnabled);
            }
            if (_trafficDisplay != null)
            {
                SetText(_trafficModeText, _trafficDisplay.TrackUpModeEnabled ? "Track up" : "North up");
                SetText(_trafficChartText, _trafficDisplay.ChartBackgroundVisible ? "Hide chart" : "Show chart");
                SetText(_trafficChartCaption, _trafficDisplay.ChartBackgroundVisible ? "Chart layer · visible" : "Chart layer · hidden");
                SetText(_trafficBackgroundText, _trafficDisplay.ShowRadarBackground ? "Hide backdrop" : "Show backdrop");
                SetText(_trafficBackgroundCaption, _trafficDisplay.ShowRadarBackground ? "Dark backdrop · visible" : "Dark backdrop · hidden");
                SetText(_trafficRingsText, $"{_trafficDisplay.RangeRingCount} rings");
                SetText(_trafficOpacityText, $"{Mathf.RoundToInt(_trafficDisplay.ChartOpacity * 100)}%");
                SetText(_trafficSourceText, ReadableMapSource(_trafficDisplay.MapSourceName));
                SetText(_trafficFocusSourceText, ReadableMapSource(_trafficDisplay.MapSourceName));
                SetText(_trafficFocusOpacityText, $"{Mathf.RoundToInt(_trafficDisplay.ChartOpacity * 100)}%");
                SetText(_trafficFocusRangeText, $"{_trafficDisplay.RangeNM:0.#}");
            }
            SetText(_weatherSizeText, $"{GetRadarPixelSize(_weatherRoot as RectTransform):0} px");
            SetText(_trafficSizeText, $"{GetRadarPixelSize(_trafficRoot as RectTransform):0} px");
        }
    }
}
