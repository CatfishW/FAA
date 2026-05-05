using System.Collections.Generic;
using AircraftControl.Core;
using AviationUI;
using UnityEngine;
using UnityEngine.UIElements;

namespace FAA.HUDToolkit
{
    [DefaultExecutionOrder(9000)]
    [RequireComponent(typeof(UIDocument))]
    [AddComponentMenu("FAA/HUD/UI Toolkit HUD")]
    public class FaaUiToolkitHud : MonoBehaviour
    {
        [Header("Data Sources")]
        [SerializeField] private AviationFlightDataProvider flightDataProvider;
        [SerializeField] private AircraftController aircraftController;
        [SerializeField] private bool autoFindSources = true;

        [Header("Appearance")]
        [SerializeField] private Color hudColor = new Color(0.16f, 1f, 0.34f, 0.95f);
        [SerializeField] private float hudScale = 0.86f;
        [SerializeField] private float pitchPixelsPerDegree = 6.8f;
        [SerializeField] private bool visibleOnStart = false;

        private readonly List<PitchMark> _pitchMarks = new List<PitchMark>();
        private readonly List<VisualElement> _bankTicks = new List<VisualElement>();

        private UIDocument _document;
        private VisualElement _root;
        private VisualElement _hudRoot;
        private VisualElement _waterline;
        private VisualElement _waterlineLeftTick;
        private VisualElement _waterlineRightTick;
        private VisualElement _fpvRing;
        private VisualElement _fpvLeft;
        private VisualElement _fpvRight;
        private VisualElement _fpvTop;
        private Label _bankPointer;
        private Label _statusLabel;
        private Label _airspeedValue;
        private Label _altitudeValue;
        private Label _verticalSpeedValue;
        private Label _headingValue;
        private Label _pitchRollValue;
        private Label _engineValue;
        private Label _windValue;
        private Label _aglValue;

        private AviationFlightData _currentData = new AviationFlightData();
        private bool _isVisible;

        public bool IsVisible => _isVisible;
        public AviationFlightData CurrentData => _currentData;

        private void Awake()
        {
            EnsureBuilt();
            RefreshDataSources();
            SetVisible(visibleOnStart);
        }

        private void OnEnable()
        {
            EnsureBuilt();
            SubscribeProvider();
            SetVisible(_isVisible || visibleOnStart);
        }

        private void OnDisable()
        {
            UnsubscribeProvider();
        }

        private void Update()
        {
            if (!_isVisible)
            {
                return;
            }

            if (autoFindSources && (flightDataProvider == null || aircraftController == null))
            {
                RefreshDataSources();
            }

            if (flightDataProvider != null && flightDataProvider.FlightData != null)
            {
                _currentData = flightDataProvider.FlightData;
            }
            else if (aircraftController != null && aircraftController.State != null)
            {
                _currentData = FromAircraftState(aircraftController.State);
            }

            UpdateVisuals();
        }

        [ContextMenu("Refresh Data Sources")]
        public void RefreshDataSources()
        {
            AviationFlightDataProvider oldProvider = flightDataProvider;

            if (flightDataProvider == null && autoFindSources)
            {
                flightDataProvider = FindAnyObjectByType<AviationFlightDataProvider>(FindObjectsInactive.Include);
            }

            if (aircraftController == null && autoFindSources)
            {
                aircraftController = FindAnyObjectByType<AircraftController>(FindObjectsInactive.Include);
            }

            if (oldProvider != flightDataProvider)
            {
                if (oldProvider != null)
                {
                    oldProvider.OnFlightDataUpdated -= HandleFlightDataUpdated;
                }

                SubscribeProvider();
            }
        }

        public void Configure(AviationFlightDataProvider provider, AircraftController controller)
        {
            if (flightDataProvider != null)
            {
                flightDataProvider.OnFlightDataUpdated -= HandleFlightDataUpdated;
            }

            flightDataProvider = provider;
            aircraftController = controller;
            SubscribeProvider();
        }

