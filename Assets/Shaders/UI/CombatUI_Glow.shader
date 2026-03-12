// ════════════════════════════════════════════════════════════════════════════════
// EPIC 15.9 · Combat UI Glow Shader
// URP-compatible UI shader with glow, pulse, and gradient effects
// ════════════════════════════════════════════════════════════════════════════════
Shader "DIG/UI/CombatUI_Glow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        
        [Header(Glow)]
        _GlowColor ("Glow Color", Color) = (1,1,1,1)
        _GlowIntensity ("Glow Intensity", Range(0, 5)) = 1.0
        _GlowWidth ("Glow Width", Range(0, 0.5)) = 0.1
        
        [Header(Pulse)]
        _PulseSpeed ("Pulse Speed", Range(0, 10)) = 2.0
        _PulseMinMax ("Pulse Min/Max", Vector) = (0.8, 1.2, 0, 0)
        
        [Header(Gradient)]
        _GradientStart ("Gradient Start", Color) = (1,0,0,1)
        _GradientEnd ("Gradient End", Color) = (0,1,0,1)
        _GradientAngle ("Gradient Angle", Range(0, 360)) = 0
        _UseGradient ("Use Gradient", Float) = 0
        
        [Header(Stencil)]
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255

        _ColorMask ("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
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
            Name "Default"
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

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
                float4 worldPosition : TEXCOORD1;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                float4 _GlowColor;
                float _GlowIntensity;
                float _GlowWidth;
                float _PulseSpeed;
                float4 _PulseMinMax;
                float4 _GradientStart;
                float4 _GradientEnd;
                float _GradientAngle;
                float _UseGradient;
                float4 _ClipRect;
                float4 _TextureSampleAdd;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.worldPosition = IN.positionOS;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color = IN.color * _Color;
                return OUT;
            }

            float4 frag(Varyings IN) : SV_Target
            {
                float4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                texColor += _TextureSampleAdd;
                
                float4 color = texColor * IN.color;
                
                // Gradient overlay
                if (_UseGradient > 0.5)
                {
                    float angle = _GradientAngle * 0.0174533; // deg to rad
                    float2 dir = float2(cos(angle), sin(angle));
                    float t = dot(IN.uv - 0.5, dir) + 0.5;
                    t = saturate(t);
                    float4 gradColor = lerp(_GradientStart, _GradientEnd, t);
                    color.rgb = gradColor.rgb * color.a;
                }
                
                // Pulse animation
                float pulse = lerp(_PulseMinMax.x, _PulseMinMax.y, 
                    (sin(_Time.y * _PulseSpeed) + 1.0) * 0.5);
                
                // Glow effect (edge detection via alpha gradient)
                float2 uvOffset = _GlowWidth * float2(1, 1);
                float alphaLeft = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv - float2(uvOffset.x, 0)).a;
                float alphaRight = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2(uvOffset.x, 0)).a;
                float alphaUp = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2(0, uvOffset.y)).a;
                float alphaDown = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv - float2(0, uvOffset.y)).a;
                
                float edge = saturate(abs(texColor.a - alphaLeft) + abs(texColor.a - alphaRight) +
                                       abs(texColor.a - alphaUp) + abs(texColor.a - alphaDown));
                
                float3 glow = _GlowColor.rgb * edge * _GlowIntensity * pulse;
                color.rgb += glow;
                
                #ifdef UNITY_UI_CLIP_RECT
                float2 inside = step(_ClipRect.xy, IN.worldPosition.xy) * step(IN.worldPosition.xy, _ClipRect.zw);
                color.a *= inside.x * inside.y;
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif

                return color;
            }
            ENDHLSL
        }
    }
    
    FallBack "UI/Default"
}
