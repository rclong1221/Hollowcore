Shader "DIG/UI/ProceduralHealthBar"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _FillAmount ("Fill Amount", Range(0, 1)) = 1.0
        _IsCritical ("Is Critical", Float) = 0.0
        _GlowIntensity ("Glow Intensity", Range(0, 2)) = 0.5
        _ShowShine ("Show Shine", Float) = 1.0
        
        _ColorFull ("Color Full", Color) = (0.2, 0.9, 0.3, 1)
        _ColorMid ("Color Mid", Color) = (0.9, 0.9, 0.2, 1)
        _ColorLow ("Color Low", Color) = (0.9, 0.3, 0.2, 1)
        _ColorCritical ("Color Critical", Color) = (1, 0.1, 0.1, 1)
        
        _BackgroundColor ("Background Color", Color) = (0.1, 0.1, 0.12, 0.8)
        _BorderColor ("Border Color", Color) = (0.4, 0.4, 0.45, 1)
        
        _CornerRadius ("Corner Radius", Range(0, 0.5)) = 0.15
        _BorderWidth ("Border Width", Range(0, 0.1)) = 0.02
        _ShineSpeed ("Shine Speed", Range(0, 5)) = 1.5
        _PulseSpeed ("Pulse Speed", Range(0, 10)) = 3.0
    }
    
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 100
        
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };
            
            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };
            
            sampler2D _MainTex;
            float _FillAmount;
            float _IsCritical;
            float _GlowIntensity;
            float _ShowShine;
            
            float4 _ColorFull;
            float4 _ColorMid;
            float4 _ColorLow;
            float4 _ColorCritical;
            float4 _BackgroundColor;
            float4 _BorderColor;
            
            float _CornerRadius;
            float _BorderWidth;
            float _ShineSpeed;
            float _PulseSpeed;
            
            // Signed distance function for rounded rectangle
            float sdRoundedBox(float2 p, float2 b, float r)
            {
                float2 q = abs(p) - b + r;
                return min(max(q.x, q.y), 0.0) + length(max(q, 0.0)) - r;
            }
            
            // Smooth step with configurable edge
            float smoothEdge(float edge, float width, float value)
            {
                return smoothstep(edge - width, edge + width, value);
            }
            
            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }
            
            float4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;
                float2 centeredUV = uv - 0.5;
                
                // Aspect ratio for proper rounded corners (assuming 8:1 ratio)
                float2 size = float2(0.5 - _CornerRadius * 0.125, 0.5 - _CornerRadius);
                float cornerR = _CornerRadius;
                
                // Calculate distances
                float distOuter = sdRoundedBox(centeredUV, size, cornerR);
                float distInner = sdRoundedBox(centeredUV, size - _BorderWidth, cornerR - _BorderWidth * 0.5);
                
                // Anti-aliased edges
                float pixelWidth = fwidth(distOuter);
                float outerMask = 1.0 - smoothstep(-pixelWidth, pixelWidth, distOuter);
                float innerMask = 1.0 - smoothstep(-pixelWidth, pixelWidth, distInner);
                float borderMask = outerMask * (1.0 - innerMask);
                
                // Fill mask (only fill up to _FillAmount)
                float fillMask = step(uv.x, _FillAmount) * innerMask;
                
                // Calculate fill color based on fill amount
                float4 fillColor;
                if (_FillAmount > 0.6)
                {
                    fillColor = lerp(_ColorMid, _ColorFull, (_FillAmount - 0.6) / 0.4);
                }
                else if (_FillAmount > 0.3)
                {
                    fillColor = lerp(_ColorLow, _ColorMid, (_FillAmount - 0.3) / 0.3);
                }
                else
                {
                    fillColor = lerp(_ColorCritical, _ColorLow, _FillAmount / 0.3);
                }
                
                // Critical state pulse
                if (_IsCritical > 0.5)
                {
                    float pulse = sin(_Time.y * _PulseSpeed) * 0.5 + 0.5;
                    fillColor = lerp(fillColor, _ColorCritical, pulse * 0.5);
                    fillColor.rgb *= 1.0 + pulse * 0.3;
                }
                
                // Shine effect (sweeping highlight)
                if (_ShowShine > 0.5)
                {
                    float shinePos = frac(_Time.y * _ShineSpeed * 0.1);
                    float shine = smoothstep(shinePos - 0.1, shinePos, uv.x) * 
                                  smoothstep(shinePos + 0.1, shinePos, uv.x);
                    shine *= step(uv.x, _FillAmount);
                    shine *= (1.0 - abs(uv.y - 0.5) * 2.0); // Fade at top/bottom
                    fillColor.rgb += shine * 0.4;
                }
                
                // Top highlight gradient
                float topHighlight = smoothstep(0.6, 0.9, uv.y) * 0.15;
                fillColor.rgb += topHighlight * fillMask;
                
                // Bottom shadow
                float bottomShadow = smoothstep(0.4, 0.1, uv.y) * 0.2;
                fillColor.rgb -= bottomShadow * fillMask;
                
                // Glow around filled area
                float glowDist = abs(uv.x - _FillAmount);
                float glow = exp(-glowDist * 20.0) * _GlowIntensity * step(0.01, _FillAmount);
                float4 glowColor = fillColor;
                glowColor.a *= glow * innerMask;
                
                // Combine layers
                float4 result = float4(0, 0, 0, 0);
                
                // Background
                result = lerp(result, _BackgroundColor, innerMask);
                
                // Fill
                result = lerp(result, fillColor, fillMask);
                
                // Add glow
                result.rgb += glowColor.rgb * glow * 0.5;
                
                // Border with subtle glow
                float4 borderColorFinal = _BorderColor;
                if (_IsCritical > 0.5)
                {
                    float pulse = sin(_Time.y * _PulseSpeed) * 0.5 + 0.5;
                    borderColorFinal = lerp(_BorderColor, _ColorCritical, pulse * 0.3);
                }
                result = lerp(result, borderColorFinal, borderMask);
                
                // Outer glow
                float outerGlow = exp(-distOuter * 30.0) * _GlowIntensity * 0.5;
                outerGlow *= (1.0 - outerMask);
                result.rgb += fillColor.rgb * outerGlow;
                result.a = max(result.a, outerGlow * 0.5);
                
                return result;
            }
            ENDCG
        }
    }
}
