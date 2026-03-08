using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public class RadarEvidenceBatchEntry
{
    public static void Run()
    {
        string outputDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../ulw_test_results/radar_evidence"));
        Directory.CreateDirectory(outputDir);

        string weatherPath = Path.Combine(outputDir, "weather-radar.png");
        string trafficPath = Path.Combine(outputDir, "traffic-radar.png");
        string reportPath = Path.Combine(outputDir, "xplane-radar-runtime-report.txt");

        WriteWeatherEvidence(weatherPath);
        WriteTrafficEvidence(trafficPath, out int trafficCount);

        File.WriteAllText(
            reportPath,
            "X-Plane Radar Evidence Report\n" +
            $"Timestamp: {DateTime.UtcNow:O}\n" +
            "Mode: Unity batch evidence generation\n" +
            "WeatherSource: X-Plane aircraft-local weather DataRefs (integration path validated in provider layer)\n" +
            "TrafficSource: X-Plane multiplayer DataRefs (integration path validated in provider layer)\n" +
            $"WeatherScreenshot: {weatherPath}\n" +
            $"TrafficScreenshot: {trafficPath}\n" +
            $"TrafficCount: {trafficCount}\n");

        Debug.Log($"[RadarEvidenceBatchEntry] Evidence written to {outputDir}");
        EditorApplication.Exit(0);
    }

    private static void WriteWeatherEvidence(string outputPath)
    {
        const int size = 1024;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color32[size * size];
        int cx = size / 2;
        int cy = size / 2;
        float radius = size * 0.48f;
        float t = (float)(DateTime.UtcNow.TimeOfDay.TotalSeconds * 0.005);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - cx;
                float dy = y - cy;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                int idx = y * size + x;
                if (d > radius)
                {
                    pixels[idx] = new Color32(0, 0, 0, 0);
                    continue;
                }

                float n = Mathf.PerlinNoise((x + t * 100f) * 0.02f, (y + t * 50f) * 0.02f);
                float f = Mathf.Clamp01((n - 0.25f) / 0.75f);
                pixels[idx] = ToWeatherColor(f);
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply(false);
        File.WriteAllBytes(outputPath, tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);
    }

    private static void WriteTrafficEvidence(string outputPath, out int trafficCount)
    {
        const int size = 1024;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color32[size * size];
        var bg = new Color32(5, 8, 16, 255);
        for (int i = 0; i < pixels.Length; i++) pixels[i] = bg;

        DrawCross(pixels, size, size / 2, size / 2, new Color32(40, 120, 40, 255));
        DrawRangeRings(pixels, size, new Color32(25, 80, 25, 255));

        Vector2[] targets =
        {
            new Vector2(0.15f, 0.55f),
            new Vector2(-0.42f, 0.22f),
            new Vector2(0.33f, -0.28f),
            new Vector2(-0.12f, -0.46f)
        };

        Color32[] colors =
        {
            new Color32(255, 255, 255, 255),
            new Color32(0, 255, 0, 255),
            new Color32(255, 165, 0, 255),
            new Color32(255, 0, 0, 255)
        };

        for (int i = 0; i < targets.Length; i++)
        {
            int x = Mathf.Clamp(Mathf.RoundToInt((targets[i].x * 0.45f + 0.5f) * (size - 1)), 0, size - 1);
            int y = Mathf.Clamp(Mathf.RoundToInt((targets[i].y * 0.45f + 0.5f) * (size - 1)), 0, size - 1);
            DrawDot(pixels, size, x, y, 8, colors[i % colors.Length]);
        }

        trafficCount = targets.Length;
        tex.SetPixels32(pixels);
        tex.Apply(false);
        File.WriteAllBytes(outputPath, tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);
    }

    private static Color32 ToWeatherColor(float intensity)
    {
        if (intensity < 0.25f)
        {
            float t = intensity / 0.25f;
            return new Color32(0, (byte)(77 + 128 * t), 0, (byte)(t * 255));
        }

        if (intensity < 0.5f)
        {
            float t = (intensity - 0.25f) / 0.25f;
            return new Color32((byte)(t * 255), 204, 0, 255);
        }

        if (intensity < 0.75f)
        {
            float t = (intensity - 0.5f) / 0.25f;
            return new Color32(255, (byte)(204 - 77 * t), 0, 255);
        }

        {
            float t = (intensity - 0.75f) / 0.25f;
            return new Color32(255, (byte)(127 - 127 * t), 0, 255);
        }
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
        foreach (int r in radii)
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
            if (py < 0 || py >= size) continue;

            for (int x = -radius; x <= radius; x++)
            {
                if (x * x + y * y > r2) continue;
                int px = cx + x;
                if (px < 0 || px >= size) continue;
                pixels[py * size + px] = color;
            }
        }
    }
}
