// Weather Noise Functions for Volumetric Cloud Rendering
// Part of WeatherVisualization3D System
// Contains 3D Perlin and Worley noise functions for cloud detail

#ifndef WEATHER_NOISE_CGINC
#define WEATHER_NOISE_CGINC

// Hash functions for noise generation
float hash(float n)
{
    return frac(sin(n) * 43758.5453123);
}

float hash3(float3 p)
{
    p = frac(p * 0.3183099 + 0.1);
    p *= 17.0;
    return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
}

float3 hash33(float3 p)
{
    p = float3(dot(p, float3(127.1, 311.7, 74.7)),
               dot(p, float3(269.5, 183.3, 246.1)),
               dot(p, float3(113.5, 271.9, 124.6)));
    return frac(sin(p) * 43758.5453123);
}

// 3D Value Noise
float valueNoise3D(float3 p)
{
    float3 i = floor(p);
    float3 f = frac(p);
    
    // Smooth interpolation
    f = f * f * (3.0 - 2.0 * f);
    
    // Sample 8 corners
    float n000 = hash3(i + float3(0, 0, 0));
    float n001 = hash3(i + float3(0, 0, 1));
    float n010 = hash3(i + float3(0, 1, 0));
    float n011 = hash3(i + float3(0, 1, 1));
    float n100 = hash3(i + float3(1, 0, 0));
    float n101 = hash3(i + float3(1, 0, 1));
    float n110 = hash3(i + float3(1, 1, 0));
    float n111 = hash3(i + float3(1, 1, 1));
    
    // Trilinear interpolation
    float n00 = lerp(n000, n001, f.z);
    float n01 = lerp(n010, n011, f.z);
    float n10 = lerp(n100, n101, f.z);
    float n11 = lerp(n110, n111, f.z);
    
    float n0 = lerp(n00, n01, f.y);
    float n1 = lerp(n10, n11, f.y);
    
    return lerp(n0, n1, f.x);
}

// Gradient for Perlin noise
float3 gradient3D(float3 i)
{
    float h = hash3(i) * 16.0;
    int b = (int)h;
    float3 g = float3(
        (b & 1) ? 1.0 : -1.0,
        (b & 2) ? 1.0 : -1.0,
        (b & 4) ? 1.0 : -1.0
    );
    return normalize(g + hash33(i) * 0.5);
}

// 3D Perlin Noise
float perlinNoise3D(float3 p)
{
    float3 i = floor(p);
    float3 f = frac(p);
    
    // Quintic interpolation
    float3 u = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);
    
    // Gradients at 8 corners
    float3 g000 = gradient3D(i + float3(0, 0, 0));
    float3 g001 = gradient3D(i + float3(0, 0, 1));
    float3 g010 = gradient3D(i + float3(0, 1, 0));
    float3 g011 = gradient3D(i + float3(0, 1, 1));
    float3 g100 = gradient3D(i + float3(1, 0, 0));
    float3 g101 = gradient3D(i + float3(1, 0, 1));
    float3 g110 = gradient3D(i + float3(1, 1, 0));
    float3 g111 = gradient3D(i + float3(1, 1, 1));
    
    // Dot products
    float n000 = dot(g000, f - float3(0, 0, 0));
    float n001 = dot(g001, f - float3(0, 0, 1));
    float n010 = dot(g010, f - float3(0, 1, 0));
    float n011 = dot(g011, f - float3(0, 1, 1));
    float n100 = dot(g100, f - float3(1, 0, 0));
    float n101 = dot(g101, f - float3(1, 0, 1));
    float n110 = dot(g110, f - float3(1, 1, 0));
    float n111 = dot(g111, f - float3(1, 1, 1));
    
    // Trilinear interpolation
    float n00 = lerp(n000, n001, u.z);
    float n01 = lerp(n010, n011, u.z);
    float n10 = lerp(n100, n101, u.z);
    float n11 = lerp(n110, n111, u.z);
    
    float n0 = lerp(n00, n01, u.y);
    float n1 = lerp(n10, n11, u.y);
    
    return lerp(n0, n1, u.x) * 0.5 + 0.5; // Remap to 0-1
}

