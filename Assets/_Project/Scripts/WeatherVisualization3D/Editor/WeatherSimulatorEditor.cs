using UnityEngine;
using UnityEditor;
using System.Linq;

namespace WeatherVisualization3D
{
    /// <summary>
    /// Custom editor for WeatherSimulator with testing controls and visualization options.
    /// Provides real-time testing and debugging capabilities for the weather system.
    /// </summary>
    [CustomEditor(typeof(WeatherSimulator))]
    public class WeatherSimulatorEditor : UnityEditor.Editor
    {
        #region Serialized Properties
        
        private SerializedProperty activeScenarioProp;
        private SerializedProperty defaultScenarioTypeProp;
        private SerializedProperty simulationEnabledProp;
        private SerializedProperty timeScaleProp;
        private SerializedProperty updateFrequencyProp;
        private SerializedProperty volumeUpdateFrequencyProp;
        private SerializedProperty volumeResolutionProp;
        private SerializedProperty volumeWorldSizeProp;
        private SerializedProperty volumeOriginProp;
        private SerializedProperty autoRespawnCellsProp;
        private SerializedProperty maxActiveCellsProp;
        private SerializedProperty showDebugInfoProp;
        private SerializedProperty drawCellGizmosProp;
        
        #endregion

        #region State
        
        private bool showScenarioFoldout = true;
        private bool showSimulationFoldout = true;
        private bool showVolumeFoldout = true;
        private bool showCellFoldout = true;
        private bool showDebugFoldout = true;
        private bool showTestControlsFoldout = true;
        private bool showActiveCellsFoldout = false;
        
        private Vector2 cellListScroll;
        private IntensityLevel spawnIntensity = IntensityLevel.Moderate;
        private Vector2 spawnPosition = Vector2.zero;
        
        #endregion

