Shader "DIG/UI/ProceduralHungerBar"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _FillAmount ("Fill Amount", Range(0, 1)) = 1.0
        _IsStarving ("Is Starving", Float) = 0.0
        
        _ColorFull ("Color Full (Satisfied)", Color) = (0.9, 0.7, 0.3, 1)
        _ColorMid ("Color Mid", Color) = (0.8, 0.5, 0.2, 1)
        _ColorLow ("Color Low (Hungry)", Color) = (0.6, 0.3, 0.1, 1)
        _ColorStarving ("Color Starving", Color) = (0.5, 0.15, 0.1, 1)
        
        _BackgroundColor ("Background Color", Color) = (0.1, 0.08, 0.05, 0.85)
        _BorderColor ("Border Color", Color) = (0.5, 0.35, 0.2, 1)
        
        _CornerRadius ("Corner Radius", Range(0, 0.5)) = 0.12
        _BorderWidth ("Border Width", Range(0, 0.1)) = 0.018
        _ShakeIntensity ("Shake Intensity", Range(0, 0.05)) = 0.01
        _PulseSpeed ("Pulse Speed", Range(0, 10)) = 3.0
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
            
            float _FillAmount, _IsStarving;
            float4 _ColorFull, _ColorMid, _ColorLow, _ColorStarving;
            float4 _BackgroundColor, _BorderColor;
            float _CornerRadius, _BorderWidth, _ShakeIntensity, _PulseSpeed;
            
            float sdRoundedBox(float2 p, float2 b, float r)
            {
                float2 q = abs(p) - b + r;
                return min(max(q.x, q.y), 0.0) + length(max(q, 0.0)) - r;
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
                
                // Stomach growl shake when starving
                float shake = sin(_Time.y * 20.0) * _ShakeIntensity * _IsStarving;
                uv.x += shake;
                
                float2 centeredUV = uv - 0.5;
                
                float2 size = float2(0.5 - _CornerRadius * 0.125, 0.5 - _CornerRadius);
                float distOuter = sdRoundedBox(centeredUV, size, _CornerRadius);
                float distInner = sdRoundedBox(centeredUV, size - _BorderWidth, _CornerRadius - _BorderWidth * 0.5);
                
                float pixelWidth = fwidth(distOuter);
                float outerMask = 1.0 - smoothstep(-pixelWidth, pixelWidth, distOuter);
                float innerMask = 1.0 - smoothstep(-pixelWidth, pixelWidth, distInner);
                float borderMask = outerMask * (1.0 - innerMask);
                float fillMask = step(uv.x, _FillAmount) * innerMask;
                
                // Color gradient (inverted - full = satisfied, empty = starving)
                float4 fillColor;
                if (_FillAmount > 0.6) fillColor = lerp(_ColorMid, _ColorFull, (_FillAmount - 0.6) / 0.4);
                else if (_FillAmount > 0.3) fillColor = lerp(_ColorLow, _ColorMid, (_FillAmount - 0.3) / 0.3);
                else fillColor = lerp(_ColorStarving, _ColorLow, _FillAmount / 0.3);
                
                // Starving pulse
                float pulse = sin(_Time.y * _PulseSpeed) * 0.2 + 0.8;
                fillColor.rgb *= lerp(1.0, pulse, _IsStarving);
                
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
