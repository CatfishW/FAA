using UnityEngine;
using WeatherRadar;

namespace Weather3D
{
    /// <summary>
    /// Bridges the existing WeatherRadar provider with the new Weather3D system.
    /// </summary>
    public class Weather3DBridge : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Weather3DManager _weather3DManager;
        [SerializeField] private WeatherRadarProviderBase _radarProvider;

        [Header("Update Settings")]
        [SerializeField] private float _updateInterval = 5f;

        private float _lastUpdateTime;

        private void Awake()
        {
            if (_weather3DManager == null)
                _weather3DManager = GetComponent<Weather3DManager>();
        }

        private void Start()
        {
            if (_radarProvider == null)
            {
                _radarProvider = FindObjectOfType<WeatherRadarProviderBase>();
            }

            if (_radarProvider != null)
            {
                _radarProvider.OnRadarDataUpdated += OnRadarDataUpdated;
            }
        }

        private void OnDestroy()
        {
            if (_radarProvider != null)
            {
                _radarProvider.OnRadarDataUpdated -= OnRadarDataUpdated;
            }
        }

        private void Update()
        {
            if (Time.time - _lastUpdateTime < _updateInterval) return;

            // Request fresh data from provider
            if (_radarProvider != null)
            {
                ConvertAndUpdate();
            }
        }

        private void OnRadarDataUpdated(Texture2D radarTexture)
        {
            ConvertAndUpdate();
        }

        private void ConvertAndUpdate()
        {
            if (_weather3DManager == null || _radarProvider == null) return;

            // Create 3D data from provider
            var data = new Weather3DData
            {
                AircraftPosition = new Vector3(
                    _radarProvider.Longitude * 111320f,
                    _radarProvider.Altitude * 0.3048f,
                    _radarProvider.Latitude * 110540f
                ),
                AircraftAltitude = _radarProvider.Altitude,
                AircraftHeading = _radarProvider.Heading,
                CoverageRangeNM = _radarProvider.RangeNM,
                MaxAltitudeFt = 50000f
            };

            // Generate sample cells from radar data
            // This is simplified - in real implementation, parse the radar texture
            GenerateSampleCells(data);

            _weather3DManager.UpdateWeatherData(data);
            _lastUpdateTime = Time.time;
        }

        private void GenerateSampleCells(Weather3DData data)
        {
            // Generate some sample weather cells for testing
            // In production, this would parse the actual radar texture
            for (int i = 0; i < 5; i++)
            {
                float angle = (i / 5f) * Mathf.PI * 2f;
                float distance = Random.Range(10f, data.CoverageRangeNM * 1852f * 0.5f); // meters

                var cell = new WeatherCell3D
                {
                    Position = new Vector3(
                        data.AircraftPosition.x + Mathf.Cos(angle) * distance,
                        Random.Range(1000f, 10000f),
                        data.AircraftPosition.z + Mathf.Sin(angle) * distance
                    ),
                    Size = new Vector3(5000f, 5000f, 5000f),
                    Intensity = Random.Range(0.2f, 0.9f),
                    CellType = WeatherCellType.Thunderstorm,
                    BaseAltitude = 2000f,
                    TopAltitude = 35000f
                };

                data.WeatherCells.Add(cell);

                if (cell.Intensity > 0.6f)
                {
                    data.StormCells.Add(new StormCell3D
                    {
                        Position = cell.Position,
                        Intensity = cell.Intensity,
                        HasLightning = true,
                        LightningFrequency = cell.Intensity * 0.5f
                    });
                }
            }
        }
    }
}
