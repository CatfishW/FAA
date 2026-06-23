using UnityEngine;
using System;
using System.Collections.Generic;

namespace Weather3D
{
    /// <summary>
    /// Main manager for the 3D Weather Visualization System (without volumetric clouds).
    /// Manages precipitation, lightning, and intensity pillars.
    /// </summary>
    [DisallowMultipleComponent]
    public class Weather3DManager : MonoBehaviour
    {
        #region Configuration

        [Header("Configuration")]
        [SerializeField] private Weather3DConfig _config;

        [Header("Display Settings")]
        [SerializeField] private Transform _displayOrigin;
        [SerializeField] private float _worldScale = 0.001f;

        [Header("Layer Visibility")]
        [SerializeField] private bool _showIntensityPillars = true;
        [SerializeField] private bool _showLightning = true;
        [SerializeField] private bool _showPrecipitation = true;

        [Header("Debug")]
        [SerializeField] private bool _debugMode = false;

        #endregion

        #region Private Fields

        private List<IWeather3DEffect> _effects = new List<IWeather3DEffect>();
        private Weather3DData _currentData;
        private bool _isInitialized = false;

        #endregion

        #region Properties

        public Weather3DConfig Config => _config;
        public Weather3DData CurrentData => _currentData;
        public bool IsInitialized => _isInitialized;

        public bool ShowIntensityPillars
        {
            get => _showIntensityPillars;
            set { _showIntensityPillars = value; UpdateLayerVisibility(); }
        }

        public bool ShowLightning
        {
            get => _showLightning;
            set { _showLightning = value; UpdateLayerVisibility(); }
        }

        public bool ShowPrecipitation
        {
            get => _showPrecipitation;
            set { _showPrecipitation = value; UpdateLayerVisibility(); }
        }

        #endregion

        #region Events

        public event Action OnInitialized;
        public event Action<Weather3DData> OnDataUpdated;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (_displayOrigin == null)
                _displayOrigin = transform;
        }

        private void Start()
        {
            Initialize();
        }

        #endregion

        #region Initialization

        public void Initialize()
        {
            if (_isInitialized) return;

            FindAndInitializeEffects();
            UpdateLayerVisibility();

            _isInitialized = true;

            if (_debugMode)
                Debug.Log("[Weather3DManager] Initialized successfully");

            OnInitialized?.Invoke();
        }

        private void FindAndInitializeEffects()
        {
            var effects = GetComponentsInChildren<IWeather3DEffect>(true);
            foreach (var effect in effects)
            {
                effect.Initialize(_config);
                _effects.Add(effect);

                if (_debugMode)
                    Debug.Log($"[Weather3DManager] Found effect: {effect.EffectName}");
            }
        }

        #endregion

        #region Data Update

        public void UpdateWeatherData(Weather3DData data)
        {
            _currentData = data;

            foreach (var effect in _effects)
            {
                effect.UpdateVisualization(data);
            }

            OnDataUpdated?.Invoke(data);
        }

        #endregion

        #region Layer Visibility

        private void UpdateLayerVisibility()
        {
            foreach (var effect in _effects)
            {
                switch (effect.EffectType)
                {
                    case WeatherEffectType.IntensityPillar:
                        effect.SetVisible(_showIntensityPillars);
                        break;
                    case WeatherEffectType.Lightning:
                        effect.SetVisible(_showLightning);
                        break;
                    case WeatherEffectType.Precipitation:
                        effect.SetVisible(_showPrecipitation);
                        break;
                }
            }
        }

        #endregion

        #region Public Methods

        public void ClearVisualization()
        {
            foreach (var effect in _effects)
            {
                effect.Clear();
            }
        }

        public Vector3 WorldToDisplay(Vector3 worldPos)
        {
            if (_displayOrigin == null) return worldPos * _worldScale;

            Vector3 relative = worldPos - (_currentData?.AircraftPosition ?? Vector3.zero);
            return _displayOrigin.position + relative * _worldScale;
        }

        #endregion
    }

    public interface IWeather3DEffect
    {
        string EffectName { get; }
        WeatherEffectType EffectType { get; }
        void Initialize(Weather3DConfig config);
        void UpdateVisualization(Weather3DData data);
        void SetVisible(bool visible);
        void Clear();
    }

    public enum WeatherEffectType
    {
        IntensityPillar,
        Lightning,
        Precipitation
    }
}
