Shader "DIG/Voxel/Triplanar"
{
    Properties
    {
        _MainTex ("Texture Array", 2DArray) = "white" {}
        _Smoothness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0
        _TextureScale ("Texture Scale", Float) = 1.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            // DOTS Instancing Support
            #pragma target 4.5
            #pragma multi_compile _ DOTS_INSTANCING_ON
            
            #pragma vertex vert
            #pragma fragment frag
            #pragma require 2darray

            // Universal Render Pipeline keywords
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            
            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float4 color        : COLOR; // r = materialID
                float2 uv           : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float3 positionWS   : TEXCOORD0;
                float3 normalWS     : TEXCOORD1;
                float materialID    : TEXCOORD2;
                float fogFactor     : TEXCOORD3;
                float3 viewDirWS    : TEXCOORD4;
                half3 vertexSH      : TEXCOORD5;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            TEXTURE2D_ARRAY(_MainTex);
            SAMPLER(sampler_MainTex);

            // Standard Unity Instancing & SRP Batcher Support
            // SRP Batcher requires all material properties to be in UnityPerMaterial CBuffer
            CBUFFER_START(UnityPerMaterial)
                float _Smoothness;
                float _Metallic;
                float _TextureScale;
            CBUFFER_END
            
            // Instancing macros map to CBuffer for SRP, or Instanced Array for GPU Instancing
            #ifdef UNITY_INSTANCING_ENABLED
                UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
                    UNITY_DEFINE_INSTANCED_PROP(float, _Smoothness)
                    UNITY_DEFINE_INSTANCED_PROP(float, _Metallic)
                    UNITY_DEFINE_INSTANCED_PROP(float, _TextureScale)
                UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)
                
                #define _Smoothness     UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Smoothness)
                #define _Metallic       UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Metallic)
                #define _TextureScale   UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _TextureScale)
            #endif
            
            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                
                // Ensure _TextureScale is accessed correctly if needed in vert, currently not used in vert except for potential UVs
                


                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, float4(1,1,1,1));
                output.normalWS = normalInput.normalWS;
                
                output.materialID = floor(input.color.r * 255.0 + 0.5); // Material ID from vertex color (0..1 -> 0..255)
                output.viewDirWS = GetWorldSpaceViewDir(output.positionWS);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);

                // Spherical harmonics for ambient lighting
                output.vertexSH = SampleSH(output.normalWS);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 pos = input.positionWS * _TextureScale;
                float3 normal = normalize(input.normalWS);
                float3 blend = abs(normal);
                blend /= (blend.x + blend.y + blend.z);

                // Triplanar Mapping with Texture Array
                // Pass materialID as index
                half4 xCol = SAMPLE_TEXTURE2D_ARRAY(_MainTex, sampler_MainTex, pos.yz, input.materialID);
                half4 yCol = SAMPLE_TEXTURE2D_ARRAY(_MainTex, sampler_MainTex, pos.xz, input.materialID);
                half4 zCol = SAMPLE_TEXTURE2D_ARRAY(_MainTex, sampler_MainTex, pos.xy, input.materialID);

                half4 baseColor = xCol * blend.x + yCol * blend.y + zCol * blend.z;

                // Simple lighting setup
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = normal;
                inputData.viewDirectionWS = normalize(input.viewDirWS);
                inputData.fogCoord = input.fogFactor;
                inputData.vertexLighting = float3(0,0,0);
                inputData.bakedGI = input.vertexSH;
                inputData.normalizedScreenSpaceUV = float2(0,0);
                inputData.shadowMask = float4(1,1,1,1);

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = baseColor.rgb;
                surfaceData.metallic = _Metallic;
                surfaceData.smoothness = _Smoothness;
                surfaceData.normalTS = float3(0,0,1);
                surfaceData.occlusion = 1.0;
                surfaceData.alpha = 1.0;
                surfaceData.emission = float3(0,0,0);
                surfaceData.specular = float3(0,0,0);
                surfaceData.clearCoatMask = 0;
                surfaceData.clearCoatSmoothness = 0;

                // URP PBR Lighting
                return UniversalFragmentPBR(inputData, surfaceData);
            }
            ENDHLSL
        }
        
        // Shadow Caster Pass (Standard)
        Pass 
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }
        
        // Depth Only Pass
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ColorMask 0
            ZWrite On

            HLSLPROGRAM
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }
    }
    
    Fallback "Universal Render Pipeline/Lit"
}
