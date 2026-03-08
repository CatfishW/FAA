using UnityEngine;
using UnityEditor;
using System.Net.Sockets;
using System.Net;
using System.Threading.Tasks;
using FAA.XPlaneIntegration;
using FAA.XPlaneIntegration.Providers;
using AviationUI;
using WeatherRadar;
using TrafficRadar;
using TrafficRadar.Core;

namespace FAA.XPlaneIntegration.Editor
{
    /// <summary>
    /// Comprehensive Unity Editor window for X-Plane integration setup and configuration.
    /// Provides visual status indicators, auto-configuration, validation, and testing tools.
    /// 
    /// Access via: Tools > X-Plane Integration > Setup
    /// </summary>
    public class XPlaneIntegrationSetupEditor : EditorWindow
    {
        #region Styles and Colors

        private static readonly Color StatusGreen = new Color(0.2f, 0.8f, 0.2f);
        private static readonly Color StatusRed = new Color(0.8f, 0.2f, 0.2f);
        private static readonly Color StatusYellow = new Color(0.9f, 0.7f, 0.1f);
        private static readonly Color StatusGray = new Color(0.5f, 0.5f, 0.5f);
        private static readonly Color PanelBackground = new Color(0.15f, 0.15f, 0.15f, 0.3f);
        private static readonly Color HeaderBackground = new Color(0.2f, 0.2f, 0.25f, 0.5f);

        private GUIStyle _headerStyle;
        private GUIStyle _statusBoxStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _componentBoxStyle;

        private void InitializeStyles()
        {
            _headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(10, 10, 10, 10)
            };

            _statusBoxStyle = new GUIStyle("box")
            {
                padding = new RectOffset(15, 15, 15, 15),
                margin = new RectOffset(10, 10, 10, 10)
            };

            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                fixedHeight = 28
            };

