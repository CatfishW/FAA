Shader "WeatherVisualization3D/WeatherCloudVolumeSRP"
{
    Properties
    {
        // 3D Noise Textures (from UnityVolumetricCloudsURP)
        [NoScaleOffset] _WorleyNoise("Worley Noise", 3D) = "white" {}
        [NoScaleOffset] _ErosionNoise("Erosion Noise", 3D) = "white" {}

        // Volume Bounds
        _VolumeMin("Volume Min", Vector) = (-25000, 1000, -25000)
        _VolumeMax("Volume Max", Vector) = (25000, 8000, 25000)

        // Raymarching
        _RaymarchSteps("Raymarch Steps", Range(16, 96)) = 48
        _LightSteps("Light Steps", Range(1, 8)) = 4
        _StepSize("Step Size", Range(100, 2000)) = 500
        _JitterAmount("Jitter Amount", Range(0, 1)) = 0.5

        // Cloud Shape
        _ShapeScale("Shape Scale", Range(0.1, 20)) = 3.0
        _ShapeFactor("Shape Factor", Range(0, 1)) = 0.7
        _ErosionScale("Erosion Scale", Range(1, 200)) = 50.0
        _ErosionFactor("Erosion Factor", Range(0, 1)) = 0.6
        _DensityMultiplier("Density Multiplier", Range(0, 2)) = 0.5

        // Height
        _CloudBaseHeight("Cloud Base Height", Range(0, 1)) = 0.2
        _CloudTopHeight("Cloud Top Height", Range(0, 1)) = 0.85
        _BaseSoftness("Base Softness", Range(0, 1)) = 0.3
        _TopSoftness("Top Softness", Range(0, 1)) = 0.5

        // Animation
        _WindSpeed("Wind Speed", Range(0, 100)) = 10.0
        _WindDirection("Wind Direction", Vector) = (1, 0, 0)
        _ShapeEvolution("Shape Evolution", Range(0, 1)) = 0.1
        _ErosionEvolution("Erosion Evolution", Range(0, 1)) = 0.05

        // Lighting
        _LightAbsorption("Light Absorption", Range(0, 1)) = 0.3
        _Scattering("Scattering", Range(0, 1)) = 0.5
        _SilverLining("Silver Lining", Range(0, 2)) = 0.8
        _AmbientIntensity("Ambient Intensity", Range(0, 2)) = 0.5
        _SunIntensity("Sun Intensity", Range(0, 3)) = 1.5
        _LightDir("Light Direction", Vector) = (0, 1, 0)
        _LightColor("Light Color", Color) = (1, 1, 1, 1)

        // Weather Colors
        _ColorBlend("Weather Color Blend", Range(0, 1)) = 0.7
        _LightColor_Weather("Light Weather", Color) = (0.2, 0.9, 0.2, 1)
        _ModerateColor("Moderate", Color) = (1.0, 0.95, 0.1, 1)
        _HeavyColor("Heavy", Color) = (1.0, 0.5, 0.1, 1)
        _IntenseColor("Intense", Color) = (1.0, 0.15, 0.15, 1)
        _ExtremeColor("Extreme", Color) = (1.0, 0.1, 0.8, 1)
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+100" "RenderPipeline"="UniversalPipeline" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "Volumetric Clouds SRP"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 vertex : POSITION;
            };

            struct Varyings
            {
                float4 vertex : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float4 screenUV : TEXCOORD1;
            };

            // Textures
            TEXTURE3D(_WorleyNoise);
            SAMPLER(sampler_WorleyNoise);
            TEXTURE3D(_ErosionNoise);
            SAMPLER(sampler_ErosionNoise);

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
            float3 _WindDirection;
            float _ShapeEvolution;

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
            // NOISE SAMPLING
            // ============================================
            float sampleShapeNoise(float3 pos, float time)
            {
                float3 animatedPos = pos + _WindDirection * time * _WindSpeed * 0.01;
                float3 uvw = animatedPos * _ShapeScale * 0.0001;
                float noise = SAMPLE_TEXTURE3D_LOD(_WorleyNoise, sampler_WorleyNoise, uvw, 0).r;
                float noise2 = SAMPLE_TEXTURE3D_LOD(_WorleyNoise, sampler_WorleyNoise, uvw * 2.0 + 0.5, 1).g;
                return lerp(noise, noise2, 0.3);
            }

            float sampleErosionNoise(float3 pos, float time)
            {
                float3 animatedPos = pos + _WindDirection * time * _WindSpeed * 0.02;
                float3 uvw = animatedPos * _ErosionScale * 0.0001 + float3(_ShapeEvolution, 0, 0);
                return SAMPLE_TEXTURE3D_LOD(_ErosionNoise, sampler_ErosionNoise, uvw, 0).r;
            }

            // ============================================
            // DENSITY
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

                float shapeNoise = sampleShapeNoise(worldPos, _Time.y);
                float height = uvw.y;
                float hGradient = heightGradient(height);

                float coverage = 0.6;
                float density = DensityRemap(shapeNoise, (1.0 - _ShapeFactor) * coverage, 1.0, 0.0, 1.0);

                float erosionNoise = sampleErosionNoise(worldPos, _Time.y);
                float erosionMask = smoothstep(0.2, 0.8, density);
                density = DensityRemap(density, erosionNoise * _ErosionFactor * erosionMask, 1.0, 0.0, 1.0);

                density *= hGradient;
                density = saturate(density * _DensityMultiplier);
                intensity = density;

                return max(0.0, density);
            }

            // ============================================
            // WEATHER COLOR
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

                float3 ambient = unity_AmbientSky.rgb * _AmbientIntensity;
                float heightFactor = smoothstep(_CloudBaseHeight, _CloudTopHeight, uvw.y);
                ambient = lerp(ambient * 0.5, ambient * 1.5, heightFactor);

                float3 extinction = weatherColor * density * _LightAbsorption;
                float3 inScattering = scattering * lightEnergy + ambient;

                return extinction * inScattering;
            }

            // ============================================
            // VERTEX SHADER
            // ============================================
            Varyings vert(Attributes v)
            {
                Varyings o;
                o.vertex = TransformObjectToHClip(v.vertex.xyz);
                o.worldPos = TransformObjectToWorld(v.vertex.xyz);
                o.screenUV = ComputeScreenPos(o.vertex);
                return o;
            }

            // ============================================
            // FRAGMENT SHADER
            // ============================================
            half4 frag(Varyings i) : SV_Target
            {
                float3 rayOrigin = GetCameraPositionWS();
                float3 rayDir = normalize(i.worldPos - rayOrigin);
                float2 screenUV = i.screenUV.xy / i.screenUV.w;

                // Ray-box intersection
                float2 tHit = rayBoxIntersection(rayOrigin, rayDir, _VolumeMin, _VolumeMax);

                if (tHit.x < 0.0)
                    return half4(0, 0, 0, 0);

                // Jitter starting position
                float jitter = blueNoise(screenUV) * _JitterAmount;
                float t = tHit.x + jitter * _StepSize;

                float stepSize = _StepSize;
                float3 accumulatedColor = float3(0, 0, 0);
                float transmittance = 1.0;

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
                return half4(accumulatedColor, alpha);
            }
            ENDHLSL
        }
    }
}
