Shader "DIG/VoxelTriplanarEnhanced"
{
    Properties
    {
        [Header(Material Texture Arrays)]
        _AlbedoArray ("Albedo Array", 2DArray) = "" {}
        _NormalArray ("Normal Array", 2DArray) = "" {}
        _HeightArray ("Height Array", 2DArray) = "" {}
        
        [Header(Detail Textures)]
        _DetailTex ("Detail Albedo (tiled)", 2D) = "white" {}
        _DetailNormal ("Detail Normal (tiled)", 2D) = "bump" {}
        _DetailScale ("Detail Scale", Range(1, 20)) = 8.0
        _DetailStrength ("Detail Strength", Range(0, 1)) = 0.3
        _DetailFadeDistance ("Detail Fade Distance", Range(5, 50)) = 20.0
        
        [Header(Triplanar Settings)]
        _Scale ("Texture Scale", Float) = 0.25
        _BlendSharpness ("Blend Sharpness", Range(1, 16)) = 4.0
        _HeightBlendSharpness ("Height Blend Sharpness", Range(0, 10)) = 2.0
        
        [Header(Surface Properties)]
        _Smoothness ("Smoothness", Range(0, 1)) = 0.1
        _Metallic ("Metallic", Range(0, 1)) = 0.0
        _NormalStrength ("Normal Strength", Range(0, 2)) = 1.0
        
        [Header(Ambient Occlusion)]
        _AOStrength ("AO Strength", Range(0, 1)) = 0.5
        _AOFromVertexColor ("AO From Vertex Alpha", Range(0, 1)) = 0.0
        
        [Header(Color Tints)]
        _Color1 ("Material 1 Tint", Color) = (1, 1, 1, 1)
        _Color2 ("Material 2 Tint", Color) = (1, 1, 1, 1)
        _Color3 ("Material 3 Tint", Color) = (1, 1, 1, 1)
        _Color4 ("Material 4 Tint", Color) = (1, 1, 1, 1)
    }
    
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline" = "UniversalPipeline" "Queue"="Geometry" }
        Cull Off
        
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #pragma require 2darray
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float4 color : COLOR; // R = material ID, G = blend, A = AO
            };
            
            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float3 worldTangent : TEXCOORD2;
                float3 worldBitangent : TEXCOORD3;
                float4 vertexColor : TEXCOORD4;
                float fogFactor : TEXCOORD5;
                float viewDist : TEXCOORD6;
            };
            
            TEXTURE2D_ARRAY(_AlbedoArray);
            SAMPLER(sampler_AlbedoArray);
            TEXTURE2D_ARRAY(_NormalArray);
            SAMPLER(sampler_NormalArray);
            TEXTURE2D_ARRAY(_HeightArray);
            SAMPLER(sampler_HeightArray);
            
            TEXTURE2D(_DetailTex);
            SAMPLER(sampler_DetailTex);
            TEXTURE2D(_DetailNormal);
            SAMPLER(sampler_DetailNormal);
            
            CBUFFER_START(UnityPerMaterial)
                float _Scale;
                float _BlendSharpness;
                float _HeightBlendSharpness;
                float _Smoothness;
                float _Metallic;
                float _NormalStrength;
                float _DetailScale;
                float _DetailStrength;
                float _DetailFadeDistance;
                float _AOStrength;
                float _AOFromVertexColor;
                float4 _Color1;
                float4 _Color2;
                float4 _Color3;
                float4 _Color4;
            CBUFFER_END
            
            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                
                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs normInputs = GetVertexNormalInputs(IN.normalOS, IN.tangentOS);
                
                OUT.positionHCS = posInputs.positionCS;
                OUT.worldPos = posInputs.positionWS;
                OUT.worldNormal = normInputs.normalWS;
                OUT.worldTangent = normInputs.tangentWS;
                OUT.worldBitangent = normInputs.bitangentWS;
                OUT.vertexColor = IN.color;
                OUT.fogFactor = ComputeFogFactor(posInputs.positionCS.z);
                OUT.viewDist = length(posInputs.positionWS - _WorldSpaceCameraPos);
                
                return OUT;
            }
            
            float3 GetTriplanarBlending(float3 worldNormal)
            {
                float3 blending = pow(abs(worldNormal), _BlendSharpness);
                blending /= (blending.x + blending.y + blending.z + 0.001);
                return blending;
            }
            
            half4 TriplanarSampleArray(TEXTURE2D_ARRAY_PARAM(tex, samp), float3 worldPos, float3 blending, int index)
            {
                float2 uvX = worldPos.zy * _Scale;
                float2 uvY = worldPos.xz * _Scale;
                float2 uvZ = worldPos.xy * _Scale;
                
                half4 xSample = SAMPLE_TEXTURE2D_ARRAY(tex, samp, uvX, index);
                half4 ySample = SAMPLE_TEXTURE2D_ARRAY(tex, samp, uvY, index);
                half4 zSample = SAMPLE_TEXTURE2D_ARRAY(tex, samp, uvZ, index);
                
                return xSample * blending.x + ySample * blending.y + zSample * blending.z;
            }
            
            half4 TriplanarSample2D(TEXTURE2D_PARAM(tex, samp), float3 worldPos, float3 blending, float scale)
            {
                float2 uvX = worldPos.zy * scale;
                float2 uvY = worldPos.xz * scale;
                float2 uvZ = worldPos.xy * scale;
                
                half4 xSample = SAMPLE_TEXTURE2D(tex, samp, uvX);
                half4 ySample = SAMPLE_TEXTURE2D(tex, samp, uvY);
                half4 zSample = SAMPLE_TEXTURE2D(tex, samp, uvZ);
                
                return xSample * blending.x + ySample * blending.y + zSample * blending.z;
            }
            
            // Blend normals using RNM (Reoriented Normal Mapping)
            float3 BlendNormalsRNM(float3 n1, float3 n2)
            {
                n1.z += 1.0;
                n2 *= float3(-1, -1, 1);
                return n1 * dot(n1, n2) / n1.z - n2;
            }
            
            half4 frag(Varyings IN) : SV_Target
            {
                float3 worldNormal = normalize(IN.worldNormal);
                float3 blending = GetTriplanarBlending(worldNormal);
                
                // Get material ID from vertex color (0-255 mapped to 0-1)
                int materialIndex = (int)(IN.vertexColor.r * 255.0 + 0.5);
                materialIndex = clamp(materialIndex, 0, 15); // Max 16 materials
                
                // Get tint color based on material
                float4 tint = _Color1;
                if (materialIndex == 1) tint = _Color1;
                else if (materialIndex == 2) tint = _Color2;
                else if (materialIndex == 3) tint = _Color3;
                else if (materialIndex >= 4) tint = _Color4;
                
                // Sample base textures
                half4 albedo = TriplanarSampleArray(TEXTURE2D_ARRAY_ARGS(_AlbedoArray, sampler_AlbedoArray), IN.worldPos, blending, materialIndex);
                half4 normalSample = TriplanarSampleArray(TEXTURE2D_ARRAY_ARGS(_NormalArray, sampler_NormalArray), IN.worldPos, blending, materialIndex);
                half4 heightSample = TriplanarSampleArray(TEXTURE2D_ARRAY_ARGS(_HeightArray, sampler_HeightArray), IN.worldPos, blending, materialIndex);
                
                // Apply tint
                albedo *= tint;
                
                // Detail textures (fade with distance)
                float detailFade = saturate(1.0 - (IN.viewDist / _DetailFadeDistance));
                detailFade *= _DetailStrength;
                
                if (detailFade > 0.01)
                {
                    half4 detailAlbedo = TriplanarSample2D(TEXTURE2D_ARGS(_DetailTex, sampler_DetailTex), IN.worldPos, blending, _DetailScale);
                    half4 detailNormal = TriplanarSample2D(TEXTURE2D_ARGS(_DetailNormal, sampler_DetailNormal), IN.worldPos, blending, _DetailScale);
                    
                    // Blend detail (overlay mode for albedo)
                    albedo.rgb = lerp(albedo.rgb, albedo.rgb * detailAlbedo.rgb * 2.0, detailFade);
                    
                    // Blend detail normals
                    float3 baseNormal = UnpackNormalScale(normalSample, _NormalStrength);
                    float3 detNormal = UnpackNormalScale(detailNormal, detailFade);
                    normalSample = half4(BlendNormalsRNM(baseNormal, detNormal) * 0.5 + 0.5, 1.0);
                }
                
                // Compute tangent-space normal
                float3 normalTS = UnpackNormalScale(normalSample, _NormalStrength);
                float3x3 TBN = float3x3(IN.worldTangent, IN.worldBitangent, worldNormal);
                float3 finalNormal = normalize(mul(normalTS, TBN));
                
                // Ambient occlusion
                float ao = 1.0;
                ao = lerp(ao, heightSample.r, _AOStrength); // Use height as AO approximation
                ao = lerp(ao, IN.vertexColor.a, _AOFromVertexColor); // Optionally use vertex AO
                
                // Setup lighting
                InputData inputData = (InputData)0;
                inputData.positionWS = IN.worldPos;
                inputData.normalWS = finalNormal;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(IN.worldPos);
                inputData.shadowCoord = TransformWorldToShadowCoord(IN.worldPos);
                inputData.fogCoord = IN.fogFactor;
                inputData.bakedGI = SampleSH(finalNormal) * ao;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.positionHCS);
                inputData.shadowMask = half4(1, 1, 1, 1);
                
                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = albedo.rgb;
                surfaceData.metallic = _Metallic;
                surfaceData.smoothness = _Smoothness;
                surfaceData.normalTS = normalTS;
                surfaceData.occlusion = ao;
                surfaceData.alpha = 1.0;
                
                half4 color = UniversalFragmentPBR(inputData, surfaceData);
                color.rgb = MixFog(color.rgb, IN.fogFactor);
                
                return color;
            }
            ENDHLSL
        }
        
        // Shadow caster pass
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Off
            
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
                float4 positionHCS : SV_POSITION;
            };
            
            float3 _LightDirection;
            
            Varyings ShadowPassVertex(Attributes IN)
            {
                Varyings OUT;
                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(IN.normalOS);
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));
                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                OUT.positionHCS = positionCS;
                return OUT;
            }
            
            half4 ShadowPassFragment(Varyings IN) : SV_Target { return 0; }
            ENDHLSL
        }
        
        // Depth pass for SSAO
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            
            ZWrite On
            ColorMask 0
            Cull Off
            
            HLSLPROGRAM
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
            struct Attributes
            {
                float4 positionOS : POSITION;
            };
            
            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
            };
            
            Varyings DepthOnlyVertex(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }
            
            half4 DepthOnlyFragment(Varyings IN) : SV_Target { return 0; }
            ENDHLSL
        }
        
        // Depth normals for SSAO
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            
            ZWrite On
            Cull Off
            
            HLSLPROGRAM
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };
            
            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
            };
            
            Varyings DepthNormalsVertex(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                return OUT;
            }
            
            half4 DepthNormalsFragment(Varyings IN) : SV_Target
            {
                return half4(normalize(IN.normalWS) * 0.5 + 0.5, 0);
            }
            ENDHLSL
        }
    }
    
    FallBack "DIG/VoxelTriplanarMultiMaterial"
}
