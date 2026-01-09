// Crest Ocean System

// This file is subject to the MIT License as seen in the root of this folder structure (LICENSE)

// Renders the geometry to the shadow texture and sets shadow data to provided value.

Shader "Crest/Inputs/Shadows/Override Shadows"
{
	Properties
	{
		_ShadowValue("Shadow Value", Range(0.0, 1.0)) = 1.0
	}

	SubShader
	{
		Tags { "Queue" = "Geometry" }

		Pass
		{
			Tags { "LightMode" = "UniversalForward" }
			Blend Off
			ZWrite Off
			ColorMask RG

			HLSLPROGRAM
			#pragma vertex Vert
			#pragma fragment Frag

			#define UNIVERSAL_RENDER_PIPELINE

			#if defined(UNIVERSAL_RENDER_PIPELINE)
				#include "../../Helpers/URP/Core.hlsl"
			#else
				#include "UnityCG.cginc"
			#endif

			half _ShadowValue;

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
				return _ShadowValue;
			}
			ENDHLSL
		}
	}
}
