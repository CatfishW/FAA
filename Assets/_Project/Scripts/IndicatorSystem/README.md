# On/Off Screen Indicator System

A comprehensive indicator system for displaying on-screen symbols and off-screen directional arrows for traffic and weather radar targets.

## Quick Start

1. **One-Click Setup**: `Tools > Indicator System > Setup Indicator System`
2. The system auto-links to existing `TrafficRadarController` and `WeatherRadarProviderBase`
3. Enter Play mode to see indicators

## Features

- **On-Screen Indicators**: Symbol at target position when visible
- **Off-Screen Arrows**: Directional arrows at screen edge pointing to off-screen targets
- **Threat-Level Colors**: Cyan (normal) → Amber (advisory) → Red (resolution advisory)
- **Altitude Indicators**: Up/down arrows showing relative altitude
- **Smooth Animations**: Configurable smooth movement and pulse effects
- **Object Pooling**: Efficient memory management

## Structure

```
IndicatorSystem/
├── Core/          - Interfaces, data structures, calculations
├── Display/       - UI components (IndicatorElement, IndicatorPool)
├── Controller/    - Main controller coordinating the system
├── Integration/   - Bridges to TrafficRadar and WeatherRadar
├── Editor/        - Setup tools and custom inspectors
└── Settings/      - IndicatorSettings.asset (auto-created)
```

## Configuration

Edit `IndicatorSettings.asset` to customize:
- Indicator sizes and colors
- Distance/altitude display options
- Animation settings
- Per-type enable/disable

## Integration

The system uses **event-based communication**:
- `TrafficIndicatorBridge` listens to `OnTargetsUpdated`
- `WeatherIndicatorBridge` listens to `OnDataUpdated`

No modifications needed to existing radar code.
