// ════════════════════════════════════════════════════════════════════════════════
// EPIC 15.9 · Combat UI Health Bar Shader
// URP-compatible horizontal fill with trail, gradient, and glow effects
// ════════════════════════════════════════════════════════════════════════════════
Shader "DIG/UI/CombatUI_HealthBar"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        [PerRendererData] _Color ("Tint", Color) = (1,1,1,1)
        
        [Header(Fill)]
        [PerRendererData] _FillAmount ("Fill Amount", Range(0, 1)) = 1.0
        [PerRendererData] _TrailAmount ("Trail Amount", Range(0, 1)) = 1.0
        
        [Header(Colors)]
        _HealthyColor ("Healthy Color (100%)", Color) = (0,1,0,1)
        _DamagedColor ("Damaged Color (50%)", Color) = (1,1,0,1)
        _CriticalColor ("Critical Color (0%)", Color) = (1,0,0,1)
        _TrailColor ("Trail Color", Color) = (1,0.3,0.3,0.8)
        _BackgroundColor ("Background Color", Color) = (0.1,0.1,0.1,0.8)
        
        [Header(Glow)]
        _GlowColor ("Glow Color", Color) = (1,1,1,0.5)
        _GlowIntensity ("Glow Intensity", Range(0, 3)) = 0.5
        _GlowWidth ("Glow Width", Range(0, 0.1)) = 0.02
        
        [Header(Border)]
        _BorderColor ("Border Color", Color) = (0,0,0,1)
        _BorderWidth ("Border Width", Range(0, 0.1)) = 0.02
        _CornerRadius ("Corner Radius", Range(0, 0.5)) = 0.1
        
        [Header(Animation)]
        [PerRendererData] _DamageFlash ("Damage Flash", Range(0, 1)) = 0
        _LowHealthPulse ("Low Health Pulse", Range(0, 1)) = 0
        
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
            Name "HealthBar"
            
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
                float4 _HealthyColor;
                float4 _DamagedColor;
                float4 _CriticalColor;
                float4 _TrailColor;
                float4 _BackgroundColor;
                float4 _GlowColor;
                float _GlowIntensity;
                float _GlowWidth;
                float4 _BorderColor;
                float _BorderWidth;
                float _CornerRadius;
                float _LowHealthPulse;
            CBUFFER_END

            // Per-renderer properties (set via MaterialPropertyBlock, outside CBUFFER for SRP Batcher compatibility)
            float4 _Color;
            float _FillAmount;
            float _TrailAmount;
            float _DamageFlash;

            // Rounded rectangle SDF
            float roundedBoxSDF(float2 p, float2 b, float r)
            {
                float2 q = abs(p) - b + r;
                return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r;
            }

            float3 getHealthColor(float fill)
            {
                float3 color;
                if (fill > 0.5)
                {
                    float t = (fill - 0.5) * 2.0;
                    color = lerp(_DamagedColor.rgb, _HealthyColor.rgb, t);
                }
                else
                {
                    float t = fill * 2.0;
                    color = lerp(_CriticalColor.rgb, _DamagedColor.rgb, t);
                }
                return color;
            }

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
                float2 uv = IN.uv;
                float2 p = uv - 0.5;
                
                // Bar dimensions (assume aspect ratio ~4:1)
                float2 boxSize = float2(0.5 - _BorderWidth, 0.5 - _BorderWidth * 4.0);
                
                // Rounded box mask
                float dist = roundedBoxSDF(p, boxSize, _CornerRadius);
                float boxMask = 1.0 - smoothstep(0.0, 0.01, dist);
                
                // Border
                float borderDist = roundedBoxSDF(p, boxSize + _BorderWidth, _CornerRadius);
                float borderMask = (1.0 - smoothstep(0.0, 0.01, borderDist)) * (1.0 - boxMask);
                
                // Fill masks
                float fillMask = step(uv.x, _FillAmount);
                float trailMask = step(uv.x, _TrailAmount) * (1.0 - fillMask);
                float bgMask = 1.0 - fillMask - trailMask;
                
                // Colors
                float3 healthColor = getHealthColor(_FillAmount);
                
                // Low health pulse
                if (_LowHealthPulse > 0.5 && _FillAmount < 0.25)
                {
                    float pulse = 0.7 + 0.3 * sin(_Time.y * 8.0);
                    healthColor *= pulse;
                }
                
                // Damage flash
                healthColor = lerp(healthColor, float3(1, 1, 1), _DamageFlash);
                
                float4 fillColor = float4(healthColor, 1.0) * fillMask;
                float4 trailColor = _TrailColor * trailMask;
                float4 bgColor = _BackgroundColor * bgMask;
                
                float4 barColor = (fillColor + trailColor + bgColor) * boxMask;
                float4 border = _BorderColor * borderMask;
                
                // Glow at fill edge
                float edgeDist = abs(uv.x - _FillAmount);
                float glow = exp(-edgeDist / _GlowWidth) * _GlowIntensity * boxMask;
                barColor.rgb += _GlowColor.rgb * glow;
                
                float4 color = barColor + border;
                color *= IN.color;
                color.a *= (boxMask + borderMask);
                
                return color;
            }
            ENDHLSL
        }
    }
    
    FallBack "UI/Default"
}
