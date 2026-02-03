// Volumetric Cloud Core Functions for Raymarching
// Part of WeatherVisualization3D System
// Contains raymarching utilities and lighting calculations

#ifndef VOLUMETRIC_CLOUD_CORE_CGINC
#define VOLUMETRIC_CLOUD_CORE_CGINC

#include "WeatherNoise.cginc"

// ============================================
// UNIFORM DECLARATIONS
// ============================================

// Volume textures
sampler3D _DensityVolume;
float4 _DensityVolume_TexelSize;

// Volume bounds
float3 _VolumeMin;
float3 _VolumeMax;
float3 _VolumeSize;
float3 _VolumeCenter;

// Raymarching parameters
int _RaymarchSteps;
float _StepSize;
float _JitterAmount;
float _EarlyTerminationThreshold;

// Cloud appearance
float _CloudDensity;
float _DetailScale;
float _DetailStrength;
float _EdgeSoftness;
float _AnimationSpeed;
// Note: _Time is built-in Unity variable, do not redeclare

// Lighting
float3 _LightDir;
float3 _LightColor;
float3 _AmbientColor;
float _LightAbsorption;
float _ForwardScattering;
float _MultiScatterStrength;
int _ShadowSteps;
float _SelfShadowing;

// Intensity colors
float4 _LightColor_Weather;
float4 _ModerateColor;
float4 _HeavyColor;
float4 _IntenseColor;
float4 _ExtremeColor;
float4 _StormCoreColor;

// ============================================
// RAY-BOX INTERSECTION
// ============================================

// Returns (tNear, tFar) or (-1, -1) if no intersection
float2 rayBoxIntersection(float3 rayOrigin, float3 rayDir, float3 boxMin, float3 boxMax)
{
    float3 invDir = 1.0 / rayDir;
    float3 t0 = (boxMin - rayOrigin) * invDir;
    float3 t1 = (boxMax - rayOrigin) * invDir;
    
    float3 tmin = min(t0, t1);
    float3 tmax = max(t0, t1);
    
    float tNear = max(max(tmin.x, tmin.y), tmin.z);
    float tFar = min(min(tmax.x, tmax.y), tmax.z);
    
    // No intersection if tNear > tFar or tFar < 0
    if (tNear > tFar || tFar < 0.0)
        return float2(-1.0, -1.0);
    
    return float2(max(tNear, 0.0), tFar);
}

// ============================================
// COORDINATE TRANSFORMS
// ============================================

// Convert world position to normalized volume UVW (0-1)
float3 worldToVolumeUVW(float3 worldPos)
{
    return (worldPos - _VolumeMin) / _VolumeSize;
}

// Convert normalized UVW to world position
float3 volumeUVWToWorld(float3 uvw)
{
    return uvw * _VolumeSize + _VolumeMin;
}

// Check if position is inside volume
bool isInsideVolume(float3 worldPos)
{
    float3 uvw = worldToVolumeUVW(worldPos);
    return all(uvw >= 0.0) && all(uvw <= 1.0);
}

// ============================================
// DENSITY SAMPLING
// ============================================

// Sample raw density from volume texture
float4 sampleDensityRaw(float3 uvw)
{
    // R = density, G = type, B = turbulence, A = lightning
    return tex3Dlod(_DensityVolume, float4(uvw, 0));
}

// Sample weather density with detail noise
float sampleCloudDensity(float3 worldPos, float3 uvw, float lod)
{
    // Sample base density from texture
    float4 volumeSample = tex3Dlod(_DensityVolume, float4(uvw, lod));
    float baseDensity = volumeSample.r;
    
    if (baseDensity < 0.01)
        return 0.0;
    
    // Add procedural detail noise
    float3 noisePos = worldPos * _DetailScale * 0.0001;
    float time = _Time * _AnimationSpeed;
    float detailNoise = cloudNoise3D(noisePos, time, _DetailScale, _DetailStrength);
    
    // Combine base and detail
    float density = baseDensity * (0.5 + detailNoise * 0.5);
    
    // Apply edge softness
    float edgeFade = smoothstep(0.0, _EdgeSoftness, baseDensity);
    density *= edgeFade;
    
    // Apply global density multiplier
    density *= _CloudDensity;
    
    return saturate(density);
}

// Get intensity level from density (for coloring)
float getIntensityLevel(float3 uvw)
{
    float4 volumeSample = tex3Dlod(_DensityVolume, float4(uvw, 0));
    return volumeSample.r; // Density directly maps to intensity
}

// Get weather type from volume
float getWeatherType(float3 uvw)
{
    float4 volumeSample = tex3Dlod(_DensityVolume, float4(uvw, 0));
    return volumeSample.g * 255.0; // Decode type
}

// ============================================
// COLOR MAPPING
// ============================================

