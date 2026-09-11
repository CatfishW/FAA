using IndicatorSystem.Controller;
using IndicatorSystem.Core;
using IndicatorSystem.Integration;
using FAA.Customization;
using UnityEngine;
using UnityEngine.UI;

namespace IndicatorSystem.Display
{
    /// <summary>Marker controls, separate from radar display power. Status stays visible when cues are hidden.</summary>
    public sealed class IndicatorControlsPanel : MonoBehaviour
    {
        private IndicatorSystemController _controller;
        private TrafficIndicatorBridge _traffic;
        private WeatherIndicatorBridge _weather;
        private Text _trafficTitle, _trafficStatus, _weatherTitle, _weatherStatus, _summary, _range;
        private Image _trafficBackground, _weatherBackground;
        private GameObject _content;
        private Text _headerLabel;
        private GameObject _legend;
        private bool _expanded;
        private readonly Vector3[] _boundsCorners = new Vector3[4];
        private float _nextRefresh;
        private const string TrafficKey = "FAA.Cues.TrafficVisible";
        private const string WeatherKey = "FAA.Cues.WeatherVisible";
        private const string ExpandedKey = "FAA.Cues.ControlsExpanded";
        private static readonly Color Active = new Color(0.08f, 0.23f, 0.26f, 0.96f);
        private static readonly Color Inactive = new Color(0.065f, 0.10f, 0.14f, 0.96f);
        public bool IsExpanded => _expanded;

