// Shader for circular masking of radar chart background with transparency control
Shader "TrafficRadar/CircularRadarMask"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Opacity ("Background Opacity", Range(0, 1)) = 0.5
        _SoftEdge ("Edge Softness", Range(0, 0.1)) = 0.02
        // Optional fixed-center mask used by the sectional chart. The chart
        // RawImage is larger than the radar while panning; keeping the mask
        // centre/radius independent of that image prevents the circle from
        // travelling with the map and revealing a transparent crescent.
        _MaskCenter ("Mask Center (UV)", Vector) = (0.5, 0.5, 0, 0)
        _MaskRadius ("Mask Radius (UV)", Vector) = (0.5, 0.5, 0, 0)
        _UseFixedMask ("Use Fixed Mask", Float) = 0

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
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

        ColorMask [_ColorMask]
        
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        
        Pass
        {
            Name "Default"
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            
            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            
            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;
            float _Opacity;
            float _SoftEdge;
            float4 _MaskCenter;
            float4 _MaskRadius;
            float _UseFixedMask;
            
            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                
                OUT.color = v.color * _Color;
                return OUT;
            }
            
            fixed4 frag(v2f IN) : SV_Target
            {
                // The default mask is centred on this RawImage. The chart
                // display opts into a fixed centre/radius supplied by
                // TrafficRadarDisplay so map panning does not move the mask.
                float2 maskCenter = (_UseFixedMask > 0.5) ? _MaskCenter.xy : float2(0.5, 0.5);
                float2 maskRadius = (_UseFixedMask > 0.5)
                    ? max(_MaskRadius.xy, float2(0.0001, 0.0001))
                    : float2(0.5, 0.5);
                float2 normalizedOffset = (IN.texcoord - maskCenter) / maskRadius;
                float dist = length(normalizedOffset);
                
                // Create circular mask with soft edge
                float circleMask = 1.0 - smoothstep(1.0 - _SoftEdge, 1.0, dist);
                
                // Sample texture
                half4 color = (tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd) * IN.color;
                
                // Apply opacity and circular mask
                color.a *= _Opacity * circleMask;
                
                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif
                
                clip(color.a - 0.001);
                
                return color;
            }
            ENDCG
        }
    }
}
