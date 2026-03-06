# X-Plane Weather Integration Architecture

## Executive Summary

This document describes the architecture for integrating X-Plane weather data into Unity's weather provider system. X-Plane provides **point-instant weather** via DataRefs (`sim/weather/aircraft/*`), which differs from the volumetric radar data used by the existing weather system.

---

## 1. Weather Provider Base Pattern

### File: `WeatherRadarProviderBase.cs`

**Location:** `_Project/Scripts/WeatherRadar/Core/WeatherRadarProviderBase.cs`

### Architecture

```
WeatherRadarProviderBase (abstract)
├── Properties (settable from external scripts)
│   ├── Altitude (feet MSL)
│   ├── Latitude (decimal degrees)
│   ├── Longitude (decimal degrees)
│   └── Heading (degrees 0-360)
├── Settings
│   ├── autoUpdate (bool) - automatic vs sweep-triggered updates
│   ├── updateInterval (float) - update frequency
│   ├── rangeNM (float) - radar range 5-320 NM
│   ├── tiltDegrees (float) - antenna tilt -15 to +15
│   └── gainDB (float) - gain offset -8 to +8
├── Events
│   ├── OnRadarDataUpdated(Texture2D)
│   ├── OnStatusChanged(ProviderStatus)
│   └── OnPositionChanged(alt, lat, lon)
└── Abstract Method
    └── GenerateRadarData() - implement in derived class
```

### Key Methods for External Integration

```csharp
// Primary method for updating position from X-Plane
public virtual void SetAircraftPosition(float altitudeFt, float lat, float lon, float hdg = -1)

// Manual refresh trigger (called by WeatherRadarPanel on sweep complete)
public virtual void RefreshData()

// Enable/disable automatic updates
public void SetAutoUpdate(bool enabled, float interval = 5f)
```

### Update Modes

| Mode | Configuration | Use Case |
|------|---------------|----------|
| Sweep-Triggered | `autoUpdate = false` | Recommended - updates on radar sweep completion |
| Automatic | `autoUpdate = true` | Continuous updates at `updateInterval` |

---

## 2. Aviation Flight Data Exposure (HUD)

### File: `AviationFlightData.cs`

**Location:** `_Project/Scripts/HUD/Core/AviationFlightData.cs`

### Data Structure

```csharp
[Serializable]
public class AviationFlightData
{
    // Attitude
    public float pitch;              // degrees (-90 to 90)
    public float roll;               // degrees (-180 to 180)
    public float heading;            // degrees (0 to 360)
    
    // Speed
    public float indicatedAirspeed;  // knots
    public float groundSpeed;        // knots
    public float trueAirspeed;       // knots
    
    // Altitude
    public float altitudeMSL;        // feet
    public float altitudeAGL;        // feet (radar altitude)
    public float verticalSpeed;      // feet per minute
    public float barometricSetting;  // inHg
    
    // Wind (WEATHER DATA)
    public float windDirection;      // degrees (0-360)
    public float windSpeed;          // knots
    
    // Navigation
    public float magneticVariation;
    public float track;
    
    // Engine data (Engine 1 & 2)
    public float engine1Torque, engine1NR, engine1NG;
    public float engine2Torque, engine2NR, engine2NG;
}
```

### Provider MonoBehaviour

```csharp
public class AviationFlightDataProvider : MonoBehaviour
{
    public AviationFlightData FlightData { get; }
    
    public event Action<AviationFlightData> OnFlightDataUpdated;
    
    // Update methods
    public void UpdateFlightData(AviationFlightData newData);
    public void SetHeading(float heading);
    public void SetPitch(float pitch);
    // ... individual setters
}
```

### X-Plane to AviationFlightData Mapping

**File:** `XPlaneDataRefMapper.cs`

**Location:** `_Project/Scripts/XPlaneIntegration/Core/XPlaneDataRefMapper.cs`

```csharp
// X-Plane DataRefs for weather
public const string DataRef_WindSpeed = "sim/weather/wind_speed_total[0]";      // m/s
public const string DataRef_WindDirection = "sim/weather/wind_direction_true[0]"; // degrees
public const string DataRef_Pressure = "sim/weather/barometer[0]";              // hPa
public const string DataRef_Temperature = "sim/weather/temperature_c[0]";       // Celsius

// Conversion utilities
public static float MpsToKnots(float mps) => mps * 1.94384f;
public static float HpaToInHg(float hpa) => hpa * 0.02953f;
public static float MetersToFeet(float meters) => meters * 3.28084f;
```