        public void SetVisible(bool visible)
        {
            _isVisible = visible;
            EnsureBuilt();

            if (_hudRoot != null)
            {
                _hudRoot.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        [ContextMenu("Show UI Toolkit HUD")]
        public void ShowHud()
        {
            SetVisible(true);
        }

        [ContextMenu("Hide UI Toolkit HUD")]
        public void HideHud()
        {
            SetVisible(false);
        }

        private void SubscribeProvider()
        {
            if (flightDataProvider != null)
            {
                flightDataProvider.OnFlightDataUpdated -= HandleFlightDataUpdated;
                flightDataProvider.OnFlightDataUpdated += HandleFlightDataUpdated;
            }
        }

        private void UnsubscribeProvider()
        {
            if (flightDataProvider != null)
            {
                flightDataProvider.OnFlightDataUpdated -= HandleFlightDataUpdated;
            }
        }

        private void HandleFlightDataUpdated(AviationFlightData data)
        {
            if (data == null)
            {
                return;
            }

            _currentData = data;
            if (_isVisible)
            {
                UpdateVisuals();
            }
        }

        private void EnsureBuilt()
        {
            if (_document == null)
            {
                _document = GetComponent<UIDocument>();
            }

            if (_document == null || _document.rootVisualElement == null)
            {
                return;
            }

            if (_hudRoot != null)
            {
                return;
            }

            _root = _document.rootVisualElement;
            _root.pickingMode = PickingMode.Ignore;

            _hudRoot = new VisualElement { name = "FAA-UI-Toolkit-HUD" };
            _hudRoot.pickingMode = PickingMode.Ignore;
            _hudRoot.style.position = Position.Absolute;
            _hudRoot.style.left = 0f;
            _hudRoot.style.top = 0f;
            _hudRoot.style.right = 0f;
            _hudRoot.style.bottom = 0f;
            _hudRoot.style.backgroundColor = Color.clear;
            _root.Add(_hudRoot);

            BuildStaticElements();
            BuildPitchLadder();
            BuildBankTicks();
            UpdateVisuals();
        }

        private void BuildStaticElements()
        {
            _statusLabel = MakeLabel(string.Empty, 12, TextAnchor.MiddleLeft);
            _statusLabel.style.display = DisplayStyle.None;
            _hudRoot.Add(_statusLabel);

            _waterline = MakeLine();
            _waterlineLeftTick = MakeLine();
            _waterlineRightTick = MakeLine();
            _hudRoot.Add(_waterline);
            _hudRoot.Add(_waterlineLeftTick);
            _hudRoot.Add(_waterlineRightTick);

            _fpvRing = MakeBox();
            _fpvRing.style.borderTopLeftRadius = 18f;
            _fpvRing.style.borderTopRightRadius = 18f;
            _fpvRing.style.borderBottomLeftRadius = 18f;
            _fpvRing.style.borderBottomRightRadius = 18f;
            _fpvRing.style.backgroundColor = Color.clear;
            _fpvLeft = MakeLine();
            _fpvRight = MakeLine();
            _fpvTop = MakeLine();
            _hudRoot.Add(_fpvRing);
            _hudRoot.Add(_fpvLeft);
            _hudRoot.Add(_fpvRight);
            _hudRoot.Add(_fpvTop);

            _bankPointer = MakeLabel("^", 22, TextAnchor.MiddleCenter);
            _hudRoot.Add(_bankPointer);

            _airspeedValue = MakeReadout("IAS");
            _altitudeValue = MakeReadout("ALT");
            _verticalSpeedValue = MakeReadout("VS");
            _headingValue = MakeReadout("HDG");
            _pitchRollValue = MakeReadout("ATT");
            _engineValue = MakeReadout("PWR");
            _windValue = MakeReadout("WIND");
            _aglValue = MakeReadout("AGL");

            _hudRoot.Add(_airspeedValue);
            _hudRoot.Add(_altitudeValue);
            _hudRoot.Add(_verticalSpeedValue);
            _hudRoot.Add(_headingValue);
            _hudRoot.Add(_pitchRollValue);
            _hudRoot.Add(_engineValue);
            _hudRoot.Add(_windValue);
            _hudRoot.Add(_aglValue);
        }

        private void BuildPitchLadder()
        {
            for (int angle = -30; angle <= 30; angle += 5)
            {
                if (angle == 0)
                {
                    continue;
                }

                PitchMark mark = new PitchMark
                {
                    Angle = angle,
                    Line = MakeLine(),
                    LeftLabel = MakeLabel(Mathf.Abs(angle).ToString("00"), 14, TextAnchor.MiddleRight),
                    RightLabel = MakeLabel(Mathf.Abs(angle).ToString("00"), 14, TextAnchor.MiddleLeft)
                };

                _pitchMarks.Add(mark);
                _hudRoot.Add(mark.Line);
                _hudRoot.Add(mark.LeftLabel);
                _hudRoot.Add(mark.RightLabel);
            }
        }

        private void BuildBankTicks()
        {
            int[] angles = { -60, -45, -30, -20, -10, 0, 10, 20, 30, 45, 60 };
            foreach (int angle in angles)
            {
                VisualElement tick = MakeLine();
                tick.name = $"BankTick_{angle}";
                _bankTicks.Add(tick);
                _hudRoot.Add(tick);
            }
        }

        private Label MakeReadout(string prefix)
        {
            Label label = MakeLabel(prefix, 17, TextAnchor.MiddleCenter);
            label.style.backgroundColor = Color.clear;
            label.style.borderTopWidth = 0f;
            label.style.borderRightWidth = 0f;
            label.style.borderBottomWidth = 0f;
            label.style.borderLeftWidth = 0f;
            return label;
        }

        private Label MakeLabel(string text, int fontSize, TextAnchor align)
        {
            Label label = new Label(text);
            label.pickingMode = PickingMode.Ignore;
            label.style.position = Position.Absolute;
            label.style.color = hudColor;
            label.style.fontSize = fontSize;
            label.style.unityTextAlign = align;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.whiteSpace = WhiteSpace.NoWrap;
            return label;
        }

        private VisualElement MakeLine()
        {
            VisualElement line = new VisualElement();
            line.pickingMode = PickingMode.Ignore;
            line.style.position = Position.Absolute;
            line.style.backgroundColor = hudColor;
            return line;
        }

        private VisualElement MakeBox()
        {
            VisualElement box = new VisualElement();
            box.pickingMode = PickingMode.Ignore;
            box.style.position = Position.Absolute;
            box.style.borderTopWidth = 2f;
            box.style.borderRightWidth = 2f;
            box.style.borderBottomWidth = 2f;
            box.style.borderLeftWidth = 2f;
            box.style.borderTopColor = hudColor;
            box.style.borderRightColor = hudColor;
            box.style.borderBottomColor = hudColor;
            box.style.borderLeftColor = hudColor;
            return box;
        }

        private void UpdateVisuals()
        {
            if (_hudRoot == null)
            {
                return;
            }

            float width = SanitizedDimension(_hudRoot.resolvedStyle.width, Screen.width);
            float height = SanitizedDimension(_hudRoot.resolvedStyle.height, Screen.height);
            float scale = Mathf.Clamp(Mathf.Min(width / 1280f, height / 720f), 0.7f, 1.5f) * Mathf.Max(0.25f, hudScale);
            float centerX = width * 0.5f;
            float centerY = height * 0.52f;

            AviationFlightData data = _currentData ?? new AviationFlightData();
            float pitch = Mathf.Clamp(data.pitch, -90f, 90f);
            float roll = Mathf.DeltaAngle(0f, data.roll);
            float heading = Mathf.Repeat(data.heading, 360f);

            _statusLabel.style.display = DisplayStyle.None;

            SetBox(_waterline, centerX - 110f * scale, centerY - 1f * scale, 220f * scale, 2f * scale);
            SetBox(_waterlineLeftTick, centerX - 110f * scale, centerY - 12f * scale, 2f * scale, 24f * scale);
            SetBox(_waterlineRightTick, centerX + 108f * scale, centerY - 12f * scale, 2f * scale, 24f * scale);

            UpdatePitchLadder(centerX, centerY, scale, pitch);
            UpdateBankTicks(centerX, centerY, scale, roll);
            UpdateFlightPathVector(centerX, centerY, scale, data);
            UpdateReadouts(centerX, centerY, scale, data, pitch, roll, heading);
        }

        private void UpdatePitchLadder(float centerX, float centerY, float scale, float pitch)
        {
            foreach (PitchMark mark in _pitchMarks)
            {
                float y = centerY - ((mark.Angle - pitch) * pitchPixelsPerDegree * scale);
                bool visible = y > centerY - 190f * scale && y < centerY + 190f * scale;
                DisplayStyle display = visible ? DisplayStyle.Flex : DisplayStyle.None;
                mark.Line.style.display = display;
                mark.LeftLabel.style.display = display;
                mark.RightLabel.style.display = display;

                if (!visible)
                {
                    continue;
                }

                bool major = mark.Angle % 10 == 0;
                float lineWidth = major ? 96f * scale : 58f * scale;
                float lineHeight = major ? 2f * scale : 1f * scale;
                SetBox(mark.Line, centerX - lineWidth * 0.5f, y, lineWidth, Mathf.Max(1f, lineHeight));

                if (major)
                {
                    SetBox(mark.LeftLabel, centerX - lineWidth * 0.5f - 42f * scale, y - 10f * scale, 34f * scale, 20f * scale);
                    SetBox(mark.RightLabel, centerX + lineWidth * 0.5f + 8f * scale, y - 10f * scale, 34f * scale, 20f * scale);
                }
                else
                {
                    mark.LeftLabel.style.display = DisplayStyle.None;
                    mark.RightLabel.style.display = DisplayStyle.None;
                }
            }
        }

        private void UpdateBankTicks(float centerX, float centerY, float scale, float roll)
        {
            float radius = 205f * scale;
            float arcCenterY = centerY + 30f * scale;
            int[] angles = { -60, -45, -30, -20, -10, 0, 10, 20, 30, 45, 60 };

            for (int i = 0; i < _bankTicks.Count && i < angles.Length; i++)
            {
                int angle = angles[i];
                float radians = angle * Mathf.Deg2Rad;
                float x = centerX + Mathf.Sin(radians) * radius;
                float y = arcCenterY - Mathf.Cos(radians) * radius;
                float tickHeight = Mathf.Abs(angle) % 30 == 0 ? 18f * scale : 10f * scale;
                SetBox(_bankTicks[i], x - 1f * scale, y, Mathf.Max(1f, 2f * scale), tickHeight);
            }

            float pointerAngle = Mathf.Clamp(-roll, -60f, 60f) * Mathf.Deg2Rad;
            float pointerX = centerX + Mathf.Sin(pointerAngle) * radius;
            float pointerY = arcCenterY - Mathf.Cos(pointerAngle) * radius - 22f * scale;
            SetBox(_bankPointer, pointerX - 14f * scale, pointerY, 28f * scale, 24f * scale);
        }

        private void UpdateFlightPathVector(float centerX, float centerY, float scale, AviationFlightData data)
        {
            float relativeTrack = Mathf.DeltaAngle(data.heading, data.track);
            float x = centerX + Mathf.Clamp(relativeTrack, -10f, 10f) * 10f * scale;
            float y = centerY - Mathf.Clamp(data.flightPathAngle, -15f, 15f) * pitchPixelsPerDegree * scale;
            float radius = 14f * scale;

            SetBox(_fpvRing, x - radius, y - radius, radius * 2f, radius * 2f);
            SetBox(_fpvLeft, x - 42f * scale, y - 1f * scale, 24f * scale, Mathf.Max(1f, 2f * scale));
            SetBox(_fpvRight, x + 18f * scale, y - 1f * scale, 24f * scale, Mathf.Max(1f, 2f * scale));
            SetBox(_fpvTop, x - 1f * scale, y - 42f * scale, Mathf.Max(1f, 2f * scale), 24f * scale);
        }

        private void UpdateReadouts(
            float centerX,
            float centerY,
            float scale,
            AviationFlightData data,
            float pitch,
            float roll,
            float heading)
        {
            SetBox(_airspeedValue, centerX - 410f * scale, centerY - 32f * scale, 120f * scale, 34f * scale);
            SetBox(_altitudeValue, centerX + 290f * scale, centerY - 32f * scale, 132f * scale, 34f * scale);
            SetBox(_verticalSpeedValue, centerX + 430f * scale, centerY - 32f * scale, 98f * scale, 34f * scale);
            SetBox(_headingValue, centerX - 54f * scale, centerY + 204f * scale, 108f * scale, 32f * scale);
            SetBox(_pitchRollValue, centerX - 104f * scale, centerY + 244f * scale, 208f * scale, 28f * scale);
            SetBox(_engineValue, centerX - 205f * scale, centerY - 250f * scale, 410f * scale, 28f * scale);
            SetBox(_windValue, centerX - 90f * scale, centerY - 286f * scale, 180f * scale, 26f * scale);
            SetBox(_aglValue, centerX + 290f * scale, centerY + 8f * scale, 132f * scale, 28f * scale);

            _airspeedValue.text = $"{data.indicatedAirspeed:000}";
            _altitudeValue.text = $"{data.altitudeMSL:00000}";
            _verticalSpeedValue.text = $"{data.verticalSpeed:+0000;-0000;0000}";
            _headingValue.text = $"{heading:000}";
            _pitchRollValue.text = $"P {pitch:+00.0;-00.0;00.0}  R {roll:+000;-000;000}";
            _engineValue.text = $"TRQ {data.engine1Torque:000}/{data.engine2Torque:000}  NR {data.engine1NR:000}/{data.engine2NR:000}";
            _windValue.text = $"{data.windDirection:000}/{data.windSpeed:00}";
            _aglValue.text = $"RALT {data.altitudeAGL:0000}";
        }

        private static void SetBox(VisualElement element, float left, float top, float width, float height)
        {
            if (element == null)
            {
                return;
            }

            element.style.left = left;
            element.style.top = top;
            element.style.width = Mathf.Max(1f, width);
            element.style.height = Mathf.Max(1f, height);
        }

        private static float SanitizedDimension(float resolved, int fallback)
        {
            if (float.IsNaN(resolved) || resolved <= 1f)
            {
                return Mathf.Max(1f, fallback);
            }

            return resolved;
        }

        private static AviationFlightData FromAircraftState(AircraftState state)
        {
            return new AviationFlightData
            {
                pitch = state.Pitch,
                roll = state.Roll,
                heading = state.Heading,
                indicatedAirspeed = state.IndicatedAirspeedKnots,
                trueAirspeed = state.TrueAirspeedKnots,
                groundSpeed = state.GroundSpeedKnots,
                altitudeMSL = state.AltitudeFeet,
                verticalSpeed = state.VerticalSpeedFpm,
                autopilotEngaged = state.AutopilotEngaged,
                engine1Torque = state.ThrottlePercent,
                engine2Torque = state.ThrottlePercent,
                engine1NR = state.MainRotorRpm,
                engine2NR = state.TailRotorRpm
            };
        }

        private struct PitchMark
        {
            public int Angle;
            public VisualElement Line;
            public Label LeftLabel;
            public Label RightLabel;
        }
    }
}
