using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using System.Collections;

[System.Serializable]
public class GridPointsData
{
    public Properties properties;
}

[System.Serializable]
public class Properties
{
    public string gridId;
    public int gridX;
    public int gridY;
}

[System.Serializable]
public class GridData
{
    public GridProperties properties;
}

[System.Serializable]
public class GridProperties
{
    public QuantitativePrecipitation quantitativePrecipitation;
}

[System.Serializable]
public class QuantitativePrecipitation
{
    public QPFValue[] values;
}

[System.Serializable]
public class QPFValue
{
    public float value;
}

public class RadarDisplay : MonoBehaviour
{
    [Header("Display Settings")]
    public RawImage radarImage;
    public bool useRealData = false;
    public float updateInterval = 2f;

    [Header("Simulation Settings")]
    public float noiseScale = 0.1f;
    public float noiseOffsetSpeed = 0.1f;

    [Header("Real Data Settings")]
    public float latitude = 40.7128f;
    public float longitude = -74.0060f;
    public float dataRadiusKm = 1f;

    [Header("Radar Scan Settings")]
    public float scanSpeed = 180f; // Degrees per second
    public Color scanColor = new Color(1, 1, 1, 0.5f); // Semi-transparent white

    [Header("Radar Display Settings")]
    public float radarRadiusKm = 1f; // Radar radius in kilometers

    private Texture2D radarTexture;
    private Texture2D displayTexture;
    private bool isScanning;
    private float lastUpdateTime;
    private float noiseOffsetX;
    private float noiseOffsetY;
    private float scanAngle;
    public float zoom_scale = 0.1f;

    void Start()
    {
        InitializeTextures();
        noiseOffsetX = Random.Range(0f, 100f);
        noiseOffsetY = Random.Range(0f, 100f);
        scanAngle = 0f;
        isScanning = false;
        //StartCoroutine(FetchWeatherDataRoutine());
    }

    void Update()
    {
        UpdateScanEffect();
        // if (Time.time - lastUpdateTime >= updateInterval)
        // {
        //     lastUpdateTime = Time.time;
        //     if (useRealData)
        //     {
        //         StartCoroutine(FetchRealWeatherData());
        //     }
        //     else
        //     {
        //         GenerateSimulatedData();
        //     }
        //     StartNewScan();
        // }
    }