---

## 3. MQTT Weather Provider Pattern

### File: `MQTTWeatherProvider.cs`

**Location:** `_Project/Scripts/WeatherRadar/Providers/MQTTWeatherProvider.cs`

### Architecture

```
MQTTWeatherProvider : WeatherRadarProviderBase
├── MQTT Topics
│   ├── radarImageTopic = "NEXRADImage" (Base64 PNG)
│   ├── coordinatesTopic = "NOAAWeatherCoordinates" (outbound)
│   └── weatherDataTopic = "NOAAWeatherData" (text)
├── Connection
│   ├── brokerAddress (default: 127.0.0.1)
│   └── brokerPort (default: 1883)
└── Position Publishing
    └── Publishes: lat,lon,tilt,gain,heading
```

### MQTT Message Flow

```
Unity (Outbound)                    Python Backend
     │                                    │
     │──"lat,lon,tilt,gain,heading"──────>│  (coordinates topic)
     │                                    │
     │<────Base64 PNG Image───────────────│  (radar image topic)
     │                                    │
     │<────Weather Text Data──────────────│  (weather data topic)
```

### Key Implementation Pattern

```csharp
// Outbound position update
private void PublishPositionUpdate()
{
    string message = $"{latitude},{longitude},{tiltDegrees},{gainDB},{heading}";
    mqttClient.Publish(coordinatesTopic, Encoding.UTF8.GetBytes(message), 1, false);
}

// Inbound radar image processing
private void ProcessRadarImage(byte[] payload)
{
    string base64String = Encoding.UTF8.GetString(payload);
    byte[] imageBytes = Convert.FromBase64String(base64String);
    
    Texture2D tempTexture = new Texture2D(2, 2);
    if (tempTexture.LoadImage(imageBytes))
    {
        CopyTextureData(tempTexture);
        NotifyDataUpdated();
    }
}
```

---

## 4. Weather Data Structure

### Current Weather Fields

| Field | Source | Unit | X-Plane DataRef |
|-------|--------|------|-----------------|
| **Wind Speed** | AviationFlightData | knots | `sim/weather/wind_speed_total[0]` (m/s) |
| **Wind Direction** | AviationFlightData | degrees | `sim/weather/wind_direction_true[0]` |
| **Pressure** | AviationFlightData | inHg | `sim/weather/barometer[0]` (hPa) |
| **Temperature** | XPlaneDataRefMapper | Celsius | `sim/weather/temperature_c[0]` |
| **Latitude** | WeatherRadarProviderBase | degrees | `sim/flightmodel/position/latitude` |
| **Longitude** | WeatherRadarProviderBase | degrees | `sim/flightmodel/position/longitude` |
| **Altitude** | WeatherRadarProviderBase | feet | `sim/flightmodel/position/elevation` (meters) |

### OpenMeteo Data Models (3D Weather)

**File:** `OpenMeteoDataModels.cs`

```csharp
[Serializable]
public class OpenMeteoCurrentData
{
    public float temperature_2m;
    public float relative_humidity_2m;
    public float precipitation;
    public int weather_code;
    public float cloud_cover;
    public float wind_speed_10m;
    public float wind_direction_10m;
}

// Pressure level data (for altitude-specific weather)
public class OpenMeteoHourlyData
{
    public float[] temperature_1000hPa;  // ~360 ft
    public float[] temperature_850hPa;   // ~4,920 ft
    public float[] temperature_700hPa;   // ~9,840 ft
    public float[] temperature_500hPa;   // ~18,370 ft
    public float[] temperature_300hPa;   // ~30,180 ft
    
    public float[] relative_humidity_1000hPa;
    public float[] cloud_cover_1000hPa;
    // ... etc
}
```

### Weather Cell Structure (3D Visualization)

```csharp
[Serializable]
public class WeatherCellInfo
{
    public Vector3 Position;
    public Vector3 Size;
    public float Intensity;         // 0-1
    public WeatherType Type;        // LightRain, Thunderstorm, etc.
    public float BaseAltitudeFt;
    public float TopAltitudeFt;
    public bool HasLightning;
    public float TurbulenceLevel;   // 0-1
}
```

---

## 5. Data Flow: External Source → HUD Display

