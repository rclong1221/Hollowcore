Shader "DIG/Map/MinimapFogOverlay"
{
    // EPIC 17.6: Composites fog-of-war texture over the minimap render texture.
    // Unexplored areas (fog R=0) show as dark overlay; explored areas (fog R=1) are transparent.
    // Used on the fog overlay RawImage in MinimapView / WorldMapView.

    Properties
    {
        _MainTex ("Fog Texture (R8)", 2D) = "black" {}
        _UnexploredColor ("Unexplored Color", Color) = (0, 0, 0, 0.85)
        _ExploredColor ("Explored Color", Color) = (0, 0, 0, 0)
        _EdgeSoftness ("Fog Edge Softness", Range(0, 0.2)) = 0.05
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
        }

        Pass
        {
            Name "FogOverlay"

            Blend SrcAlpha OneMinusSrcAlpha
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
                half4 _UnexploredColor;
                half4 _ExploredColor;
                float _EdgeSoftness;
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
                // Sample fog reveal value (R channel: 0=unexplored, 1=explored)
                half fogReveal = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).r;

                // Smooth the transition edge
                half softEdge = max(_EdgeSoftness, 0.001);
                half reveal = smoothstep(0.0, softEdge, fogReveal);

                // Interpolate between unexplored (dark) and explored (transparent)
                half4 color = lerp(_UnexploredColor, _ExploredColor, reveal);

                return color;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
