Shader "WeatherVisualization3D/WeatherCloudVolume"
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
        Tags { "RenderType"="Transparent" "Queue"="Transparent+100" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5

            #include "UnityCG.cginc"
            #include "./WeatherCloudVolume.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float4 screenUV : TEXCOORD1;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.screenUV = ComputeScreenPos(o.vertex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 rayOrigin = _WorldSpaceCameraPos;
                float3 rayDir = normalize(i.worldPos - rayOrigin);
                float2 screenUV = i.screenUV.xy / i.screenUV.w;

                // Set volume size for include
                _VolumeSize = _VolumeMax - _VolumeMin;

                float4 result = raymarchWeatherCloud(rayOrigin, rayDir, screenUV, 100000.0);
                return fixed4(result.rgb, result.a);
            }
            ENDCG
        }
    }
}
