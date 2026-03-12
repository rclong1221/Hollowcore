// ════════════════════════════════════════════════════════════════════════════════
// EPIC 15.9 · Combat UI Radial Fill Shader
// URP-compatible shader for smooth radial progress bars with glow
// ════════════════════════════════════════════════════════════════════════════════
Shader "DIG/UI/CombatUI_RadialFill"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        
        [Header(Radial Fill)]
        _FillAmount ("Fill Amount", Range(0, 1)) = 1.0
        _FillAngle ("Start Angle", Range(0, 360)) = 0
        _FillDirection ("Fill Direction", Float) = 1 // 1 = CW, -1 = CCW
        
        [Header(Ring)]
        _InnerRadius ("Inner Radius", Range(0, 1)) = 0.3
        _OuterRadius ("Outer Radius", Range(0, 1)) = 0.5
        _Smoothness ("Edge Smoothness", Range(0, 0.1)) = 0.01
        
        [Header(Colors)]
        _FillColor ("Fill Color", Color) = (0,1,0,1)
        _BackgroundColor ("Background Color", Color) = (0.2,0.2,0.2,0.5)
        _GlowColor ("Glow Color", Color) = (1,1,1,0.5)
        _GlowIntensity ("Glow Intensity", Range(0, 3)) = 1.0
        
        [Header(Animation)]
        _PulseSpeed ("Pulse Speed", Range(0, 10)) = 0
        _RotationSpeed ("Rotation Speed", Range(-180, 180)) = 0
        
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
            Name "RadialFill"
            
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

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _FillAmount;
                float _FillAngle;
                float _FillDirection;
                float _InnerRadius;
                float _OuterRadius;
                float _Smoothness;
                float4 _FillColor;
                float4 _BackgroundColor;
                float4 _GlowColor;
                float _GlowIntensity;
                float _PulseSpeed;
                float _RotationSpeed;
            CBUFFER_END

            #define PI 3.14159265359
            #define TAU 6.28318530718

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                OUT.color = IN.color * _Color;
                return OUT;
            }

            float4 frag(Varyings IN) : SV_Target
            {
                float2 uv = IN.uv - 0.5; // Center UV
                
                // Apply rotation
                float rot = _RotationSpeed * _Time.y * 0.0174533;
                float cosRot = cos(rot);
                float sinRot = sin(rot);
                uv = float2(uv.x * cosRot - uv.y * sinRot, uv.x * sinRot + uv.y * cosRot);
                
                float dist = length(uv);
                float angle = atan2(uv.y, uv.x); // -PI to PI
                angle = angle + PI; // 0 to TAU
                
                // Apply start angle offset
                float startAngle = _FillAngle * 0.0174533;
                angle = fmod(angle - startAngle + TAU, TAU);
                
                // Flip direction if needed
                if (_FillDirection < 0)
                    angle = TAU - angle;
                
                // Ring mask
                float ringMask = smoothstep(_InnerRadius - _Smoothness, _InnerRadius + _Smoothness, dist);
                ringMask *= 1.0 - smoothstep(_OuterRadius - _Smoothness, _OuterRadius + _Smoothness, dist);
                
                // Fill mask
                float fillAngle = _FillAmount * TAU;
                float fillMask = smoothstep(fillAngle + _Smoothness, fillAngle - _Smoothness, angle);
                
                // Pulse
                float pulse = 1.0;
                if (_PulseSpeed > 0)
                {
                    pulse = 0.8 + 0.2 * sin(_Time.y * _PulseSpeed);
                }
                
                // Colors
                float4 fill = _FillColor * fillMask;
                float4 bg = _BackgroundColor * (1.0 - fillMask);
                float4 color = (fill + bg) * ringMask;
                
                // Glow at fill edge
                float edgeDist = abs(angle - fillAngle);
                float glow = exp(-edgeDist * 10.0) * _GlowIntensity * ringMask * pulse;
                color.rgb += _GlowColor.rgb * glow;
                
                color *= IN.color;
                color.a *= ringMask;
                
                return color;
            }
            ENDHLSL
        }
    }
    
    FallBack "UI/Default"
}