            _componentBoxStyle = new GUIStyle("box")
            {
                padding = new RectOffset(10, 10, 10, 10),
                margin = new RectOffset(5, 5, 5, 5)
            };
        }

        #endregion

        #region Settings

        private string _xPlaneIp = "127.0.0.1";
        private int _xPlanePort = 49009;
        private bool _autoConnect = true;

        #endregion

        #region State

        private ConnectionState _connectionState = ConnectionState.Disconnected;
        private string _connectionStatusMessage = "Not connected";
        private float _lastLatency = 0f;
        private bool _isTesting = false;
        private Vector2 _scrollPosition;

        private XPlaneUdpListener _udpListener;
        private XPlaneAircraftProvider _aircraftProvider;
        private XPlaneWeatherProvider _weatherProvider;
        private XPlaneTrafficProvider _trafficProvider;
        private AviationFlightDataProvider _flightDataProvider;
        private TrafficRadarController _trafficRadarController;
        private WeatherRadarProviderBase _weatherRadarProvider;

        #endregion

        #region Menu

        [MenuItem("Tools/X-Plane Integration/Setup")]
        public static void ShowWindow()
        {
            var window = GetWindow<XPlaneIntegrationSetupEditor>("X-Plane Setup");
            window.minSize = new Vector2(550, 650);
            window.Show();
        }

        [MenuItem("Tools/X-Plane Integration/Auto-Configure Scene")]
        public static void AutoConfigureScene()
        {
            var instance = CreateInstance<XPlaneIntegrationSetupEditor>();
            instance.PerformAutoConfigure();
            DestroyImmediate(instance);
        }

        [MenuItem("Tools/X-Plane Integration/Validate Setup")]
        public static void ValidateSetup()
        {
            var instance = CreateInstance<XPlaneIntegrationSetupEditor>();
            instance.RunValidation();
            DestroyImmediate(instance);
        }

        #endregion

        #region GUI

        private void OnEnable()
        {
            InitializeStyles();
            ScanScene();
        }

        private void OnGUI()
        {
            InitializeStyles();

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            DrawHeader();
            DrawConnectionStatusPanel();
            DrawComponentStatusPanel();
            DrawQuickActionsPanel();
            DrawSettingsPanel();
            DrawValidationPanel();

            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            EditorGUILayout.Space(15);

            var headerRect = GUILayoutUtility.GetRect(position.width, 50);
            EditorGUI.DrawRect(headerRect, HeaderBackground);

            var oldColor = GUI.contentColor;
            GUI.contentColor = Color.white;
            EditorGUILayout.LabelField("X-Plane Integration Setup", _headerStyle);
            GUI.contentColor = oldColor;

            EditorGUILayout.Space(10);
        }

        private void DrawConnectionStatusPanel()
        {
            EditorGUILayout.BeginVertical(_statusBoxStyle);

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField("CONNECTION STATUS", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            var statusColor = GetConnectionColor();
            var statusRect = GUILayoutUtility.GetRect(20, 20);
            EditorGUI.DrawRect(statusRect, statusColor);

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("IP Address:", GUILayout.Width(80));
            EditorGUILayout.LabelField(_xPlaneIp);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Port:", GUILayout.Width(80));
            EditorGUILayout.LabelField(_xPlanePort.ToString());
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("State:", GUILayout.Width(80));
            EditorGUILayout.LabelField(_connectionStatusMessage);
            EditorGUILayout.EndHorizontal();

            if (_lastLatency > 0)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Latency:", GUILayout.Width(80));
                EditorGUILayout.LabelField($"{_lastLatency:F1} ms");
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            GUI.enabled = !_isTesting;
            if (GUILayout.Button(_isTesting ? "Testing..." : "Test Connection", _buttonStyle, GUILayout.Width(120)))
            {
                _ = TestConnectionAsync();
            }
            GUI.enabled = true;

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawComponentStatusPanel()
        {
            EditorGUILayout.BeginVertical(_componentBoxStyle);

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField("COMPONENT STATUS", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Scan Scene", GUILayout.Width(100)))
            {
                ScanScene();
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            DrawComponentLine("XPlaneUdpListener", _udpListener != null);
            DrawComponentLine("XPlaneAircraftProvider", _aircraftProvider != null);
            DrawComponentLine("XPlaneWeatherProvider", _weatherProvider != null);
            DrawComponentLine("XPlaneTrafficProvider", _trafficProvider != null);

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("External Dependencies", EditorStyles.miniBoldLabel);
            EditorGUILayout.Space(2);

            DrawComponentLine("AviationFlightDataProvider", _flightDataProvider != null);
            DrawComponentLine("TrafficRadarController", _trafficRadarController != null);
            DrawComponentLine("WeatherRadarProviderBase", _weatherRadarProvider != null);

            EditorGUILayout.EndVertical();
        }

        private void DrawComponentLine(string name, bool exists)
        {
            EditorGUILayout.BeginHorizontal();

            var statusRect = GUILayoutUtility.GetRect(16, 16);
            EditorGUI.DrawRect(statusRect, exists ? StatusGreen : StatusRed);

            EditorGUILayout.LabelField(name, GUILayout.Width(200));

            var statusStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                fontStyle = exists ? FontStyle.Normal : FontStyle.Italic
            };
            EditorGUILayout.LabelField(exists ? "Found" : "Missing", statusStyle);

            EditorGUILayout.EndHorizontal();
        }

        private void DrawQuickActionsPanel()
        {
            EditorGUILayout.BeginVertical(_statusBoxStyle);

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField("QUICK ACTIONS", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8);

            var autoConfigStyle = new GUIStyle(_buttonStyle)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold
            };

            GUI.backgroundColor = new Color(0.3f, 0.7f, 0.3f);
            if (GUILayout.Button("Auto-Configure Scene", autoConfigStyle, GUILayout.Height(35)))
            {
                PerformAutoConfigure();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();

            GUI.backgroundColor = StatusGreen;
            GUI.enabled = _connectionState != ConnectionState.Connected;
            if (GUILayout.Button("Connect", _buttonStyle))
            {
                ConnectToXPlane();
            }
            GUI.enabled = true;

            GUI.backgroundColor = StatusRed;
            GUI.enabled = _connectionState == ConnectionState.Connected;
            if (GUILayout.Button("Disconnect", _buttonStyle))
            {
                DisconnectFromXPlane();
            }
            GUI.enabled = true;

            GUI.backgroundColor = StatusYellow;
            GUI.enabled = _connectionState == ConnectionState.Connected || _connectionState == ConnectionState.Error;
            if (GUILayout.Button("Reconnect", _buttonStyle))
            {
                ReconnectToXPlane();
            }
            GUI.enabled = true;

            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawSettingsPanel()
        {
            EditorGUILayout.BeginVertical(_componentBoxStyle);

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField("SETTINGS", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            _xPlaneIp = EditorGUILayout.TextField("X-Plane IP", _xPlaneIp);
            _xPlanePort = EditorGUILayout.IntField("UDP Port", _xPlanePort);
            _autoConnect = EditorGUILayout.Toggle("Auto-Connect on Start", _autoConnect);

            EditorGUILayout.Space(5);

            EditorGUILayout.LabelField("Quick Presets", EditorStyles.miniBoldLabel);
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Local (127.0.0.1)"))
            {
                _xPlaneIp = "127.0.0.1";
                _xPlanePort = 49009;
            }

            if (GUILayout.Button("Network (192.168.x.x)"))
            {
                _xPlaneIp = "192.168.1.100";
                _xPlanePort = 49009;
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawValidationPanel()
        {
            EditorGUILayout.Space(10);

            GUI.backgroundColor = new Color(0.2f, 0.4f, 0.6f);
            if (GUILayout.Button("Run Full Validation", GUILayout.Height(30)))
            {
                RunValidation();
            }
            GUI.backgroundColor = Color.white;
        }

        #endregion

        #region Scene Scanning

        private void ScanScene()
        {
            var udpWrapper = FindObjectOfType<XPlaneUdpListenerWrapper>();
            _udpListener = udpWrapper != null ? udpWrapper.GetListener() : null;
            _aircraftProvider = FindObjectOfType<XPlaneAircraftProvider>();
            _weatherProvider = FindObjectOfType<XPlaneWeatherProvider>();
            _trafficProvider = FindObjectOfType<XPlaneTrafficProvider>();
            _flightDataProvider = FindObjectOfType<AviationFlightDataProvider>();
            _trafficRadarController = FindObjectOfType<TrafficRadarController>();
            _weatherRadarProvider = FindObjectOfType<WeatherRadarProviderBase>();
        }

        #endregion

        #region Connection Management

        private void ConnectToXPlane()
        {
            if (_udpListener == null)
            {
                var udpWrapper = FindObjectOfType<XPlaneUdpListenerWrapper>();
                _udpListener = udpWrapper != null ? udpWrapper.GetListener() : null;
                if (_udpListener == null)
                {
                    var go = new GameObject("XPlaneUdpListener");
                    _udpListener = go.AddComponent<XPlaneUdpListenerWrapper>().GetListener();
                    Undo.RegisterCreatedObjectUndo(go, "Create XPlaneUdpListener");
                }
            }

            try
            {
                _udpListener.Connect(_xPlaneIp);
                _connectionState = ConnectionState.Connected;
                _connectionStatusMessage = "Connected";
                Debug.Log("[XPlaneIntegration] Connected to X-Plane");
            }
            catch (System.Exception ex)
            {
                _connectionState = ConnectionState.Error;
                _connectionStatusMessage = $"Error: {ex.Message}";
                Debug.LogError($"[XPlaneIntegration] Connection failed: {ex.Message}");
            }
        }

        private void DisconnectFromXPlane()
        {
            if (_udpListener != null)
            {
                _udpListener.Disconnect();
                _connectionState = ConnectionState.Disconnected;
                _connectionStatusMessage = "Disconnected";
                Debug.Log("[XPlaneIntegration] Disconnected from X-Plane");
            }
        }

        private void ReconnectToXPlane()
        {
            DisconnectFromXPlane();
            EditorApplication.delayCall += () => ConnectToXPlane();
        }

        private async Task TestConnectionAsync()
        {
            _isTesting = true;
            _connectionStatusMessage = "Testing...";

            try
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                using (var udpClient = new UdpClient())
                {
                    udpClient.Client.ReceiveTimeout = 2000;

                    byte[] testData = System.Text.Encoding.ASCII.GetBytes("TEST");
                    await udpClient.SendAsync(testData, testData.Length, _xPlaneIp, _xPlanePort);

                    var endPoint = new IPEndPoint(IPAddress.Any, 0);
                    var receiveTask = udpClient.ReceiveAsync();

                    if (await Task.WhenAny(receiveTask, Task.Delay(2000)) == receiveTask)
                    {
                        var receivedBytes = await receiveTask;
                        stopwatch.Stop();
                        _lastLatency = stopwatch.ElapsedMilliseconds;

                        if (receivedBytes.Buffer != null && receivedBytes.Buffer.Length > 0)
                        {
                            _connectionState = ConnectionState.Connected;
                            _connectionStatusMessage = "Connected";
                            ShowNotification(new GUIContent("Connection test passed!"));
                        }
                        else
                        {
                            _connectionState = ConnectionState.Error;
                            _connectionStatusMessage = "No data received";
                        }
                    }
                    else
                    {
                        _connectionState = ConnectionState.Error;
                        _connectionStatusMessage = "Connection timeout";
                    }
                }
            }
            catch (SocketException ex)
            {
                _connectionState = ConnectionState.Error;
                _connectionStatusMessage = $"Socket error: {ex.Message}";
            }
            catch (System.Exception ex)
            {
                _connectionState = ConnectionState.Error;
                _connectionStatusMessage = $"Error: {ex.Message}";
            }
            finally
            {
                _isTesting = false;
            }
        }

        private Color GetConnectionColor()
        {
            switch (_connectionState)
            {
                case ConnectionState.Connected:
                    return StatusGreen;
                case ConnectionState.Error:
                    return StatusRed;
                case ConnectionState.Connecting:
                    return StatusYellow;
                default:
                    return StatusGray;
            }
        }

        #endregion

        #region Auto-Configuration

        private void PerformAutoConfigure()
        {
            Undo.SetCurrentGroupName("X-Plane Integration Auto-Configure");
            int undoGroup = Undo.GetCurrentGroup();

            GameObject integrationRoot = GameObject.Find("X-Plane Integration");
            if (integrationRoot == null)
            {
                integrationRoot = new GameObject("X-Plane Integration");
                Undo.RegisterCreatedObjectUndo(integrationRoot, "Create X-Plane Integration");
            }

            _udpListener = integrationRoot.GetComponent<XPlaneUdpListenerWrapper>()?.GetListener();
            if (_udpListener == null)
            {
                var wrapper = Undo.AddComponent<XPlaneUdpListenerWrapper>(integrationRoot);
                _udpListener = wrapper.GetListener();
            }

            CreateProvider<XPlaneAircraftProvider>(integrationRoot, "Aircraft Provider", ref _aircraftProvider);
            CreateProvider<XPlaneWeatherProvider>(integrationRoot, "Weather Provider", ref _weatherProvider);
            CreateProvider<XPlaneTrafficProvider>(integrationRoot, "Traffic Provider", ref _trafficProvider);

            WireUpReferences();

            EnsureExternalDependencies();

            Undo.CollapseUndoOperations(undoGroup);

            Selection.activeGameObject = integrationRoot;

            EditorUtility.DisplayDialog(
                "X-Plane Integration Configured",
                "X-Plane Integration has been set up with:\n\n" +
                "✓ XPlaneUdpListener\n" +
                "✓ XPlaneAircraftProvider\n" +
                "✓ XPlaneWeatherProvider\n" +
                "✓ XPlaneTrafficProvider\n" +
                "✓ Bridge Components\n\n" +
                "External dependencies verified.",
                "OK");

            Debug.Log("<color=green>[XPlaneIntegration]</color> Auto-configuration complete!");

            ScanScene();
        }

        private void CreateProvider<T>(GameObject parent, string name, ref T provider) where T : MonoBehaviour
        {
            provider = parent.GetComponentInChildren<T>();
            if (provider == null)
            {
                var go = new GameObject(name);
                go.transform.SetParent(parent.transform, false);
                provider = Undo.AddComponent<T>(go);
                Debug.Log($"[XPlaneIntegration] Created {typeof(T).Name}");
            }
        }

        private void WireUpReferences()
        {
            if (_weatherProvider != null)
            {
                if (_udpListener != null)
                {
                    _weatherProvider.SetUdpListener(_udpListener);
                }
            }
        }

        private void EnsureExternalDependencies()
        {
            _flightDataProvider = FindObjectOfType<AviationFlightDataProvider>();
            if (_flightDataProvider == null)
            {
                Debug.LogWarning("[XPlaneIntegration] AviationFlightDataProvider not found. Create one manually.");
            }

            _trafficRadarController = FindObjectOfType<TrafficRadarController>();
            if (_trafficRadarController == null)
            {
                Debug.LogWarning("[XPlaneIntegration] TrafficRadarController not found. Create one manually.");
            }

            _weatherRadarProvider = FindObjectOfType<WeatherRadarProviderBase>();
            if (_weatherRadarProvider == null)
            {
                Debug.LogWarning("[XPlaneIntegration] WeatherRadarProviderBase not found. Create one manually.");
            }
        }

        #endregion

        #region Validation

        private void RunValidation()
        {
            ScanScene();

            var report = new System.Text.StringBuilder();
            report.AppendLine("=== X-Plane Integration Validation Report ===\n");

            bool allValid = true;

            report.AppendLine("--- Core Components ---");
            CheckComponent(report, "XPlaneUdpListener", _udpListener != null, ref allValid, true);

            report.AppendLine("\n--- Providers ---");
            CheckComponent(report, "XPlaneAircraftProvider", _aircraftProvider != null, ref allValid);
            CheckComponent(report, "XPlaneWeatherProvider", _weatherProvider != null, ref allValid);
            CheckComponent(report, "XPlaneTrafficProvider", _trafficProvider != null, ref allValid);

            report.AppendLine("\n--- External Dependencies ---");
            CheckComponent(report, "AviationFlightDataProvider", _flightDataProvider != null, ref allValid, true);
            CheckComponent(report, "TrafficRadarController", _trafficRadarController != null, ref allValid, true);
            CheckComponent(report, "WeatherRadarProviderBase", _weatherRadarProvider != null, ref allValid, true);

            report.AppendLine("\n--- Reference Validation ---");
            ValidateReferences(report, ref allValid);

            report.AppendLine("\n--- Summary ---");
            report.AppendLine(allValid ? "✓ All required components found" : "⚠ Some components missing or misconfigured");

            Debug.Log(report.ToString());

            EditorUtility.DisplayDialog(
                "Validation Result",
                report.ToString(),
                "OK");
        }

        private void CheckComponent(System.Text.StringBuilder report, string name, bool exists, ref bool allValid, bool required = false)
        {
            if (exists)
            {
                report.AppendLine($"✓ {name}: Found");
            }
            else if (required)
            {
                report.AppendLine($"✗ {name}: MISSING (REQUIRED)");
                allValid = false;
            }
            else
            {
                report.AppendLine($"⚠ {name}: Not found (optional)");
            }
        }

        private void ValidateReferences(System.Text.StringBuilder report, ref bool allValid)
        {
            if (_aircraftProvider != null)
            {
                var so = new SerializedObject(_aircraftProvider);
                var udpRef = so.FindProperty("udpListener");
                if (udpRef != null && udpRef.objectReferenceValue == null)
                {
                    report.AppendLine("⚠ XPlaneAircraftProvider: UDP Listener not assigned");
                }
                else
                {
                    report.AppendLine("✓ XPlaneAircraftProvider: References valid");
                }
            }

            if (_weatherProvider != null)
            {
                var so = new SerializedObject(_weatherProvider);
                var udpRef = so.FindProperty("udpListener");
                if (udpRef != null && udpRef.objectReferenceValue == null)
                {
                    report.AppendLine("⚠ XPlaneWeatherProvider: UDP Listener not assigned");
                }
                else
                {
                    report.AppendLine("✓ XPlaneWeatherProvider: References valid");
                }

                var dataProviderRef = so.FindProperty("flightDataProvider");
                if (dataProviderRef != null && dataProviderRef.objectReferenceValue == null)
                {
                    report.AppendLine("⚠ XPlaneWeatherProvider: FlightDataProvider not assigned");
                }
            }

            if (_trafficProvider != null)
            {
                var so = new SerializedObject(_trafficProvider);
                var udpRef = so.FindProperty("udpListener");
                if (udpRef != null && udpRef.objectReferenceValue == null)
                {
                    report.AppendLine("⚠ XPlaneTrafficProvider: UDP Listener not assigned");
                }
                else
                {
                    report.AppendLine("✓ XPlaneTrafficProvider: References valid");
                }

                var radarRef = so.FindProperty("trafficRadarController");
                if (radarRef != null && radarRef.objectReferenceValue == null)
                {
                    report.AppendLine("⚠ XPlaneTrafficProvider: TrafficRadarController not assigned");
                }
            }
        }

        #endregion

        #region Nested Types

        private enum ConnectionState
        {
            Disconnected,
            Connecting,
            Connected,
            Error
        }

        /// <summary>
        /// Wrapper component to make XPlaneUdpListener accessible as a MonoBehaviour.
        /// XPlaneUdpListener is a pure C# class; this wrapper enables Unity component workflow.
        /// </summary>
        public class XPlaneUdpListenerWrapper : MonoBehaviour
        {
            private XPlaneUdpListener _listener;

            public XPlaneUdpListener GetListener() => _listener;

            private void Awake()
            {
                _listener = new XPlaneUdpListener();
            }

            private void OnDestroy()
            {
                _listener?.Dispose();
            }

            private void OnEnable()
            {
            }

            private void OnDisable()
            {
                _listener?.Disconnect();
            }
        }

        #endregion
    }
}
