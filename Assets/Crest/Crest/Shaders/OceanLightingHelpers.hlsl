// Crest Ocean System

// This file is subject to the MIT License as seen in the root of this folder structure (LICENSE)

#ifndef CREST_OCEAN_LIGHTING_HELPERS_H
#define CREST_OCEAN_LIGHTING_HELPERS_H

namespace WaveHarmonic
{
	namespace Crest
	{
// Check for either BIRP Lighting.cginc or URP Lighting.hlsl
#if defined(LIGHTING_INCLUDED) || defined(UNIVERSAL_LIGHTING_INCLUDED) || defined(UNIVERSAL_RENDER_PIPELINE)
		float3 WorldSpaceLightDir(float3 worldPos)
		{
#if defined(UNIVERSAL_RENDER_PIPELINE)
			Light mainLight = GetMainLight();
			return mainLight.direction;
#else
			float3 lightDir = _WorldSpaceLightPos0.xyz;
			if (_WorldSpaceLightPos0.w > 0.)
			{
				// non-directional light - this is a position, not a direction
				lightDir = normalize(lightDir - worldPos.xyz);
			}
			return lightDir;
#endif
		}
#endif

		half3 AmbientLight()
		{
			return half3(unity_SHAr.w, unity_SHAg.w, unity_SHAb.w);
		}
	}
}


#endif // CREST_OCEAN_LIGHTING_HELPERS_H
