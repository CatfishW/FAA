// Enhanced Weather Noise Functions
// Improved Perlin-Worley noise for realistic cloud rendering
// Based on HDRP Volumetric Clouds noise approach

#ifndef WEATHER_NOISE_ENHANCED_CGINC
#define WEATHER_NOISE_ENHANCED_CGINC

// ============================================
// HASH FUNCTIONS
// ============================================

float2 hash22(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.xx + p3.yz) * p3.zy);
}

float3 hash33(float3 p)
{
    p = float3(dot(p, float3(127.1, 311.7, 74.7)),
               dot(p, float3(269.5, 183.3, 246.1)),
               dot(p, float3(113.5, 271.9, 124.6)));
    return frac(sin(p) * 43758.5453);
}

float hash(float n)
{
    return frac(sin(n) * 43758.5453123);
}

// ============================================
// QUINTIC INTERPOLATION (smoother than cubic)
// ============================================

float quintic(float t)
{
    return t * t * t * (t * (t * 6.0 - 15.0) + 10.0);
}

float3 quintic3(float3 t)
{
    return t * t * t * (t * (t * 6.0 - 15.0) + 10.0);
}

// ============================================
// IMPROVED 3D PERLIN NOISE
// ============================================

float3 perlinGradient3D(float3 i)
{
    float h = hash(dot(i, float3(1.0, 57.0, 113.0))) * 16.0;
    int b = (int)h;

    // Select from 16 gradient directions
    float3 g = float3(
        (b & 1) ? 1.0 : -1.0,
        (b & 2) ? 1.0 : -1.0,
        (b & 4) ? 1.0 : -1.0
    );

    // Add randomness to gradient
    g += hash33(i) * 0.3;
    return normalize(g);
}

float perlinNoise3D(float3 p)
{
    float3 i = floor(p);
    float3 f = frac(p);

    // Quintic interpolation for smoother results
    float3 u = quintic3(f);

    // Gradients at 8 corners
    float c000 = dot(perlinGradient3D(i + float3(0, 0, 0)), f - float3(0, 0, 0));
    float c001 = dot(perlinGradient3D(i + float3(0, 0, 1)), f - float3(0, 0, 1));
    float c010 = dot(perlinGradient3D(i + float3(0, 1, 0)), f - float3(0, 1, 0));
    float c011 = dot(perlinGradient3D(i + float3(0, 1, 1)), f - float3(0, 1, 1));
    float c100 = dot(perlinGradient3D(i + float3(1, 0, 0)), f - float3(1, 0, 0));
    float c101 = dot(perlinGradient3D(i + float3(1, 0, 1)), f - float3(1, 0, 1));
    float c110 = dot(perlinGradient3D(i + float3(1, 1, 0)), f - float3(1, 1, 0));
    float c111 = dot(perlinGradient3D(i + float3(1, 1, 1)), f - float3(1, 1, 1));

    // Trilinear interpolation
    float c00 = lerp(c000, c001, u.z);
    float c01 = lerp(c010, c011, u.z);
    float c10 = lerp(c100, c101, u.z);
    float c11 = lerp(c110, c111, u.z);

    float c0 = lerp(c00, c01, u.y);
    float c1 = lerp(c10, c11, u.y);

    return lerp(c0, c1, u.x) * 0.5 + 0.5;
}

// ============================================
// IMPROVED WORLEY (CELLULAR) NOISE
// ============================================

float worleyNoise3D(float3 p)
{
    float3 i = floor(p);
    float3 f = frac(p);

    float minDist = 1.0;
    float secondMinDist = 1.0;

    // Check 3x3x3 neighborhood
    for (int x = -1; x <= 1; x++)
    {
        for (int y = -1; y <= 1; y++)
        {
            for (int z = -1; z <= 1; z++)
            {
                float3 neighbor = float3(x, y, z);
                float3 cellPos = hash33(i + neighbor);

                // Animate cell points slightly
                cellPos = 0.5 + 0.5 * sin(cellPos * 6.28 + _Time.y * 0.1);

                float3 diff = neighbor + cellPos - f;
                float dist = length(diff);

                if (dist < minDist)
                {
                    secondMinDist = minDist;
                    minDist = dist;
                }
                else if (dist < secondMinDist)
                {
                    secondMinDist = dist;
                }
            }
        }
    }

    // Return distance with some variation
    return minDist;
}

