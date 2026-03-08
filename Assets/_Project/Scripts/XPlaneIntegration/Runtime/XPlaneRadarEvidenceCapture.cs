using System;
using System.IO;
using System.Text;
using FAA.XPlaneIntegration.Providers;
using TrafficRadar;
using TrafficRadar.Core;
using UnityEngine;
using WeatherRadar;

namespace FAA.XPlaneIntegration.Runtime
{
    [AddComponentMenu("X-Plane Integration/Runtime/X-Plane Radar Evidence Capture")]
    public class XPlaneRadarEvidenceCapture : MonoBehaviour
    {
        [SerializeField] private string outputDirectory = "ulw_test_results/radar_evidence";
        [SerializeField] private bool captureOnStart = true;
        [SerializeField] private float captureDelaySeconds = 2f;

        [SerializeField] private Camera weatherRadarCamera;
        [SerializeField] private Camera trafficRadarCamera;

        [SerializeField] private XPlaneWeatherProvider weatherProvider;
        [SerializeField] private XPlaneTrafficProvider trafficProvider;
        [SerializeField] private WeatherRadarProviderBase weatherRadarProvider;
        [SerializeField] private TrafficRadarDataManager trafficDataManager;
        [SerializeField] private TrafficRadarController trafficRadarController;

        private void Start()
        {
            if (!captureOnStart)
            {
                return;
            }

            Invoke(nameof(CaptureEvidence), captureDelaySeconds);
        }

        [ContextMenu("Capture Radar Evidence")]
        public void CaptureEvidence()
        {
            ResolveReferences();

            string baseDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", outputDirectory));
            Directory.CreateDirectory(baseDir);

            string weatherPath = Path.Combine(baseDir, "weather-radar.png");
            string trafficPath = Path.Combine(baseDir, "traffic-radar.png");
            string reportPath = Path.Combine(baseDir, "xplane-radar-runtime-report.txt");

            CaptureCamera(weatherRadarCamera, weatherPath, 1024, 1024);
            CaptureCamera(trafficRadarCamera, trafficPath, 1024, 1024);
            WriteRuntimeReport(reportPath, weatherPath, trafficPath);
        }

        private void ResolveReferences()
        {
            if (weatherProvider == null)
            {
                weatherProvider = FindObjectOfType<XPlaneWeatherProvider>();
            }

            if (trafficProvider == null)
            {
                trafficProvider = FindObjectOfType<XPlaneTrafficProvider>();
            }

            if (weatherRadarProvider == null)
            {
                weatherRadarProvider = FindObjectOfType<WeatherRadarProviderBase>();
            }

            if (trafficDataManager == null)
            {
                trafficDataManager = FindObjectOfType<TrafficRadarDataManager>();
            }

            if (trafficRadarController == null)
            {
                trafficRadarController = FindObjectOfType<TrafficRadarController>();
            }
        }

        private static void CaptureCamera(Camera camera, string outputPath, int width, int height)
        {
            if (camera == null)
            {
                return;
            }

            var rt = new RenderTexture(width, height, 24);
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);

            var previous = camera.targetTexture;
            var previousActive = RenderTexture.active;

            camera.targetTexture = rt;
            camera.Render();
            RenderTexture.active = rt;

            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();

            byte[] bytes = tex.EncodeToPNG();
            File.WriteAllBytes(outputPath, bytes);

            camera.targetTexture = previous;
            RenderTexture.active = previousActive;

            UnityEngine.Object.Destroy(rt);
            UnityEngine.Object.Destroy(tex);
        }

        private void WriteRuntimeReport(string reportPath, string weatherPath, string trafficPath)
        {
            var sb = new StringBuilder();
            sb.AppendLine("X-Plane Radar Runtime Evidence Report");
            sb.AppendLine($"Timestamp: {DateTime.UtcNow:O}");
            sb.AppendLine();

            sb.AppendLine("Weather Provider");
            if (weatherProvider != null)
            {
                var current = weatherProvider.CurrentWeather;
                sb.AppendLine($"- Connected: {weatherProvider.IsConnected}");
                sb.AppendLine($"- LastUpdateTime: {weatherProvider.LastUpdateTime:F2}");
                sb.AppendLine($"- Wind: {current.WindDirection:F0} deg @ {current.WindSpeed:F1} kt");
                sb.AppendLine($"- Pressure: {current.BarometricPressure:F2} inHg");
                sb.AppendLine($"- Visibility: {current.Visibility:F1} m");
                sb.AppendLine($"- CloudBase: {current.CloudBase:F1} m");
            }
            else
            {
                sb.AppendLine("- Not found");
            }

            sb.AppendLine();
            sb.AppendLine("Traffic Provider");
            if (trafficProvider != null)
            {
                sb.AppendLine($"- Monitoring: {trafficProvider.IsMonitoring}");
                sb.AppendLine($"- TrackedTrafficCount: {trafficProvider.TrackedTrafficCount}");
            }
            else
            {
                sb.AppendLine("- Not found");
            }

            if (trafficDataManager != null)
            {
                sb.AppendLine($"- DataManager AircraftCount: {trafficDataManager.AircraftCount}");
            }

            if (trafficRadarController != null)
            {
                sb.AppendLine($"- Radar TargetCount: {trafficRadarController.TargetCount}");
                sb.AppendLine($"- Radar HighestThreat: {trafficRadarController.HighestThreat}");
            }

            sb.AppendLine();
            sb.AppendLine("Screenshots");
            sb.AppendLine($"- WeatherRadar: {weatherPath}");
            sb.AppendLine($"- TrafficRadar: {trafficPath}");

            File.WriteAllText(reportPath, sb.ToString());
        }
    }
}
