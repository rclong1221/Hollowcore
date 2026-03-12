Shader "DIG/VoxelTriplanarMultiMaterial"
{
    Properties
    {
        [Header(Material Textures)]
        _DirtTex ("Dirt Texture", 2D) = "white" {}
        _StoneTex ("Stone Texture", 2D) = "gray" {}
        _OreTex ("Ore Texture", 2D) = "white" {}
        
        [Header(Normal Maps)]
        _DirtNormal ("Dirt Normal", 2D) = "bump" {}
        _StoneNormal ("Stone Normal", 2D) = "bump" {}
        _OreNormal ("Ore Normal", 2D) = "bump" {}
        
        [Header(Material Colors)]
        _DirtColor ("Dirt Tint", Color) = (0.6, 0.45, 0.3, 1)
        _StoneColor ("Stone Tint", Color) = (0.5, 0.5, 0.5, 1)
        _OreColor ("Ore Tint", Color) = (0.8, 0.7, 0.2, 1)
        
        [Header(Triplanar Settings)]
        _Scale ("Texture Scale", Float) = 0.25
        _BlendSharpness ("Blend Sharpness", Range(1, 8)) = 4.0
        
        [Header(Surface Properties)]
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
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 color : COLOR; // R = material ID (0=dirt, 0.5=stone, 1=ore)
            };
            
            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float materialBlend : TEXCOORD2; // Material ID from vertex color
                float fogFactor : TEXCOORD3;
            };
            
            TEXTURE2D(_DirtTex);
            SAMPLER(sampler_DirtTex);
            TEXTURE2D(_StoneTex);
            SAMPLER(sampler_StoneTex);
            TEXTURE2D(_OreTex);
            SAMPLER(sampler_OreTex);
            
            CBUFFER_START(UnityPerMaterial)
                float _Scale;
                float _BlendSharpness;
                float4 _DirtColor;
                float4 _StoneColor;
                float4 _OreColor;
                float _Smoothness;
                float _Metallic;
            CBUFFER_END
            
            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                
                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs normInputs = GetVertexNormalInputs(IN.normalOS);
                
                OUT.positionHCS = posInputs.positionCS;
                OUT.worldPos = posInputs.positionWS;
                OUT.worldNormal = normInputs.normalWS;
                OUT.materialBlend = IN.color.r;
                OUT.fogFactor = ComputeFogFactor(posInputs.positionCS.z);
                
                return OUT;
            }
            
            float3 GetTriplanarBlending(float3 worldNormal)
            {
                float3 blending = pow(abs(worldNormal), _BlendSharpness);
                blending /= (blending.x + blending.y + blending.z + 0.001);
                return blending;
            }
            
            half4 TriplanarSample(TEXTURE2D_PARAM(tex, samp), float3 worldPos, float3 blending)
            {
                float2 uvX = worldPos.zy * _Scale;
                float2 uvY = worldPos.xz * _Scale;
                float2 uvZ = worldPos.xy * _Scale;
                
                half4 xSample = SAMPLE_TEXTURE2D(tex, samp, uvX);
                half4 ySample = SAMPLE_TEXTURE2D(tex, samp, uvY);
                half4 zSample = SAMPLE_TEXTURE2D(tex, samp, uvZ);
                
                return xSample * blending.x + ySample * blending.y + zSample * blending.z;
            }
            
            half4 frag(Varyings IN) : SV_Target
            {
                float3 worldNormal = normalize(IN.worldNormal);
                float3 blending = GetTriplanarBlending(worldNormal);
                
                // Sample all materials
                half4 dirtSample = TriplanarSample(TEXTURE2D_ARGS(_DirtTex, sampler_DirtTex), IN.worldPos, blending) * _DirtColor;
                half4 stoneSample = TriplanarSample(TEXTURE2D_ARGS(_StoneTex, sampler_StoneTex), IN.worldPos, blending) * _StoneColor;
                half4 oreSample = TriplanarSample(TEXTURE2D_ARGS(_OreTex, sampler_OreTex), IN.worldPos, blending) * _OreColor;
                
                // Blend materials based on material ID
                // 0 = dirt, 0.5 = stone, 1 = ore
                float matID = IN.materialBlend;
                half4 albedo;
                
                if (matID < 0.25)
                {
                    // Dirt
                    albedo = dirtSample;
                }
                else if (matID < 0.75)
                {
                    // Stone (blend between dirt and stone at edges)
                    float t = saturate((matID - 0.25) * 2.0);
                    albedo = lerp(dirtSample, stoneSample, t);
                }
                else
                {
                    // Ore (blend between stone and ore at edges)
                    float t = saturate((matID - 0.75) * 4.0);
                    albedo = lerp(stoneSample, oreSample, t);
                }
                
                // Setup lighting
                InputData inputData = (InputData)0;
                inputData.positionWS = IN.worldPos;
                inputData.normalWS = worldNormal;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(IN.worldPos);
                inputData.shadowCoord = TransformWorldToShadowCoord(IN.worldPos);
                inputData.fogCoord = IN.fogFactor;
                inputData.bakedGI = SampleSH(worldNormal);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.positionHCS);
                inputData.shadowMask = half4(1, 1, 1, 1);
                
                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = albedo.rgb;
                surfaceData.metallic = _Metallic;
                surfaceData.smoothness = _Smoothness;
                surfaceData.normalTS = float3(0, 0, 1);
                surfaceData.occlusion = 1.0;
                surfaceData.alpha = 1.0;
                
                half4 color = UniversalFragmentPBR(inputData, surfaceData);
                color.rgb = MixFog(color.rgb, IN.fogFactor);
                
                return color;
            }
            ENDHLSL
        }
        
        // Shadow caster pass (same as VoxelTriplanar)
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
    }
    
    FallBack "DIG/VoxelTriplanar"
}
