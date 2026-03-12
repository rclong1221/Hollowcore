Shader "DIG/UI/GradientBar"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _FillAmount ("Fill Amount", Range(0, 1)) = 1.0
        
        // Gradient colors (left to right as health decreases)
        _ColorFull ("Color Full (100%)", Color) = (0.18, 0.8, 0.44, 1)
        _ColorHigh ("Color High (75%)", Color) = (0.15, 0.68, 0.38, 1)
        _ColorMid ("Color Mid (50%)", Color) = (0.95, 0.77, 0.06, 1)
        _ColorLow ("Color Low (25%)", Color) = (0.9, 0.49, 0.13, 1)
        _ColorCritical ("Color Critical (0%)", Color) = (0.91, 0.3, 0.24, 1)
        
        // Effects
        _GlowIntensity ("Glow Intensity", Range(0, 2)) = 0.5
        _GlowColor ("Glow Color", Color) = (1, 1, 1, 0.5)
        _PulseSpeed ("Pulse Speed", Range(0, 10)) = 2.0
        _PulseAmount ("Pulse Amount", Range(0, 0.5)) = 0.1
        _IsCritical ("Is Critical", Range(0, 1)) = 0
        
        // Shine effect
        _ShineSpeed ("Shine Speed", Range(0, 5)) = 1.0
        _ShineWidth ("Shine Width", Range(0, 0.5)) = 0.1
        _ShineIntensity ("Shine Intensity", Range(0, 1)) = 0.3
        
        // Border
        _BorderColor ("Border Color", Color) = (0, 0, 0, 0.5)
        _BorderWidth ("Border Width", Range(0, 0.1)) = 0.02
        _CornerRadius ("Corner Radius", Range(0, 0.5)) = 0.1
        
        // Background
        _BackgroundColor ("Background Color", Color) = (0, 0, 0, 0.7)
        
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
            float _FillAmount;
            float4 _ColorFull;
            float4 _ColorHigh;
            float4 _ColorMid;
            float4 _ColorLow;
            float4 _ColorCritical;
            float _GlowIntensity;
            float4 _GlowColor;
            float _PulseSpeed;
            float _PulseAmount;
            float _IsCritical;
            float _ShineSpeed;
            float _ShineWidth;
            float _ShineIntensity;
            float4 _BorderColor;
            float _BorderWidth;
            float _CornerRadius;
            float4 _BackgroundColor;
            
            // Rounded rectangle SDF
            float roundedRectSDF(float2 p, float2 size, float radius)
            {
                float2 q = abs(p) - size + radius;
                return min(max(q.x, q.y), 0.0) + length(max(q, 0.0)) - radius;
            }
            
            // 5-color gradient based on fill amount
            float4 getHealthColor(float fill)
            {
                if (fill > 0.75)
                    return lerp(_ColorHigh, _ColorFull, (fill - 0.75) * 4.0);
                else if (fill > 0.5)
                    return lerp(_ColorMid, _ColorHigh, (fill - 0.5) * 4.0);
                else if (fill > 0.25)
                    return lerp(_ColorLow, _ColorMid, (fill - 0.25) * 4.0);
                else
                    return lerp(_ColorCritical, _ColorLow, fill * 4.0);
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
                float2 uv = i.uv;
                float2 rectUV = uv - 0.5;
                
                // Rounded rectangle
                float dist = roundedRectSDF(rectUV, float2(0.5, 0.5), _CornerRadius);
                
                // Background
                float4 color = _BackgroundColor;
                
                // Fill area (with slight padding for visual appeal)
                float fillPadding = 0.02;
                float fillDist = roundedRectSDF(rectUV, float2(0.5 - fillPadding, 0.5 - fillPadding), _CornerRadius * 0.8);
                
                // Check if in fill area
                float inFillArea = smoothstep(0.01, -0.01, fillDist);
                float inFilled = step(uv.x, _FillAmount);
                
                // Get gradient color based on fill amount
                float4 fillColor = getHealthColor(_FillAmount);
                
                // Apply pulse effect when critical
                float pulse = 1.0 + sin(_Time.y * _PulseSpeed) * _PulseAmount * _IsCritical;
                fillColor.rgb *= pulse;
                
                // Shine effect (sweeping highlight)
                float shinePos = frac(_Time.y * _ShineSpeed);
                float shine = smoothstep(shinePos - _ShineWidth, shinePos, uv.x) * 
                              smoothstep(shinePos + _ShineWidth, shinePos, uv.x);
                fillColor.rgb += shine * _ShineIntensity * inFilled;
                
                // Vertical gradient for depth (lighter at top)
                float verticalGradient = lerp(0.8, 1.2, uv.y);
                fillColor.rgb *= verticalGradient;
                
                // Combine fill with background
                color = lerp(color, fillColor, inFillArea * inFilled);
                
                // Add glow at the edge of fill
                float edgeDist = abs(uv.x - _FillAmount);
                float edgeGlow = smoothstep(0.05, 0.0, edgeDist) * _GlowIntensity * inFillArea;
                color.rgb += _GlowColor.rgb * edgeGlow * (1.0 + _IsCritical);
                
                // Border
                float border = smoothstep(0.0, -0.01, dist) - smoothstep(-_BorderWidth, -_BorderWidth - 0.01, dist);
                color = lerp(color, _BorderColor, border * _BorderColor.a);
                
                // Alpha mask
                color.a *= smoothstep(0.01, -0.01, dist);
                
                return color * i.color;
            }
            ENDCG
        }
    }
    
    Fallback "UI/Default"
}
