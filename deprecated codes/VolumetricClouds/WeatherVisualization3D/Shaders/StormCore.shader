// Storm Core Shader
// Part of WeatherVisualization3D System
// Renders glowing storm cores for severe weather cells

Shader "WeatherVisualization3D/StormCore"
{
    Properties
    {
        _CoreColor ("Core Color", Color) = (1, 0.2, 0.2, 1)
        _GlowColor ("Glow Color", Color) = (1, 0.5, 0.3, 1)
        _Intensity ("Intensity", Range(0, 1)) = 0.8
        _GlowRadius ("Glow Radius", Range(0.5, 3)) = 1.5
        _PulseSpeed ("Pulse Speed", Range(0, 5)) = 2
        _PulseAmount ("Pulse Amount", Range(0, 1)) = 0.3
        _NoiseScale ("Noise Scale", Range(1, 20)) = 5
        _NoiseSpeed ("Noise Speed", Range(0, 2)) = 0.5
        _DistortionStrength ("Distortion", Range(0, 0.5)) = 0.1
    }
    
    SubShader
    {
        Tags 
        { 
            "RenderType" = "Transparent" 
            "Queue" = "Transparent+200"
            "IgnoreProjector" = "True"
        }
        
        Pass
        {
            Name "StormCore"
            
            Cull Off
            ZWrite Off
            ZTest LEqual
            Blend One One  // Additive blending for glow
            
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
                float3 objPos : TEXCOORD3;
            };
            
            float4 _CoreColor;
            float4 _GlowColor;
            float _Intensity;
            float _GlowRadius;
            float _PulseSpeed;
            float _PulseAmount;
            float _NoiseScale;
            float _NoiseSpeed;
            float _DistortionStrength;
            
            // Simple 3D noise
            float hash(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }
            
            float noise3D(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                
                return lerp(
                    lerp(
                        lerp(hash(i + float3(0,0,0)), hash(i + float3(1,0,0)), f.x),
                        lerp(hash(i + float3(0,1,0)), hash(i + float3(1,1,0)), f.x),
                        f.y
                    ),
                    lerp(
                        lerp(hash(i + float3(0,0,1)), hash(i + float3(1,0,1)), f.x),
                        lerp(hash(i + float3(0,1,1)), hash(i + float3(1,1,1)), f.x),
                        f.y
                    ),
                    f.z
                );
            }
            
            v2f vert(appdata v)
            {
                v2f o;
                
                // Add vertex distortion for organic feel
                float3 pos = v.vertex.xyz;
                float noise = noise3D(pos * _NoiseScale + _Time.y * _NoiseSpeed);
                pos += v.normal * noise * _DistortionStrength;
                
                o.pos = UnityObjectToClipPos(float4(pos, 1));
                o.uv = v.uv;
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.viewDir = normalize(_WorldSpaceCameraPos - o.worldPos);
                o.objPos = v.vertex.xyz;
                return o;
            }
            
            float4 frag(v2f i) : SV_Target
            {
                // Distance from center
                float dist = length(i.objPos);
                
                // Animated noise
                float time = _Time.y * _NoiseSpeed;
                float3 noisePos = i.objPos * _NoiseScale + time;
                float noiseFactor = noise3D(noisePos) * 0.5 + 0.5;
                
                // Pulsing
                float pulse = 1.0 + sin(_Time.y * _PulseSpeed) * _PulseAmount;
                pulse += noiseFactor * 0.2;
                
                // Core glow (brightest at center)
                float coreFalloff = 1.0 - smoothstep(0.0, 0.5, dist);
                float outerGlow = 1.0 - smoothstep(0.3, _GlowRadius, dist);
                
                // Combine core and outer glow
                float3 coreContrib = _CoreColor.rgb * coreFalloff * 2.0;
                float3 glowContrib = _GlowColor.rgb * outerGlow;
                
                float3 finalColor = (coreContrib + glowContrib) * _Intensity * pulse;
                
                // Add flickering for lightning effect
                float flicker = noise3D(i.worldPos * 0.1 + _Time.y * 10.0);
                flicker = smoothstep(0.7, 0.9, flicker);
                finalColor += _CoreColor.rgb * flicker * 3.0 * _Intensity;
                
                // Alpha based on glow
                float alpha = (coreFalloff + outerGlow * 0.5) * _Intensity;
                
                return float4(finalColor, alpha);
            }
            ENDCG
        }
    }
    
    Fallback Off
}