### Complete Flow Diagram

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                           X-PLANE SIMULATOR                                  │
│  DataRefs: sim/weather/wind_speed_total[0], sim/weather/barometer[0], etc.  │
└─────────────────────────────────────────────────────────────────────────────┘
                                      │
                                      │ UDP Port 49009
                                      │ RREF requests / DATA packets
                                      ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                        XPlaneUdpListener                                     │
│  - Sends RREF commands to request DataRefs at specified frequency           │
│  - Receives DATA packets, parses float values                               │
│  - Thread-safe queue for main thread consumption                            │
└─────────────────────────────────────────────────────────────────────────────┘
                                      │
                                      │ Dictionary<string, float>
                                      ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                       XPlaneDataRefMapper                                    │
│  - Converts m/s → knots, meters → feet, hPa → inHg, radians → degrees       │
│  - Maps DataRef paths to AviationFlightData fields                          │
└─────────────────────────────────────────────────────────────────────────────┘
                                      │
                                      │ AviationFlightData
                                      ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                     AviationFlightDataProvider                               │
│  - MonoBehaviour wrapper                                                    │
│  - Fires OnFlightDataUpdated event                                          │
│  - HUD components subscribe to this event                                   │
└─────────────────────────────────────────────────────────────────────────────┘
                                      │
                                      │ OnFlightDataUpdated
                                      ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                          HUD UI Components                                   │
│  - Wind indicator, altimeter, airspeed tape, etc.                           │
└─────────────────────────────────────────────────────────────────────────────┘
```

### Weather Radar Specific Flow

```
┌─────────────────────────────────────────────────────────────────────────────┐
│  EXTERNAL SOURCES                                                           │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐        │
│  │ X-Plane     │  │ MQTT        │  │ NOAA/IEM    │  │ Open-Meteo  │        │
│  │ DataRefs    │  │ Backend     │  │ Tiles       │  │ API         │        │
│  └─────────────┘  └─────────────┘  └─────────────┘  └─────────────┘        │
└─────────────────────────────────────────────────────────────────────────────┘
         │                  │                  │                  │
         ▼                  ▼                  ▼                  ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                    Weather Provider Layer                                    │
│  ┌─────────────────────────────────────────────────────────────────────┐   │
│  │              WeatherRadarProviderBase (abstract)                     │   │
│  │  - Position: lat, lon, alt, heading                                  │   │
│  │  - Settings: range, tilt, gain                                       │   │
│  │  - Events: OnRadarDataUpdated, OnStatusChanged                       │   │
│  └─────────────────────────────────────────────────────────────────────┘   │
│                          │ implementations                                  │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐        │
│  │Simulated    │  │ MQTT        │  │ NOAA        │  │ IEM         │        │
│  │Weather      │  │ Weather     │  │ Multi-      │  │ NEXRAD      │        │
│  │Provider     │  │ Provider    │  │ Source      │  │ Provider    │        │
│  └─────────────┘  └─────────────┘  └─────────────┘  └─────────────┘        │
└─────────────────────────────────────────────────────────────────────────────┘
                                      │
                                      │ Texture2D (radar image)
                                      ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                        WeatherRadarPanel                                     │
│  - Controls sweep timing (sweepCycleDuration)                               │
│  - Calls provider.RefreshData() on sweep complete                           │
│  - Manages child renderers                                                  │
└─────────────────────────────────────────────────────────────────────────────┘
                                      │
                                      ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                        Display Renderers                                     │
│  - RadarReturnRenderer: Weather texture display                             │
│  - RadarSweepRenderer: Sweep animation                                      │
│  - RangeRingsRenderer: Range ring overlay                                   │
│  - WaypointOverlayRenderer: Navigation waypoints                            │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## 6. X-Plane Weather DataRef Reference

### Point-Instant Weather (Aircraft Position)

| DataRef Path | Type | Unit | Description |
|--------------|------|------|-------------|
| `sim/weather/wind_speed_total[0]` | float | m/s | Total wind speed at aircraft |
| `sim/weather/wind_direction_true[0]` | float | degrees | Wind direction (true) |
| `sim/weather/barometer[0]` | float | hPa | Sea level pressure |
| `sim/weather/temperature_c[0]` | float | Celsius | Temperature at aircraft |
| `sim/weather/visibility[0]` | float | meters | Visibility |
| `sim/weather/precip_rate[0]` | float | kg/m²/s | Precipitation rate |
| `sim/weather/cloud_base[0]` | float | meters MSL | Cloud base altitude |
| `sim/weather/turbulence[0]` | float | 0-1 | Turbulence intensity |

### Position DataRefs

