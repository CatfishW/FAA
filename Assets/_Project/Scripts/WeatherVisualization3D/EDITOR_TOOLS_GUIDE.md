# Editor Tools & Testing Infrastructure

This document summarizes all the editor tools and testing infrastructure available for the Volumetric Weather Visualization 3D system.

## 📁 Files Created/Updated

### Editor Tools
| File | Description |
|------|-------------|
| `Editor/VolumetricWeatherSetupWizard.cs` | Main setup wizard for creating weather systems |
| `Editor/WeatherTestSceneGenerator.cs` | Complete test scene generator with camera and UI |
| `Editor/WeatherSimulatorEditor.cs` | Custom inspector for WeatherSimulator with runtime controls |
| `Editor/WeatherQuickActions.cs` | Menu items for one-click actions |
| `Editor/VolumetricCloudSetupTool.cs` | Cloud-specific setup tools |
| `Editor/WeatherMaterialSetup.cs` | Material configuration tools |
| `Editor/WeatherPrefabCreator.cs` | Prefab creation utilities |

### Debug Tools
| File | Description |
|------|-------------|
| `Debug/WeatherDebugPanel.cs` | Runtime UI panel with controls and stats |

### Core Components
| File | Description |
|------|-------------|
| `Core/VolumetricWeatherManager.cs` | Main orchestrator |
| `Core/WeatherVolumeData.cs` | 3D volume data container |
| `Core/WeatherDataMapper.cs` | 2D to 3D conversion |
| `Core/IVolumetricRenderer.cs` | Renderer interfaces |
| `Core/IWeatherDataSource.cs` | Data source interfaces |

### Simulation
| File | Description |
|------|-------------|
| `Simulation/WeatherSimulator.cs` | Procedural weather simulation |
| `Simulation/SimulatedStormCell.cs` | Storm cell logic |
| `Simulation/WeatherScenarioPreset.cs` | Scenario configurations |

### Rendering
| File | Description |
|------|-------------|
| `Rendering/VolumetricCloudVolume.cs` | Raymarched clouds |
| `Rendering/IntensityPillarRenderer.cs` | Intensity pillars |

### Effects
| File | Description |
|------|-------------|
| `Effects/VolumetricLightning.cs` | Lightning effects |
| `Effects/PrecipitationVFX.cs` | Rain/snow particles |

### Shaders
| File | Description |
|------|-------------|
| `Shaders/VolumetricCloud.shader` | Main cloud shader |
| `Shaders/VolumetricCloudCore.cginc` | Raymarching core |
| `Shaders/WeatherNoise.cginc` | Noise functions |
| `Shaders/IntensityPillar.shader` | Pillar shader |
| `Shaders/StormCore.shader` | Storm core effects |

---

## 🔧 Menu Structure

```
Tools
└── Weather Visualization
    ├── Quick Setup
    │   ├── Create Complete System
    │   ├── Add to Selected Object
    │   ├── Create Minimal (Clouds Only)
    │   ├── Create With Pillars
    │   └── Create Full Storm
    │
    ├── Scenarios (Play Mode)
    │   ├── Scattered Showers
    │   ├── Thunderstorm Cells
    │   ├── Squall Line
    │   └── Supercell
    │
    ├── Visibility
    │   ├── Toggle All On
    │   ├── Toggle All Off
    │   ├── Clouds Only
    │   └── Pillars Only
    │
    ├── Debug
    │   ├── Log System Status
    │   ├── Focus Scene View on Weather
    │   └── Create Debug Camera
    │
    ├── Generate Test Scene
    ├── Setup Wizard
    ├── Create Default Setup
    ├── Documentation
    └── Remove All Weather Objects
```

---

## 🎮 Runtime Debug Panel Features

When `WeatherDebugPanel` is present in the scene (automatically created via test scene generator):

### Status Display
- Current scenario name
- Active cell count by intensity
- Simulation time
- Running/paused state

### Controls
- Time scale slider (0.1x - 100x)
- Quality level slider
- Pause/Resume button
- Reset button

### Visibility Toggles
- Clouds
- Pillars
- Lightning
- Precipitation

### Quick Scenario Buttons
- Scattered Showers
- Thunderstorm Cells
- Squall Line
- Supercell

---

## 🧪 WeatherSimulator Custom Inspector Features

When WeatherSimulator is selected in the editor (play mode):

### Test Controls
- Pause/Resume simulation
- Reset simulation
- Step forward (1s, 10s)
- Adjust time scale slider

### Cell Spawning
- Select intensity level
- Set spawn position
- Spawn at position button
- Spawn at random button

### Active Cells List
- Intensity color indicator
- Position and radius
- Altitude range
- Remaining lifetime

---

## 🚀 Quick Testing Workflow

### Method 1: Quick Setup
```
1. Menu: Tools → Weather Visualization → Quick Setup → Create Complete System
2. Press Play
3. See weather simulation running
```

### Method 2: Test Scene
```
1. Menu: Tools → Weather Visualization → Generate Test Scene
2. Configure settings
3. Click "Generate Test Scene"
4. Press Play
5. Use F1 to toggle debug panel
```

### Method 3: Custom Setup
```
1. Menu: Tools → Weather Visualization → Setup Wizard
2. Select components
3. Configure volume settings
4. Click "Create Weather System"
5. Press Play
```

---

## ⌨️ Keyboard Reference

### In Play Mode with Debug Panel
| Key | Action |
|-----|--------|
| F1 | Toggle debug panel |
| P | Pause/Resume |
| 1-4 | Quick scenario switch |
| +/- | Adjust time scale |
| Ctrl+R | Reset simulation |
| WASD | Move camera |
| RMB+Mouse | Look around |
| Space | Move up |
| Ctrl | Move down |
| Shift | Move faster |

---

## 📊 Performance Presets

| Setting | Low | Medium | High | Ultra |
|---------|-----|--------|------|-------|
| Volume Resolution | 32³ | 64³ | 96³ | 128³ |
| Raymarch Steps | 32 | 64 | 128 | 256 |
| Shadow Steps | 2 | 4 | 6 | 16 |
| Quality Level | 0.3 | 0.5 | 0.8 | 1.0 |
| Estimated Memory | ~4MB | ~16MB | ~54MB | ~128MB |

---

*Last updated: 2026-02-03*
