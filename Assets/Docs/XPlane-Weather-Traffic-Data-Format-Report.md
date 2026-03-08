# X-Plane Weather/Traffic Data Format Report

## Scope

This report documents the X-Plane data formats used by FAA Unity weather/traffic radar integration and how those values are consumed in the codebase.

## Weather Data (X-Plane → Unity)

### DataRefs used in integration

| DataRef | Type | Units / Range | Unity consumer |
|---|---|---|---|
| `sim/weather/aircraft/wind_speed_kt` | float | knots | `XPlaneWeatherProvider` (`WindSpeed`) |
| `sim/weather/aircraft/wind_direction_deg` | float | degrees true (0-360) | `XPlaneWeatherProvider` (`WindDirection`) |
| `sim/weather/aircraft/barometer_sealevel_inhg` | float | inHg | `XPlaneWeatherProvider` (`BarometricPressure`) |
| `sim/weather/aircraft/ambient_temperature_c` | float | °C | `XPlaneWeatherProvider` (`Temperature`) |
| `sim/weather/aircraft/visibility_reported_m` | float | meters | `XPlaneWeatherProvider` (`Visibility`) |
| `sim/weather/aircraft/cloud_base_msl_m` | float | meters MSL | `XPlaneWeatherProvider` (`CloudBase`) |

### Weather radar controls / related official refs

| DataRef | Type | Units / Range | Meaning |
|---|---|---|---|
| `sim/cockpit2/EFIS/EFIS_weather_mode` | int | 0-5 | WX mode selection |
| `sim/cockpit2/EFIS/EFIS_weather_gain` | float | ~0.0-2.0 | Radar gain |
| `sim/cockpit2/EFIS/EFIS_weather_tilt` | float | degrees | Commanded tilt |
| `sim/cockpit2/EFIS/EFIS_weather_tilt_antenna` | float | degrees | Actual antenna tilt |

## Traffic Data (X-Plane → Unity)

### Multiplayer slot DataRefs used in integration

The integration currently reads plane slots `1..N` (default N=10):

| DataRef pattern | Type | Units / Range | Unity mapping |
|---|---|---|---|
| `sim/multiplayer/position/plane{i}_lat` | float | degrees | `AircraftState.Latitude` |
| `sim/multiplayer/position/plane{i}_lon` | float | degrees | `AircraftState.Longitude` |
| `sim/multiplayer/position/plane{i}_el` | float | meters MSL | `AircraftState.AltitudeMeters` |
| `sim/multiplayer/position/plane{i}_psi` | float | degrees | `AircraftState.Heading` |
| `sim/multiplayer/position/plane{i}_the` | float | degrees | pitch (kept in slot data) |
| `sim/multiplayer/position/plane{i}_phi` | float | degrees | roll (kept in slot data) |
| `sim/multiplayer/position/plane{i}_v_x` | float | m/s | velocity vector X |
| `sim/multiplayer/position/plane{i}_v_y` | float | m/s | velocity vector Y / vertical rate source |
| `sim/multiplayer/position/plane{i}_v_z` | float | m/s | velocity vector Z |
| `sim/multiplayer/position/plane{i}_gear_deploy` | float | ratio 0..1 | `OnGround` via `>0.5` |
| `sim/multiplayer/position/plane{i}_flap_ratio` | float | ratio 0..1 | flap state |

### TCAS-related authoritative formats (for future expansion)

| DataRef | Type | Units / Range | Meaning |
|---|---|---|---|
| `sim/operation/override/override_TCAS` | int | 0/1 | TCAS override control |
| `sim/cockpit2/tcas/targets/modeS_id[64]` | int[] | 24-bit id | per-target identifier |
| `sim/cockpit2/tcas/targets/position/x|y|z[64]` | float[] | meters | local coordinates |
| `sim/cockpit2/tcas/targets/position/vx|vy|vz[64]` | float[] | m/s | local velocity |
| `sim/cockpit2/tcas/targets/position/psi|the|phi[64]` | float[] | degrees | orientation |

## Integration status summary

- Weather data path is validated as aircraft-local weather telemetry (HUD/radar context), not volumetric NEXRAD returns.
- Traffic path is validated through multiplayer position slots into TrafficRadar pipeline.
- Recent hardening in this branch:
  - single-path event-driven traffic processing (removed duplicate update polling path)
  - geographic filter enforcement before injection into `TrafficRadarDataManager`
  - stale slot cleanup logic
  - manager-driven shared UDP listener injection into weather/traffic providers

## Evidence artifacts

Runtime evidence script:

- `Assets/_Project/Scripts/XPlaneIntegration/Runtime/XPlaneRadarEvidenceCapture.cs`

Default output location at runtime:

- `ulw_test_results/radar_evidence/weather-radar.png`
- `ulw_test_results/radar_evidence/traffic-radar.png`
- `ulw_test_results/radar_evidence/xplane-radar-runtime-report.txt`

## Source references

- X-Plane Developer weather docs: weather radar + weather datarefs
- X-Plane Developer TCAS override docs
- FAA integration code under:
  - `Assets/_Project/Scripts/XPlaneIntegration/Providers/`
  - `Assets/_Project/Scripts/XPlaneIntegration/Bridges/`
  - `Assets/_Project/Scripts/TrafficRadar/`
  - `Assets/_Project/Scripts/WeatherRadar/`
