Shader "FAA/XPlaneTerrain"
{
    Properties
    {
        _HazeColor ("Distant terrain haze", Color) = (0.34, 0.43, 0.46, 1)
        _HazeDistance ("Haze distance (meters)", Float) = 135000
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; float4 color : COLOR; };
            struct v2f { float4 position : SV_POSITION; float3 normal : TEXCOORD0; float3 world : TEXCOORD1; float4 color : COLOR; };
            float4 _HazeColor;
            float _HazeDistance;
            v2f vert(appdata v)
            {
                v2f o;
                o.position = UnityObjectToClipPos(v.vertex);
                o.world = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.color = v.color;
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                // Stable synthetic relief lighting; not a claim about live sun/weather.
                float3 normal = normalize(i.normal);
                float light = .48 + .52 * saturate(dot(normal, normalize(float3(-.45, .75, .3))));
                float slope = 1 - saturate(normal.y);
                float3 surface = lerp(i.color.rgb, float3(.43, .40, .34), saturate(slope * 2)) * light;
                float distance = length(i.world.xz - _WorldSpaceCameraPos.xz);
                float haze = smoothstep(_HazeDistance * .15, _HazeDistance, distance) * .85;
                return fixed4(lerp(surface, _HazeColor.rgb, haze), 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
