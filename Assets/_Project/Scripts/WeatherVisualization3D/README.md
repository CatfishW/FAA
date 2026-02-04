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
Assets/_Project/
├── Prefabs/WeatherVisualization/
│   ├── WeatherSystemRoot.prefab      - Root weather system
│   ├── WeatherSimulator.prefab       - Simulation controller
│   ├── VolumetricCloudVolume.prefab  - Cloud renderer
│   ├── IntensityPillarRenderer.prefab - Pillar renderer
│   ├── VolumetricLightning.prefab    - Lightning effects
│   ├── PrecipitationVFX.prefab       - Rain/snow particles
│   └── LightningBolt.prefab          - Individual bolt
│
├── ScriptableObjects/WeatherVisualization/
│   ├── WeatherPrefabRegistry.asset   - Prefab references
│   └── WeatherVolumeConfig.asset     - System configuration
│
└── Scripts/WeatherVisualization3D/
    ├── Core/
    │   ├── VolumetricWeatherManager.cs  - Main orchestrator
    │   ├── WeatherVolumeData.cs         - 3D volume data container
    │   ├── WeatherDataMapper.cs         - 2D to 3D conversion
    │   ├── IVolumetricRenderer.cs       - Renderer interfaces
    │   └── IWeatherDataSource.cs        - Data source interfaces
    │
    ├── Providers/
    │   └── RainViewer3DProvider.cs      - Real-time global radar data (FREE)
    │
    ├── Data/
    │   ├── WeatherVolumeConfig.cs       - ScriptableObject config
    │   └── WeatherPrefabRegistry.cs     - Prefab registry
    │
    ├── Simulation/
    │   ├── WeatherSimulator.cs          - Procedural weather simulation
    │   ├── SimulatedStormCell.cs        - Individual storm cell logic
    │   └── WeatherScenarioPreset.cs     - Scenario configurations
    │
    ├── Rendering/
    │   ├── VolumetricCloudVolume.cs     - Raymarched volumetric clouds
    │   └── IntensityPillarRenderer.cs   - Vertical intensity pillars
    │
    ├── Effects/
    │   ├── VolumetricLightning.cs       - Lightning effects
    │   ├── PrecipitationVFX.cs          - Rain/snow particles
    │   └── WeatherParticleTextureGenerator.cs
    │
    ├── Shaders/
    │   ├── VolumetricCloud.shader       - Main cloud shader
    │   ├── VolumetricCloudCore.cginc    - Raymarching functions
    │   ├── WeatherNoise.cginc           - Noise functions
    │   └── IntensityPillar.shader       - Pillar rendering
    │
    ├── Editor/
    │   ├── VolumetricWeatherSetupWizard.cs - Main setup wizard
    │   ├── WeatherPrefabFactory.cs         - Prefab creation tool
    │   ├── WeatherTestSceneGenerator.cs    - Test scene creator
    │   ├── WeatherSimulatorEditor.cs       - Custom inspector
    │   ├── WeatherQuickActions.cs          - Quick action menus
    │   └── ...
    │
    └── Debug/
        └── WeatherDebugPanel.cs         - Runtime debug UI
```

---

## 🌍 Real-Time Weather Data Providers

### RainViewer 3D Provider (FREE - Recommended)

**RainViewer API** provides global precipitation radar data completely free.
- **Coverage**: Global
- **Update Frequency**: ~5-10 minutes
- **Cost**: FREE (no API key required)
- **Documentation**: https://www.rainviewer.com/api.html

#### Setup

1. Add `RainViewer3DProvider` component to your weather object (or create via menu)
2. Configure in Inspector:
   - **Tile Size**: 512 (recommended) or 256
   - **Zoom Level**: 6-8 (higher = more detail)
   - **Tile Radius**: 2 (5x5 grid coverage)
   - **Coverage Range**: 40-320 NM
3. Set position via `SetPosition(lat, lon, altitude)`
4. Call `StartUpdates()` to begin fetching

#### Quick Setup Script
```csharp
// Add to scene at runtime
var provider = gameObject.AddComponent<RainViewer3DProvider>();
provider.SetPosition(39.7392f, -104.9903f, 5000f); // Denver area
provider.SetRange(160f); // 160 NM coverage
provider.StartUpdates();

