using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

[AddComponentMenu("Air Traffic/Traffic Data Logger")]
public class TrafficDataLogger : MonoBehaviour
{
    [SerializeField] private TrafficDataManager trafficDataManager;
    [SerializeField] private string subfolder = "TrafficLogs";
    [SerializeField] private string filenamePrefix = "traffic_";
    [SerializeField] private bool enabledLogging = true;

    private string filePath;
    private StreamWriter writer;

    private void OnEnable()
    {
        if (trafficDataManager == null) trafficDataManager = FindObjectOfType<TrafficDataManager>();
        PrepareFile();
        if (trafficDataManager != null)
        {
            trafficDataManager.onDataUpdated.AddListener(OnDataUpdated);
        }
    }

    private void OnDisable()
    {
        if (trafficDataManager != null)
        {
            trafficDataManager.onDataUpdated.RemoveListener(OnDataUpdated);
        }
        CloseFile();
    }

    private void PrepareFile()
    {
        try
        {
            string folder = Path.Combine(Application.persistentDataPath, subfolder);
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
            string ts = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            filePath = Path.Combine(folder, filenamePrefix + ts + ".jsonl");
            writer = new StreamWriter(filePath, false, Encoding.UTF8);
            Debug.Log($"[TrafficDataLogger] Logging to {filePath}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[TrafficDataLogger] Failed to create log file: {e.Message}");
        }
    }

    private void CloseFile()
    {
        try { writer?.Flush(); writer?.Close(); } catch { }
        writer = null;
    }

    private void OnDataUpdated(List<TrafficDataManager.AircraftData> data)
    {
        if (!enabledLogging || writer == null || data == null) return;
        try
        {
            // Write one JSON line per update with a compact snapshot
            var snapshot = new
            {
                time = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                count = data.Count,
                aircraft = data.ConvertAll(a => new {
                    a.icao24, a.callsign, a.latitude, a.longitude, a.altitude, a.velocity, a.heading, a.verticalRate, type = a.type.ToString()
                })
            };
            string line = Newtonsoft.Json.JsonConvert.SerializeObject(snapshot);
            writer.WriteLine(line);
            writer.Flush();
        }
        catch (Exception e)
        {
            Debug.LogError($"[TrafficDataLogger] Write failed: {e.Message}");
        }
    }
}