    IEnumerator FetchWeatherDataRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(updateInterval);
            if (useRealData)
            {
                yield return FetchRealWeatherData();
            }
            else
            {
                GenerateSimulatedData();
            }
            StartNewScan();
        }
    }

    void InitializeTextures()
    {
        radarTexture = new Texture2D(256, 256, TextureFormat.RGBA32, false);
        displayTexture = new Texture2D(256, 256, TextureFormat.RGBA32, false);
        ClearTextureToBlack(radarTexture);
        radarImage.texture = displayTexture;
    }

    public void StartNewScan()
    {
        isScanning = true;
        scanAngle = 90f;
    }

    void UpdateScanEffect()
    {
        if (!isScanning) return;

        displayTexture.SetPixels(radarTexture.GetPixels());
        scanAngle += scanSpeed * Time.deltaTime;
        DrawScanLine(scanAngle);
        displayTexture.Apply();

        if (scanAngle >= 360f)
        {
            isScanning = false;
            displayTexture.SetPixels(radarTexture.GetPixels());
            displayTexture.Apply();
        }
    }

    void DrawScanLine(float angle)
    {
        int centerX = displayTexture.width / 2;
        int centerY = displayTexture.height / 2;
        float radius = displayTexture.width / 2;

        float rad = angle * Mathf.Deg2Rad;
        Vector2 direction = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));

        for (float r = 0; r < radius; r += 0.5f)
        {
            int x = Mathf.RoundToInt(centerX + direction.x * r);
            int y = Mathf.RoundToInt(centerY + direction.y * r);

            if (x >= 0 && x < displayTexture.width && y >= 0 && y < displayTexture.height)
            {
                Color baseColor = radarTexture.GetPixel(x, y);
                displayTexture.SetPixel(x, y, Color.Lerp(baseColor, scanColor, 0.5f));
            }
        }
    }

    void ClearTextureToBlack(Texture2D texture)
    {
        int centerX = texture.width / 2;
        int centerY = texture.height / 2;
        float radius = texture.width / 2;

        for (int x = 0; x < texture.width; x++)
        {
            for (int y = 0; y < texture.height; y++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), new Vector2(centerX, centerY));
                texture.SetPixel(x, y, distance > radius ? Color.clear : Color.black);
            }
        }
        texture.Apply();
    }

    void GenerateSimulatedData()
    {
        ClearTextureToBlack(radarTexture);

        noiseOffsetX += noiseOffsetSpeed * Time.deltaTime;
        noiseOffsetY += noiseOffsetSpeed * Time.deltaTime;

        int centerX = radarTexture.width / 2;
        int centerY = radarTexture.height / 2;
        float radius = radarRadiusKm * (radarTexture.width / (2 * dataRadiusKm)); // Adjust radius based on radarRadiusKm

        for (int x = 0; x < radarTexture.width; x++)
        {
            for (int y = 0; y < radarTexture.height; y++)
            {
                // float distance = Vector2.Distance(new Vector2(x, y), new Vector2(centerX, centerY));
                // if (distance > radius)
                // {
                //     continue;
                // }

                float noiseValue = Mathf.PerlinNoise(
                    (x + Random.Range(-10f, 10f)) * noiseScale + noiseOffsetX,
                    (y + Random.Range(-10f, 10f)) * noiseScale + noiseOffsetY
                );

                Color currentColor = radarTexture.GetPixel(x, y);
                Color newColor = GetSimulationColor(noiseValue);
                if (newColor != Color.black)
                {
                    radarTexture.SetPixel(x, y, Color.Lerp(currentColor, newColor, 0.5f));
                }
            }
        }
        radarTexture.Apply();
    }

    Color GetSimulationColor(float noiseValue)
    {
        if (noiseValue > 0.8f) return Color.red;
        if (noiseValue > 0.6f) return Color.yellow;
        if (noiseValue > 0.4f) return Color.green;
        return Color.black;
    }

    IEnumerator FetchRealWeatherData()
    {
        string pointsUrl = $"https://api.weather.gov/points/{latitude},{longitude}";
        UnityWebRequest pointsRequest = UnityWebRequest.Get(pointsUrl);
        pointsRequest.SetRequestHeader("User-Agent", "UnityWeatherRadar/1.0");
        yield return pointsRequest.SendWebRequest();

        if (pointsRequest.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("Grid points request failed: " + pointsRequest.error);
            yield break;
        }

        GridPointsData pointsData = JsonUtility.FromJson<GridPointsData>(pointsRequest.downloadHandler.text);
        string gridId = pointsData.properties.gridId;
        int gridX = pointsData.properties.gridX;
        int gridY = pointsData.properties.gridY;

        float gridSpacingKm = 1f;
        int numGrids = Mathf.CeilToInt(dataRadiusKm / gridSpacingKm);
        int minX = gridX - numGrids;
        int maxX = gridX + numGrids;
        int minY = gridY - numGrids;
        int maxY = gridY + numGrids;

        float[,] precipitationGrid = new float[maxX - minX + 1, maxY - minY + 1];

        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                string gridUrl = $"https://api.weather.gov/gridpoints/{gridId}/{x},{y}";
                UnityWebRequest gridRequest = UnityWebRequest.Get(gridUrl);
                gridRequest.SetRequestHeader("User-Agent", "UnityWeatherRadar/1.0");
                yield return gridRequest.SendWebRequest();

                if (gridRequest.result == UnityWebRequest.Result.Success)
                {
                    GridData weatherData = JsonUtility.FromJson<GridData>(gridRequest.downloadHandler.text);
                    float precipValue = weatherData.properties.quantitativePrecipitation?.values?[0].value ?? 0f;
                    precipitationGrid[x - minX, y - minY] = precipValue;
                    Debug.Log($"Precipitation value at ({x},{y}): {precipValue}");
                }
                else
                {
                    Debug.LogWarning($"Grid data request failed at ({x},{y}): {gridRequest.error}");
                }
            }
        }

        GenerateRealDataTexture(precipitationGrid, zoom_scale);
    }

    void GenerateRealDataTexture(float[,] precipitationData, float scale = 1f)
    {
        ClearTextureToBlack(radarTexture);

        int centerX = radarTexture.width / 2;
        int centerY = radarTexture.height / 2;
        float radius = radarRadiusKm * (radarTexture.width / (2 * dataRadiusKm)); // Adjust radius based on radarRadiusKm

        for (int x = 0; x < radarTexture.width; x++)
        {
            for (int y = 0; y < radarTexture.height; y++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), new Vector2(centerX, centerY));
                if (distance > radius)
                {
                    radarTexture.SetPixel(x, y, Color.black);
                    continue;
                }

                int dataX = (int)Mathf.Lerp(0, precipitationData.GetLength(0) - 1, (x / (float)radarTexture.width) * scale);
                int dataY = (int)Mathf.Lerp(0, precipitationData.GetLength(1) - 1, (y / (float)radarTexture.height) * scale);
                float precipValue = precipitationData[Mathf.Clamp(dataX, 0, precipitationData.GetLength(0) - 1), Mathf.Clamp(dataY, 0, precipitationData.GetLength(1) - 1)];

                Color currentColor = radarTexture.GetPixel(x, y);
                Color newColor = GetRealDataColor(precipValue);
                if (newColor != Color.black)
                {
                    radarTexture.SetPixel(x, y, Color.Lerp(currentColor, newColor, 0.5f));
                }
            }
        }
        radarTexture.Apply();
    }

    Color GetRealDataColor(float precipValue)
    {
        if (precipValue > 10f) return Color.red;
        if (precipValue > 3f) return Color.yellow;
        if (precipValue > 1f) return Color.green;
        return Color.black;
    }

    void ClearTextureToRadarBase()
    {
        displayTexture.SetPixels(radarTexture.GetPixels());
    }
}