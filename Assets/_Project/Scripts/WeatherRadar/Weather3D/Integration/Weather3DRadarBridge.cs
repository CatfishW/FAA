using UnityEngine;
using System;

namespace WeatherRadar.Weather3D
{
    /// <summary>
    /// Bridge component that connects the existing 2D WeatherRadarProviderBase
    /// to the 3D Weather visualization system.
    /// 
    /// Attach this component to enable 3D weather visualization from 2D radar data.
    /// </summary>
    [RequireComponent(typeof(Weather3DManager))]
    public class Weather3DRadarBridge : MonoBehaviour, IWeather3DProvider
    {
        [Header("2D Radar Source")]
        [SerializeField] private WeatherRadarProviderBase radarProvider;
        
        [Header("Conversion Settings")]
        [Tooltip("Scale factor for converting radar coordinates to 3D world")]
        [SerializeField] private float worldScale = 1f;
        
        [Tooltip("How often to convert 2D data to 3D (seconds)")]
        [SerializeField] private float conversionInterval = 1f;
        
        [Header("Debug")]
        [SerializeField] private bool debugMode = false;

        // Components
        private Weather3DManager manager;
        private Weather3DConfig config;
        
        // State
        private Weather3DData currentData;
        private Weather3DProviderStatus status = Weather3DProviderStatus.Inactive;
        private float lastConversionTime;
        private Texture2D cachedRadarTexture;
        
        // Aircraft state
        private Vector3 aircraftPosition;
        private float aircraftHeading;
        private float aircraftAltitude;
        private float rangeNM = 40f;

        // Events
        public event Action<Weather3DData> OnDataUpdated;
        public event Action<Weather3DProviderStatus> OnStatusChanged;

        #region IWeather3DProvider Implementation

        public string ProviderName => "2D Radar Bridge";
        public Weather3DProviderStatus Status => status;
        public bool IsActive => status == Weather3DProviderStatus.Active;
        public Weather3DData CurrentData => currentData;

        public void SetAircraftPosition(Vector3 position, float heading, float altitude)
        {
            aircraftPosition = position;
            aircraftHeading = heading;
            aircraftAltitude = altitude;
        }

        public void SetRange(float range)
        {
            rangeNM = Mathf.Clamp(range, 5f, 320f);
        }

        public void Activate()
        {
            if (status == Weather3DProviderStatus.Active) return;
            
            SetStatus(Weather3DProviderStatus.Connecting);
            
            if (radarProvider != null)
            {
                SetStatus(Weather3DProviderStatus.Active);
                
                // Process any existing data
                if (cachedRadarTexture != null)
                {
                    ProcessRadarTexture(cachedRadarTexture);
                }
            }
            else
            {
                SetStatus(Weather3DProviderStatus.Error);
                Debug.LogWarning("[Weather3DRadarBridge] No radar provider assigned");
            }
        }

        public void Deactivate()
        {
            SetStatus(Weather3DProviderStatus.Inactive);
        }

        public void RefreshData()
        {
            if (cachedRadarTexture != null && status == Weather3DProviderStatus.Active)
            {
                ProcessRadarTexture(cachedRadarTexture);
            }
        }

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            manager = GetComponent<Weather3DManager>();
            
            if (manager != null)
            {
                config = manager.Config;
            }
        }

        private void Start()
        {
            AutoFindProvider();
            SubscribeToRadarProvider();
            
            // Auto-activate if provider is available
            if (radarProvider != null && radarProvider.IsActive)
            {
                Activate();
            }
        }

        private void OnEnable()
        {
            SubscribeToRadarProvider();
        }

        private void OnDisable()
        {
            UnsubscribeFromRadarProvider();
        }

        private void Update()
        {
            if (status != Weather3DProviderStatus.Active) return;
            
            // Sync aircraft position from radar provider
            SyncAircraftPosition();
        }

        #endregion

        #region Radar Provider Integration

        private void AutoFindProvider()
        {
            if (radarProvider == null)
            {
                radarProvider = FindObjectOfType<WeatherRadarProviderBase>();
                
                if (debugMode && radarProvider != null)
                {
                    Debug.Log($"[Weather3DRadarBridge] Auto-found provider: {radarProvider.ProviderName}");
                }
            }
        }

