// Enhanced Volumetric Cloud Core Functions
// Based on HDRP Volumetric Clouds and industry techniques

#ifndef VOLUMETRIC_CLOUD_ENHANCED_CORE_CGINC
#define VOLUMETRIC_CLOUD_ENHANCED_CORE_CGINC

#include "WeatherNoiseEnhanced.cginc"

// ============================================
// UNIFORM DECLARATIONS
// ============================================

sampler3D _DensityVolume;
float4 _DensityVolume_TexelSize;

float3 _VolumeMin;
float3 _VolumeMax;
float3 _VolumeSize;
float3 _VolumeCenter;

// Raymarching
int _RaymarchSteps;
int _ShadowSteps;
float _StepSize;
float _JitterAmount;
float _BlueNoiseOffset;

// Shape
float _ShapeScale;
float _ErosionScale;
float _ShapeStrength;
float _ErosionStrength;
float _DensityMultiplier;

// Height gradient
float _CloudBaseHeight;
float _CloudTopHeight;
float _BaseSoftness;
float _TopSoftness;
float _AnvilAmount;

// Animation
float _WindSpeed;
float3 _WindDirection;
float _ShapeEvolution;
float _ErosionEvolution;

// Lighting
float3 _LightDir;
float3 _LightColor;
float3 _AmbientColor;
float _SunIntensity;
float _AmbientIntensity;
float _LightAbsorption;
float _Scattering;
float _SilverLining;
float _DarknessThreshold;

// Weather colors
float _ColorBlend;
float4 _LightColor_Weather;
float4 _ModerateColor;
float4 _HeavyColor;
float4 _IntenseColor;
float4 _ExtremeColor;
float4 _CoreGlow;

// Atmosphere
float _FogDensity;
float3 _FogColor;
float _HorizonBlend;

// Debug
float _DebugNoise;
float _DebugGradient;
float _DebugLighting;

// ============================================
// RAY-BOX INTERSECTION
// ============================================

float2 rayBoxIntersection(float3 rayOrigin, float3 rayDir, float3 boxMin, float3 boxMax)
{
    float3 invDir = 1.0 / (rayDir + 1e-6);
    float3 t0 = (boxMin - rayOrigin) * invDir;
    float3 t1 = (boxMax - rayOrigin) * invDir;

    float3 tmin = min(t0, t1);
    float3 tmax = max(t0, t1);

    float tNear = max(max(tmin.x, tmin.y), tmin.z);
    float tFar = min(min(tmax.x, tmax.y), tmax.z);

    if (tNear > tFar || tFar < 0.0)
        return float2(-1.0, -1.0);

    return float2(max(tNear, 0.0), tFar);
}

// ============================================
// COORDINATE TRANSFORMS
// ============================================

float3 worldToVolumeUVW(float3 worldPos)
{
    return (worldPos - _VolumeMin) / _VolumeSize;
}

bool isInsideVolume(float3 uvw)
{
    return all(uvw >= 0.0) && all(uvw <= 1.0);
}

// ============================================
// NOISE SAMPLING (PERLIN-WORLEY HYBRID)
// ============================================

// Sample base shape noise (large-scale Perlin)
float sampleShapeNoise(float3 pos, float time)
{
    float3 animatedPos = pos + _WindDirection.xyz * time * _WindSpeed * 0.01;
    animatedPos += float3(time * _ShapeEvolution, 0, time * _ShapeEvolution * 0.5);

    // Multi-octave FBM for base shape
    float noise = 0.0;
    float amplitude = 1.0;
    float frequency = _ShapeScale;

    for (int i = 0; i < 3; i++)
    {
        noise += amplitude * perlinNoise3D(animatedPos * frequency);
        amplitude *= 0.5;
        frequency *= 2.0;
    }

    return noise;
}

// Sample erosion noise (high-frequency Worley)
float sampleErosionNoise(float3 pos, float time)
{
    float3 animatedPos = pos + _WindDirection.xyz * time * _WindSpeed * 0.02;
    animatedPos += float3(time * _ErosionEvolution * 0.5, time * _ErosionEvolution, 0);

    // Worley FBM for detail/erosion
    float noise = 0.0;
    float amplitude = 1.0;
    float frequency = _ErosionScale * 0.1;

    for (int i = 0; i < 3; i++)
    {
        noise += amplitude * (1.0 - worleyNoise3D(animatedPos * frequency));
        amplitude *= 0.5;
        frequency *= 2.5;
    }

    return noise;
}

// ============================================
// HEIGHT GRADIENT
// ============================================

float heightGradient(float normalizedHeight)
{
    // Base gradient - clouds denser at bottom, wispy at top
    float baseFade = smoothstep(_CloudBaseHeight, _CloudBaseHeight + _BaseSoftness * 0.2, normalizedHeight);
    float topFade = 1.0 - smoothstep(_CloudTopHeight - _TopSoftness * 0.2, _CloudTopHeight, normalizedHeight);

    // Anvil shape for severe weather (bulge at mid-top)
    float anvilCenter = (_CloudBaseHeight + _CloudTopHeight) * 0.65;
    float anvilWidth = (_CloudTopHeight - _CloudBaseHeight) * 0.3;
    float anvil = 1.0 - abs(normalizedHeight - anvilCenter) / anvilWidth;
    anvil = saturate(anvil) * _AnvilAmount;

    return baseFade * topFade * (1.0 + anvil);
}

