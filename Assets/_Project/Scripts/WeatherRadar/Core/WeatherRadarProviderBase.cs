using System;
using UnityEngine;

namespace WeatherRadar
{
    /// <summary>
    /// Base class for weather radar data providers.
    /// Provides a unified interface for position (altitude, latitude, longitude) that can
    /// be updated from external scripts like aircraft controllers.
    /// 
    /// USAGE FROM EXTERNAL SCRIPTS:
    /// 
    ///   // Find the provider
    ///   var provider = FindObjectOfType<WeatherRadarProviderBase>();
    ///   
    ///   // Update position from your aircraft
    ///   provider.SetAircraftPosition(altitude, latitude, longitude, heading);
    ///   
    ///   // Or set individual values
    ///   provider.Latitude = myAircraft.latitude;
    ///   provider.Longitude = myAircraft.longitude;
    ///   provider.Altitude = myAircraft.altitude;
    ///   provider.Heading = myAircraft.heading;
    /// 
    /// UPDATE MODES:
    /// - Sweep-Triggered (recommended): Updates occur when RefreshData() is called by
    ///   the WeatherRadarPanel upon sweep completion. Set autoUpdate = false.
    /// - Automatic: Updates occur at updateInterval. Set autoUpdate = true.
    /// </summary>
    public abstract class WeatherRadarProviderBase : MonoBehaviour, IWeatherRadarDataProvider
    {
        [Header("Aircraft Position")]
        [Tooltip("Aircraft altitude in feet MSL")]
        [SerializeField] protected float altitude = 10000f;
        
        [Tooltip("Aircraft latitude in decimal degrees")]
        [SerializeField] protected float latitude = 32.9f;
        
        [Tooltip("Aircraft longitude in decimal degrees")]
        [SerializeField] protected float longitude = -97.0f;
        
        [Tooltip("Aircraft heading in degrees (0-360)")]
        [SerializeField] protected float heading = 0f;

        [Header("Provider Settings")]
        [SerializeField] protected bool activateOnStart = true;
        
        [Tooltip("If true, provider updates automatically at updateInterval. If false, updates only when RefreshData() is called.")]
        [SerializeField] protected bool autoUpdate = false;
        
        [Tooltip("Interval for automatic updates (only used if autoUpdate is true)")]
        [SerializeField] protected float updateInterval = 5f;

        [Header("Radar Settings")]
        [SerializeField] protected int textureSize = 512;
        [SerializeField] protected float rangeNM = 40f;
        [SerializeField] protected float tiltDegrees = 0f;
        [SerializeField] protected float gainDB = 0f;

        protected Texture2D radarTexture;
        protected float lastUpdateTime;
        protected ProviderStatus status = ProviderStatus.Inactive;
        protected bool isGenerating = false;

        public event Action<Texture2D> OnRadarDataUpdated;
        public event Action<ProviderStatus> OnStatusChanged;
        public event Action<float, float, float> OnPositionChanged;

        #region Public Properties - Accessible from External Scripts

        /// <summary>
        /// Aircraft altitude in feet MSL. Settable from external scripts.
        /// </summary>
        public float Altitude
        {
            get => altitude;
            set
            {
                altitude = value;
                OnPositionChanged?.Invoke(altitude, latitude, longitude);
            }
        }

        /// <summary>
        /// Aircraft latitude in decimal degrees. Settable from external scripts.
        /// </summary>
        public float Latitude
        {
            get => latitude;
            set
            {
                latitude = value;
                OnPositionChanged?.Invoke(altitude, latitude, longitude);
            }
        }

        /// <summary>
        /// Aircraft longitude in decimal degrees. Settable from external scripts.
        /// </summary>
        public float Longitude
        {
            get => longitude;
            set
            {
                longitude = value;
                OnPositionChanged?.Invoke(altitude, latitude, longitude);
            }
        }

        /// <summary>
        /// Aircraft heading in degrees (0-360). Settable from external scripts.
        /// </summary>
        public float Heading
        {
            get => heading;
            set => heading = Mathf.Repeat(value, 360f);
        }

        /// <summary>
        /// Current radar range in nautical miles
        /// </summary>
        public float RangeNM
        {
            get => rangeNM;
            set => rangeNM = Mathf.Clamp(value, 5f, 320f);
        }

        /// <summary>
        /// Current antenna tilt in degrees
        /// </summary>
        public float TiltDegrees
        {
            get => tiltDegrees;
            set => tiltDegrees = Mathf.Clamp(value, -15f, 15f);
        }

        /// <summary>
        /// Current gain offset in dB
        /// </summary>
        public float GainDB
        {
            get => gainDB;
            set => gainDB = Mathf.Clamp(value, -8f, 8f);
        }

