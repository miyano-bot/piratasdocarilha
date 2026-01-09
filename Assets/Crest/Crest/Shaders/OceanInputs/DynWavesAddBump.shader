// Crest Ocean System

// This file is subject to the MIT License as seen in the root of this folder structure (LICENSE)

Shader "Crest/Inputs/Dynamic Waves/Add Bump"
{
	Properties
	{
		_Amplitude( "Amplitude", float ) = 1
		_Radius( "Radius", float) = 3
	}

	SubShader
	{
		Pass
		{
			Tags { "LightMode" = "UniversalForward" }
			Blend One One

			HLSLPROGRAM
			#pragma vertex Vert
			#pragma fragment Frag

			#define UNIVERSAL_RENDER_PIPELINE

			#if defined(UNIVERSAL_RENDER_PIPELINE)
				#include "../Helpers/URP/Core.hlsl"
			#else
				#include "UnityCG.cginc"
			#endif

			#include "../OceanGlobals.hlsl"
			#include "../OceanInputsDriven.hlsl"
			#include "../OceanHelpersNew.hlsl"

			CBUFFER_START(CrestPerOceanInput)
			float _Radius;
			float _SimCount;
			float _SimDeltaTime;
			float _Amplitude;
			float3 _DisplacementAtInputPosition;
			CBUFFER_END

			struct Attributes
			{
				float3 positionOS : POSITION;
				float2 texcoord : TEXCOORD0;
			};

			struct Varyings
			{
				float4 positionCS : SV_POSITION;
				float2 worldOffsetScaled : TEXCOORD0;
				float2 positionWS : TEXCOORD1;
			};

			Varyings Vert(Attributes input)
			{
				Varyings o;
				#if defined(UNIVERSAL_RENDER_PIPELINE)
					o.positionCS = TransformObjectToHClip(input.positionOS);
				#else
					o.positionCS = UnityObjectToClipPos(input.positionOS);
				#endif

				float3 worldPos = mul(UNITY_MATRIX_M, float4(input.positionOS, 1.0));
				float3 centerPos = UNITY_MATRIX_M._m03_m13_m23;
				o.worldOffsetScaled.xy = worldPos.xz - centerPos.xz;

				// shape is symmetric around center with known radius - fix the vert positions to perfectly wrap the shape.
				o.worldOffsetScaled.xy = sign(o.worldOffsetScaled.xy);
				float4 newWorldPos = float4(centerPos, 1.0);
				newWorldPos.xz += o.worldOffsetScaled.xy * _Radius;

				// Correct for displacement
				newWorldPos.xz -= _DisplacementAtInputPosition.xz;
				
				o.positionCS = mul(UNITY_MATRIX_VP, newWorldPos);
				o.positionWS = newWorldPos.xz;

				return o;
			}

			float4 Frag(Varyings input) : SV_Target
			{
				// power 4 smoothstep - no normalize needed
				// credit goes to stubbe's shadertoy: https://www.shadertoy.com/view/4ldSD2
				float r2 = dot(input.worldOffsetScaled.xy, input.worldOffsetScaled.xy);
				if (r2 > 1.0)
					return (float4)0.0;

				r2 = 1.0 - r2;

				float y = r2 * r2;
				y = pow(y, 0.05);
				y *= _Amplitude;

				if (_SimCount > 0.0) // user friendly - avoid nans
					y /= _SimCount;

				// Feather edges to reduce streaking without introducing reflections.
				y *= FeatherWeightFromUV(WorldToUV(input.positionWS, _CrestCascadeData[_LD_SliceIndex], _LD_SliceIndex), 0.1);

				// accelerate velocities
				return float4(0.0, _SimDeltaTime * y, 0.0, 0.0);
			}

			ENDHLSL
		}
	}
}
