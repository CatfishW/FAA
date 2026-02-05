# Volumetric Clouds - Quick Start Guide

## One-Click Setup

1. Open the setup window:
   ```
   Menu: Weather > Volumetric Clouds Setup
   ```

2. Click **"🚀 One-Click Full Setup"**

3. Done! You should see clouds in Scene view immediately.

## Manual Setup Steps

### Step 1: Generate Noise Textures
- In the Cloud Setup window, click **"Generate Noise Textures"**
- This creates 3D Worley and Perlin noise textures

### Step 2: Create Material
- Click **"Create Cloud Material"**
- Uses the correct shader for your render pipeline (Built-in/URP/SRP)

### Step 3: Create Cloud Volume
- Click **"Create Cloud Volume in Scene"**
- Creates a GameObject ready to render

## Viewing Without Play Mode

The clouds render automatically in Scene view:
- **Auto Refresh** is enabled by default for animation preview
- Adjust settings in real-time
- Use **"Force Refresh Preview"** if needed

## Quick Adjustments

With a cloud volume selected, adjust:
- **Cloud Density** - Overall thickness
- **Shape Scale** - Size of cloud formations
- **Erosion Scale** - Detail level
- **Raymarch Steps** - Quality (higher = better but slower)
- **Wind Speed** - Animation speed

## Render Pipeline Support

The setup auto-detects your render pipeline:
- ✅ **Built-in Render Pipeline** - Uses CG shaders
- ✅ **Universal Render Pipeline (URP)** - Uses HLSL/SRP shaders
- ✅ **High Definition Render Pipeline (HDRP)** - Uses HLSL/SRP shaders

## Troubleshooting

| Issue | Solution |
|-------|----------|
| Clouds not visible | Check "Force Refresh Preview" or increase Density |
| Pink/magenta material | Shader compilation error - check Console |
| Slow performance | Reduce Raymarch Steps or increase Step Size |
| Blocky clouds | Increase Shape Scale or Erosion Scale |
| No animation | Enable Auto Refresh or check Wind Speed |

## Keyboard Shortcuts

None yet - use the Cloud Setup window for all operations.
