Shader "Custom/FFTOceanURP"
{
    Properties
    {
        [Header(Colors)]
        _ShallowColor ("Shallow Color", Color) = (0.1, 0.4, 0.6, 0.8)
        _DeepColor ("Deep Color", Color) = (0.02, 0.05, 0.15, 1.0)
        _ShoreColor ("Shore Color", Color) = (0.3, 0.9, 0.8, 1.0)
        _HorizonColor ("Horizon Color", Color) = (0.3, 0.5, 0.7, 1.0)
        _DepthFade ("Depth Fade", Range(0.1, 50)) = 5
        _ShoreFade ("Shore Fade", Range(0.1, 5)) = 0.5
        
        [Header(FFT Textures)]
        _DisplacementMap ("Displacement Map", 2D) = "black" {}
        _NormalFoamMap ("Normal + Foam Map", 2D) = "bump" {}
        _DisplacementStrength ("Displacement Strength", Range(0, 10)) = 1.0
        _NormalStrength ("Normal Strength", Range(0, 3)) = 1.0
        
        [Header(Surface)]
        _SurfaceNoise ("Surface Noise (Normals)", 2D) = "bump" {}
        _SurfaceNoiseScale ("Surface Noise Scale", Float) = 1.0
        _SurfaceNoiseSpeed ("Surface Noise Speed", Float) = 0.5
        _Smoothness ("Smoothness", Range(0, 1)) = 0.95
        _SpecularHardness ("Specular Hardness", Range(0, 1)) = 0.5
        _FresnelPower ("Fresnel Power", Range(0.5, 10)) = 5
        _ReflectionIntensity ("Reflection Intensity", Range(0, 1)) = 0.6
        
        [Header(Refraction)]
        _RefractionStrength ("Refraction Strength", Range(0, 0.5)) = 0.1
        
        [Header(Subsurface Scattering)]
        [Header(Visual Depth)]
        _CrestHighlight ("Crest Highlight", Range(0, 1)) = 0.5
        _TroughDarkness ("Trough Darkness", Range(0, 1)) = 0.5
        _SSSColor ("Subsurface Color", Color) = (0.1, 0.4, 0.3, 1.0)
        _SSSStrength ("SSS Strength", Range(0, 1)) = 0.3
        _SSSPower ("SSS Power", Range(1, 8)) = 4
        
        [Header(Foam)]
        _FoamColor ("Foam Color", Color) = (1, 1, 1, 1)
        _FoamIntensity ("Foam Intensity", Range(0, 5)) = 2.5
        _ShorelineFoamRange ("Shoreline Foam Range", Range(0, 3)) = 1.0
        
        [Header(Caustics)]
        _CausticsStrength ("Caustics Strength", Range(0, 2)) = 0.5
        _CausticsScale ("Caustics Scale", Range(1, 20)) = 8
        _CausticsSpeed ("Caustics Speed", Range(0, 2)) = 0.5
        
        [Header(Tiling)]
        _OceanSize ("Ocean Size (for UV)", Float) = 100
    }
    
    SubShader
    {
        Tags 
        { 
            "RenderType" = "Transparent" 
            "Queue" = "Transparent" 
            "RenderPipeline" = "UniversalPipeline"
        }
        
        Pass
        {
            Name "FFTOceanForward"
            Tags { "LightMode" = "UniversalForward" }
            
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };
            
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float4 screenPos : TEXCOORD3;
                float fogFactor : TEXCOORD4;
                float foam : TEXCOORD5;
                float waveHeight : TEXCOORD6;
            };
            
            // Properties
            CBUFFER_START(UnityPerMaterial)
                float4 _ShallowColor;
                float4 _DeepColor;
                float4 _ShoreColor;
                float4 _HorizonColor;
                float _DepthFade;
                float _ShoreFade;
                
                float _DisplacementStrength;
                float _NormalStrength;
                
                float _SurfaceNoiseScale;
                float _SurfaceNoiseSpeed;
                float _Smoothness;
                float _SpecularHardness;
                float _FresnelPower;
                float _ReflectionIntensity;
                
                float _RefractionStrength;
                
                float _CrestHighlight;
                float _TroughDarkness;
                float4 _SSSColor;
                float _SSSStrength;
                float _SSSPower;
                
                float4 _FoamColor;
                float _FoamIntensity;
                float _ShorelineFoamRange;
                
                float _CausticsStrength;
                float _CausticsScale;
                float _CausticsSpeed;
                
                float _OceanSize;
            CBUFFER_END
            
            // FFT Textures
            TEXTURE2D(_DisplacementMap);
            SAMPLER(sampler_DisplacementMap);
            TEXTURE2D(_NormalFoamMap);
            SAMPLER(sampler_NormalFoamMap);
            TEXTURE2D(_SurfaceNoise);
            SAMPLER(sampler_SurfaceNoise);
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            
            // Caustics noise
            float Voronoi(float2 uv, float time)
            {
                float2 g = floor(uv);
                float2 f = frac(uv);
                
                float minDist = 1.0;
                for (int y = -1; y <= 1; y++)
                {
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 neighbor = float2(x, y);
                        float2 cellPoint = frac(sin(dot(g + neighbor, float2(127.1, 311.7))) * 43758.5453);
                        cellPoint = 0.5 + 0.5 * sin(time + 6.2831 * cellPoint);
                        
                        float2 diff = neighbor + cellPoint - f;
                        float dist = length(diff);
                        minDist = min(minDist, dist);
                    }
                }
                return minDist;
            }
            
            Varyings vert(Attributes input)
            {
                Varyings output;
                
                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);
                
                // World-space UV for FFT sampling (tiled across ocean)
                float2 fftUV = posWS.xz / _OceanSize;
                
                // Sample displacement from FFT
                float4 displacement = SAMPLE_TEXTURE2D_LOD(_DisplacementMap, sampler_DisplacementMap, fftUV, 0);
                posWS.y += displacement.y * _DisplacementStrength;
                posWS.xz += displacement.xz * _DisplacementStrength; // Horizontal choppiness
                
                // Sample normal and foam from FFT. Note: R=X, G=Z, B=Up(Y)
                float4 normalFoam = SAMPLE_TEXTURE2D_LOD(_NormalFoamMap, sampler_NormalFoamMap, fftUV, 0);
                float3 unpackedNormal = normalFoam.xyz * 2.0 - 1.0;
                float3 fftNormal = float3(unpackedNormal.x, unpackedNormal.z, unpackedNormal.y);
                fftNormal = lerp(float3(0, 1, 0), fftNormal, _NormalStrength);
                
                output.positionWS = posWS;
                output.positionCS = TransformWorldToHClip(posWS);
                output.normalWS = normalize(fftNormal);
                output.uv = fftUV;
                output.screenPos = ComputeScreenPos(output.positionCS);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                output.foam = normalFoam.w; // Jacobian-based foam from FFT
                output.waveHeight = displacement.y; // Relative height for shading
                
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                float2 screenUV = input.screenPos.xy / input.screenPos.w;
                
                // Depth
                float rawDepth = SampleSceneDepth(screenUV);
                float sceneDepth = LinearEyeDepth(rawDepth, _ZBufferParams);
                float surfaceDepth = input.screenPos.w;
                float depthDiff = max(0, sceneDepth - surfaceDepth);
                
                float depthFactor = saturate(depthDiff / _DepthFade);
                float shoreFactor = saturate(1.0 - (depthDiff / _ShoreFade));
                
                // Base colors
                float4 waterColor = lerp(_ShallowColor, _DeepColor, depthFactor);
                // Boost shore color visibility
                waterColor = lerp(waterColor, _ShoreColor, shoreFactor * 0.8);
                
                // Apply height-based depth (trough darkening / crest highlight)
                float relHeight = input.waveHeight;
                waterColor.rgb *= lerp(1.0, 1.0 - _TroughDarkness * 0.5, saturate(-relHeight * 0.5));
                waterColor.rgb += _CrestHighlight * 0.2 * saturate(relHeight * 0.5) * _DeepColor.rgb;
                
                // View and light
                float3 viewDir = normalize(_WorldSpaceCameraPos - input.positionWS);
                Light mainLight = GetMainLight();
                float3 lightDir = mainLight.direction;
                
                // === Micro-Normals ===
                float2 noiseUV1 = input.uv * _SurfaceNoiseScale + _Time.y * _SurfaceNoiseSpeed * 0.1;
                float2 noiseUV2 = input.uv * _SurfaceNoiseScale * 1.5 - _Time.y * _SurfaceNoiseSpeed * 0.15;
                float3 noiseNormal1 = UnpackNormal(SAMPLE_TEXTURE2D(_SurfaceNoise, sampler_SurfaceNoise, noiseUV1));
                float3 noiseNormal2 = UnpackNormal(SAMPLE_TEXTURE2D(_SurfaceNoise, sampler_SurfaceNoise, noiseUV2));
                float3 detailNormal = normalize(noiseNormal1 + noiseNormal2);
                
                float3 finalNormal = normalize(input.normalWS + detailNormal * 0.2);
                
                // Fresnel
                float fresnel = pow(1.0 - saturate(dot(finalNormal, viewDir)), _FresnelPower);
                
                // Horizon blend
                float horizonFactor = pow(1.0 - saturate(dot(float3(0, 1, 0), viewDir)), 4);
                waterColor = lerp(waterColor, _HorizonColor, horizonFactor * 0.5);
                
                // === Refraction ===
                float2 refractionOffset = finalNormal.xz * _RefractionStrength;
                float2 refractionUV = screenUV + refractionOffset;
                
                // Only refract what's underwater
                float refractedSceneDepth = SampleSceneDepth(refractionUV);
                if (refractedSceneDepth < surfaceDepth)
                    refractionUV = screenUV;
                
                float3 refractionColor = SampleSceneColor(refractionUV);
                
                // === Subsurface Scattering ===
                float sss = pow(saturate(dot(viewDir, -lightDir)), _SSSPower) * _SSSStrength;
                float3 sssContrib = _SSSColor.rgb * sss * (1.0 - depthFactor);
                
                // === Caustics ===
                float causticsPattern = 0;
                if (depthDiff > 0 && depthDiff < _DepthFade)
                {
                    float2 causticsUV = input.uv * _CausticsScale;
                    float caustics1 = Voronoi(causticsUV, _Time.y * _CausticsSpeed);
                    float caustics2 = Voronoi(causticsUV * 1.3 + 0.5, _Time.y * _CausticsSpeed * 0.8);
                    causticsPattern = smoothstep(0.1, 0.3, min(caustics1, caustics2)) * _CausticsStrength;
                    causticsPattern *= (1.0 - depthFactor);
                }
                
                // === Foam ===
                float2 foamUV = input.uv * 50;
                float foamNoise = frac(sin(dot(foamUV + _Time.y * 0.1, float2(12.9898, 78.233))) * 43758.5453);
                // Jacobian-based foam is more selective
                float foamMask = smoothstep(0.1, 0.4, input.foam * _FoamIntensity + foamNoise * 0.1);
                
                float shorelineFoam = 0;
                if (depthDiff > 0 && depthDiff < _ShorelineFoamRange)
                {
                    shorelineFoam = (1.0 - saturate(depthDiff / _ShorelineFoamRange));
                    float shoreNoise = frac(sin(dot(input.uv * 100 + _Time.y * 0.2, float2(12.9898, 78.233))) * 43758.5453);
                    shorelineFoam = smoothstep(0.4, 0.7, shorelineFoam * 0.8 + shoreNoise * 0.2);
                }
                
                float totalFoam = saturate(foamMask + shorelineFoam);
                
                // === Stylized Specular ===
                float3 halfDir = normalize(lightDir + viewDir);
                float smoothnessExp = exp2(10.0 * _Smoothness + 1.0);
                float specSoft = pow(saturate(dot(finalNormal, halfDir)), smoothnessExp);
                float specHard = smoothstep(0.005, 0.01, specSoft);
                float finalSpec = lerp(specSoft, specHard, _SpecularHardness);
                
                // === Environment Reflection ===
                float3 reflectDir = reflect(-viewDir, finalNormal);
                float3 envReflection = GlossyEnvironmentReflection(reflectDir, _Smoothness, 1.0);
                
                // Combine
                float3 finalColor = waterColor.rgb;
                
                float3 tintedRefraction = lerp(refractionColor, refractionColor * waterColor.rgb * 2.0, saturate(depthFactor));
                finalColor = lerp(tintedRefraction, finalColor, saturate(depthFactor * 0.8 + 0.2));
                
                finalColor += sssContrib;
                finalColor += causticsPattern * mainLight.color;
                
                finalColor = lerp(finalColor, envReflection, fresnel * _ReflectionIntensity);
                finalColor += finalSpec * mainLight.color * (1.0 - totalFoam);
                
                finalColor = lerp(finalColor, _FoamColor.rgb, totalFoam);
                finalColor = MixFog(finalColor, input.fogFactor);
                
                // Alpha - Increase base opacity for shallow water
                float baseAlpha = lerp(_ShallowColor.a, 1.0, depthFactor);
                float finalAlpha = saturate(baseAlpha + fresnel * 0.5 + totalFoam + shoreFactor * 0.5);
                
                return float4(finalColor, finalAlpha);
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}