// Connect to VolumetricWeatherManager
var manager = FindObjectOfType<VolumetricWeatherManager>();
manager.SetDataSource(provider);
```

#### Color Schemes
- 1 = Black (original)
- 2 = Blue (default)
- 3 = Standard
- 4 = Detailed
- 5 = Classic
- 6 = Universal Blue
- 7 = TITAN
- 8 = The Weather Channel

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

## 👁️ Scene View Preview (Edit Mode)

Preview weather effects in the Scene view without entering Play mode.

### Opening the Preview Window

**Menu:** `Tools → Weather Visualization → Preview → Preview Window`

### Features

| Feature | Description |
|---------|-------------|
| **Cloud Volume Bounds** | Shows volumetric cloud rendering bounds |
| **Storm Cells** | Preview storm cell positions and intensities |
| **Intensity Pillars** | Shows vertical pillar visualization |
| **Precipitation Areas** | Displays rain/snow coverage zones |
| **Lightning Zones** | Shows lightning strike areas |

### Quick Preview Controls

- **Generate Cells** - Create preview storm cells
- **Clear Cells** - Remove all preview cells
- **Refresh** - Update Scene view display
- **Auto Refresh** - Continuously update preview

### Toggle All Previews

**Menu:** `Tools → Weather Visualization → Preview → Toggle Scene View Preview`

Or use the Preview Window to control individual elements.

### Gizmo Colors

| Element | Color | Meaning |
|---------|-------|---------|
| Cloud Volume | Cyan | Volumetric rendering bounds |
| Light Intensity | Green | 0-30% intensity |
| Moderate Intensity | Yellow | 30-60% intensity |
| Heavy Intensity | Orange | 60-80% intensity |
| Extreme Intensity | Magenta | 80-100% intensity |
| Precipitation | Blue | Rain/snow coverage |
| Lightning | Yellow | Strike zones |

---

## 📦 Prefab System

All weather visualization components use prefabs for instantiation. This ensures consistency and makes it easy to customize the system.

### Available Prefabs

| Prefab | Description |
|--------|-------------|
| `WeatherSystemRoot` | Root object with VolumetricWeatherManager |
| `WeatherSimulator` | Procedural weather simulation controller |
| `VolumetricCloudVolume` | Raymarched volumetric cloud renderer |
| `IntensityPillarRenderer` | Vertical intensity pillar renderer |
| `VolumetricLightning` | Lightning effect controller |
| `PrecipitationVFX` | Rain and snow particle effects |
| `LightningBolt` | Individual lightning bolt prefab |

### Creating Prefabs

**Menu:** `Tools → Weather Visualization → Prefabs → Create All Prefabs`

This creates all prefabs in:
```
Assets/_Project/Prefabs/WeatherVisualization/
```

And a registry asset at:
```
Assets/_Project/ScriptableObjects/WeatherVisualization/WeatherPrefabRegistry.asset
```

### Using Prefabs

```csharp
// Get the registry
var registry = WeatherPrefabRegistry.GetOrCreate();

// Instantiate from prefab
GameObject cloudObj = Instantiate(registry.volumetricCloudVolume, parent);
```

### Customizing Prefabs

1. Edit the prefab in the Project view
2. All changes apply to future instantiations
3. Existing instances in scenes are not affected

---

## ☁️ Enhanced Volumetric Cloud Shader

A new enhanced cloud shader with realistic rendering based on industry techniques.

### Features

| Feature | Description |
|---------|-------------|
| **Perlin-Worley Noise** | Industry-standard noise for cloud shape and erosion |
| **Height Gradient** | Realistic density falloff from base to top |
| **Anvil Shaping** | Thunderstorm anvil cloud formation |
| **Silver Lining** | Bright edges when looking toward the sun |
| **Improved Lighting** | Better scattering and self-shadowing |
| **Wind Animation** | Animated cloud evolution |

### Comparison

| Original Shader | Enhanced Shader |
|-----------------|-----------------|
| Basic Perlin noise | Perlin-Worley hybrid |
| Flat height profile | Natural height gradient |
| Simple lighting | Advanced scattering |
| Hard edges | Soft, eroded edges |

### How to Enable

1. Select your `VolumetricCloudVolume` component
2. Check **"Use Enhanced Shader"** in the inspector
3. Adjust settings in `WeatherVolumeConfig`:
   - Shape Scale: Large cloud formations
   - Erosion Scale: Edge detail
   - Anvil Amount: Thunderstorm tops
   - Silver Lining: Edge brightness

### Key Settings

```
WeatherVolumeConfig (ScriptableObject)
├── Enhanced Cloud Rendering
│   ├── Shape Scale (0.1-5): Large formations
│   ├── Erosion Scale (1-50): Edge detail
│   ├── Shape Strength (0-2): Base density
│   ├── Erosion Strength (0-1.5): Edge softness
│   ├── Base/Top Softness: Height transitions
│   ├── Anvil Amount (0-1): Storm tops
│   ├── Wind Speed: Animation speed
│   └── Silver Lining: Edge brightness
```

### Technical Details

The enhanced shader uses:
- **3-octave Perlin noise** for base cloud shapes
- **3-octave Worley noise** (inverted) for billowy detail
- **Quintic interpolation** for smoother gradients
- **Beer-Lambert law** with powder effect
- **Henyey-Greenstein phase function** for scattering
- **Adaptive step sizing** for performance

---

*Last updated: 2026-02-03*

Sources:
- [RainViewer API Documentation](https://www.rainviewer.com/api.html)
- [Best Weather APIs 2025](https://www.rainviewer.com/blog/weather-radar-apis-2025-overview.html)
- [Weather API Comparison](https://www.tomorrow.io/blog/top-weather-apis/)
- [Real-time Cloudscapes with Volumetric Raymarching](https://blog.maximeheckel.com/posts/real-time-cloudscapes-with-volumetric-raymarching/)
- [Unity HDRP Volumetric Clouds](https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@14.0/manual/Override-Volumetric-Clouds.html)
