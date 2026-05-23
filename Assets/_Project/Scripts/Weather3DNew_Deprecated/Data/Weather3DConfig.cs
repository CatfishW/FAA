using UnityEngine;

namespace Weather3D
{
    /// <summary>
    /// Configuration asset for the 3D Weather Visualization System.
    /// </summary>
    [CreateAssetMenu(fileName = "Weather3DConfig", menuName = "Weather/3D Config")]
    public class Weather3DConfig : ScriptableObject
    {
        [Header("Display Settings")]
        [Tooltip("World scale factor for converting real-world distances to display units")]
        public float WorldScale = 0.001f;

        [Tooltip("Maximum display range in nautical miles")]
        public float MaxRangeNM = 80f;

        [Tooltip("Maximum altitude in feet")]
        public float MaxAltitudeFt = 50000f;

        [Header("Intensity Pillars")]
        [Tooltip("Enable intensity pillar visualization")]
        public bool EnableIntensityPillars = true;

        [Tooltip("Pillar height scale")]
        public float PillarHeightScale = 1f;

        [Tooltip("Pillar color intensity")]
        public float PillarColorIntensity = 1f;

        [Header("Lightning")]
        [Tooltip("Enable lightning visualization")]
        public bool EnableLightning = true;

        [Tooltip("Lightning flash rate")]
        public float LightningFlashRate = 0.5f;

        [Header("Precipitation")]
        [Tooltip("Enable precipitation particles")]
        public bool EnablePrecipitation = true;

        [Tooltip("Particle density")]
        public int ParticleDensity = 1000;

        public static Weather3DConfig CreateDefault()
        {
            var config = CreateInstance<Weather3DConfig>();
            config.WorldScale = 0.001f;
            config.MaxRangeNM = 80f;
            config.MaxAltitudeFt = 50000f;
            config.EnableIntensityPillars = true;
            config.EnableLightning = true;
            config.EnablePrecipitation = true;
            return config;
        }
    }
}
