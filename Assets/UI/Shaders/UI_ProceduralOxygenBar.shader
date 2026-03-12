Shader "DIG/UI/ProceduralOxygenBar"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _FillAmount ("Fill Amount", Range(0, 1)) = 1.0
        _IsSuffocating ("Is Suffocating", Float) = 0.0
        _IsRecovering ("Is Recovering", Float) = 0.0
        
        _ColorFull ("Color Full", Color) = (0.4, 0.8, 1.0, 1)
        _ColorMid ("Color Mid", Color) = (0.3, 0.6, 0.9, 1)
        _ColorLow ("Color Low", Color) = (0.2, 0.3, 0.8, 1)
        _ColorSuffocate ("Color Suffocate", Color) = (0.6, 0.2, 0.3, 1)
        
        _BackgroundColor ("Background Color", Color) = (0.05, 0.08, 0.12, 0.85)
        _BorderColor ("Border Color", Color) = (0.3, 0.5, 0.7, 1)
        
        _CornerRadius ("Corner Radius", Range(0, 0.5)) = 0.15
        _BorderWidth ("Border Width", Range(0, 0.1)) = 0.018
        _BubbleSpeed ("Bubble Speed", Range(0, 5)) = 1.5
        _PulseSpeed ("Pulse Speed", Range(0, 10)) = 4.0
    }
    
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float2 uv : TEXCOORD0; float4 vertex : SV_POSITION; };
            
            float _FillAmount, _IsSuffocating, _IsRecovering;
            float4 _ColorFull, _ColorMid, _ColorLow, _ColorSuffocate;
            float4 _BackgroundColor, _BorderColor;
            float _CornerRadius, _BorderWidth, _BubbleSpeed, _PulseSpeed;
            
            float sdRoundedBox(float2 p, float2 b, float r)
            {
                float2 q = abs(p) - b + r;
                return min(max(q.x, q.y), 0.0) + length(max(q, 0.0)) - r;
            }
            
            float hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            
            // Bubble effect
            float bubbles(float2 uv, float time)
            {
                float b = 0.0;
                for (int i = 0; i < 5; i++)
                {
                    float2 pos = float2(hash(float2(i, 0)) * 0.8 + 0.1, frac(time * _BubbleSpeed * (0.5 + hash(float2(i, 1)) * 0.5) + hash(float2(i, 2))));
                    float size = 0.02 + hash(float2(i, 3)) * 0.02;
                    float d = length(uv - pos);
                    b += smoothstep(size, size * 0.5, d) * 0.3;
                }
                return b;
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
                
                float2 size = float2(0.5 - _CornerRadius * 0.125, 0.5 - _CornerRadius);
                float distOuter = sdRoundedBox(centeredUV, size, _CornerRadius);
                float distInner = sdRoundedBox(centeredUV, size - _BorderWidth, _CornerRadius - _BorderWidth * 0.5);
                
                float pixelWidth = fwidth(distOuter);
                float outerMask = 1.0 - smoothstep(-pixelWidth, pixelWidth, distOuter);
                float innerMask = 1.0 - smoothstep(-pixelWidth, pixelWidth, distInner);
                float borderMask = outerMask * (1.0 - innerMask);
                float fillMask = step(uv.x, _FillAmount) * innerMask;
                
                // Color based on fill
                float4 fillColor;
                if (_FillAmount > 0.5) fillColor = lerp(_ColorMid, _ColorFull, (_FillAmount - 0.5) * 2.0);
                else if (_FillAmount > 0.2) fillColor = lerp(_ColorLow, _ColorMid, (_FillAmount - 0.2) / 0.3);
                else fillColor = lerp(_ColorSuffocate, _ColorLow, _FillAmount / 0.2);
                
                // Bubbles
                float bub = bubbles(uv, _Time.y) * fillMask;
                fillColor.rgb += bub * 0.5;
                
                // Suffocation pulse
                float pulse = sin(_Time.y * _PulseSpeed) * 0.3 + 0.7;
                fillColor.rgb *= lerp(1.0, pulse, _IsSuffocating);
                
                // Compose
                float4 finalColor = float4(0, 0, 0, 0);
                finalColor = lerp(finalColor, _BackgroundColor, innerMask * (1.0 - fillMask) * _BackgroundColor.a);
                finalColor = lerp(finalColor, fillColor, fillMask);
                finalColor = lerp(finalColor, _BorderColor, borderMask);
                finalColor.a *= outerMask;
                
                return finalColor;
            }
            ENDCG
        }
    }
    FallBack "UI/Default"
}
