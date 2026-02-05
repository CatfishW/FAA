# Volumetric Clouds URP Setup Guide

## Overview
This implementation uses real 3D noise textures from the UnityVolumetricCloudsURP repository (https://github.com/jiaozi158/UnityVolumetricCloudsURP) to render high-quality volumetric clouds.

## What Was Cloned
- Repository: `UnityVolumetricCloudsURP` (outside Assets folder)
- Location: `/Users/zladwu/Development/Projects/FAA/UnityVolumetricCloudsURP/`

## Files Copied to Project

### Noise Textures (in `Assets/_Project/Textures/CloudNoise/`)
1. **WorleyNoise128RGBA.png** - 128x128x128 3D Worley noise (RGBA channels)
2. **WorleyNoise32RGB.png** - 32x32x32 3D Worley noise for erosion
3. **PerlinNoise32RGB.png** - 32x32x32 3D Perlin noise for base shape
4. **CloudLutRainAO.png** - Cloud lookup texture for rain and ambient occlusion

These textures are pre-baked 3D noise data stored as 2D PNG images. They need to be imported as Texture3D assets.

## New Shader Files Created

1. **WeatherCloudVolume.shader** - Main cloud shader using real 3D textures
2. **WeatherCloudVolume.cginc** - Core raymarching functions
3. **VolumetricCloudWeatherURP.shader** - Alternative shader with full properties
4. **CloudNoiseTextureImporter.cs** - Editor tool to import noise textures as 3D assets

## Setup Steps

### Step 1: Import 3D Noise Textures
In Unity Editor, go to:
```
Weather > Import Cloud Noise Textures
```

This will create 3D texture assets:
- `WorleyNoise128RGBA_3D.asset`
- `WorleyNoise32RGB_3D.asset`
- `PerlinNoise32RGB_3D.asset`

### Step 2: Create Cloud Material
In Unity Editor, go to:
```
Weather > Create Cloud Material
```

This creates a material at:
- `Assets/_Project/Materials/VolumetricCloudWeatherMaterial.mat`

### Step 3: Assign to VolumetricCloudVolume
1. Select your VolumetricCloudVolume GameObject in the scene
2. Assign the new material to the MeshRenderer
3. The material will automatically have the 3D noise textures assigned

## Key Differences from Previous Implementation

### Before (Procedural Noise)
- Generated noise mathematically in shader
- Limited detail and performance issues
- Textures appeared empty/gray in editor

### After (3D Texture Sampling)
- Uses pre-baked high-quality 3D Worley and Perlin noise
- Much better visual quality
- Better performance with pre-computed noise
- Works properly in Scene view with real texture data

## Technical Details

### Raymarching Pipeline
1. **Ray-Box Intersection** - Find entry/exit points of camera ray through cloud volume
2. **3D Noise Sampling** - Sample Worley noise for cloud shape
3. **Erosion** - Apply detail noise for cloud edges
4. **Height Gradient** - Apply vertical density gradient
5. **Lighting** - Calculate light transmittance with Beer-Lambert law
6. **Compositing** - Front-to-back alpha blending

### Shader Properties
- **Shape Scale** - Size of large cloud formations (0.1 - 20)
- **Erosion Scale** - Size of cloud detail (1 - 200)
- **Density Multiplier** - Overall cloud density (0 - 2)
- **Raymarch Steps** - Quality vs performance trade-off (16 - 96)
- **Weather Colors** - Aviation standard colors (green/yellow/orange/red/magenta)

### Performance Tips
- Reduce `_RaymarchSteps` for better FPS
- Increase `_StepSize` for faster marching
- Use lower resolution noise textures on mobile

## Troubleshooting

### Clouds Not Visible
1. Check that 3D textures are imported (should show in Project window)
2. Verify material has textures assigned
3. Check that Volume Min/Max bounds are correct
4. Try increasing Density Multiplier

### Artifacts or Banding
- Increase Jitter Amount to reduce banding
- Increase Raymarch Steps for smoother clouds
- Check Step Size isn't too large

### Performance Issues
- Reduce Raymarch Steps to 24-32
- Reduce Light Steps to 2-4
- Increase Step Size
- Use lower resolution noise textures

## Credits
Based on Unity's HDRP Volumetric Clouds ported to URP by jiaozi158:
https://github.com/jiaozi158/UnityVolumetricCloudsURP