        private void SubscribeToRadarProvider()
        {
            if (radarProvider != null)
            {
                radarProvider.OnRadarDataUpdated += OnRadarDataReceived;
                radarProvider.OnStatusChanged += OnRadarStatusChanged;
                radarProvider.OnPositionChanged += OnRadarPositionChanged;
                
                if (debugMode)
                {
                    Debug.Log($"[Weather3DRadarBridge] Subscribed to {radarProvider.ProviderName}");
                }
            }
        }

        private void UnsubscribeFromRadarProvider()
        {
            if (radarProvider != null)
            {
                radarProvider.OnRadarDataUpdated -= OnRadarDataReceived;
                radarProvider.OnStatusChanged -= OnRadarStatusChanged;
                radarProvider.OnPositionChanged -= OnRadarPositionChanged;
            }
        }

        private void OnRadarDataReceived(Texture2D radarTexture)
        {
            if (radarTexture == null) return;
            
            cachedRadarTexture = radarTexture;
            
            if (status == Weather3DProviderStatus.Active)
            {
                // Throttle conversion to avoid excessive processing
                if (Time.time - lastConversionTime >= conversionInterval)
                {
                    ProcessRadarTexture(radarTexture);
                    lastConversionTime = Time.time;
                }
            }
        }

        private void OnRadarStatusChanged(ProviderStatus newStatus)
        {
            switch (newStatus)
            {
                case ProviderStatus.Active:
                    if (status == Weather3DProviderStatus.Connecting)
                    {
                        Activate();
                    }
                    break;
                    
                case ProviderStatus.Inactive:
                    if (status == Weather3DProviderStatus.Active)
                    {
                        SetStatus(Weather3DProviderStatus.NoData);
                    }
                    break;
                    
                case ProviderStatus.Error:
                    SetStatus(Weather3DProviderStatus.Error);
                    break;
            }
        }

        private void OnRadarPositionChanged(float altitude, float latitude, float longitude)
        {
            // Convert geographic coordinates to world position
            // This is a simplified conversion - in production you'd use proper projection
            aircraftAltitude = altitude;
            aircraftPosition = new Vector3(
                longitude * 111320f * Mathf.Cos(latitude * Mathf.Deg2Rad), // Approximate meters per degree
                altitude * 0.3048f, // Feet to meters
                latitude * 110540f
            );
        }

        private void SyncAircraftPosition()
        {
            if (radarProvider != null)
            {
                aircraftHeading = radarProvider.Heading;
                rangeNM = radarProvider.RangeNM;
                
                // Position is synced via OnRadarPositionChanged event
            }
        }

        #endregion

        #region Data Conversion

        private void ProcessRadarTexture(Texture2D radarTexture)
        {
            if (config == null)
            {
                Debug.LogWarning("[Weather3DRadarBridge] No config available for conversion");
                return;
            }
            
            // Convert 2D radar to 3D data
            currentData = Weather2DTo3DConverter.Convert(
                radarTexture,
                config,
                aircraftPosition,
                aircraftAltitude,
                rangeNM
            );
            
            if (currentData != null)
            {
                currentData.aircraftHeading = aircraftHeading;
                
                // Notify listeners
                OnDataUpdated?.Invoke(currentData);
                
                if (debugMode)
                {
                    Debug.Log($"[Weather3DRadarBridge] Converted: {currentData.weatherCells.Count} cells, " +
                             $"{currentData.cloudLayers.Count} layers");
                }
            }
        }

        #endregion

        #region Utility

        private void SetStatus(Weather3DProviderStatus newStatus)
        {
            if (status != newStatus)
            {
                status = newStatus;
                OnStatusChanged?.Invoke(status);
                
                if (debugMode)
                {
                    Debug.Log($"[Weather3DRadarBridge] Status changed to: {status}");
                }
            }
        }

        /// <summary>
        /// Set the radar provider at runtime
        /// </summary>
        public void SetRadarProvider(WeatherRadarProviderBase provider)
        {
            UnsubscribeFromRadarProvider();
            radarProvider = provider;
            SubscribeToRadarProvider();
            
            if (provider != null && provider.IsActive)
            {
                Activate();
            }
        }

        #endregion

        #region Editor

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Application.isPlaying && radarProvider != null)
            {
                UnsubscribeFromRadarProvider();
                SubscribeToRadarProvider();
            }
        }
#endif

        #endregion
    }
}
