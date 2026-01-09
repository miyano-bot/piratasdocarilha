// Crest Ocean System

// This file is subject to the MIT License as seen in the root of this folder structure (LICENSE)

// Renders a specific slice of a 2D Texture Array
// https://docs.unity3d.com/Manual/SL-TextureArrays.html
Shader "Hidden/Crest/Debug/TextureArray"
{
	SubShader
	{
		Tags { "RenderType"="Opaque" }
		Cull Off
		ZWrite Off

		Pass
		{
			HLSLPROGRAM
			#pragma vertex Vert
			#pragma fragment Frag
			#pragma require 2darray

			#define UNIVERSAL_RENDER_PIPELINE

			#if defined(UNIVERSAL_RENDER_PIPELINE)
				#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#else
				#include "UnityCG.cginc"
			#endif

			struct Attributes
			{
				float4 positionOS : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct Varyings
			{
				float4 positionCS : SV_POSITION;
				float3 uv : TEXCOORD0;
			};

			#if defined(UNIVERSAL_RENDER_PIPELINE)
				TEXTURE2D_ARRAY(_MainTex);
				SAMPLER(sampler_MainTex);
			#else
				UNITY_DECLARE_TEX2DARRAY(_MainTex);
			#endif

			uint _Depth;
			float _Scale;
			float _Bias;

			Varyings Vert(Attributes input)
			{
				Varyings o;
				#if defined(UNIVERSAL_RENDER_PIPELINE)
					o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
				#else
					o.positionCS = UnityObjectToClipPos(input.positionOS);
				#endif
				o.uv = float3(input.uv.xy, _Depth);
				return o;
			}

			half4 Frag(Varyings input) : SV_TARGET
			{
				#if defined(UNIVERSAL_RENDER_PIPELINE)
					return _Scale * SAMPLE_TEXTURE2D_ARRAY(_MainTex, sampler_MainTex, input.uv.xy, input.uv.z) + _Bias;
				#else
					return _Scale * UNITY_SAMPLE_TEX2DARRAY(_MainTex, input.uv) + _Bias;
				#endif
			}
			ENDHLSL
		}
	}
}
