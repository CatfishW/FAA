// Weather Cloud Volume Core Functions
// Based on UnityVolumetricCloudsURP approach with real 3D noise textures

#ifndef WEATHER_CLOUD_VOLUME_CGINC
#define WEATHER_CLOUD_VOLUME_CGINC

#include "UnityCG.cginc"

// ============================================
// TEXTURE DECLARATIONS
// ============================================
sampler3D _WorleyNoise;
sampler3D _ErosionNoise;
sampler2D _CloudLut;

// Volume Bounds
float3 _VolumeMin;
float3 _VolumeMax;
float3 _VolumeSize;

// Raymarching
int _RaymarchSteps;
int _LightSteps;
float _StepSize;
float _JitterAmount;

// Shape
float _ShapeScale;
float _ShapeFactor;
float _ErosionScale;
float _ErosionFactor;
float _DensityMultiplier;

// Height
float _CloudBaseHeight;
float _CloudTopHeight;
float _BaseSoftness;
float _TopSoftness;

// Animation
float _WindSpeed;
float3 _WindDirection;
float _ShapeEvolution;
float _ErosionEvolution;

// Lighting
float _LightAbsorption;
float _Scattering;
float _SilverLining;
float _AmbientIntensity;
float _SunIntensity;
float3 _LightDir;
float3 _LightColor;

// Weather Colors
float _ColorBlend;
float4 _LightColor_Weather;
float4 _ModerateColor;
float4 _HeavyColor;
float4 _IntenseColor;
float4 _ExtremeColor;

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
// BLUE NOISE JITTER
// ============================================
float blueNoise(float2 uv)
{
    float2 pixel = uv * _ScreenParams.xy;
    float random = frac(sin(dot(pixel, float2(12.9898, 78.233))) * 43758.5453);
    return random;
}

// ============================================
// HEIGHT GRADIENT
// ============================================
float heightGradient(float normalizedHeight)
{
    float baseFade = smoothstep(_CloudBaseHeight, _CloudBaseHeight + _BaseSoftness * 0.2, normalizedHeight);
    float topFade = 1.0 - smoothstep(_CloudTopHeight - _TopSoftness * 0.2, _CloudTopHeight, normalizedHeight);
    return baseFade * topFade;
}

// ============================================
// NOISE SAMPLING (Using Real 3D Textures)
// ============================================
float sampleShapeNoise(float3 pos, float time)
{
    // Animate position with wind
    float3 animatedPos = pos + _WindDirection * time * _WindSpeed * 0.01;

    // Sample from 3D Worley noise texture
    // Scale coordinates for proper noise frequency
    float3 uvw = animatedPos * _ShapeScale * 0.0001;

    // Sample 3D texture with trilinear filtering
    float noise = tex3Dlod(_WorleyNoise, float4(uvw, 0)).r;

    // Add second channel for variation
    float noise2 = tex3Dlod(_WorleyNoise, float4(uvw * 2.0 + 0.5, 1)).g;
    noise = lerp(noise, noise2, 0.3);

    return noise;
}

float sampleErosionNoise(float3 pos, float time)
{
    float3 animatedPos = pos + _WindDirection * time * _WindSpeed * 0.02;
    float3 uvw = animatedPos * _ErosionScale * 0.0001 + float3(_ErosionEvolution, 0, 0);

    float noise = tex3Dlod(_ErosionNoise, float4(uvw, 0)).r;
    return noise;
}

// ============================================
// DENSITY CALCULATION
// ============================================
float DensityRemap(float x, float a, float b, float c, float d)
{
    return saturate(((x - a) / (b - a)) * (d - c) + c);
}

float sampleCloudDensity(float3 worldPos, float3 uvw, out float intensity)
{
    intensity = 0.0;

    if (!isInsideVolume(uvw))
        return 0.0;

    // Sample shape noise
    float shapeNoise = sampleShapeNoise(worldPos, _Time.y);

    // Apply height gradient
    float height = uvw.y;
    float hGradient = heightGradient(height);

    // Base density
    float coverage = 0.6; // Overall cloud coverage
    float density = DensityRemap(shapeNoise, (1.0 - _ShapeFactor) * coverage, 1.0, 0.0, 1.0);

    // Apply erosion
    float erosionNoise = sampleErosionNoise(worldPos, _Time.y);
    float erosionMask = smoothstep(0.2, 0.8, density);
    density = DensityRemap(density, erosionNoise * _ErosionFactor * erosionMask, 1.0, 0.0, 1.0);

    // Apply height gradient
    density *= hGradient;

    // Final density
    density = saturate(density * _DensityMultiplier);

    intensity = density;
    return density;
}

