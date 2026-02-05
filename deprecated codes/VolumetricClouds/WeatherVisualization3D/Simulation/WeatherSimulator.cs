using UnityEngine;
using System;
using System.Collections.Generic;

namespace WeatherVisualization3D
{
    /// <summary>
    /// Main weather simulation controller that generates procedural weather data.
    /// Implements ISimulationDataSource to provide data to the visualization system.
    /// </summary>
    public class WeatherSimulator : MonoBehaviour, ISimulationDataSource
    {
        #region Inspector Fields
        
        [Header("Scenario Configuration")]
        [Tooltip("Active weather scenario preset")]
        [SerializeField] private WeatherScenarioPreset activeScenario;
        
        [Tooltip("Auto-generate scenario if none assigned")]
        [SerializeField] private ScenarioType defaultScenarioType = ScenarioType.ThunderstormCells;
        
        [Header("Simulation Settings")]
        [Tooltip("Enable simulation updates")]
        [SerializeField] private bool simulationEnabled = true;
        
        [Tooltip("Time scale multiplier (1 = realtime, 10 = 10x faster)")]
        [Range(0.1f, 100f)]
        [SerializeField] private float timeScale = 1f;
        
        [Tooltip("Update frequency in Hz")]
        [Range(1f, 60f)]
        [SerializeField] private float updateFrequency = 10f;
        
        [Tooltip("Volume data update frequency in Hz (lower for performance)")]
        [Range(0.5f, 10f)]
        [SerializeField] private float volumeUpdateFrequency = 2f;
        
        [Header("Volume Configuration")]
        [Tooltip("Volume resolution (X, Y, Z voxels)")]
        [SerializeField] private Vector3Int volumeResolution = new Vector3Int(64, 32, 64);
        
        [Tooltip("Volume world size in units")]
        [SerializeField] private Vector3 volumeWorldSize = new Vector3(50000f, 50000f, 50000f);
        
        [Tooltip("Volume world origin")]
        [SerializeField] private Vector3 volumeOrigin = Vector3.zero;
        
        [Header("Cell Management")]
        [Tooltip("Automatically respawn expired cells")]
        [SerializeField] private bool autoRespawnCells = true;
        
        [Tooltip("Maximum active cells")]
        [Range(1, 50)]
        [SerializeField] private int maxActiveCells = 20;
        
        [Header("Debug")]
        [SerializeField] private bool showDebugInfo = false;
        [SerializeField] private bool drawCellGizmos = true;
        
        #endregion

        #region Private Fields
        
        private List<SimulatedStormCell> activeCells = new List<SimulatedStormCell>();
        private WeatherVolumeData volumeData;
        private float simulationTime = 0f;
        private float lastUpdateTime = 0f;
        private float lastVolumeUpdateTime = 0f;
        private bool isInitialized = false;
        private DataSourceStatus currentStatus = DataSourceStatus.Uninitialized;
        private bool isPaused = false;
        
        // Position tracking
        private float currentLatitude = 0f;
        private float currentLongitude = 0f;
        private float currentAltitude = 0f;
        private float currentRange = 80f;
        private float currentHeading = 0f;
        
        #endregion

        #region IWeatherDataSource Implementation
        
        public string SourceName => "Procedural Weather Simulator";
        public DataSourceStatus Status => currentStatus;
        public bool IsDataValid => isInitialized && volumeData != null;
        public WeatherVolumeData CurrentData => volumeData;
        
        public event Action<WeatherVolumeData> OnDataUpdated;
        public event Action<DataSourceStatus> OnStatusChanged;
        
        public void Initialize()
        {
            try
            {
                SetStatus(DataSourceStatus.Initializing);
                
                // Create volume data container
                volumeData = new WeatherVolumeData(
                    volumeResolution.x,
                    volumeResolution.y,
                    volumeResolution.z,
                    volumeOrigin,
                    volumeWorldSize
                );
                
                // Load or create scenario
                if (activeScenario == null)
                {
                    CreateDefaultScenario();
                }
                
                // Spawn initial cells
                SpawnInitialCells();
                
                isInitialized = true;
                SetStatus(DataSourceStatus.Active);
                
                Debug.Log($"[WeatherSimulator] Initialized with {activeCells.Count} cells, scenario: {activeScenario?.scenarioName ?? "Default"}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[WeatherSimulator] Initialization failed: {ex.Message}");
                SetStatus(DataSourceStatus.Error);
            }
        }
        