// Map intensity to aviation weather color
float3 intensityToColor(float intensity)
{
    if (intensity < 0.2)
        return lerp(float3(0, 0, 0), _LightColor_Weather.rgb, intensity / 0.2);
    else if (intensity < 0.4)
        return lerp(_LightColor_Weather.rgb, _ModerateColor.rgb, (intensity - 0.2) / 0.2);
    else if (intensity < 0.6)
        return lerp(_ModerateColor.rgb, _HeavyColor.rgb, (intensity - 0.4) / 0.2);
    else if (intensity < 0.8)
        return lerp(_HeavyColor.rgb, _IntenseColor.rgb, (intensity - 0.6) / 0.2);
    else
        return lerp(_IntenseColor.rgb, _ExtremeColor.rgb, (intensity - 0.8) / 0.2);
}

// ============================================
// LIGHTING
// ============================================

// Henyey-Greenstein phase function for forward scattering
float henyeyGreenstein(float cosTheta, float g)
{
    float g2 = g * g;
    return (1.0 - g2) / (4.0 * 3.14159 * pow(1.0 + g2 - 2.0 * g * cosTheta, 1.5));
}

// Beer-Lambert law for light absorption
float beerLambert(float density, float distance)
{
    return exp(-density * _LightAbsorption * distance);
}

// Calculate shadow from light direction
float calculateShadow(float3 worldPos, float3 uvw)
{
    if (_SelfShadowing < 0.5)
        return 1.0;
    
    float shadow = 1.0;
    float stepSize = _StepSize * 2.0;
    float3 lightStep = -_LightDir * stepSize;
    float3 samplePos = worldPos;
    
    for (int i = 0; i < _ShadowSteps; i++)
    {
        samplePos += lightStep;
        float3 sampleUVW = worldToVolumeUVW(samplePos);
        
        if (!all(sampleUVW >= 0.0) || !all(sampleUVW <= 1.0))
            break;
        
        float density = sampleCloudDensity(samplePos, sampleUVW, 1.0);
        shadow *= beerLambert(density, stepSize);
        
        if (shadow < 0.01)
            break;
    }
    
    return shadow;
}

// Full lighting calculation
float3 calculateLighting(float3 worldPos, float3 uvw, float density, float3 viewDir)
{
    // Base weather color from intensity
    float intensity = getIntensityLevel(uvw);
    float3 weatherColor = intensityToColor(intensity);
    
    // Shadow
    float shadow = calculateShadow(worldPos, uvw);
    
    // Phase function for scattering
    float cosTheta = dot(viewDir, -_LightDir);
    float phase = lerp(
        0.25, // Isotropic
        henyeyGreenstein(cosTheta, _ForwardScattering), // Forward scattering
        0.5
    );
    
    // Direct lighting
    float3 directLight = _LightColor * shadow * phase;
    
    // Multi-scattering approximation (fake ambient occlusion inversion)
    float3 multiScatter = weatherColor * _MultiScatterStrength * (1.0 - density * 0.5);
    
    // Ambient
    float3 ambient = _AmbientColor * weatherColor;
    
    // Storm core glow for high intensity
    float coreGlow = smoothstep(0.7, 1.0, intensity) * (1.0 - shadow);
    float3 coreEmission = _StormCoreColor.rgb * coreGlow * 2.0;
    
    // Combine
    float3 finalColor = weatherColor * (directLight + ambient + multiScatter) + coreEmission;
    
    return finalColor;
}

// ============================================
// RAYMARCHING
// ============================================

// Random jitter for ray start (reduces banding)
float getJitter(float2 screenPos)
{
    return frac(sin(dot(screenPos, float2(12.9898, 78.233))) * 43758.5453) * _JitterAmount;
}

// Main raymarching function
// Returns: RGB = color, A = transmittance
float4 raymarchVolume(float3 rayOrigin, float3 rayDir, float2 screenPos, float maxDist)
{
    // Intersect with volume bounds
    float2 tHit = rayBoxIntersection(rayOrigin, rayDir, _VolumeMin, _VolumeMax);
    
    if (tHit.x < 0.0)
        return float4(0, 0, 0, 1); // No intersection
    
    // Clamp to max distance
    tHit.y = min(tHit.y, maxDist);
    
    // Initialize raymarching
    float t = tHit.x + getJitter(screenPos) * _StepSize;
    float stepSize = _StepSize;
    
    float3 accumulatedColor = float3(0, 0, 0);
    float transmittance = 1.0;
    
    // Raymarch loop
    for (int i = 0; i < _RaymarchSteps; i++)
    {
        if (t > tHit.y || transmittance < (1.0 - _EarlyTerminationThreshold))
            break;
        
        float3 samplePos = rayOrigin + rayDir * t;
        float3 uvw = worldToVolumeUVW(samplePos);
        
        // Sample density
        float density = sampleCloudDensity(samplePos, uvw, 0.0);
        
        if (density > 0.001)
        {
            // Calculate lighting
            float3 sampleColor = calculateLighting(samplePos, uvw, density, -rayDir);
            
            // Beer-Lambert transmittance
            float sampleTransmittance = beerLambert(density, stepSize);
            
            // Front-to-back compositing
            float3 integScatter = sampleColor * (1.0 - sampleTransmittance);
            accumulatedColor += transmittance * integScatter;
            transmittance *= sampleTransmittance;
        }
        
        t += stepSize;
    }
    
    return float4(accumulatedColor, transmittance);
}

#endif // VOLUMETRIC_CLOUD_CORE_CGINC
