Shader "Water/OceanWaterURP"
{
    Properties
    {
        [Header(Water Colors)]
        _ShallowColor ("Shallow Color", Color) = (0.2, 0.6, 0.8, 0.8)
        _DeepColor ("Deep Color", Color) = (0.0, 0.2, 0.4, 1.0)
        _DepthFade ("Depth Fade Distance", Range(0.1, 10)) = 2
        
        [Header(Wave Parameters)]
        _WaveAmplitude ("Wave Amplitude", Range(0, 5)) = 0.5
        _WaveFrequency ("Wave Frequency", Range(0.1, 5)) = 0.63
        _WaveSpeed ("Wave Speed", Range(0, 5)) = 1
        _WaveDirection ("Wave Direction", Vector) = (0, 1, 0, 0)
        _WaveSteepness ("Wave Steepness (Q)", Range(0, 1)) = 0.5
        
        [Header(Secondary Wave)]
        _Wave2Amplitude ("Wave 2 Amplitude", Range(0, 2)) = 0.25
        _Wave2Frequency ("Wave 2 Frequency", Range(0.1, 5)) = 1.26
        _Wave2Speed ("Wave 2 Speed", Range(0, 5)) = 1.5
        _Wave2Direction ("Wave 2 Direction", Vector) = (0.707, 0.707, 0, 0)
        _Wave2Steepness ("Wave 2 Steepness", Range(0, 1)) = 0.3
        
        [Header(Additional Waves)]
        _Wave3Amplitude ("Wave 3 Amplitude", Range(0, 2)) = 0.15
        _Wave3Frequency ("Wave 3 Frequency", Range(0.1, 5)) = 2.4
        _Wave3Speed ("Wave 3 Speed", Range(0, 5)) = 2.0
        _Wave3Direction ("Wave 3 Direction", Vector) = (-0.5, 0.5, 0, 0)
        _Wave3Steepness ("Wave 3 Steepness", Range(0, 1)) = 0.2
        
        _Wave4Amplitude ("Wave 4 Amplitude", Range(0, 2)) = 0.1
        _Wave4Frequency ("Wave 4 Frequency", Range(0.1, 5)) = 3.6
        _Wave4Speed ("Wave 4 Speed", Range(0, 5)) = 2.5
        _Wave4Direction ("Wave 4 Direction", Vector) = (0.1, -0.9, 0, 0)
        _Wave4Steepness ("Wave 4 Steepness", Range(0, 1)) = 0.1
        
        [Header(Surface)]
        _Smoothness ("Smoothness", Range(0, 1)) = 0.9
        _FresnelPower ("Fresnel Power", Range(0.5, 5)) = 4
        _NormalStrength ("Normal Strength", Range(0, 2)) = 0.5
        _ReflectionIntensity ("Reflection Intensity", Range(0, 1)) = 0.5
        
        [Header(Foam)]
        _FoamColor ("Foam Color", Color) = (1, 1, 1, 1)
        _FoamThreshold ("Shore Foam Range", Range(0, 2)) = 0.8
        _CrestFoamThreshold ("Crest Foam Threshold", Range(0, 1)) = 0.5
        _FoamIntensity ("Foam Intensity", Range(0, 1)) = 1.0
        _FoamScale ("Foam Scale", Range(0.1, 50)) = 15.0
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
            Name "WaterForward"
            Tags { "LightMode" = "UniversalForward" }
            
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _ENVIRONMENTREFLECTIONS_OFF
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
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
                float height : TEXCOORD5;
            };
            
            // Properties
            CBUFFER_START(UnityPerMaterial)
                float4 _ShallowColor;
                float4 _DeepColor;
                float _DepthFade;
                
                float _WaveAmplitude;
                float _WaveFrequency;
                float _WaveSpeed;
                float4 _WaveDirection;
                float _WaveSteepness;
                
                float _Wave2Amplitude;
                float _Wave2Frequency;
                float _Wave2Speed;
                float4 _Wave2Direction;
                float _Wave2Steepness;
                
                float _Wave3Amplitude;
                float _Wave3Frequency;
                float _Wave3Speed;
                float4 _Wave3Direction;
                float _Wave3Steepness;
                
                float _Wave4Amplitude;
                float _Wave4Frequency;
                float _Wave4Speed;
                float4 _Wave4Direction;
                float _Wave4Steepness;
                
                float _Smoothness;
                float _FresnelPower;
                float _NormalStrength;
                float _ReflectionIntensity;
                
                float4 _FoamColor;
                float _FoamThreshold;
                float _CrestFoamThreshold;
                float _FoamIntensity;
                float _FoamScale;
            CBUFFER_END
            
            // Depth texture for shore effects
            TEXTURE2D(_CameraDepthTexture);
            SAMPLER(sampler_CameraDepthTexture);
            
            // Gerstner wave calculation
            void GerstnerWave(
                float2 position, 
                float time,
                float amplitude,
                float frequency,
                float speed,
                float2 direction,
                float steepness,
                inout float3 displacement,
                inout float3 tangent,
                inout float3 binormal)
            {
                float q = steepness;
                float phase = speed * frequency;
                float dotProduct = dot(direction, position);
                float theta = frequency * dotProduct + phase * time;
                
                float sinTheta = sin(theta);
                float cosTheta = cos(theta);
                
                displacement.x += q * amplitude * direction.x * cosTheta;
                displacement.y += amplitude * sinTheta;
                displacement.z += q * amplitude * direction.y * cosTheta;
                
                float wa = frequency * amplitude;
                tangent.x += -q * direction.x * direction.x * wa * sinTheta;
                tangent.y += direction.x * wa * cosTheta;
                tangent.z += -q * direction.x * direction.y * wa * sinTheta;
                
                binormal.x += -q * direction.x * direction.y * wa * sinTheta;
                binormal.y += direction.y * wa * cosTheta;
                binormal.z += -q * direction.y * direction.y * wa * sinTheta;
            }
            
            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 displacement = float3(0, 0, 0);
                float3 tangent = float3(0, 0, 0);
                float3 binormal = float3(0, 0, 0);
                float time = _Time.y;
                
                GerstnerWave(posWS.xz, time, _WaveAmplitude, _WaveFrequency, _WaveSpeed, normalize(_WaveDirection.xy), _WaveSteepness, displacement, tangent, binormal);
                GerstnerWave(posWS.xz, time, _Wave2Amplitude, _Wave2Frequency, _Wave2Speed, normalize(_Wave2Direction.xy), _Wave2Steepness, displacement, tangent, binormal);
                GerstnerWave(posWS.xz, time, _Wave3Amplitude, _Wave3Frequency, _Wave3Speed, normalize(_Wave3Direction.xy), _Wave3Steepness, displacement, tangent, binormal);
                GerstnerWave(posWS.xz, time, _Wave4Amplitude, _Wave4Frequency, _Wave4Speed, normalize(_Wave4Direction.xy), _Wave4Steepness, displacement, tangent, binormal);
                
                posWS += displacement;
                float3 normalWS = normalize(cross(float3(1, tangent.y, tangent.z), float3(binormal.x, binormal.y, 1)));
                normalWS = lerp(float3(0, 1, 0), normalWS, _NormalStrength);
                
                output.positionWS = posWS;
                output.positionCS = TransformWorldToHClip(posWS);
                output.normalWS = normalWS;
                output.uv = input.uv;
                output.screenPos = ComputeScreenPos(output.positionCS);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                output.height = displacement.y;
                
                return output;
            }
            
            float hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453123);
            }

            float4 frag(Varyings input) : SV_Target
            {
                // Depth logic
                float2 screenUV = input.screenPos.xy / input.screenPos.w;
                float rawDepth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, sampler_CameraDepthTexture, screenUV);
                float sceneDepth = LinearEyeDepth(rawDepth, _ZBufferParams);
                float surfaceDepth = input.screenPos.w;
                float depthDist = sceneDepth - surfaceDepth;
                
                float farPlane = _ProjectionParams.z;
                bool isFar = sceneDepth >= farPlane * 0.99;
                bool noTerrain = isFar || depthDist < 0 || depthDist > 100;
                
                float depthFactor = noTerrain ? 1.0 : saturate(depthDist / _DepthFade);
                float4 waterColor = lerp(_ShallowColor, _DeepColor, depthFactor);
                
                // Procedural detail
                float2 p = input.uv * _FoamScale;
                float noise = hash(floor(p + _Time.y * 0.2));
                float3 finalNormal = normalize(input.normalWS + float3(noise * 0.05, 0, 0));

                // Environment Reflections
                float3 viewDir = normalize(_WorldSpaceCameraPos - input.positionWS);
                float3 reflectDir = reflect(-viewDir, finalNormal);
                float4 reflectColor = float4(GlossyEnvironmentReflection(reflectDir, _Smoothness, 1.0), 1.0);
                
                float fresnel = pow(1.0 - saturate(dot(finalNormal, viewDir)), _FresnelPower);
                
                // Lighting
                Light mainLight = GetMainLight();
                float spec = pow(saturate(dot(finalNormal, normalize(mainLight.direction + viewDir))), _Smoothness * 128);
                
                // Stylized Foam
                float foamNoise = hash(floor(input.uv * _FoamScale + _Time.y * 0.1));
                
                // 1. Shore Foam (Toon style)
                float shoreFoam = 0;
                if (!noTerrain && depthDist < _FoamThreshold)
                {
                    float shoreFactor = 1.0 - saturate(depthDist / _FoamThreshold);
                    shoreFoam = smoothstep(0.4, 0.5, shoreFactor + foamNoise * 0.2);
                }
                
                // 2. Crest Foam
                float crestFoam = 0;
                float crestFactor = saturate(input.height / _CrestFoamThreshold);
                crestFoam = smoothstep(0.7, 0.8, crestFactor + foamNoise * 0.3);

                float combinedFoam = saturate(shoreFoam + crestFoam) * _FoamIntensity;

                // Combine
                float3 finalColor = waterColor.rgb;
                // Add reflections based on fresnel
                finalColor = lerp(finalColor, reflectColor.rgb, fresnel * _ReflectionIntensity);
                // Add light and spec
                finalColor += spec * mainLight.color * 0.5;
                // Layer foam on top
                finalColor = lerp(finalColor, _FoamColor.rgb, combinedFoam);
                
                // Fog and Alpha
                finalColor = MixFog(finalColor, input.fogFactor);
                float alpha = noTerrain ? 1.0 : lerp(waterColor.a, 1.0, fresnel * 0.5 + combinedFoam);
                
                return float4(finalColor, alpha);
            }
            ENDHLSL
        }
        
        // Shadow caster pass (optional, for water casting shadows)
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            
            ZWrite On
            ZTest LEqual
            ColorMask 0
            
            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
            struct ShadowAttributes
            {
                float4 positionOS : POSITION;
            };
            
            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
            };
            
            ShadowVaryings ShadowVert(ShadowAttributes input)
            {
                ShadowVaryings output;
                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(posWS);
                return output;
            }
            
            half4 ShadowFrag(ShadowVaryings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
    
    FallBack "Universal Render Pipeline/Lit"
}