        public void StartUpdates()
        {
            simulationEnabled = true;
            isPaused = false;
            SetStatus(DataSourceStatus.Active);
        }
        
        public void StopUpdates()
        {
            simulationEnabled = false;
            SetStatus(DataSourceStatus.Paused);
        }
        
        public void ForceRefresh()
        {
            if (isInitialized)
            {
                UpdateVolumeData();
            }
        }
        
        public void SetPosition(float latitude, float longitude, float altitudeFt)
        {
            currentLatitude = latitude;
            currentLongitude = longitude;
            currentAltitude = altitudeFt;
            // Could recenter volume based on position
        }
        
        public void SetRange(float rangeNM)
        {
            currentRange = rangeNM;
            // Could adjust volume size based on range
        }
        
        public void SetHeading(float headingDegrees)
        {
            currentHeading = headingDegrees;
        }
        
        #endregion

        #region ISimulationDataSource Implementation
        
        public float TimeScale
        {
            get => timeScale;
            set => timeScale = Mathf.Clamp(value, 0.1f, 100f);
        }
        
        public bool IsPaused
        {
            get => isPaused;
            set
            {
                isPaused = value;
                if (isPaused)
                    SetStatus(DataSourceStatus.Paused);
                else if (isInitialized)
                    SetStatus(DataSourceStatus.Active);
            }
        }
        
        public void StepSimulation(float seconds)
        {
            if (!isInitialized) return;
            
            UpdateCells(seconds);
            UpdateVolumeData();
        }
        
        public void ResetSimulation()
        {
            simulationTime = 0f;
            activeCells.Clear();
            SpawnInitialCells();
            UpdateVolumeData();
            Debug.Log("[WeatherSimulator] Simulation reset");
        }
        
        public void LoadScenario(WeatherScenarioPreset scenario)
        {
            SetScenario(scenario);
        }
        
        #endregion

        #region Unity Lifecycle
        
        private void Awake()
        {
            if (!isInitialized)
            {
                Initialize();
            }
        }
        
        private void OnEnable()
        {
            if (!isInitialized)
            {
                Initialize();
            }
            StartUpdates();
        }
        
        private void OnDisable()
        {
            StopUpdates();
        }
        
        private void OnDestroy()
        {
            activeCells.Clear();
            volumeData?.Dispose();
            volumeData = null;
            isInitialized = false;
            SetStatus(DataSourceStatus.Disposed);
        }
        
        private void Update()
        {
            if (!simulationEnabled || !isInitialized || isPaused)
                return;
            
            float deltaTime = Time.deltaTime * timeScale;
            simulationTime += deltaTime;
            
            // Update cells at specified frequency
            float updateInterval = 1f / updateFrequency;
            if (Time.time - lastUpdateTime >= updateInterval)
            {
                UpdateCells(deltaTime);
                lastUpdateTime = Time.time;
            }
            
            // Update volume data at lower frequency
            float volumeInterval = 1f / volumeUpdateFrequency;
            if (Time.time - lastVolumeUpdateTime >= volumeInterval)
            {
                UpdateVolumeData();
                lastVolumeUpdateTime = Time.time;
            }
        }
        
        #endregion

        #region Simulation Logic
        
        private void CreateDefaultScenario()
        {
            switch (defaultScenarioType)
            {
                case ScenarioType.ScatteredShowers:
                    activeScenario = WeatherScenarioPreset.CreateScatteredShowers();
                    break;
                case ScenarioType.ThunderstormCells:
                    activeScenario = WeatherScenarioPreset.CreateThunderstormCells();
                    break;
                case ScenarioType.SquallLine:
                    activeScenario = WeatherScenarioPreset.CreateSquallLine();
                    break;
                case ScenarioType.Supercell:
                    activeScenario = WeatherScenarioPreset.CreateSupercell();
                    break;
                default:
                    activeScenario = WeatherScenarioPreset.CreateThunderstormCells();
                    break;
            }
        }
        
