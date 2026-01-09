// Crest Ocean System

// This file is subject to the Unity Companion License:
// https://github.com/Unity-Technologies/Graphics/blob/61584ec20cf305929dae85cec7b94ff2ed3942f3/LICENSE.md

// Add two functions from:
// https://github.com/Unity-Technologies/Graphics/blob/61584ec20cf305929dae85cec7b94ff2ed3942f3/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl

// Generates a triangle in homogeneous clip space, s.t.
// v0 = (-1, -1, 1), v1 = (3, -1, 1), v2 = (-1, 3, 1).
#ifndef CREST_FULLSCREEN_TRIANGLE_INCLUDED
#define CREST_FULLSCREEN_TRIANGLE_INCLUDED

// These functions might be defined in URP/HDRP Core library
#if !defined(UNIVERSAL_RENDER_PIPELINE) && !defined(UNITY_CORE_COMMON_HLSL) && !defined(CORE_COMMON_HLSL)
float2 GetFullScreenTriangleTexCoord(uint vertexID)
{
#if UNITY_UV_STARTS_AT_TOP
    return float2((vertexID << 1) & 2, 1.0 - (vertexID & 2));
#else
    return float2((vertexID << 1) & 2, vertexID & 2);
#endif
}

float4 GetFullScreenTriangleVertexPosition(uint vertexID, float z = UNITY_NEAR_CLIP_VALUE)
{
    float2 uv = float2((vertexID << 1) & 2, vertexID & 2);
    return float4(uv * 2.0 - 1.0, z, 1.0);
}
#endif // !UNIVERSAL_RENDER_PIPELINE && !UNITY_CORE_COMMON_HLSL && !CORE_COMMON_HLSL

#endif // CREST_FULLSCREEN_TRIANGLE_INCLUDED