// ============================================
// DENSITY CALCULATION
// ============================================

float sampleCloudDensity(float3 worldPos, float3 uvw, out float intensity)
{
    // Sample base density from volume texture
    float4 volumeSample = tex3Dlod(_DensityVolume, float4(uvw, 0));
    float baseDensity = volumeSample.r;
    intensity = baseDensity;

    if (baseDensity < 0.01)
        return 0.0;

    // Get height for gradient
    float height = uvw.y;
    float hGradient = heightGradient(height);

    // Sample procedural noise for shape and erosion
    float shapeNoise = sampleShapeNoise(worldPos, _Time.y);
    float erosionNoise = sampleErosionNoise(worldPos, _Time.y);

    // Combine: base shape from texture, modulated by procedural noise
    float density = baseDensity * _ShapeStrength;

    // Add shape variation from noise
    density *= lerp(0.5, 1.5, shapeNoise);

    // Apply erosion (subtract detail noise)
    float erosionMask = smoothstep(0.3, 0.7, baseDensity);
    density -= erosionNoise * _ErosionStrength * erosionMask;

    // Apply height gradient
    density *= hGradient;

    // Remap density to sharpen edges
    density = saturate(density * _DensityMultiplier * 2.0); // Boost density for visibility
    density = pow(density, 0.8); // Soften instead of sharpen for more visible clouds

    return max(0.0, density);
}

// ============================================
// COLOR MAPPING (WEATHER INTENSITY)
// ============================================

float3 intensityToWeatherColor(float intensity)
{
    float3 color;

    if (intensity < 0.2)
        color = lerp(float3(0, 0, 0), _LightColor_Weather.rgb, smoothstep(0.0, 0.2, intensity));
    else if (intensity < 0.4)
        color = lerp(_LightColor_Weather.rgb, _ModerateColor.rgb, smoothstep(0.2, 0.4, intensity));
    else if (intensity < 0.6)
        color = lerp(_ModerateColor.rgb, _HeavyColor.rgb, smoothstep(0.4, 0.6, intensity));
    else if (intensity < 0.8)
        color = lerp(_HeavyColor.rgb, _IntenseColor.rgb, smoothstep(0.6, 0.8, intensity));
    else
        color = lerp(_IntenseColor.rgb, _ExtremeColor.rgb, smoothstep(0.8, 1.0, intensity));

    return color;
}

// ============================================
// LIGHTING
// ============================================

// Henyey-Greenstein phase function
float henyeyGreenstein(float cosTheta, float g)
{
    float g2 = g * g;
    return (1.0 - g2) / (4.0 * 3.14159 * pow(1.0 + g2 - 2.0 * g * cosTheta, 1.5));
}

// Beer-Lambert law
float beerLambert(float density, float distance)
{
    return exp(-density * _LightAbsorption * distance);
}

// Powder effect (dark edges when looking toward light)
float powderEffect(float density, float cosTheta)
{
    float powder = 1.0 - exp(-density * 2.0);
    return lerp(1.0, powder, smoothstep(0.0, -0.5, cosTheta));
}

// Light scattering calculation
float3 calculateScattering(float density, float cosTheta)
{
    // Isotropic + forward scattering
    float phase = lerp(0.25, henyeyGreenstein(cosTheta, _Scattering), 0.7);

    // Silver lining (bright edges)
    float silverLining = pow(max(0.0, cosTheta), 4.0) * _SilverLining;

    return _LightColor * _SunIntensity * (phase + silverLining);
}

// Calculate light energy at sample point
float calculateLightEnergy(float3 worldPos, float3 uvw, float density, float cosTheta)
{
    if (_ShadowSteps <= 0)
        return 1.0;

    float lightTransmittance = 1.0;
    float stepSize = _StepSize * 1.5;
    float3 lightStep = -normalize(_LightDir) * stepSize;
    float3 samplePos = worldPos;

    for (int i = 0; i < _ShadowSteps; i++)
    {
        samplePos += lightStep;
        float3 sampleUVW = worldToVolumeUVW(samplePos);

        if (!isInsideVolume(sampleUVW))
            break;

        float dummy;
        float sampleDensity = sampleCloudDensity(samplePos, sampleUVW, dummy);
        lightTransmittance *= beerLambert(sampleDensity, stepSize);

        if (lightTransmittance < 0.01)
            break;
    }

    // Apply powder effect
    lightTransmittance *= powderEffect(density, cosTheta);

    return lightTransmittance;
}

