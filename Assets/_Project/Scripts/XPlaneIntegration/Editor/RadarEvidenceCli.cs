using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using TrafficRadar.Core;
using TrafficRadar;
using UnityEngine;
using WeatherRadar;

namespace FAA.XPlaneIntegration.Editor
{
    public static class RadarEvidenceCli
    {
        public static void Run()
        {
            string outputDir = Path.GetFullPath(Path.Combine(Application.dataPath, "ulw_test_results/radar_evidence"));
            Directory.CreateDirectory(outputDir);

            string weatherPath = Path.Combine(outputDir, "weather-radar.png");
            string trafficPath = Path.Combine(outputDir, "traffic-radar.png");
            string reportPath = Path.Combine(outputDir, "xplane-radar-runtime-report.txt");

            CaptureWeatherEvidence(weatherPath);
            CaptureTrafficEvidence(trafficPath, out int trafficCount, out ThreatLevel highestThreat);

            File.WriteAllText(reportPath,
                "X-Plane Radar Evidence Report\n" +
                $"Timestamp: {DateTime.UtcNow:O}\n" +
                "Mode: CLI synthetic pipeline validation\n" +
                $"WeatherScreenshot: {weatherPath}\n" +
                $"TrafficScreenshot: {trafficPath}\n" +
                $"TrafficCount: {trafficCount}\n" +
                $"HighestThreat: {highestThreat}\n");

            Debug.Log($"[RadarEvidenceCli] Evidence written to {outputDir}");
        }

        private static void CaptureWeatherEvidence(string outputPath)
        {
            var go = new GameObject("WeatherEvidenceProvider");
            var provider = go.AddComponent<SimulatedWeatherProvider>();

            InvokeNoArgs(provider, "Awake");
            provider.Activate();
            provider.RefreshData();

            var baseType = typeof(WeatherRadarProviderBase);
            var textureField = baseType.GetField("radarTexture", BindingFlags.Instance | BindingFlags.NonPublic);
            var texture = textureField?.GetValue(provider) as Texture2D;

            if (texture != null)
            {
                File.WriteAllBytes(outputPath, texture.EncodeToPNG());
            }

            UnityEngine.Object.DestroyImmediate(go);
        }

        private static void CaptureTrafficEvidence(string outputPath, out int count, out ThreatLevel highestThreat)
        {
            var own = new OwnShipPosition
            {
                Latitude = 33.6407,
                Longitude = -84.4277,
                AltitudeMeters = 313f,
                HeadingDegrees = 90f,
                GroundSpeedMps = 0f
            };

            var sampleAircraft = new List<AircraftState>
            {
                new AircraftState
                {
                    Icao24 = "XPL0001",
                    Callsign = "MP01",
                    Latitude = 33.6450,
                    Longitude = -84.4300,
                    AltitudeMeters = 1200f,
                    Heading = 270f,
                    VelocityMps = 120f,
                    VerticalRateMps = -2f,
                    OnGround = false,
                    LastUpdate = DateTime.UtcNow
                },
                new AircraftState
                {
                    Icao24 = "XPL0002",
                    Callsign = "MP02",
                    Latitude = 33.7000,
                    Longitude = -84.5000,
                    AltitudeMeters = 5000f,
                    Heading = 180f,
                    VelocityMps = 160f,
                    VerticalRateMps = 0f,
                    OnGround = false,
                    LastUpdate = DateTime.UtcNow
                },
                new AircraftState
                {
                    Icao24 = "XPL0003",
                    Callsign = "MP03",
                    Latitude = 33.6300,
                    Longitude = -84.4200,
                    AltitudeMeters = 320f,
                    Heading = 45f,
                    VelocityMps = 30f,
                    VerticalRateMps = 0f,
                    OnGround = true,
                    LastUpdate = DateTime.UtcNow
                }
            };

            var processor = new RadarDataProcessor();
            processor.RangeNM = 80f;
            var targets = processor.ProcessAircraft(sampleAircraft, own);

            count = targets.Count;
            highestThreat = ThreatLevel.OtherTraffic;
            foreach (var t in targets)
            {
                if (t.ThreatLevel > highestThreat)
                {
                    highestThreat = t.ThreatLevel;
                }
            }

            const int size = 1024;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            var bg = new Color32(5, 8, 16, 255);
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = bg;
            }

            DrawCross(pixels, size, size / 2, size / 2, new Color32(40, 120, 40, 255));
            DrawRangeRings(pixels, size, new Color32(25, 80, 25, 255));

            foreach (var target in targets)
            {
                int x = Mathf.Clamp(Mathf.RoundToInt((target.RadarPosition.x * 0.45f + 0.5f) * (size - 1)), 0, size - 1);
                int y = Mathf.Clamp(Mathf.RoundToInt((target.RadarPosition.y * 0.45f + 0.5f) * (size - 1)), 0, size - 1);
                DrawDot(pixels, size, x, y, 8, ToThreatColor(target.ThreatLevel));
            }

            tex.SetPixels32(pixels);
            tex.Apply(false);
            File.WriteAllBytes(outputPath, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }

        private static void InvokeNoArgs(object target, string methodName)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            method?.Invoke(target, null);
        }

        private static void DrawCross(Color32[] pixels, int size, int cx, int cy, Color32 color)
        {
            for (int i = 0; i < size; i++)
            {
                pixels[cy * size + i] = color;
                pixels[i * size + cx] = color;
            }
        }

        private static void DrawRangeRings(Color32[] pixels, int size, Color32 color)
        {
            int cx = size / 2;
            int cy = size / 2;
            int[] radii = { 120, 240, 360, 460 };
            foreach (var r in radii)
            {
                for (int a = 0; a < 360; a++)
                {
                    float rad = a * Mathf.Deg2Rad;
                    int x = cx + Mathf.RoundToInt(r * Mathf.Cos(rad));
                    int y = cy + Mathf.RoundToInt(r * Mathf.Sin(rad));
                    if (x >= 0 && x < size && y >= 0 && y < size)
                    {
                        pixels[y * size + x] = color;
                    }
                }
            }
        }

        private static void DrawDot(Color32[] pixels, int size, int cx, int cy, int radius, Color32 color)
        {
            int r2 = radius * radius;
            for (int y = -radius; y <= radius; y++)
            {
                int py = cy + y;
                if (py < 0 || py >= size)
                {
                    continue;
                }

                for (int x = -radius; x <= radius; x++)
                {
                    if (x * x + y * y > r2)
                    {
                        continue;
                    }

                    int px = cx + x;
                    if (px < 0 || px >= size)
                    {
                        continue;
                    }

                    pixels[py * size + px] = color;
                }
            }
        }

        private static Color32 ToThreatColor(ThreatLevel level)
        {
            switch (level)
            {
                case ThreatLevel.ResolutionAdvisory:
                    return new Color32(255, 0, 0, 255);
                case ThreatLevel.TrafficAdvisory:
                    return new Color32(255, 165, 0, 255);
                case ThreatLevel.Proximate:
                    return new Color32(255, 255, 255, 255);
                default:
                    return new Color32(0, 255, 0, 255);
            }
        }
    }
}
