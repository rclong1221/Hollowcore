Shader "DIG/URP/Dissolve"
{
    Properties
    {
        [MainTexture] _BaseMap ("Albedo", 2D) = "white" {}
        [MainColor]   _BaseColor ("Color", Color) = (1,1,1,1)
        _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Scale", Float) = 1.0
        _MetallicGlossMap ("Metallic (R) Smoothness (A)", 2D) = "white" {}
        _Metallic ("Metallic", Range(0,1)) = 0.0
        _Smoothness ("Smoothness", Range(0,1)) = 0.5

        [Header(Dissolve)]
        _DissolveAmount ("Dissolve Amount", Range(0,1)) = 0.0
        _DissolveNoise ("Dissolve Noise", 2D) = "white" {}
        _DissolveEdgeWidth ("Edge Width", Range(0, 0.15)) = 0.04
        [HDR] _DissolveEdgeColor ("Edge Color", Color) = (3, 1.5, 0.3, 1)
        _DissolveDirection ("Dissolve Direction (world)", Vector) = (0, 1, 0, 0)
        [Toggle] _UseDirectional ("Use Directional Dissolve", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "AlphaTest"
        }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On
            AlphaToMask Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);        SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap);        SAMPLER(sampler_BumpMap);
            TEXTURE2D(_MetallicGlossMap); SAMPLER(sampler_MetallicGlossMap);
            TEXTURE2D(_DissolveNoise);  SAMPLER(sampler_DissolveNoise);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _BumpScale;
                half _Metallic;
                half _Smoothness;
                float _DissolveAmount;
                float4 _DissolveNoise_ST;
                float _DissolveEdgeWidth;
                half4 _DissolveEdgeColor;
                float4 _DissolveDirection;
                float _UseDirectional;
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
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float3 tangentWS : TEXCOORD3;
                float3 bitangentWS : TEXCOORD4;
                float fogFactor : TEXCOORD5;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.normalWS = normInputs.normalWS;
                output.tangentWS = normInputs.tangentWS;
                output.bitangentWS = normInputs.bitangentWS;
                output.fogFactor = ComputeFogFactor(posInputs.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // ─── Dissolve clip ───
                float noise = SAMPLE_TEXTURE2D(_DissolveNoise, sampler_DissolveNoise,
                    input.uv * _DissolveNoise_ST.xy + _DissolveNoise_ST.zw).r;

                float dissolveThreshold;
                if (_UseDirectional > 0.5)
                {
                    // Directional dissolve: height-based + noise perturbation
                    float3 dir = normalize(_DissolveDirection.xyz);
                    float height = dot(input.positionWS, dir);
                    // Normalize to approximate [0,1] range using object bounds
                    // Scale factor makes it work for typical character meshes (0-2m tall)
                    float heightNorm = saturate(height * 0.5 + 0.5);
                    dissolveThreshold = heightNorm + noise * 0.3;
                }
                else
                {
                    // Noise-based dissolve
                    dissolveThreshold = noise;
                }

                float alpha = step(_DissolveAmount, dissolveThreshold);
                clip(alpha - 0.001);

                // ─── Emissive dissolve edge ───
                float edgeMask = smoothstep(_DissolveAmount - _DissolveEdgeWidth, _DissolveAmount, dissolveThreshold)
                               * (1.0 - alpha);
                // Invert: edgeMask should glow on the surviving side near the clip boundary
                edgeMask = smoothstep(_DissolveAmount, _DissolveAmount + _DissolveEdgeWidth, dissolveThreshold)
                         * step(dissolveThreshold, _DissolveAmount + _DissolveEdgeWidth);

                // ─── Standard PBR ───
                half4 baseColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;

                // Normal mapping
                half3 normalTS = UnpackNormalScale(
                    SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv), _BumpScale);
                float3x3 TBN = float3x3(input.tangentWS, input.bitangentWS, input.normalWS);
                float3 normalWS = normalize(mul(normalTS, TBN));

                // Metallic/Smoothness
                half4 metallicGloss = SAMPLE_TEXTURE2D(_MetallicGlossMap, sampler_MetallicGlossMap, input.uv);
                half metallic = metallicGloss.r * _Metallic;
                half smoothness = metallicGloss.a * _Smoothness;

                // URP lighting
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.fogCoord = input.fogFactor;

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = baseColor.rgb;
                surfaceData.metallic = metallic;
                surfaceData.smoothness = smoothness;
                surfaceData.normalTS = normalTS;
                surfaceData.occlusion = 1.0;
                surfaceData.emission = edgeMask * _DissolveEdgeColor.rgb;
                surfaceData.alpha = 1.0;

                half4 color = UniversalFragmentPBR(inputData, surfaceData);
                color.rgb = MixFog(color.rgb, input.fogFactor);
                return color;
            }
            ENDHLSL
        }

        // Shadow caster pass with dissolve clip
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_DissolveNoise); SAMPLER(sampler_DissolveNoise);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _BumpScale;
                half _Metallic;
                half _Smoothness;
                float _DissolveAmount;
                float4 _DissolveNoise_ST;
                float _DissolveEdgeWidth;
                half4 _DissolveEdgeColor;
                float4 _DissolveDirection;
                float _UseDirectional;
            CBUFFER_END

            struct ShadowAttributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
            };

            ShadowVaryings ShadowVert(ShadowAttributes input)
            {
                ShadowVaryings output;
                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.uv = input.uv * _BaseMap_ST.xy + _BaseMap_ST.zw;
                return output;
            }

            half4 ShadowFrag(ShadowVaryings input) : SV_Target
            {
                float noise = SAMPLE_TEXTURE2D(_DissolveNoise, sampler_DissolveNoise,
                    input.uv * _DissolveNoise_ST.xy + _DissolveNoise_ST.zw).r;

                float dissolveThreshold;
                if (_UseDirectional > 0.5)
                {
                    float3 dir = normalize(_DissolveDirection.xyz);
                    float height = dot(input.positionWS, dir);
                    float heightNorm = saturate(height * 0.5 + 0.5);
                    dissolveThreshold = heightNorm + noise * 0.3;
                }
                else
                {
                    dissolveThreshold = noise;
                }

                clip(step(_DissolveAmount, dissolveThreshold) - 0.001);
                return 0;
            }
            ENDHLSL
        }

        // Depth pass with dissolve clip
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_DissolveNoise); SAMPLER(sampler_DissolveNoise);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _BumpScale;
                half _Metallic;
                half _Smoothness;
                float _DissolveAmount;
                float4 _DissolveNoise_ST;
                float _DissolveEdgeWidth;
                half4 _DissolveEdgeColor;
                float4 _DissolveDirection;
                float _UseDirectional;
            CBUFFER_END

            struct ShadowAttributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
            };

            ShadowVaryings ShadowVert(ShadowAttributes input)
            {
                ShadowVaryings output;
                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.uv = input.uv * _BaseMap_ST.xy + _BaseMap_ST.zw;
                return output;
            }

            half4 ShadowFrag(ShadowVaryings input) : SV_Target
            {
                float noise = SAMPLE_TEXTURE2D(_DissolveNoise, sampler_DissolveNoise,
                    input.uv * _DissolveNoise_ST.xy + _DissolveNoise_ST.zw).r;

                float dissolveThreshold;
                if (_UseDirectional > 0.5)
                {
                    float3 dir = normalize(_DissolveDirection.xyz);
                    float height = dot(input.positionWS, dir);
                    float heightNorm = saturate(height * 0.5 + 0.5);
                    dissolveThreshold = heightNorm + noise * 0.3;
                }
                else
                {
                    dissolveThreshold = noise;
                }

                clip(step(_DissolveAmount, dissolveThreshold) - 0.001);
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
