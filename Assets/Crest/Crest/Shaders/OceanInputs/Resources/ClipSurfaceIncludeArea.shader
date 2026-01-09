// Crest Ocean System

// This file is subject to the MIT License as seen in the root of this folder structure (LICENSE)

// Renders the geometry to the clip surface data and sets the value to 'include'.

Shader "Crest/Inputs/Clip Surface/Include Area"
{
	SubShader
	{
		Tags { "Queue" = "Geometry-10" }

		Pass
		{
			Tags { "LightMode" = "UniversalForward" }
			Blend Off
			ZWrite Off
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

			struct Attributes
			{
				float3 positionOS : POSITION;
			};

			struct Varyings
			{
				float4 positionCS : SV_POSITION;
			};

			Varyings Vert(Attributes input)
			{
				Varyings o;
				o.positionCS = UnityObjectToClipPos(input.positionOS);
				return o;
			}

			half4 Frag(Varyings input) : SV_Target
			{
				return 0.0;
			}
			ENDHLSL
		}
	}
}
