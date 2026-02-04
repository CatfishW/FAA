# Volumetric Weather Visualization 3D

[![GitHub](https://img.shields.io/badge/GitHub-CatfishW%2FFAA-blue)](https://github.com/CatfishW/FAA)

A comprehensive 3D volumetric weather visualization system for Unity, designed for aviation applications.

## 🚀 Quick Start

### Option 1: One-Click Setup (Recommended)
1. Open Unity Editor
2. Go to **Tools → Weather Visualization → Quick Setup → Create Complete System**
3. Press **Play** to see the simulation

### Option 2: Test Scene Generator
1. Go to **Tools → Weather Visualization → Generate Test Scene**
2. Configure settings and click **Generate Test Scene**
3. Open the generated scene and press **Play**

### Option 3: Setup Wizard
1. Go to **Tools → Weather Visualization → Setup Wizard**
2. Select components and scenario
3. Click **Create Weather System**
4. Press **Play**

---

## 📂 Project Structure

```
Assets/_Project/Scripts/WeatherVisualization3D/
├── Core/
│   ├── VolumetricWeatherManager.cs  - Main orchestrator
│   ├── WeatherVolumeData.cs         - 3D volume data container
│   ├── WeatherDataMapper.cs         - 2D to 3D conversion
│   ├── IVolumetricRenderer.cs       - Renderer interfaces
│   └── IWeatherDataSource.cs        - Data source interfaces
├── Data/
│   └── WeatherVolumeConfig.cs       - ScriptableObject config
├── Simulation/
│   ├── WeatherSimulator.cs          - Procedural weather simulation
│   ├── SimulatedStormCell.cs        - Individual storm cell logic
│   └── WeatherScenarioPreset.cs     - Scenario configurations
├── Rendering/
│   ├── VolumetricCloudVolume.cs     - Raymarched volumetric clouds
│   └── IntensityPillarRenderer.cs   - Vertical intensity pillars
├── Effects/
│   ├── VolumetricLightning.cs       - Lightning effects
│   ├── PrecipitationVFX.cs          - Rain/snow particles
│   └── WeatherParticleTextureGenerator.cs
├── Shaders/
│   ├── VolumetricCloud.shader       - Main cloud shader
│   ├── VolumetricCloudCore.cginc    - Raymarching functions
│   ├── WeatherNoise.cginc           - Noise functions
│   └── IntensityPillar.shader       - Pillar rendering
├── Editor/
│   ├── VolumetricWeatherSetupWizard.cs - Main setup wizard
│   ├── WeatherTestSceneGenerator.cs    - Test scene creator
│   ├── WeatherSimulatorEditor.cs       - Custom inspector
│   ├── WeatherQuickActions.cs          - Quick action menus
│   └── ...
└── Debug/
    └── WeatherDebugPanel.cs         - Runtime debug UI
```

---

## 🎮 Runtime Controls

### Keyboard Shortcuts (Runtime Debug Panel)
| Key | Action |
|-----|--------|
| **F1** | Toggle debug panel |
| **P** | Pause/Resume simulation |
| **1-4** | Quick scenario switch |
| **+/-** | Adjust time scale |
| **Ctrl+R** | Reset simulation |
| **WASD** | Move camera |
| **RMB+Mouse** | Look around |
| **Space/Ctrl** | Move up/down |
| **Shift** | Move faster |

### Scenario Quick Keys
| Key | Scenario |
|-----|----------|
| **1** | Scattered Showers |
| **2** | Thunderstorm Cells |
| **3** | Squall Line |
| **4** | Supercell |

---

## 🛠️ Menu Reference

### Tools → Weather Visualization

#### Quick Setup
- **Create Complete System** - Full setup with all components
- **Create Minimal (Clouds Only)** - Just volumetric clouds
- **Create With Pillars** - Clouds + intensity pillars
- **Create Full Storm** - Supercell with all effects

#### Scenarios (Play mode only)
- **Scattered Showers** - Light scattered precipitation
- **Thunderstorm Cells** - Active thunderstorm cells
- **Squall Line** - Organized storm line
- **Supercell** - Severe isolated supercell

#### Visibility
- **Toggle All On/Off** - Show/hide all layers
- **Clouds Only** - Show only volumetric clouds
- **Pillars Only** - Show only intensity pillars

#### Debug
- **Log System Status** - Print component status to console
- **Focus Scene View on Weather** - Center view on weather
- **Create Debug Camera** - Add a free-fly camera

#### Other
- **Setup Wizard** - Full configuration wizard
- **Generate Test Scene** - Create complete test scene
- **Remove All Weather Objects** - Clean up scene

---

## 📐 Component Configuration

### VolumetricWeatherManager
Main orchestrator component. Configure:
- **Config** - Reference to WeatherVolumeConfig asset
- **Volume Origin** - World position of volume center
- **World Scale** - Scale multiplier
- **View Mode** - Perspective3D, PlanView, etc.
- **Layer Visibility** - Toggle individual layers

### WeatherSimulator
Procedural weather simulation. Configure:
- **Scenario Type** - Weather scenario preset
- **Time Scale** - Simulation speed (1-100x)
- **Update Frequency** - How often cells update (Hz)
- **Volume Resolution** - 3D texture resolution
- **Volume World Size** - Coverage area in meters
- **Max Active Cells** - Maximum storm cells

### VolumetricCloudVolume
Raymarched cloud rendering. Configure:
- **Quality Level** - Render quality (0-1)
- **Volume Size** - Render bounds

### WeatherVolumeConfig (ScriptableObject)
All visual settings in one asset:
- **Raymarching** - Steps, step size, jitter
- **Cloud Appearance** - Density, detail, animation
- **Lighting** - Sun color, ambient, scattering
- **Weather Colors** - Aviation-standard intensity colors
- **Height Extrusion** - Altitude mapping
- **Effects** - Lightning, precipitation settings

---

## 🎨 Weather Intensity Colors

Standard aviation weather radar colors:
| Level | Color | dBZ Range | Description |
|-------|-------|-----------|-------------|
| Light | 🟢 Green | 20-30 | Light precipitation |
| Moderate | 🟡 Yellow | 30-40 | Moderate precipitation |
| Heavy | 🟠 Orange | 40-50 | Heavy precipitation |
| Intense | 🔴 Red | 50-60 | Intense precipitation |
| Extreme | 🟣 Magenta | 60+ | Extreme/Turbulence |

---

## 🔧 Troubleshooting

### No clouds visible
1. Check shader exists: `WeatherVisualization3D/VolumetricCloud`
2. Verify 3D texture is being generated (check DensityVolume)
3. Ensure camera far clip plane is large enough (>100000)
4. Check VolumetricCloudVolume has MeshRenderer enabled

### Shader errors
1. Verify all shader includes are present:
   - `VolumetricCloudCore.cginc`
   - `WeatherNoise.cginc`
2. Check shader target is 3.5 or higher
3. Try reimporting shader assets

### Performance issues
1. Reduce volume resolution (try 32x16x32)
2. Lower raymarch steps in config
3. Disable self-shadowing
4. Reduce quality level at runtime

### Simulation not running
1. Ensure you're in Play mode
2. Check WeatherSimulator.simulationEnabled = true
3. Verify IsPaused = false
4. Check debug console for errors

---

## 📝 API Examples

### Spawning a storm cell at runtime
```csharp
var simulator = FindObjectOfType<WeatherSimulator>();
simulator.SpawnCellAt(new Vector2(10000, 5000), IntensityLevel.Heavy);
```

### Changing scenario
```csharp
simulator.SetScenarioByType(ScenarioType.Supercell);
```

### Adjusting time scale
```csharp
simulator.TimeScale = 10f; // 10x speed
```

### Toggling visibility
```csharp
var manager = FindObjectOfType<VolumetricWeatherManager>();
manager.ShowVolumetricClouds = false;
manager.ShowIntensityPillars = true;
```

### Getting weather at position
```csharp
var manager = FindObjectOfType<VolumetricWeatherManager>();
float intensity = manager.GetIntensityAtPosition(transform.position);
WeatherType type = manager.GetWeatherTypeAtPosition(transform.position);
```

---

## 🎯 Performance Guidelines

| Setting | Low | Medium | High | Ultra |
|---------|-----|--------|------|-------|
| Resolution | 32³ | 64³ | 96³ | 128³ |
| Raymarch Steps | 32 | 64 | 128 | 256 |
| Shadow Steps | 2 | 4 | 6 | 16 |
| Quality Level | 0.3 | 0.5 | 0.8 | 1.0 |
| Est. Memory | ~4MB | ~16MB | ~54MB | ~128MB |

---

## 📞 Support

For issues or feature requests, check:
1. Console for error messages
2. Debug menu: **Tools → Weather Visualization → Debug → Log System Status**
3. Gizmos in Scene View (enable on WeatherSimulator)

---

*Last updated: 2026-02-03*