// ============================================
// WEATHER COLOR MAPPING
// ============================================
float3 intensityToWeatherColor(float intensity)
{
    float3 color;

    if (intensity < 0.2)
        color = lerp(float3(0.1, 0.1, 0.1), _LightColor_Weather.rgb, smoothstep(0.0, 0.2, intensity));
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
float henyeyGreenstein(float cosTheta, float g)
{
    float g2 = g * g;
    return (1.0 - g2) / (4.0 * 3.14159265 * pow(1.0 + g2 - 2.0 * g * cosTheta, 1.5));
}

float beerLambert(float density, float distance)
{
    return exp(-density * _LightAbsorption * distance);
}

float powderEffect(float density, float cosTheta)
{
    float powder = 1.0 - exp(-density * 4.0);
    powder = saturate(powder * 2.0);
    return lerp(1.0, powder, smoothstep(0.5, -0.5, cosTheta));
}

float3 calculateScattering(float density, float cosTheta)
{
    float phase = lerp(0.25, henyeyGreenstein(cosTheta, _Scattering), 0.7);
    float silverLining = pow(max(0.0, cosTheta), 4.0) * _SilverLining;
    return _LightColor * _SunIntensity * (phase + silverLining);
}

float calculateLightEnergy(float3 worldPos, float3 uvw, float density, float cosTheta)
{
    if (_LightSteps <= 0)
        return 1.0;

    float lightTransmittance = 1.0;
    float stepSize = _StepSize * 1.5;
    float3 lightStep = -normalize(_LightDir) * stepSize;
    float3 samplePos = worldPos;

    for (int i = 0; i < _LightSteps; i++)
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

    lightTransmittance *= powderEffect(density, cosTheta);
    return lightTransmittance;
}

float3 calculateCloudLighting(float3 worldPos, float3 uvw, float density, float3 viewDir, float intensity)
{
    float3 weatherColor = intensityToWeatherColor(intensity);
    float cosTheta = dot(normalize(viewDir), -normalize(_LightDir));

    float lightEnergy = calculateLightEnergy(worldPos, uvw, density, cosTheta);
    float3 scattering = calculateScattering(density, cosTheta);
    float3 ambient = UNITY_LIGHTMODEL_AMBIENT.rgb * _AmbientIntensity;

    float heightFactor = smoothstep(_CloudBaseHeight, _CloudTopHeight, uvw.y);
    ambient = lerp(ambient * 0.5, ambient * 1.5, heightFactor);

    float3 extinction = weatherColor * density * _LightAbsorption;
    float3 inScattering = scattering * lightEnergy + ambient;

    return extinction * inScattering;
}

// ============================================
// MAIN RAYMARCHING
// ============================================
float4 raymarchWeatherCloud(float3 rayOrigin, float3 rayDir, float2 screenUV, float maxDist)
{
    // Ray-box intersection
    float2 tHit = rayBoxIntersection(rayOrigin, rayDir, _VolumeMin, _VolumeMax);

    if (tHit.x < 0.0)
        return float4(0, 0, 0, 0);

    tHit.y = min(tHit.y, maxDist);

    // Jitter starting position
    float jitter = blueNoise(screenUV) * _JitterAmount;
    float t = tHit.x + jitter * _StepSize;

    float stepSize = _StepSize;
    float3 accumulatedColor = float3(0, 0, 0);
    float transmittance = 1.0;

    int steps = min(_RaymarchSteps, 64);

    for (int i = 0; i < steps; i++)
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
            float3 sampleColor = calculateCloudLighting(samplePos, uvw, density, rayDir, intensity);
            float sampleTransmittance = beerLambert(density, stepSize);

            float alpha = (1.0 - sampleTransmittance);
            accumulatedColor += transmittance * sampleColor * alpha;
            transmittance *= sampleTransmittance;

            if (transmittance < 0.01)
                break;
        }

        float adaptiveStep = stepSize * (1.0 + density * 0.5);
        t += adaptiveStep;
    }

    float alpha = saturate((1.0 - transmittance) * 2.0);
    return float4(accumulatedColor, alpha);
}

#endif // WEATHER_CLOUD_VOLUME_CGINC
