using System;
using System.IO;
using System.Net;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace FAA.XPlaneIntegration.Editor
{
    public static class RadarEvidenceCli
    {
        private const string SnapshotUrl = "https://faa.agaii.org/xplane12/v1/snapshot";
        private const string WeatherTextureUrl = "https://faa.agaii.org/xplane12/v1/render/weather.png";
        private const string TrafficTextureUrl = "https://faa.agaii.org/xplane12/v1/render/traffic.png";

        public static void Run()
        {
            string outputDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../ulw_test_results/radar_evidence"));
            Directory.CreateDirectory(outputDir);

            string timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            string weatherPath = Path.Combine(outputDir, $"xplane-weather-radar-{timestamp}.png");
            string trafficPath = Path.Combine(outputDir, $"xplane-traffic-radar-{timestamp}.png");
            string reportPath = Path.Combine(outputDir, $"xplane-radar-live-report-{timestamp}.txt");

            JObject snapshot = FetchJson(SnapshotUrl);
            CaptureWeatherEvidence(weatherPath);
            CaptureTrafficEvidence(trafficPath);

            JObject health = snapshot["health"] as JObject;
            JObject weather = snapshot["weather"] as JObject;
            int trafficCount = (snapshot["traffic"] as JArray)?.Count ?? 0;

            File.WriteAllText(reportPath,
                "X-Plane Radar Evidence Report\n" +
                $"Timestamp: {DateTime.UtcNow:O}\n" +
                "Mode: live X-Plane feed validation\n" +
                $"SnapshotUrl: {SnapshotUrl}\n" +
                $"WeatherTextureUrl: {WeatherTextureUrl}\n" +
                $"TrafficTextureUrl: {TrafficTextureUrl}\n" +
                $"Health: {health?["status"] ?? "unknown"} age={ReadFloat(health?["last_packet_age_sec"], -1f):0.000}s error='{health?["last_error"] ?? string.Empty}'\n" +
                $"Precipitation: {ReadFloat(weather?["precipitation_on_aircraft_ratio"], -1f):0.00}\n" +
                $"WeatherScreenshot: {weatherPath}\n" +
                $"TrafficScreenshot: {trafficPath}\n" +
                $"TrafficCount: {trafficCount}\n");

            Debug.Log($"[RadarEvidenceCli] Evidence written to {outputDir}");
        }

        private static void CaptureWeatherEvidence(string outputPath)
        {
            File.WriteAllBytes(outputPath, FetchBytes(WeatherTextureUrl));
        }

        private static void CaptureTrafficEvidence(string outputPath)
        {
            File.WriteAllBytes(outputPath, FetchBytes(TrafficTextureUrl));
        }

        private static JObject FetchJson(string url)
        {
            string json;
            using (var client = new WebClient())
            {
                client.Headers.Set(HttpRequestHeader.UserAgent, "FAA-Unity-RadarEvidenceCli");
                json = client.DownloadString(url);
            }

            return JObject.Parse(json);
        }

        private static byte[] FetchBytes(string url)
        {
            using (var client = new WebClient())
            {
                client.Headers.Set(HttpRequestHeader.UserAgent, "FAA-Unity-RadarEvidenceCli");
                return client.DownloadData(url);
            }
        }

        private static float ReadFloat(JToken token, float defaultValue)
        {
            if (token == null)
            {
                return defaultValue;
            }

            return float.TryParse(token.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float parsed)
                ? parsed
                : defaultValue;
        }

    }
}
