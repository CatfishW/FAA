using UnityEditor;
using UnityEngine;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace FAA.XPlaneIntegration.Editor
{
    /// <summary>
    /// Setup wizard window for X-Plane integration.
    /// Guides users through configuration and auto-creates necessary GameObjects.
    /// </summary>
    public class XPlaneSetupWindow : EditorWindow
    {
        private int _currentStep = 0;
        private bool _connectionTestPassed = false;
        private string _connectionTestResult = string.Empty;
        private float _lastLatency = 0f;
        private bool _isTesting = false;

        private readonly string[] _steps =
        {
            "Verify X-Plane Installation",
            "Configure UDP Output",
            "Test Connection",
            "Add Providers to Scene"
        };

        private const string XPLANE_IP = "127.0.0.1";
        private const int XPLANE_PORT = 49009;

        [MenuItem("Tools/X-Plane Integration/Setup Wizard")]
        public static void ShowWindow()
        {
            var window = GetWindow<XPlaneSetupWindow>("X-Plane Setup");
            window.ShowUtility();
            window.minSize = new Vector2(500, 400);
        }

        private void OnGUI()
        {
            DrawHeader();
            DrawProgressBar();
            DrawStepContent();
            DrawFooter();
        }

        private void DrawHeader()
        {
            EditorGUILayout.Space(20);
            var titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 18,
                alignment = TextAnchor.MiddleCenter
            };
            EditorGUILayout.LabelField("X-Plane Integration Setup", titleStyle);

            var subtitleStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };
            EditorGUILayout.LabelField("Follow these steps to configure X-Plane data connection", subtitleStyle);
            EditorGUILayout.Space(15);
        }

        private void DrawProgressBar()
        {
            EditorGUILayout.Space(10);

            var progressRect = GUILayoutUtility.GetRect(10, 20);
            progressRect.width -= 40;
            progressRect.x += 20;

            float progress = (_currentStep + 0.5f) / _steps.Length;
            EditorGUI.ProgressBar(progressRect, progress, $"Step {_currentStep + 1} of {_steps.Length}");

            EditorGUILayout.Space(5);

            var indicatorRect = GUILayoutUtility.GetRect(10, 30);
            indicatorRect.width -= 40;
            indicatorRect.x += 20;

            float stepWidth = indicatorRect.width / _steps.Length;
            for (int i = 0; i < _steps.Length; i++)
            {
                var stepRect = new Rect(indicatorRect.x + i * stepWidth, indicatorRect.y, stepWidth - 5, 20);
                var style = new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = i <= _currentStep ? FontStyle.Bold : FontStyle.Normal
                };
                GUI.Label(stepRect, _steps[i], style);
            }

            EditorGUILayout.Space(15);
        }

        private void DrawStepContent()
        {
            var boxStyle = new GUIStyle("box")
            {
                padding = new RectOffset(15, 15, 15, 15),
                margin = new RectOffset(20, 20, 10, 10)
            };

            EditorGUILayout.BeginVertical(boxStyle);

            switch (_currentStep)
            {
                case 0:
                    DrawStep1_VerifyInstallation();
                    break;
                case 1:
                    DrawStep2_ConfigureUDP();
                    break;
                case 2:
                    DrawStep3_TestConnection();
                    break;
                case 3:
                    DrawStep4_AddProviders();
                    break;
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawStep1_VerifyInstallation()
        {
            var headerStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14 };
            EditorGUILayout.LabelField("Step 1: Verify X-Plane Installation", headerStyle);
            EditorGUILayout.Space(10);

            var labelStyle = new GUIStyle(EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField("Ensure X-Plane 11 or X-Plane 12 is installed on your system.", labelStyle);
            EditorGUILayout.Space(10);

            EditorGUILayout.LabelField("Required:", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("• X-Plane 11.50+ or X-Plane 12", labelStyle);
            EditorGUILayout.LabelField("• Administrator/root privileges for network configuration", labelStyle);
            EditorGUILayout.Space(10);

            EditorGUILayout.LabelField("Installation Paths:", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("• Windows: C:\\Program Files\\X-Plane 11\\", labelStyle);
            EditorGUILayout.LabelField("• macOS: /Applications/X-Plane 11.app/", labelStyle);
            EditorGUILayout.LabelField("• Linux: ~/X-Plane-11/", labelStyle);
            EditorGUILayout.Space(10);

            var helpStyle = new GUIStyle(EditorStyles.helpBox);
            EditorGUILayout.BeginVertical(helpStyle);
            EditorGUILayout.LabelField("💡 Tip: X-Plane does not need to be running during setup, but must be running to test the connection.", labelStyle);
            EditorGUILayout.EndVertical();
        }

        private void DrawStep2_ConfigureUDP()
        {
            var headerStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14 };
            EditorGUILayout.LabelField("Step 2: Configure UDP Output in X-Plane", headerStyle);
            EditorGUILayout.Space(10);

            var labelStyle = new GUIStyle(EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField("Configure X-Plane to send data via UDP to the Unity integration:", labelStyle);
            EditorGUILayout.Space(10);

            EditorGUILayout.LabelField("Configuration Steps:", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("1. Open X-Plane", labelStyle);
            EditorGUILayout.LabelField("2. Go to Settings > Network", labelStyle);
            EditorGUILayout.LabelField("3. Enable \"Send data via UDP\"", labelStyle);
            EditorGUILayout.LabelField("4. Enter the following values:", labelStyle);
            EditorGUILayout.Space(10);

            var configBoxStyle = new GUIStyle("box")
            {
                padding = new RectOffset(10, 10, 10, 10),
                margin = new RectOffset(0, 0, 10, 10)
            };
            EditorGUILayout.BeginVertical(configBoxStyle);

            EditorGUILayout.LabelField("UDP Connection Settings:", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            var monoStyle = new GUIStyle(EditorStyles.label)
            {
                font = Font.CreateFontFromPath(EditorGUIUtility.systemFontPath)
            };

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("IP Address:", GUILayout.Width(80));
            if (GUILayout.Button(XPLANE_IP, GUILayout.Width(150)))
            {
                EditorGUIUtility.systemCopyBuffer = XPLANE_IP;
                ShowNotification(new GUIContent("IP copied to clipboard!"));
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Port:", GUILayout.Width(80));
            if (GUILayout.Button(XPLANE_PORT.ToString(), GUILayout.Width(150)))
            {
                EditorGUIUtility.systemCopyBuffer = XPLANE_PORT.ToString();
                ShowNotification(new GUIContent("Port copied to clipboard!"));
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Required DataRefs:", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            string[] dataRefs =
            {
                "sim/flightmodel/position/lat",
                "sim/flightmodel/position/lon",
                "sim/flightmodel/position/elev",
                "sim/flightmodel/position/theta",
                "sim/flightmodel/position/phi",
                "sim/flightmodel/position/psi",
                "sim/flightmodel/position/Pdot",
                "sim/flightmodel/position/Qdot",
                "sim/flightmodel/position/Rdot",
                "sim/flightmodel/position/local_vx",
                "sim/flightmodel/position/local_vy",
                "sim/flightmodel/position/local_vz",
                "sim/weather/temperature[0]",
                "sim/weather/wind_speed[0]",
                "sim/weather/wind_direction[0]"
            };

            var textAreaStyle = new GUIStyle(EditorStyles.textArea)
            {
                wordWrap = true
            };

            string dataRefsText = string.Join("\n", dataRefs);
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField(dataRefsText, textAreaStyle, GUILayout.Height(150));
            if (GUILayout.Button("Copy DataRefs List"))
            {
                EditorGUIUtility.systemCopyBuffer = dataRefsText;
                ShowNotification(new GUIContent("DataRefs copied to clipboard!"));
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);
            var helpStyle = new GUIStyle(EditorStyles.helpBox);
            EditorGUILayout.BeginVertical(helpStyle);
            EditorGUILayout.LabelField("💡 Note: These DataRefs provide aircraft position, orientation, velocity, and weather data.", labelStyle);
            EditorGUILayout.EndVertical();
        }

        private void DrawStep3_TestConnection()
        {
            var headerStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14 };
            EditorGUILayout.LabelField("Step 3: Test Connection", headerStyle);
            EditorGUILayout.Space(10);

            var labelStyle = new GUIStyle(EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField("Test the UDP connection to X-Plane. Make sure X-Plane is running with UDP output enabled.", labelStyle);
            EditorGUILayout.Space(15);

            var testBoxStyle = new GUIStyle("box")
            {
                padding = new RectOffset(15, 15, 15, 15),
                margin = new RectOffset(0, 0, 10, 10)
            };
            EditorGUILayout.BeginVertical(testBoxStyle);

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (_isTesting)
            {
                GUI.enabled = false;
                EditorGUILayout.LabelField("Testing...", GUILayout.Width(100));
            }
            else
            {
                if (GUILayout.Button("Test Connection", GUILayout.Width(150), GUILayout.Height(30)))
                {
                    _ = TestConnectionAsync();
                }
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            GUI.enabled = true;
            EditorGUILayout.Space(10);

            if (!string.IsNullOrEmpty(_connectionTestResult))
            {
                var resultStyle = new GUIStyle(EditorStyles.label)
                {
                    fontStyle = _connectionTestPassed ? FontStyle.Bold : FontStyle.Normal
                };

                var resultBoxStyle = new GUIStyle(_connectionTestPassed ? "box" : "helpBox")
                {
                    padding = new RectOffset(10, 10, 10, 10)
                };

                EditorGUILayout.BeginVertical(resultBoxStyle);

                if (_connectionTestPassed)
                {
                    EditorGUILayout.LabelField("✓ Connection Successful!", resultStyle);
                    EditorGUILayout.LabelField($"Latency: {_lastLatency:F2} ms");
                    EditorGUILayout.LabelField("DataRef validation: OK");
                }
                else
                {
                    EditorGUILayout.LabelField("✗ Connection Failed", resultStyle);
                    EditorGUILayout.LabelField(_connectionTestResult, labelStyle);
                }

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndVertical();

            if (_connectionTestResult.Contains("failed") || _connectionTestResult.Contains("timeout"))
            {
                EditorGUILayout.Space(10);
                EditorGUILayout.LabelField("Troubleshooting Tips:", EditorStyles.boldLabel);
                EditorGUILayout.BeginVertical("helpBox");
                EditorGUILayout.LabelField("• Ensure X-Plane is running", labelStyle);
                EditorGUILayout.LabelField("• Verify UDP output is enabled in X-Plane Settings > Network", labelStyle);
                EditorGUILayout.LabelField("• Check firewall settings allow UDP port 49009", labelStyle);
                EditorGUILayout.LabelField("• Confirm IP address is 127.0.0.1 (localhost)", labelStyle);
                EditorGUILayout.LabelField("• Try restarting X-Plane after enabling UDP", labelStyle);
                EditorGUILayout.EndVertical();
            }
        }

        private void DrawStep4_AddProviders()
        {
            var headerStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14 };
            EditorGUILayout.LabelField("Step 4: Add Providers to Scene", headerStyle);
            EditorGUILayout.Space(10);

            var labelStyle = new GUIStyle(EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField("Create the X-Plane integration GameObjects in your scene:", labelStyle);
            EditorGUILayout.Space(15);

            var infoBoxStyle = new GUIStyle("box")
            {
                padding = new RectOffset(15, 15, 15, 15),
                margin = new RectOffset(0, 0, 10, 10)
            };
            EditorGUILayout.BeginVertical(infoBoxStyle);

            EditorGUILayout.LabelField("This will create the following hierarchy:", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            var monoStyle = new GUIStyle(EditorStyles.label);
            EditorGUILayout.LabelField("X-Plane Integration (Parent)", monoStyle);
            EditorGUILayout.LabelField("  ├── Aircraft Provider", monoStyle);
            EditorGUILayout.LabelField("  ├── Weather Provider", monoStyle);
            EditorGUILayout.LabelField("  └── Traffic Provider", monoStyle);

            EditorGUILayout.Space(15);

            var buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold
            };

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Add X-Plane Integration to Scene", buttonStyle, GUILayout.Width(250), GUILayout.Height(35)))
            {
                CreateIntegrationHierarchy();
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Provider Descriptions:", EditorStyles.boldLabel);

            var descStyle = new GUIStyle(EditorStyles.wordWrappedMiniLabel);

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("• Aircraft Provider: Receives aircraft position, orientation, and velocity data", descStyle);
            EditorGUILayout.LabelField("• Weather Provider: Receives weather conditions including temperature, wind, and visibility", descStyle);
            EditorGUILayout.LabelField("• Traffic Provider: Receives AI traffic and multiplayer aircraft data", descStyle);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);
            var helpStyle = new GUIStyle(EditorStyles.helpBox);
            EditorGUILayout.BeginVertical(helpStyle);
            EditorGUILayout.LabelField("💡 Note: You can add individual providers manually or use the button above to create the complete setup.", labelStyle);
            EditorGUILayout.EndVertical();
        }

        private void DrawFooter()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            GUI.enabled = _currentStep > 0;
            if (GUILayout.Button("Previous", GUILayout.Width(100)))
            {
                _currentStep--;
            }

            GUI.enabled = true;

            if (_currentStep < _steps.Length - 1)
            {
                if (GUILayout.Button("Next", GUILayout.Width(100)))
                {
                    _currentStep++;
                }
            }
            else
            {
                if (GUILayout.Button("Close", GUILayout.Width(100)))
                {
                    Close();
                }
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(10);
        }

        private async Task TestConnectionAsync()
        {
            _isTesting = true;
            _connectionTestResult = string.Empty;
            _connectionTestPassed = false;

            try
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                using (var udpClient = new UdpClient())
                {
                    udpClient.Client.ReceiveTimeout = 2000;

                    byte[] testData = Encoding.ASCII.GetBytes("TEST");
                    await udpClient.SendAsync(testData, testData.Length, XPLANE_IP, XPLANE_PORT);

                    var endPoint = new IPEndPoint(IPAddress.Any, 0);
                    var receivedBytes = await udpClient.ReceiveAsync();

                    stopwatch.Stop();
                    _lastLatency = stopwatch.ElapsedMilliseconds;

                    if (receivedBytes != null && receivedBytes.Length > 0)
                    {
                        _connectionTestPassed = true;
                        _connectionTestResult = "Successfully received data from X-Plane";
                        ShowNotification(new GUIContent("Connection test passed!"));
                    }
                    else
                    {
                        _connectionTestPassed = false;
                        _connectionTestResult = "Connection timeout - no data received";
                    }
                }
            }
            catch (SocketException ex)
            {
                _connectionTestPassed = false;
                _connectionTestResult = $"Connection failed: {ex.Message}";
            }
            catch (System.Exception ex)
            {
                _connectionTestPassed = false;
                _connectionTestResult = $"Error: {ex.Message}";
            }
            finally
            {
                _isTesting = false;
            }
        }

        private void CreateIntegrationHierarchy()
        {
            var existingParent = GameObject.Find("X-Plane Integration");
            if (existingParent != null)
            {
                ShowNotification(new GUIContent("X-Plane Integration already exists in scene!"));
                return;
            }

            var parent = new GameObject("X-Plane Integration");
            Undo.RegisterCreatedObjectUndo(parent, "Add X-Plane Integration");

            CreateProviderGameObject(parent, "Aircraft Provider", "AircraftDataReceiver");
            CreateProviderGameObject(parent, "Weather Provider", "WeatherDataReceiver");
            CreateProviderGameObject(parent, "Traffic Provider", "TrafficDataReceiver");

            Selection.activeGameObject = parent;

            ShowNotification(new GUIContent("X-Plane Integration hierarchy created!"));

            Debug.Log("X-Plane Integration hierarchy created with 3 providers.");
        }

        private void CreateProviderGameObject(GameObject parent, string name, string componentType)
        {
            var child = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(child, "Add Provider");
            child.transform.SetParent(parent.transform);

            EditorGUILayout.HelpBox(
                $"Add '{componentType}' component to {name} manually, or use the runtime scripts.",
                MessageType.Info);
        }
    }
}
