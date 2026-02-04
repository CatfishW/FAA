Shader "WeatherVisualization3D/SimpleCloudPreview"
{
    Properties
    {
        _DensityVolume("Density Volume", 3D) = "white" {}
        _VolumeMin("Volume Min", Vector) = (0,0,0,0)
        _VolumeMax("Volume Max", Vector) = (1,1,1,0)
        _AlphaScale("Alpha Scale", Range(0,10)) = 2
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+100" }
        LOD 100

        Pass
        {
            Cull Off
            ZWrite Off
            ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 worldPos : TEXCOORD0;
            };

            sampler3D _DensityVolume;
            float3 _VolumeMin;
            float3 _VolumeMax;
            float _AlphaScale;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 uvw = (i.worldPos - _VolumeMin) / (_VolumeMax - _VolumeMin);

                // Check bounds
                if (any(uvw < 0) || any(uvw > 1))
                    return fixed4(0,0,0,0);

                float density = tex3D(_DensityVolume, uvw).r;
                float alpha = saturate(density * _AlphaScale);

                // Skip if invisible
                if (alpha < 0.01)
                    return fixed4(0,0,0,0);

                // Color based on density - aviation weather colors
                fixed3 color;
                if (density < 0.3)
                    color = fixed3(0.2, 0.9, 0.2); // Light - Green
                else if (density < 0.5)
                    color = fixed3(1.0, 0.95, 0.1); // Moderate - Yellow
                else if (density < 0.7)
                    color = fixed3(1.0, 0.5, 0.1); // Heavy - Orange
                else if (density < 0.85)
                    color = fixed3(1.0, 0.15, 0.15); // Intense - Red
                else
                    color = fixed3(1.0, 0.1, 0.8); // Extreme - Magenta

                return fixed4(color, alpha);
            }
            ENDCG
        }
    }
}
