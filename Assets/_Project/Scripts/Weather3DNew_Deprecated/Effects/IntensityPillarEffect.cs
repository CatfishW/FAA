using UnityEngine;
using System.Collections.Generic;

namespace Weather3D
{
    /// <summary>
    /// Renders intensity pillars for weather cells.
    /// </summary>
    public class IntensityPillarEffect : MonoBehaviour, IWeather3DEffect
    {
        [Header("Pillar Settings")]
        [SerializeField] private GameObject _pillarPrefab;
        [SerializeField] private Material _pillarMaterial;
        [SerializeField] private float _pillarHeight = 100f;
        [SerializeField] private float _pillarWidth = 10f;

        private Weather3DConfig _config;
        private List<GameObject> _pillarPool = new List<GameObject>();
        private List<GameObject> _activePillars = new List<GameObject>();
        private bool _isVisible = true;

        public string EffectName => "Intensity Pillars";
        public WeatherEffectType EffectType => WeatherEffectType.IntensityPillar;

        public void Initialize(Weather3DConfig config)
        {
            _config = config;

            // Create pool
            for (int i = 0; i < 50; i++)
            {
                var pillar = CreatePillar();
                pillar.SetActive(false);
                _pillarPool.Add(pillar);
            }
        }

        private GameObject CreatePillar()
        {
            if (_pillarPrefab != null)
            {
                return Instantiate(_pillarPrefab, transform);
            }

            // Create simple cylinder if no prefab
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            obj.transform.SetParent(transform);
            Destroy(obj.GetComponent<Collider>());

            var renderer = obj.GetComponent<Renderer>();
            if (_pillarMaterial != null)
                renderer.material = _pillarMaterial;

            return obj;
        }

        public void UpdateVisualization(Weather3DData data)
        {
            if (!_isVisible) return;

            // Return all active pillars to pool
            foreach (var pillar in _activePillars)
            {
                pillar.SetActive(false);
                _pillarPool.Add(pillar);
            }
            _activePillars.Clear();

            // Create pillars for weather cells
            foreach (var cell in data.WeatherCells)
            {
                if (cell.Intensity < 0.1f) continue;

                GameObject pillar;
                if (_pillarPool.Count > 0)
                {
                    pillar = _pillarPool[_pillarPool.Count - 1];
                    _pillarPool.RemoveAt(_pillarPool.Count - 1);
                }
                else
                {
                    pillar = CreatePillar();
                }

                pillar.transform.position = cell.Position;
                pillar.transform.localScale = new Vector3(
                    _pillarWidth * (0.5f + cell.Intensity),
                    _pillarHeight * cell.Intensity,
                    _pillarWidth * (0.5f + cell.Intensity)
                );

                var renderer = pillar.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material.color = cell.GetIntensityColor();
                }

                pillar.SetActive(true);
                _activePillars.Add(pillar);
            }
        }

        public void SetVisible(bool visible)
        {
            _isVisible = visible;
            foreach (var pillar in _activePillars)
            {
                pillar.SetActive(visible);
            }
        }

        public void Clear()
        {
            foreach (var pillar in _activePillars)
            {
                pillar.SetActive(false);
                _pillarPool.Add(pillar);
            }
            _activePillars.Clear();
        }

        private void OnDestroy()
        {
            foreach (var pillar in _pillarPool)
            {
                if (pillar != null) Destroy(pillar);
            }
            foreach (var pillar in _activePillars)
            {
                if (pillar != null) Destroy(pillar);
            }
        }
    }
}
