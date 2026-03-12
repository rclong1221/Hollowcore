Shader "DIG/UI/ProceduralShieldBar"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _FillAmount ("Fill Amount", Range(0, 1)) = 1.0
        _IsRecharging ("Is Recharging", Float) = 0.0
        _IsDepleted ("Is Depleted", Float) = 0.0
        
        _ColorFull ("Color Full", Color) = (0.3, 0.6, 1.0, 1)
        _ColorMid ("Color Mid", Color) = (0.2, 0.5, 0.9, 1)
        _ColorLow ("Color Low", Color) = (0.15, 0.3, 0.7, 1)
        _ColorRecharge ("Color Recharge", Color) = (0.5, 0.8, 1.0, 1)
        
        _BackgroundColor ("Background Color", Color) = (0.05, 0.1, 0.15, 0.8)
        _BorderColor ("Border Color", Color) = (0.3, 0.5, 0.8, 1)
        
        _CornerRadius ("Corner Radius", Range(0, 0.5)) = 0.12
        _BorderWidth ("Border Width", Range(0, 0.1)) = 0.02
        _GlowIntensity ("Glow Intensity", Range(0, 2)) = 0.8
        _HexScale ("Hex Pattern Scale", Range(1, 20)) = 8.0
        _ShimmerSpeed ("Shimmer Speed", Range(0, 5)) = 2.0
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
            
            float _FillAmount, _IsRecharging, _IsDepleted;
            float4 _ColorFull, _ColorMid, _ColorLow, _ColorRecharge;
            float4 _BackgroundColor, _BorderColor;
            float _CornerRadius, _BorderWidth, _GlowIntensity, _HexScale, _ShimmerSpeed;
            
            float sdRoundedBox(float2 p, float2 b, float r)
            {
                float2 q = abs(p) - b + r;
                return min(max(q.x, q.y), 0.0) + length(max(q, 0.0)) - r;
            }
            
            // Hexagon pattern
            float hexPattern(float2 uv)
            {
                float2 p = uv * _HexScale;
                float2 h = float2(1.0, 1.732);
                float2 a = fmod(p, h) - h * 0.5;
                float2 b = fmod(p + h * 0.5, h) - h * 0.5;
                return min(dot(a, a), dot(b, b));
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
                
                // Color gradient
                float4 fillColor = lerp(_ColorLow, lerp(_ColorMid, _ColorFull, saturate((_FillAmount - 0.5) * 2.0)), saturate(_FillAmount * 2.0));
                
                // Hex pattern overlay
                float hex = hexPattern(uv);
                fillColor.rgb += hex * 0.1;
                
                // Recharge shimmer
                float shimmer = sin((uv.x - _Time.y * _ShimmerSpeed) * 10.0) * 0.5 + 0.5;
                fillColor.rgb += shimmer * _IsRecharging * 0.3 * _ColorRecharge.rgb;
                
                // Compose
                float4 finalColor = float4(0, 0, 0, 0);
                finalColor = lerp(finalColor, _BackgroundColor, innerMask * (1.0 - fillMask) * _BackgroundColor.a);
                finalColor = lerp(finalColor, fillColor, fillMask);
                
                // Glow
                float glow = saturate(1.0 - distOuter * 6.0) * _GlowIntensity * 0.4 * _FillAmount;
                finalColor.rgb += glow * fillColor.rgb;
                
                finalColor = lerp(finalColor, _BorderColor, borderMask);
                finalColor.a *= outerMask;
                
                return finalColor;
            }
            ENDCG
        }
    }
    FallBack "UI/Default"
}
