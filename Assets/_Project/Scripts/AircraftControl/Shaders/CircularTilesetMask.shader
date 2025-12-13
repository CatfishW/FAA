// Shader for circular masking of OnlineMaps TileSet with world-space distance calculation
// This shader creates a circular mask effect for 3D tileset maps
Shader "OnlineMaps/CircularTilesetMask"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _CenterX ("Center X", Float) = 0
        _CenterZ ("Center Z", Float) = 0
        _Radius ("Radius", Float) = 512
        _SoftEdge ("Edge Softness", Range(0, 100)) = 10
    }
    
    SubShader
    {
        Tags
        {
            "Queue" = "Geometry"
            "RenderType" = "Opaque"
        }
        
        LOD 100
        
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            
            #include "UnityCG.cginc"
            
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };
            
            struct v2f
            {
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
                float3 worldPos : TEXCOORD2;
            };
            
            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            float _CenterX;
            float _CenterZ;
            float _Radius;
            float _SoftEdge;
            
            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                UNITY_TRANSFER_FOG(o, o.vertex);
                return o;
            }
            
            fixed4 frag(v2f i) : SV_Target
            {
                // Calculate distance from center in world space (XZ plane)
                float2 offset = i.worldPos.xz - float2(_CenterX, _CenterZ);
                float dist = length(offset);
                
                // Create circular mask with soft edge
                float innerRadius = _Radius - _SoftEdge;
                float alpha = 1.0 - saturate((dist - innerRadius) / max(_SoftEdge, 0.001));
                
                // Discard pixels outside the circle
                clip(alpha - 0.001);
                
                // Sample texture
                fixed4 col = tex2D(_MainTex, i.uv) * _Color;
                col.a *= alpha;
                
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
    
    // Fallback for transparent rendering
    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }
        
        LOD 100
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            
            #include "UnityCG.cginc"
            
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };
            
            struct v2f
            {
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
                float3 worldPos : TEXCOORD2;
            };
            
            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            float _CenterX;
            float _CenterZ;
            float _Radius;
            float _SoftEdge;
            
            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                UNITY_TRANSFER_FOG(o, o.vertex);
                return o;
            }
            
            fixed4 frag(v2f i) : SV_Target
            {
                // Calculate distance from center in world space (XZ plane)
                float2 offset = i.worldPos.xz - float2(_CenterX, _CenterZ);
                float dist = length(offset);
                
                // Create circular mask with soft edge
                float innerRadius = _Radius - _SoftEdge;
                float alpha = 1.0 - saturate((dist - innerRadius) / max(_SoftEdge, 0.001));
                
                // Sample texture
                fixed4 col = tex2D(_MainTex, i.uv) * _Color;
                col.a *= alpha;
                
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
    
    Fallback "Diffuse"
}