| DataRef Path | Type | Unit | Description |
|--------------|------|------|-------------|
| `sim/flightmodel/position/latitude` | float | degrees | Aircraft latitude |
| `sim/flightmodel/position/longitude` | float | degrees | Aircraft longitude |
| `sim/flightmodel/position/elevation` | float | meters | Elevation MSL |
| `sim/flightmodel/position/y_agl` | float | meters | Altitude AGL |
| `sim/flightmodel/position/psi` | float | radians | Heading |

---

## 7. Integration Recommendations

### For X-Plane Weather Injection

**Important Constraint:** X-Plane provides **point-instant** weather only (local conditions at aircraft position), NOT volumetric radar data.

#### Recommended Approach: XPlaneWeatherProvider

```csharp
public class XPlaneWeatherProvider : WeatherRadarProviderBase
{
    public override string ProviderName => "X-Plane Sim Weather";
    
    [SerializeField] private XPlaneUdpListener udpListener;
    
    protected override void Start()
    {
        base.Start();
        
        // Request weather DataRefs from X-Plane
        udpListener.SendRrefRequest("sim/weather/wind_speed_total[0]", 1);
        udpListener.SendRrefRequest("sim/weather/wind_direction_true[0]", 1);
        udpListener.SendRrefRequest("sim/weather/barometer[0]", 1);
        udpListener.SendRrefRequest("sim/weather/temperature_c[0]", 1);
        
        udpListener.OnDataReceived += OnXPlaneDataReceived;
    }
    
    private void OnXPlaneDataReceived(Dictionary<string, float> dataRefs)
    {
        // Map to AviationFlightData
        var flightData = XPlaneDataRefMapper.Map(dataRefs);
        
        // Update wind display
        // Note: This updates HUD wind indicator, NOT radar imagery
    }
    
    protected override void GenerateRadarData()
    {
        // X-Plane does NOT provide radar imagery
        // Options:
        // 1. Use simulated weather (procedural)
        // 2. Use external service (NOAA/IEM) based on position
        // 3. Clear radar display, show "SIM WX" mode
        ClearTexture();
        NotifyDataUpdated();
    }
}
```

### Data Flow Summary

| Data Type | Source | Destination | Notes |
|-----------|--------|-------------|-------|
| **Wind Speed/Direction** | X-Plane DataRef | AviationFlightData → HUD | Real-time via UDP |
| **Pressure** | X-Plane DataRef | AviationFlightData → Altimeter | Requires hPa→inHg conversion |
| **Temperature** | X-Plane DataRef | AviationFlightData (not displayed) | Celsius |
| **Position** | X-Plane DataRef | WeatherRadarProviderBase | For radar center point |
| **Radar Imagery** | NOT from X-Plane | WeatherRadarPanel | Use NOAA/IEM/MQTT |

---

## 8. Files Reference

| File | Purpose |
|------|---------|
| `WeatherRadarProviderBase.cs` | Base class for all weather providers |
| `AviationFlightData.cs` | Flight data container for HUD |
| `AviationFlightDataProvider.cs` | MonoBehaviour wrapper for flight data |
| `MQTTWeatherProvider.cs` | MQTT-based weather provider |
| `NOAAWeatherProvider.cs` | Multi-source NOAA weather provider |
| `IEMWeatherProvider.cs` | Iowa Mesonet NEXRAD provider |
| `SimulatedWeatherProvider.cs` | Procedural weather simulation |
| `XPlaneDataRefMapper.cs` | X-Plane DataRef to Unity mapping |
| `XPlaneUdpListener.cs` | UDP communication with X-Plane |
| `WeatherRadarPanel.cs` | Main radar display controller |
| `WeatherRadarData.cs` | Radar state container |
| `OpenMeteoDataModels.cs` | Open-Meteo API data models |
| `WeatherDataMapper.cs` | 2D→3D weather data conversion |
| `Weather3DData.cs` | 3D weather data container |

---

## 9. Key Constraints

1. **X-Plane Weather is Point-Instant Only**: No volumetric/radar data available from X-Plane DataRefs
2. **Unit Conversions Required**: X-Plane uses metric (m/s, meters, hPa), Unity HUD uses aviation units (knots, feet, inHg)
3. **Thread Safety**: XPlaneUdpListener runs on separate thread; use PollData() or ProcessQueuedData() on main thread
4. **Radar Timing**: WeatherRadarPanel controls update timing via sweep completion, not provider auto-update
