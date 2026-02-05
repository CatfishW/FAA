Shader "WeatherVisualization3D/VolumetricCloudWeatherURP"
{
    Properties
    {
        // 3D Noise Textures (from UnityVolumetricCloudsURP)
        [NoScaleOffset] _WorleyNoise("Worley Noise (128 RGBA)", 3D) = "white" {}
        [NoScaleOffset] _ErosionNoise("Erosion Noise (32 RGB)", 3D) = "white" {}
        [NoScaleOffset] _PerlinNoise("Perlin Noise (32 RGB)", 3D) = "white" {}
        [NoScaleOffset] _CloudLut("Cloud LUT (Rain AO)", 2D) = "white" {}

        // Volume Bounds
        _VolumeMin("Volume Min", Vector) = (-25000, 1000, -25000, 0)
        _VolumeMax("Volume Max", Vector) = (25000, 8000, 25000, 0)

        // Raymarching Settings
        _RaymarchSteps("Raymarch Steps", Range(16, 128)) = 48
        _LightSteps("Light Steps", Range(1, 16)) = 4
        _StepSize("Step Size", Range(100, 2000)) = 500
        _JitterAmount("Jitter Amount", Range(0, 1)) = 0.5

        // Cloud Shape
        _ShapeScale("Shape Scale", Range(0.1, 20)) = 3.0
        _ShapeFactor("Shape Factor", Range(0, 1)) = 0.7
        _ErosionScale("Erosion Scale", Range(1, 200)) = 50.0
        _ErosionFactor("Erosion Factor", Range(0, 1)) = 0.6
        _DensityMultiplier("Density Multiplier", Range(0, 2)) = 0.5

        // Height Gradient
        _CloudBaseHeight("Cloud Base Height", Range(0, 1)) = 0.2
        _CloudTopHeight("Cloud Top Height", Range(0, 1)) = 0.85
        _BaseSoftness("Base Softness", Range(0, 1)) = 0.3
        _TopSoftness("Top Softness", Range(0, 1)) = 0.5

        // Animation
        _WindSpeed("Wind Speed", Range(0, 100)) = 10.0
        _WindDirection("Wind Direction", Vector) = (1, 0, 0, 0)
        _ShapeEvolution("Shape Evolution", Range(0, 1)) = 0.1

        // Lighting
        _LightAbsorption("Light Absorption", Range(0, 1)) = 0.3
        _Scattering("Scattering", Range(0, 1)) = 0.5
        _SilverLining("Silver Lining", Range(0, 2)) = 0.8
        _AmbientIntensity("Ambient Intensity", Range(0, 2)) = 0.5
        _SunIntensity("Sun Intensity", Range(0, 3)) = 1.5

        // Weather Colors (Aviation standard)
        _LightColor_Weather("Light Weather Color", Color) = (0.2, 0.9, 0.2, 1)
        _ModerateColor("Moderate Color", Color) = (1.0, 0.95, 0.1, 1)
        _HeavyColor("Heavy Color", Color) = (1.0, 0.5, 0.1, 1)
        _IntenseColor("Intense Color", Color) = (1.0, 0.15, 0.15, 1)
        _ExtremeColor("Extreme Color", Color) = (1.0, 0.1, 0.8, 1)
        _ColorBlend("Weather Color Blend", Range(0, 1)) = 0.7

        // Debug
        [Toggle] _DebugNoise("Debug Noise", Float) = 0
        [Toggle] _DebugGradient("Debug Gradient", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #pragma multi_compile_fog

            #include "UnityCG.cginc"
            #include "Lighting.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float3 localPos : TEXCOORD1;
                float4 screenUV : TEXCOORD2;
            };

            // Textures
            sampler3D _WorleyNoise;
            sampler3D _ErosionNoise;
            sampler3D _PerlinNoise;
            sampler2D _CloudLut;

            // Volume Bounds
            float3 _VolumeMin;
            float3 _VolumeMax;

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
            float4 _WindDirection;
            float _ShapeEvolution;

            // Lighting
            float _LightAbsorption;
            float _Scattering;
            float _SilverLining;
            float _AmbientIntensity;
            float _SunIntensity;

            // Weather Colors
            float _ColorBlend;
            float4 _LightColor_Weather;
            float4 _ModerateColor;
            float4 _HeavyColor;
            float4 _IntenseColor;
            float4 _ExtremeColor;

            // Debug
            float _DebugNoise;
            float _DebugGradient;

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
                return (worldPos - _VolumeMin) / (_VolumeMax - _VolumeMin);
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
            // SAMPLE NOISE
            // ============================================
            float sampleShapeNoise(float3 pos, float time)
            {
                float3 animatedPos = pos + _WindDirection.xyz * time * _WindSpeed * 0.001;

                // Sample Perlin-Worley hybrid for large shapes
                float3 uvw = animatedPos * _ShapeScale * 0.01;
                float noise = tex3Dlod(_PerlinNoise, float4(uvw, 0)).r;

                // Add second octave from Worley
                float worley = tex3Dlod(_WorleyNoise, float4(uvw * 2.0, 1)).r;
                noise = lerp(noise, worley, 0.5);

                return noise;
            }

            float sampleErosionNoise(float3 pos, float time)
            {
                float3 animatedPos = pos + _WindDirection.xyz * time * _WindSpeed * 0.002;

                float3 uvw = animatedPos * _ErosionScale * 0.001;
                float noise = tex3Dlod(_ErosionNoise, float4(uvw, 0)).r;

                return noise;
            }

            // ============================================
            // CLOUD DENSITY
            // ============================================
            float sampleCloudDensity(float3 worldPos, float3 uvw, out float intensity)
            {
                intensity = 0.0;

                if (!isInsideVolume(uvw))
                    return 0.0;

                // Sample low-frequency shape noise
                float shapeNoise = sampleShapeNoise(worldPos, _Time.y);

                // Apply height gradient
                float height = uvw.y;
                float hGradient = heightGradient(height);

                // Base density from shape noise
                float density = shapeNoise * _ShapeFactor;

                // Remap density with coverage control
                float coverage = 0.7; // Fixed coverage for weather visualization
                density = saturate(DensityRemap(density, 1.0 - coverage, 1.0, 0.0, 1.0));

                // Apply erosion for detail
                float erosionNoise = sampleErosionNoise(worldPos, _Time.y);
                float erosionMask = smoothstep(0.3, 0.7, density);
                density -= erosionNoise * _ErosionFactor * erosionMask;

                // Apply height gradient
                density *= hGradient;

                // Final density multiplier
                density = saturate(density * _DensityMultiplier);

                intensity = density;

                return max(0.0, density);
            }

            float DensityRemap(float x, float a, float b, float c, float d)
            {
                return (((x - a) * 1.0/(b - a)) * (d - c)) + c;
            }

            // ============================================
            // WEATHER COLOR MAPPING
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
            float henyeyGreenstein(float cosTheta, float g)
            {
                float g2 = g * g;
                return (1.0 - g2) / (4.0 * 3.14159 * pow(1.0 + g2 - 2.0 * g * cosTheta, 1.5));
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
                // Isotropic + forward scattering
                float phase = lerp(0.25, henyeyGreenstein(cosTheta, _Scattering), 0.7);

                // Silver lining (bright edges)
                float silverLining = pow(max(0.0, cosTheta), 4.0) * _SilverLining;

                return _LightColor0.rgb * _SunIntensity * (phase + silverLining);
            }

            float calculateLightEnergy(float3 worldPos, float3 uvw, float density, float cosTheta)
            {
                if (_LightSteps <= 0)
                    return 1.0;

                float lightTransmittance = 1.0;
                float stepSize = _StepSize * 1.5;
                float3 lightStep = -normalize(_WorldSpaceLightPos0.xyz) * stepSize;
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

                // Apply powder effect
                lightTransmittance *= powderEffect(density, cosTheta);

                return lightTransmittance;
            }

            float3 calculateCloudLighting(float3 worldPos, float3 uvw, float density, float3 viewDir, float intensity)
            {
                // Weather color
                float3 weatherColor = intensityToWeatherColor(intensity);

                // Cosine of angle between view and light
                float cosTheta = dot(normalize(viewDir), -normalize(_WorldSpaceLightPos0.xyz));

                // Light energy (shadow + scattering)
                float lightEnergy = calculateLightEnergy(worldPos, uvw, density, cosTheta);

                // Scattering
                float3 scattering = calculateScattering(density, cosTheta);

                // Ambient
                float3 ambient = UNITY_LIGHTMODEL_AMBIENT.rgb * _AmbientIntensity;

                // Height-based ambient
                float heightFactor = smoothstep(_CloudBaseHeight, _CloudTopHeight, uvw.y);
                ambient = lerp(ambient * 0.5, ambient * 1.5, heightFactor);

                // Combine
                float3 extinction = weatherColor * density * _LightAbsorption;
                float3 inScattering = scattering * lightEnergy + ambient;

                return extinction * inScattering;
            }

            // ============================================
            // VERTEX SHADER
            // ============================================
            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.localPos = v.vertex.xyz;
                o.screenUV = ComputeScreenPos(o.vertex);
                return o;
            }

            // ============================================
            // FRAGMENT SHADER - RAYMARCHING
            // ============================================
            fixed4 frag(v2f i) : SV_Target
            {
                float3 rayOrigin = _WorldSpaceCameraPos;
                float3 rayDir = normalize(i.worldPos - rayOrigin);
                float2 screenUV = i.screenUV.xy / i.screenUV.w;

                // Debug modes
                if (_DebugNoise > 0.5)
                {
                    float3 center = (_VolumeMin + _VolumeMax) * 0.5;
                    float noise = sampleShapeNoise(center, _Time.y);
                    return fixed4(noise, noise, noise, 1.0);
                }
                else if (_DebugGradient > 0.5)
                {
                    float3 color;
                    color.r = heightGradient(0.25);
                    color.g = heightGradient(0.5);
                    color.b = heightGradient(0.75);
                    return fixed4(color, 1.0);
                }

                // Ray-box intersection
                float2 tHit = rayBoxIntersection(rayOrigin, rayDir, _VolumeMin, _VolumeMax);

                if (tHit.x < 0.0)
                    return fixed4(0, 0, 0, 0);

                // Jitter starting position to reduce banding
                float jitter = blueNoise(screenUV) * _JitterAmount;
                float t = tHit.x + jitter * _StepSize;

                float stepSize = _StepSize;

                float3 accumulatedColor = float3(0, 0, 0);
                float transmittance = 1.0;

                // Raymarch loop
                int steps = min(_RaymarchSteps, 64);
                for (int s = 0; s < steps; s++)
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

                        // Early termination
                        if (transmittance < 0.01)
                            break;
                    }

                    // Adaptive step size
                    float adaptiveStep = stepSize * (1.0 + density * 0.5);
                    t += adaptiveStep;
                }

                // Output with alpha
                float alpha = saturate((1.0 - transmittance) * 2.0);

                return fixed4(accumulatedColor, alpha);
            }
            ENDCG
        }
    }
}
