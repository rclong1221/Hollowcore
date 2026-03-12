// ════════════════════════════════════════════════════════════════════════════════
// EPIC 15.9 · Combat UI Hitmarker Shader
// URP-compatible shader with scale animation, glow, and hit type effects
// ════════════════════════════════════════════════════════════════════════════════
Shader "DIG/UI/CombatUI_Hitmarker"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        
        [Header(Animation)]
        _AnimProgress ("Animation Progress", Range(0, 1)) = 0
        _ScaleMultiplier ("Scale Multiplier", Range(0.5, 3)) = 1.5
        
        [Header(Glow)]
        _GlowColor ("Glow Color", Color) = (1,1,1,1)
        _GlowIntensity ("Glow Intensity", Range(0, 5)) = 2.0
        _GlowRadius ("Glow Radius", Range(0, 0.5)) = 0.2
        
        [Header(Hit Type)]
        _IsCritical ("Is Critical", Float) = 0
        _IsKill ("Is Kill", Float) = 0
        _CriticalColor ("Critical Color", Color) = (1,0.8,0,1)
        _KillColor ("Kill Color", Color) = (1,0,0,1)
        
        [Header(Stencil)]
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255

        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
            "RenderPipeline"="UniversalPipeline"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Hitmarker"
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                float _AnimProgress;
                float _ScaleMultiplier;
                float4 _GlowColor;
                float _GlowIntensity;
                float _GlowRadius;
                float _IsCritical;
                float _IsKill;
                float4 _CriticalColor;
                float4 _KillColor;
            CBUFFER_END

            // Ease out elastic
            float easeOutElastic(float t)
            {
                float c4 = (2.0 * 3.14159) / 3.0;
                return t == 0.0 ? 0.0 : t == 1.0 ? 1.0 :
                    pow(2.0, -10.0 * t) * sin((t * 10.0 - 0.75) * c4) + 1.0;
            }
            
            // Ease out cubic
            float easeOutCubic(float t)
            {
                return 1.0 - pow(1.0 - t, 3.0);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                
                // Animate scale
                float scaleAnim = lerp(_ScaleMultiplier, 1.0, easeOutElastic(_AnimProgress));
                float3 scaled = IN.positionOS.xyz * scaleAnim;
                
                OUT.positionCS = TransformObjectToHClip(scaled);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color = IN.color * _Color;
                return OUT;
            }

            float4 frag(Varyings IN) : SV_Target
            {
                float4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                
                // Determine base color from hit type
                float4 baseColor = _Color;
                if (_IsKill > 0.5)
                    baseColor = _KillColor;
                else if (_IsCritical > 0.5)
                    baseColor = _CriticalColor;
                
                float4 color = texColor * baseColor * IN.color;
                
                // Fade out over animation
                color.a *= 1.0 - easeOutCubic(_AnimProgress);
                
                // Glow effect
                float2 center = IN.uv - 0.5;
                float dist = length(center);
                float glow = exp(-dist / _GlowRadius) * _GlowIntensity;
                glow *= texColor.a; // Only glow where there's content
                glow *= (1.0 - _AnimProgress); // Fade glow with animation
                
                float4 glowColor = _GlowColor;
                if (_IsKill > 0.5)
                    glowColor = _KillColor;
                else if (_IsCritical > 0.5)
                    glowColor = _CriticalColor;
                
                color.rgb += glowColor.rgb * glow;
                
                return color;
            }
            ENDHLSL
        }
    }
    
    FallBack "UI/Default"
}