        private void OnEnable()
        {
            activeScenarioProp = serializedObject.FindProperty("activeScenario");
            defaultScenarioTypeProp = serializedObject.FindProperty("defaultScenarioType");
            simulationEnabledProp = serializedObject.FindProperty("simulationEnabled");
            timeScaleProp = serializedObject.FindProperty("timeScale");
            updateFrequencyProp = serializedObject.FindProperty("updateFrequency");
            volumeUpdateFrequencyProp = serializedObject.FindProperty("volumeUpdateFrequency");
            volumeResolutionProp = serializedObject.FindProperty("volumeResolution");
            volumeWorldSizeProp = serializedObject.FindProperty("volumeWorldSize");
            volumeOriginProp = serializedObject.FindProperty("volumeOrigin");
            autoRespawnCellsProp = serializedObject.FindProperty("autoRespawnCells");
            maxActiveCellsProp = serializedObject.FindProperty("maxActiveCells");
            showDebugInfoProp = serializedObject.FindProperty("showDebugInfo");
            drawCellGizmosProp = serializedObject.FindProperty("drawCellGizmos");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            
            var simulator = (WeatherSimulator)target;
            
            DrawHeader();
            
            EditorGUILayout.Space(5);
            
            // Scenario Configuration
            showScenarioFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(showScenarioFoldout, "Scenario Configuration");
            if (showScenarioFoldout)
            {
                DrawScenarioSection();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
            
            // Simulation Settings
            showSimulationFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(showSimulationFoldout, "Simulation Settings");
            if (showSimulationFoldout)
            {
                DrawSimulationSection();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
            
            // Volume Configuration
            showVolumeFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(showVolumeFoldout, "Volume Configuration");
            if (showVolumeFoldout)
            {
                DrawVolumeSection();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
            
            // Cell Management
            showCellFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(showCellFoldout, "Cell Management");
            if (showCellFoldout)
            {
                DrawCellSection();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
            
            // Debug Settings
            showDebugFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(showDebugFoldout, "Debug");
            if (showDebugFoldout)
            {
                DrawDebugSection();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
            
            // Test Controls (only in Play mode)
            if (Application.isPlaying)
            {
                showTestControlsFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(showTestControlsFoldout, "⚡ Test Controls");
                if (showTestControlsFoldout)
                {
                    DrawTestControls(simulator);
                }
                EditorGUILayout.EndFoldoutHeaderGroup();
                
                // Active Cells List
                showActiveCellsFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(showActiveCellsFoldout, "Active Storm Cells");
                if (showActiveCellsFoldout)
                {
                    DrawActiveCellsList(simulator);
                }
                EditorGUILayout.EndFoldoutHeaderGroup();
            }
            
            serializedObject.ApplyModifiedProperties();
            
            // Repaint in play mode for real-time updates
            if (Application.isPlaying)
            {
                Repaint();
            }
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            var headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14,
                alignment = TextAnchor.MiddleCenter
            };
            
            EditorGUILayout.LabelField("☁️ Weather Simulator", headerStyle);
            
            var simulator = (WeatherSimulator)target;
            
            if (Application.isPlaying)
            {
                var stats = simulator.GetStats();
                
                EditorGUILayout.Space(5);
                
                EditorGUILayout.BeginHorizontal();
                
                // Status indicator
                var statusStyle = new GUIStyle(EditorStyles.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold
                };
                
                string statusText;
                Color statusColor;
                
                if (stats.IsRunning)
                {
                    statusText = "▶ RUNNING";
                    statusColor = new Color(0.3f, 0.8f, 0.3f);
                }
                else
                {
                    statusText = "⏸ PAUSED";
                    statusColor = new Color(0.8f, 0.6f, 0.2f);
                }
                
                var prevColor = GUI.color;
                GUI.color = statusColor;
                EditorGUILayout.LabelField(statusText, statusStyle);
                GUI.color = prevColor;
                
                EditorGUILayout.EndHorizontal();
                
                // Stats
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Cells: {stats.ActiveCellCount}", GUILayout.Width(80));
                EditorGUILayout.LabelField($"Time: {stats.SimulationTime:F1}s", GUILayout.Width(100));
                EditorGUILayout.LabelField($"Scale: {stats.TimeScale:F1}x", GUILayout.Width(80));
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.LabelField($"Scenario: {stats.ScenarioName}", EditorStyles.centeredGreyMiniLabel);
            }
            else
            {
                EditorGUILayout.HelpBox("Enter Play mode to test the simulation", MessageType.Info);
            }
            
            EditorGUILayout.EndVertical();
        }

        private void DrawScenarioSection()
        {
            EditorGUI.indentLevel++;
            
            EditorGUILayout.PropertyField(activeScenarioProp, new GUIContent("Active Scenario"));
            EditorGUILayout.PropertyField(defaultScenarioTypeProp, new GUIContent("Default Type"));
            
            EditorGUILayout.Space(5);
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Quick Scenarios:", GUILayout.Width(100));
            
            if (GUILayout.Button("Scattered", EditorStyles.miniButtonLeft))
            {
                defaultScenarioTypeProp.enumValueIndex = (int)ScenarioType.ScatteredShowers;
                if (Application.isPlaying)
                    ((WeatherSimulator)target).SetScenarioByType(ScenarioType.ScatteredShowers);
            }
            if (GUILayout.Button("Thunder", EditorStyles.miniButtonMid))
            {
                defaultScenarioTypeProp.enumValueIndex = (int)ScenarioType.ThunderstormCells;
                if (Application.isPlaying)
                    ((WeatherSimulator)target).SetScenarioByType(ScenarioType.ThunderstormCells);
            }
            if (GUILayout.Button("Squall", EditorStyles.miniButtonMid))
            {
                defaultScenarioTypeProp.enumValueIndex = (int)ScenarioType.SquallLine;
                if (Application.isPlaying)
                    ((WeatherSimulator)target).SetScenarioByType(ScenarioType.SquallLine);
            }
            if (GUILayout.Button("Supercell", EditorStyles.miniButtonRight))
            {
                defaultScenarioTypeProp.enumValueIndex = (int)ScenarioType.Supercell;
                if (Application.isPlaying)
                    ((WeatherSimulator)target).SetScenarioByType(ScenarioType.Supercell);
            }
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUI.indentLevel--;
        }

        private void DrawSimulationSection()
        {
            EditorGUI.indentLevel++;
            
            EditorGUILayout.PropertyField(simulationEnabledProp, new GUIContent("Enabled"));
            EditorGUILayout.PropertyField(timeScaleProp, new GUIContent("Time Scale"));
            EditorGUILayout.PropertyField(updateFrequencyProp, new GUIContent("Update Frequency (Hz)"));
            EditorGUILayout.PropertyField(volumeUpdateFrequencyProp, new GUIContent("Volume Update Freq (Hz)"));
            
            EditorGUI.indentLevel--;
        }

        private void DrawVolumeSection()
        {
            EditorGUI.indentLevel++;
            
            EditorGUILayout.PropertyField(volumeResolutionProp, new GUIContent("Resolution"));
            EditorGUILayout.PropertyField(volumeWorldSizeProp, new GUIContent("World Size"));
            EditorGUILayout.PropertyField(volumeOriginProp, new GUIContent("Origin"));
            
            // Memory estimate
            var res = volumeResolutionProp.vector3IntValue;
            float memoryMB = (res.x * res.y * res.z * 4f) / (1024f * 1024f);
            EditorGUILayout.HelpBox($"Estimated Volume Memory: {memoryMB:F2} MB", MessageType.None);
            
            EditorGUI.indentLevel--;
        }

        private void DrawCellSection()
        {
            EditorGUI.indentLevel++;
            
            EditorGUILayout.PropertyField(autoRespawnCellsProp, new GUIContent("Auto Respawn"));
            EditorGUILayout.PropertyField(maxActiveCellsProp, new GUIContent("Max Active Cells"));
            
            EditorGUI.indentLevel--;
        }

        private void DrawDebugSection()
        {
            EditorGUI.indentLevel++;
            
            EditorGUILayout.PropertyField(showDebugInfoProp, new GUIContent("Show Debug Info"));
            EditorGUILayout.PropertyField(drawCellGizmosProp, new GUIContent("Draw Cell Gizmos"));
            
            EditorGUI.indentLevel--;
        }

        private void DrawTestControls(WeatherSimulator simulator)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            // Playback controls
            EditorGUILayout.LabelField("Playback Controls", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button(simulator.IsPaused ? "▶ Resume" : "⏸ Pause"))
            {
                simulator.IsPaused = !simulator.IsPaused;
            }
            
            if (GUILayout.Button("⟳ Reset"))
            {
                simulator.ResetSimulation();
            }
            
            if (GUILayout.Button("⏩ Step 1s"))
            {
                simulator.StepSimulation(1f);
            }
            
            if (GUILayout.Button("⏩ Step 10s"))
            {
                simulator.StepSimulation(10f);
            }
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(10);
            
            // Time scale slider
            EditorGUILayout.LabelField("Time Scale");
            float newTimeScale = EditorGUILayout.Slider(simulator.TimeScale, 0.1f, 100f);
            if (!Mathf.Approximately(newTimeScale, simulator.TimeScale))
            {
                simulator.TimeScale = newTimeScale;
            }
            
            EditorGUILayout.Space(10);
            
            // Spawn controls
            EditorGUILayout.LabelField("Spawn Storm Cell", EditorStyles.boldLabel);
            
            spawnIntensity = (IntensityLevel)EditorGUILayout.EnumPopup("Intensity", spawnIntensity);
            spawnPosition = EditorGUILayout.Vector2Field("Position (X, Z)", spawnPosition);
            
            if (GUILayout.Button("⚡ Spawn Cell at Position"))
            {
                var cell = simulator.SpawnCellAt(spawnPosition, spawnIntensity);
                if (cell != null)
                {
                    Debug.Log($"[WeatherSimulatorEditor] Spawned cell: {cell}");
                }
            }
            
            if (GUILayout.Button("⚡ Spawn Cell at Random Position"))
            {
                var worldSize = volumeWorldSizeProp.vector3Value;
                var origin = volumeOriginProp.vector3Value;
                
                Vector2 randomPos = new Vector2(
                    origin.x + Random.Range(-worldSize.x * 0.4f, worldSize.x * 0.4f),
                    origin.z + Random.Range(-worldSize.z * 0.4f, worldSize.z * 0.4f)
                );
                
                var cell = simulator.SpawnCellAt(randomPos, spawnIntensity);
                if (cell != null)
                {
                    Debug.Log($"[WeatherSimulatorEditor] Spawned cell: {cell}");
                }
            }
            
            EditorGUILayout.EndVertical();
        }

        private void DrawActiveCellsList(WeatherSimulator simulator)
        {
            var cells = simulator.GetActiveCells();
            
            if (cells == null || cells.Count == 0)
            {
                EditorGUILayout.HelpBox("No active storm cells", MessageType.Info);
                return;
            }
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            cellListScroll = EditorGUILayout.BeginScrollView(cellListScroll, GUILayout.MaxHeight(200));
            
            int index = 0;
            foreach (var cell in cells)
            {
                if (cell == null || !cell.IsActive) continue;
                
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                
                // Intensity color indicator
                var prevBg = GUI.backgroundColor;
                GUI.backgroundColor = GetIntensityColor(cell.Intensity);
                GUILayout.Box("", GUILayout.Width(20), GUILayout.Height(20));
                GUI.backgroundColor = prevBg;
                
                // Cell info
                EditorGUILayout.BeginVertical();
                EditorGUILayout.LabelField($"Cell {index + 1}: {cell.Intensity}", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"Pos: ({cell.Position.x:F0}, {cell.Position.y:F0}) | Radius: {cell.Radius:F0}m");
                EditorGUILayout.LabelField($"Alt: {cell.BaseAltitude:F0}ft - {cell.TopAltitude:F0}ft | Life: {(cell.Lifetime - cell.Age):F0}s");
                EditorGUILayout.EndVertical();
                
                EditorGUILayout.EndHorizontal();
                
                index++;
            }
            
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private Color GetIntensityColor(IntensityLevel intensity)
        {
            return intensity switch
            {
                IntensityLevel.Light => Color.green,
                IntensityLevel.Moderate => Color.yellow,
                IntensityLevel.Heavy => new Color(1f, 0.5f, 0f),
                IntensityLevel.Extreme => Color.red,
                _ => Color.gray
            };
        }
    }
}
