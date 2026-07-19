Shader "FAA/UI/XPlaneWeatherRadarSweep"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Sweep Color", Color) = (0.12, 1, 0.52, 0.68)
        _OriginUV ("Origin UV", Vector) = (0.5, 0.07, 0, 0)
        _Aspect ("Texture Aspect", Float) = 1.4140625
        _SectorHalfAngle ("Sector Half Angle", Float) = 55
        _OuterRadius ("Outer Radius", Range(0.4, 1)) = 0.86
        _ScanAngle ("Scan Angle", Float) = 0
        _ScanDirection ("Scan Direction", Float) = 1
        _BeamWidth ("Beam Width", Float) = 0.85
        _GlowWidth ("Glow Width", Float) = 3
        _TrailWidth ("Trail Width", Float) = 12
        _TrailStrength ("Trail Strength", Range(0, 1)) = 0.18

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "WeatherRadarSweep"

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            fixed4 _Color;
            float4 _ClipRect;
            float4 _OriginUV;
            float _Aspect;
            float _SectorHalfAngle;
            float _OuterRadius;
            float _ScanAngle;
            float _ScanDirection;
            float _BeamWidth;
            float _GlowWidth;
            float _TrailWidth;
            float _TrailStrength;

            v2f vert(appdata_t input)
            {
                v2f output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.worldPosition = input.vertex;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.texcoord = input.texcoord;
                output.color = input.color * _Color;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float2 offset = input.texcoord - _OriginUV.xy;
                offset.x *= max(_Aspect, 0.001);

                float radius = length(offset);
                float angle = degrees(atan2(offset.x, offset.y));
                float angleDelta = angle - _ScanAngle;
                float absoluteDelta = abs(angleDelta);

                float sectorMask = 1.0 - smoothstep(
                    max(0.0, _SectorHalfAngle - 1.5),
                    _SectorHalfAngle + 0.5,
                    abs(angle));
                float originMask = smoothstep(0.018, 0.055, radius);
                float rangeMask = 1.0 - smoothstep(
                    max(0.0, _OuterRadius - 0.008),
                    _OuterRadius + 0.003,
                    radius);

                float beam = 1.0 - smoothstep(0.0, max(0.01, _BeamWidth), absoluteDelta);
                float glow = 1.0 - smoothstep(
                    max(0.01, _BeamWidth),
                    max(_BeamWidth + 0.01, _GlowWidth),
                    absoluteDelta);

                float behind = -angleDelta * _ScanDirection;
                float trailGate = step(0.0, behind) * step(behind, _TrailWidth);
                float trail = trailGate * (1.0 - smoothstep(0.0, max(0.01, _TrailWidth), behind));

                float intensity = saturate(beam * 0.92 + glow * 0.22 + trail * _TrailStrength);
                fixed4 color = input.color;
                color.rgb *= 0.72 + beam * 0.38;
                color.a *= intensity * sectorMask * originMask * rangeMask;

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(input.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif

                return color;
            }
            ENDCG
        }
    }
}