        private void SpawnInitialCells()
        {
            if (activeScenario == null) return;
            
            activeCells.Clear();
            
            int targetCount = Mathf.Min(activeScenario.cellCount, maxActiveCells);
            List<Vector2> usedPositions = new List<Vector2>();
            
            for (int i = 0; i < targetCount; i++)
            {
                Vector2 position = GenerateValidPosition(usedPositions);
                var cell = SimulatedStormCell.CreateFromPreset(activeScenario, position);
                
                // Stagger initial ages for variety
                float initialAge = UnityEngine.Random.value * cell.Lifetime * 0.5f;
                for (int j = 0; j < Mathf.FloorToInt(initialAge); j++)
                {
                    cell.Update(1f, activeScenario.dynamicIntensity, activeScenario.intensityChangeRate);
                }
                
                cell.OnExpired += OnCellExpired;
                activeCells.Add(cell);
                usedPositions.Add(position);
                
                if (showDebugInfo)
                {
                    Debug.Log($"[WeatherSimulator] Spawned {cell}");
                }
            }
        }
        
        private Vector2 GenerateValidPosition(List<Vector2> existingPositions)
        {
            Vector2 spawnArea = activeScenario?.spawnAreaSize ?? new Vector2(50000f, 50000f);
            float minSpacing = activeScenario?.minimumCellSpacing ?? 5000f;
            
            for (int attempt = 0; attempt < 50; attempt++)
            {
                Vector2 position = new Vector2(
                    volumeOrigin.x + UnityEngine.Random.Range(-spawnArea.x * 0.5f, spawnArea.x * 0.5f),
                    volumeOrigin.z + UnityEngine.Random.Range(-spawnArea.y * 0.5f, spawnArea.y * 0.5f)
                );
                
                bool valid = true;
                foreach (var existingPos in existingPositions)
                {
                    if (Vector2.Distance(position, existingPos) < minSpacing)
                    {
                        valid = false;
                        break;
                    }
                }
                
                foreach (var cell in activeCells)
                {
                    if (Vector2.Distance(position, cell.Position) < minSpacing)
                    {
                        valid = false;
                        break;
                    }
                }
                
                if (valid) return position;
            }
            
            return new Vector2(
                volumeOrigin.x + UnityEngine.Random.Range(-spawnArea.x * 0.5f, spawnArea.x * 0.5f),
                volumeOrigin.z + UnityEngine.Random.Range(-spawnArea.y * 0.5f, spawnArea.y * 0.5f)
            );
        }
        
        private void UpdateCells(float deltaTime)
        {
            for (int i = activeCells.Count - 1; i >= 0; i--)
            {
                var cell = activeCells[i];
                cell.Update(deltaTime, 
                    activeScenario?.dynamicIntensity ?? true, 
                    activeScenario?.intensityChangeRate ?? 0.3f);
            }
            
            activeCells.RemoveAll(c => c.IsExpired);
        }
        
        private void OnCellExpired(SimulatedStormCell cell)
        {
            if (showDebugInfo)
            {
                Debug.Log($"[WeatherSimulator] Cell expired: {cell.DisplayName}");
            }
            
            if (autoRespawnCells && simulationEnabled && activeCells.Count < (activeScenario?.cellCount ?? 5))
            {
                SpawnNewCell();
            }
        }
        
        private void SpawnNewCell()
        {
            if (activeScenario == null || activeCells.Count >= maxActiveCells) return;
            
            List<Vector2> existingPositions = new List<Vector2>();
            foreach (var cell in activeCells)
            {
                existingPositions.Add(cell.Position);
            }
            
            Vector2 position = GenerateValidPosition(existingPositions);
            var newCell = SimulatedStormCell.CreateFromPreset(activeScenario, position);
            newCell.OnExpired += OnCellExpired;
            activeCells.Add(newCell);
            
            if (showDebugInfo)
            {
                Debug.Log($"[WeatherSimulator] Spawned new cell: {newCell}");
            }
        }
        