        public ProviderStatus Status => status;
        public abstract string ProviderName { get; }
        public bool IsActive => status == ProviderStatus.Active;

        /// <summary>
        /// Current update interval
        /// </summary>
        public float UpdateInterval
        {
            get => updateInterval;
            set => updateInterval = Mathf.Max(1f, value);
        }

        #endregion

        #region Public Methods - For External Scripts

        /// <summary>
        /// Set aircraft position from an external script (e.g., flight controller).
        /// This is the primary method for updating position data.
        /// </summary>
        /// <param name="altitudeFt">Altitude in feet MSL</param>
        /// <param name="lat">Latitude in decimal degrees</param>
        /// <param name="lon">Longitude in decimal degrees</param>
        /// <param name="hdg">Optional heading in degrees (0-360)</param>
        public virtual void SetAircraftPosition(float altitudeFt, float lat, float lon, float hdg = -1)
        {
            altitude = altitudeFt;
            latitude = lat;
            longitude = lon;
            if (hdg >= 0)
            {
                heading = Mathf.Repeat(hdg, 360f);
            }
            OnPositionChanged?.Invoke(altitude, latitude, longitude);
        }

        /// <summary>
        /// Enable or disable automatic updates
        /// </summary>
        public void SetAutoUpdate(bool enabled, float interval = 5f)
        {
            autoUpdate = enabled;
            updateInterval = Mathf.Max(1f, interval);
        }

        /// <summary>
        /// Request immediate data refresh.
        /// This is the primary method called by WeatherRadarPanel on sweep completion.
        /// </summary>
        public virtual void RefreshData()
        {
            if (isGenerating)
            {
                return;
            }

            if (status == ProviderStatus.Active || status == ProviderStatus.Connecting)
            {
                isGenerating = true;
                lastUpdateTime = Time.time;
                GenerateRadarData();
            }
            else if (status == ProviderStatus.Inactive)
            {
                Activate();
            }
        }

        #endregion

        #region Unity Lifecycle

        protected virtual void Awake()
        {
            InitializeTexture();
        }

        protected virtual void Start()
        {
            if (activateOnStart)
            {
                Activate();
            }
        }

        protected virtual void Update()
        {
            // Only auto-update if enabled and not already generating
            if (autoUpdate && status == ProviderStatus.Active && !isGenerating)
            {
                if (Time.time - lastUpdateTime >= updateInterval)
                {
                    lastUpdateTime = Time.time;
                    GenerateRadarData();
                }
            }
        }

        protected virtual void OnDestroy()
        {
            if (radarTexture != null)
            {
                Destroy(radarTexture);
            }
        }

        #endregion

        #region Protected Methods

        protected virtual void InitializeTexture()
        {
            if (radarTexture == null)
            {
                radarTexture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
                radarTexture.filterMode = FilterMode.Bilinear;
                radarTexture.wrapMode = TextureWrapMode.Clamp;
                ClearTexture();
            }
        }

        protected virtual void ClearTexture()
        {
            Color32[] pixels = new Color32[textureSize * textureSize];
            Color32 clear = new Color32(0, 0, 0, 0);
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = clear;
            }
            radarTexture.SetPixels32(pixels);
            radarTexture.Apply();
        }

        protected void SetStatus(ProviderStatus newStatus)
        {
            if (status != newStatus)
            {
                status = newStatus;
                OnStatusChanged?.Invoke(status);
            }
        }

        protected void NotifyDataUpdated()
        {
            isGenerating = false;
            OnRadarDataUpdated?.Invoke(radarTexture);
        }

        /// <summary>
        /// Override this method to implement specific data generation logic
        /// </summary>
        protected abstract void GenerateRadarData();

        #endregion

        #region IWeatherRadarDataProvider Implementation

        public virtual void SetPosition(float lat, float lon)
        {
            latitude = lat;
            longitude = lon;
        }

        public virtual void SetHeading(float headingDeg)
        {
            heading = Mathf.Repeat(headingDeg, 360f);
        }

        public virtual void SetRange(float range)
        {
            rangeNM = Mathf.Clamp(range, 5f, 320f);
        }

        public virtual void SetTilt(float tilt)
        {
            tiltDegrees = Mathf.Clamp(tilt, -15f, 15f);
        }

        public virtual void SetGain(float gain)
        {
            gainDB = Mathf.Clamp(gain, -8f, 8f);
        }

        public virtual void Activate()
        {
            SetStatus(ProviderStatus.Active);
            lastUpdateTime = Time.time;
        }

        public virtual void Deactivate()
        {
            SetStatus(ProviderStatus.Inactive);
        }

        #endregion
    }
}
