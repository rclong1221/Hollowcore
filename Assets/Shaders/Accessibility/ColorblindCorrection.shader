Shader "DIG/Accessibility/ColorblindCorrection"
{
    Properties
    {
        [HideInInspector] _MainTex ("Source", 2D) = "white" {}
        [HideInInspector] _Intensity ("Intensity", Range(0, 1)) = 1.0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        LOD 100

        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "ColorblindCorrection"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4x4 _ColorMatrix;
                float _Intensity;
            CBUFFER_END

            float4 Frag(Varyings input) : SV_Target
            {
                float4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord);

                // Apply Daltonization color matrix
                float3 corrected;
                corrected.r = dot(color.rgb, _ColorMatrix[0].rgb);
                corrected.g = dot(color.rgb, _ColorMatrix[1].rgb);
                corrected.b = dot(color.rgb, _ColorMatrix[2].rgb);

                // Lerp by intensity
                color.rgb = lerp(color.rgb, saturate(corrected), _Intensity);

                return color;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