        private void UpdateVolumeData()
        {
            if (volumeData == null) return;
            
            volumeData.Clear();
            
            foreach (var cell in activeCells)
            {
                if (!cell.IsActive || cell.Radius <= 0) continue;
                RasterizeCellToVolume(cell);
            }
            
            volumeData.UpdateTextures();
            OnDataUpdated?.Invoke(volumeData);
        }
        
        private void RasterizeCellToVolume(SimulatedStormCell cell)
        {
            Bounds cellBounds = cell.GetBounds();
            
            Vector3Int minVoxel = volumeData.WorldToVoxel(cellBounds.min);
            Vector3Int maxVoxel = volumeData.WorldToVoxel(cellBounds.max);
            
            minVoxel = Vector3Int.Max(minVoxel, Vector3Int.zero);
            maxVoxel = Vector3Int.Min(maxVoxel, new Vector3Int(
                volumeData.Resolution.x - 1,
                volumeData.Resolution.y - 1,
                volumeData.Resolution.z - 1
            ));
            
            for (int z = minVoxel.z; z <= maxVoxel.z; z++)
            {
                for (int y = minVoxel.y; y <= maxVoxel.y; y++)
                {
                    for (int x = minVoxel.x; x <= maxVoxel.x; x++)
                    {
                        Vector3 worldPos = volumeData.VoxelToWorld(x, y, z);
                        float density = cell.GetDensityAt3D(worldPos);
                        
                        if (density > 0)
                        {
                            float existingDensity = volumeData.GetDensity(x, y, z);
                            volumeData.SetDensity(x, y, z, Mathf.Max(existingDensity, density));
                            
                            WeatherType weatherType = IntensityToWeatherType(cell.Intensity);
                            volumeData.SetWeatherType(x, y, z, weatherType);
                            
                            float existingTurbulence = volumeData.GetTurbulence(x, y, z);
                            volumeData.SetTurbulence(x, y, z, Mathf.Max(existingTurbulence, cell.TurbulenceIntensity * density));
                        }
                    }
                }
            }
        }
        
        private WeatherType IntensityToWeatherType(IntensityLevel intensity)
        {
            return intensity switch
            {
                IntensityLevel.Light => WeatherType.LightRain,
                IntensityLevel.Moderate => WeatherType.ModerateRain,
                IntensityLevel.Heavy => WeatherType.HeavyRain,
                IntensityLevel.Extreme => WeatherType.Thunderstorm,
                _ => WeatherType.Clear
            };
        }
        
        private void SetStatus(DataSourceStatus status)
        {
            if (currentStatus != status)
            {
                currentStatus = status;
                OnStatusChanged?.Invoke(status);
            }
        }
        
        #endregion

        #region Public API
        
        public void SetScenario(WeatherScenarioPreset scenario)
        {
            activeScenario = scenario;
            activeCells.Clear();
            SpawnInitialCells();
            UpdateVolumeData();
            Debug.Log($"[WeatherSimulator] Switched to scenario: {scenario?.scenarioName ?? "None"}");
        }
        
        public void SetScenarioByType(ScenarioType type)
        {
            defaultScenarioType = type;
            CreateDefaultScenario();
            SetScenario(activeScenario);
        }
        
        public SimulatedStormCell SpawnCellAt(Vector2 position, IntensityLevel intensity)
        {
            if (activeCells.Count >= maxActiveCells)
            {
                Debug.LogWarning("[WeatherSimulator] Cannot spawn cell: max active cells reached");
                return null;
            }
            
            var cell = new SimulatedStormCell(
                position,
                activeScenario?.GetRandomRadius() ?? 5000f,
                intensity,
                activeScenario?.GetRandomLifetime() ?? 300f,
                activeScenario?.GetRandomBaseAltitude() ?? 5000f,
                activeScenario?.GetTopAltitudeForIntensity(intensity) ?? 30000f
            );
            
            if (activeScenario != null)
            {
                cell.Velocity = activeScenario.GetRandomVelocity();
            }
            
            cell.OnExpired += OnCellExpired;
            activeCells.Add(cell);
            
            return cell;
        }
        
