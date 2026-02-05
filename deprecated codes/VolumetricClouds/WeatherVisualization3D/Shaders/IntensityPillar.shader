// Intensity Pillar Shader - Production Ready
// Part of WeatherVisualization3D System
// Renders semi-transparent vertical intensity pillars with realistic volumetric appearance

Shader "IntensityPillar"
{
    Properties
    {
        [Header(Base Settings)]
        _Color ("Tint Color", Color) = (0, 1, 0, 0.3)
        _Intensity ("Intensity", Range(0, 1)) = 0.5
        _Opacity ("Base Opacity", Range(0, 1)) = 0.4
        
        [Header(Gradient)]
        _TopFade ("Top Fade", Range(0, 1)) = 0.8
        _BottomFade ("Bottom Fade", Range(0, 1)) = 0.2
        _EdgeFalloff ("Edge Falloff", Range(0.1, 3)) = 1.5
        
        [Header(Animation)]
        _PulseSpeed ("Pulse Speed", Range(0, 3)) = 1.0
        _PulseAmount ("Pulse Amount", Range(0, 0.3)) = 0.1
        _VerticalWaveSpeed ("Vertical Wave Speed", Range(0, 2)) = 0.5
        _VerticalWaveScale ("Vertical Wave Scale", Range(1, 10)) = 3
        
        [Header(Volumetric Effect)]
        _FresnelPower ("Fresnel Power", Range(0.5, 5)) = 2.0
        _FresnelIntensity ("Fresnel Intensity", Range(0, 1)) = 0.3
        _InnerGlow ("Inner Glow", Range(0, 1)) = 0.2
        _GlowIntensity ("Edge Glow Intensity", Range(0, 2)) = 0.5
        
        [Header(Noise)]
        _NoiseScale ("Noise Scale", Range(0.001, 0.1)) = 0.01
        _NoiseStrength ("Noise Strength", Range(0, 1)) = 0.3
        _NoiseSpeed ("Noise Animation Speed", Range(0, 1)) = 0.2
        
        [Header(Weather Colors - Set by Script)]
        _LightColor ("Light (Green)", Color) = (0.2, 0.9, 0.2, 0.4)
        _ModerateColor ("Moderate (Yellow)", Color) = (0.95, 0.9, 0.2, 0.5)
        _HeavyColor ("Heavy (Orange)", Color) = (1, 0.5, 0.1, 0.6)
        _IntenseColor ("Intense (Red)", Color) = (0.95, 0.15, 0.1, 0.7)
        _ExtremeColor ("Extreme (Magenta)", Color) = (0.95, 0.2, 0.8, 0.8)
    }
    
    SubShader
    {
        Tags 
        { 
            "RenderType" = "Transparent" 
            "Queue" = "Transparent+50"
            "IgnoreProjector" = "True"
            "ForceNoShadowCasting" = "True"
        }
        
        Pass
        {
            Name "IntensityPillar"
            
            Cull Back
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_fog
            
            #include "UnityCG.cginc"
            
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float3 normal : NORMAL;
            };
            
            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float3 worldNormal : TEXCOORD2;
                float3 viewDir : TEXCOORD3;
                float3 objectPos : TEXCOORD4;
                UNITY_FOG_COORDS(5)
            };
            
            // Properties
            float4 _Color;
            float _Intensity;
            float _Opacity;
            float _TopFade;
            float _BottomFade;
            float _EdgeFalloff;
            float _PulseSpeed;
            float _PulseAmount;
            float _VerticalWaveSpeed;
            float _VerticalWaveScale;
            float _FresnelPower;
            float _FresnelIntensity;
            float _InnerGlow;
            float _GlowIntensity;
            float _NoiseScale;
            float _NoiseStrength;
            float _NoiseSpeed;
            
            float4 _LightColor;
            float4 _ModerateColor;
            float4 _HeavyColor;
            float4 _IntenseColor;
            float4 _ExtremeColor;
            
            // Simple 3D noise function
            float hash(float3 p)
            {
                p = frac(p * float3(443.897, 441.423, 437.195));
                p += dot(p, p.yxz + 19.19);
                return frac((p.x + p.y) * p.z);
            }
            
            float noise3D(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                
                float n = lerp(
                    lerp(
                        lerp(hash(i), hash(i + float3(1,0,0)), f.x),
                        lerp(hash(i + float3(0,1,0)), hash(i + float3(1,1,0)), f.x),
                        f.y),
                    lerp(
                        lerp(hash(i + float3(0,0,1)), hash(i + float3(1,0,1)), f.x),
                        lerp(hash(i + float3(0,1,1)), hash(i + float3(1,1,1)), f.x),
                        f.y),
                    f.z);
                return n;
            }
            
            // Map intensity to weather color
            float4 getIntensityColor(float intensity)
            {
                float4 col;
                if (intensity < 0.2)
                    col = lerp(float4(0,0,0,0), _LightColor, intensity / 0.2);
                else if (intensity < 0.4)
                    col = lerp(_LightColor, _ModerateColor, (intensity - 0.2) / 0.2);
                else if (intensity < 0.6)
                    col = lerp(_ModerateColor, _HeavyColor, (intensity - 0.4) / 0.2);
                else if (intensity < 0.8)
                    col = lerp(_HeavyColor, _IntenseColor, (intensity - 0.6) / 0.2);
                else
                    col = lerp(_IntenseColor, _ExtremeColor, (intensity - 0.8) / 0.2);
                return col;
            }
            
            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.viewDir = normalize(_WorldSpaceCameraPos - o.worldPos);
                o.objectPos = v.vertex.xyz;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }
            
            float4 frag(v2f i) : SV_Target
            {
                // Base weather color from intensity
                float4 weatherColor = getIntensityColor(_Intensity);
                float4 tintedColor = weatherColor * _Color;
                
                // Height coordinate (0 at bottom, 1 at top)
                float height = saturate(i.uv.y);
                
                // Edge distance for cylindrical fade
                float2 centeredXZ = float2(i.objectPos.x, i.objectPos.z);
                float edgeDist = length(centeredXZ);
                float edgeFade = 1.0 - pow(saturate(edgeDist), _EdgeFalloff);
                
                // Height-based fade
                float topFade = 1.0 - smoothstep(1.0 - _TopFade, 1.0, height);
                float bottomFade = smoothstep(0.0, _BottomFade, height);
                float heightFade = topFade * bottomFade;
                
                // Animated noise for organic look
                float time = _Time.y;
                float3 noisePos = i.worldPos * _NoiseScale + float3(0, time * _NoiseSpeed, 0);
                float noiseVal = noise3D(noisePos) * 2.0 - 1.0;
                float noiseModifier = 1.0 + noiseVal * _NoiseStrength;
                
                // Vertical wave animation
                float wave = sin(height * _VerticalWaveScale * 3.14159 + time * _VerticalWaveSpeed) * 0.5 + 0.5;
                float waveModifier = 1.0 + wave * 0.1;
                
                // Pulsing animation
                float pulse = 1.0 + sin(time * _PulseSpeed) * _PulseAmount;
                
                // Fresnel effect for edge glow
                float fresnel = pow(1.0 - saturate(dot(i.viewDir, i.worldNormal)), _FresnelPower);
                float fresnelGlow = fresnel * _FresnelIntensity;
                
                // Inner volumetric glow (brighter toward center)
                float innerGlow = (1.0 - edgeDist) * _InnerGlow;
                
                // Combine all effects
                float combinedAlpha = edgeFade * heightFade * noiseModifier * waveModifier * pulse;
                combinedAlpha *= _Opacity * weatherColor.a;
                combinedAlpha = saturate(combinedAlpha);
                
                // Final color with glow effects
                float3 finalColor = tintedColor.rgb;
                finalColor += weatherColor.rgb * (fresnelGlow + innerGlow) * _GlowIntensity;
                finalColor *= pulse;
                
                // Soften very low alpha regions
                combinedAlpha *= smoothstep(0.01, 0.1, combinedAlpha);
                
                float4 result = float4(finalColor, combinedAlpha);
                
                // Apply fog
                UNITY_APPLY_FOG(i.fogCoord, result);
                
                return result;
            }
            ENDCG
        }
        
        // Second pass for back faces (interior glow)
        Pass
        {
            Name "IntensityPillarInner"
            
            Cull Front
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha One
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            
            #include "UnityCG.cginc"
            
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float3 normal : NORMAL;
            };
            
            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float3 viewDir : TEXCOORD2;
            };
            
            float4 _Color;
            float _Intensity;
            float _Opacity;
            float _InnerGlow;
            float _TopFade;
            
            float4 _LightColor;
            float4 _ModerateColor;
            float4 _HeavyColor;
            float4 _IntenseColor;
            float4 _ExtremeColor;
            
            float4 getIntensityColor(float intensity)
            {
                float4 col;
                if (intensity < 0.2)
                    col = lerp(float4(0,0,0,0), _LightColor, intensity / 0.2);
                else if (intensity < 0.4)
                    col = lerp(_LightColor, _ModerateColor, (intensity - 0.2) / 0.2);
                else if (intensity < 0.6)
                    col = lerp(_ModerateColor, _HeavyColor, (intensity - 0.4) / 0.2);
                else if (intensity < 0.8)
                    col = lerp(_HeavyColor, _IntenseColor, (intensity - 0.6) / 0.2);
                else
                    col = lerp(_IntenseColor, _ExtremeColor, (intensity - 0.8) / 0.2);
                return col;
            }
            
            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.viewDir = normalize(_WorldSpaceCameraPos - o.worldPos);
                return o;
            }
            
            float4 frag(v2f i) : SV_Target
            {
                float4 weatherColor = getIntensityColor(_Intensity);
                
                float height = saturate(i.uv.y);
                float topFade = 1.0 - smoothstep(1.0 - _TopFade, 1.0, height);
                
                float alpha = _InnerGlow * _Opacity * weatherColor.a * topFade * 0.15;
                
                return float4(weatherColor.rgb * _Color.rgb, alpha);
            }
            ENDCG
        }
    }
    
    Fallback "Transparent/Diffuse"
}
