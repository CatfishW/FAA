#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace FAA.XPlaneIntegration.Editor
{
    #region XPlaneUdpListener PropertyDrawer

    [CustomPropertyDrawer(typeof(XPlaneUdpListener))]
    public class XPlaneUdpListenerDrawer : PropertyDrawer
    {
        private bool _showAdvanced = false;
        private const float _fieldHeight = 20f;
        private const float _spacing = 4f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var rect = new Rect(position.x, position.y, position.width, _fieldHeight);

            EditorGUI.LabelField(rect, label, EditorStyles.boldLabel);
            rect.y += _fieldHeight + _spacing;

            var statusProp = property.FindPropertyRelative("isConnected");
            var isListening = statusProp != null && statusProp.boolValue;
            
            DrawConnectionStatus(rect, isListening);
            rect.y += _fieldHeight + _spacing * 2;

            var ipProp = property.FindPropertyRelative("ipAddress");
            if (ipProp != null)
            {
                EditorGUI.PropertyField(rect, ipProp, new GUIContent("IP Address", "X-Plane IP address"));
                rect.y += _fieldHeight + _spacing;
            }

            var portProp = property.FindPropertyRelative("port");
            if (portProp != null)
            {
                EditorGUI.PropertyField(rect, portProp, new GUIContent("Port", "X-Plane UDP port (default: 49000)"));
                rect.y += _fieldHeight + _spacing;
            }

            var broadcastProp = property.FindPropertyRelative("broadcastEnabled");
            if (broadcastProp != null)
            {
                EditorGUI.PropertyField(rect, broadcastProp, new GUIContent("Broadcast", "Enable broadcast mode"));
                rect.y += _fieldHeight + _spacing * 2;
            }

            _showAdvanced = EditorGUI.Foldout(rect, _showAdvanced, "Advanced Settings", true);
            if (_showAdvanced)
            {
                rect.y += _fieldHeight + _spacing;
                
                var timeoutProp = property.FindPropertyRelative("timeoutMs");
                if (timeoutProp != null)
                {
                    EditorGUI.PropertyField(rect, timeoutProp, new GUIContent("Timeout (ms)", "Connection timeout in milliseconds"));
                    rect.y += _fieldHeight + _spacing;
                }

                var retryProp = property.FindPropertyRelative("retryCount");
                if (retryProp != null)
                {
                    EditorGUI.PropertyField(rect, retryProp, new GUIContent("Retry Count", "Number of connection retries"));
                    rect.y += _fieldHeight + _spacing;
                }
            }

            EditorGUI.EndProperty();
        }

        private void DrawConnectionStatus(Rect rect, bool isConnected)
        {
            const float indicatorSize = 12f;
            const float labelWidth = 100f;
            
            var indicatorRect = new Rect(rect.x, rect.y, indicatorSize, indicatorSize);
            var color = isConnected ? Color.green : Color.red;
            
            EditorGUI.DrawRect(indicatorRect, color);
            
            var borderRect = new Rect(rect.x - 1, rect.y - 1, indicatorSize + 2, indicatorSize + 2);
            EditorGUI.DrawRect(borderRect, Color.black);

            var labelRect = new Rect(rect.x + indicatorSize + 8, rect.y, labelWidth, rect.height);
            var statusText = isConnected ? "Connected" : "Disconnected";
            var statusColor = isConnected ? Color.green : Color.gray;
            
            var oldColor = GUI.color;
            GUI.color = statusColor;
            EditorGUI.LabelField(labelRect, statusText);
            GUI.color = oldColor;
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float height = _fieldHeight * 5 + _spacing * 6;
            if (_showAdvanced)
            {
                height += _fieldHeight * 2 + _spacing * 2;
            }
            return height;
        }
    }

    #endregion

    #region XPlaneAircraftProvider Editor

    [CustomEditor(typeof(XPlaneAircraftProvider))]
    public class XPlaneAircraftProviderEditor : Editor
    {
        private bool _showDataRefs = true;
        private bool _showLivePreview = true;
        private XPlaneAircraftProvider _provider;
        private Vector2 _scrollPosition;

        private void OnEnable()
        {
            _provider = (XPlaneAircraftProvider)target;
            EditorApplication.update += Repaint;
        }

        private void OnDisable()
        {
            EditorApplication.update -= Repaint;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawHeader();
            EditorGUILayout.Space(10);

            DrawConnectionPanel();
            EditorGUILayout.Space(10);

            DrawDataRefPanel();
            EditorGUILayout.Space(10);

            DrawLivePreviewPanel();
            EditorGUILayout.Space(10);

            DrawHelpBox();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawHeader()
        {
            var style = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14,
                alignment = TextAnchor.MiddleCenter
            };
            EditorGUILayout.LabelField("X-Plane Aircraft Provider", style);
        }

        private void DrawConnectionPanel()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            
            var isConnected = _provider != null && _provider.IsConnected;
            DrawStatusIndicator(isConnected, 16f);
            
            EditorGUILayout.LabelField(
                isConnected ? "Connected to X-Plane" : "Disconnected",
                isConnected ? EditorStyles.boldLabel : EditorStyles.miniLabel
            );
            
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();
            
            GUI.enabled = !isConnected;
            if (GUILayout.Button("Connect", GUILayout.Height(30)))
            {
                _provider?.Connect();
            }
            
            GUI.enabled = isConnected;
            if (GUILayout.Button("Disconnect", GUILayout.Height(30)))
            {
                _provider?.Disconnect();
            }
            
            GUI.enabled = true;
            if (GUILayout.Button("Refresh", GUILayout.Height(30)))
            {
                _provider?.RefreshData();
            }
            
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawDataRefPanel()
        {
            _showDataRefs = EditorGUILayout.Foldout(_showDataRefs, "DataRef Subscriptions", true);
            
            if (!_showDataRefs) return;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            var dataRefsProp = serializedObject.FindProperty("subscribedDataRefs");
            if (dataRefsProp != null)
            {
                for (int i = 0; i < dataRefsProp.arraySize; i++)
                {
                    var item = dataRefsProp.GetArrayElementAtIndex(i);
                    var nameProp = item.FindPropertyRelative("dataRefName");
                    var enabledProp = item.FindPropertyRelative("isEnabled");
                    var valueProp = item.FindPropertyRelative("currentValue");

                    EditorGUILayout.BeginHorizontal();
                    
                    if (enabledProp != null)
                    {
                        enabledProp.boolValue = EditorGUILayout.Toggle(enabledProp.boolValue, GUILayout.Width(20));
                    }
                    
                    if (nameProp != null)
                    {
                        EditorGUILayout.LabelField(nameProp.stringValue, GUILayout.MinWidth(200));
                    }
                    
                    if (valueProp != null)
                    {
                        EditorGUILayout.LabelField($"= {valueProp.floatValue:F2}", EditorStyles.miniLabel, GUILayout.Width(80));
                    }
                    
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.EndHorizontal();
                }
            }
            else
            {
                EditorGUILayout.LabelField("No DataRefs configured", EditorStyles.miniLabel);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawLivePreviewPanel()
        {
            _showLivePreview = EditorGUILayout.Foldout(_showLivePreview, "Live Data Preview", true);
            
            if (!_showLivePreview) return;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, GUILayout.Height(150));

            if (_provider != null)
            {
                EditorGUILayout.LabelField("Flight Data", EditorStyles.boldLabel);
                DrawDataField("Altitude", _provider.Altitude?.ToString("F1") ?? "N/A", "ft");
                DrawDataField("Airspeed", _provider.Airspeed?.ToString("F1") ?? "N/A", "kts");
                DrawDataField("Heading", _provider.Heading?.ToString("F1") ?? "N/A", "°");
                DrawDataField("Pitch", _provider.Pitch?.ToString("F2") ?? "N/A", "°");
                DrawDataField("Roll", _provider.Roll?.ToString("F2") ?? "N/A", "°");
                DrawDataField("Vertical Speed", _provider.VerticalSpeed?.ToString("F1") ?? "N/A", "fpm");

                EditorGUILayout.Space(5);

                EditorGUILayout.LabelField("Position", EditorStyles.boldLabel);
                DrawDataField("Latitude", _provider.Latitude?.ToString("F6") ?? "N/A", "");
                DrawDataField("Longitude", _provider.Longitude?.ToString("F6") ?? "N/A", "");
                
                EditorGUILayout.Space(5);

                EditorGUILayout.LabelField("Systems", EditorStyles.boldLabel);
                DrawDataField("Fuel Flow", _provider.FuelFlow?.ToString("F1") ?? "N/A", "PPH");
                DrawDataField("EPR", _provider.EPR?.ToString("F2") ?? "N/A", "");
            }
            else
            {
                EditorGUILayout.LabelField("No provider available", EditorStyles.miniLabel);
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawDataField(string label, string value, string unit)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(120));
            EditorGUILayout.LabelField($"{value} {unit}", GUILayout.Width(100));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawHelpBox()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("X-Plane Setup", EditorStyles.boldLabel);
            
            EditorGUILayout.HelpBox(
                "1. Ensure X-Plane is running\n" +
                "2. Enable UDP output in X-Plane settings\n" +
                "3. Configure matching IP/port settings\n" +
                "4. Click 'Connect' to establish connection",
                MessageType.Info);

            if (GUILayout.Button("Open X-Plane Settings"))
            {
                try
                {
                    System.Diagnostics.Process.Start("X-Plane");
                }
                catch
                {
                    EditorUtility.DisplayDialog("X-Plane Not Found", 
                        "Could not launch X-Plane. Please ensure it's installed.", "OK");
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawStatusIndicator(bool isConnected, float size)
        {
            var rect = GUILayoutUtility.GetRect(size, size);
            var color = isConnected ? Color.green : Color.red;
            
            EditorGUI.DrawRect(rect, color);
            
            var borderRect = new Rect(rect.x - 1, rect.y - 1, size + 2, size + 2);
            EditorGUI.DrawRect(borderRect, Color.black);
        }
    }

    #endregion

    #region XPlaneWeatherProvider Editor

    [CustomEditor(typeof(XPlaneWeatherProvider))]
    public class XPlaneWeatherProviderEditor : Editor
    {
        private bool _showWeatherData = true;
        private XPlaneWeatherProvider _provider;

        private void OnEnable()
        {
            _provider = (XPlaneWeatherProvider)target;
            EditorApplication.update += Repaint;
        }

        private void OnDisable()
        {
            EditorApplication.update -= Repaint;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawWarningBanner();
            EditorGUILayout.Space(10);

            DrawHeader();
            EditorGUILayout.Space(10);

            DrawConnectionStatus();
            EditorGUILayout.Space(10);

            DrawWeatherVisualization();
            EditorGUILayout.Space(10);

            DrawConfiguration();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawWarningBanner()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            var style = new GUIStyle(EditorStyles.label)
            {
                wordWrap = true,
                fontStyle = FontStyle.Bold
            };
            
            EditorGUILayout.LabelField("⚠️ Point-Data Limitation", style);
            EditorGUILayout.HelpBox(
                "X-Plane weather data is limited to specific observation points. " +
                "Data between points is interpolated and may not reflect actual conditions. " +
                "For accurate weather, position aircraft near METAR stations.",
                MessageType.Warning);
            
            EditorGUILayout.EndVertical();
        }

        private void DrawHeader()
        {
            var style = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14,
                alignment = TextAnchor.MiddleCenter
            };
            EditorGUILayout.LabelField("X-Plane Weather Provider", style);
        }

        private void DrawConnectionStatus()
        {
            EditorGUILayout.BeginHorizontal();
            
            var isConnected = _provider != null && _provider.IsConnected;
            var color = isConnected ? Color.green : Color.red;
            
            var rect = GUILayoutUtility.GetRect(12, 12);
            EditorGUI.DrawRect(rect, color);
            
            EditorGUILayout.LabelField(
                isConnected ? "Weather Data Active" : "Weather Data Inactive",
                isConnected ? EditorStyles.boldLabel : EditorStyles.miniLabel
            );
            
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawWeatherVisualization()
        {
            _showWeatherData = EditorGUILayout.Foldout(_showWeatherData, "Weather Conditions", true);
            
            if (!_showWeatherData) return;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            if (_provider != null)
            {
                DrawWeatherBar("Temperature", _provider.TemperatureC?.ToString("F1") ?? "N/A", "°C", 
                    _provider.TemperatureC ?? 0, -50, 50, Color.red);

                EditorGUILayout.LabelField("Wind", EditorStyles.boldLabel);
                DrawDataField("Speed", _provider.WindSpeed?.ToString("F1") ?? "N/A", "kts");
                DrawDataField("Direction", _provider.WindDirection?.ToString("F0") ?? "N/A", "°");
                DrawDataField("Gust", _provider.WindGust?.ToString("F1") ?? "N/A", "kts");

                EditorGUILayout.Space(5);

                DrawDataField("Visibility", _provider.Visibility?.ToString("F1") ?? "N/A", "nm");

                EditorGUILayout.LabelField("Cloud Layers", EditorStyles.boldLabel);
                if (_provider.CloudLayers != null)
                {
                    foreach (var cloud in _provider.CloudLayers)
                    {
                        EditorGUILayout.LabelField($"  • {cloud.type} @ {cloud.altitude:F0} ft", EditorStyles.miniLabel);
                    }
                }
                else
                {
                    EditorGUILayout.LabelField("  No cloud data", EditorStyles.miniLabel);
                }

                EditorGUILayout.Space(5);

                DrawDataField("Barometer", _provider.Pressure?.ToString("F2") ?? "N/A", "inHg");
            }
            else
            {
                EditorGUILayout.LabelField("No weather provider available", EditorStyles.miniLabel);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawWeatherBar(string label, string value, string unit, float currentValue, float min, float max, Color color)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(100));
            
            var rect = GUILayoutUtility.GetRect(100, 16);
            var normalizedValue = Mathf.InverseLerp(min, max, currentValue);
            
            var fillRect = new Rect(rect.x, rect.y, rect.width * normalizedValue, rect.height);
            var oldColor = GUI.color;
            GUI.color = color;
            EditorGUI.DrawRect(fillRect, color);
            GUI.color = oldColor;
            
            EditorGUI.DrawRect(rect, new Color(0, 0, 0, 0));
            
            EditorGUILayout.LabelField($"{value} {unit}", GUILayout.Width(80));
            EditorGUILayout.EndHorizontal();
        }

        private void DrawDataField(string label, string value, string unit)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(100));
            EditorGUILayout.LabelField($"{value} {unit}", GUILayout.Width(100));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawConfiguration()
        {
            EditorGUILayout.LabelField("Configuration", EditorStyles.boldLabel);
            
            var updateIntervalProp = serializedObject.FindProperty("updateInterval");
            if (updateIntervalProp != null)
            {
                EditorGUILayout.PropertyField(updateIntervalProp, new GUIContent("Update Interval (s)"));
            }

            var autoRefreshProp = serializedObject.FindProperty("autoRefresh");
            if (autoRefreshProp != null)
            {
                EditorGUILayout.PropertyField(autoRefreshProp, new GUIContent("Auto Refresh"));
            }
        }
    }

    #endregion

    #region XPlaneTrafficProvider Editor

    [CustomEditor(typeof(XPlaneTrafficProvider))]
    public class XPlaneTrafficProviderEditor : Editor
    {
        private const int MaxTrafficSlots = 20;
        private bool[] _slotExpanded = new bool[MaxTrafficSlots];
        private XPlaneTrafficProvider _provider;

        private void OnEnable()
        {
            _provider = (XPlaneTrafficProvider)target;
            EditorApplication.update += Repaint;
            
            if (_slotExpanded.Length != MaxTrafficSlots)
            {
                _slotExpanded = new bool[MaxTrafficSlots];
            }
        }

        private void OnDisable()
        {
            EditorApplication.update -= Repaint;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawHeader();
            EditorGUILayout.Space(10);

            DrawStatusSummary();
            EditorGUILayout.Space(10);

            DrawTrafficSlotGrid();
            EditorGUILayout.Space(10);

            DrawConfiguration();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawHeader()
        {
            var style = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14,
                alignment = TextAnchor.MiddleCenter
            };
            EditorGUILayout.LabelField("X-Plane Traffic Provider", style);
        }

        private void DrawStatusSummary()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            int activeSlots = 0;
            if (_provider != null && _provider.TrafficSlots != null)
            {
                foreach (var slot in _provider.TrafficSlots)
                {
                    if (slot != null && slot.isActive)
                    {
                        activeSlots++;
                    }
                }
            }

            EditorGUILayout.BeginHorizontal();
            
            var isConnected = _provider != null && _provider.IsConnected;
            var color = isConnected ? Color.green : Color.red;
            var rect = GUILayoutUtility.GetRect(12, 12);
            EditorGUI.DrawRect(rect, color);
            
            EditorGUILayout.LabelField(
                isConnected ? "Traffic Data Active" : "Traffic Data Inactive",
                isConnected ? EditorStyles.boldLabel : EditorStyles.miniLabel
            );
            
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField($"{activeSlots}/{MaxTrafficSlots} slots active", EditorStyles.miniLabel);
            
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawTrafficSlotGrid()
        {
            EditorGUILayout.LabelField("Traffic Slots", EditorStyles.boldLabel);

            const int columns = 4;
            const float slotWidth = 70f;
            const float slotHeight = 50f;

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            EditorGUILayout.BeginVertical();

            if (_provider != null && _provider.TrafficSlots != null)
            {
                for (int i = 0; i < MaxTrafficSlots; i++)
                {
                    if (i % columns == 0 && i > 0)
                    {
                        EditorGUILayout.EndHorizontal();
                        EditorGUILayout.BeginHorizontal();
                        GUILayout.FlexibleSpace();
                    }

                    var slot = i < _provider.TrafficSlots.Length ? _provider.TrafficSlots[i] : null;
                    DrawTrafficSlot(i, slot, slotWidth, slotHeight);
                }
            }
            else
            {
                for (int i = 0; i < MaxTrafficSlots; i++)
                {
                    if (i % columns == 0 && i > 0)
                    {
                        EditorGUILayout.EndHorizontal();
                        EditorGUILayout.BeginHorizontal();
                        GUILayout.FlexibleSpace();
                    }

                    DrawTrafficSlot(i, null, slotWidth, slotHeight);
                }
            }

            EditorGUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            EditorGUILayout.LabelField("Slot Details", EditorStyles.boldLabel);
            
            var scroll = EditorGUILayout.BeginScrollView(Vector2.zero, GUILayout.Height(200));
            
            if (_provider != null && _provider.TrafficSlots != null)
            {
                for (int i = 0; i < _provider.TrafficSlots.Length; i++)
                {
                    _slotExpanded[i] = EditorGUILayout.Foldout(_slotExpanded[i], $"Slot {i:D2}", true);
                    
                    if (!_slotExpanded[i]) continue;

                    var slot = _provider.TrafficSlots[i];
                    if (slot != null)
                    {
                        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                        
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.Toggle(slot.isActive, GUILayout.Width(20));
                        EditorGUILayout.LabelField("Active", GUILayout.Width(50));
                        EditorGUILayout.EndHorizontal();

                        DrawSlotDataField("Callsign", slot.callsign ?? "N/A");
                        DrawSlotDataField("Altitude", $"{slot.altitude:F0} ft");
                        DrawSlotDataField("Speed", $"{slot.speed:F0} kts");
                        DrawSlotDataField("Heading", $"{slot.heading:F0}°");
                        DrawSlotDataField("Lat", $"{slot.latitude:F4}");
                        DrawSlotDataField("Lon", $"{slot.longitude:F4}");
                        
                        EditorGUILayout.EndVertical();
                    }
                }
            }
            
            EditorGUILayout.EndScrollView();
        }

        private void DrawTrafficSlot(int index, XPlaneTrafficSlot slot, float width, float height)
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(width), GUILayout.Height(height));

            var isActive = slot != null && slot.isActive;
            var hasData = slot != null && !string.IsNullOrEmpty(slot.callsign);

            var boxStyle = new GUIStyle(EditorStyles.helpBox)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 9
            };

            var oldColor = GUI.backgroundColor;
            GUI.backgroundColor = isActive ? Color.green : Color.gray;
            
            EditorGUILayout.BeginVertical(boxStyle, GUILayout.Width(width - 5), GUILayout.Height(height - 5));
            
            EditorGUILayout.LabelField($"{index:D2}", EditorStyles.boldLabel);
            
            if (hasData)
            {
                EditorGUILayout.LabelField(slot.callsign, EditorStyles.miniLabel);
                EditorGUILayout.LabelField($"{slot.altitude:F0}", EditorStyles.miniLabel);
            }
            else
            {
                EditorGUILayout.LabelField("Empty", EditorStyles.miniLabel);
            }
            
            EditorGUILayout.EndVertical();
            
            GUI.backgroundColor = oldColor;

            EditorGUILayout.EndVertical();
        }

        private void DrawSlotDataField(string label, string value)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(70));
            EditorGUILayout.LabelField(value);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawConfiguration()
        {
            EditorGUILayout.LabelField("Configuration", EditorStyles.boldLabel);

            var refreshIntervalProp = serializedObject.FindProperty("refreshInterval");
            if (refreshIntervalProp != null)
            {
                EditorGUILayout.PropertyField(refreshIntervalProp, new GUIContent("Refresh Interval (s)"));
            }

            var maxSlotsProp = serializedObject.FindProperty("maxSlots");
            if (maxSlotsProp != null)
            {
                EditorGUILayout.PropertyField(maxSlotsProp, new GUIContent("Max Slots"));
            }

            var filterProp = serializedObject.FindProperty("aircraftFilter");
            if (filterProp != null)
            {
                EditorGUILayout.PropertyField(filterProp, new GUIContent("Aircraft Filter"));
            }
        }
    }

    #endregion

    #region Data Structures (for editor reference)

    [Serializable]
    public class XPlaneTrafficSlot
    {
        public bool isActive;
        public string callsign;
        public float altitude;
        public float speed;
        public float heading;
        public float latitude;
        public float longitude;
    }

    #endregion
}
#endif