        public IReadOnlyList<SimulatedStormCell> GetActiveCells()
        {
            return activeCells.AsReadOnly();
        }
        
        public SimulationStats GetStats()
        {
            return new SimulationStats
            {
                SimulationTime = simulationTime,
                TimeScale = timeScale,
                ActiveCellCount = activeCells.Count,
                TotalCellsSpawned = -1,
                IsRunning = simulationEnabled && !isPaused,
                ScenarioName = activeScenario?.scenarioName ?? "None"
            };
        }
        
        #endregion

        #region Debug Visualization
        
        private void OnDrawGizmos()
        {
            if (!drawCellGizmos) return;
            
            Gizmos.color = new Color(0.5f, 0.5f, 1f, 0.3f);
            Gizmos.DrawWireCube(volumeOrigin + volumeWorldSize * 0.5f, volumeWorldSize);
            
            foreach (var cell in activeCells)
            {
                if (cell == null || !cell.IsActive) continue;
                
                Color cellColor = cell.Intensity switch
                {
                    IntensityLevel.Light => Color.green,
                    IntensityLevel.Moderate => Color.yellow,
                    IntensityLevel.Heavy => new Color(1f, 0.5f, 0f),
                    IntensityLevel.Extreme => Color.red,
                    _ => Color.gray
                };
                cellColor.a = cell.Opacity * 0.5f;
                
                Gizmos.color = cellColor;
                
                Vector3 center = new Vector3(cell.Position.x, cell.BaseAltitude, cell.Position.y);
                DrawGizmoCircle(center, cell.Radius, 32);
                
                Vector3 topCenter = new Vector3(cell.Position.x, cell.TopAltitude, cell.Position.y);
                DrawGizmoCircle(topCenter, cell.Radius * (1f + cell.AnvilSpread), 32);
                
                Gizmos.DrawLine(center + Vector3.right * cell.Radius, topCenter + Vector3.right * cell.Radius);
                Gizmos.DrawLine(center - Vector3.right * cell.Radius, topCenter - Vector3.right * cell.Radius);
                Gizmos.DrawLine(center + Vector3.forward * cell.Radius, topCenter + Vector3.forward * cell.Radius);
                Gizmos.DrawLine(center - Vector3.forward * cell.Radius, topCenter - Vector3.forward * cell.Radius);
                
                Gizmos.color = Color.cyan;
                Vector3 velocityEnd = center + new Vector3(cell.Velocity.x, 0f, cell.Velocity.y) * 100f;
                Gizmos.DrawLine(center, velocityEnd);
            }
        }
        
        private void DrawGizmoCircle(Vector3 center, float radius, int segments)
        {
            float angleStep = 360f / segments;
            Vector3 prevPoint = center + new Vector3(radius, 0f, 0f);
            
            for (int i = 1; i <= segments; i++)
            {
                float angle = i * angleStep * Mathf.Deg2Rad;
                Vector3 newPoint = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                Gizmos.DrawLine(prevPoint, newPoint);
                prevPoint = newPoint;
            }
        }
        
        #if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (showDebugInfo && activeCells != null)
            {
                foreach (var cell in activeCells)
                {
                    if (cell == null) continue;
                    
                    Vector3 labelPos = new Vector3(cell.Position.x, cell.TopAltitude + 2000f, cell.Position.y);
                    UnityEditor.Handles.Label(labelPos, cell.ToString());
                }
            }
        }
        #endif
        
        #endregion
    }

    /// <summary>
    /// Simulation statistics container
    /// </summary>
    [Serializable]
    public struct SimulationStats
    {
        public float SimulationTime;
        public float TimeScale;
        public int ActiveCellCount;
        public int TotalCellsSpawned;
        public bool IsRunning;
        public string ScenarioName;
    }
}