// Worley (Cellular) Noise for cloud edges
float worleyNoise3D(float3 p)
{
    float3 i = floor(p);
    float3 f = frac(p);
    
    float minDist = 1.0;
    
    // Check 3x3x3 neighborhood
    for (int x = -1; x <= 1; x++)
    {
        for (int y = -1; y <= 1; y++)
        {
            for (int z = -1; z <= 1; z++)
            {
                float3 neighbor = float3(x, y, z);
                float3 cellPos = hash33(i + neighbor);
                float3 diff = neighbor + cellPos - f;
                float dist = length(diff);
                minDist = min(minDist, dist);
            }
        }
    }
    
    return minDist;
}

// Inverse Worley for cloud density
float inverseWorley3D(float3 p)
{
    return 1.0 - worleyNoise3D(p);
}

// Fractal Brownian Motion (FBM) with Perlin
float fbmPerlin3D(float3 p, int octaves, float lacunarity, float persistence)
{
    float value = 0.0;
    float amplitude = 1.0;
    float frequency = 1.0;
    float maxValue = 0.0;
    
    for (int i = 0; i < octaves; i++)
    {
        value += amplitude * perlinNoise3D(p * frequency);
        maxValue += amplitude;
        amplitude *= persistence;
        frequency *= lacunarity;
    }
    
    return value / maxValue;
}

// FBM with Worley
float fbmWorley3D(float3 p, int octaves, float lacunarity, float persistence)
{
    float value = 0.0;
    float amplitude = 1.0;
    float frequency = 1.0;
    float maxValue = 0.0;
    
    for (int i = 0; i < octaves; i++)
    {
        value += amplitude * inverseWorley3D(p * frequency);
        maxValue += amplitude;
        amplitude *= persistence;
        frequency *= lacunarity;
    }
    
    return value / maxValue;
}

// Combined cloud noise (Perlin-Worley hybrid)
// This creates realistic cloud shapes
float cloudNoise3D(float3 p, float time, float detailScale, float detailStrength)
{
    // Base shape with low-frequency Perlin
    float baseShape = fbmPerlin3D(p * 0.5, 4, 2.0, 0.5);
    
    // Detail with higher frequency Worley
    float3 detailPos = p * detailScale + float3(time * 0.1, 0, time * 0.05);
    float detail = fbmWorley3D(detailPos, 3, 2.5, 0.4);
    
    // Combine: base shape modulated by detail
    float noise = baseShape - detail * detailStrength;
    
    return saturate(noise);
}

// Animated turbulence for cloud movement
float turbulenceNoise3D(float3 p, float time)
{
    float3 animatedPos = p + float3(
        sin(time * 0.3 + p.z * 0.1) * 0.5,
        cos(time * 0.2 + p.x * 0.1) * 0.3,
        sin(time * 0.25 + p.y * 0.1) * 0.4
    );
    
    return fbmPerlin3D(animatedPos, 3, 2.0, 0.5);
}

// Height-based density gradient for realistic cloud profiles
float heightGradient(float normalizedHeight, float cloudBase, float cloudTop)
{
    // Clouds typically denser at base, wispy at top
    float t = saturate((normalizedHeight - cloudBase) / (cloudTop - cloudBase));
    
    // Anvil shape for thunderstorms
    float base = smoothstep(0.0, 0.1, t);
    float top = 1.0 - smoothstep(0.7, 1.0, t);
    float bulge = 1.0 - abs(t - 0.3) * 2.0;
    bulge = max(0, bulge);
    
    return base * top * (1.0 + bulge * 0.5);
}

// Storm cell shape function
float stormCellShape(float3 localPos, float intensity)
{
    // Ellipsoid base shape
    float3 scale = float3(1.0, 1.5 + intensity, 1.0);
    float dist = length(localPos / scale);
    
    // Core is denser
    float core = 1.0 - smoothstep(0.0, 0.3, dist);
    float outer = 1.0 - smoothstep(0.3, 1.0, dist);
    
    return lerp(outer, core, intensity * 0.5);
}

#endif // WEATHER_NOISE_CGINC
