
            Shader "WeatherVisualization3D/CloudPreview"
            {
                Properties
                {
                    _DensityTex("Density", 3D) = "white" {}
                    _VolumeMin("Min", Vector) = (0,0,0,0)
                    _VolumeMax("Max", Vector) = (1,1,1,0)
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

                        sampler3D _DensityTex;
                        float3 _VolumeMin;
                        float3 _VolumeMax;

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

                            if (any(uvw < 0) || any(uvw > 1))
                                return fixed4(0,0,0,0);

                            float density = tex3D(_DensityTex, uvw).r;

                            // Aviation weather colors
                            fixed3 color;
                            if (density < 0.2)
                                color = fixed3(0.2, 0.9, 0.2);      // Light - Green
                            else if (density < 0.4)
                                color = fixed3(1.0, 0.95, 0.1);     // Moderate - Yellow
                            else if (density < 0.6)
                                color = fixed3(1.0, 0.5, 0.1);      // Heavy - Orange
                            else if (density < 0.8)
                                color = fixed3(1.0, 0.15, 0.15);    // Intense - Red
                            else
                                color = fixed3(1.0, 0.1, 0.8);      // Extreme - Magenta

                            return fixed4(color, density * 0.5);
                        }
                        ENDCG
                    }
                }
            }