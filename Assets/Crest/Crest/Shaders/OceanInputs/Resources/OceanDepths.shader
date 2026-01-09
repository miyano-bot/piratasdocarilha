// Crest Ocean System

// This file is subject to the MIT License as seen in the root of this folder structure (LICENSE)

// Renders ocean depth - signed distance from sea level to sea floor
Shader "Crest/Inputs/Depth/Ocean Depth From Geometry"
{
	SubShader
	{
		Pass
		{
			Tags { "LightMode" = "UniversalForward" }
			BlendOp Max
			ColorMask R
			
			HLSLPROGRAM
			#pragma vertex Vert
			#pragma fragment Frag

			#define UNIVERSAL_RENDER_PIPELINE

			#if defined(UNIVERSAL_RENDER_PIPELINE)
				#include "../../Helpers/URP/Core.hlsl"
			#else
				#include "UnityCG.cginc"
			#endif
			#include "../../OceanConstants.hlsl"

			struct Attributes
			{
				float3 positionOS : POSITION;
			};

			struct Varyings
			{
				float4 positionCS : SV_POSITION;
				float terrainHeight : TEXCOORD0;
			};

			Varyings Vert(Attributes input)
			{
				Varyings o;
				o.positionCS = UnityObjectToClipPos(input.positionOS);
				o.terrainHeight = mul(unity_ObjectToWorld, float4(input.positionOS, 1.0)).y;
				return o;
			}

			float4 Frag(Varyings input) : SV_Target
			{
				return float4(input.terrainHeight, 0.0, 0.0, 0.0);
			}
			ENDHLSL
		}
	}
}
