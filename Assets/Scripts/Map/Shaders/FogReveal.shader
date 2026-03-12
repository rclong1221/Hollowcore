Shader "DIG/Map/FogReveal"
{
    // EPIC 17.6: Circle stamp shader for fog-of-war reveal.
    // Blits a white circle at _Center with _Radius onto an R8 render texture.
    // Uses Max blend mode so revealed areas stay revealed (monotonic).
    // Used by FogOfWarSystem via Graphics.Blit().

    Properties
    {
        _MainTex ("Fog Texture", 2D) = "black" {}
        _Center ("Circle Center UV", Vector) = (0.5, 0.5, 0, 0)
        _Radius ("Circle Radius UV", Float) = 0.05
        _Softness ("Edge Softness", Float) = 0.01
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
            Name "FogRevealStamp"

            // Max blend: dst = max(src, existing). Revealed pixels stay white.
            Blend One One
            BlendOp Max

            ZTest Always
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Center;
                float _Radius;
                float _Softness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Distance from circle center in UV space
                float2 delta = input.uv - _Center.xy;
                float dist = length(delta);

                // Soft circle: 1.0 inside radius, smooth falloff at edge
                float softEdge = max(_Softness, 0.001);
                float circle = 1.0 - saturate((dist - _Radius + softEdge) / softEdge);

                // Output white (revealed) modulated by circle mask
                return half4(circle, circle, circle, circle);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
