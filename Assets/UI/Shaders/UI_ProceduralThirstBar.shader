Shader "DIG/UI/ProceduralThirstBar"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _FillAmount ("Fill Amount", Range(0, 1)) = 1.0
        _IsDehydrated ("Is Dehydrated", Float) = 0.0
        
        _ColorFull ("Color Full (Hydrated)", Color) = (0.3, 0.7, 0.95, 1)
        _ColorMid ("Color Mid", Color) = (0.25, 0.5, 0.8, 1)
        _ColorLow ("Color Low (Thirsty)", Color) = (0.2, 0.35, 0.6, 1)
        _ColorDehydrated ("Color Dehydrated", Color) = (0.5, 0.3, 0.2, 1)
        
        _BackgroundColor ("Background Color", Color) = (0.05, 0.08, 0.12, 0.85)
        _BorderColor ("Border Color", Color) = (0.25, 0.45, 0.7, 1)
        
        _CornerRadius ("Corner Radius", Range(0, 0.5)) = 0.12
        _BorderWidth ("Border Width", Range(0, 0.1)) = 0.018
        _WaveSpeed ("Wave Speed", Range(0, 5)) = 1.0
        _WaveAmplitude ("Wave Amplitude", Range(0, 0.05)) = 0.02
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
            
            float _FillAmount, _IsDehydrated;
            float4 _ColorFull, _ColorMid, _ColorLow, _ColorDehydrated;
            float4 _BackgroundColor, _BorderColor;
            float _CornerRadius, _BorderWidth, _WaveSpeed, _WaveAmplitude;
            
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
                float2 centeredUV = uv - 0.5;
                
                float2 size = float2(0.5 - _CornerRadius * 0.125, 0.5 - _CornerRadius);
                float distOuter = sdRoundedBox(centeredUV, size, _CornerRadius);
                float distInner = sdRoundedBox(centeredUV, size - _BorderWidth, _CornerRadius - _BorderWidth * 0.5);
                
                float pixelWidth = fwidth(distOuter);
                float outerMask = 1.0 - smoothstep(-pixelWidth, pixelWidth, distOuter);
                float innerMask = 1.0 - smoothstep(-pixelWidth, pixelWidth, distInner);
                float borderMask = outerMask * (1.0 - innerMask);
                
                // Water wave effect on fill edge
                float wave = sin(uv.y * 30.0 + _Time.y * _WaveSpeed * 5.0) * _WaveAmplitude * (1.0 - _IsDehydrated);
                float fillEdge = _FillAmount + wave;
                float fillMask = step(uv.x, fillEdge) * innerMask;
                
                // Color gradient
                float4 fillColor;
                if (_FillAmount > 0.6) fillColor = lerp(_ColorMid, _ColorFull, (_FillAmount - 0.6) / 0.4);
                else if (_FillAmount > 0.3) fillColor = lerp(_ColorLow, _ColorMid, (_FillAmount - 0.3) / 0.3);
                else fillColor = lerp(_ColorDehydrated, _ColorLow, _FillAmount / 0.3);
                
                // Shimmer
                float shimmer = sin(uv.x * 20.0 - _Time.y * 3.0) * 0.1 + 0.9;
                fillColor.rgb *= shimmer;
                
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