// F1-F2 Worley (difference between closest and 2nd closest)
float worleyF1F2_3D(float3 p)
{
    float3 i = floor(p);
    float3 f = frac(p);

    float minDist = 1.0;
    float secondMinDist = 1.0;

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

                if (dist < minDist)
                {
                    secondMinDist = minDist;
                    minDist = dist;
                }
                else if (dist < secondMinDist)
                {
                    secondMinDist = dist;
                }
            }
        }
    }

    return secondMinDist - minDist;
}

// ============================================
// FRACTAL BROWNIAN MOTION (FBM)
// ============================================

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

float fbmWorley3D(float3 p, int octaves, float lacunarity, float persistence)
{
    float value = 0.0;
    float amplitude = 1.0;
    float frequency = 1.0;
    float maxValue = 0.0;

    for (int i = 0; i < octaves; i++)
    {
        value += amplitude * (1.0 - worleyNoise3D(p * frequency));
        maxValue += amplitude;
        amplitude *= persistence;
        frequency *= lacunarity;
    }

    return value / maxValue;
}

// Perlin-Worley hybrid (industry standard for clouds)
float perlinWorley3D(float3 p, float time)
{
    // Base shape from Perlin
    float perlin = fbmPerlin3D(p, 3, 2.0, 0.5);

    // Detail from Worley (inverted for billowy shapes)
    float worley = fbmWorley3D(p * 3.0 + time * 0.1, 3, 2.5, 0.4);

    // Combine: Perlin for large shapes, Worley for detail/erosion
    return saturate(perlin * 0.7 + worley * 0.3);
}

// ============================================
// CURL NOISE (for turbulent motion)
// ============================================

float3 curlNoise3D(float3 p)
{
    float eps = 0.01;

    // Sample noise at offsets
    float n1 = perlinNoise3D(p + float3(eps, 0, 0));
    float n2 = perlinNoise3D(p - float3(eps, 0, 0));
    float n3 = perlinNoise3D(p + float3(0, eps, 0));
    float n4 = perlinNoise3D(p - float3(0, eps, 0));
    float n5 = perlinNoise3D(p + float3(0, 0, eps));
    float n6 = perlinNoise3D(p - float3(0, 0, eps));

    // Calculate curl
    float3 curl;
    curl.x = (n4 - n3) / (2.0 * eps) - (n6 - n5) / (2.0 * eps);
    curl.y = (n6 - n5) / (2.0 * eps) - (n2 - n1) / (2.0 * eps);
    curl.z = (n2 - n1) / (2.0 * eps) - (n4 - n3) / (2.0 * eps);

    return normalize(curl + 0.0001);
}

// ============================================
// UTILITY FUNCTIONS
// ============================================

// Remap value from one range to another
float remap(float value, float oldMin, float oldMax, float newMin, float newMax)
{
    return newMin + (value - oldMin) * (newMax - newMin) / (oldMax - oldMin);
}

// Smooth maximum (softer than regular max)
float smoothMax(float a, float b, float k)
{
    return log(exp(k * a) + exp(k * b)) / k;
}

// Smooth minimum
float smoothMin(float a, float b, float k)
{
    return -smoothMax(-a, -b, k);
}

// Domain warp for more interesting shapes
float3 domainWarp3D(float3 p, float warpAmount)
{
    float3 warp = float3(
        perlinNoise3D(p + float3(0, 0, 0)),
        perlinNoise3D(p + float3(5.2, 1.3, 0)),
        perlinNoise3D(p + float3(1.7, 9.2, 0))
    );
    return p + (warp - 0.5) * warpAmount;
}

#endif // WEATHER_NOISE_ENHANCED_CGINC