        public static IndicatorControlsPanel Create(IndicatorSystemController controller, Transform parent)
        {
            var root = new GameObject("Screen cue controls", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster), typeof(Image));
            root.transform.SetParent(parent, false);
            var rt = root.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1, 1);
            rt.anchoredPosition = new Vector2(-62, -62);
            rt.sizeDelta = new Vector2(306, 167);
            var canvas = root.GetComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 5026;
            var background = root.GetComponent<Image>();
            background.color = new Color(0.012f, 0.045f, 0.068f, 0.91f);
            background.raycastTarget = false;
            var panel = root.AddComponent<IndicatorControlsPanel>();
            panel._controller = controller;
            panel._traffic = controller.GetComponent<TrafficIndicatorBridge>();
            panel._weather = controller.GetComponent<WeatherIndicatorBridge>();
            Button header = MakeButton(root.transform, "Show or hide cue controls", new Vector2(6, -4), new Vector2(148, 27));
            var title = PilotIndicatorCue.MakeText(header.transform, "Title", 12, FontStyle.Bold);
            PilotIndicatorCue.LayoutText(title, new Vector2(6, 0), new Vector2(142, 27));
            panel._headerLabel = title;
            title.color = new Color(0.80f, 0.92f, 0.97f);
            header.onClick.AddListener(panel.ToggleExpanded);
            panel._content = new GameObject("Expandable cue controls", typeof(RectTransform));
            panel._content.transform.SetParent(root.transform, false);
            var contentRect = panel._content.GetComponent<RectTransform>();
            contentRect.anchorMin = Vector2.zero; contentRect.anchorMax = Vector2.one;
            contentRect.offsetMin = contentRect.offsetMax = Vector2.zero;
            Button keyButton = MakeButton(panel._content.transform, "Symbol key", new Vector2(161, -6), new Vector2(44, 24));
            var keyLabel = PilotIndicatorCue.MakeText(keyButton.transform, "Label", 10, FontStyle.Bold);
            PilotIndicatorCue.LayoutText(keyLabel, new Vector2(6, 0), new Vector2(38, 24));
            keyLabel.text = "KEY";
            keyLabel.color = title.color;
            keyButton.onClick.AddListener(panel.ToggleLegend);
            Button rangeButton = MakeButton(panel._content.transform, "Marker range", new Vector2(211, -6), new Vector2(83, 24));
            panel._range = PilotIndicatorCue.MakeText(rangeButton.transform, "Range", 11, FontStyle.Bold);
            PilotIndicatorCue.LayoutText(panel._range, new Vector2(5, 0), new Vector2(78, 24));
            rangeButton.onClick.AddListener(panel.CycleRange);
            panel.MakeRow(IndicatorType.Traffic, -35, out panel._trafficTitle, out panel._trafficStatus, out panel._trafficBackground);
            panel.MakeRow(IndicatorType.Weather, -85, out panel._weatherTitle, out panel._weatherStatus, out panel._weatherBackground);
            panel._summary = PilotIndicatorCue.MakeText(panel._content.transform, "Visibility and declutter", 10, FontStyle.Normal);
            PilotIndicatorCue.LayoutText(panel._summary, new Vector2(12, -133), new Vector2(290, 14));
            panel._summary.color = new Color(0.60f, 0.74f, 0.79f);
            var help = PilotIndicatorCue.MakeText(panel._content.transform, "Arrow explanation", 10, FontStyle.Normal);
            PilotIndicatorCue.LayoutText(help, new Vector2(12, -150), new Vector2(290, 14));
            help.text = "ARROW = OUTSIDE VIEW · ICON = TARGET TYPE";
            help.color = panel._summary.color;
            if (PlayerPrefs.HasKey(TrafficKey)) controller.SetTypeVisible(IndicatorType.Traffic, PlayerPrefs.GetInt(TrafficKey) != 0);
            if (PlayerPrefs.HasKey(WeatherKey)) controller.SetTypeVisible(IndicatorType.Weather, PlayerPrefs.GetInt(WeatherKey) != 0);
            panel.ApplyExpandedState(PlayerPrefs.GetInt(ExpandedKey, 0) != 0);
            panel.Refresh();
            return panel;
        }

        public void ToggleExpanded() => SetExpanded(!_expanded);

        public void SetExpanded(bool expanded)
        {
            ApplyExpandedState(expanded);
            PlayerPrefs.SetInt(ExpandedKey, expanded ? 1 : 0);
            PlayerPrefs.Save();
        }

        private void ApplyExpandedState(bool expanded)
        {
            _expanded = expanded;
            _content.SetActive(expanded);
            ((RectTransform)transform).sizeDelta = expanded ? new Vector2(306, 167) : new Vector2(160, 35);
            _headerLabel.text = expanded ? "SCREEN CUES  −" : "SCREEN CUES  +";
            // Hiding controls never switches traffic or weather markers off.
            // The next expansion starts with controls, not a large open legend.
            if (!expanded && _legend != null) _legend.SetActive(false);
        }

        public void ToggleLegend()
        {
            if (!_expanded) SetExpanded(true);
            if (_legend == null)
            {
                _legend = new GameObject("Screen cue symbol key", typeof(RectTransform), typeof(Image));
                _legend.transform.SetParent(_content.transform, false);
                var rt = _legend.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
                rt.anchoredPosition = new Vector2(0, -174);
                rt.sizeDelta = new Vector2(306, 294);
                _legend.GetComponent<Image>().color = new Color(.012f, .045f, .068f, .98f);
                FaaRadarIcon[] icons = { FaaRadarIcon.AircraftAirliner, FaaRadarIcon.AircraftGeneral, FaaRadarIcon.AircraftHelicopter,
                    FaaRadarIcon.AircraftMilitary, FaaRadarIcon.AircraftUnknown, FaaRadarIcon.WeatherRainLight,
                    FaaRadarIcon.WeatherRainModerate, FaaRadarIcon.WeatherRainHeavy };
                string[] labels = { "AIRLINER", "LIGHT AIRCRAFT", "HELICOPTER", "MILITARY CATEGORY", "AIRCRAFT TYPE UNREPORTED", "LIGHT RAIN RETURN", "MODERATE RAIN RETURN", "HEAVY RAIN RETURN" };
                for (int i = 0; i < icons.Length; i++)
                {
                    var symbol = new GameObject(labels[i], typeof(RectTransform), typeof(PilotCueSymbol)).GetComponent<PilotCueSymbol>();
                    symbol.transform.SetParent(_legend.transform, false);
                    symbol.rectTransform.anchorMin = symbol.rectTransform.anchorMax = new Vector2(0, 1);
                    symbol.rectTransform.anchoredPosition = new Vector2(25, -23 - i * 31);
                    symbol.rectTransform.sizeDelta = new Vector2(31, 31);
                    symbol.raycastTarget = false;
                    symbol.Configure(icons[i], false, 0, new Color(.55f, .91f, .96f));
                    var label = PilotIndicatorCue.MakeText(_legend.transform, "Meaning", 11, FontStyle.Bold);
                    PilotIndicatorCue.LayoutText(label, new Vector2(48, -11 - i * 31), new Vector2(250, 24));
                    label.text = labels[i]; label.color = new Color(.80f, .92f, .97f);
                }
                var note = PilotIndicatorCue.MakeText(_legend.transform, "Source caveat", 10, FontStyle.Normal);
                PilotIndicatorCue.LayoutText(note, new Vector2(12, -260), new Vector2(282, 28));
                note.text = "SIMULATED = illustrative, not measured weather.\nUnknown type is never guessed from speed.";
                note.color = new Color(.65f, .78f, .82f);
            }
            else _legend.SetActive(!_legend.activeSelf);
        }

        public Rect OccupiedScreenBounds()
        {
            var corners = _boundsCorners;
            ((RectTransform)transform).GetWorldCorners(corners);
            var canvas = GetComponentInParent<Canvas>().rootCanvas;
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Vector2 min = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
            Vector2 max = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
            if (_legend != null && _legend.activeInHierarchy)
            {
                _legend.GetComponent<RectTransform>().GetWorldCorners(corners);
                min = Vector2.Min(min, RectTransformUtility.WorldToScreenPoint(camera, corners[0]));
                max = Vector2.Max(max, RectTransformUtility.WorldToScreenPoint(camera, corners[2]));
            }
            return Rect.MinMaxRect(min.x - 8, min.y - 8, max.x + 8, max.y + 8);
        }

        private void MakeRow(IndicatorType type, float y, out Text title, out Text status, out Image background)
        {
            Button button = MakeButton(_content.transform, type + " marker toggle", new Vector2(8, y), new Vector2(290, 44));
            background = button.GetComponent<Image>();
            var iconObject = new GameObject("Vector icon", typeof(RectTransform), typeof(PilotCueSymbol));
            iconObject.transform.SetParent(button.transform, false);
            var icon = iconObject.GetComponent<PilotCueSymbol>();
            icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(0, 0.5f);
            icon.rectTransform.anchoredPosition = new Vector2(20, 0);
            icon.rectTransform.sizeDelta = new Vector2(32, 32);
            icon.raycastTarget = false;
            icon.Configure(type == IndicatorType.Weather, false, 0, new Color(0.52f, 0.91f, 0.95f));
            title = PilotIndicatorCue.MakeText(button.transform, "Marker state", 12, FontStyle.Bold);
            PilotIndicatorCue.LayoutText(title, new Vector2(40, -4), new Vector2(242, 18));
            status = PilotIndicatorCue.MakeText(button.transform, "Data status", 10, FontStyle.Normal);
            PilotIndicatorCue.LayoutText(status, new Vector2(40, -24), new Vector2(242, 16));
            button.onClick.AddListener(() => Toggle(type));
        }

        private static Button MakeButton(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = position; rt.sizeDelta = size;
            var image = go.GetComponent<Image>(); image.color = Active;
            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.highlightedColor = new Color(1.18f, 1.18f, 1.18f);
            colors.pressedColor = new Color(0.72f, 0.90f, 0.92f);
            colors.fadeDuration = 0.12f;
            button.colors = colors;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            return button;
        }

        public void Toggle(IndicatorType type)
        {
            bool show = !_controller.IsTypeVisible(type);
            _controller.SetTypeVisible(type, show);
            PlayerPrefs.SetInt(type == IndicatorType.Weather ? WeatherKey : TrafficKey, show ? 1 : 0);
            PlayerPrefs.Save();
            Refresh();
        }

        public void CycleRange()
        {
            float value = _controller.Settings.maxDisplayDistance;
            _controller.Settings.maxDisplayDistance = value < 20f ? 20f : value < 40f ? 40f : value < 80f ? 80f : 10f;
            _controller.RefreshSettings();
            Refresh();
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextRefresh || _controller == null) return;
            _nextRefresh = Time.unscaledTime + 0.2f;
            Refresh();
        }

        private void Refresh()
        {
            if (_controller == null || _controller.Settings == null) return;
            if (_traffic == null) _traffic = _controller.GetComponent<TrafficIndicatorBridge>();
            if (_weather == null) _weather = _controller.GetComponent<WeatherIndicatorBridge>();
            UpdateRow(IndicatorType.Traffic, _trafficTitle, _trafficStatus, _trafficBackground,
                _controller.TrafficVisibleCount, _traffic != null ? _traffic.SourceStatus : "SOURCE NOT CONNECTED");
            UpdateRow(IndicatorType.Weather, _weatherTitle, _weatherStatus, _weatherBackground,
                _controller.WeatherVisibleCount, _weather != null ? _weather.SourceStatus : "SOURCE NOT CONNECTED");
            _range.text = _controller.Settings.maxDisplayDistance.ToString("F0") + " NM  ›";
            _range.color = new Color(0.65f, 0.87f, 0.92f);
            _summary.text = _controller.OnScreenCount + " IN VIEW   ·   " + _controller.OffScreenCount + " OFF SCREEN" +
                (_controller.SuppressedCount > 0 ? "   ·   " + _controller.SuppressedCount + " DECLUTTERED" : "");
        }

        private void UpdateRow(IndicatorType type, Text title, Text status, Image background, int count, string source)
        {
            bool enabled = _controller.IsTypeVisible(type);
            string name = type == IndicatorType.Traffic ? "TRAFFIC" : "WEATHER";
            title.text = name + " MARKERS     " + (enabled ? "ON · " + count : "OFF");
            title.color = enabled ? new Color(0.75f, 0.96f, 0.95f) : new Color(0.59f, 0.66f, 0.72f);
            status.text = enabled ? source : "HIDDEN · TAP TO SHOW";
            status.color = new Color(0.60f, 0.76f, 0.80f);
            background.color = enabled ? Active : Inactive;
        }
    }
}
