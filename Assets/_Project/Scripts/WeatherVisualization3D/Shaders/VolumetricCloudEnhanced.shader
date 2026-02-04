// Enhanced Volumetric Weather Cloud Shader
// Based on research from HDRP Volumetric Clouds and industry techniques
// Features: Perlin-Worley noise, proper edge erosion, height gradients, realistic scattering
//
// References:
// - Unity HDRP Volumetric Clouds (2025)
// - "Real-time Cloudscapes with Volumetric Raymarching" by Maxime Heckel
// - "Production Volume Rendering" by Pixar (SIGGRAPH 2017)

Shader "WeatherVisualization3D/VolumetricCloudEnhanced"
{
    Properties
    {
        [Header(Volume Texture)]
        _DensityVolume ("Density Volume", 3D) = "white" {}

        [Header(Raymarching Quality)]
        _RaymarchSteps ("Primary Steps", Range(64, 512)) = 256
        _ShadowSteps ("Light Steps", Range(4, 32)) = 16
        _StepSize ("Step Size", Range(1, 500)) = 50
        _JitterAmount ("Jitter Amount", Range(0, 1)) = 0.3
        _BlueNoiseOffset ("Blue Noise Offset", Range(0, 1)) = 0.5

        [Header(Cloud Shape)]
        _ShapeScale ("Shape Scale", Range(0.1, 5)) = 1.0
        _ErosionScale ("Erosion Scale", Range(1, 50)) = 20
        _ShapeStrength ("Shape Strength", Range(0, 2)) = 1.2
        _ErosionStrength ("Erosion Strength", Range(0, 1.5)) = 0.8
        _DensityMultiplier ("Density Multiplier", Range(0.1, 5)) = 2.0

        [Header(Height Gradient)]
        _CloudBaseHeight ("Cloud Base (0-1)", Range(0, 0.5)) = 0.0
        _CloudTopHeight ("Cloud Top (0.5-1)", Range(0.5, 1)) = 1.0
        _BaseSoftness ("Base Softness", Range(0, 1)) = 0.3
        _TopSoftness ("Top Softness", Range(0, 1)) = 0.5
        _AnvilAmount ("Anvil Amount", Range(0, 1)) = 0.3

        [Header(Animation)]
        _WindSpeed ("Wind Speed", Range(0, 100)) = 20
        _WindDirection ("Wind Direction", Vector) = (1, 0, 0, 0)
        _ShapeEvolution ("Shape Evolution", Range(0, 1)) = 0.1
        _ErosionEvolution ("Erosion Evolution", Range(0, 1)) = 0.3

        [Header(Lighting)]
        _LightDir ("Light Direction", Vector) = (0.5, 1, 0.3, 0)
        _LightColor ("Light Color", Color) = (1, 0.97, 0.9, 1)
        _AmbientColor ("Ambient Color", Color) = (0.3, 0.35, 0.4, 1)
        _SunIntensity ("Sun Intensity", Range(0.5, 5)) = 1.5
        _AmbientIntensity ("Ambient Intensity", Range(0.1, 2)) = 0.8
        _LightAbsorption ("Light Absorption", Range(0.1, 3)) = 1.2
        _Scattering ("Scattering", Range(0, 1)) = 0.8
        _SilverLining ("Silver Lining", Range(0, 2)) = 1.0
        _DarknessThreshold ("Darkness Threshold", Range(0, 1)) = 0.3

        [Header(Weather Colors)]
        _ColorBlend ("Color Blend", Range(0, 1)) = 0.6
        _LightColor_Weather ("Light (Green)", Color) = (0.2, 0.9, 0.2, 0.6)
        _ModerateColor ("Moderate (Yellow)", Color) = (1, 0.95, 0.1, 0.75)
        _HeavyColor ("Heavy (Orange)", Color) = (1, 0.5, 0.1, 0.85)
        _IntenseColor ("Intense (Red)", Color) = (1, 0.15, 0.15, 0.9)
        _ExtremeColor ("Extreme (Magenta)", Color) = (1, 0.1, 0.8, 0.95)
        _CoreGlow ("Core Glow", Color) = (1, 0.6, 0.4, 1)

        [Header(Atmosphere)]
        _FogDensity ("Fog Density", Range(0, 0.1)) = 0.001
        _FogColor ("Fog Color", Color) = (0.6, 0.7, 0.8, 1)
        _HorizonBlend ("Horizon Blend", Range(0, 1)) = 0.5

        [Header(Debug)]
        [Toggle] _DebugNoise ("Debug Noise", Float) = 0
        [Toggle] _DebugGradient ("Debug Gradient", Float) = 0
        [Toggle] _DebugLighting ("Debug Lighting", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+100"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "VolumetricRaymarchEnhanced"

            Cull Front
            ZWrite Off
            ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.0
            #pragma multi_compile_fog

            #include "UnityCG.cginc"
            #include "VolumetricCloudEnhancedCore.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float3 viewDir : TEXCOORD1;
                float4 screenPos : TEXCOORD2;
                float3 cameraPos : TEXCOORD3;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.viewDir = normalize(o.worldPos - _WorldSpaceCameraPos);
                o.screenPos = ComputeScreenPos(o.pos);
                o.cameraPos = _WorldSpaceCameraPos;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                // Ray setup
                float3 rayOrigin = i.cameraPos;
                float3 rayDir = normalize(i.viewDir);
                float2 screenUV = i.screenPos.xy / i.screenPos.w;

                // Calculate max distance
                float maxDist = length(i.worldPos - rayOrigin) * 2.0;

                // Raymarch through volume
                float4 result = raymarchVolumeEnhanced(rayOrigin, rayDir, screenUV, maxDist);

                // Apply fog
                UNITY_APPLY_FOG(i.pos.z, result.rgb);

                return result;
            }
            ENDCG
        }
    }

    Fallback Off
}
