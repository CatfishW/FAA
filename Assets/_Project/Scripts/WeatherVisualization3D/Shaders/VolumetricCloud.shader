// Volumetric Weather Cloud Shader
// Part of WeatherVisualization3D System
// Raymarching-based volumetric cloud rendering for weather visualization
// 
// This shader renders weather data as true volumetric clouds using
// raymarching through a 3D density texture. It supports:
// - Aviation-standard weather intensity coloring
// - Height-based volumetric extrusion
// - Realistic cloud lighting with scattering
// - Storm core glow effects
// - Animated cloud detail

Shader "WeatherVisualization3D/VolumetricCloud"
{
    Properties
    {
        [Header(Volume Texture)]
        _DensityVolume ("Density Volume", 3D) = "white" {}
        
        [Header(Raymarching)]
        _RaymarchSteps ("Raymarch Steps", Range(32, 256)) = 128
        _StepSize ("Step Size", Range(10, 1000)) = 100
        _JitterAmount ("Jitter Amount", Range(0, 1)) = 0.5
        _EarlyTerminationThreshold ("Early Termination", Range(0.9, 0.99)) = 0.95
        
        [Header(Cloud Appearance)]
        _CloudDensity ("Cloud Density", Range(0.1, 5)) = 1.5
        _DetailScale ("Detail Scale", Range(0.5, 10)) = 3
        _DetailStrength ("Detail Strength", Range(0, 1)) = 0.5
        _EdgeSoftness ("Edge Softness", Range(0, 1)) = 0.3
        _AnimationSpeed ("Animation Speed", Range(0, 1)) = 0.1
        
        [Header(Lighting)]
        _LightDir ("Light Direction", Vector) = (0.5, 1, 0.5, 0)
        _LightColor ("Light Color", Color) = (1, 0.95, 0.9, 1)
        _AmbientColor ("Ambient Color", Color) = (0.4, 0.45, 0.5, 1)
        _LightAbsorption ("Light Absorption", Range(0.1, 2)) = 0.8
        _ForwardScattering ("Forward Scattering", Range(0, 1)) = 0.7
        _MultiScatterStrength ("Multi-Scatter Strength", Range(0, 1)) = 0.5
        _ShadowSteps ("Shadow Steps", Range(2, 16)) = 6
        [Toggle] _SelfShadowing ("Self Shadowing", Float) = 1
        
        [Header(Weather Colors)]
        _LightColor_Weather ("Light (Green)", Color) = (0, 0.85, 0, 0.7)
        _ModerateColor ("Moderate (Yellow)", Color) = (1, 1, 0, 0.8)
        _HeavyColor ("Heavy (Orange)", Color) = (1, 0.55, 0, 0.85)
        _IntenseColor ("Intense (Red)", Color) = (1, 0, 0, 0.9)
        _ExtremeColor ("Extreme (Magenta)", Color) = (1, 0, 1, 1)
        _StormCoreColor ("Storm Core", Color) = (1, 0.3, 0.3, 1)
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
            Name "VolumetricRaymarch"
            
            Cull Front
            ZWrite Off
            ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #pragma multi_compile_fog
            
            #include "UnityCG.cginc"
            #include "VolumetricCloudCore.cginc"
            
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
            };
            
            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.viewDir = o.worldPos - _WorldSpaceCameraPos;
                o.screenPos = ComputeScreenPos(o.pos);
                return o;
            }
            
            float4 frag(v2f i) : SV_Target
            {
                // Ray setup
                float3 rayOrigin = _WorldSpaceCameraPos;
                float3 rayDir = normalize(i.viewDir);
                float2 screenPos = i.screenPos.xy / i.screenPos.w * _ScreenParams.xy;
                
                // Calculate max distance (camera to far plane)
                float maxDist = length(i.viewDir) * 2.0;
                
                // Raymarch through volume
                float4 result = raymarchVolume(rayOrigin, rayDir, screenPos, maxDist);
                
                // Output: color with alpha from transmittance
                float alpha = 1.0 - result.a;
                
                // Apply fog
                UNITY_APPLY_FOG(i.pos.z, result.rgb);
                
                return float4(result.rgb, alpha);
            }
            ENDCG
        }
    }
    
    Fallback Off
}
