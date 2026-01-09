Shader "Custom/ProceduralTerrainURP"
{
    Properties
    {
        // Layer 0 - Sand/Beach
        _Layer0Albedo ("Layer 0 Albedo (Sand)", 2D) = "white" {}
        _Layer0Normal ("Layer 0 Normal", 2D) = "bump" {}
        _Layer0Tint ("Layer 0 Tint", Color) = (0.96, 0.87, 0.70, 1)
        _Layer0Scale ("Layer 0 Scale", Float) = 20
        
        // Layer 1 - Grass
        _Layer1Albedo ("Layer 1 Albedo (Grass)", 2D) = "white" {}
        _Layer1Normal ("Layer 1 Normal", 2D) = "bump" {}
        _Layer1Tint ("Layer 1 Tint", Color) = (0.3, 0.6, 0.2, 1)
        _Layer1Scale ("Layer 1 Scale", Float) = 15
        
        // Layer 2 - Rock
        _Layer2Albedo ("Layer 2 Albedo (Rock)", 2D) = "white" {}
        _Layer2Normal ("Layer 2 Normal", 2D) = "bump" {}
        _Layer2Tint ("Layer 2 Tint", Color) = (0.5, 0.5, 0.5, 1)
        _Layer2Scale ("Layer 2 Scale", Float) = 10
        
        // Layer 3 - Snow/Peak
        _Layer3Albedo ("Layer 3 Albedo (Snow)", 2D) = "white" {}
        _Layer3Normal ("Layer 3 Normal", 2D) = "bump" {}
        _Layer3Tint ("Layer 3 Tint", Color) = (0.95, 0.95, 0.98, 1)
        _Layer3Scale ("Layer 3 Scale", Float) = 25
        
        // Height-based blending
        _HeightBlendSharpness ("Height Blend Sharpness", Range(0.01, 10)) = 2
        
        // Layer heights (normalized 0-1)
        _SandHeight ("Sand Max Height", Range(0, 1)) = 0.1
        _GrassHeight ("Grass Max Height", Range(0, 1)) = 0.5
        _RockHeight ("Rock Max Height", Range(0, 1)) = 0.8
        
        // Slope blending
        _SlopeThreshold ("Slope Threshold", Range(0, 1)) = 0.4
        _SlopeBlend ("Slope Blend", Range(0.01, 1)) = 0.2
        
        // Terrain properties
        _MaxHeight ("Max Terrain Height", Float) = 50
        _Metallic ("Metallic", Range(0, 1)) = 0
        _Smoothness ("Smoothness", Range(0, 1)) = 0.3
        
        // Shore/Water edge
        _ShoreColor ("Shore Color", Color) = (0.7, 0.8, 0.6, 1)
        _ShoreHeight ("Shore Height", Range(0, 0.2)) = 0.02
        _ShoreBlend ("Shore Blend", Range(0.001, 0.1)) = 0.01
        
        // Triplanar mapping
        _TriplanarSharpness ("Triplanar Sharpness", Range(1, 10)) = 5
    }
    
    SubShader
    {
        Tags 
        { 
            "RenderType" = "Opaque" 
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }
        
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            
            TEXTURE2D(_Layer0Albedo); SAMPLER(sampler_Layer0Albedo);
            TEXTURE2D(_Layer0Normal); SAMPLER(sampler_Layer0Normal);
            TEXTURE2D(_Layer1Albedo); SAMPLER(sampler_Layer1Albedo);
            TEXTURE2D(_Layer1Normal); SAMPLER(sampler_Layer1Normal);
            TEXTURE2D(_Layer2Albedo); SAMPLER(sampler_Layer2Albedo);
            TEXTURE2D(_Layer2Normal); SAMPLER(sampler_Layer2Normal);
            TEXTURE2D(_Layer3Albedo); SAMPLER(sampler_Layer3Albedo);
            TEXTURE2D(_Layer3Normal); SAMPLER(sampler_Layer3Normal);
            
            CBUFFER_START(UnityPerMaterial)
                float4 _Layer0Tint;
                float4 _Layer1Tint;
                float4 _Layer2Tint;
                float4 _Layer3Tint;
                float _Layer0Scale;
                float _Layer1Scale;
                float _Layer2Scale;
                float _Layer3Scale;
                float _HeightBlendSharpness;
                float _SandHeight;
                float _GrassHeight;
                float _RockHeight;
                float _SlopeThreshold;
                float _SlopeBlend;
                float _MaxHeight;
                float _Metallic;
                float _Smoothness;
                float4 _ShoreColor;
                float _ShoreHeight;
                float _ShoreBlend;
                float _TriplanarSharpness;
            CBUFFER_END
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
            };
            
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float4 tangentWS : TEXCOORD2;
                float2 uv : TEXCOORD3;
                float fogFactor : TEXCOORD4;
                float3 viewDirWS : TEXCOORD5;
            };
            
            Varyings vert(Attributes input)
            {
                Varyings output;
                
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                
                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.normalWS = normalInput.normalWS;
                output.tangentWS = float4(normalInput.tangentWS, input.tangentOS.w);
                output.uv = input.uv;
                output.fogFactor = ComputeFogFactor(vertexInput.positionCS.z);
                output.viewDirWS = GetWorldSpaceViewDir(vertexInput.positionWS);
                
                return output;
            }
            
            // Triplanar sampling for better slope texturing
            float4 SampleTriplanar(TEXTURE2D_PARAM(tex, samp), float3 worldPos, float3 worldNormal, float scale)
            {
                float3 blendWeights = pow(abs(worldNormal), _TriplanarSharpness);
                blendWeights /= (blendWeights.x + blendWeights.y + blendWeights.z);
                
                float4 xProj = SAMPLE_TEXTURE2D(tex, samp, worldPos.yz * scale);
                float4 yProj = SAMPLE_TEXTURE2D(tex, samp, worldPos.xz * scale);
                float4 zProj = SAMPLE_TEXTURE2D(tex, samp, worldPos.xy * scale);
                
                return xProj * blendWeights.x + yProj * blendWeights.y + zProj * blendWeights.z;
            }
            
            // Smooth blend function
            float SmoothBlend(float value, float min, float max, float blend)
            {
                float lower = smoothstep(min - blend, min + blend, value);
                float upper = 1.0 - smoothstep(max - blend, max + blend, value);
                return lower * upper;
            }
            
            float4 frag(Varyings input) : SV_Target
            {
                // Calculate normalized height and slope
                float height = input.positionWS.y / _MaxHeight;
                height = saturate(height);
                
                float3 normalWS = normalize(input.normalWS);
                float slope = 1.0 - abs(normalWS.y); // 0 = flat, 1 = vertical
                
                // Calculate layer weights based on height
                float blend = 1.0 / _HeightBlendSharpness;
                
                float sandWeight = SmoothBlend(height, 0, _SandHeight, blend);
                float grassWeight = SmoothBlend(height, _SandHeight * 0.8, _GrassHeight, blend);
                float rockWeight = SmoothBlend(height, _GrassHeight * 0.8, _RockHeight, blend);
                float snowWeight = SmoothBlend(height, _RockHeight * 0.8, 1.0, blend);
                
                // Shore enhancement
                float shoreWeight = SmoothBlend(height, 0, _ShoreHeight, _ShoreBlend);
                
                // Slope-based rock override
                float slopeRockFactor = smoothstep(_SlopeThreshold - _SlopeBlend, _SlopeThreshold + _SlopeBlend, slope);
                rockWeight = max(rockWeight, slopeRockFactor);
                grassWeight *= (1.0 - slopeRockFactor);
                sandWeight *= (1.0 - slopeRockFactor * 0.5);
                
                // Normalize weights
                float totalWeight = sandWeight + grassWeight + rockWeight + snowWeight + 0.0001;
                sandWeight /= totalWeight;
                grassWeight /= totalWeight;
                rockWeight /= totalWeight;
                snowWeight /= totalWeight;
                
                // Sample textures using triplanar for slopes
                float3 worldPos = input.positionWS;
                
                float4 sandColor = SampleTriplanar(TEXTURE2D_ARGS(_Layer0Albedo, sampler_Layer0Albedo), 
                                                   worldPos, normalWS, 1.0 / _Layer0Scale) * _Layer0Tint;
                float4 grassColor = SampleTriplanar(TEXTURE2D_ARGS(_Layer1Albedo, sampler_Layer1Albedo), 
                                                    worldPos, normalWS, 1.0 / _Layer1Scale) * _Layer1Tint;
                float4 rockColor = SampleTriplanar(TEXTURE2D_ARGS(_Layer2Albedo, sampler_Layer2Albedo), 
                                                   worldPos, normalWS, 1.0 / _Layer2Scale) * _Layer2Tint;
                float4 snowColor = SampleTriplanar(TEXTURE2D_ARGS(_Layer3Albedo, sampler_Layer3Albedo), 
                                                   worldPos, normalWS, 1.0 / _Layer3Scale) * _Layer3Tint;
                
                // Blend colors
                float4 albedo = sandColor * sandWeight + 
                               grassColor * grassWeight + 
                               rockColor * rockWeight + 
                               snowColor * snowWeight;
                
                // Apply shore tint
                albedo = lerp(albedo, _ShoreColor, shoreWeight * 0.5);
                
                // Sample and blend normals
                float3 sandNorm = UnpackNormal(SampleTriplanar(TEXTURE2D_ARGS(_Layer0Normal, sampler_Layer0Normal), 
                                               worldPos, normalWS, 1.0 / _Layer0Scale));
                float3 grassNorm = UnpackNormal(SampleTriplanar(TEXTURE2D_ARGS(_Layer1Normal, sampler_Layer1Normal), 
                                                worldPos, normalWS, 1.0 / _Layer1Scale));
                float3 rockNorm = UnpackNormal(SampleTriplanar(TEXTURE2D_ARGS(_Layer2Normal, sampler_Layer2Normal), 
                                               worldPos, normalWS, 1.0 / _Layer2Scale));
                float3 snowNorm = UnpackNormal(SampleTriplanar(TEXTURE2D_ARGS(_Layer3Normal, sampler_Layer3Normal), 
                                               worldPos, normalWS, 1.0 / _Layer3Scale));
                
                float3 blendedNormal = normalize(sandNorm * sandWeight + 
                                                 grassNorm * grassWeight + 
                                                 rockNorm * rockWeight + 
                                                 snowNorm * snowWeight);
                
                // Transform normal to world space
                float3 bitangentWS = cross(normalWS, input.tangentWS.xyz) * input.tangentWS.w;
                float3x3 TBN = float3x3(input.tangentWS.xyz, bitangentWS, normalWS);
                float3 finalNormal = normalize(mul(blendedNormal, TBN));
                
                // Lighting
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = finalNormal;
                inputData.viewDirectionWS = normalize(input.viewDirWS);
                inputData.fogCoord = input.fogFactor;
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                
                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = albedo.rgb;
                surfaceData.metallic = _Metallic;
                surfaceData.smoothness = _Smoothness;
                surfaceData.normalTS = blendedNormal;
                surfaceData.occlusion = 1.0;
                surfaceData.alpha = 1.0;
                
                float4 color = UniversalFragmentPBR(inputData, surfaceData);
                color.rgb = MixFog(color.rgb, input.fogFactor);
                
                return color;
            }
            ENDHLSL
        }
        
        // Shadow casting pass
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            
            ZWrite On
            ZTest LEqual
            ColorMask 0
            
            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };
            
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };
            
            float3 _LightDirection;
            
            Varyings ShadowPassVertex(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));
                
                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #endif
                
                output.positionCS = positionCS;
                return output;
            }
            
            float4 ShadowPassFragment(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
        
        // Depth pass
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            
            ZWrite On
            ColorMask 0
            
            HLSLPROGRAM
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
            struct Attributes
            {
                float4 position : POSITION;
            };
            
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };
            
            Varyings DepthOnlyVertex(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.position.xyz);
                return output;
            }
            
            float4 DepthOnlyFragment(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
    
    FallBack "Universal Render Pipeline/Lit"
}
