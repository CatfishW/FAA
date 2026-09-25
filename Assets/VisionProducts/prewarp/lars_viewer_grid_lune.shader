/* Copyright:      SA Photonics 2018 - All rights Reserved.

   Comments:       The script is intended to take raw warp data in the text format
                   and load the data as a texture in Unity shader for X and Y positions.
				   Added luning mitigation.
*/
Shader "lars_viewer_grid_lune"
{
	Properties
	{
		_MainTex("Base (RGB)", 2D) = "red" {}
		_BackgroundTex("Background Texture", 2D) = "red" {} // Similar to _MainTex
		greenTex_X("greenTexture_X", 2D) = "white" {}  
		greenTex_Y("greenTexture_Y", 2D) = "white" {}
		_leftSideLune("leftSideLune", Range(0.0, 1.0)) = 0
		_rightSideLune("rightSideLune", Range(0.0, 1.0)) = 0

	}
	SubShader
	 {
		GrabPass
		{
		}
		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
            #pragma target 4.5
			#include "UnityCG.cginc"

			/* struct appdata_custom is to access camera world position into vertex shader  */
			struct appdata_custom
			{
				float4 vertex : POSITION;
			};

			/* struct v2f will take the values from vertex shader function to fragment shader function, here frag() */
			struct v2f
			{
				float4 vertex : SV_POSITION;
				float4 screenpos : TEXCOORD0;
			};

			/* In order to access the following variables in the shader code, these need to be declare here and intiallize in the properties  */
			uniform sampler2D _MainTex;             // Texture containing pixel vales 
			uniform sampler2D _BackgroundTex;  // Similar to _MainTex
			uniform float _leftSideLune;
			uniform float _rightSideLune;
			half4 _MainTex_TexelSize;              // Belong to render texture to access texture coordinates 
			uniform sampler2D greenTex_X, greenTex_Y;
			uniform int alpha_pixel_offset;
			uniform float Scurve_Midpoint, Scurve_Steepness;

			v2f vert(appdata_custom app_obj)
			{
				v2f obj;
				UNITY_INITIALIZE_OUTPUT(v2f, obj);
				obj.vertex = UnityObjectToClipPos(app_obj.vertex);     //coordinates object.vertex.xy are in [-w,w] format 
				obj.screenpos = ComputeScreenPos(obj.vertex);         //remap obj.vertex.xy from [-w, w] to [0,w] and stored in obj.screenpos.xy
                #if UNITY_UV_STARTS_AT_TOP
				obj.screenpos.y = 1-obj.screenpos.y;
				if (_MainTex_TexelSize.y < 0)                       // Flip the Vertical Coordinates 
				{
					obj.screenpos.y = 1-obj.screenpos.y;
				}
                #endif 
				
				return obj;
			}
			
			float decodeWarpPointFixed(float4 rgba)
			{
				int rgba_r = int(rgba.r * 255.0); /* Multiplied by 255.0 to Unnormalized the tex2D data */
				int rgba_g = int(rgba.g * 255.0);
				int rgba_b = int(rgba.b * 255.0);
				int rgba_a = int(rgba.a * 255.0);

				/* Combine the rgba to 32 bit Value */
				int rgba32 = (rgba_a << 24) | (rgba_b << 16) | (rgba_g << 8) | rgba_r;
				float ans = float(rgba32 / (pow(2, 16)));
				return ans;
			}

			float4 frag(v2f i) : COLOR
			{
				float4 gl_Fragcolor;
			    float2 screenuv = i.screenpos.xy / i.screenpos.w ;     
				/* Grid Implementation */
				float2 screenpixel = screenuv * _ScreenParams.xy; // Pixel Coordinates 
				/* Pixel maping to warp texture*/
				float integer_divide_GX = floor(screenpixel.x / 16.0);
				float integer_divide_GY = floor(screenpixel.y / 16.0);
				float mod_value_X =  (fmod(screenpixel.x, 16.0)) / 16.0;
				float mod_value_Y = (fmod(screenpixel.y, 16.0)) / 16.0;
				/* UV conversion from pixel maping*/
				float2 A_uv = float2(integer_divide_GX  / 120.0, integer_divide_GY / 75.0);
				float2 B_uv = float2((integer_divide_GX + 1) / 120.0, integer_divide_GY / 75.0);
				float2 C_uv = float2(integer_divide_GX / 120.0, (integer_divide_GY + 1) / 75.0);
				float2 D_uv = float2((integer_divide_GX + 1) / 120.0, (integer_divide_GY + 1) / 75.0);
				/* Grab rgba value for A, B, C, D*/
				float4 X_A_rgba = tex2D(greenTex_X, A_uv);
				float4 X_B_rgba = tex2D(greenTex_X, B_uv);
				float4 X_C_rgba = tex2D(greenTex_X, C_uv);
				float4 X_D_rgba = tex2D(greenTex_X, D_uv);

				float4 Y_A_rgba = tex2D(greenTex_Y, A_uv);
				float4 Y_B_rgba = tex2D(greenTex_Y, B_uv);
				float4 Y_C_rgba = tex2D(greenTex_Y, C_uv);
				float4 Y_D_rgba = tex2D(greenTex_Y, D_uv);
				/* Warp coordinate for A, B, C, D*/
				float A_x = decodeWarpPointFixed(X_A_rgba);
				float B_x = decodeWarpPointFixed(X_B_rgba);
				float C_x = decodeWarpPointFixed(X_C_rgba);
				float D_x = decodeWarpPointFixed(X_D_rgba);

				float A_y = decodeWarpPointFixed(Y_A_rgba);
				float B_y = decodeWarpPointFixed(Y_B_rgba);
				float C_y = decodeWarpPointFixed(Y_C_rgba);
				float D_y = decodeWarpPointFixed(Y_D_rgba);


				/* Bilinear Calculation */
				float warp_point_X = ((1 - mod_value_X) * (1 - mod_value_Y) * A_x) + (mod_value_X * (1 - mod_value_Y) * B_x) + ((1 - mod_value_X) *  mod_value_Y * C_x) + (mod_value_X *  mod_value_Y * D_x);
				float warp_point_Y = ((1 - mod_value_X) * (1 - mod_value_Y) * A_y) + (mod_value_X * (1 - mod_value_Y) * B_y) + ((1 - mod_value_X) *  mod_value_Y * C_y) + (mod_value_X *  mod_value_Y * D_y);

				float2 g_warp = float2(warp_point_X / _ScreenParams.x, warp_point_Y / _ScreenParams.y);
				gl_Fragcolor = tex2D(_MainTex, g_warp);

				if (warp_point_X < 0 || warp_point_Y < 0 || warp_point_X >_ScreenParams.x || warp_point_Y > _ScreenParams.y)
				{
					gl_Fragcolor.rgba = float4(0.0, 0.0, 0.0, 0.0);
				}

				//Left side Lune
				if ((_leftSideLune == 1.0) && (screenpixel.x < 960)) {
					float a = 50.0;
					float k = 0.25;
					float s = 25.0;
					float blend = 1.0;
					float4 color = float4(1.0, 1.0, 1.0, 1.0);
					float2 pixelPos = g_warp;
					float x = pixelPos * 1920.0;
					color.rgb = tex2D(_MainTex, pixelPos).rgb;
					blend = 1.0 / (1.0 + a * exp(-k * (x - s)));
					color.rgb = color.rgb * float3(blend, blend, blend);
					gl_Fragcolor = color;
				}
				
				//Right side lune
				if ((_rightSideLune == 1.0) && screenpixel.x > 960) {
					float a = 50.0;
					float k = 0.25;
					float s = 1870.0;
					float blend = 1.0;
					float4 color = float4(1.0, 1.0, 1.0, 1.0);
					float2 pixelPos = g_warp;
					float x = pixelPos * 1920.0;
					color.rgb = tex2D(_MainTex, pixelPos).rgb;
					blend = 1.0 - (1.0 / (1.0 + a * exp(-k * (x - s))));
					color.rgb = color.rgb * float3(blend, blend, blend);
					gl_Fragcolor = color;
					//end luning
				}
				

				if (warp_point_X < 0 || warp_point_Y < 0 || warp_point_X >_ScreenParams.x || warp_point_Y > _ScreenParams.y)
				{
					gl_Fragcolor.rgba = float4(0.0, 0.0, 0.0, 0.0);
				}
				return gl_Fragcolor;
			}

			ENDCG
		 }
	 }
}