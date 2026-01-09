// Crest Ocean System

// This file is subject to the MIT License as seen in the root of this folder structure (LICENSE)

#ifndef CREST_URP_LIGHTING_INCLUDED
#define CREST_URP_LIGHTING_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

// Abstraction over Light shading data.
// Crest expects these fields in its BIRP Lighting.hlsl
/*
struct Light
{
    half3   direction;
    half3   color;
};
*/
// URP's Light struct already has these fields.

// Wrap URP's GetMainLight
// In URP, it's defined as Light GetMainLight() or Light GetMainLight(float4 shadowCoord)
// We provide a version that matches BIRP signature if needed, but BIRP one took no args.

#endif // CREST_URP_LIGHTING_INCLUDED
