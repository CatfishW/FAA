using System;
using System.Collections.Generic;
using UnityEngine;

namespace IndicatorSystem.Core
{
    public struct WeatherCueSample
    {
        public string Id;
        public Vector2 PositionNM;
        public float Intensity;
    }

    /// <summary>
    /// Retains identities across nearby samples of the same return area. Only current
    /// detections are selected: no synthetic coasting after data loss or a clear scan.
    /// </summary>
    public sealed class StableWeatherCueSelector
    {
        private readonly List<WeatherCueSample> _previous = new List<WeatherCueSample>();
        private readonly List<WeatherCueSample> _candidates = new List<WeatherCueSample>();
        private int _nextId;

        public IReadOnlyList<WeatherCueSample> Select(IReadOnlyList<WeatherCueSample> samples,
            float matchRadiusNM, float separationNM, int limit)
        {
            _candidates.Clear();
            if (samples != null)
                foreach (var sample in samples)
                    if (Finite(sample.PositionNM.x) && Finite(sample.PositionNM.y) && Finite(sample.Intensity) && sample.Intensity > 0)
                        _candidates.Add(new WeatherCueSample { PositionNM = sample.PositionNM, Intensity = sample.Intensity });
            _candidates.Sort(Compare);
            float radiusSquared = Mathf.Max(0, matchRadiusNM) * Mathf.Max(0, matchRadiusNM);
            foreach (var previous in _previous)
            {
                int nearest = -1;
                float distance = radiusSquared;
                for (int i = 0; i < _candidates.Count; i++)
                {
                    if (_candidates[i].Id != null) continue;
                    float candidateDistance = (_candidates[i].PositionNM - previous.PositionNM).sqrMagnitude;
                    if (candidateDistance <= distance) { nearest = i; distance = candidateDistance; }
                }
                if (nearest < 0) continue;
                var match = _candidates[nearest]; match.Id = previous.Id; _candidates[nearest] = match;
            }
            // Intensity class outranks continuity; a new strong return is never hidden
            // solely to preserve an older weak cue. Ties retain existing identities.
            _candidates.Sort(Compare);
            _previous.Clear();
            float separationSquared = Mathf.Max(0, separationNM) * Mathf.Max(0, separationNM);
            foreach (var candidate in _candidates)
            {
                if (_previous.Count >= Mathf.Max(0, limit)) break;
                if (_previous.Exists(other => (other.PositionNM - candidate.PositionNM).sqrMagnitude < separationSquared)) continue;
                var selected = candidate;
                if (selected.Id == null) selected.Id = "WX_CELL_" + (++_nextId).ToString("D4");
                _previous.Add(selected);
            }
            return _previous;
        }

        public void Clear() { _previous.Clear(); _candidates.Clear(); }

        private static int Compare(WeatherCueSample a, WeatherCueSample b)
        {
            int severity = Band(b.Intensity).CompareTo(Band(a.Intensity));
            if (severity != 0) return severity;
            int retained = (b.Id != null).CompareTo(a.Id != null);
            if (retained != 0) return retained;
            if (a.Id != null && b.Id != null) return string.CompareOrdinal(a.Id, b.Id);
            int intensity = b.Intensity.CompareTo(a.Intensity);
            if (intensity != 0) return intensity;
            int x = a.PositionNM.x.CompareTo(b.PositionNM.x);
            return x != 0 ? x : a.PositionNM.y.CompareTo(b.PositionNM.y);
        }

        private static int Band(float intensity) => intensity > .7f ? 3 : intensity > .4f ? 2 : 1;
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
