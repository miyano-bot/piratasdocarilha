// Crest Ocean System

// This file is subject to the Unity Companion License:
// https://github.com/Unity-Technologies/Graphics/blob/7ff8fd444c179fd9bb380d61f4865be6935b47dd/LICENSE.md

#ifndef CREST_URP_CORE_INCLUDED
#define CREST_URP_CORE_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

// Add missing "fixed" equivalent for URP (which uses real/half/float)
#define fixed half
#define fixed2 half2
#define fixed3 half3
#define fixed4 half4

#define fixed2x2 half2x2
#define fixed3x3 half3x3
#define fixed4x4 half4x4

#define ZERO_INITIALIZE(type, name) name = (type)0;

// BIRP compatibility macros for struct initialization
#define UNITY_INITIALIZE_OUTPUT(type, name) name = (type)0

// URP already defines TEXTURE2D_X, SAMPLE_TEXTURE2D_X, LOAD_TEXTURE2D_X in Core.hlsl
// Do not redefine them to avoid conflicts

// Fog compatibility - use fogCoord for BIRP compatibility
#define UNITY_FOG_COORDS(idx) float fogCoord : TEXCOORD##idx;
#define UNITY_TRANSFER_FOG(o, outpos) o.fogCoord = ComputeFogFactor(outpos.z);
#define UNITY_APPLY_FOG(fogCoord, col) col.rgb = MixFog(col.rgb, fogCoord);

// BIRP Glossy Environment compatibility for URP
struct Unity_GlossyEnvironmentData
{
    half roughness;
    half3 reflUVW;
};

// URP version of Unity_GlossyEnvironment
// Note: These functions are defined in Lighting.hlsl, so we provide fallbacks
#ifndef UNITY_SPECCUBE_LOD_STEPS
#define UNITY_SPECCUBE_LOD_STEPS 6
#endif

half CrestPerceptualRoughnessToMipmapLevel(half perceptualRoughness)
{
    return perceptualRoughness * UNITY_SPECCUBE_LOD_STEPS;
}

// DecodeHDREnvironment fallback - decodes HDR cubemap value
half3 CrestDecodeHDREnvironment(half4 encodedIrradiance, half4 decodeInstructions)
{
    // Take into account texture alpha if decodeInstructions.w is true(the alpha value affects the RGB channels)
    half alpha = max(decodeInstructions.w * (encodedIrradiance.a - 1.0) + 1.0, 0.0);
    // If Linear mode is not supported we can skip exponent part
    return (decodeInstructions.x * pow(abs(alpha), decodeInstructions.y)) * encodedIrradiance.rgb;
}

half3 Unity_GlossyEnvironment(TEXTURECUBE_PARAM(tex, samp), half4 hdr, Unity_GlossyEnvironmentData envData)
{
    half perceptualRoughness = envData.roughness;
    half mip = CrestPerceptualRoughnessToMipmapLevel(perceptualRoughness);
    half4 encodedIrradiance = SAMPLE_TEXTURECUBE_LOD(tex, samp, envData.reflUVW, mip);
    return CrestDecodeHDREnvironment(encodedIrradiance, hdr);
}

// BIRP compatibility macros for texture cube passing
#define UNITY_PASS_TEXCUBE(tex) tex, sampler##tex
#define UNITY_PASS_TEXCUBE_SAMPLER(tex, samp) tex, sampler##samp

// Compatibility with BIRP
#define UnityObjectToClipPos(pos) TransformObjectToHClip(pos.xyz)
#define UnityObjectToWorldDir(dir) TransformObjectToWorldDir(dir)
#define UnityWorldToObjectDir(dir) TransformWorldToObjectDir(dir)
#define UnityWorldToClipPos(pos) TransformWorldToHClip(pos)
#define UnityViewToClipPos(pos) TransformViewToHClip(pos)
#define UnityObjectToViewPos(pos) TransformObjectToView(pos.xyz)
#define UnityWorldToViewPos(pos) TransformWorldToView(pos)

#define unity_ObjectToWorld GetObjectToWorldMatrix()
#define unity_WorldToObject GetWorldToObjectMatrix()

// These are already defined in URP Core.hlsl, only define if missing
#ifndef UNITY_MATRIX_VP
#define UNITY_MATRIX_VP GetWorldToHClipMatrix()
#endif
#ifndef UNITY_MATRIX_V
#define UNITY_MATRIX_V GetWorldToViewMatrix()
#endif
#ifndef UNITY_MATRIX_P
#define UNITY_MATRIX_P GetViewToHClipMatrix()
#endif
#ifndef UNITY_MATRIX_I_V
#define UNITY_MATRIX_I_V GetViewToWorldMatrix()
#endif

#ifndef _WorldSpaceCameraPos
#define _WorldSpaceCameraPos GetCameraPositionWS()
#endif

// BIRP lighting compatibility - these are set by Crest C# code
half4 _LightColor0;
float4 _WorldSpaceLightPos0;

// Screen space compatibility
#define ComputeGrabScreenPos(pos) ComputeScreenPos(pos)

// URP standard samplers
SAMPLER(sampler_linear_clamp);
#define sampler_LinearClamp sampler_linear_clamp

#define UNITY_SAMPLE_SCREENSPACE_TEXTURE(tex, uv) SAMPLE_TEXTURE2D_X(tex, sampler_LinearClamp, uv)
#define UNITY_DECLARE_SCREENSPACE_TEXTURE(tex) TEXTURE2D_X(tex)

#endif // CREST_URP_CORE_INCLUDED
