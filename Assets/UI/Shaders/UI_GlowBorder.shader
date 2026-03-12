Shader "DIG/UI/GlowBorder"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _GlowColor ("Glow Color", Color) = (0.2, 0.8, 1, 1)
        _GlowIntensity ("Glow Intensity", Range(0, 3)) = 1.5
        _GlowSize ("Glow Size", Range(0, 0.5)) = 0.1
        _BorderWidth ("Border Width", Range(0, 0.1)) = 0.02
        _CornerRadius ("Corner Radius", Range(0, 0.5)) = 0.1
        _PulseSpeed ("Pulse Speed", Range(0, 10)) = 2.0
        _PulseAmount ("Pulse Amount", Range(0, 1)) = 0.3
        _InnerColor ("Inner Color", Color) = (0, 0, 0, 0)
        
        // Stencil
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
            "Queue" = "Transparent" 
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
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
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            
            #include "UnityCG.cginc"
            
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };
            
            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };
            
            sampler2D _MainTex;
            float4 _GlowColor;
            float _GlowIntensity;
            float _GlowSize;
            float _BorderWidth;
            float _CornerRadius;
            float _PulseSpeed;
            float _PulseAmount;
            float4 _InnerColor;
            
            float roundedRectSDF(float2 p, float2 size, float radius)
            {
                float2 q = abs(p) - size + radius;
                return min(max(q.x, q.y), 0.0) + length(max(q, 0.0)) - radius;
            }
            
            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }
            
            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv - 0.5;
                
                // Rounded rectangle distance
                float dist = roundedRectSDF(uv, float2(0.5, 0.5), _CornerRadius);
                
                // Pulsing effect
                float pulse = 1.0 + sin(_Time.y * _PulseSpeed) * _PulseAmount;
                
                // Outer glow (exponential falloff)
                float outerGlow = exp(-dist / _GlowSize * 3.0) * _GlowIntensity * pulse;
                outerGlow = saturate(outerGlow);
                
                // Inner glow (inside the border)
                float innerGlow = exp(dist / _GlowSize * 2.0) * _GlowIntensity * 0.5 * pulse;
                innerGlow = saturate(innerGlow) * step(dist, 0);
                
                // Sharp border
                float border = smoothstep(0.0, -0.01, dist) - smoothstep(-_BorderWidth, -_BorderWidth - 0.01, dist);
                
                // Combine
                float4 color = float4(0, 0, 0, 0);
                
                // Inner fill
                float innerMask = smoothstep(0.0, -0.01, dist);
                color = lerp(color, _InnerColor, innerMask * _InnerColor.a);
                
                // Add inner glow
                color.rgb += _GlowColor.rgb * innerGlow;
                
                // Add border
                color = lerp(color, _GlowColor, border);
                
                // Add outer glow
                color.rgb += _GlowColor.rgb * outerGlow * (1.0 - innerMask);
                color.a = max(color.a, outerGlow * 0.8);
                
                return color * i.color;
            }
            ENDCG
        }
    }
    
    Fallback "UI/Default"
}
