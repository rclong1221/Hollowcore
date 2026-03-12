Shader "DIG/VoxelTriplanar"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _NormalMap ("Normal Map", 2D) = "bump" {}
        _Scale ("Texture Scale", Float) = 0.25
        _BlendSharpness ("Blend Sharpness", Range(1, 8)) = 4.0
        _Color ("Tint Color", Color) = (1, 1, 1, 1)
        _Smoothness ("Smoothness", Range(0, 1)) = 0.1
        _Metallic ("Metallic", Range(0, 1)) = 0.0
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
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float3 worldTangent : TEXCOORD2;
                float3 worldBitangent : TEXCOORD3;
                float fogFactor : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_NormalMap);
            SAMPLER(sampler_NormalMap);
            
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _Scale;
                float _BlendSharpness;
                float4 _Color;
                float _Smoothness;
                float _Metallic;
            CBUFFER_END
            
            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                
                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs normInputs = GetVertexNormalInputs(IN.normalOS, IN.tangentOS);
                
                OUT.positionHCS = posInputs.positionCS;
                OUT.worldPos = posInputs.positionWS;
                OUT.worldNormal = normInputs.normalWS;
                OUT.worldTangent = normInputs.tangentWS;
                OUT.worldBitangent = normInputs.bitangentWS;
                OUT.fogFactor = ComputeFogFactor(posInputs.positionCS.z);
                
                return OUT;
            }
            
            // Calculate triplanar blending weights from surface normal
            float3 GetTriplanarBlending(float3 worldNormal)
            {
                float3 blending = pow(abs(worldNormal), _BlendSharpness);
                blending /= (blending.x + blending.y + blending.z + 0.001);
                return blending;
            }
            
            // Sample texture using triplanar projection
            half4 TriplanarSample(TEXTURE2D_PARAM(tex, samp), float3 worldPos, float3 blending)
            {
                // Project from 3 axes
                float2 uvX = worldPos.zy * _Scale;
                float2 uvY = worldPos.xz * _Scale;
                float2 uvZ = worldPos.xy * _Scale;
                
                // Sample from each projection
                half4 xSample = SAMPLE_TEXTURE2D(tex, samp, uvX);
                half4 ySample = SAMPLE_TEXTURE2D(tex, samp, uvY);
                half4 zSample = SAMPLE_TEXTURE2D(tex, samp, uvZ);
                
                // Blend based on normal direction
                return xSample * blending.x + ySample * blending.y + zSample * blending.z;
            }
            
            // Sample normal map using triplanar projection
            float3 TriplanarSampleNormal(float3 worldPos, float3 worldNormal, float3 blending)
            {
                // Project from 3 axes
                float2 uvX = worldPos.zy * _Scale;
                float2 uvY = worldPos.xz * _Scale;
                float2 uvZ = worldPos.xy * _Scale;
                
                // Sample normals
                float3 xNormal = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uvX));
                float3 yNormal = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uvY));
                float3 zNormal = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uvZ));
                
                // Whiteout blending for normals
                xNormal = float3(xNormal.xy + worldNormal.zy, abs(xNormal.z) * worldNormal.x);
                yNormal = float3(yNormal.xy + worldNormal.xz, abs(yNormal.z) * worldNormal.y);
                zNormal = float3(zNormal.xy + worldNormal.xy, abs(zNormal.z) * worldNormal.z);
                
                // Swizzle based on projection axis
                float3 blendedNormal = normalize(
                    xNormal.zyx * blending.x +
                    yNormal.xzy * blending.y +
                    zNormal.xyz * blending.z
                );
                
                return blendedNormal;
            }
            
            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                // Get blending weights
                float3 worldNormal = normalize(IN.worldNormal);
                float3 blending = GetTriplanarBlending(worldNormal);
                
                // Sample albedo
                half4 albedo = TriplanarSample(TEXTURE2D_ARGS(_MainTex, sampler_MainTex), IN.worldPos, blending);
                albedo *= _Color;
                
                // Sample normal (optional - only if normal map assigned)
                float3 normal = TriplanarSampleNormal(IN.worldPos, worldNormal, blending);
                
                // Setup surface data for PBR lighting
                InputData inputData = (InputData)0;
                inputData.positionWS = IN.worldPos;
                inputData.normalWS = normal;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(IN.worldPos);
                inputData.shadowCoord = TransformWorldToShadowCoord(IN.worldPos);
                inputData.fogCoord = IN.fogFactor;
                inputData.vertexLighting = half3(0, 0, 0);
                inputData.bakedGI = SampleSH(normal);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.positionHCS);
                inputData.shadowMask = half4(1, 1, 1, 1);
                
                // Surface data
                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = albedo.rgb;
                surfaceData.metallic = _Metallic;
                surfaceData.specular = half3(0, 0, 0);
                surfaceData.smoothness = _Smoothness;
                surfaceData.normalTS = float3(0, 0, 1);
                surfaceData.emission = half3(0, 0, 0);
                surfaceData.occlusion = 1.0;
                surfaceData.alpha = 1.0;
                
                // Calculate final lighting
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
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
            };
            
            float3 _LightDirection;
            
            Varyings ShadowPassVertex(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                
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
            
            half4 ShadowPassFragment(Varyings IN) : SV_Target
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
            Cull Off
            
            HLSLPROGRAM
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
            };
            
            Varyings DepthOnlyVertex(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }
            
            half4 DepthOnlyFragment(Varyings IN) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
    
    FallBack "Universal Render Pipeline/Lit"
}
