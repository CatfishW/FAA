# Traffic Radar System

A complete FAA TCAS-compliant aviation traffic radar system for Unity.

## Quick Setup

**Menu:** `Tools > Traffic Radar > Setup Wizard`

Or use one-click setup: `Tools > Traffic Radar > One-Click Complete Setup`

## Architecture

```
TrafficRadarController (main coordinator)
    ├── TrafficRadarDataManager (API fetching from Airplanes.live)
    ├── RadarDataProcessor (threat classification)
    └── TrafficRadarDisplay (circular radar rendering)

OwnAircraftRadarBridge (optional, for dynamic position)
    └── Updates TrafficRadarController.SetOwnPosition()
```

## Components

| Component | Purpose |
|-----------|---------|
| `TrafficRadarController` | Main entry point - coordinates all components |
| `TrafficRadarDataManager` | Fetches aircraft from Airplanes.live API |
| `RadarDataProcessor` | Calculates distances, bearings, threat levels |
| `TrafficRadarDisplay` | Renders circular radar with symbols |
| `GeoUtilities` | Static helper for geographic calculations |
| `OwnAircraftRadarBridge` | Links AircraftController position to radars |

## Threat Levels (FAA TCAS)

| Level | Color | Symbol | Criteria |
|-------|-------|--------|----------|
| Resolution Advisory | Red | Square | <1 NM, <300 ft |
| Traffic Advisory | Amber | Circle | <3 NM, <500 ft |
| Proximate | Cyan | Filled Diamond | <6 NM, <1200 ft |
| Other Traffic | Cyan | Diamond | Beyond proximate |

## Features

- Live aircraft data from Airplanes.live API
- Auto-range adjustment when aircraft beyond range
- FAA sectional chart tile background
- **Circular chart mask** - Chart and radar clip to circular shape
- **Adjustable chart transparency** - Control via Inspector or runtime
- **Smooth continuous zoom** - Animated zoom with configurable speed (like Online Maps)
- **Zoom range limits** - Min/max range constraints
- Dynamic position via OwnAircraftRadarBridge
- Preset locations (ATL, JFK, LAX, ORD, DFW, LHR)

## Inspector Settings

### TrafficRadarController
- `rangeNM` - Radar range in nautical miles
- `autoRangeEnabled` - Auto-adjust range for aircraft
- `verboseLogging` - Enable debug logs

### TrafficRadarDisplay
- `rangeNM` - Current range in NM
- `minRangeNM` / `maxRangeNM` - Zoom limits (default: 2-150 NM)
- `zoomSpeed` - Multiplier per zoom step (default: 1.5x)
- `enableSmoothZoom` - Enable animated zoom transitions
- `zoomAnimationDuration` - Animation time (default: 0.3s)
- `showRadarBackground` - Show/hide solid background circle
- `chartOpacity` - Chart transparency (0-1)
- `chartEdgeSoftness` - Circular mask edge softness (0-0.1)

### TrafficRadarDataManager
- `referenceLatitude/Longitude` - API fetch center
- `radiusFilterKm` - Fetch radius
- `updateInterval` - Refresh rate in seconds

## Runtime API

### Zoom Control
```csharp
TrafficRadarDisplay display = FindObjectOfType<TrafficRadarDisplay>();

// Smooth zoom (animated if enableSmoothZoom is true)
display.ZoomIn();   // Zoom in by zoomSpeed multiplier
display.ZoomOut();  // Zoom out by zoomSpeed multiplier
display.ZoomBy(-5f); // Zoom in by 5 NM
display.SetRange(20f); // Set to 20 NM (animated)

// Immediate (no animation)
display.SetRangeImmediate(20f);

// Direct animation control
display.StartZoomAnimation(10f);  // Animate to 10 NM

// Subscribe to changes
display.OnZoomChanged.AddListener((range) => Debug.Log($"Range: {range}"));
```

### Chart Background Control
```csharp
display.ChartOpacity = 0.5f;
display.ToggleChartBackground();
```
