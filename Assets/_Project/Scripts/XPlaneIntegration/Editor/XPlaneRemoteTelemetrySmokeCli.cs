using System;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Threading;
using AircraftControl.Core;
using TrafficRadar;
using UnityEditor;
using UnityEngine;
using WeatherRadar;

namespace FAA.XPlaneIntegration.Editor
{
    public static class XPlaneRemoteTelemetrySmokeCli
    {
        public static void Run()
        {
            string outputDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "ulw_test_results/xplane_remote_smoke"));
            Directory.CreateDirectory(outputDir);

            string host = Environment.GetEnvironmentVariable("XPLANE_REMOTE_HOST") ?? "127.0.0.1";
            string portRaw = Environment.GetEnvironmentVariable("XPLANE_REMOTE_PORT") ?? "37211";
            int port = int.TryParse(portRaw, out int parsedPort) ? parsedPort : 37211;

            GameObject aircraftGo = new GameObject("RemoteTelemetryAircraft");
            GameObject trafficGo = new GameObject("RemoteTelemetryTraffic");
            trafficGo.SetActive(false);
            GameObject weatherGo = new GameObject("RemoteTelemetryWeather");
            GameObject bridgeGo = new GameObject("RemoteTelemetryBridge");

            try
            {
                var aircraftController = aircraftGo.AddComponent<AircraftController>();
                aircraftController.SetUserControlled(false);

                var trafficDataManager = trafficGo.AddComponent<TrafficRadarDataManager>();
                SetPrivateField(trafficDataManager, "autoStartFetching", false);
                trafficGo.SetActive(true);
                trafficDataManager.StopFetching();

                var simulatedWeatherProvider = weatherGo.AddComponent<SimulatedWeatherProvider>();
                InvokeProtected(simulatedWeatherProvider, "Awake");
                simulatedWeatherProvider.Activate();

                Component bridge = AddRemoteTelemetryBridge(bridgeGo);
                SetPrivateField(bridge, "relayHost", host);
                SetPrivateField(bridge, "relayPort", port);
                SetPrivateField(bridge, "autoConnectOnStart", false);
                InvokeInstanceMethod(bridge, "SetAircraftController", aircraftController);
                InvokeInstanceMethod(bridge, "SetTrafficRadarDataManager", trafficDataManager);
                InvokeInstanceMethod(bridge, "SetWeatherRadarProvider", simulatedWeatherProvider);

                InvokeInstanceMethod(bridge, "Connect");

                DateTime deadline = DateTime.UtcNow.AddSeconds(6);
                while (DateTime.UtcNow < deadline && GetInstanceProperty<object>(bridge, "LatestSnapshot") == null)
                {
                    Thread.Sleep(100);
                    InvokeInstanceMethod(bridge, "ProcessPendingSnapshots");
                }

                InvokeInstanceMethod(bridge, "ProcessPendingSnapshots");
                simulatedWeatherProvider.RefreshData();

                string reportPath = Path.Combine(outputDir, "xplane-remote-smoke-report.txt");
                string weatherPath = Path.Combine(outputDir, "weather-radar.png");
                WriteWeatherTexture(simulatedWeatherProvider, weatherPath);
                File.WriteAllText(reportPath, BuildReport(bridge, aircraftController, trafficDataManager, simulatedWeatherProvider, weatherPath));
                Debug.Log($"[XPlaneRemoteTelemetrySmokeCli] Smoke report written to {reportPath}");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(bridgeGo);
                UnityEngine.Object.DestroyImmediate(weatherGo);
                UnityEngine.Object.DestroyImmediate(trafficGo);
                UnityEngine.Object.DestroyImmediate(aircraftGo);
            }
        }

        private static string BuildReport(
            Component bridge,
            AircraftController aircraftController,
            TrafficRadarDataManager trafficDataManager,
            WeatherRadarProviderBase weatherProvider,
            string weatherPath)
        {
            var snapshot = GetInstanceProperty<object>(bridge, "LatestSnapshot");
            var state = aircraftController.State;
            string connectionState = GetInstanceProperty<object>(bridge, "CurrentState")?.ToString() ?? "unknown";
            string lastError = GetInstanceProperty<string>(bridge, "LastError") ?? string.Empty;
            string snapshotSourceMode = GetObjectProperty<object>(snapshot, "SourceMode")?.ToString() ?? "none";
            object weather = GetObjectProperty<object>(snapshot, "Weather");

            return
                "X-Plane Remote Telemetry Smoke Test\n" +
                $"Timestamp: {DateTime.UtcNow:O}\n" +
                $"ConnectionState: {connectionState}\n" +
                $"LastError: {lastError}\n" +
                $"SnapshotReceived: {snapshot != null}\n" +
                $"SnapshotSourceMode: {snapshotSourceMode}\n" +
                $"OwnshipLatLon: {state?.Latitude:F6}, {state?.Longitude:F6}\n" +
                $"OwnshipAltitudeM: {state?.AltitudeMeters:F2}\n" +
                $"OwnshipHeadingDeg: {state?.Heading:F2}\n" +
                $"OwnshipPitchRollDeg: {state?.Pitch:F2}, {state?.Roll:F2}\n" +
                $"OwnshipAirspeedKt: {state?.IndicatedAirspeedKnots:F2}\n" +
                $"SnapshotBarometerInHg: {GetObjectProperty<float?>(weather, "BarometerInHg"):F2}\n" +
                $"SnapshotWind: {GetObjectProperty<float?>(weather, "WindDirectionDeg"):F2}@{GetObjectProperty<float?>(weather, "WindSpeedKt"):F2}\n" +
                $"TrafficCount: {trafficDataManager.AircraftCount}\n" +
                $"WeatherProviderPosition: {weatherProvider.Latitude:F6}, {weatherProvider.Longitude:F6}, {weatherProvider.Altitude:F2}ft\n" +
                $"WeatherTexture: {weatherPath}\n";
        }

        private static Component AddRemoteTelemetryBridge(GameObject bridgeGo)
        {
            Type bridgeType = AppDomain.CurrentDomain
                .GetAssemblies()
                .Select(assembly => assembly.GetType("FAA.XPlaneIntegration.Runtime.XPlaneRemoteTelemetryBridge", false))
                .FirstOrDefault(type => type != null);

            if (bridgeType == null)
            {
                throw new InvalidOperationException("Could not locate FAA.XPlaneIntegration.Runtime.XPlaneRemoteTelemetryBridge at runtime.");
            }

            return bridgeGo.AddComponent(bridgeType);
        }

        private static void WriteWeatherTexture(WeatherRadarProviderBase provider, string outputPath)
        {
            FieldInfo textureField = typeof(WeatherRadarProviderBase).GetField("radarTexture", BindingFlags.Instance | BindingFlags.NonPublic);
            var texture = textureField?.GetValue(provider) as Texture2D;
            if (texture != null)
            {
                File.WriteAllBytes(outputPath, texture.EncodeToPNG());
            }
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            field?.SetValue(target, value);
        }

        private static T GetInstanceProperty<T>(object target, string propertyName)
        {
            if (target == null)
            {
                return default;
            }

            PropertyInfo property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null)
            {
                object propertyValue = property.GetValue(target);
                return propertyValue is T typedPropertyValue ? typedPropertyValue : default;
            }

            FieldInfo field = target.GetType().GetField(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null)
            {
                return default;
            }

            object fieldValue = field.GetValue(target);
            return fieldValue is T typedFieldValue ? typedFieldValue : default;
        }

        private static T GetObjectProperty<T>(object target, string propertyName)
        {
            if (target == null)
            {
                return default;
            }

            PropertyInfo property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null)
            {
                object propertyValue = property.GetValue(target);
                return propertyValue is T typedPropertyValue ? typedPropertyValue : default;
            }

            FieldInfo field = target.GetType().GetField(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null)
            {
                return default;
            }

            object fieldValue = field.GetValue(target);
            return fieldValue is T typedFieldValue ? typedFieldValue : default;
        }

        private static object InvokeInstanceMethod(object target, string methodName, params object[] args)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return method?.Invoke(target, args);
        }

        private static void InvokeProtected(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            method?.Invoke(target, null);
        }
    }
}