// Full lighting calculation
float3 calculateCloudLighting(float3 worldPos, float3 uvw, float density, float3 viewDir, float intensity)
{
    // Weather color
    float3 weatherColor = intensityToWeatherColor(intensity);

    // Cosine of angle between view and light
    float cosTheta = dot(normalize(viewDir), -normalize(_LightDir));

    // Light energy (shadow + scattering)
    float lightEnergy = calculateLightEnergy(worldPos, uvw, density, cosTheta);

    // Scattering
    float3 scattering = calculateScattering(density, cosTheta);

    // Ambient (sky approximation)
    float3 ambient = _AmbientColor * _AmbientIntensity;

    // Height-based ambient (darker at bottom, brighter at top)
    float heightFactor = smoothstep(_CloudBaseHeight, _CloudTopHeight, uvw.y);
    ambient = lerp(ambient * 0.5, ambient * 1.5, heightFactor);

    // Core glow for high intensity areas
    float coreGlow = smoothstep(0.75, 0.95, intensity) * (1.0 - lightEnergy) * 2.0;
    float3 emission = _CoreGlow.rgb * coreGlow;

    // Combine
    float3 extinction = weatherColor * density * _LightAbsorption;
    float3 inScattering = scattering * lightEnergy + ambient;

    // Energy conservation approximation
    float3 finalColor = extinction * inScattering + emission;

    // Blend between realistic white clouds and weather colors
    float3 naturalCloudColor = float3(0.95, 0.97, 1.0);
    finalColor = lerp(finalColor, naturalCloudColor * inScattering, (1.0 - intensity) * 0.5);

    return finalColor;
}

// ============================================
// RAYMARCHING
// ============================================

// Blue noise for jitter (reduces banding)
float blueNoise(float2 uv)
{
    float2 pixel = uv * _ScreenParams.xy;
    float random = frac(sin(dot(pixel, float2(12.9898, 78.233))) * 43758.5453);
    return random;
}

// Main enhanced raymarching function
float4 raymarchVolumeEnhanced(float3 rayOrigin, float3 rayDir, float2 screenUV, float maxDist)
{
    // Intersect with volume bounds
    float2 tHit = rayBoxIntersection(rayOrigin, rayDir, _VolumeMin, _VolumeMax);

    if (tHit.x < 0.0)
        return float4(0, 0, 0, 0);

    tHit.y = min(tHit.y, maxDist);

    // Jitter starting position to reduce banding
    float jitter = blueNoise(screenUV) * _JitterAmount;
    float t = tHit.x + jitter * _StepSize;

    float stepSize = _StepSize;

    float3 accumulatedColor = float3(0, 0, 0);
    float transmittance = 1.0;
    float accumulatedDensity = 0.0;

    // Raymarch loop
    for (int i = 0; i < _RaymarchSteps; i++)
    {
        if (t > tHit.y || transmittance < 0.01)
            break;

        float3 samplePos = rayOrigin + rayDir * t;
        float3 uvw = worldToVolumeUVW(samplePos);

        if (!isInsideVolume(uvw))
            break;

        float intensity;
        float density = sampleCloudDensity(samplePos, uvw, intensity);

        if (density > 0.001)
        {
            // Calculate lighting
            float3 sampleColor = calculateCloudLighting(samplePos, uvw, density, rayDir, intensity);

            // Beer-Lambert transmittance
            float sampleTransmittance = beerLambert(density, stepSize);

            // Front-to-back compositing
            float alpha = (1.0 - sampleTransmittance);
            accumulatedColor += transmittance * sampleColor * alpha;
            transmittance *= sampleTransmittance;
            accumulatedDensity += density;

            // Early termination if nearly opaque
            if (transmittance < 0.01)
                break;
        }

        // Adaptive step size based on density
        float adaptiveStep = stepSize * (1.0 + density * 0.5);
        t += adaptiveStep;
    }

    // Apply fog
    float fogFactor = 1.0 - exp(-accumulatedDensity * _FogDensity * 1000.0);
    accumulatedColor = lerp(accumulatedColor, _FogColor, fogFactor * _HorizonBlend);

    // Output with alpha (inverse transmittance for alpha blending)
    // Boost alpha for better visibility in Scene view
    float alpha = saturate((1.0 - transmittance) * 1.5);

    // Debug modes
    if (_DebugNoise > 0.5)
    {
        float3 center = (_VolumeMin + _VolumeMax) * 0.5;
        accumulatedColor = sampleShapeNoise(center, _Time.y) * float3(1, 1, 1);
        alpha = 1.0;
    }
    else if (_DebugGradient > 0.5)
    {
        accumulatedColor = float3(heightGradient(0.25), heightGradient(0.5), heightGradient(0.75));
        alpha = 1.0;
    }
    else if (_DebugLighting > 0.5)
    {
        accumulatedColor = calculateScattering(1.0, dot(rayDir, -normalize(_LightDir)));
        alpha = 1.0;
    }

    return float4(accumulatedColor, alpha);
}

#endif // VOLUMETRIC_CLOUD_ENHANCED_CORE_CGINC
